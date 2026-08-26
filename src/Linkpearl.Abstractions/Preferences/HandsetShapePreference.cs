using Linkpearl.Chassis;

namespace Linkpearl.Preferences;

// In-memory only for now, same as DisplayPreferences (no settings-persistence layer exists yet).
// Shared between HandsetWindow (which reads both every frame to size the window, and writes
// ScaleStep from corner-drag) and whatever UI controls let a player pick a size or form
// explicitly, so dragging and an explicit control never fight over two separate sources of truth.
public sealed class HandsetShapePreference
{
    private float scaleStep;
    private HandsetForm form;

    public HandsetShapePreference(float initialScaleStep, HandsetForm initialForm)
    {
        scaleStep = initialScaleStep;
        form = initialForm;
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

    public HandsetForm Form
    {
        get => form;
        set
        {
            if (form == value)
            {
                return;
            }

            form = value;
            Changed?.Invoke();
        }
    }
}
