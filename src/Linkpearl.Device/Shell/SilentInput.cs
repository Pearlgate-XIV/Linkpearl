using Linkpearl.Geometry;
using Linkpearl.Input;

namespace Linkpearl.Device.Shell;

internal sealed class SilentInput : IInputProbe
{
    public static SilentInput Instance { get; } = new();

    public Vector2 Pointer => Vector2.Zero;

    public Vector2 PointerDelta => Vector2.Zero;

    public float ScrollDelta => 0f;

    public bool IsHovering(Rect area) => false;

    public bool WasPressed(Rect area, PointerButton button = PointerButton.Primary) => false;

    public bool WasReleased(Rect area, PointerButton button = PointerButton.Primary) => false;

    public bool IsHeld(PointerButton button = PointerButton.Primary) => false;

    public bool WasClicked(Rect area, PointerButton button = PointerButton.Primary) => false;

    public bool ConsumeClick(Rect area, PointerButton button = PointerButton.Primary) => false;

    public void Claim(Rect area)
    {
    }

    public bool IsClaimed(Rect area) => false;
}
