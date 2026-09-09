using System.Globalization;

namespace Linkpearl.Time;

public static class ZoneClock
{
    public static bool TryWhen(string? id, DateTimeOffset now, DateTimeOffset utc, out DateTimeOffset when,
        out bool eorzea)
    {
        var resolved = WorldZones.Sanitize(id);
        eorzea = WorldZones.IsEorzea(resolved);
        if (eorzea)
        {
            when = utc;
            return true;
        }

        if (resolved.Length == 0)
        {
            when = now.ToLocalTime();
            return true;
        }

        try
        {
            when = TimeZoneInfo.ConvertTime(now, TimeZoneInfo.FindSystemTimeZoneById(resolved));
            return true;
        }
        catch (TimeZoneNotFoundException)
        {
            when = default;
            return false;
        }
        catch (InvalidTimeZoneException)
        {
            when = default;
            return false;
        }
    }

    public static string Stamp(string? id, bool use24Hour, DateTimeOffset? now = null, DateTimeOffset? utc = null)
    {
        var local = now ?? DateTimeOffset.Now;
        var universal = utc ?? DateTimeOffset.UtcNow;
        if (!TryWhen(id, local, universal, out var when, out var eorzea))
        {
            return string.Empty;
        }

        if (eorzea)
        {
            return EorzeaTime.FromUnix(universal.ToUnixTimeSeconds()).Format() + " ET";
        }

        return use24Hour
            ? when.ToString("HH:mm", CultureInfo.CurrentCulture)
            : when.ToString("h:mm tt", CultureInfo.CurrentCulture);
    }

    public static string Line(string? id, bool use24Hour, DateTimeOffset? now = null, DateTimeOffset? utc = null)
    {
        var stamp = Stamp(id, use24Hour, now, utc);
        if (stamp.Length == 0)
        {
            return string.Empty;
        }

        return stamp + " · " + WorldZones.CityOf(id);
    }
}
