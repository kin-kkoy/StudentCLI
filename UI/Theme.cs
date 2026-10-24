using System.Globalization;
using System.Text.RegularExpressions;
using Spectre.Console;
using Spectre.Console.Rendering;

namespace CanvasMail.UI;


// Colors + "how should this value look on screen" helpers, shared by every screen.
public static class Theme
{
    public static readonly Color CanvasAccent = Color.Salmon1;
    public static readonly Color MailAccent = Color.SkyBlue1;
    public static readonly Color Muted = Color.Grey50;
    public static readonly Color Highlight = Color.Grey23;     // background of the selected row
    public static readonly Color HeaderBar = Color.Grey15;     // background of the top bar
    public static readonly Color Success = Color.Green3;
    public static readonly Color Error = Color.Red1;
    public static readonly Color Warning = Color.Yellow;


    // "Overdue / Today / Tomorrow / Monday / Oct 30" + a color for how urgent it is
    public static (string Text, Color Color) DueLabel(DateTime? dueLocal, DateTime now)
    {
        if (dueLocal is null) return ("No due date", Muted);

        DateTime due = dueLocal.Value;
        DateTime today = now.Date;

        if (due < now)                        return ($"Overdue · {due:MMM d, h:mm tt}", Color.Red1);
        if (due.Date == today)                return ($"Today {due:h:mm tt}", Color.Orange1);
        if (due.Date == today.AddDays(1))     return ($"Tomorrow {due:h:mm tt}", Color.Yellow);
        if (due.Date < today.AddDays(7))      return ($"{due:dddd h:mm tt}", Color.White);
        return ($"{due:MMM d, h:mm tt}", Muted);
    }

    // Inbox-style time: "2:46 PM" today, "Yesterday", "Mon" this week, "Oct 12" older
    public static string EmailTime(DateTime? date, DateTime now)
    {
        if (date is null) return "";

        DateTime d = date.Value;
        if (d.Date == now.Date)               return d.ToString("h:mm tt");
        if (d.Date == now.Date.AddDays(-1))   return "Yesterday";
        if (d.Date > now.Date.AddDays(-7))    return d.ToString("ddd");
        return d.ToString("MMM d");
    }

    public static string LongDate(DateTime? date) =>
        date is null ? "—" : date.Value.ToString("ddd, MMM d yyyy · h:mm tt");

    // "Prof Tan <tan@school.edu>" -> "Prof Tan"
    public static string SenderName(string from)
    {
        int bracket = from.IndexOf('<');
        if (bracket < 0) return from.Trim();

        string name = from[..bracket].Trim().Trim('"');
        return name.Length > 0 ? name : from[(bracket + 1)..].TrimEnd('>').Trim();
    }

    // Canvas plannable_type -> short tag shown in the to-do list
    public static string PlannerTag(string type) => type switch
    {
        "assignment" => "ASG",
        "quiz" => "QUIZ",
        "announcement" => "ANN",
        "discussion_topic" => "DISC",
        "wiki_page" => "PAGE",
        "calendar_event" => "EVENT",
        "planner_note" => "NOTE",
        _ => type.ToUpperInvariant()
    };
}


// Text measuring/fitting. Terminal "width" is counted in CELLS, not characters:
//  an emoji like 🎮 is 1 character to C# but takes 2 cells on screen. Spectre does the measuring for us.
public static class TextUtil
{
    private static readonly IAnsiConsole Measurer = AnsiConsole.Create(new AnsiConsoleSettings
    {
        Out = new AnsiConsoleOutput(TextWriter.Null),
        Ansi = AnsiSupport.No,
        ColorSystem = ColorSystemSupport.NoColors
    });
    private static readonly RenderOptions Options = RenderOptions.Create(Measurer);
    private static readonly Dictionary<string, int> WidthCache = new();


    // Squash newlines/tabs/repeated spaces into single spaces (subjects, titles, etc.)
    public static string OneLine(string? text) =>
        string.IsNullOrWhiteSpace(text) ? "" : Regex.Replace(text, @"\s+", " ").Trim();

    public static int Width(string text) =>
        text.Length == 0 ? 0 : ((IRenderable)new Text(text)).Measure(Options, int.MaxValue).Max;

    // Cut text to fit in `maxCells`, ending with "…" if anything was cut
    public static string Truncate(string text, int maxCells)
    {
        if (maxCells <= 0) return "";
        if (Width(text) <= maxCells) return text;

        var result = new System.Text.StringBuilder();
        int used = 0;
        var elements = StringInfo.GetTextElementEnumerator(text);   // walks "user-visible characters", so emoji aren't split in half
        while (elements.MoveNext())
        {
            string element = elements.GetTextElement();
            if (!WidthCache.TryGetValue(element, out int cells))
                WidthCache[element] = cells = Width(element);

            if (used + cells > maxCells - 1) break;   // keep 1 cell for the "…"
            result.Append(element);
            used += cells;
        }
        return result.Append('…').ToString();
    }

    // Word-wrap a (possibly multi-line) body into lines that each fit in `width` cells
    public static List<string> Wrap(string text, int width)
    {
        if (width <= 0 || text.Length == 0) return [];

        var segments = ((IRenderable)new Text(text.Replace("\r", "").Replace("\t", "    "))).Render(Options, width);
        return Segment.SplitLines(segments, width)
            .Select(line => string.Concat(line.Select(segment => segment.Text)).TrimEnd())
            .ToList();
    }
}


// Key checks in one place, so every screen agrees on what "up" or "back" means (arrows + vim keys)
public static class Keys
{
    public static bool Up(ConsoleKeyInfo k) => k.Key == ConsoleKey.UpArrow || k.KeyChar == 'k';
    public static bool Down(ConsoleKeyInfo k) => k.Key == ConsoleKey.DownArrow || k.KeyChar == 'j';
    public static bool PageUp(ConsoleKeyInfo k) => k.Key == ConsoleKey.PageUp;
    public static bool PageDown(ConsoleKeyInfo k) => k.Key == ConsoleKey.PageDown || k.Key == ConsoleKey.Spacebar;
    public static bool Top(ConsoleKeyInfo k) => k.Key == ConsoleKey.Home || k.KeyChar == 'g';
    public static bool Bottom(ConsoleKeyInfo k) => k.Key == ConsoleKey.End || k.KeyChar == 'G';
    public static bool Enter(ConsoleKeyInfo k) => k.Key == ConsoleKey.Enter;
    public static bool Left(ConsoleKeyInfo k) => k.Key == ConsoleKey.LeftArrow || k.KeyChar == 'h';
    public static bool Right(ConsoleKeyInfo k) => k.Key == ConsoleKey.RightArrow || k.KeyChar == 'l';

    // Ctrl+<letter> arrives either as (Key + Control modifier) or as the raw control character
    public static bool CtrlL(ConsoleKeyInfo k) => k.KeyChar == '\f' || (k.Key == ConsoleKey.L && k.Modifiers.HasFlag(ConsoleModifiers.Control));
    public static bool CtrlC(ConsoleKeyInfo k) => k.KeyChar == '\x03' || (k.Key == ConsoleKey.C && k.Modifiers.HasFlag(ConsoleModifiers.Control));
}
