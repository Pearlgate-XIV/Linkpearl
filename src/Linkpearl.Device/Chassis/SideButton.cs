using Linkpearl.Geometry;
using Linkpearl.Input;
using Linkpearl.Painting;

namespace Linkpearl.Device.Chassis;

public enum SideEdge : byte
{
    Right = 0,
    Left = 1,
}

// A single functional button on the window's outer rail, matching where a real phone's power
// button sits: upper third of the edge, protruding slightly from the frame. Position and length
// are fractions of window height so it scales with every size step.
public readonly struct SideButton
{
    private const float CenterFraction = 0.22f;
    private const float LengthUnits = 34f;

    public readonly SideEdge Edge;

    public SideButton(SideEdge edge)
    {
        Edge = edge;
    }

    public Rect Area(Rect window, float railDepth, float scale)
    {
        var length = LengthUnits * scale;
        var centerY = window.Min.Y + window.Height * CenterFraction;
        var top = new Vector2(0f, centerY - length * 0.5f);
        var bottom = new Vector2(0f, centerY + length * 0.5f);
        return Edge == SideEdge.Right
            ? new Rect(new Vector2(window.Max.X - railDepth, top.Y), new Vector2(window.Max.X, bottom.Y))
            : new Rect(new Vector2(window.Min.X, top.Y), new Vector2(window.Min.X + railDepth, bottom.Y));
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
