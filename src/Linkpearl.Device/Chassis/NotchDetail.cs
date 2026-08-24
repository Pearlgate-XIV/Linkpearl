using Linkpearl.Geometry;
using Linkpearl.Painting;

namespace Linkpearl.Device.Chassis;

// A single sensor cutout at top-center, teardrop-shaped: flat where it meets the top edge,
// rounded at the bottom. Painted as an overlay in the frame color rather than a true clip region
// (ImGui clipping is rect-only), so it reads as a physical cutout without touching content layout.
public static class NotchDetail
{
    private const float WidthFraction = 0.22f;
    private const float HeightUnits = 20f;
    private const float SensorRadiusUnits = 3.5f;

    public static void Draw(IPaintSurface paint, Rect screen, float scale, Vector4 frameColor, Vector4 sensorColor)
    {
        var width = screen.Width * WidthFraction;
        var height = HeightUnits * scale;
        var notch = new Rect(new Vector2(screen.Center.X - width * 0.5f, screen.Min.Y),
            new Vector2(screen.Center.X + width * 0.5f, screen.Min.Y + height));

        paint.Fill(notch, frameColor, height * 0.9f, Corner.Bottom);
        paint.FillCircle(new Vector2(notch.Center.X, notch.Max.Y - height * 0.35f), SensorRadiusUnits * scale,
            sensorColor);
    }
}
