namespace Linkpearl.Geometry;

public readonly struct Edges : IEquatable<Edges>
{
    public float Left { get; }
    public float Top { get; }
    public float Right { get; }
    public float Bottom { get; }

    public Edges(float uniform)
    {
        Left = uniform;
        Top = uniform;
        Right = uniform;
        Bottom = uniform;
    }

    public Edges(float horizontal, float vertical)
    {
        Left = horizontal;
        Top = vertical;
        Right = horizontal;
        Bottom = vertical;
    }

    public Edges(float left, float top, float right, float bottom)
    {
        Left = left;
        Top = top;
        Right = right;
        Bottom = bottom;
    }

    public static Edges None => new(0f);

    public float Horizontal => Left + Right;

    public float Vertical => Top + Bottom;

    public Edges Scaled(float factor) => new(Left * factor, Top * factor, Right * factor, Bottom * factor);

    public bool Equals(Edges other) =>
        Left.Equals(other.Left) && Top.Equals(other.Top) && Right.Equals(other.Right) &&
        Bottom.Equals(other.Bottom);

    public override bool Equals(object? obj) => obj is Edges other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(Left, Top, Right, Bottom);

    public static bool operator ==(Edges left, Edges right) => left.Equals(right);

    public static bool operator !=(Edges left, Edges right) => !left.Equals(right);
}
