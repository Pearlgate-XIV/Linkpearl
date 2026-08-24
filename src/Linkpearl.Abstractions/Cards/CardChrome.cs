using Linkpearl.Applets;
using Linkpearl.Geometry;
using Linkpearl.Painting;
using Linkpearl.Theming;

namespace Linkpearl.Cards;

// The shared "smoked aetherglass" surface every card in the new destination UI sits on: a
// translucent dark fill, a hairline border rather than a heavy frame, restrained corner
// rounding. Deliberately plain — the reference calls for calm, premium software over an ornate
// physical shell, not a second layer of decoration competing with the chassis.
public static class CardChrome
{
    public static void Draw(in AppletFrame frame, Rect area, float emphasis = 1f)
    {
        var radius = frame.Units(14f);
        frame.Paint.Fill(area, frame.Theme.Palette.SurfaceOverlay, radius);
        frame.Paint.Stroke(area, frame.Theme.Palette.Separator, frame.Units(1f), radius);
        if (emphasis > 1f)
        {
            frame.Paint.Stroke(area, frame.Theme.Palette.Accent with { W = 0.35f }, frame.Units(1f), radius);
        }
    }

    public static void DrawKicker(in AppletFrame frame, Rect area, string label, Vector4? color = null)
    {
        frame.Text.DrawIn(area, label,
            new TextStyle(FontRole.Caption, color ?? frame.Theme.Palette.InkMuted, TextAlign.Left));
    }
}
