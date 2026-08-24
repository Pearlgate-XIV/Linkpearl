using Linkpearl.Geometry;

namespace Linkpearl.Painting;

public enum Corner : byte
{
    None = 0,
    TopLeft = 1,
    TopRight = 2,
    BottomLeft = 4,
    BottomRight = 8,
    Top = TopLeft | TopRight,
    Bottom = BottomLeft | BottomRight,
    Left = TopLeft | BottomLeft,
    Right = TopRight | BottomRight,
    All = TopLeft | TopRight | BottomLeft | BottomRight,
}

public enum GradientAxis : byte
{
    Vertical = 0,
    Horizontal = 1,
}

public interface IPaintSurface
{
    void Fill(Rect area, Vector4 color);

    void Fill(Rect area, Vector4 color, float radius, Corner corners = Corner.All);

    void Stroke(Rect area, Vector4 color, float thickness, float radius = 0f, Corner corners = Corner.All);

    void FillGradient(Rect area, Vector4 from, Vector4 to, GradientAxis axis);

    void FillCircle(Vector2 center, float radius, Vector4 color);

    void StrokeCircle(Vector2 center, float radius, Vector4 color, float thickness);

    void FillSquircle(Rect area, Vector4 color, float radius);

    void Line(Vector2 from, Vector2 to, Vector4 color, float thickness);

    void Polyline(ReadOnlySpan<Vector2> points, Vector4 color, float thickness, bool closed);

    void Glow(Rect area, Vector4 color, float radius, float spread);

    void Image(ITextureHandle texture, Rect area, Vector4 tint);

    void Image(ITextureHandle texture, Rect area, Vector2 uvMin, Vector2 uvMax, Vector4 tint);

    void PushClip(Rect area);

    void PopClip();
}

public interface ITextureHandle : IDisposable
{
    nint Handle { get; }

    Vector2 Size { get; }

    bool IsReady { get; }
}
