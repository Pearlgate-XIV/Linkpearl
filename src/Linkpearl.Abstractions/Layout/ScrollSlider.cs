using Linkpearl.Applets;
using Linkpearl.Geometry;

namespace Linkpearl.Layout;

// Wheel and content-drag for a clipped page. No on-screen rail.
public static class ScrollSlider
{
    public static void Apply(in AppletFrame frame, Rect viewport, ref float offset, float content,
        bool live = true)
    {
        var max = MathF.Max(0f, content - viewport.Height);
        if (live)
        {
            Steer(frame, viewport, ref offset, max);
        }

        offset = Math.Clamp(offset, 0f, max);
    }

    public static void Steer(in AppletFrame frame, Rect viewport, ref float offset, float max)
    {
        if (max <= 0f)
        {
            return;
        }

        if (frame.Input.PointerClaimed() ||
            (!frame.Input.IsHovering(viewport) &&
             !(frame.Input.IsHeld() && viewport.Contains(frame.Input.Cursor))))
        {
            return;
        }

        if (frame.Input.ScrollDelta != 0f)
        {
            offset -= frame.Input.ScrollDelta * frame.Units(24f);
        }

        if (frame.Input.IsHeld() && MathF.Abs(frame.Input.PointerDelta.Y) > 0.15f)
        {
            offset -= frame.Input.PointerDelta.Y;
        }
    }
}
