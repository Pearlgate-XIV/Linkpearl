using Dalamud.Bindings.ImGui;
using Linkpearl.Geometry;
using Linkpearl.Input;

namespace Linkpearl.Canvas.Input;

public sealed class DalamudInputProbe : IInputProbe
{
    private readonly HashSet<(Vector2 Min, Vector2 Max)> claimed = new();

    public Vector2 Pointer => ImGui.GetMousePos();

    public Vector2 PointerDelta => ImGui.GetIO().MouseDelta;

    public float ScrollDelta => ImGui.GetIO().MouseWheel;

    public void BeginFrame() => claimed.Clear();

    public bool IsHovering(Rect area) => ImGui.IsMouseHoveringRect(area.Min, area.Max);

    public bool WasPressed(Rect area, PointerButton button = PointerButton.Primary) =>
        IsHovering(area) && ImGui.IsMouseClicked(ToImGuiButton(button));

    public bool WasReleased(Rect area, PointerButton button = PointerButton.Primary) =>
        IsHovering(area) && ImGui.IsMouseReleased(ToImGuiButton(button));

    public bool IsHeld(PointerButton button = PointerButton.Primary) => ImGui.IsMouseDown(ToImGuiButton(button));

    public bool WasClicked(Rect area, PointerButton button = PointerButton.Primary) => WasReleased(area, button);

    public bool ConsumeClick(Rect area, PointerButton button = PointerButton.Primary)
    {
        if (IsClaimed(area))
        {
            return false;
        }

        if (!WasClicked(area, button))
        {
            return false;
        }

        Claim(area);
        return true;
    }

    public void Claim(Rect area) => claimed.Add((area.Min, area.Max));

    public bool IsClaimed(Rect area) => claimed.Contains((area.Min, area.Max));

    private static ImGuiMouseButton ToImGuiButton(PointerButton button) => button switch
    {
        PointerButton.Secondary => ImGuiMouseButton.Right,
        PointerButton.Middle => ImGuiMouseButton.Middle,
        _ => ImGuiMouseButton.Left,
    };
}
