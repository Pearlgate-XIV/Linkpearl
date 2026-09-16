using Linkpearl.Geometry;
using Linkpearl.Input;
using Linkpearl.Painting;
using Linkpearl.Theming;

namespace Linkpearl.Device.Shell;

public enum SoftKey : byte
{
    None = 0,
    Recents = 1,
    Home = 2,
    Back = 3,
}

// Recents, Home, Back along the bottom of the glass. Home is the Linkpearl diamond.
public static class SoftKeyBar
{
    private const float HeightUnits = 36f;
    private const float AppGapUnits = 5f;

    public static float Height(float scale) => HeightUnits * scale;

    // Open apps sit this far above the glass bottom: the key strip plus a 5px gap.
    public static float AppLift(float scale) => Height(scale) + AppGapUnits * scale;

    public static Rect StripArea(Rect screen, float scale) => KeyBand(screen, scale);

    public static Rect HomeCell(Rect screen, float scale)
    {
        var strip = KeyBand(screen, scale);
        return CellAt(strip, 1, strip.Width / 3f);
    }

    public static SoftKey Consume(IInputProbe input, Rect screen, float scale, bool canGoBack)
    {
        var strip = KeyBand(screen, scale);
        var cellWidth = strip.Width / 3f;
        if (input.ConsumeClick(CellAt(strip, 0, cellWidth)))
        {
            return SoftKey.Recents;
        }

        if (input.ConsumeClick(CellAt(strip, 1, cellWidth)))
        {
            return SoftKey.Home;
        }

        if (input.ConsumeClick(CellAt(strip, 2, cellWidth)))
        {
            return SoftKey.Back;
        }

        return SoftKey.None;
    }

    public static void Paint(IPaintSurface paint, ITheme theme, IInputProbe input, Rect screen, float scale,
        bool canGoBack)
    {
        _ = canGoBack;
        var strip = KeyBand(screen, scale);
        var cellWidth = strip.Width / 3f;
        var ink = theme.Palette.Ink;
        var mark = scale * 8.5f;
        var recents = CellAt(strip, 0, cellWidth);
        var home = CellAt(strip, 1, cellWidth);
        var back = CellAt(strip, 2, cellWidth);
        DrawKey(paint, recents, scale, ink, input, DrawRecents);
        DrawKey(paint, home, scale, ink, input, (p, c, r, color) =>
            DrawHome(p, c, r, color, theme.Palette.SurfaceOverlay));
        DrawKey(paint, back, scale, ink, input, DrawBack);
    }

    private static void DrawKey(IPaintSurface paint, Rect cell, float scale, Vector4 ink, IInputProbe input,
        Action<IPaintSurface, Vector2, float, Vector4> glyph)
    {
        var down = Pressed(input, cell);
        if (down)
        {
            DrawPressOval(paint, cell, scale, ink);
        }

        glyph(paint, cell.Center, scale * 8.5f, ink);
    }

    private static void DrawPressOval(IPaintSurface paint, Rect cell, float scale, Vector4 ink)
    {
        var gap = scale * 3f;
        var size = new Vector2(MathF.Max(scale * 22f, cell.Width - gap * 2f), scale * 22f);
        var area = Rect.FromSize(cell.Center - size * 0.5f, size);
        paint.Fill(area, ink with { W = 0.20f }, area.Height * 0.5f);
    }

    private static bool Pressed(IInputProbe input, Rect cell) =>
        input.WasPressed(cell) || (input.IsHeld() && cell.Contains(input.Cursor));

    private static Rect KeyBand(Rect screen, float scale)
    {
        var strip = screen.BottomSlice(Height(scale));
        return strip.Inset(new Edges(strip.Width * 0.08f, 0f));
    }

    private static Rect CellAt(Rect strip, int index, float cellWidth) =>
        strip.Translate(new Vector2(index * cellWidth, 0f)).WithWidth(cellWidth);

    private static void DrawRecents(IPaintSurface paint, Vector2 center, float radius, Vector4 color)
    {
        var rise = radius * 0.72f;
        var gap = radius * 0.40f;
        var stroke = MathF.Max(1.8f, radius * 0.20f);
        paint.Line(center + new Vector2(-gap, -rise), center + new Vector2(-gap, rise), color, stroke);
        paint.Line(center + new Vector2(0f, -rise), center + new Vector2(0f, rise), color, stroke);
        paint.Line(center + new Vector2(gap, -rise), center + new Vector2(gap, rise), color, stroke);
    }

    private static void DrawHome(IPaintSurface paint, Vector2 center, float radius, Vector4 ink, Vector4 inner)
    {
        DrawDiamond(paint, center, radius, ink, MathF.Max(1.8f, radius * 0.20f));
        DrawDiamond(paint, center, radius * 0.50f, inner, MathF.Max(1.5f, radius * 0.16f));
    }

    private static void DrawBack(IPaintSurface paint, Vector2 center, float radius, Vector4 color)
    {
        var stroke = MathF.Max(1.8f, radius * 0.20f);
        var rise = radius * 0.72f;
        var tip = center + new Vector2(-radius * 0.78f, 0f);
        paint.Line(center + new Vector2(radius * 0.22f, -rise), tip, color, stroke);
        paint.Line(center + new Vector2(radius * 0.22f, rise), tip, color, stroke);
    }

    private static void DrawDiamond(IPaintSurface paint, Vector2 center, float radius, Vector4 color, float stroke,
        float stretchY = 1f)
    {
        Span<Vector2> points = stackalloc Vector2[4]
        {
            center + new Vector2(0f, -radius * stretchY),
            center + new Vector2(radius, 0f),
            center + new Vector2(0f, radius * stretchY),
            center + new Vector2(-radius, 0f),
        };
        paint.Polyline(points, color, stroke, closed: true);
    }
}
