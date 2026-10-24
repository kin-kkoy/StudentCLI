using Spectre.Console;
using Spectre.Console.Rendering;

namespace CanvasMail.UI.Screens;


// A full-screen list where Enter opens the selected item (courses, announcements, ...)
public sealed class ListScreen<T> : IScreen
{
    private readonly SelectableList<T> _list;
    private readonly Func<T, App, Task> _open;
    private readonly Color _accent;
    private readonly string _emptyText;

    public string Title { get; }
    public string KeyHints => "↑↓ move · ⏎ open · Esc back";


    public ListScreen(string title, IEnumerable<T> items, Func<T, ListLine> format, Func<T, App, Task> open,
                      Color accent, string emptyText = "Nothing here.")
    {
        Title = title;
        _list = new SelectableList<T>(items, format);
        _open = open;
        _accent = accent;
        _emptyText = emptyText;
    }


    public IRenderable Render(int width, int height)
    {
        string counter = _list.Count > 0 ? $" ({_list.Selected + 1}/{_list.Count})" : "";

        return new Panel(_list.Render(width - 4, height - 2, focused: true, _emptyText))
        {
            Header = new PanelHeader($" {Title}{counter} "),
            Border = BoxBorder.Rounded,
            BorderStyle = new Style(_accent),
            Expand = true
        };
    }

    public async Task<bool> HandleKeyAsync(ConsoleKeyInfo key, App app)
    {
        if (_list.HandleKey(key)) return true;

        if (Keys.Enter(key) && _list.Current is T item)
        {
            await _open(item, app);
            return true;
        }
        if (Keys.Left(key))
        {
            app.Pop();
            return true;
        }
        return false;
    }
}
