using Dalamud.Bindings.ImGui;
using Linkpearl.Geometry;

namespace Linkpearl.Device.Windows;

// Owns handset drag start/end. ImGui's built-in move bit flips with hover and can
// leave the window captured after mouse-up; this grab is explicit and always
// released on mouse-up, Escape, lost focus, or close.
internal sealed class WindowGrab
{
    public bool IsDragging { get; private set; }

    public Vector2 Offset { get; private set; }

    public bool WasFocused { get; set; } = true;

    public void Begin(Vector2 pointer, Vector2 windowMin)
    {
        IsDragging = true;
        Offset = pointer - windowMin;
    }

    public Vector2 Follow(Vector2 pointer, Vector2 size) =>
        HandsetPlacement.Clamp(pointer - Offset, size);

    public bool ShouldDrop(bool windowOpen)
    {
        if (!IsDragging)
        {
            return false;
        }

        if (!windowOpen || !WasFocused)
        {
            return true;
        }

        if (!ImGui.IsMouseDown(ImGuiMouseButton.Left) || ImGui.IsMouseReleased(ImGuiMouseButton.Left))
        {
            return true;
        }

        return ImGui.IsKeyPressed(ImGuiKey.Escape, false);
    }

    public void Drop()
    {
        IsDragging = false;
        Offset = Vector2.Zero;
    }

    public static bool HitsChrome(Vector2 pointer, Rect window, Rect screen, bool pocket, bool locked,
        Rect power, Rect volume, Rect lockHit, Rect slider, Rect notice)
    {
        if (locked || window.IsEmpty || !window.Contains(pointer))
        {
            return false;
        }

        if (power.Contains(pointer) || volume.Contains(pointer) || lockHit.Contains(pointer))
        {
            return false;
        }

        if (pocket)
        {
            if (slider.Contains(pointer) || notice.Contains(pointer))
            {
                return false;
            }

            return !screen.Contains(pointer);
        }

        return !screen.Contains(pointer);
    }
}
