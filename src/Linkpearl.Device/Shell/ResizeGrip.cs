using Linkpearl.Chassis;
using Linkpearl.Device.Chassis;
using Linkpearl.Geometry;
using Linkpearl.Input;

namespace Linkpearl.Device.Shell;

public enum ResizeCorner : sbyte
{
    None = -1,
    TopLeft = 0,
    TopRight = 1,
    BottomLeft = 2,
    BottomRight = 3,
}

// Corner-grip drag to resize: grab near any outer corner and drag away from center to scale up,
// toward center to scale down. Distance-ratio based so the grab point stays under the cursor.
// Snaps to the nearest HandsetSizeCatalog step only once the drag releases, so mid-drag motion
// stays continuous instead of stair-stepping.
public sealed class ResizeGrip
{
    private const float GripUnits = 18f;

    private bool dragging;
    private float startStep;
    private float startDistance;

    public bool IsDragging => dragging;

    public ResizeCorner Update(Rect window, IInputProbe input, float scale, ref float step)
    {
        var grip = GripUnits * scale;
        var pointer = input.Pointer;
        var hovered = HitCorner(window, grip, pointer);

        if (dragging)
        {
            if (input.IsHeld())
            {
                var distance = MathF.Max(Vector2.Distance(pointer, window.Center), 1f);
                step = Math.Clamp(startStep * (distance / startDistance), HandsetSizeCatalog.MinScale,
                    HandsetSizeCatalog.MaxScale);
                return hovered != ResizeCorner.None ? hovered : CornerFromPoint(window, pointer);
            }

            step = HandsetSizeCatalog.SnapToStep(step);
            dragging = false;
            return ResizeCorner.None;
        }

        if (hovered != ResizeCorner.None && input.WasPressed(GripArea(window, grip, hovered)))
        {
            dragging = true;
            startStep = step;
            startDistance = MathF.Max(Vector2.Distance(pointer, window.Center), 1f);
        }

        return hovered;
    }

    public static bool IsDiagonalNwse(ResizeCorner corner) => corner is ResizeCorner.TopLeft or ResizeCorner.BottomRight;

    private static ResizeCorner HitCorner(Rect window, float grip, Vector2 pointer)
    {
        for (var corner = ResizeCorner.TopLeft; corner <= ResizeCorner.BottomRight; corner++)
        {
            if (GripArea(window, grip, corner).Contains(pointer))
            {
                return corner;
            }
        }

        return ResizeCorner.None;
    }

    private static ResizeCorner CornerFromPoint(Rect window, Vector2 pointer)
    {
        var left = pointer.X < window.Center.X;
        var top = pointer.Y < window.Center.Y;
        return (left, top) switch
        {
            (true, true) => ResizeCorner.TopLeft,
            (false, true) => ResizeCorner.TopRight,
            (true, false) => ResizeCorner.BottomLeft,
            _ => ResizeCorner.BottomRight,
        };
    }

    private static Rect GripArea(Rect window, float grip, ResizeCorner corner) => corner switch
    {
        ResizeCorner.TopLeft => Rect.FromSize(window.Min, new Vector2(grip, grip)),
        ResizeCorner.TopRight => Rect.FromSize(new Vector2(window.Max.X - grip, window.Min.Y), new Vector2(grip, grip)),
        ResizeCorner.BottomLeft => Rect.FromSize(new Vector2(window.Min.X, window.Max.Y - grip), new Vector2(grip, grip)),
        ResizeCorner.BottomRight => Rect.FromSize(window.Max - new Vector2(grip, grip), new Vector2(grip, grip)),
        _ => Rect.Empty,
    };
}
