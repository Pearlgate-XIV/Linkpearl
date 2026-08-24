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

// Android/AQUOS-style soft keys: Recents, Home, Back, in a translucent strip along the bottom
// edge. This is the identity choice that keeps Linkpearl off an iOS gesture-nav read.
public static class SoftKeyBar
{
    private const float HeightUnits = 48f;
    private const float FillAlpha = 0.42f;
    private const float StrokeWidth = 1.7f;

    public static float Height(float scale) => HeightUnits * scale;

    public static Rect StripArea(Rect screen, float scale) => screen.BottomSlice(Height(scale));

    public static SoftKey Draw(in AppletFrame frame, Rect screen, bool canGoBack)
    {
        var strip = StripArea(screen, frame.Scale);
        frame.Paint.Fill(strip, frame.Theme.Palette.Surface with { W = FillAlpha });

        var cellWidth = strip.Width / 3f;
        var pressed = SoftKey.None;

        if (DrawKey(frame, CellAt(strip, 0, cellWidth), true, DrawRecentsGlyph))
        {
            pressed = SoftKey.Recents;
        }

        if (DrawKey(frame, CellAt(strip, 1, cellWidth), true, DrawHomeGlyph))
        {
            pressed = SoftKey.Home;
        }

        if (DrawKey(frame, CellAt(strip, 2, cellWidth), canGoBack, DrawBackGlyph))
        {
            pressed = SoftKey.Back;
        }

        return pressed;
    }

    private static Rect CellAt(Rect strip, int index, float cellWidth) =>
        strip.Translate(new Vector2(index * cellWidth, 0f)).WithWidth(cellWidth);

    private static bool DrawKey(in AppletFrame frame, Rect cell, bool enabled, Action<IPaintSurface, Vector2, float, Vector4> draw)
    {
        var ink = enabled ? frame.Theme.Palette.Ink : frame.Theme.Palette.InkFaint;
        draw(frame.Paint, cell.Center, frame.Units(9f), ink);
        return enabled && frame.Input.ConsumeClick(cell);
    }

    private static void DrawRecentsGlyph(IPaintSurface paint, Vector2 center, float radius, Vector4 color) =>
        paint.Stroke(Rect.FromSize(center - new Vector2(radius * 0.8f), new Vector2(radius, radius) * 1.6f), color,
            StrokeWidth, radius * 0.3f);

    private static void DrawHomeGlyph(IPaintSurface paint, Vector2 center, float radius, Vector4 color) =>
        paint.StrokeCircle(center, radius * 0.75f, color, StrokeWidth * 1.3f);

    private static void DrawBackGlyph(IPaintSurface paint, Vector2 center, float radius, Vector4 color)
    {
        var tip = center + new Vector2(-radius * 0.5f, 0f);
        paint.Polyline(new[]
        {
            center + new Vector2(radius * 0.4f, -radius * 0.6f), tip, center + new Vector2(radius * 0.4f, radius * 0.6f),
        }, color, StrokeWidth, false);
    }
}
