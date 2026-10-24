using System.Runtime.InteropServices;
using Spectre.Console;
using Spectre.Console.Rendering;

using CanvasMail.Controllers;
using CanvasMail.UI.Screens;

namespace CanvasMail.UI;


/*  ARCHITECTURE:
    ┌───────────────────────────────────────────────────────────┐
    │ App = the "window manager" of the terminal UI             │
    │   - owns the screen stack (Briefing -> List -> Detail)    │
    │   - main loop: wait for key/resize -> handle -> redraw    │
    │   - draws the top bar + bottom bar around the screen      │
    │   - shared helpers: loading spinner, y/n confirm, errors  │
    ├───────────────────────────────────────────────────────────┤
    │ Redraw triggers:                                          │
    │   - any key press                                         │
    │   - Ctrl+L (manual redraw)                                │
    │   - background work finished (e.g. fresh briefing)        │
    │   - SIGWINCH (Linux tells us the window was resized)      │
    │   - window size changed (backup check in case a signal    │
    │     arrives before the new size has settled)             │
    └───────────────────────────────────────────────────────────┘
*/

public enum StatusKind { Info, Success, Warning, Error }

public sealed class App
{
    private const int MinWidth = 50;
    private const int MinHeight = 14;
    private static readonly string[] SpinnerFrames = ["⠋", "⠙", "⠹", "⠸", "⠼", "⠴", "⠦", "⠧", "⠇", "⠏"];

    private readonly Stack<IScreen> _screens = new();
    private readonly ColorSystemSupport _colors;
    private bool _running = true;

    // Set to 1 by the SIGWINCH handler, which runs on ANOTHER thread.
    //  Interlocked = a thread-safe way to set/read it. The main loop does the actual redraw.
    private int _resizeSignal;
    private (int Width, int Height) _lastSize;

    // What the bottom bar is currently showing (only one at a time, in this priority order)
    private string? _inputLabel;            // text prompt (e.g. "Semester start date:") + what's typed so far
    private string _inputText = "";
    private string? _confirmPrompt;
    private string? _loadingMessage;
    private int _spinnerFrame;
    private string? _status;
    private StatusKind _statusKind;

    // Work running WITHOUT blocking the keyboard (e.g. refreshing the briefing while you browse the saved one)
    private sealed record BackgroundJob(string Message, Task Work, Action OnFinished);
    private readonly List<BackgroundJob> _backgroundJobs = [];

    // Google sign-in again: requested anywhere, carried out by the main loop (it has to leave full screen for it)
    private HomeScreen? _home;
    private string? _signInReason;          // non-null = a sign-in is waiting to happen
    private bool _autoSignInTried;          // automatic retry only once per run, so a broken setup can't loop

    public HubController Hub { get; }


    public App(HubController hub)
    {
        Hub = hub;
        // Ask Spectre what the REAL terminal supports (Kitty = truecolor), so our off-screen frames match it
        _colors = (ColorSystemSupport)(int)AnsiConsole.Profile.Capabilities.ColorSystem;
    }


    // MAIN LOOP ===========================================================================

    public async Task RunAsync()
    {
        PosixSignalRegistration? resizeRegistration = null;
        if (!OperatingSystem.IsWindows())
            resizeRegistration = PosixSignalRegistration.Create(PosixSignal.SIGWINCH, _ => Interlocked.Exchange(ref _resizeSignal, 1));

        EnterFullScreen();
        try
        {
            Hub.SemesterStart = AppSettings.Load().SemesterStart;

            _home = new HomeScreen(Hub);
            _screens.Push(_home);
            _home.Start(this);      // shows the saved briefing instantly + refreshes in the background

            while (_running)
            {
                Render();

                if (_signInReason is not null)
                {
                    await SignInToGoogleAgainAsync();
                    continue;
                }

                ConsoleKeyInfo? key = await ReadKeyAsync();
                if (key is null) continue;          // resize -> loop around and redraw at the new size

                await HandleKeyAsync(key.Value);
            }
        }
        finally
        {
            // ALWAYS give the terminal back the way we found it, even if something crashed
            LeaveFullScreen();
            resizeRegistration?.Dispose();
        }
    }

    // Returns a key, or null when the screen needs a redraw (resize, background work finished, spinner tick)
    private async Task<ConsoleKeyInfo?> ReadKeyAsync()
    {
        long waitStarted = Environment.TickCount64;
        while (true)
        {
            if (Console.KeyAvailable)
                return Console.ReadKey(intercept: true);

            bool resized = Interlocked.Exchange(ref _resizeSignal, 0) == 1;
            if (resized || (Console.WindowWidth, Console.WindowHeight) != _lastSize)
                return null;

            if (_backgroundJobs.Count > 0)
            {
                if (_backgroundJobs.Any(job => job.Work.IsCompleted)) return null;
                if (Environment.TickCount64 - waitStarted >= 80)
                {
                    _spinnerFrame++;        // keep the bottom-bar spinner moving
                    return null;
                }
            }

            await Task.Delay(25);
        }
    }

    private async Task HandleKeyAsync(ConsoleKeyInfo key)
    {
        if (Keys.CtrlC(key)) { Quit(); return; }
        if (Keys.CtrlL(key)) return;               // nothing to do: the loop redraws after every key anyway

        _status = null;                             // any key dismisses the last status message

        if (await _screens.Peek().HandleKeyAsync(key, this)) return;

        // Global keys, only if the screen didn't use the key itself
        if (key.KeyChar == 'q') Quit();
        else if (key.Key is ConsoleKey.Escape or ConsoleKey.Backspace) Pop();
    }


    // SCREEN STACK =========================================================================

    public void Push(IScreen screen) => _screens.Push(screen);

    public void Pop()
    {
        if (_screens.Count > 1) _screens.Pop();     // never pop the briefing (home)
    }

    public void Quit() => _running = false;


    // SHARED HELPERS (used by screens) =====================================================

    public void SetStatus(string message, StatusKind kind = StatusKind.Info)
    {
        _status = message;
        _statusKind = kind;
    }

    // Runs a network call while showing a spinner (and still redrawing on resize).
    //  Errors become a friendly red message in the bottom bar instead of crashing the app.
    public async Task<(bool Ok, T Value)> LoadAsync<T>(string message, Func<Task<T>> work)
    {
        _loadingMessage = message;
        try
        {
            Task<T> task = work();
            while (!task.IsCompleted)
            {
                Render();
                await Task.WhenAny(task, Task.Delay(80));
                _spinnerFrame++;
            }
            return (true, await task);
        }
        catch (Exception ex)
        {
            if (IsGoogleLoginExpired(ex)) RequestGoogleSignIn();
            else SetStatus(FriendlyError(ex), StatusKind.Error);
            return (false, default!);
        }
        finally
        {
            _loadingMessage = null;
        }
    }

    // Starts work and returns IMMEDIATELY; the user can keep using the app.
    //  When the work finishes, onSuccess/onError run on the main loop (never on another thread), then the screen redraws.
    public void RunInBackground<T>(string message, Func<Task<T>> work, Action<T> onSuccess, Action<Exception> onError)
    {
        Task<T> task;
        try
        {
            task = work();
        }
        catch (Exception ex)
        {
            onError(ex);
            return;
        }

        _backgroundJobs.Add(new BackgroundJob(message, task, () =>
        {
            if (task.IsCompletedSuccessfully) onSuccess(task.Result);
            else if (task.IsCanceled) onError(new TaskCanceledException());
            else onError(task.Exception!.InnerException ?? task.Exception);
        }));
    }

    private void FinishCompletedBackgroundJobs()
    {
        foreach (BackgroundJob job in _backgroundJobs.Where(job => job.Work.IsCompleted).ToList())
        {
            _backgroundJobs.Remove(job);
            job.OnFinished();
        }
    }

    // Asks for a line of text in the bottom bar. Returns null if cancelled (Esc).
    public async Task<string?> PromptAsync(string label, string initialText = "")
    {
        _inputLabel = label;
        _inputText = initialText;
        try
        {
            while (true)
            {
                Render();
                ConsoleKeyInfo? key = await ReadKeyAsync();
                if (key is null) continue;

                ConsoleKeyInfo k = key.Value;
                if (k.Key == ConsoleKey.Enter) return _inputText.Trim();
                if (k.Key == ConsoleKey.Escape || Keys.CtrlC(k)) return null;

                if (k.Key == ConsoleKey.Backspace)
                {
                    if (_inputText.Length > 0) _inputText = _inputText[..^1];
                }
                else if (!char.IsControl(k.KeyChar))
                {
                    _inputText += k.KeyChar;
                }
            }
        }
        finally
        {
            _inputLabel = null;
        }
    }


    // GOOGLE SIGN-IN AGAIN ==================================================================

    // Google rejected the saved login: "invalid_grant" = expired or revoked (password change, admin policy,
    //  or the 7-day limit that only applies to External apps in Testing mode)
    public static bool IsGoogleLoginExpired(Exception ex) =>
        ex is Google.Apis.Auth.OAuth2.Responses.TokenResponseException { Error.Error: "invalid_grant" };

    // userAsked = from Settings (always allowed). Automatic = only once per run.
    public void RequestGoogleSignIn(bool userAsked = false)
    {
        if (!userAsked && _autoSignInTried)
        {
            SetStatus("Google still rejects the login. Try Settings › Sign in to Google again.", StatusKind.Error);
            return;
        }
        _signInReason = userAsked
            ? "Signing in to Google again."
            : "Your Google login expired or was signed out.";
    }

    // Leaves full screen so the browser login has the terminal, deletes the old login, signs in, comes back
    private async Task SignInToGoogleAgainAsync()
    {
        string reason = _signInReason!;
        _signInReason = null;
        _autoSignInTried = true;

        LeaveFullScreen();
        Console.WriteLine(reason);
        Console.WriteLine("Opening your browser to sign in... (Ctrl+C to cancel)");
        try
        {
            await Hub.ReconnectGmailAsync();
            SetStatus("Signed in to Google again", StatusKind.Success);
        }
        catch (Exception ex)
        {
            SetStatus($"Google sign-in failed: {FriendlyError(ex)}", StatusKind.Error);
        }
        finally
        {
            EnterFullScreen();
        }

        _home?.Refresh(this, announce: false);
    }

    // Shows "<prompt> (y/n)" in the bottom bar and waits for an answer
    public async Task<bool> ConfirmAsync(string prompt)
    {
        _confirmPrompt = prompt;
        try
        {
            while (true)
            {
                Render();
                ConsoleKeyInfo? key = await ReadKeyAsync();
                if (key is null) continue;

                if (key.Value.KeyChar is 'y' or 'Y') return true;
                if (key.Value.KeyChar is 'n' or 'N' || key.Value.Key == ConsoleKey.Escape || Keys.CtrlC(key.Value)) return false;
            }
        }
        finally
        {
            _confirmPrompt = null;
        }
    }

    // Public so screens can phrase errors the same way (e.g. the briefing's per-panel errors)
    public static string FriendlyError(Exception ex) => ex switch
    {
        HttpRequestException { StatusCode: not null } http => $"Request failed: {(int)http.StatusCode} {http.StatusCode}",
        HttpRequestException => "Couldn't reach the server. Check your connection.",
        // Google logins in "Testing" mode expire after 7 days -> the saved token stops working
        Google.Apis.Auth.OAuth2.Responses.TokenResponseException { Error.Error: "invalid_grant" } =>
            "Google login expired.",
        Google.GoogleApiException google => $"Gmail error: {google.Error?.Message ?? google.Message}",
        TaskCanceledException => "The request timed out.",
        _ => ex.Message
    };


    // DRAWING ===============================================================================

    private void Render()
    {
        // Every redraw path goes through here, so finished background work is always picked up
        FinishCompletedBackgroundJobs();

        int width = Console.WindowWidth;
        int height = Console.WindowHeight;
        _lastSize = (width, height);
        Interlocked.Exchange(ref _resizeSignal, 0);

        // Build the whole frame first, then write it in ONE go:
        //  ESC[?2026h/l = "synchronized update" (Kitty/Foot show the frame all at once -> no flicker)
        //  ESC[H = cursor to top-left, ESC[2J = clear screen
        string frame = BuildFrame(width, height);
        Console.Out.Write("\x1b[?2026h\x1b[H\x1b[2J" + frame + "\x1b[?2026l");
    }

    // Draws into a string instead of the real terminal (so we can write it all at once)
    private string BuildFrame(int width, int height)
    {
        var buffer = new StringWriter();
        var console = AnsiConsole.Create(new AnsiConsoleSettings
        {
            Ansi = AnsiSupport.Yes,
            ColorSystem = _colors,
            Out = new AnsiConsoleOutput(buffer),
            Interactive = InteractionSupport.No
        });
        console.Profile.Width = width;
        console.Profile.Height = height;
        console.Profile.Capabilities.Unicode = true;

        if (width < MinWidth || height < MinHeight)
        {
            console.Write(new Text($"Terminal too small ({width}×{height}).\nNeed at least {MinWidth}×{MinHeight}.", new Style(Theme.Warning)));
        }
        else
        {
            IScreen screen = _screens.Peek();

            var layout = new Layout("root").SplitRows(
                new Layout("header").Size(1),
                new Layout("body"),
                new Layout("footer").Size(2));

            layout["header"].Update(TopBar(width));
            layout["body"].Update(screen.Render(width, height - 3));
            layout["footer"].Update(BottomBar(width, screen));

            console.Write(layout);
        }

        // No trailing newline: on the last row it would scroll the whole screen up by one line
        return buffer.ToString().TrimEnd('\r', '\n');
    }

    // " TerminalHub › Briefing › Email                    Fri, Oct 24 · 2:46 PM "
    private IRenderable TopBar(int width)
    {
        var bar = new Style(Color.White, Theme.HeaderBar);
        var dim = new Style(Theme.Muted, Theme.HeaderBar);

        string right = DateTime.Now.ToString("ddd, MMM d · h:mm tt") + " ";
        string crumbs = string.Join(" › ", _screens.Reverse().Select(s => s.Title));
        string left = " TerminalHub  " + TextUtil.Truncate(crumbs, width - TextUtil.Width(right) - 16);

        int gap = Math.Max(1, width - TextUtil.Width(left) - TextUtil.Width(right));

        return new Paragraph()
            .Append(" TerminalHub", new Style(Color.White, Theme.HeaderBar, Decoration.Bold))
            .Append(left[" TerminalHub".Length..], dim)
            .Append(new string(' ', gap), bar)
            .Append(right, dim);
    }

    // Line 1: confirm prompt / spinner / status message.  Line 2: key hints.
    private IRenderable BottomBar(int width, IScreen screen)
    {
        IRenderable line1;
        if (_inputLabel is not null)
            line1 = new Paragraph()
                .Append($" {_inputLabel} ", new Style(Theme.Warning, decoration: Decoration.Bold))
                .Append(TextUtil.Truncate(_inputText, width - TextUtil.Width(_inputLabel) - 6), new Style(Color.White))
                .Append("▏", new Style(Color.White));
        else if (_confirmPrompt is not null)
            line1 = new Text(TextUtil.Truncate($" {_confirmPrompt}  [y] yes  [n] no", width - 1), new Style(Theme.Warning, decoration: Decoration.Bold));
        else if (_loadingMessage is not null)
            line1 = new Text($" {SpinnerFrames[_spinnerFrame % SpinnerFrames.Length]} {TextUtil.Truncate(_loadingMessage, width - 5)}", new Style(Theme.MailAccent));
        else if (_status is not null)
            line1 = new Text(TextUtil.Truncate(" " + StatusIcon(_statusKind) + " " + _status, width - 1), new Style(StatusColor(_statusKind)));
        else if (_backgroundJobs.Count > 0)
            line1 = new Text($" {SpinnerFrames[_spinnerFrame % SpinnerFrames.Length]} {TextUtil.Truncate(_backgroundJobs[0].Message, width - 5)}", new Style(Theme.Muted));
        else
            line1 = new Text("");

        string hints = $" {screen.KeyHints} · ^L redraw · q quit";
        var line2 = new Text(TextUtil.Truncate(hints, width - 1), new Style(Theme.Muted));

        return new Rows(line1, line2);
    }

    private static string StatusIcon(StatusKind kind) => kind switch
    {
        StatusKind.Success => "✓",
        StatusKind.Warning => "!",
        StatusKind.Error => "✗",
        _ => "·"
    };

    private static Color StatusColor(StatusKind kind) => kind switch
    {
        StatusKind.Success => Theme.Success,
        StatusKind.Warning => Theme.Warning,
        StatusKind.Error => Theme.Error,
        _ => Color.White
    };


    // TERMINAL SETUP =========================================================================

    private static void EnterFullScreen()
    {
        Console.TreatControlCAsInput = true;        // Ctrl+C arrives as a key, so we can quit cleanly
        // ESC[?1049h = switch to the "alternate screen" (like vim/htop: your shell history comes back on exit)
        // ESC[?25l   = hide the cursor
        Console.Out.Write("\x1b[?1049h\x1b[?25l");
    }

    private static void LeaveFullScreen()
    {
        Console.Out.Write("\x1b[?25h\x1b[?1049l");
        Console.TreatControlCAsInput = false;
    }
}
