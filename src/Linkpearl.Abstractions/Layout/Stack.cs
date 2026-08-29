using Linkpearl.Geometry;

namespace Linkpearl.Layout;

public enum StackAxis : byte
{
    Vertical = 0,
    Horizontal = 1,
}

// Splits a Rect into a run of cells along one axis, forward-only: each Take() consumes from the
// remaining space and advances past the gap. Take always reserves the requested length, even when
// that walks past the original box, so later cells stack instead of collapsing on the last pixel.
// Remaining.Height (or Width) can go negative; Compose uses that to report the true content size.
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
            cell = Rect.FromSize(remaining.Min, new Vector2(remaining.Width, length));
            remaining = new Rect(new Vector2(remaining.Min.X, remaining.Min.Y + length + gap), remaining.Max);
        }
        else
        {
            cell = Rect.FromSize(remaining.Min, new Vector2(length, remaining.Height));
            remaining = new Rect(new Vector2(remaining.Min.X + length + gap, remaining.Min.Y), remaining.Max);
        }

        return cell;
    }

    public Rect TakeRemaining() => remaining;

    public void Skip(float length) => Take(length);
}
