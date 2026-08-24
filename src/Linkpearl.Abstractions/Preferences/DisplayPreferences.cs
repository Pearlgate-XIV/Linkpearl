namespace Linkpearl.Preferences;

// In-memory only for now: no settings-persistence layer exists yet (see docs/STATUS.md), so this
// resets every launch. Shared between whichever applet exposes a control for it and whatever
// reads it to change how it draws (the shell's clock format, for now).
public sealed class DisplayPreferences
{
    private bool use24HourClock;

    public event Action? Changed;

    public bool Use24HourClock
    {
        get => use24HourClock;
        set
        {
            if (use24HourClock == value)
            {
                return;
            }

            use24HourClock = value;
            Changed?.Invoke();
        }
    }
}
