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
        var radius = frame.Units(12f);
        frame.Paint.Fill(area, frame.Theme.Palette.SurfaceOverlay, radius);
        frame.Paint.Stroke(area, frame.Theme.Palette.Separator, frame.Units(1f), radius);
        if (emphasis > 1f)
        {
            frame.Paint.Stroke(area, frame.Theme.Palette.Accent with { W = 0.35f }, frame.Units(1f), radius);
        }
    }

    public static void DrawGold(in AppletFrame frame, Rect area)
    {
        var radius = frame.Units(12f);
        var hovered = frame.Input.IsHovering(area);
        frame.Paint.Fill(area, frame.Theme.Palette.SurfaceOverlay, radius);
        var gold = frame.Theme.Palette.WarmAccent with { W = hovered ? 0.55f : 0.32f };
        frame.Paint.Stroke(area, gold, frame.Units(1f), radius);
        if (hovered)
        {
            frame.Paint.Glow(area, frame.Theme.Palette.WarmAccent with { W = 0.16f }, radius, frame.Units(5f));
        }
    }

    public static void DrawKicker(in AppletFrame frame, Rect area, string label, Vector4? color = null)
    {
        frame.Text.DrawIn(area, label,
            new TextStyle(FontRole.Caption, color ?? frame.Theme.Palette.InkMuted, TextAlign.Left));
    }
}
