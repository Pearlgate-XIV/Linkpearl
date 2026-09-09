
namespace Linkpearl.Chassis;

// Base unit follows the bundled chassis art (phone 517×1008, tablet 710×987), scaled to an
// 800-tall window so size steps stay in the same neighborhood as before.
public static class HandsetSizeCatalog
{
    private static readonly Vector2 PhoneBase = new(517f / 1008f * 800f, 800f);
    private static readonly Vector2 TabletBase = new(710f / 987f * 800f, 800f);

    private static readonly float[] Steps = { 0.80f, 1.00f, 1.25f };
    private static readonly string[] Labels = { "S", "M", "L" };

    public const float DefaultStep = 1.000f;

    // Settings S is 0.80. High Dalamud UI scale still needs a lower floor so the
    // window can fit the game viewport.
    public const float FloorScale = 0.35f;

    // Free drag may grow past the last settings preset, but never past the display.
    public const float FreeCeiling = 8f;

    public static IReadOnlyList<float> ScaleSteps => Steps;

    public static IReadOnlyList<string> StepLabels => Labels;

    public static float MinScale => Steps[0];

    public static float MaxScale => Steps[^1];

    public static Vector2 BaseUnits(HandsetForm form) => form == HandsetForm.Tablet ? TabletBase : PhoneBase;

    public static Vector2 BaseUnits(HandsetForm form, HandsetCase casing)
    {
        if (casing == HandsetCase.Android && form != HandsetForm.Tablet)
        {
            return new Vector2(ChassisCatalog.Android.Aspect * 800f, 800f);
        }

        return BaseUnits(form);
    }

    public static Vector2 SizeFor(HandsetForm form, float scale) => SizeFor(form, HandsetCase.Pearl, scale);

    public static Vector2 SizeFor(HandsetForm form, HandsetCase casing, float scale)
    {
        var clamped = Math.Clamp(scale, FloorScale, FreeCeiling);
        return BaseUnits(form, casing) * clamped;
    }

    public static float SnapToStep(float scale)
    {
        var closestIndex = 0;
        var closestDelta = float.MaxValue;
        for (var index = 0; index < Steps.Length; index++)
        {
            var delta = MathF.Abs(Steps[index] - scale);
            if (delta < closestDelta)
            {
                closestDelta = delta;
                closestIndex = index;
            }
        }

        return Steps[closestIndex];
    }

    public static int StepIndex(float scale)
    {
        var snapped = SnapToStep(scale);
        return Array.IndexOf(Steps, snapped);
    }

    public static float ClampFree(float scale, float maxFit) =>
        Math.Clamp(scale, FloorScale, Math.Clamp(maxFit, FloorScale, FreeCeiling));

    public static float FitScale(HandsetForm form, HandsetCase casing, Vector2 maxPixels, float dip, bool landscape)
    {
        var units = BaseUnits(form, casing) * MathF.Max(dip, 0.01f);
        var sized = landscape ? new Vector2(units.Y, units.X) : units;
        if (sized.X < 1f || sized.Y < 1f || maxPixels.X < 1f || maxPixels.Y < 1f)
        {
            return FloorScale;
        }

        return MathF.Max(FloorScale, MathF.Min(maxPixels.X / sized.X, maxPixels.Y / sized.Y));
    }
}
