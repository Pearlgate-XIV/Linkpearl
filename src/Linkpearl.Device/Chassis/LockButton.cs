using Linkpearl.Geometry;
using Linkpearl.Input;
using Linkpearl.Painting;
using Linkpearl.Theming;

namespace Linkpearl.Device.Chassis;

// Position pin on the status row, just inward of the right edge and next to the battery.
public static class LockButton
{
    public const float WidthUnits = 18f;
    public const float HeightUnits = 30f;
    public const float InwardUnits = 7f;
    public const float MarksGapUnits = 2f;

    public static float ReservedRight(float scale) =>
        (InwardUnits + WidthUnits + MarksGapUnits) * scale;

    public static Rect Area(Rect window, Rect screen, float scale)
    {
        _ = window;
        var width = WidthUnits * scale;
        var height = HeightUnits * scale;
        var inward = InwardUnits * scale;
        var right = screen.Max.X - inward;
        return new Rect(
            new Vector2(right - width, screen.Min.Y),
            new Vector2(right, screen.Min.Y + height));
    }

    public static Rect HitArea(Rect window, Rect screen, float scale) =>
        Area(window, screen, scale).Expand(scale * 6f);

    public static bool WasTapped(Rect area, IInputProbe input) => input.ConsumeClick(area);

    public static void Draw(IPaintSurface paint, ITheme theme, Rect area, bool locked)
    {
        var color = locked ? theme.Palette.Ink : theme.Palette.InkMuted;
        DrawPadlock(paint, area, color, locked);
    }

    private static void DrawPadlock(IPaintSurface paint, Rect area, Vector4 color, bool locked)
    {
        var size = MathF.Min(area.Width, area.Height);
        var bodyWidth = size * 0.46f;
        var bodyHeight = bodyWidth * 0.78f;
        var body = Rect.FromSize(
            new Vector2(area.Center.X - bodyWidth * 0.5f, area.Center.Y - bodyHeight * 0.12f),
            new Vector2(bodyWidth, bodyHeight));
        var thickness = MathF.Max(1.4f, size * 0.09f);
        var shackleRadius = bodyWidth * 0.40f;
        var hinge = new Vector2(body.Center.X, body.Min.Y);
        if (locked)
        {
            paint.StrokeCircle(hinge, shackleRadius, color, thickness);
        }
        else
        {
            DrawOpenShackle(paint, hinge, shackleRadius, color, thickness);
        }

        paint.Fill(body, color, bodyWidth * 0.18f);
    }

    private static void DrawOpenShackle(IPaintSurface paint, Vector2 hinge, float radius, Vector4 color,
        float thickness)
    {
        const int steps = 14;
        const float from = MathF.PI;
        const float to = MathF.PI * 1.86f;
        var previous = hinge + new Vector2(MathF.Cos(from), MathF.Sin(from)) * radius;
        for (var index = 1; index <= steps; index++)
        {
            var angle = from + (to - from) * (index / (float)steps);
            var point = hinge + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * radius;
            paint.Line(previous, point, color, thickness);
            previous = point;
        }
    }
}
