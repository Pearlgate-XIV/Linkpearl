using Dalamud.Bindings.ImGui;
using Linkpearl.Geometry;
using Linkpearl.Input;

namespace Linkpearl.Canvas.Input;

public sealed class DalamudInputProbe : IInputProbe
{
    // A tap is press+release without travel. The probe is constructed each frame, so
    // the gesture lives here: resize and content slides must not open what they land on.
    private const float DragSlop = 6f;
    private static readonly Vector2[] pressAt = new Vector2[3];
    private static readonly bool[] armed = new bool[3];
    private static readonly bool[] dragged = new bool[3];

    private readonly HashSet<(Vector2 Min, Vector2 Max)> claimed = new();
    private static bool copyTaken;

    public bool Live { get; set; } = true;

    public Vector2 Pointer => ImGui.GetMousePos();

    public Vector2 PointerDelta => ImGui.GetIO().MouseDelta;

    public float ScrollDelta => Live ? ImGui.GetIO().MouseWheel : 0f;

    public DalamudInputProbe() => RememberDrags();

    public void BeginFrame()
    {
        claimed.Clear();
        copyTaken = false;
        RememberDrags();
    }

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

    public bool WasClicked(Rect area, PointerButton button = PointerButton.Primary)
    {
        var index = ButtonIndex(button);
        return WasReleased(area, button) && !dragged[index];
    }

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

    public bool PointerClaimed() => PointerOnClaim();

    public bool EscapePressed() => Live && ImGui.IsKeyPressed(ImGuiKey.Escape, false);

    public bool PointerReleased(PointerButton button = PointerButton.Primary)
    {
        var index = ButtonIndex(button);
        return Live && ImGui.IsMouseReleased(ToImGuiButton(button)) && !dragged[index];
    }

    public bool CopyChord()
    {
        if (!Live || copyTaken)
        {
            return false;
        }

        var chord = ImGui.GetIO().KeyCtrl || ImGui.GetIO().KeySuper;
        if (!chord || !ImGui.IsKeyPressed(ImGuiKey.C, false))
        {
            return false;
        }

        copyTaken = true;
        return true;
    }

    public static bool CopyTaken => copyTaken;

    public static void MarkCopy() => copyTaken = true;

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

    private static void RememberDrags()
    {
        var pointer = ImGui.GetMousePos();
        var slopSq = DragSlop * DragSlop;
        for (var i = 0; i < 3; i++)
        {
            var mouse = (ImGuiMouseButton)i;
            if (ImGui.IsMouseClicked(mouse))
            {
                pressAt[i] = pointer;
                armed[i] = true;
                dragged[i] = false;
            }

            if (armed[i] && ImGui.IsMouseDown(mouse))
            {
                var delta = pointer - pressAt[i];
                if (delta.X * delta.X + delta.Y * delta.Y >= slopSq)
                {
                    dragged[i] = true;
                }
            }
            else if (!ImGui.IsMouseDown(mouse) && !ImGui.IsMouseReleased(mouse))
            {
                armed[i] = false;
                dragged[i] = false;
            }
        }
    }

    private static int ButtonIndex(PointerButton button) => button switch
    {
        PointerButton.Secondary => 1,
        PointerButton.Middle => 2,
        _ => 0,
    };

    private static ImGuiMouseButton ToImGuiButton(PointerButton button) => (ImGuiMouseButton)ButtonIndex(button);
}
