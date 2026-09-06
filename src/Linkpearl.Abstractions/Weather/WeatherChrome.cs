using System.Globalization;
using Linkpearl.Applets;
using Linkpearl.Geometry;
using Linkpearl.Layout;
using Linkpearl.Painting;
using Linkpearl.Platform;
using Linkpearl.Time;

namespace Linkpearl.Weather;

public static class WeatherChrome
{
    public const int HourCells = 6;
    public const int ForecastHours = 24;

    public static void Dock(in AppletFrame frame, Rect area, string place, string condition, uint iconId,
        EorzeaTime bells, IReadOnlyList<WeatherWindow> hours)
    {
        var night = SkyChrome.IsNight(bells);
        var ink = Ink(night);
        var hush = Hush(night);
        var radius = SkyChrome.Corner(frame, area);
        SkyChrome.Paint(frame, area, condition, night, radius);

        var inset = area.Inset(new Edges(frame.Units(14f), frame.Units(10f), frame.Units(14f), frame.Units(8f)));
        var stack = new Stack(inset, StackAxis.Vertical, frame.Units(2f));
        DrawPlace(frame, stack.Take(frame.Units(16f)), place, ink, false);
        var hero = stack.Take(MathF.Max(frame.Units(36f), inset.Height * 0.34f));
        SkyChrome.Hero(frame, hero.RightSlice(MathF.Min(hero.Height + frame.Units(8f), hero.Width * 0.42f)), condition,
            night);
        DrawHeroTime(frame, hero.Inset(new Edges(0f, 0f, hero.Height * 0.46f, 0f)), bells, ink, true);
        DrawHiLo(frame, stack.Take(frame.Units(16f)), hush, condition);
        DrawStrip(frame, stack.TakeRemaining(), hours, bells, ink, hush, compact: true);
    }

    public static float App(in AppletFrame frame, Rect page, string place, string condition, uint iconId,
        EorzeaTime bells, IReadOnlyList<WeatherWindow> hours, IReadOnlyList<WeatherWindow> runs, string nextSky)
    {
        var night = SkyChrome.IsNight(bells);
        var ink = Ink(night);
        var hush = Hush(night);
        var stack = new Stack(page, StackAxis.Vertical, frame.Units(10f));
        DrawToolbar(frame, stack.Take(frame.Units(22f)), night);
        DrawPlace(frame, stack.Take(frame.Units(20f)), place, ink, true);
        SkyChrome.Hero(frame, stack.Take(frame.Units(108f)), condition, night);
        DrawHeroTime(frame, stack.Take(frame.Units(64f)), bells, ink, false);
        frame.Text.DrawIn(stack.Take(frame.Units(20f)), condition,
            new TextStyle(FontRole.Body, ink, TextAlign.Center));
        DrawHiLo(frame, stack.Take(frame.Units(18f)), hush, null);
        DrawHourlyCard(frame, stack.Take(frame.Units(124f)), hours, bells, night, ink, hush);
        DrawDailyCard(frame, stack.Take(frame.Units(28f + Math.Min(6, runs.Count) * 36f)), runs, bells, night, ink,
            hush);
        DrawTiles(frame, stack.Take(frame.Units(168f)), bells, condition, nextSky, night, ink, hush);
        DrawPager(frame, stack.Take(frame.Units(14f)), night);
        _ = iconId;
        return page.Height - stack.Remaining.Height;
    }

    public static Vector4 Ink(bool night) =>
        night ? new Vector4(1f, 1f, 1f, 0.96f) : new Vector4(0.10f, 0.14f, 0.20f, 0.94f);

    public static Vector4 Hush(bool night) =>
        night ? new Vector4(1f, 1f, 1f, 0.70f) : new Vector4(0.22f, 0.28f, 0.36f, 0.78f);

    public static void Glass(in AppletFrame frame, Rect area, bool night)
    {
        var fill = night
            ? new Vector4(0.10f, 0.12f, 0.16f, 0.52f)
            : new Vector4(1f, 1f, 1f, 0.78f);
        var edge = night
            ? new Vector4(1f, 1f, 1f, 0.10f)
            : new Vector4(1f, 1f, 1f, 0.55f);
        frame.Paint.Fill(area, fill, frame.Units(22f));
        frame.Paint.Stroke(area, edge, frame.Theme.Metrics.Hairline, frame.Units(22f));
    }

    public static string Twelve(int hour)
    {
        var wrapped = ((hour % 24) + 24) % 24;
        var twelve = wrapped % 12;
        if (twelve == 0)
        {
            twelve = 12;
        }

        return twelve.ToString(CultureInfo.InvariantCulture) + (wrapped >= 12 ? "PM" : "AM");
    }

    private static void DrawToolbar(in AppletFrame frame, Rect row, bool night)
    {
        var ink = Hush(night);
        var burger = row.LeftSlice(frame.Units(22f));
        for (var index = 0; index < 3; index++)
        {
            var y = burger.Min.Y + burger.Height * (0.28f + index * 0.18f);
            frame.Paint.Line(new Vector2(burger.Min.X + frame.Units(4f), y),
                new Vector2(burger.Min.X + frame.Units(16f), y), ink, frame.Units(1.6f));
        }

        var more = row.RightSlice(frame.Units(22f));
        for (var index = 0; index < 3; index++)
        {
            frame.Paint.FillCircle(new Vector2(more.Center.X, more.Min.Y + more.Height * (0.28f + index * 0.22f)),
                frame.Units(1.5f), ink);
        }
    }

    private static void DrawPlace(in AppletFrame frame, Rect row, string place, Vector4 ink, bool center)
    {
        var pin = center
            ? Rect.FromSize(new Vector2(row.Center.X - frame.Units(8f) - frame.Text.Measure(place, FontRole.BodyStrong).X * 0.5f,
                row.Min.Y), new Vector2(frame.Units(14f), row.Height))
            : row.LeftSlice(frame.Units(14f));
        SkyChrome.Pin(frame.Paint, pin, ink);
        var copy = center
            ? row
            : row.Inset(new Edges(frame.Units(16f), 0f, 0f, 0f));
        frame.Text.DrawEllipsized(copy, place,
            new TextStyle(FontRole.BodyStrong, ink, center ? TextAlign.Center : TextAlign.Left));
    }

    private static void DrawHeroTime(in AppletFrame frame, Rect area, EorzeaTime bells, Vector4 ink, bool dock)
    {
        var text = bells.Format();
        frame.Text.DrawFitted(area, text, new TextStyle(FontRole.Display, ink, dock ? TextAlign.Left : TextAlign.Center));
        var stamp = dock
            ? area.RightSlice(frame.Units(22f)).BottomSlice(frame.Units(12f))
            : Rect.FromSize(new Vector2(area.Center.X + area.Width * 0.28f, area.Min.Y + frame.Units(8f)),
                new Vector2(frame.Units(28f), frame.Units(14f)));
        frame.Text.DrawIn(stamp, "ET",
            new TextStyle(FontRole.CaptionStrong, ink with { W = ink.W * 0.72f }, TextAlign.Left, scale: 0.86f));
    }

    private static void DrawHiLo(in AppletFrame frame, Rect row, Vector4 hush, string? condition)
    {
        var line = "H:18   L:06";
        if (!string.IsNullOrEmpty(condition))
        {
            line = condition + "   " + line;
        }

        frame.Text.DrawIn(row, line, new TextStyle(FontRole.Caption, hush, TextAlign.Center));
    }

    private static void DrawHourlyCard(in AppletFrame frame, Rect card, IReadOnlyList<WeatherWindow> hours,
        EorzeaTime bells, bool night, Vector4 ink, Vector4 hush)
    {
        Glass(frame, card, night);
        var inner = card.Inset(new Edges(frame.Units(12f), frame.Units(10f), frame.Units(12f), frame.Units(8f)));
        var stack = new Stack(inner, StackAxis.Vertical, frame.Units(4f));
        frame.Text.DrawIn(stack.Take(frame.Units(16f)), "Hourly forecast",
            new TextStyle(FontRole.CaptionStrong, hush));
        DrawStrip(frame, stack.TakeRemaining(), hours, bells, ink, hush, compact: false);
    }

    private static void DrawStrip(in AppletFrame frame, Rect area, IReadOnlyList<WeatherWindow> hours,
        EorzeaTime bells, Vector4 ink, Vector4 hush, bool compact)
    {
        if (area.Height < frame.Units(18f) || area.Width < frame.Units(70f))
        {
            return;
        }

        var width = area.Width / HourCells;
        for (var index = 0; index < HourCells; index++)
        {
            var cell = Rect.FromSize(new Vector2(area.Min.X + width * index, area.Min.Y),
                new Vector2(width, area.Height));
            var when = bells.AddHours(index);
            var window = index < hours.Count ? hours[index] : default;
            var on = index == 0;
            frame.Text.DrawIn(cell.TopSlice(frame.Units(14f)), on ? "Now" : Twelve(when.Hour),
                new TextStyle(FontRole.Caption, on ? ink : hush, TextAlign.Center, scale: 0.82f));
            var icon = compact
                ? cell.Inset(new Edges(frame.Units(4f), frame.Units(14f), frame.Units(4f), frame.Units(2f)))
                : cell.Inset(new Edges(frame.Units(6f), frame.Units(16f), frame.Units(6f), frame.Units(18f)));
            SkyChrome.Icon(frame, icon, window.Name, window.IconId, when);
            if (!compact)
            {
                frame.Text.DrawIn(cell.BottomSlice(frame.Units(16f)),
                    string.IsNullOrEmpty(window.Name) ? "—" : Short(window.Name),
                    new TextStyle(FontRole.Caption, hush, TextAlign.Center, scale: 0.80f));
            }
        }
    }

    private static void DrawDailyCard(in AppletFrame frame, Rect card, IReadOnlyList<WeatherWindow> runs,
        EorzeaTime bells, bool night, Vector4 ink, Vector4 hush)
    {
        Glass(frame, card, night);
        var inner = card.Inset(new Edges(frame.Units(12f), frame.Units(10f), frame.Units(12f), frame.Units(8f)));
        var stack = new Stack(inner, StackAxis.Vertical, 0f);
        frame.Text.DrawIn(stack.Take(frame.Units(16f)), "10-day forecast",
            new TextStyle(FontRole.CaptionStrong, hush));
        var show = Math.Min(runs.Count, 6);
        for (var index = 0; index < show; index++)
        {
            var run = runs[index];
            var row = stack.Take(frame.Units(36f));
            var when = index == 0
                ? "Today"
                : Twelve(EorzeaTime.FromUnix(run.Starts.ToUnixTimeSeconds()).Hour);
            frame.Text.DrawIn(row.LeftSlice(frame.Units(46f)), when,
                new TextStyle(FontRole.CaptionStrong, index == 0 ? ink : hush));
            SkyChrome.Icon(frame,
                Rect.FromSize(new Vector2(row.Min.X + frame.Units(46f), row.Min.Y + frame.Units(4f)),
                    new Vector2(frame.Units(28f), row.Height - frame.Units(8f))), run.Name, run.IconId, bells);
            frame.Text.DrawEllipsized(
                row.Inset(new Edges(frame.Units(78f), 0f, frame.Units(54f), 0f)),
                string.IsNullOrEmpty(run.Name) ? "—" : run.Name, new TextStyle(FontRole.Caption, ink));
            DrawRange(frame, row.RightSlice(frame.Units(50f)).Inset(new Edges(0f, frame.Units(14f))), index, show,
                night);
        }
    }

    private static void DrawRange(in AppletFrame frame, Rect area, int index, int count, bool night)
    {
        if (area.Width < frame.Units(16f))
        {
            return;
        }

        var track = new Vector4(night ? 0.35f : 0.72f, night ? 0.40f : 0.78f, night ? 0.50f : 0.88f, 0.55f);
        var fill = new Vector4(1f, 0.78f, 0.28f, 0.92f);
        frame.Paint.Fill(area, track, area.Height * 0.5f);
        var start = count <= 1 ? 0f : index / (float)count;
        var span = count <= 1 ? 1f : 1f / count;
        var bar = Rect.FromSize(new Vector2(area.Min.X + area.Width * start, area.Min.Y),
            new Vector2(area.Width * span, area.Height));
        frame.Paint.Fill(bar, fill, area.Height * 0.5f);
    }

    private static void DrawTiles(in AppletFrame frame, Rect area, EorzeaTime bells, string condition, string nextSky,
        bool night, Vector4 ink, Vector4 hush)
    {
        var gap = frame.Units(8f);
        var cellW = (area.Width - gap) * 0.5f;
        var cellH = (area.Height - gap) * 0.5f;
        Tile(frame, Rect.FromSize(area.Min, new Vector2(cellW, cellH)), "Sunrise", "Dawn", "06:00", night, ink, hush);
        Tile(frame, Rect.FromSize(new Vector2(area.Min.X + cellW + gap, area.Min.Y), new Vector2(cellW, cellH)),
            "Sunset", "Dusk", "18:00", night, ink, hush);
        Tile(frame, Rect.FromSize(new Vector2(area.Min.X, area.Min.Y + cellH + gap), new Vector2(cellW, cellH)),
            "UV index", SkyChrome.Uv(bells), SkyChrome.Phase(bells), night, ink, hush);
        Tile(frame,
            Rect.FromSize(new Vector2(area.Min.X + cellW + gap, area.Min.Y + cellH + gap), new Vector2(cellW, cellH)),
            "Precipitation", SkyChrome.Precip(condition),
            nextSky.Length > 0 ? nextSky : "Steady skies", night, ink, hush);
    }

    private static void Tile(in AppletFrame frame, Rect area, string title, string value, string detail, bool night,
        Vector4 ink, Vector4 hush)
    {
        Glass(frame, area, night);
        var inner = area.Inset(frame.Units(12f));
        var stack = new Stack(inner, StackAxis.Vertical, frame.Units(4f));
        frame.Text.DrawIn(stack.Take(frame.Units(14f)), title.ToUpperInvariant(),
            new TextStyle(FontRole.CaptionStrong, hush, scale: 0.86f));
        frame.Text.DrawEllipsized(stack.Take(frame.Units(22f)), value, new TextStyle(FontRole.BodyStrong, ink));
        frame.Text.DrawEllipsized(stack.TakeRemaining(), detail, new TextStyle(FontRole.Caption, hush));
    }

    private static void DrawPager(in AppletFrame frame, Rect row, bool night)
    {
        var ink = Ink(night);
        var hush = Hush(night);
        var center = row.Center;
        frame.Paint.FillCircle(center, frame.Units(2.4f), ink);
        frame.Paint.FillCircle(center - new Vector2(frame.Units(10f), 0f), frame.Units(2f), hush with { W = hush.W * 0.5f });
        frame.Paint.FillCircle(center + new Vector2(frame.Units(10f), 0f), frame.Units(2f), hush with { W = hush.W * 0.5f });
    }

    private static string Short(string name) =>
        name.Length <= 8 ? name : name.Split(' ', 2, StringSplitOptions.TrimEntries)[0];
}
