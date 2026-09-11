using System.Globalization;

namespace Linkpearl.Destinations;

internal static class UnixAgo
{
    public static string Format(long unixSeconds, DateTimeOffset now)
    {
        if (unixSeconds <= 0)
        {
            return string.Empty;
        }

        DateTimeOffset then;
        try
        {
            then = DateTimeOffset.FromUnixTimeSeconds(unixSeconds);
        }
        catch (ArgumentOutOfRangeException)
        {
            return string.Empty;
        }

        var delta = now - then;
        if (delta.TotalSeconds < 45)
        {
            return "Now";
        }

        if (delta.TotalMinutes < 60)
        {
            return ((int)delta.TotalMinutes).ToString(CultureInfo.InvariantCulture) + "m";
        }

        if (delta.TotalHours < 24)
        {
            return ((int)delta.TotalHours).ToString(CultureInfo.InvariantCulture) + "h";
        }

        return ((int)delta.TotalDays).ToString(CultureInfo.InvariantCulture) + "d";
    }

    public static string Format(DateTimeOffset then, DateTimeOffset now)
    {
        if (then == DateTimeOffset.MinValue)
        {
            return string.Empty;
        }

        try
        {
            return Format(then.ToUnixTimeSeconds(), now);
        }
        catch (ArgumentOutOfRangeException)
        {
            return string.Empty;
        }
    }
}
