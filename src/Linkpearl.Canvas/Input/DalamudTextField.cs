using Dalamud.Bindings.ImGui;
using Linkpearl.Geometry;
using Linkpearl.Input;

namespace Linkpearl.Canvas.Input;

public sealed class DalamudTextField : ITextField
{
    public string Draw(string id, Rect area, string value, string placeholder)
    {
        ImGui.SetCursorScreenPos(area.Min);
        ImGui.SetNextItemWidth(area.Width);
        var current = value;
        ImGui.InputTextWithHint($"##{id}", placeholder, ref current, 128);
        return current;
    }
}
