using Linkpearl.Applets;
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
    private const float HeightUnits = 54f;

    public static float Height(float scale) => HeightUnits * scale;

    public static Rect StripArea(Rect screen, float scale) => screen.BottomSlice(Height(scale));

    public static SoftKey Consume(IInputProbe input, Rect screen, float scale, bool canGoBack)
    {
        var strip = StripArea(screen, scale);
        var cellWidth = strip.Width / 3f;
        if (input.ConsumeClick(CellAt(strip, 0, cellWidth)))
        {
            return SoftKey.Recents;
        }

        if (input.ConsumeClick(CellAt(strip, 1, cellWidth)))
        {
            return SoftKey.Home;
        }

        if (canGoBack && input.ConsumeClick(CellAt(strip, 2, cellWidth)))
        {
            return SoftKey.Back;
        }

        return SoftKey.None;
    }

    public static void Paint(IPaintSurface paint, ITheme theme, Rect screen, float scale, bool canGoBack)
    {
        var strip = StripArea(screen, scale);
        var cellWidth = strip.Width / 3f;
        var ink = theme.Palette.Ink;
        var gold = theme.Palette.WarmAccent;
        DrawRecents(paint, CellAt(strip, 0, cellWidth).Center, scale * 12f, ink);
        DrawHome(paint, CellAt(strip, 1, cellWidth).Center, scale * 14f, gold, theme.Palette.SurfaceOverlay);
        DrawBack(paint, CellAt(strip, 2, cellWidth).Center, scale * 12f, canGoBack ? ink : theme.Palette.InkFaint);
    }

    private static Rect CellAt(Rect strip, int index, float cellWidth) =>
        strip.Translate(new Vector2(index * cellWidth, 0f)).WithWidth(cellWidth);

    private static void DrawRecents(IPaintSurface paint, Vector2 center, float radius, Vector4 color)
    {
        var rise = radius * 0.95f;
        var gap = radius * 0.48f;
        var stroke = MathF.Max(2.2f, radius * 0.22f);
        paint.Line(center + new Vector2(-gap, -rise), center + new Vector2(-gap, rise), color, stroke);
        paint.Line(center + new Vector2(0f, -rise), center + new Vector2(0f, rise), color, stroke);
        paint.Line(center + new Vector2(gap, -rise), center + new Vector2(gap, rise), color, stroke);
    }

    private static void DrawHome(IPaintSurface paint, Vector2 center, float radius, Vector4 gold, Vector4 inner)
    {
        var hit = Rect.FromSize(center - new Vector2(radius, radius), new Vector2(radius, radius) * 2f);
        paint.Glow(hit, gold with { W = 0.82f }, radius * 1.15f, radius * 0.85f);
        DrawDiamond(paint, center, radius, gold, MathF.Max(2.4f, radius * 0.22f));
        DrawDiamond(paint, center, radius * 0.52f, inner, MathF.Max(1.8f, radius * 0.16f));
    }

    private static void DrawBack(IPaintSurface paint, Vector2 center, float radius, Vector4 color)
    {
        var stroke = MathF.Max(2.4f, radius * 0.24f);
        var tip = center + new Vector2(-radius * 0.42f, 0f);
        paint.Line(center + new Vector2(radius * 0.38f, -radius * 0.7f), tip, color, stroke);
        paint.Line(center + new Vector2(radius * 0.38f, radius * 0.7f), tip, color, stroke);
    }

    private static void DrawDiamond(IPaintSurface paint, Vector2 center, float radius, Vector4 color, float stroke)
    {
        Span<Vector2> points = stackalloc Vector2[4]
        {
            center + new Vector2(0f, -radius),
            center + new Vector2(radius, 0f),
            center + new Vector2(0f, radius),
            center + new Vector2(-radius, 0f),
        };
        paint.Polyline(points, color, stroke, closed: true);
    }
}
