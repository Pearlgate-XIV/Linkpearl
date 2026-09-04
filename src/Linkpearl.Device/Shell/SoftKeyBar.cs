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
    private const float HeightUnits = 51.3f;

    public static float Height(float scale) => HeightUnits * scale;

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
        var strip = KeyBand(screen, scale);
        var cellWidth = strip.Width / 3f;
        var ink = theme.Palette.Ink;
        var rest = theme.Palette.InkFaint;
        var mark = scale * 8.64f;
        var recents = CellAt(strip, 0, cellWidth);
        var home = CellAt(strip, 1, cellWidth);
        var back = CellAt(strip, 2, cellWidth);
        DrawRecents(paint, MarkCenter(recents, scale), mark, Lit(input, recents) ? ink : rest);
        DrawHome(paint, MarkCenter(home, scale), scale * 10.08f, ink, theme.Palette.SurfaceOverlay);
        var backHot = canGoBack && Lit(input, back);
        DrawBack(paint, MarkCenter(back, scale), mark, backHot ? Vector4.One : rest);
    }

    private static bool Lit(IInputProbe input, Rect cell) =>
        cell.Contains(input.Pointer) || input.IsHovering(cell) || input.WasPressed(cell) ||
        (input.IsHeld() && cell.Contains(input.Pointer));

    private static Rect KeyBand(Rect screen, float scale)
    {
        var strip = screen.BottomSlice(Height(scale));
        return strip.Inset(new Edges(strip.Width * 0.08f, 0f));
    }

    private static Rect CellAt(Rect strip, int index, float cellWidth) =>
        strip.Translate(new Vector2(index * cellWidth, 0f)).WithWidth(cellWidth);

    private static Vector2 MarkCenter(Rect cell, float scale) =>
        cell.Center + new Vector2(0f, cell.Height * 0.20f - 5f * scale);

    private static void DrawRecents(IPaintSurface paint, Vector2 center, float radius, Vector4 color)
    {
        var rise = radius * 0.95f * 0.95f;
        var gap = radius * 0.48f;
        var stroke = MathF.Max(2.2f, radius * 0.22f);
        paint.Line(center + new Vector2(-gap, -rise), center + new Vector2(-gap, rise), color, stroke);
        paint.Line(center + new Vector2(0f, -rise), center + new Vector2(0f, rise), color, stroke);
        paint.Line(center + new Vector2(gap, -rise), center + new Vector2(gap, rise), color, stroke);
    }

    private static void DrawHome(IPaintSurface paint, Vector2 center, float radius, Vector4 ink, Vector4 inner)
    {
        var hit = Rect.FromSize(center - new Vector2(radius, radius), new Vector2(radius, radius) * 2f);
        paint.Glow(hit, ink with { W = 0.28f }, radius * 1.05f, radius * 0.55f);
        DrawDiamond(paint, center, radius, ink, MathF.Max(2.4f, radius * 0.22f), 0.95f);
        DrawDiamond(paint, center, radius * 0.52f, inner, MathF.Max(1.8f, radius * 0.16f), 0.95f);
    }

    private static void DrawBack(IPaintSurface paint, Vector2 center, float radius, Vector4 color)
    {
        var stroke = MathF.Max(2.4f, radius * 0.24f);
        var tip = center + new Vector2(-radius * 0.42f, 0f);
        paint.Line(center + new Vector2(radius * 0.38f, -radius * 0.7f * 0.95f), tip, color, stroke);
        paint.Line(center + new Vector2(radius * 0.38f, radius * 0.7f * 0.95f), tip, color, stroke);
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
