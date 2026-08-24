using Linkpearl.Destinations;
using Linkpearl.Geometry;
using Linkpearl.Input;
using Linkpearl.Painting;
using Linkpearl.Theming;

namespace Linkpearl.Device.Shell;

public readonly record struct DestinationBarResult(DestinationTab? Selected, bool CrystalTapped);

// The permanent bottom navigation: exactly four destinations, no android-style soft keys, no
// per-app icons. The central crystal is not a fifth tab — it floats above the row and opens
// Universal Search, matching the reference's raised, glowing diamond between the tabs.
public static class DestinationBar
{
    private const float HeightUnits = 58f;
    private const float CrystalRadiusUnits = 16f;
    private const float CrystalLiftUnits = 12f;

    public static float Height(float scale) => HeightUnits * scale;

    public static Rect StripArea(Rect screen, float scale) => screen.BottomSlice(Height(scale));

    public static DestinationBarResult Draw(IPaintSurface paint, ITextPainter text, IInputProbe input, ITheme theme,
        Rect screen, float scale, IReadOnlyList<IDestinationScreen> destinations, DestinationTab current)
    {
        var strip = StripArea(screen, scale);
        paint.Fill(strip, theme.Palette.SurfaceOverlay);
        paint.Stroke(strip.TopSlice(1f), theme.Palette.Separator, theme.Metrics.Hairline);

        var cellWidth = strip.Width / destinations.Count;
        DestinationTab? pressed = null;
        for (var index = 0; index < destinations.Count; index++)
        {
            var cell = strip.Translate(new Vector2(index * cellWidth, 0f)).WithWidth(cellWidth);
            var destination = destinations[index];
            var isActive = destination.Tab == current;
            var ink = isActive ? theme.Palette.Accent : theme.Palette.InkFaint;

            var glyphArea = cell.TopSlice(cell.Height * 0.6f);
            text.DrawIn(glyphArea, destination.Glyph, new TextStyle(FontRole.Title, ink, TextAlign.Center));
            var labelArea = cell.BottomSlice(cell.Height * 0.4f);
            text.DrawIn(labelArea, destination.Label, new TextStyle(FontRole.Caption, ink, TextAlign.Center));

            if (input.ConsumeClick(cell))
            {
                pressed = destination.Tab;
            }
        }

        var crystalCenter = new Vector2(strip.Center.X, strip.Min.Y - CrystalLiftUnits * scale);
        var crystalRadius = CrystalRadiusUnits * scale;
        var crystalHit = Rect.FromSize(crystalCenter - new Vector2(crystalRadius, crystalRadius),
            new Vector2(crystalRadius, crystalRadius) * 2f);
        var crystalTapped = input.ConsumeClick(crystalHit);

        paint.Glow(crystalHit, theme.Palette.WarmAccent with { W = 0.5f }, crystalRadius, crystalRadius * 0.6f);
        DrawDiamond(paint, crystalCenter, crystalRadius, theme.Palette.WarmAccent);
        DrawDiamond(paint, crystalCenter, crystalRadius * 0.55f, theme.Palette.SurfaceOverlay);

        return new DestinationBarResult(crystalTapped ? null : pressed, crystalTapped);
    }

    private static void DrawDiamond(IPaintSurface paint, Vector2 center, float radius, Vector4 color)
    {
        Span<Vector2> points = stackalloc Vector2[4]
        {
            center + new Vector2(0f, -radius), center + new Vector2(radius, 0f), center + new Vector2(0f, radius),
            center + new Vector2(-radius, 0f),
        };
        paint.Polyline(points, color, MathF.Max(radius * 0.16f, 1.5f), closed: true);
    }
}
