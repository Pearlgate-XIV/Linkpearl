using Linkpearl.Geometry;
using Linkpearl.Modules;
using Linkpearl.Painting;
using Linkpearl.Platform;

namespace Linkpearl.Input;

// The one widget in this codebase that needs real OS-level keyboard capture (IME, cursor
// blink, selection) rather than custom hit-testing over draw-list paint like everything else.
// Draw is one line. Write is a wrapped compose box.
public interface ITextField
{
    string Draw(string id, Rect area, string value, string placeholder);

    string Draw(string id, Rect area, string value, string placeholder, int maxLength, out bool submitted);

    string Draw(string id, Rect area, string value, string placeholder, int maxLength, out bool submitted,
        bool retainFocus);

    string Draw(string id, Rect area, string value, string placeholder, int maxLength, out bool submitted,
        bool retainFocus, bool secret) =>
        Draw(id, area, value, placeholder, maxLength, out submitted, retainFocus);

    string Draw(string id, Rect area, string value, string placeholder, bool secret) =>
        Draw(id, area, value, placeholder, 128, out _, false, secret);

    string Write(string id, Rect area, string value, string placeholder, int maxLength);

    int Pick(string id, Rect area, IReadOnlyList<string> labels, int selected);

    int Combo(string id, Rect area, IReadOnlyList<string> labels, int selected);

    bool Owns(string id);

    void Focus(string id);

    string Insert(string id, string value, string text);

    void Release();

    string ClipboardText();

    void PutClipboard(string text)
    {
    }

    bool TakingKeys => false;

    void Dress(IPaintSurface paint, ITextPainter text, ITextureSource textures, HostPaths paths)
    {
    }
}
