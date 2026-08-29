using System.Globalization;
using Linkpearl.Preferences;
using Linkpearl.Time;

namespace Linkpearl.Device.Time;

public static class HandsetClockText
{
    public static string Format(IClock clock, DisplayPreferences display)
    {
        var local = FormatLocal(clock, display.Use24HourClock);
        if (display.ClockFace == ClockFace.Local)
        {
            return local;
        }

        var bells = EorzeaTime.FromUnix(clock.UtcNow.ToUnixTimeSeconds()).Format();
        return display.ClockFace == ClockFace.Eorzea ? bells : local + " · " + bells;
    }

    public static string FormatLocal(IClock clock, bool use24Hour) => use24Hour
        ? clock.Now.ToString("HH:mm", CultureInfo.CurrentCulture)
        : clock.Now.ToString("h:mm tt", CultureInfo.CurrentCulture);
}
