namespace Linkpearl.Preferences;

// In-memory only for now, same as DisplayPreferences (no settings-persistence layer exists yet).
// Shared between HandsetWindow (which both writes it from corner-drag and reads it every frame
// to size the window) and whatever UI control lets a player pick a size explicitly, so dragging
// and an explicit control never fight over two separate sources of truth.
public sealed class HandsetSizePreference
{
    private float scaleStep;

    public HandsetSizePreference(float initialScaleStep)
    {
        scaleStep = initialScaleStep;
    }

    public event Action? Changed;

    public float ScaleStep
    {
        get => scaleStep;
        set
        {
            if (scaleStep.Equals(value))
            {
                return;
            }

            scaleStep = value;
            Changed?.Invoke();
        }
    }
}
