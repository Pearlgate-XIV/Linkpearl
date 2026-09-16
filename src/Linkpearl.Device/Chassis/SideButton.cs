using Linkpearl.Geometry;
using Linkpearl.Input;
using Linkpearl.Painting;

namespace Linkpearl.Device.Chassis;

public enum SideEdge : byte
{
    Right = 0,
    Left = 1,
}

// A functional button on the window's outer rail, outside the glass. Power sits on the
// upper-right edge. Position and length are fractions/units of the window so they scale.
public readonly struct SideButton
{
    public SideEdge Edge { get; }
    public float CenterFraction { get; }
    public float LengthUnits { get; }

    public SideButton(SideEdge edge, float centerFraction = 0.22f, float lengthUnits = 34f)
    {
        Edge = edge;
        CenterFraction = centerFraction;
        LengthUnits = lengthUnits;
    }

    public Rect Area(Rect window, float railDepth, float scale)
    {
        var length = LengthUnits * scale;
        var centerY = window.Min.Y + window.Height * CenterFraction;
        var top = centerY - length * 0.5f;
        var bottom = centerY + length * 0.5f;
        return Edge == SideEdge.Right
            ? new Rect(new Vector2(window.Max.X - railDepth, top), new Vector2(window.Max.X, bottom))
            : new Rect(new Vector2(window.Min.X, top), new Vector2(window.Min.X + railDepth, bottom));
    }

    public bool Update(Rect window, float railDepth, float scale, IInputProbe input) =>
        input.ConsumeClick(Area(window, railDepth, scale));

    public void Draw(IPaintSurface paint, Rect window, float railDepth, float scale, Vector4 color)
    {
        var area = Area(window, railDepth, scale);
        var corners = Edge == SideEdge.Right ? Corner.Left : Corner.Right;
        paint.Fill(area, color, railDepth * 0.4f, corners);
    }
}
