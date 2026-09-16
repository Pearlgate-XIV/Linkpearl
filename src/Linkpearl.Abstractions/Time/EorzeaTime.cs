using System.Globalization;

namespace Linkpearl.Time;

// Eorzea runs 20 and 4/7 times faster than Earth: one Eorzean day is 70 Earth minutes.
public readonly struct EorzeaTime
{
    public const double EarthToEorzea = 144.0 / 7.0;

    public int Hour { get; }
    public int Minute { get; }

    public EorzeaTime(int hour, int minute)
    {
        Hour = hour;
        Minute = minute;
    }

    public static EorzeaTime FromUnix(long unixSeconds) =>
        FromEorzeaSeconds(unixSeconds * EarthToEorzea);

    public static EorzeaTime FromEorzeaSeconds(double etSeconds)
    {
        var day = etSeconds % 86400.0;
        if (day < 0.0)
        {
            day += 86400.0;
        }

        var hour = (int)(day / 3600.0);
        var minute = (int)((day % 3600.0) / 60.0);
        return new EorzeaTime(hour, minute);
    }

    public EorzeaTime AddHours(int hours)
    {
        var total = ((Hour + hours) % 24 + 24) % 24;
        return new EorzeaTime(total, Minute);
    }

    public int HoursUntilNextWeather()
    {
        var used = Hour % 8;
        return used == 0 && Minute == 0 ? 8 : 8 - used;
    }

    public string Format() =>
        Hour.ToString("00", CultureInfo.InvariantCulture) + ":" +
        Minute.ToString("00", CultureInfo.InvariantCulture);
}