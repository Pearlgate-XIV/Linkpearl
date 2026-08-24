using Linkpearl.Device.Chassis;

namespace Linkpearl.Device.Chassis;

// Base unit is a 450x800 slate: portrait, close to a real communicator's proportions rather
// than a tall modern phone. Six snap steps carry every layout unit through the same ratio.
public static class HandsetSizeCatalog
{
    private static readonly Vector2 PocketBase = new(450f, 800f);
    private static readonly Vector2 SlateBase = new(600f, 800f);

    private static readonly float[] Steps = { 0.778f, 0.889f, 1.000f, 1.111f, 1.250f, 1.389f };
    private static readonly string[] Labels = { "XS", "S", "M", "L", "XL", "XXL" };

    public const float DefaultStep = 1.000f;

    public static IReadOnlyList<float> ScaleSteps => Steps;

    public static IReadOnlyList<string> StepLabels => Labels;

    public static float MinScale => Steps[0];

    public static float MaxScale => Steps[^1];

    public static Vector2 BaseUnits(HandsetForm form) => form == HandsetForm.Slate ? SlateBase : PocketBase;

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
