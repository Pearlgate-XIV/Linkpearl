using System.Globalization;
using Linkpearl.Time;

namespace Linkpearl.Device.Time;

public static class HandsetClockText
{
    public static string Format(IClock clock, bool use24Hour) => use24Hour
        ? clock.Now.ToString("HH:mm", CultureInfo.CurrentCulture)
        : clock.Now.ToString("h:mm tt", CultureInfo.CurrentCulture);
}
