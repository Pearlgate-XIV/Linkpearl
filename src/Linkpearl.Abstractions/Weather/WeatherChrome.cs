using System.Globalization;
using Linkpearl.Applets;
using Linkpearl.Geometry;
using Linkpearl.Layout;
using Linkpearl.Painting;
using Linkpearl.Platform;
using Linkpearl.Preferences;
using Linkpearl.Time;

namespace Linkpearl.Weather;

public static class WeatherChrome
{
    public const int HourCells = 6;
    public const int ForecastHours = 24;

    public static IReadOnlyList<WeatherWindow> AlignNow(IReadOnlyList<WeatherWindow> hours, SkyLook look)
    {
        if (hours.Count == 0 || look.Name.Length == 0)
        {
            return hours;
        }

        var copy = new WeatherWindow[hours.Count];
        for (var index = 0; index < hours.Count; index++)
        {
            copy[index] = hours[index];
        }

        var first = copy[0];
        copy[0] = new WeatherWindow(look.Name, look.IconId != 0 ? look.IconId : first.IconId, first.Starts, first.Ends);
        return copy;
    }

    public static void Dock(in AppletFrame frame, Rect area, string place, string condition, uint iconId,
        EorzeaTime bells, IReadOnlyList<WeatherWindow> hours)
    {
        var night = SkyChrome.IsNight(bells);
        var ink = Ink(night);
        var hush = Hush(night);
        var radius = SkyChrome.Corner(frame, area);
        SkyChrome.Paint(frame, area, condition, night, radius);

        var inset = area.Inset(new Edges(frame.Units(14f), frame.Units(10f), frame.Units(14f), frame.Units(8f)));
        var stack = new LayoutFlow(inset, StackAxis.Vertical, frame.Units(2f));
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
        return Forecast(frame, page, place, condition, iconId, bells, hours, runs, nextSky);
    }

    public static float Forecast(in AppletFrame frame, Rect page, string place, string condition, uint iconId,
        EorzeaTime bells, IReadOnlyList<WeatherWindow> hours, IReadOnlyList<WeatherWindow> runs, string nextSky)
    {
        var night = SkyChrome.IsNight(bells);
        var ink = Ink(night);
        var hush = Hush(night);
        var stack = new LayoutFlow(page, StackAxis.Vertical, frame.Units(8f));
        DrawBack(frame, stack.Take(frame.Units(22f)), ink);
        frame.Text.DrawIn(stack.Take(frame.Units(22f)), place,
            new TextStyle(FontRole.Title, ink, TextAlign.Center));
        SkyChrome.Hero(frame, stack.Take(frame.Units(108f)), condition, night);
        frame.Text.DrawIn(stack.Take(frame.Units(24f)), condition,
            new TextStyle(FontRole.Title, ink, TextAlign.Center));
        if (nextSky.Length > 0)
        {
            frame.Text.DrawIn(stack.Take(frame.Units(16f)), nextSky,
                new TextStyle(FontRole.Caption, hush, TextAlign.Center));
        }

        DrawNearCard(frame, stack.Take(frame.Units(118f)), hours, night, ink, hush);
        DrawRunCard(frame, stack.Take(frame.Units(28f + Math.Min(5, Math.Max(1, runs.Count)) * 36f)), runs, bells,
            night, ink, hush);
        _ = iconId;
        return page.Height - stack.Remaining.Height;
    }

    public static float Control(in AppletFrame frame, Rect page, EorzeaTime bells, IReadOnlyList<SkyChoice> choices,
        ISkyDesk sky, bool companion, bool night)
    {
        var ink = Ink(night);
        var hush = Hush(night);
        var stack = new LayoutFlow(page, StackAxis.Vertical, frame.Units(10f));
        DrawBack(frame, stack.Take(frame.Units(22f)), ink);
        DrawTimeCard(frame, stack.Take(frame.Units(148f)), bells, sky, night, ink, hush);
        DrawWeatherGrid(frame, stack.Take(WeatherGridHeight(frame, choices.Count + 1)), choices, sky, night, ink,
            hush);
        var reset = stack.Take(frame.Units(40f));
        Glass(frame, reset, night);
        frame.Text.DrawIn(reset, PhoneLanguages.T("weather.reset"),
            new TextStyle(FontRole.BodyStrong, ink, TextAlign.Center));
        if (frame.Input.ConsumeClick(reset))
        {
            sky.Reset();
        }

        frame.Text.DrawWrapped(stack.Take(frame.Units(48f)),
            companion ? PhoneLanguages.T("weather.note.companion") : PhoneLanguages.T("weather.note"),
            new TextStyle(FontRole.Caption, hush, TextAlign.Center));
        return page.Height - stack.Remaining.Height;
    }

    public static int Tabs(in AppletFrame frame, Rect area, int selected, bool night)
    {
        var ink = Ink(night);
        var hush = Hush(night);
        Glass(frame, area, night);
        var pad = frame.Units(6f);
        var inner = area.Inset(new Edges(pad, pad, pad, pad));
        var cell = inner.Width * 0.5f;
        var next = selected;
        if (Tab(frame, Rect.FromSize(inner.Min, new Vector2(cell, inner.Height)), PhoneLanguages.T("weather.forecast"),
                selected == 0, night, ink, hush, forecast: true))
        {
            next = 0;
        }

        if (Tab(frame, Rect.FromSize(new Vector2(inner.Min.X + cell, inner.Min.Y), new Vector2(cell, inner.Height)),
                PhoneLanguages.T("weather.control"), selected == 1, night, ink, hush, forecast: false))
        {
            next = 1;
        }

        return next;
    }

    private static void DrawBack(in AppletFrame frame, Rect row, Vector4 ink)
    {
        var hit = row.LeftSlice(frame.Units(28f));
        var c = hit.Center;
        var s = frame.Units(6f);
        frame.Paint.Line(c + new Vector2(s * 0.4f, -s), c + new Vector2(-s * 0.6f, 0f), ink, frame.Units(1.8f));
        frame.Paint.Line(c + new Vector2(-s * 0.6f, 0f), c + new Vector2(s * 0.4f, s), ink, frame.Units(1.8f));
        if (frame.Input.ConsumeClick(hit))
        {
            frame.Router?.Back();
        }
    }

    private static void DrawNearCard(in AppletFrame frame, Rect card, IReadOnlyList<WeatherWindow> hours, bool night,
        Vector4 ink, Vector4 hush)
    {
        Glass(frame, card, night);
        var inner = card.Inset(new Edges(frame.Units(12f), frame.Units(10f), frame.Units(12f), frame.Units(10f)));
        var stack = new LayoutFlow(inner, StackAxis.Vertical, frame.Units(6f));
        frame.Text.DrawIn(stack.Take(frame.Units(14f)), PhoneLanguages.T("weather.near"),
            new TextStyle(FontRole.CaptionStrong, hush));
        var strip = stack.TakeRemaining();
        var count = Math.Min(5, Math.Max(1, hours.Count));
        var width = strip.Width / count;
        var now = DateTimeOffset.UtcNow;
        for (var index = 0; index < count; index++)
        {
            var cell = Rect.FromSize(new Vector2(strip.Min.X + width * index, strip.Min.Y),
                new Vector2(width, strip.Height));
            var window = index < hours.Count ? hours[index] : default;
            var stamp = index == 0
                ? PhoneLanguages.T("weather.now")
                : Ago(window.Starts, now);
            var on = index == 0;
            if (on)
            {
                frame.Paint.Fill(cell.Inset(frame.Units(3f)), night
                    ? new Vector4(1f, 1f, 1f, 0.10f)
                    : new Vector4(0.12f, 0.18f, 0.28f, 0.08f), frame.Units(12f));
            }

            frame.Text.DrawIn(cell.TopSlice(frame.Units(14f)), stamp,
                new TextStyle(FontRole.Caption, on ? ink : hush, TextAlign.Center, scale: 0.82f));
            SkyChrome.Icon(frame, cell.Inset(new Edges(frame.Units(6f), frame.Units(16f), frame.Units(6f),
                frame.Units(2f))), window.Name, window.IconId,
                window.Starts == default ? default : EorzeaTime.FromUnix(window.Starts.ToUnixTimeSeconds()));
        }
    }

    private static void DrawRunCard(in AppletFrame frame, Rect card, IReadOnlyList<WeatherWindow> runs,
        EorzeaTime bells, bool night, Vector4 ink, Vector4 hush)
    {
        Glass(frame, card, night);
        var inner = card.Inset(new Edges(frame.Units(12f), frame.Units(10f), frame.Units(12f), frame.Units(8f)));
        var stack = new LayoutFlow(inner, StackAxis.Vertical, 0f);
        frame.Text.DrawIn(stack.Take(frame.Units(16f)), PhoneLanguages.T("weather.forecast.list"),
            new TextStyle(FontRole.CaptionStrong, hush));
        var show = Math.Min(runs.Count, 5);
        if (show == 0)
        {
            frame.Text.DrawIn(stack.Take(frame.Units(28f)), "—", new TextStyle(FontRole.Caption, hush));
            return;
        }

        for (var index = 0; index < show; index++)
        {
            var run = runs[index];
            var row = stack.Take(frame.Units(36f));
            var when = index == 0
                ? PhoneLanguages.T("weather.now")
                : SkyChrome.ClockLabel(EorzeaTime.FromUnix(run.Starts.ToUnixTimeSeconds()).Hour) + ":00";
            frame.Text.DrawIn(row.LeftSlice(frame.Units(58f)), when,
                new TextStyle(FontRole.CaptionStrong, index == 0 ? ink : hush));
            SkyChrome.Icon(frame,
                Rect.FromSize(new Vector2(row.Min.X + frame.Units(58f), row.Min.Y + frame.Units(4f)),
                    new Vector2(frame.Units(28f), row.Height - frame.Units(8f))), run.Name, run.IconId, bells);
            frame.Text.DrawEllipsized(row.Inset(new Edges(frame.Units(92f), 0f, 0f, 0f)),
                string.IsNullOrEmpty(run.Name) ? "—" : run.Name, new TextStyle(FontRole.Caption, ink));
        }
    }

    private static void DrawTimeCard(in AppletFrame frame, Rect card, EorzeaTime bells, ISkyDesk sky, bool night,
        Vector4 ink, Vector4 hush)
    {
        Glass(frame, card, night);
        var inner = card.Inset(new Edges(frame.Units(12f), frame.Units(10f), frame.Units(12f), frame.Units(10f)));
        var stack = new LayoutFlow(inner, StackAxis.Vertical, frame.Units(6f));
        frame.Text.DrawIn(stack.Take(frame.Units(14f)), PhoneLanguages.T("weather.time"),
            new TextStyle(FontRole.CaptionStrong, hush));
        var shown = sky.TimeLocked
            ? new EorzeaTime(sky.LockedMinute / 60, sky.LockedMinute % 60)
            : bells;
        var clock = stack.Take(frame.Units(28f));
        frame.Text.DrawIn(clock.LeftSlice(clock.Width * 0.5f), shown.Format(),
            new TextStyle(FontRole.Title, ink));
        frame.Text.DrawIn(clock.RightSlice(clock.Width * 0.5f),
            sky.TimeLocked ? PhoneLanguages.T("weather.locked") : PhoneLanguages.T("weather.natural"),
            new TextStyle(FontRole.Caption, hush, TextAlign.Right));
        var track = stack.Take(frame.Units(28f));
        DrawMinuteSlide(frame, track, bells, sky, night, ink);
        var chips = stack.Take(frame.Units(32f));
        var chipW = chips.Width / 4f;
        Stamp(frame, Slice(chips, 0, chipW), PhoneLanguages.T("weather.dawn"), 6 * 60, sky, night, ink, hush);
        Stamp(frame, Slice(chips, 1, chipW), PhoneLanguages.T("weather.noon"), 12 * 60, sky, night, ink, hush);
        Stamp(frame, Slice(chips, 2, chipW), PhoneLanguages.T("weather.dusk"), 18 * 60, sky, night, ink, hush);
        Stamp(frame, Slice(chips, 3, chipW), PhoneLanguages.T("weather.midnight"), 0, sky, night, ink, hush);
    }

    private static void DrawMinuteSlide(in AppletFrame frame, Rect track, EorzeaTime bells, ISkyDesk sky, bool night,
        Vector4 ink)
    {
        var rail = new Rect(new Vector2(track.Min.X, track.Center.Y - frame.Units(2.2f)),
            new Vector2(track.Max.X, track.Center.Y + frame.Units(2.2f)));
        frame.Paint.Fill(rail, night ? new Vector4(1f, 1f, 1f, 0.20f) : new Vector4(0.12f, 0.16f, 0.22f, 0.22f),
            rail.Height);
        var minute = sky.TimeLocked ? sky.LockedMinute : bells.Hour * 60 + bells.Minute;
        var t = Math.Clamp(minute / 1439f, 0f, 1f);
        var knob = new Vector2(track.Min.X + track.Width * t, track.Center.Y);
        frame.Paint.FillCircle(knob, frame.Units(8f), ink);
        var hit = track.Inset(new Edges(0f, -frame.Units(8f)));
        if (frame.Input.PressedInside(hit) || (frame.Input.IsHeld() && hit.Contains(frame.Input.Cursor)))
        {
            var next = (int)MathF.Round(Math.Clamp((frame.Input.Cursor.X - track.Min.X) / MathF.Max(1f, track.Width),
                0f, 1f) * 1439f);
            sky.TryLockTime(next);
        }
    }

    private static void Stamp(in AppletFrame frame, Rect area, string label, int minute, ISkyDesk sky, bool night,
        Vector4 ink, Vector4 hush)
    {
        var on = sky.TimeLocked && Near(sky.LockedMinute, minute);
        var inner = area.Inset(new Edges(frame.Units(2f), 0f));
        frame.Paint.Fill(inner, on
            ? (night ? new Vector4(1f, 1f, 1f, 0.20f) : new Vector4(0.12f, 0.18f, 0.28f, 0.14f))
            : (night ? new Vector4(1f, 1f, 1f, 0.06f) : new Vector4(0.12f, 0.16f, 0.22f, 0.06f)), frame.Units(10f));
        frame.Text.DrawIn(inner, label, new TextStyle(FontRole.CaptionStrong, on ? ink : hush, TextAlign.Center));
        if (frame.Input.PressedInside(inner))
        {
            sky.TryLockTime(minute);
        }
    }

    private static bool Near(int locked, int stamp)
    {
        var delta = Math.Abs(locked - stamp);
        return delta <= 8 || delta >= 1440 - 8;
    }

    private static float WeatherGridHeight(in AppletFrame frame, int count)
    {
        var rows = Math.Max(1, (int)MathF.Ceiling(count / 3f));
        return frame.Units(18f) + rows * frame.Units(72f) + (rows - 1) * frame.Units(8f);
    }

    private static void DrawWeatherGrid(in AppletFrame frame, Rect area, IReadOnlyList<SkyChoice> choices,
        ISkyDesk sky, bool night, Vector4 ink, Vector4 hush)
    {
        var stack = new LayoutFlow(area, StackAxis.Vertical, frame.Units(8f));
        frame.Text.DrawIn(stack.Take(frame.Units(14f)), PhoneLanguages.T("weather.sky"),
            new TextStyle(FontRole.CaptionStrong, hush));
        var grid = stack.TakeRemaining();
        var total = choices.Count + 1;
        var cols = 3;
        var rows = Math.Max(1, (int)MathF.Ceiling(total / (float)cols));
        var gap = frame.Units(8f);
        var cellW = (grid.Width - gap * (cols - 1)) / cols;
        var cellH = (grid.Height - gap * (rows - 1)) / rows;
        for (var index = 0; index < total; index++)
        {
            var col = index % cols;
            var row = index / cols;
            var cell = Rect.FromSize(
                new Vector2(grid.Min.X + col * (cellW + gap), grid.Min.Y + row * (cellH + gap)),
                new Vector2(cellW, cellH));
            if (index == 0)
            {
                DrawSkyCell(frame, cell, 0, PhoneLanguages.T("weather.natural"), 0, !sky.WeatherLocked, sky, night,
                    ink, hush);
                continue;
            }

            var choice = choices[index - 1];
            DrawSkyCell(frame, cell, choice.Id, choice.Name, choice.IconId,
                sky.WeatherLocked && sky.LockedWeather == choice.Id, sky, night, ink, hush);
        }
    }

    private static void DrawSkyCell(in AppletFrame frame, Rect area, byte id, string name, uint icon, bool on,
        ISkyDesk sky, bool night, Vector4 ink, Vector4 hush)
    {
        Glass(frame, area, night);
        if (on)
        {
            frame.Paint.Stroke(area, ink, frame.Units(1.8f), frame.Units(22f));
        }

        var iconBox = area.TopSlice(area.Height * 0.62f).Inset(frame.Units(4f));
        if (id == 0)
        {
            SkyMarks.Draw(frame.Paint, iconBox, "natural", night);
        }
        else
        {
            SkyMarks.Draw(frame.Paint, iconBox, name, night: false);
        }
        frame.Text.DrawEllipsized(area.BottomSlice(frame.Units(18f)).Inset(new Edges(frame.Units(4f), 0f)), name,
            new TextStyle(FontRole.Caption, on ? ink : hush, TextAlign.Center, scale: 0.82f));
        if (frame.Input.ConsumeClick(area))
        {
            if (id == 0)
            {
                sky.UnlockWeather();
            }
            else
            {
                sky.TryLockWeather(id);
            }
        }
    }

    private static bool Tab(in AppletFrame frame, Rect area, string label, bool on, bool night, Vector4 ink,
        Vector4 hush, bool forecast)
    {
        if (on)
        {
            frame.Paint.Fill(area.Inset(frame.Units(2f)),
                night ? new Vector4(1f, 1f, 1f, 0.16f) : new Vector4(0.10f, 0.16f, 0.26f, 0.10f),
                frame.Units(16f));
        }

        var mark = Rect.FromSize(new Vector2(area.Center.X - frame.Units(13f), area.Min.Y + frame.Units(4f)),
            new Vector2(frame.Units(26f), frame.Units(22f)));
        if (forecast)
        {
            SkyMarks.ForecastTab(frame.Paint, mark, on, night);
        }
        else
        {
            SkyMarks.ControlTab(frame.Paint, mark, on, night);
        }

        frame.Text.DrawIn(area.BottomSlice(frame.Units(16f)), label,
            new TextStyle(FontRole.CaptionStrong, on ? ink : hush, TextAlign.Center));
        return frame.Input.PressedInside(area);
    }

    private static Rect Slice(Rect area, int index, float width) =>
        Rect.FromSize(new Vector2(area.Min.X + width * index, area.Min.Y), new Vector2(width, area.Height));

    private static string Ago(DateTimeOffset starts, DateTimeOffset now)
    {
        var minutes = Math.Max(0, (int)Math.Round((starts - now).TotalMinutes));
        return minutes.ToString(CultureInfo.InvariantCulture) + "m";
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
        var stack = new LayoutFlow(inner, StackAxis.Vertical, frame.Units(4f));
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
        var stack = new LayoutFlow(inner, StackAxis.Vertical, 0f);
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
        var stack = new LayoutFlow(inner, StackAxis.Vertical, frame.Units(4f));
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
