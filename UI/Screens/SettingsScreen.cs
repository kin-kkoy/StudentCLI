using System.Globalization;
using Spectre.Console;
using Spectre.Console.Rendering;

using CanvasMail.Controllers;

namespace CanvasMail.UI.Screens;


/*  Settings you can change without touching the code (saved to ~/.config/TerminalHub/settings.json):
      - Semester start date: which courses count as "this semester" (the `a` courses list)
      - Sign in to Google again: delete the saved Google login and log in fresh
*/
public sealed class SettingsScreen : IScreen
{
    // One row in the list: what it's called, its current value, and what Enter does
    private sealed record Setting(string Name, Func<string> Value, Func<App, Task> Edit);

    private readonly HubController _hub;
    private readonly SelectableList<Setting> _list;

    public string Title => "Settings";
    public string KeyHints => "↑↓ move · ⏎ change · Esc back";


    public SettingsScreen(HubController hub)
    {
        _hub = hub;
        _list = new SelectableList<Setting>(
        [
            new("Semester start date", () => $"{_hub.SemesterStart:MMM d, yyyy} · courses created on/after this count as this semester", EditSemesterStartAsync),
            new("Sign in to Google again", () => "Deletes the saved Google login and opens the browser to log in", SignInAgainAsync)
        ],
        setting => new ListLine(setting.Name, Sub: [new(setting.Value(), new Style(Theme.Muted))]));
    }


    public IRenderable Render(int width, int height) =>
        new Panel(_list.Render(width - 4, height - 2, focused: true, "No settings."))
        {
            Header = new PanelHeader(" Settings "),
            Border = BoxBorder.Rounded,
            BorderStyle = new Style(Color.White),
            Expand = true
        };

    public async Task<bool> HandleKeyAsync(ConsoleKeyInfo key, App app)
    {
        if (_list.HandleKey(key)) return true;

        if (Keys.Enter(key) && _list.Current is Setting setting)
        {
            await setting.Edit(app);
            return true;
        }
        if (Keys.Left(key))
        {
            app.Pop();
            return true;
        }
        return false;
    }


    // ACTIONS ================================================================================

    private async Task EditSemesterStartAsync(App app)
    {
        string? typed = await app.PromptAsync("Semester start (YYYY-MM-DD):", _hub.SemesterStart.ToString("yyyy-MM-dd"));
        if (typed is null) return;      // Esc = cancelled

        if (!DateTime.TryParseExact(typed, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime date))
        {
            app.SetStatus($"\"{typed}\" isn't a date like 2026-08-01. Nothing changed.", StatusKind.Error);
            return;
        }

        _hub.SemesterStart = date;
        bool saved = (AppSettings.Load() with { SemesterStart = date }).Save();
        app.SetStatus(saved
            ? $"Semester start set to {date:MMM d, yyyy}"
            : $"Semester start set to {date:MMM d, yyyy} for now, but it couldn't be saved for next time.",
            saved ? StatusKind.Success : StatusKind.Warning);
    }

    private async Task SignInAgainAsync(App app)
    {
        if (!await app.ConfirmAsync("Delete the saved Google login and sign in again?")) return;
        app.RequestGoogleSignIn(userAsked: true);
    }
}
