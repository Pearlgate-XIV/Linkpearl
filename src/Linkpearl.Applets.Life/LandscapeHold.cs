using Linkpearl.Geometry;
using Linkpearl.Painting;
using Linkpearl.Preferences;

namespace Linkpearl.Applets.Life;

internal static class LandscapeHold
{
    public static void Draw(in AppletFrame frame, DisplayPreferences display)
    {
        if (!display.AutoRotate)
        {
            return;
        }

        var size = frame.Units(40f);
        var pad = frame.Units(12f);
        var hit = Rect.FromSize(new Vector2(frame.Content.Max.X - pad - size, frame.Content.Max.Y - pad - size),
            new Vector2(size, size));
        var gold = frame.Theme.Palette.WarmAccent;
        var on = display.Landscape;
        frame.Paint.FillCircle(hit.Center, size * 0.5f, on ? gold : frame.Theme.Palette.SurfaceRaised);
        frame.Paint.StrokeCircle(hit.Center, size * 0.5f, gold, frame.Units(1.4f));
        frame.Text.DrawIn(hit, on ? "H" : "V",
            new TextStyle(FontRole.BodyStrong, on ? frame.Theme.Palette.SurfaceSunken : gold, TextAlign.Center));
        if (frame.Input.ConsumeClick(hit))
        {
            display.Landscape = !display.Landscape;
        }
    }
}
