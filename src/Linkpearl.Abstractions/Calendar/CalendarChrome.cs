using System.Globalization;
using Linkpearl.Applets;
using Linkpearl.Geometry;
using Linkpearl.Layout;
using Linkpearl.Painting;
using Linkpearl.Time;

namespace Linkpearl.Calendar;

public readonly record struct CalendarDayLine(string Id, string Title, string When, string Kind);

public struct CalendarAppHit
{
    public int MonthStep;
    public bool JumpToday;
    public DateTime? PickedDay;
    public bool AddEvent;
    public bool AddReminder;
    public string? OpenId;
}

public static class CalendarChrome
{
    public static void Dock(in AppletFrame frame, Rect area, DateTimeOffset now, EorzeaTime bells)
    {
        var night = Night(now);
        var radius = SkyCorner(frame, area);
        Wash(frame, area, night, radius);
        var ink = Ink(night);
        var hush = Hush(night);
        var accent = Accent(night);
        var inset = area.Inset(new Edges(frame.Units(12f), frame.Units(10f), frame.Units(12f), frame.Units(8f)));
        var wide = inset.Width >= frame.Units(168f);
        var weekH = frame.Units(28f);
        var body = inset.Height > weekH + frame.Units(36f)
            ? inset.Inset(new Edges(0f, 0f, 0f, weekH + frame.Units(4f)))
            : inset;
        if (wide)
        {
            var split = body.Width * 0.46f;
            DrawHero(frame, body.LeftSlice(split), now, ink, hush, accent, true);
            DrawMonth(frame, body.RightSlice(body.Width - split - frame.Units(8f)), now, now.Date, ink, hush, accent,
                compact: true);
        }
        else
        {
            DrawHero(frame, body, now, ink, hush, accent, true);
        }

        if (body.Max.Y < inset.Max.Y - frame.Units(2f))
        {
            DrawWeek(frame, inset.BottomSlice(weekH), now, ink, hush, accent);
        }

        _ = bells;
    }

    public static float App(in AppletFrame frame, Rect page, DateTimeOffset now, DateTime month, DateTime selected,
        IReadOnlyList<int> markedDays, IReadOnlyList<CalendarDayLine> dayLines, EorzeaTime bells,
        out CalendarAppHit hit)
    {
        hit = default;
        var night = Night(now);
        var ink = Ink(night);
        var hush = Hush(night);
        var accent = Accent(night);
        var stack = new Stack(page, StackAxis.Vertical, frame.Units(10f));
        var monthStep = 0;
        var jumpToday = false;
        DrawToolbar(frame, stack.Take(frame.Units(36f)), month, now, ink, accent, ref monthStep, ref jumpToday);
        hit.MonthStep = monthStep;
        hit.JumpToday = jumpToday;
        DrawWeek(frame, stack.Take(frame.Units(28f)), now, selected, ink, hush, accent, out var weekDay);
        if (weekDay is { } fromWeek)
        {
            hit.PickedDay = fromWeek;
        }

        var rows = MonthRows(month);
        DrawMonth(frame, stack.Take(frame.Units(28f + rows * 36f)), now, month, selected, markedDays, ink, hush,
            accent, compact: false, pickDays: true, out var monthDay);
        if (monthDay is { } fromMonth)
        {
            hit.PickedDay = fromMonth;
        }

        DrawAgenda(frame, stack.Take(frame.Units(92f) + dayLines.Count * frame.Units(40f)), selected, dayLines, bells,
            ink, hush, accent, out hit.AddEvent, out hit.AddReminder, out hit.OpenId);
        return page.Height - stack.Remaining.Height;
    }

    public static Vector4 Ink(bool night)
    {
        _ = night;
        return new Vector4(1f, 0.97f, 0.96f, 0.96f);
    }

    public static Vector4 Hush(bool night)
    {
        _ = night;
        return new Vector4(1f, 0.92f, 0.90f, 0.62f);
    }

    public static Vector4 Accent(bool night)
    {
        _ = night;
        return new Vector4(1f, 0.46f, 0.42f, 1f);
    }

    public static bool Night(DateTimeOffset now)
    {
        _ = now;
        return true;
    }

    public static void Paint(in AppletFrame frame, Rect area, DateTimeOffset now)
    {
        _ = now;
        AppGround.Paint(frame, area, "calendar");
    }

    private static void DrawHero(in AppletFrame frame, Rect area, DateTimeOffset now, Vector4 ink, Vector4 hush,
        Vector4 accent, bool dock)
    {
        var stack = new Stack(area, StackAxis.Vertical, frame.Units(2f));
        frame.Text.DrawEllipsized(stack.Take(frame.Units(16f)),
            now.ToString("dddd", CultureInfo.CurrentCulture),
            new TextStyle(FontRole.CaptionStrong, hush, dock ? TextAlign.Left : TextAlign.Center));
        var number = stack.Take(MathF.Max(frame.Units(40f), area.Height * 0.52f));
        frame.Text.DrawFitted(number, now.Day.ToString(CultureInfo.InvariantCulture),
            new TextStyle(FontRole.Display, accent, dock ? TextAlign.Left : TextAlign.Center));
        frame.Text.DrawEllipsized(stack.Take(frame.Units(16f)),
            now.ToString("MMMM yyyy", CultureInfo.CurrentCulture),
            new TextStyle(FontRole.Caption, ink, dock ? TextAlign.Left : TextAlign.Center));
        if (stack.Remaining.Height >= frame.Units(14f))
        {
            frame.Text.DrawEllipsized(stack.Take(frame.Units(14f)), "No events today",
                new TextStyle(FontRole.Caption, hush, dock ? TextAlign.Left : TextAlign.Center));
        }
    }

    private static void DrawToolbar(in AppletFrame frame, Rect row, DateTime month, DateTimeOffset now, Vector4 ink,
        Vector4 accent, ref int monthStep, ref bool jumpToday)
    {
        var next = row.RightSlice(frame.Units(28f));
        var prev = Rect.FromSize(new Vector2(next.Min.X - frame.Units(28f), row.Min.Y), next.Size);
        var away = month.Year != now.Year || month.Month != now.Month;
        var chip = away
            ? Rect.FromSize(new Vector2(prev.Min.X - frame.Units(56f), row.Min.Y + frame.Units(6f)),
                new Vector2(frame.Units(50f), row.Height - frame.Units(12f)))
            : default;
        var titleRight = (away ? chip.Min.X : prev.Min.X) - frame.Units(8f);
        frame.Text.DrawEllipsized(new Rect(row.Min, new Vector2(titleRight, row.Max.Y)),
            month.ToString("MMMM yyyy", CultureInfo.CurrentCulture), new TextStyle(FontRole.Title, ink));
        DrawChevron(frame, prev, ink, left: true);
        DrawChevron(frame, next, ink, left: false);
        if (away)
        {
            frame.Paint.Fill(chip, accent with { W = 0.16f }, chip.Height * 0.5f);
            frame.Text.DrawIn(chip, "Today",
                new TextStyle(FontRole.CaptionStrong, accent, TextAlign.Center, 1f, 0.86f));
            if (frame.Input.ConsumeClick(chip))
            {
                jumpToday = true;
                return;
            }
        }

        if (frame.Input.ConsumeClick(prev))
        {
            monthStep = -1;
        }
        else if (frame.Input.ConsumeClick(next))
        {
            monthStep = 1;
        }
    }

    private static void DrawChevron(in AppletFrame frame, Rect area, Vector4 ink, bool left)
    {
        var size = MathF.Min(area.Width, area.Height) * 0.22f;
        var c = area.Center;
        var dir = left ? -1f : 1f;
        var thick = MathF.Max(1.4f, frame.Units(1.6f));
        frame.Paint.Line(c + new Vector2(dir * -size, 0f), c + new Vector2(dir * size * 0.15f, -size), ink, thick);
        frame.Paint.Line(c + new Vector2(dir * -size, 0f), c + new Vector2(dir * size * 0.15f, size), ink, thick);
        if (frame.Input.IsHovering(area))
        {
            frame.Paint.FillCircle(c, MathF.Min(area.Width, area.Height) * 0.42f, ink with { W = 0.08f });
        }
    }

    private static void DrawWeek(in AppletFrame frame, Rect row, DateTimeOffset now, Vector4 ink, Vector4 hush,
        Vector4 accent) =>
        DrawWeek(frame, row, now, now.Date, ink, hush, accent, out _);

    private static void DrawWeek(in AppletFrame frame, Rect row, DateTimeOffset now, DateTime selected, Vector4 ink,
        Vector4 hush, Vector4 accent, out DateTime? picked)
    {
        picked = null;
        var start = now.Date.AddDays(-(int)now.DayOfWeek);
        var gap = frame.Units(3f);
        var width = (row.Width - gap * 6f) / 7f;
        for (var index = 0; index < 7; index++)
        {
            var day = start.AddDays(index);
            var cell = Rect.FromSize(new Vector2(row.Min.X + index * (width + gap), row.Min.Y),
                new Vector2(width, row.Height));
            var today = day.Date == now.Date;
            var chosen = day.Date == selected.Date;
            var label = day.ToString("ddd", CultureInfo.CurrentCulture);
            var mark = label.Length > 0 ? label[..1] : "?";
            var stack = new Stack(cell, StackAxis.Vertical, 0f);
            frame.Text.DrawIn(stack.Take(frame.Units(11f)), mark,
                new TextStyle(FontRole.Caption, hush, TextAlign.Center, 1f, 0.78f));
            var number = stack.TakeRemaining();
            var radius = MathF.Min(number.Width, number.Height) * 0.42f;
            if (today)
            {
                frame.Paint.FillCircle(number.Center, radius, accent);
            }
            else if (chosen)
            {
                frame.Paint.StrokeCircle(number.Center, radius, accent, MathF.Max(1.4f, frame.Units(1.6f)));
            }

            frame.Text.DrawIn(number, day.Day.ToString(CultureInfo.InvariantCulture),
                new TextStyle(FontRole.CaptionStrong, today ? new Vector4(1f, 1f, 1f, 0.96f) : ink, TextAlign.Center));
            if (frame.Input.ConsumeClick(cell))
            {
                picked = day.Date;
            }
        }
    }

    private static void DrawMonth(in AppletFrame frame, Rect area, DateTimeOffset now, DateTime month, Vector4 ink,
        Vector4 hush, Vector4 accent, bool compact) =>
        DrawMonth(frame, area, now, month, now.Date, [], ink, hush, accent, compact, false, out _);

    private static void DrawMonth(in AppletFrame frame, Rect area, DateTimeOffset now, DateTime month,
        DateTime selected, IReadOnlyList<int> markedDays, Vector4 ink, Vector4 hush, Vector4 accent, bool compact,
        bool pickDays, out DateTime? picked)
    {
        picked = null;
        if (area.Width < 8f || area.Height < 8f)
        {
            return;
        }

        var rows = MonthRows(month);
        var header = compact ? 0f : frame.Units(16f);
        var grid = area.Inset(new Edges(0f, header, 0f, 0f));
        var cellW = grid.Width / 7f;
        var cellH = grid.Height / (rows + (compact ? 1 : 0));
        if (compact)
        {
            for (var column = 0; column < 7; column++)
            {
                var cell = Rect.FromSize(new Vector2(grid.Min.X + column * cellW, grid.Min.Y), new Vector2(cellW, cellH));
                frame.Text.DrawIn(cell, WeekdayMark(column),
                    new TextStyle(FontRole.Caption, column == 0 ? accent with { W = 0.72f } : hush, TextAlign.Center, 1f,
                        0.78f));
            }

            grid = grid.Inset(new Edges(0f, cellH, 0f, 0f));
            cellH = rows > 0 ? grid.Height / rows : cellH;
        }
        else
        {
            for (var column = 0; column < 7; column++)
            {
                var cell = Rect.FromSize(new Vector2(area.Min.X + column * cellW, area.Min.Y),
                    new Vector2(cellW, header));
                frame.Text.DrawIn(cell, WeekdayMark(column),
                    new TextStyle(FontRole.CaptionStrong, column == 0 ? accent with { W = 0.80f } : hush,
                        TextAlign.Center, 1f, 0.84f));
            }
        }

        var first = new DateTime(month.Year, month.Month, 1);
        var lead = (int)first.DayOfWeek;
        var days = DateTime.DaysInMonth(month.Year, month.Month);
        for (var day = 1; day <= days; day++)
        {
            var index = lead + day - 1;
            var cell = Rect.FromSize(
                new Vector2(grid.Min.X + index % 7 * cellW, grid.Min.Y + index / 7 * cellH),
                new Vector2(cellW, cellH));
            var today = day == now.Day && month.Month == now.Month && month.Year == now.Year;
            var chosen = day == selected.Day && month.Month == selected.Month && month.Year == selected.Year;
            var marked = HasDay(markedDays, day);
            var radius = MathF.Min(cell.Width, cell.Height) * 0.36f;
            if (today)
            {
                frame.Paint.FillCircle(cell.Center, radius, accent);
            }
            else if (chosen)
            {
                frame.Paint.StrokeCircle(cell.Center, radius, accent, MathF.Max(1.4f, frame.Units(1.6f)));
            }

            frame.Text.DrawIn(cell, day.ToString(CultureInfo.InvariantCulture),
                new TextStyle(compact ? FontRole.Caption : FontRole.CaptionStrong,
                    today ? new Vector4(1f, 1f, 1f, 0.96f) : ink, TextAlign.Center, 1f, compact ? 0.86f : 1f));
            if (marked)
            {
                frame.Paint.FillCircle(new Vector2(cell.Center.X, cell.Max.Y - frame.Units(4f)),
                    MathF.Max(1.6f, frame.Units(2.2f)), today ? new Vector4(1f, 1f, 1f, 0.92f) : accent);
            }

            if (pickDays && frame.Input.ConsumeClick(cell))
            {
                picked = new DateTime(month.Year, month.Month, day);
            }
        }
    }

    private static void DrawAgenda(in AppletFrame frame, Rect area, DateTime selected,
        IReadOnlyList<CalendarDayLine> lines, EorzeaTime bells, Vector4 ink, Vector4 hush, Vector4 accent,
        out bool addEvent, out bool addReminder, out string? openId)
    {
        addEvent = false;
        addReminder = false;
        openId = null;
        var fill = new Vector4(0.12f, 0.08f, 0.10f, 0.48f);
        var radius = frame.Units(18f);
        frame.Paint.Fill(area, fill, radius);
        frame.Paint.Stroke(area, new Vector4(1f, 1f, 1f, 0.08f), frame.Theme.Metrics.Hairline, radius);
        var inset = area.Inset(new Edges(frame.Units(14f), frame.Units(10f), frame.Units(14f), frame.Units(10f)));
        var head = inset.TopSlice(frame.Units(18f));
        frame.Text.DrawIn(head.LeftSlice(head.Width * 0.62f),
            selected.ToString("ddd, MMM d", CultureInfo.CurrentCulture),
            new TextStyle(FontRole.CaptionStrong, accent));
        frame.Text.DrawIn(head.RightSlice(head.Width * 0.38f), bells.Format(),
            new TextStyle(FontRole.CaptionStrong, hush, TextAlign.Right));
        var actions = inset.BottomSlice(frame.Units(32f));
        var eventHit = actions.LeftSlice((actions.Width - frame.Units(8f)) * 0.5f);
        var remindHit = actions.RightSlice((actions.Width - frame.Units(8f)) * 0.5f);
        DrawAction(frame, eventHit, "Event", accent);
        DrawAction(frame, remindHit, "Reminder", accent);
        if (frame.Input.ConsumeClick(eventHit))
        {
            addEvent = true;
        }
        else if (frame.Input.ConsumeClick(remindHit))
        {
            addReminder = true;
        }

        var list = inset.Inset(new Edges(0f, frame.Units(22f), 0f, frame.Units(36f)));
        if (lines.Count == 0)
        {
            frame.Text.DrawIn(list.TopSlice(frame.Units(18f)), "Nothing on this day.",
                new TextStyle(FontRole.Caption, hush));
            return;
        }

        var rowH = frame.Units(36f);
        for (var index = 0; index < lines.Count; index++)
        {
            var row = Rect.FromSize(new Vector2(list.Min.X, list.Min.Y + index * rowH),
                new Vector2(list.Width, rowH - frame.Units(4f)));
            if (row.Max.Y > list.Max.Y + 0.5f)
            {
                break;
            }

            frame.Text.DrawEllipsized(row.TopSlice(frame.Units(16f)), lines[index].Title,
                new TextStyle(FontRole.CaptionStrong, ink));
            frame.Text.DrawEllipsized(row.BottomSlice(frame.Units(14f)),
                lines[index].When + " · " + lines[index].Kind, new TextStyle(FontRole.Caption, hush));
            if (frame.Input.ConsumeClick(row))
            {
                openId = lines[index].Id;
            }
        }
    }

    private static void DrawAction(in AppletFrame frame, Rect area, string label, Vector4 accent)
    {
        frame.Paint.Fill(area, accent with { W = 0.16f }, area.Height * 0.5f);
        frame.Text.DrawIn(area, label,
            new TextStyle(FontRole.CaptionStrong, accent, TextAlign.Center, 1f, 0.9f));
    }

    private static bool HasDay(IReadOnlyList<int> days, int day)
    {
        for (var index = 0; index < days.Count; index++)
        {
            if (days[index] == day)
            {
                return true;
            }
        }

        return false;
    }

    private static void Wash(in AppletFrame frame, Rect area, bool night, float radius)
    {
        _ = night;
        AppGround.Paint(frame, area, "calendar", radius);
    }

    private static float SkyCorner(in AppletFrame frame, Rect area) =>
        MathF.Max(frame.Units(18f), MathF.Min(area.Width, area.Height) * 0.12f);

    private static int MonthRows(DateTime month)
    {
        var first = new DateTime(month.Year, month.Month, 1);
        var lead = (int)first.DayOfWeek;
        return (lead + DateTime.DaysInMonth(month.Year, month.Month) + 6) / 7;
    }

    private static string WeekdayMark(int column)
    {
        var name = CultureInfo.CurrentCulture.DateTimeFormat.GetShortestDayName((DayOfWeek)column);
        return name.Length > 0 ? name : "?";
    }
}
