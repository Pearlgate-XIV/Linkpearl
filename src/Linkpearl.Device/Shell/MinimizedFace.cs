using Linkpearl.Geometry;
using Linkpearl.Painting;
using Linkpearl.Theming;

namespace Linkpearl.Device.Shell;

// Docked communicator: a small fixed footprint, not a scaled-down phone. Clock and crystal
// on the glass; the whole face is the maximize target.
public static class MinimizedFace
{
    public static Vector2 WindowSize => new(112f, 192f);

    public static void Draw(IPaintSurface paint, ITextPainter text, ITheme theme, Rect screen, float scale,
        string clockText)
    {
        var inset = screen.Inset(scale * 8f);
        if (inset.IsEmpty)
        {
            return;
        }

        text.DrawIn(inset.TopSlice(scale * 22f), clockText,
            new TextStyle(FontRole.CaptionStrong, theme.Palette.Ink, TextAlign.Center));
        DrawCrystal(paint, inset.Center - new Vector2(0f, scale * 8f),
            MathF.Min(inset.Width, inset.Height) * 0.16f, theme.Palette.WarmAccent);
        DrawMaximize(paint, inset.BottomSlice(scale * 28f), theme.Palette.Accent);
    }

    private static void DrawCrystal(IPaintSurface paint, Vector2 center, float radius, Vector4 color)
    {
        Span<Vector2> points =
        [
            center + new Vector2(0f, -radius),
            center + new Vector2(radius, 0f),
            center + new Vector2(0f, radius),
            center + new Vector2(-radius, 0f),
        ];
        paint.Polyline(points, color, MathF.Max(1.4f, radius * 0.18f), closed: true);
    }

    private static void DrawMaximize(IPaintSurface paint, Rect area, Vector4 color)
    {
        var size = MathF.Min(area.Width, area.Height) * 0.42f;
        var center = area.Center;
        var thickness = MathF.Max(1.4f, size * 0.18f);
        var arm = size * 0.55f;
        DrawCorner(paint, center + new Vector2(-size, -size), arm, 1f, 1f, color, thickness);
        DrawCorner(paint, center + new Vector2(size, -size), arm, -1f, 1f, color, thickness);
        DrawCorner(paint, center + new Vector2(-size, size), arm, 1f, -1f, color, thickness);
        DrawCorner(paint, center + new Vector2(size, size), arm, -1f, -1f, color, thickness);
    }

    private static void DrawCorner(IPaintSurface paint, Vector2 origin, float arm, float signX, float signY,
        Vector4 color, float thickness)
    {
        paint.Line(origin, origin + new Vector2(arm * signX, 0f), color, thickness);
        paint.Line(origin, origin + new Vector2(0f, arm * signY), color, thickness);
    }
}
