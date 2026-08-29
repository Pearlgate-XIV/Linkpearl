namespace Linkpearl.Media;

// Local-clock darkness in [0, 1]: 0 is full day, 1 is full night. Dawn and dusk each get a
// one-hour ramp so a cross-fade has somewhere to go, rather than snapping at a single minute.
public static class DayNight
{
    private const float DawnStartMinutes = 6f * 60f;
    private const float DawnEndMinutes = 7f * 60f;
    private const float DuskStartMinutes = 18f * 60f;
    private const float DuskEndMinutes = 19f * 60f;

    public static float Darkness(DateTimeOffset localTime)
    {
        var minutes = localTime.Hour * 60f + localTime.Minute + localTime.Second / 60f;
        if (minutes < DawnStartMinutes || minutes >= DuskEndMinutes)
        {
            return 1f;
        }

        if (minutes >= DawnEndMinutes && minutes < DuskStartMinutes)
        {
            return 0f;
        }

        if (minutes < DawnEndMinutes)
        {
            return 1f - (minutes - DawnStartMinutes) / (DawnEndMinutes - DawnStartMinutes);
        }

        return (minutes - DuskStartMinutes) / (DuskEndMinutes - DuskStartMinutes);
    }
}
