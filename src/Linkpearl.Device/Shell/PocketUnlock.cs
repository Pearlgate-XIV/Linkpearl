using Dalamud.Bindings.ImGui;
using Linkpearl.Geometry;
using Linkpearl.Input;
using Linkpearl.Painting;
using Linkpearl.Theming;

namespace Linkpearl.Device.Shell;

// Vertical slide-to-wake on the pocket face. Track size is locked to UI scale (DPI), not
// pocket window size, so a larger or smaller communicator keeps the same slider.
public sealed class PocketUnlock
{
    public const float WidthUnits = 24f;
    public const float HeightUnits = 104f;

    private const float Commit = 0.82f;
    private const float SpringSeconds = 0.18f;

    private float amount;
    private bool dragging;
    private float grab;

    public bool IsDragging => dragging;

    public void Reset()
    {
        amount = 0f;
        dragging = false;
        grab = 0f;
    }

    public static Rect TrackOn(Rect screen, float dip)
    {
        var width = WidthUnits * dip;
        var height = HeightUnits * dip;
        var clock = 24f * dip;
        var top = screen.Min.Y + clock + 8f * dip;
        var bottom = screen.Max.Y - 8f * dip;
        var y = top + MathF.Max(0f, (bottom - top - height) * 0.45f);
        return Rect.FromSize(new Vector2(screen.Center.X - width * 0.5f, y), new Vector2(width, height));
    }

    public static Rect HitOn(Rect screen, float dip) => TrackOn(screen, dip).Expand(3f * dip);

    public bool Draw(IPaintSurface paint, IInputProbe input, ITheme theme, Rect screen, float dip, float deltaSeconds,
        bool allowSlide)
    {
        var track = TrackOn(screen, dip);
        if (track.IsEmpty)
        {
            return false;
        }

        var pad = track.Width * 0.5f;
        var minY = track.Min.Y + pad;
        var maxY = track.Max.Y - pad;
        var travel = MathF.Max(maxY - minY, 1f);
        var hit = HitOn(screen, dip);

        if (allowSlide && !dragging && hit.Contains(input.Pointer) &&
            ImGui.IsMouseClicked(ImGuiMouseButton.Left))
        {
            dragging = true;
            var y = Scalar.Clamp(input.Pointer.Y, minY, maxY);
            amount = (maxY - y) / travel;
            grab = input.Pointer.Y - ThumbY(minY, maxY);
        }

        if (dragging && input.IsHeld())
        {
            var y = Scalar.Clamp(input.Pointer.Y - grab, minY, maxY);
            amount = (maxY - y) / travel;
        }
        else if (dragging)
        {
            dragging = false;
            if (amount >= Commit)
            {
                Reset();
                return true;
            }
        }
        else if (amount > 0.001f)
        {
            var step = MathF.Max(deltaSeconds, 0f) / SpringSeconds;
            amount = MathF.Max(0f, amount - step);
        }

        if (dragging && amount >= 0.97f)
        {
            Reset();
            return true;
        }

        var thumb = new Vector2(track.Center.X, ThumbY(minY, maxY));
        var radius = track.Width * 0.42f;
        paint.Stroke(track, theme.Palette.WarmAccent, MathF.Max(2f, 2f * dip), track.Width * 0.5f);
        paint.FillCircle(thumb, radius, new Vector4(1f, 1f, 1f, 1f));
        DrawUpArrow(paint, thumb, radius, theme.Palette.SurfaceSunken with { W = 1f });
        return false;
    }

    private float ThumbY(float minY, float maxY) => maxY + amount * (minY - maxY);

    private static void DrawUpArrow(IPaintSurface paint, Vector2 center, float radius, Vector4 color)
    {
        var thickness = MathF.Max(1.5f, radius * 0.16f);
        var rise = radius * 0.42f;
        var spread = radius * 0.32f;
        var tip = center + new Vector2(0f, -rise);
        var tail = center + new Vector2(0f, rise * 0.55f);
        paint.Line(tail, tip, color, thickness);
        paint.Line(tip, tip + new Vector2(-spread, spread * 0.95f), color, thickness);
        paint.Line(tip, tip + new Vector2(spread, spread * 0.95f), color, thickness);
    }
}
