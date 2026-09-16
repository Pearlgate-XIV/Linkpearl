namespace Linkpearl.Geometry;

public readonly struct Rect : IEquatable<Rect>
{
    public Vector2 Min { get; }
    public Vector2 Max { get; }

    public Rect(Vector2 min, Vector2 max)
    {
        Min = min;
        Max = max;
    }

    public static Rect Empty => new(Vector2.Zero, Vector2.Zero);

    public static Rect FromSize(Vector2 origin, Vector2 size) => new(origin, origin + size);

    public float Width => Max.X - Min.X;

    public float Height => Max.Y - Min.Y;

    public Vector2 Size => Max - Min;

    public Vector2 Center => (Min + Max) * 0.5f;

    public bool IsEmpty => Width <= 0f || Height <= 0f;

    public Rect Inset(float amount) => Inset(new Edges(amount));

    public Rect Inset(Edges edges) =>
        new(new Vector2(Min.X + edges.Left, Min.Y + edges.Top),
            new Vector2(Max.X - edges.Right, Max.Y - edges.Bottom));

    public Rect Expand(float amount) => Inset(new Edges(-amount));

    public Rect Translate(Vector2 offset) => new(Min + offset, Max + offset);

    public Rect WithHeight(float height) => new(Min, new Vector2(Max.X, Min.Y + height));

    public Rect WithWidth(float width) => new(Min, new Vector2(Min.X + width, Max.Y));

    public Rect TopSlice(float height) => new(Min, new Vector2(Max.X, MathF.Min(Min.Y + height, Max.Y)));

    public Rect BottomSlice(float height) => new(new Vector2(Min.X, MathF.Max(Max.Y - height, Min.Y)), Max);

    public Rect LeftSlice(float width) => new(Min, new Vector2(MathF.Min(Min.X + width, Max.X), Max.Y));

    public Rect RightSlice(float width) => new(new Vector2(MathF.Max(Max.X - width, Min.X), Min.Y), Max);

    public bool Contains(Vector2 point) =>
        point.X >= Min.X && point.X < Max.X && point.Y >= Min.Y && point.Y < Max.Y;

    public bool Overlaps(Rect other) =>
        Min.X < other.Max.X && other.Min.X < Max.X && Min.Y < other.Max.Y && other.Min.Y < Max.Y;

    public Rect Intersect(Rect other) =>
        new(Vector2.Max(Min, other.Min), Vector2.Min(Max, other.Max));

    public bool Equals(Rect other) => Min.Equals(other.Min) && Max.Equals(other.Max);

    public override bool Equals(object? obj) => obj is Rect other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(Min, Max);

    public static bool operator ==(Rect left, Rect right) => left.Equals(right);

    public static bool operator !=(Rect left, Rect right) => !left.Equals(right);
}
