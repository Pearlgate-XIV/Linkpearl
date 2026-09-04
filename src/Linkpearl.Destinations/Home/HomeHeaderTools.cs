using System.Globalization;
using Linkpearl.Applets;
using Linkpearl.Geometry;
using Linkpearl.Painting;

namespace Linkpearl.Destinations.Home;

public static class HomeHeaderTools
{
    public static void Draw(in AppletFrame frame, Rect page, int waiting, bool hush, out Rect notice,
        out Rect settings)
    {
        var gold = frame.Theme.Palette.WarmAccent;
        var markWidth = frame.Units(28f);
        var toolGap = frame.Units(6f);
        var insetX = frame.Units(18f);
        var insetY = frame.Units(8f);
        var tools = new Rect(
            new Vector2(page.Max.X - insetX - markWidth * 2f - toolGap, page.Min.Y + insetY),
            new Vector2(page.Max.X - insetX, page.Min.Y + insetY + markWidth));
        notice = tools.LeftSlice(markWidth);
        settings = tools.RightSlice(markWidth);
        var ink = gold with { W = 0.82f };
        HomeMarks.Draw(frame.Paint, notice, HomeMark.Bell, ink);
        HomeMarks.Draw(frame, HomeMarks.MatchBell(settings), HomeMark.Gear, ink);
        if (waiting > 0 && !hush)
        {
            var radius = frame.Units(7f);
            var center = new Vector2(notice.Max.X - radius * 0.15f, notice.Min.Y + radius * 0.2f);
            frame.Paint.FillCircle(center, radius, frame.Theme.Palette.Negative);
            frame.Text.Draw(center, waiting > 9 ? "9+" : waiting.ToString(CultureInfo.InvariantCulture),
                new TextStyle(FontRole.Caption, Vector4.One, TextAlign.Center));
        }
    }
}
