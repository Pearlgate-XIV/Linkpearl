using Linkpearl.Geometry;
using Linkpearl.Input;
using Linkpearl.Painting;
using Linkpearl.Theming;

namespace Linkpearl.Device.Shell;

// Right-edge, vertically centered handle. Tapping it carousels the content: destinations slide
// left and the apps screen slides in from the right. The handle stays on the right and the
// chevron points at the page you will land on.
public sealed class AppsDock
{
    private const float TriggerWidthUnits = 18f;
    private const float TriggerHeightUnits = 72f;
    private const float SlideSeconds = 0.28f;

    private bool showingApps;
    private float slide;

    public bool OnApps => showingApps;

    public bool IsOpen => showingApps || slide > 0.004f;

    public float Shift => Ease(slide);

    public void Close() => showingApps = false;

    public void Open() => showingApps = true;

    public bool DrawHandle(IPaintSurface paint, IInputProbe input, ITheme theme, Rect screen, float scale,
        float deltaSeconds, bool reduceMotion)
    {
        if (reduceMotion)
        {
            slide = showingApps ? 1f : 0f;
        }
        else
        {
            StepSlide(deltaSeconds);
        }

        var trigger = HandleArea(screen, scale);
        var onApps = Shift > 0.5f;
        var handleFill = theme.Palette.SurfaceOverlay with { W = onApps ? 0.86f : 0.50f };
        var handleInk = (onApps ? theme.Palette.Accent : theme.Palette.Ink) with { W = onApps ? 1f : 0.50f };
        paint.Fill(trigger, handleFill, trigger.Height * 0.5f, Corner.Left);
        DrawChevron(paint, trigger, handleInk, pointLeft: onApps);

        if (!input.ConsumeClick(trigger))
        {
            return false;
        }

        showingApps = !showingApps;
        return true;
    }

    private static Rect HandleArea(Rect screen, float scale)
    {
        var width = TriggerWidthUnits * scale;
        var height = TriggerHeightUnits * scale;
        var origin = new Vector2(screen.Max.X - width, screen.Center.Y - height * 0.5f);
        return Rect.FromSize(origin, new Vector2(width, height));
    }

    private void StepSlide(float deltaSeconds)
    {
        var target = showingApps ? 1f : 0f;
        var delta = MathF.Max(deltaSeconds, 0f) / SlideSeconds;
        if (slide < target)
        {
            slide = MathF.Min(target, slide + delta);
        }
        else if (slide > target)
        {
            slide = MathF.Max(target, slide - delta);
        }
    }

    private static float Ease(float value) => value * value * (3f - 2f * value);

    private static void DrawChevron(IPaintSurface paint, Rect trigger, Vector4 color, bool pointLeft)
    {
        var center = trigger.Center;
        var wing = trigger.Width * 0.16f;
        var rise = trigger.Height * 0.14f;
        var thickness = MathF.Max(1.4f, trigger.Height * 0.05f);
        var dir = pointLeft ? -1f : 1f;
        var tip = new Vector2(center.X + wing * dir, center.Y);
        var top = new Vector2(center.X - wing * dir, center.Y - rise);
        var bottom = new Vector2(center.X - wing * dir, center.Y + rise);
        paint.Line(top, tip, color, thickness);
        paint.Line(bottom, tip, color, thickness);
    }
}
