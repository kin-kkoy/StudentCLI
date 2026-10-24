using Spectre.Console;
using Spectre.Console.Rendering;

namespace CanvasMail.UI;


// One piece of styled text inside a line
public readonly record struct Span(string Text, Style Style);

// What one list item looks like: a main line (optional colored tag in front) + a dim second line
public sealed record ListLine(string Main, string? Tag = null, Color? TagColor = null, IReadOnlyList<Span>? Sub = null);


/*  A scrollable list with a selected item. Used by the briefing panels and the list screens.
    Every item takes 2 lines, so "how many fit" is simply height / 2.
*/
public sealed class SelectableList<T>
{
    private const int LinesPerItem = 2;

    private readonly List<T> _items = [];
    private readonly Func<T, ListLine> _format;
    private readonly Func<T, string>? _key;     // identifies an item, so the selection survives a refresh
    private int _top;           // index of the first visible item (for scrolling)
    private int _pageSize = 5;  // how many items fit, remembered from the last Render (for PageUp/PageDown)

    public int Selected { get; private set; }
    public int Count => _items.Count;
    public T? Current => _items.Count > 0 ? _items[Selected] : default;


    public SelectableList(IEnumerable<T> items, Func<T, ListLine> format, Func<T, string>? key = null)
    {
        _format = format;
        _key = key;
        SetItems(items);
    }

    public void SetItems(IEnumerable<T> items)
    {
        // Remember what was selected, so fresh data doesn't make the cursor jump to a different item
        string? selectedKey = _key is not null && _items.Count > 0 ? _key(_items[Selected]) : null;

        _items.Clear();
        _items.AddRange(items);

        int sameItem = selectedKey is null ? -1 : _items.FindIndex(item => _key!(item) == selectedKey);
        Selected = sameItem >= 0 ? sameItem : Math.Clamp(Selected, 0, Math.Max(0, _items.Count - 1));
    }

    public IReadOnlyList<T> Items => _items;

    public void Remove(T item)
    {
        _items.Remove(item);
        Selected = Math.Clamp(Selected, 0, Math.Max(0, _items.Count - 1));
    }

    // Returns true if the key was a list-movement key
    public bool HandleKey(ConsoleKeyInfo key)
    {
        int before = Selected;

        if (Keys.Up(key)) Selected--;
        else if (Keys.Down(key)) Selected++;
        else if (Keys.PageUp(key)) Selected -= _pageSize;
        else if (Keys.PageDown(key)) Selected += _pageSize;
        else if (Keys.Top(key)) Selected = 0;
        else if (Keys.Bottom(key)) Selected = _items.Count - 1;
        else return false;

        Selected = Math.Clamp(Selected, 0, Math.Max(0, _items.Count - 1));
        return true;
    }

    public IRenderable Render(int width, int height, bool focused, string emptyText)
    {
        if (_items.Count == 0)
            return new Text(emptyText, new Style(Theme.Muted));

        _pageSize = Math.Max(1, height / LinesPerItem);

        // Scroll just enough to keep the selected item visible
        if (Selected < _top) _top = Selected;
        if (Selected >= _top + _pageSize) _top = Selected - _pageSize + 1;
        _top = Math.Clamp(_top, 0, Math.Max(0, _items.Count - _pageSize));

        var lines = new List<IRenderable>();
        for (int i = _top; i < Math.Min(_items.Count, _top + _pageSize); i++)
        {
            bool isSelected = i == Selected;
            Color? background = isSelected && focused ? Theme.Highlight : null;
            ListLine item = _format(_items[i]);

            // Line 1: "▸ [TAG] Main text"
            var main = new List<Span>
            {
                new(isSelected ? "▸ " : "  ", new Style(isSelected ? Color.White : Theme.Muted, background, Decoration.Bold))
            };
            if (item.Tag is not null)
                main.Add(new($"{item.Tag} ", new Style(item.TagColor ?? Theme.Muted, background, Decoration.Bold)));
            main.Add(new(item.Main, new Style(Color.White, background, isSelected ? Decoration.Bold : Decoration.None)));

            // Line 2: indented, dim details
            var sub = new List<Span> { new("    ", new Style(background: background)) };
            foreach (Span span in item.Sub ?? [])
                sub.Add(span with { Style = new Style(span.Style.Foreground, background, span.Style.Decoration) });

            lines.Add(FitLine(main, width, background));
            lines.Add(FitLine(sub, width, background));
        }
        return new Rows(lines);
    }

    // Glue spans into exactly one line: cut what doesn't fit, pad the rest (so the highlight spans the full row).
    //  Uses width - 1 as a safety margin so a slightly mis-measured emoji can't wrap onto a second line.
    private static Paragraph FitLine(List<Span> spans, int width, Color? background)
    {
        int room = width - 1;
        var paragraph = new Paragraph();

        foreach (Span span in spans)
        {
            if (room <= 0) break;
            string text = TextUtil.Truncate(span.Text, room);
            paragraph.Append(text, span.Style);
            room -= TextUtil.Width(text);
        }
        if (room > 0) paragraph.Append(new string(' ', room), new Style(background: background));

        return paragraph;
    }
}
