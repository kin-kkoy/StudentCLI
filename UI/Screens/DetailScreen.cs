using Spectre.Console;
using Spectre.Console.Rendering;

namespace CanvasMail.UI.Screens;


// An extra key a detail screen offers, e.g. 's' = star this email
public sealed record DetailAction(char Key, string Hint, Func<App, Task> Run);


/*  Generic "read one thing" screen: title, a few label/value lines, then a scrollable body.
    Used for emails, announcements and assignments, so they all look and scroll the same.
*/
public sealed class DetailScreen : IScreen
{
    private readonly string _kind;
    private readonly string _heading;
    private readonly IReadOnlyList<(string Label, string Value)> _meta;
    private readonly string _body;
    private readonly Color _accent;
    private readonly IReadOnlyList<DetailAction> _actions;

    private int _scroll;
    private int _visibleLines = 10;
    private int _totalLines;
    // Wrapping is the slow part, so it's only redone when the width changes
    private List<string> _wrappedBody = [];
    private int _wrappedWidth = -1;

    public string Title => _kind;

    public string KeyHints
    {
        get
        {
            string actions = string.Concat(_actions.Select(a => $" · {a.Key} {a.Hint}"));
            string position = _totalLines > _visibleLines
                ? $" · {Math.Min(100, (_scroll + _visibleLines) * 100 / _totalLines)}%"
                : "";
            return $"↑↓ scroll · PgUp/PgDn page{actions} · Esc back{position}";
        }
    }


    public DetailScreen(string kind, string heading, IReadOnlyList<(string Label, string Value)> meta,
                        string body, Color accent, IReadOnlyList<DetailAction>? actions = null)
    {
        _kind = kind;
        _heading = TextUtil.OneLine(heading);
        _meta = meta;
        _body = string.IsNullOrWhiteSpace(body) ? "(no content)" : body;
        _accent = accent;
        _actions = actions ?? [];
    }


    public IRenderable Render(int width, int height)
    {
        int innerWidth = width - 4;     // panel border (2) + padding (2)
        int innerHeight = height - 2;   // panel border (top + bottom)

        if (innerWidth != _wrappedWidth)
        {
            _wrappedBody = TextUtil.Wrap(_body, innerWidth - 1);
            _wrappedWidth = innerWidth;
        }

        var rows = new List<IRenderable>();

        // Heading (may wrap onto a few lines)
        List<string> headingLines = TextUtil.Wrap(_heading, innerWidth - 1).Take(3).ToList();
        foreach (string line in headingLines)
            rows.Add(new Text(line, new Style(Color.White, decoration: Decoration.Bold)));

        // "From:  Prof Tan <...>"  (one line each, cut if too long)
        int labelWidth = _meta.Count == 0 ? 0 : _meta.Max(m => m.Label.Length) + 2;
        foreach (var (label, value) in _meta)
        {
            rows.Add(new Paragraph()
                .Append(label.PadRight(labelWidth), new Style(Theme.Muted))
                .Append(TextUtil.Truncate(TextUtil.OneLine(value), innerWidth - labelWidth - 1)));
        }

        rows.Add(new Rule { Style = new Style(Theme.Muted) });

        // Body: only the slice of lines that fits
        _visibleLines = Math.Max(1, innerHeight - rows.Count);
        _totalLines = _wrappedBody.Count;
        _scroll = Math.Clamp(_scroll, 0, Math.Max(0, _totalLines - _visibleLines));

        foreach (string line in _wrappedBody.Skip(_scroll).Take(_visibleLines))
            rows.Add(new Text(line));

        return new Panel(new Rows(rows))
        {
            Header = new PanelHeader($" {_kind} "),
            Border = BoxBorder.Rounded,
            BorderStyle = new Style(_accent),
            Expand = true
        };
    }

    public async Task<bool> HandleKeyAsync(ConsoleKeyInfo key, App app)
    {
        if (Keys.Up(key)) _scroll--;
        else if (Keys.Down(key)) _scroll++;
        else if (Keys.PageUp(key)) _scroll -= _visibleLines;
        else if (Keys.PageDown(key)) _scroll += _visibleLines;
        else if (Keys.Top(key)) _scroll = 0;
        else if (Keys.Bottom(key)) _scroll = int.MaxValue;     // clamped on the next Render
        else if (Keys.Left(key)) app.Pop();
        else
        {
            DetailAction? action = _actions.FirstOrDefault(a => a.Key == key.KeyChar);
            if (action is null) return false;
            await action.Run(app);
        }

        // Clamp here too, so holding ↓ at the bottom doesn't build up "invisible" scroll
        _scroll = Math.Clamp(_scroll, 0, Math.Max(0, _totalLines - _visibleLines));
        return true;
    }
}
