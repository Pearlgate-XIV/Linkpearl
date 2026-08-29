using Linkpearl.Geometry;

namespace Linkpearl.Input;

// The one widget in this codebase that needs real OS-level keyboard capture (IME, cursor
// blink, selection) rather than custom hit-testing over draw-list paint like everything else.
// Deliberately narrow: one line, one hint, returns the current value every frame.
public interface ITextField
{
    string Draw(string id, Rect area, string value, string placeholder);

    string Draw(string id, Rect area, string value, string placeholder, int maxLength, out bool submitted);

    void Release();
}
