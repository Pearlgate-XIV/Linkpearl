using Linkpearl.Applets;
using Linkpearl.Geometry;
using Linkpearl.Painting;

namespace Linkpearl.Device.Shell.Studio;

internal static class StudioChrome
{
    public static void DrawPanel(in AppletFrame frame, Rect area)
    {
        var radius = frame.Units(14f);
        var gold = frame.Theme.Palette.WarmAccent;
        var hovered = frame.Input.IsHovering(area);
        frame.Paint.Fill(area, frame.Theme.Palette.SurfaceOverlay with { W = hovered ? 0.80f : 0.68f }, radius);
        frame.Paint.Stroke(area, gold with { W = hovered ? 0.40f : 0.20f }, frame.Theme.Metrics.Hairline, radius);
    }

    public static void DrawHeader(in AppletFrame frame, Rect row, string title, bool more, out Rect moreHit)
    {
        var gold = frame.Theme.Palette.WarmAccent;
        frame.Text.DrawEllipsized(row.LeftSlice(row.Width * 0.62f), title,
            new TextStyle(FontRole.CaptionStrong, gold, TextAlign.Left, 1f, 0.92f));
        moreHit = row.RightSlice(frame.Units(72f));
        if (more)
        {
            frame.Text.DrawIn(moreHit, "Show more",
                new TextStyle(FontRole.Caption, frame.Theme.Palette.InkMuted, TextAlign.Right, 1f, 0.88f));
        }
    }
}
