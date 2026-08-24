using Linkpearl.Applets;
using Linkpearl.Geometry;
using Linkpearl.Painting;

namespace Linkpearl.Device.Shell;

// Flush translucent status chrome: clock right-aligned, no idle activity pill sitting on top of
// it at rest. Activity chips only appear while something is actually running.
public static class StatusStrip
{
    private const float HeightUnits = 34f;

    public static float Height(float scale) => HeightUnits * scale;

    public static Rect StripArea(Rect screen, float scale) => screen.TopSlice(Height(scale));

    public static void Draw(in AppletFrame frame, Rect screen, string clockText)
    {
        var strip = StripArea(screen, frame.Scale);
        frame.Paint.Fill(strip, frame.Theme.Palette.Surface with { W = 0.30f });
        var inset = strip.Inset(new Edges(frame.Units(16f), 0f));
        frame.Text.DrawIn(inset, clockText,
            new TextStyle(FontRole.Caption, frame.Theme.Palette.Ink, TextAlign.Right));
    }
}
