using Linkpearl.Geometry;

namespace Linkpearl.Input;

public enum PointerButton : byte
{
    Primary = 0,
    Secondary = 1,
    Middle = 2,
}

public interface IInputProbe
{
    Vector2 Pointer { get; }

    Vector2 PointerDelta { get; }

    float ScrollDelta { get; }

    bool IsHovering(Rect area);

    bool WasPressed(Rect area, PointerButton button = PointerButton.Primary);

    bool WasReleased(Rect area, PointerButton button = PointerButton.Primary);

    bool IsHeld(PointerButton button = PointerButton.Primary);

    // Tap: released over the area without dragging. A drag-release is not a click.
    bool WasClicked(Rect area, PointerButton button = PointerButton.Primary);

    bool ConsumeClick(Rect area, PointerButton button = PointerButton.Primary);

    bool PressedInside(Rect area, PointerButton button = PointerButton.Primary);

    void Claim(Rect area);

    bool IsClaimed(Rect area);

    bool PointerClaimed();

    // True this frame when Escape was pressed. Search sheets use it to dismiss.
    bool EscapePressed();

    // Mouse-up this frame that was not a drag, anywhere on screen (including off the phone).
    bool PointerReleased(PointerButton button = PointerButton.Primary);

    // Ctrl/Cmd+C this frame, ignoring composer capture.
    bool CopyChord() => false;
}
