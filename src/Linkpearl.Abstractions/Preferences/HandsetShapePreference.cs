using Linkpearl.Chassis;

namespace Linkpearl.Preferences;

public sealed class HandsetShapePreference
{
    private float scaleStep;
    private float pocketScale;
    private HandsetForm form;
    private HandsetFinish finish;
    private HandsetCase casing;
    private bool positionLocked;
    private bool showLockTab = true;

    public HandsetShapePreference(float initialScaleStep, HandsetForm initialForm, bool positionLocked,
        float pocketScale = 1f, HandsetFinish finish = HandsetFinish.Crystal, bool showLockTab = true,
        HandsetCase casing = HandsetCase.Pearl)
    {
        scaleStep = initialScaleStep;
        form = initialForm;
        this.positionLocked = positionLocked;
        this.pocketScale = pocketScale;
        this.finish = finish;
        this.showLockTab = showLockTab;
        this.casing = casing;
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

    public float PocketScale
    {
        get => pocketScale;
        set
        {
            var snapped = SnapPocket(value);
            if (pocketScale.Equals(snapped))
            {
                return;
            }

            pocketScale = snapped;
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

    public HandsetFinish Finish
    {
        get => finish;
        set
        {
            if (finish == value)
            {
                return;
            }

            finish = value;
            Changed?.Invoke();
        }
    }

    public HandsetCase Case
    {
        get => casing;
        set
        {
            if (casing == value)
            {
                return;
            }

            casing = value;
            Changed?.Invoke();
        }
    }

    public bool PositionLocked
    {
        get => positionLocked;
        set
        {
            if (positionLocked == value)
            {
                return;
            }

            positionLocked = value;
            Changed?.Invoke();
        }
    }

    public bool ShowLockTab
    {
        get => showLockTab;
        set
        {
            if (showLockTab == value)
            {
                return;
            }

            showLockTab = value;
            Changed?.Invoke();
        }
    }

    public static readonly float[] PocketSteps = { 0.85f, 1f, 1.22f };

    public static readonly string[] PocketLabels = { "S", "M", "L" };

    public static float SnapPocket(float scale)
    {
        var closest = PocketSteps[0];
        var best = float.MaxValue;
        for (var index = 0; index < PocketSteps.Length; index++)
        {
            var delta = MathF.Abs(PocketSteps[index] - scale);
            if (delta < best)
            {
                best = delta;
                closest = PocketSteps[index];
            }
        }

        return closest;
    }

    public int PocketIndex()
    {
        var snapped = SnapPocket(pocketScale);
        for (var index = 0; index < PocketSteps.Length; index++)
        {
            if (PocketSteps[index].Equals(snapped))
            {
                return index;
            }
        }

        return 1;
    }
}
