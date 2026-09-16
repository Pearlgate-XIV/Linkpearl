using Linkpearl.Chassis;

namespace Linkpearl.Device.Chassis;

// The Linkpearl silhouette: a hairline frame around the glass, a near-flush side rail, and a
// screen with square corners rather than an iOS-style squircle. One "weight" value drives the
// metal and glass bands together so every finish stays proportional as the handset resizes.
public readonly struct ChassisMetrics
{
    private const float RailFraction = 0.012f;
    private const float ScreenCornerFraction = 0f;

    public float RailWidth { get; }
    public float FrameWidth { get; }
    public float GlassWidth { get; }
    public float BodyCornerRadius { get; }

    private ChassisMetrics(float railWidth, float frameWidth, float glassWidth, float bodyCornerRadius)
    {
        RailWidth = railWidth;
        FrameWidth = frameWidth;
        GlassWidth = glassWidth;
        BodyCornerRadius = bodyCornerRadius;
    }

    public static ChassisMetrics For(HandsetFinish finish, float deviceWidth)
    {
        var weight = FrameWeight(finish);
        var frame = weight.Frame * deviceWidth;
        var glass = weight.Glass * deviceWidth;
        // ScreenCornerFraction is zero, so the body radius equals just the bezel stack: the
        // screen itself ends up sharp-cornered once Inset() eats the frame and glass bands.
        var bodyRadius = ScreenCornerFraction * deviceWidth + frame + glass;
        return new ChassisMetrics(RailFraction * deviceWidth, frame, glass, bodyRadius);
    }

    public static ChassisMetrics ForOuterWidth(HandsetFinish finish, float outerWidth) =>
        For(finish, outerWidth / (1f - 2f * RailFraction));

    public static ChassisMetrics Reference => For(HandsetFinish.Crystal,
        HandsetSizeCatalog.SizeFor(HandsetForm.Phone, HandsetSizeCatalog.DefaultStep).X);

    private static (float Frame, float Glass) FrameWeight(HandsetFinish finish) => finish switch
    {
        HandsetFinish.Etched => (0.028f, 0.008f),
        _ => (0.0065f, 0.0055f),
    };
}
