using System.Globalization;

namespace Linkpearl.Net;

public static class VenueTimes
{
    public static IReadOnlyList<DateTimeOffset> Opens(IReadOnlyList<VenueWeekMark> week, DateTimeOffset now, int weeks)
    {
        if (week.Count == 0 || weeks < 0)
        {
            return [];
        }

        var found = new List<DateTimeOffset>();
        for (var index = 0; index < week.Count; index++)
        {
            Collect(week[index], now, weeks, found);
        }

        found.Sort();
        var unique = new List<DateTimeOffset>(found.Count);
        for (var index = 0; index < found.Count; index++)
        {
            if (index > 0 && found[index] == found[index - 1])
            {
                continue;
            }

            unique.Add(found[index]);
        }

        return unique;
    }

    public static string WeekLine(IReadOnlyList<VenueWeekMark> week)
    {
        if (week.Count == 0)
        {
            return string.Empty;
        }

        var parts = new List<string>(week.Count);
        for (var index = 0; index < week.Count; index++)
        {
            var line = Label(week[index]);
            if (line.Length == 0 || parts.Contains(line))
            {
                continue;
            }

            parts.Add(line);
        }

        return string.Join("  ·  ", parts);
    }

    public static string Label(VenueWeekMark mark)
    {
        var start = new DateTime(2000, 1, 1, Math.Clamp(mark.StartHour, 0, 23), Math.Clamp(mark.StartMinute, 0, 59), 0);
        return mark.Day.ToString()[..3] + " " + start.ToString("h:mm tt", CultureInfo.CurrentCulture);
    }

    private static void Collect(VenueWeekMark mark, DateTimeOffset now, int weeks, List<DateTimeOffset> found)
    {
        if (!TryZone(mark.TimeZone, out var zone))
        {
            return;
        }

        var local = TimeZoneInfo.ConvertTime(now, zone);
        for (var week = 0; week <= weeks; week++)
        {
            var delta = ((int)mark.Day - (int)local.DayOfWeek + 7) % 7 + week * 7;
            var date = local.Date.AddDays(delta);
            if (mark.Commencing is { } from && date.Date < from.LocalDateTime.Date)
            {
                continue;
            }

            if (!Fits(mark, date))
            {
                continue;
            }

            var begin = date.Date.AddHours(Math.Clamp(mark.StartHour, 0, 23))
                .AddMinutes(Math.Clamp(mark.StartMinute, 0, 59));
            if (mark.StartNextDay)
            {
                begin = begin.AddDays(1);
            }

            DateTimeOffset start;
            try
            {
                var utc = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(begin, DateTimeKind.Unspecified), zone);
                start = new DateTimeOffset(utc, TimeSpan.Zero);
            }
            catch (ArgumentException)
            {
                continue;
            }

            if (start <= now)
            {
                continue;
            }

            found.Add(start);
        }
    }

    private static bool Fits(VenueWeekMark mark, DateTime date)
    {
        var every = mark.Interval <= 1 ? 1 : mark.Interval;
        if (every <= 1 || mark.Commencing is null)
        {
            return true;
        }

        var origin = mark.Commencing.Value.LocalDateTime.Date;
        var span = (int)Math.Floor((date.Date - origin).TotalDays / 7d);
        return span >= 0 && span % every == 0;
    }

    private static bool TryZone(string id, out TimeZoneInfo zone)
    {
        zone = TimeZoneInfo.Utc;
        if (string.IsNullOrWhiteSpace(id))
        {
            return false;
        }

        try
        {
            zone = TimeZoneInfo.FindSystemTimeZoneById(id.Trim());
            return true;
        }
        catch (TimeZoneNotFoundException)
        {
            return false;
        }
        catch (InvalidTimeZoneException)
        {
            return false;
        }
    }
}
