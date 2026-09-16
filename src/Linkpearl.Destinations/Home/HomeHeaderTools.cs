using Linkpearl.Applets;
using Linkpearl.Geometry;

namespace Linkpearl.Destinations.Home;

public static class HomeHeaderTools
{
    public static void Draw(in AppletFrame frame, Rect page, out Rect settings)
    {
        var gold = frame.Theme.Palette.WarmAccent;
        var markWidth = frame.Units(28f);
        var insetX = frame.Units(18f);
        var insetY = frame.Units(8f);
        settings = new Rect(
            new Vector2(page.Max.X - insetX - markWidth, page.Min.Y + insetY),
            new Vector2(page.Max.X - insetX, page.Min.Y + insetY + markWidth));
        var ink = gold with { W = 0.82f };
        HomeMarks.Draw(frame, HomeMarks.MatchBell(settings), HomeMark.Gear, ink);
    }
}
