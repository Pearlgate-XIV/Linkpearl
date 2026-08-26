
namespace Linkpearl.Chassis;

// Base unit is a 450x800 handset: portrait, close to a real communicator's proportions rather
// than a tall modern phone. Six snap steps carry every layout unit through the same ratio,
// spanning roughly 290-880 in width (M sits exactly at the 450 base) — a comparably wide range
// to what a free-resizing window would allow, just reachable through six named stops instead of
// a continuous drag.
public static class HandsetSizeCatalog
{
    private static readonly Vector2 PhoneBase = new(450f, 800f);
    private static readonly Vector2 TabletBase = new(600f, 800f);

    private static readonly float[] Steps = { 0.65f, 0.80f, 1.00f, 1.25f, 1.55f, 1.95f };
    private static readonly string[] Labels = { "XS", "S", "M", "L", "XL", "XXL" };

    public const float DefaultStep = 1.000f;

    public static IReadOnlyList<float> ScaleSteps => Steps;

    public static IReadOnlyList<string> StepLabels => Labels;

    public static float MinScale => Steps[0];

    public static float MaxScale => Steps[^1];

    public static Vector2 BaseUnits(HandsetForm form) => form == HandsetForm.Tablet ? TabletBase : PhoneBase;

    public static Vector2 SizeFor(HandsetForm form, float scale)
    {
        var clamped = Math.Clamp(scale, MinScale, MaxScale);
        return BaseUnits(form) * clamped;
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
}
