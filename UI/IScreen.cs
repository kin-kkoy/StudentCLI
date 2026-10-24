using Spectre.Console.Rendering;

namespace CanvasMail.UI;


/*  A screen = one "page" of the app (briefing, a list, an email...).
    The App keeps them in a stack: opening something pushes a screen, Esc pops it.

    The important rule: Render() only DRAWS from data the screen already has.
    It never fetches. That's what makes Ctrl+L / resize redraws instant and free.
*/
public interface IScreen
{
    // Shown in the top bar's breadcrumb, e.g. "Briefing › Email"
    string Title { get; }

    // Shown in the bottom bar, e.g. "↑↓ move · ⏎ open"
    string KeyHints { get; }

    // Draw the screen's body into exactly width × height cells
    IRenderable Render(int width, int height);

    // React to a key. Return false if this screen doesn't use that key (the App then tries its global keys)
    Task<bool> HandleKeyAsync(ConsoleKeyInfo key, App app);
}
