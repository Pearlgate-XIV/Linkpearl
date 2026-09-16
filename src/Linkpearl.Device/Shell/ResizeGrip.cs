using Dalamud.Bindings.ImGui;
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

// Corner drag keeps the opposite corner fixed and scales from that anchor. Measuring
// against the moving window center made size chase itself and shake.
public sealed class ResizeGrip
{
    private const float GripUnits = 28f;
    private const float DragSlop = 8f;

    private bool dragging;
    private bool committed;
    private ResizeCorner dragCorner = ResizeCorner.None;
    private float startStep;
    private float startDistance;
    private Vector2 anchor;
    private Vector2 pressAt;

    public bool IsDragging => dragging && committed;

    public bool Holding => dragging;

    public bool JustReleased { get; private set; }

    public ResizeCorner ActiveCorner => dragging && committed ? dragCorner : ResizeCorner.None;

    public Vector2 Anchor => anchor;

    public ResizeCorner Update(Rect window, Rect shell, Rect glass, IInputProbe input, float scale, float caseRadius,
        bool roundCorners, float minStep, float maxStep, ref float step, bool canResize)
    {
        JustReleased = false;
        if (!canResize)
        {
            Cancel();
            return ResizeCorner.None;
        }

        var pointer = input.Cursor;
        var board = shell.IsEmpty ? window : shell;
        var hovered = HitCorner(board, GripUnits * scale, caseRadius, roundCorners, pointer);
        if (!dragging && OnGlass(glass, pointer))
        {
            hovered = ResizeCorner.None;
        }

        if (dragging)
        {
            if (ImGui.IsMouseDown(ImGuiMouseButton.Left))
            {
                if (!committed)
                {
                    var travel = pointer - pressAt;
                    if (travel.X * travel.X + travel.Y * travel.Y >= DragSlop * DragSlop)
                    {
                        committed = true;
                        startDistance = MathF.Max(Vector2.Distance(pointer, anchor), 1f);
                    }

                    return dragCorner;
                }

                var distance = MathF.Max(Vector2.Distance(pointer, anchor), 1f);
                step = Math.Clamp(startStep * (distance / startDistance), minStep, maxStep);
                return dragCorner;
            }

            Release();
            return ResizeCorner.None;
        }

        if (hovered != ResizeCorner.None && ImGui.IsMouseClicked(ImGuiMouseButton.Left))
        {
            dragging = true;
            committed = false;
            dragCorner = hovered;
            startStep = step;
            pressAt = pointer;
            anchor = Opposite(window, hovered);
            startDistance = MathF.Max(Vector2.Distance(pointer, anchor), 1f);
        }

        return hovered;
    }

    public void Cancel()
    {
        JustReleased = false;
        dragging = false;
        committed = false;
        dragCorner = ResizeCorner.None;
    }

    private void Release()
    {
        JustReleased = committed;
        dragging = false;
        committed = false;
        dragCorner = ResizeCorner.None;
    }

    public static bool IsDiagonalNwse(ResizeCorner corner) => corner is ResizeCorner.TopLeft or ResizeCorner.BottomRight;

    public static bool Hits(Rect window, Rect glass, float scale, float caseRadius, bool roundCorners,
        Vector2 cursor) =>
        !OnGlass(glass, cursor) &&
        HitCorner(window, GripUnits * scale, caseRadius, roundCorners, cursor) != ResizeCorner.None;

    private static bool OnGlass(Rect glass, Vector2 cursor) =>
        !glass.IsEmpty && glass.Contains(cursor);

    public static Vector2 Opposite(Rect window, ResizeCorner grabbed) => grabbed switch
    {
        ResizeCorner.TopLeft => window.Max,
        ResizeCorner.TopRight => new Vector2(window.Min.X, window.Max.Y),
        ResizeCorner.BottomLeft => new Vector2(window.Max.X, window.Min.Y),
        _ => window.Min,
    };

    public static Vector2 PosFromAnchor(ResizeCorner grabbed, Vector2 fixedCorner, Vector2 size) => grabbed switch
    {
        ResizeCorner.TopLeft => fixedCorner - size,
        ResizeCorner.TopRight => new Vector2(fixedCorner.X, fixedCorner.Y - size.Y),
        ResizeCorner.BottomLeft => new Vector2(fixedCorner.X - size.X, fixedCorner.Y),
        _ => fixedCorner,
    };

    public static Vector2 RoomFromAnchor(ResizeCorner grabbed, Vector2 fixedCorner, Vector2 workMin, Vector2 workMax) =>
        grabbed switch
        {
            ResizeCorner.TopLeft => fixedCorner - workMin,
            ResizeCorner.TopRight => new Vector2(workMax.X - fixedCorner.X, fixedCorner.Y - workMin.Y),
            ResizeCorner.BottomLeft => new Vector2(fixedCorner.X - workMin.X, workMax.Y - fixedCorner.Y),
            _ => workMax - fixedCorner,
        };

    private static ResizeCorner HitCorner(Rect window, float grip, float caseRadius, bool roundCorners, Vector2 cursor)
    {
        if (roundCorners)
        {
            if (!InsideRound(window, caseRadius, cursor))
            {
                return ResizeCorner.None;
            }
        }
        else if (!window.Contains(cursor))
        {
            return ResizeCorner.None;
        }

        var reach = grip + MathF.Max(caseRadius * 0.55f, 8f);
        for (var corner = ResizeCorner.TopLeft; corner <= ResizeCorner.BottomRight; corner++)
        {
            if (NearCorner(window, corner, reach, cursor))
            {
                return corner;
            }
        }

        return ResizeCorner.None;
    }

    private static bool NearCorner(Rect window, ResizeCorner corner, float reach, Vector2 cursor) => corner switch
    {
        ResizeCorner.TopLeft => cursor.X <= window.Min.X + reach && cursor.Y <= window.Min.Y + reach,
        ResizeCorner.TopRight => cursor.X >= window.Max.X - reach && cursor.Y <= window.Min.Y + reach,
        ResizeCorner.BottomLeft => cursor.X <= window.Min.X + reach && cursor.Y >= window.Max.Y - reach,
        ResizeCorner.BottomRight => cursor.X >= window.Max.X - reach && cursor.Y >= window.Max.Y - reach,
        _ => false,
    };

    public static bool InsideRound(Rect window, float radius, Vector2 cursor)
    {
        if (!window.Contains(cursor))
        {
            return false;
        }

        var cap = MathF.Min(radius, MathF.Min(window.Width, window.Height) * 0.5f);
        if (cap <= 0.5f)
        {
            return true;
        }

        var center = new Vector2(
            Math.Clamp(cursor.X, window.Min.X + cap, window.Max.X - cap),
            Math.Clamp(cursor.Y, window.Min.Y + cap, window.Max.Y - cap));
        return Vector2.DistanceSquared(cursor, center) <= cap * cap + 0.25f;
    }
}
