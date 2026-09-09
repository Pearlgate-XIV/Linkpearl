using Linkpearl.Geometry;
using Linkpearl.Painting;
using Linkpearl.Theming;

namespace Linkpearl.Device.Shell;

// One of these per destination (HandsetShell keeps a dictionary keyed by tab) so switching tabs
// preserves each destination's own scroll position instead of resetting it. Offset is clamped
// every frame against the content height the destination itself just reported, so a destination
// that shrinks (fewer demo posts, a narrower size step) never leaves the view stuck scrolled past
// its own content.
public sealed class ScrollState
{
    private const float WheelUnitsPerNotch = 48f;

    private float offset;

    public float Offset => offset;

    public void Update(float contentHeight, float viewportHeight, float wheelDelta, float scale)
    {
        var maxOffset = MathF.Max(contentHeight - viewportHeight, 0f);
        offset = Scalar.Clamp(offset - wheelDelta * WheelUnitsPerNotch * scale, 0f, maxOffset);
    }

    public void Reset() => offset = 0f;

    public void Jump(float y) => offset = MathF.Max(0f, y);

    public static void DrawIndicator(IPaintSurface paint, ITheme theme, Rect viewport, float contentHeight,
        float offset, float scale)
    {
        if (contentHeight <= viewport.Height)
        {
            return;
        }

        var trackWidth = 3f * scale;
        var track = viewport.RightSlice(trackWidth).Inset(new Edges(0f, 4f * scale));
        var thumbHeight = MathF.Max(track.Height * (viewport.Height / contentHeight), 16f * scale);
        var scrollRange = MathF.Max(contentHeight - viewport.Height, 1f);
        var thumbTop = track.Min.Y + (track.Height - thumbHeight) * (offset / scrollRange);
        var thumb = new Rect(new Vector2(track.Min.X, thumbTop), new Vector2(track.Max.X, thumbTop + thumbHeight));

        paint.Fill(thumb, theme.Palette.InkFaint, trackWidth * 0.5f);
    }
}
