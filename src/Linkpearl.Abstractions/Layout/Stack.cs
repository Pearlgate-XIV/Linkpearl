using Linkpearl.Geometry;

namespace Linkpearl.Layout;

public enum StackAxis : byte
{
    Vertical = 0,
    Horizontal = 1,
}

// Splits a Rect into a run of cells along one axis, forward-only: each Take() consumes from the
// remaining space and advances past the gap. No layout state survives past the call site.
public struct Stack
{
    private Rect remaining;
    private readonly StackAxis axis;
    private readonly float gap;

    public Stack(Rect area, StackAxis axis, float gap)
    {
        remaining = area;
        this.axis = axis;
        this.gap = gap;
    }

    public readonly Rect Remaining => remaining;

    public Rect Take(float length)
    {
        Rect cell;
        if (axis == StackAxis.Vertical)
        {
            cell = remaining.TopSlice(length);
            remaining = remaining.Inset(new Edges(0f, MathF.Min(length + gap, remaining.Height), 0f, 0f));
        }
        else
        {
            cell = remaining.LeftSlice(length);
            remaining = remaining.Inset(new Edges(MathF.Min(length + gap, remaining.Width), 0f, 0f, 0f));
        }

        return cell;
    }

    public Rect TakeRemaining() => remaining;

    public void Skip(float length) => Take(length);
}
