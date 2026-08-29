using System.Globalization;
using Linkpearl.Applets;
using Linkpearl.Cards;
using Linkpearl.Geometry;
using Linkpearl.Layout;
using Linkpearl.Painting;
using Linkpearl.Time;

namespace Linkpearl.Applets.Life.Calendar;

public sealed class CalendarApplet : IApplet
{
    public static readonly AppletManifest Manifest = new()
    {
        Id = "calendar",
        DisplayNameKey = "Calendar",
        Family = AppletFamily.Life,
        Glyph = "▦",
        HomeOrder = 7,
    };

    private readonly IClock clock;

    public CalendarApplet(IClock clock)
    {
        this.clock = clock;
    }

    AppletManifest IApplet.Manifest => Manifest;

    public AppletBadge Badge => AppletBadge.None;

    public void Enter(AppletEntry entry)
    {
    }

    public void Leave()
    {
    }

    public void Compose(in AppletFrame frame)
    {
        var now = clock.Now;
        var content = frame.Content.Inset(frame.Units(16f));
        var stack = new Stack(content, StackAxis.Vertical, frame.Units(10f));
        frame.Text.DrawIn(stack.Take(frame.Units(28f)), "Calendar",
            new TextStyle(FontRole.Title, frame.Theme.Palette.Ink));
        frame.Text.DrawIn(stack.Take(frame.Units(20f)), now.ToString("MMMM yyyy", CultureInfo.CurrentCulture),
            new TextStyle(FontRole.Body, frame.Theme.Palette.InkMuted));

        var hero = stack.Take(frame.Units(88f));
        CardChrome.DrawGold(frame, hero);
        var inset = hero.Inset(frame.Units(12f));
        frame.Text.DrawIn(inset.TopSlice(frame.Units(22f)), now.ToString("dddd", CultureInfo.CurrentCulture),
            new TextStyle(FontRole.CaptionStrong, frame.Theme.Palette.WarmAccent, TextAlign.Center));
        frame.Text.DrawIn(inset.Inset(new Edges(0f, frame.Units(22f), 0f, 0f)),
            now.Day.ToString(CultureInfo.InvariantCulture),
            new TextStyle(FontRole.Display, frame.Theme.Palette.Ink, TextAlign.Center));

        var week = stack.Take(frame.Units(64f));
        DrawWeek(frame, week, now);

        var bells = EorzeaTime.FromUnix(clock.UtcNow.ToUnixTimeSeconds());
        var eorzea = stack.Take(frame.Units(56f));
        CardChrome.Draw(frame, eorzea);
        frame.Text.DrawIn(eorzea.Inset(frame.Units(12f)).TopSlice(frame.Units(16f)), "Eorzea bells",
            new TextStyle(FontRole.CaptionStrong, frame.Theme.Palette.WarmAccent));
        frame.Text.DrawIn(eorzea.Inset(frame.Units(12f)).BottomSlice(frame.Units(24f)), bells.Format(),
            new TextStyle(FontRole.Title, frame.Theme.Palette.Ink));
    }

    private static void DrawWeek(in AppletFrame frame, Rect row, DateTimeOffset now)
    {
        var start = now.Date.AddDays(-(int)now.DayOfWeek);
        var gap = frame.Units(4f);
        var width = (row.Width - gap * 6f) / 7f;
        for (var index = 0; index < 7; index++)
        {
            var day = start.AddDays(index);
            var cell = Rect.FromSize(new Vector2(row.Min.X + index * (width + gap), row.Min.Y),
                new Vector2(width, row.Height));
            var today = day.Date == now.Date;
            CardChrome.Draw(frame, cell, today ? 2f : 1f);
            var label = day.ToString("ddd", CultureInfo.CurrentCulture);
            var mark = label.Length > 0 ? label[..1] : "?";
            frame.Text.DrawIn(cell.TopSlice(frame.Units(18f)), mark,
                new TextStyle(FontRole.Caption, frame.Theme.Palette.InkMuted, TextAlign.Center));
            frame.Text.DrawIn(cell.BottomSlice(frame.Units(28f)), day.Day.ToString(CultureInfo.InvariantCulture),
                new TextStyle(FontRole.BodyStrong, today ? frame.Theme.Palette.Accent : frame.Theme.Palette.Ink,
                    TextAlign.Center));
        }
    }
}
