using Dalamud.Bindings.ImGui;
using Linkpearl.Geometry;
using Linkpearl.Input;

namespace Linkpearl.Canvas.Input;

public sealed class DalamudInputProbe : IInputProbe
{
    private readonly HashSet<(Vector2 Min, Vector2 Max)> claimed = new();

    public bool Live { get; set; } = true;

    public Vector2 Pointer => ImGui.GetMousePos();

    public Vector2 PointerDelta => ImGui.GetIO().MouseDelta;

    public float ScrollDelta => Live ? ImGui.GetIO().MouseWheel : 0f;

    public void BeginFrame() => claimed.Clear();

    // True when some other ImGui window (Penumbra, etc.) is the one under the cursor.
    public static bool OtherWindowAbove()
    {
        var any = ImGui.IsWindowHovered(ImGuiHoveredFlags.AnyWindow);
        var self = ImGui.IsWindowHovered(ImGuiHoveredFlags.RootAndChildWindows |
            ImGuiHoveredFlags.AllowWhenBlockedByActiveItem);
        return any && !self;
    }

    public bool IsHovering(Rect area) =>
        Live && ImGui.IsMouseHoveringRect(area.Min, area.Max, false);

    public bool WasPressed(Rect area, PointerButton button = PointerButton.Primary) =>
        IsHovering(area) && ImGui.IsMouseClicked(ToImGuiButton(button));

    public bool WasReleased(Rect area, PointerButton button = PointerButton.Primary) =>
        IsHovering(area) && ImGui.IsMouseReleased(ToImGuiButton(button));

    public bool IsHeld(PointerButton button = PointerButton.Primary) =>
        Live && ImGui.IsMouseDown(ToImGuiButton(button));

    public bool WasClicked(Rect area, PointerButton button = PointerButton.Primary) => WasReleased(area, button);

    public bool ConsumeClick(Rect area, PointerButton button = PointerButton.Primary)
    {
        if (IsClaimed(area) || PointerOnClaim())
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

    public bool PressedInside(Rect area, PointerButton button = PointerButton.Primary)
    {
        if (area.Width < 1f || area.Height < 1f || PointerOnClaim() || !area.Contains(Pointer))
        {
            return false;
        }

        if (!ImGui.IsMouseClicked(ToImGuiButton(button)))
        {
            return false;
        }

        Claim(area);
        return true;
    }

    public void Claim(Rect area) => claimed.Add((area.Min, area.Max));

    public bool IsClaimed(Rect area) => claimed.Contains((area.Min, area.Max));

    private bool PointerOnClaim()
    {
        var pointer = Pointer;
        foreach (var (min, max) in claimed)
        {
            if (pointer.X >= min.X && pointer.X < max.X && pointer.Y >= min.Y && pointer.Y < max.Y)
            {
                return true;
            }
        }

        return false;
    }

    private static ImGuiMouseButton ToImGuiButton(PointerButton button) => button switch
    {
        PointerButton.Secondary => ImGuiMouseButton.Right,
        PointerButton.Middle => ImGuiMouseButton.Middle,
        _ => ImGuiMouseButton.Left,
    };
}
