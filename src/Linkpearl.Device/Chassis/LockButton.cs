using Linkpearl.Geometry;
using Linkpearl.Input;
using Linkpearl.Painting;
using Linkpearl.Theming;

namespace Linkpearl.Device.Chassis;

// Padlock tab on the top-right of the glass (status row). The Crystal bezel is only a few
// pixels wide, so parking this in the rail made it disappear. It still sits in the chassis
// chrome band, overlapping the screen enough to see and tap.
public static class LockButton
{
    public const float WidthUnits = 28f;
    public const float HeightUnits = 30f;

    public static Rect Area(Rect window, Rect screen, float scale)
    {
        _ = window;
        var width = WidthUnits * scale;
        var height = HeightUnits * scale;
        return new Rect(
            new Vector2(screen.Max.X - width, screen.Min.Y),
            new Vector2(screen.Max.X, screen.Min.Y + height));
    }

    public static bool WasTapped(Rect area, IInputProbe input) => input.ConsumeClick(area);

    public static void Draw(IPaintSurface paint, ITheme theme, Rect area, bool locked)
    {
        var ink = locked ? theme.Palette.Accent : theme.Palette.Ink;
        var fill = locked
            ? theme.Palette.Accent with { W = 0.32f }
            : theme.Palette.SurfaceOverlay with { W = 0.72f };
        paint.Fill(area, fill, area.Height * 0.42f, Corner.Left);
        DrawPadlock(paint, area, ink);
    }

    private static void DrawPadlock(IPaintSurface paint, Rect area, Vector4 color)
    {
        var size = MathF.Min(area.Width, area.Height);
        var bodyWidth = size * 0.42f;
        var bodyHeight = bodyWidth * 0.78f;
        var body = Rect.FromSize(
            new Vector2(area.Center.X - bodyWidth * 0.5f, area.Center.Y - bodyHeight * 0.18f),
            new Vector2(bodyWidth, bodyHeight));
        var thickness = MathF.Max(1.4f, size * 0.08f);
        var shackleRadius = bodyWidth * 0.38f;
        paint.StrokeCircle(new Vector2(body.Center.X, body.Min.Y), shackleRadius, color, thickness);
        paint.Fill(body, color, bodyWidth * 0.18f);
    }
}
