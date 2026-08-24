using Linkpearl.Geometry;

namespace Linkpearl.Device.Chassis;

// Derives the three nested rects of a handset (body -> glass -> screen) from a metrics profile.
// Corner radii shrink band by band so the corner centers stay coincident even when the screen
// itself is perfectly square (radius zero).
public readonly struct ChassisGeometry
{
    public readonly Rect Body;
    public readonly Rect Glass;
    public readonly Rect Screen;
    public readonly float BodyRadius;
    public readonly float GlassRadius;
    public readonly float ScreenRadius;

    private ChassisGeometry(Rect body, float frameWidth, float glassWidth, float bodyRadius)
    {
        var limit = MathF.Max(MathF.Min(body.Width, body.Height) * 0.5f, 0f);
        var frame = Clamp(frameWidth, limit);
        var glass = Clamp(glassWidth, limit - frame);
        Body = body;
        Glass = body.Inset(frame);
        Screen = Glass.Inset(glass);
        BodyRadius = Math.Clamp(MathF.Max(bodyRadius, frame + glass), 0f, limit);
        GlassRadius = MathF.Max(BodyRadius - frame, 0f);
        ScreenRadius = MathF.Max(GlassRadius - glass, 0f);
    }

    public static ChassisGeometry Outer(Rect window, float railWidth, ChassisMetrics metrics)
    {
        var body = window.Inset(railWidth);
        return new ChassisGeometry(body, metrics.FrameWidth, metrics.GlassWidth, metrics.BodyCornerRadius);
    }

    public static ChassisGeometry ForBody(Rect body, HandsetFinish finish)
    {
        var metrics = ChassisMetrics.For(finish, body.Width);
        return new ChassisGeometry(body, metrics.FrameWidth, metrics.GlassWidth, metrics.BodyCornerRadius);
    }

    public static ChassisGeometry Blend(Rect body, ChassisGeometry from, ChassisGeometry to, float fraction)
    {
        var frame = Lerp(from.Glass.Min.X - from.Body.Min.X, to.Glass.Min.X - to.Body.Min.X, fraction);
        var glass = Lerp(from.Screen.Min.X - from.Glass.Min.X, to.Screen.Min.X - to.Glass.Min.X, fraction);
        var radius = Lerp(from.BodyRadius, to.BodyRadius, fraction);
        return new ChassisGeometry(body, frame, glass, radius);
    }

    private static float Clamp(float width, float limit) => width <= 0f || limit <= 0f ? 0f : MathF.Min(width, limit);

    private static float Lerp(float from, float to, float fraction) => from + (to - from) * fraction;
}
