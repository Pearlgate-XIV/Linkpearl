using Linkpearl.Geometry;

namespace Linkpearl.Painting;

public enum FontRole : byte
{
    Body = 0,
    BodyStrong = 1,
    Caption = 2,
    CaptionStrong = 3,
    Title = 4,
    Display = 5,
    Numeric = 6,
    Monospace = 7,
}

public enum TextAlign : byte
{
    Left = 0,
    Center = 1,
    Right = 2,
}

public readonly struct TextStyle
{
    public FontRole Role { get; }
    public Vector4 Color { get; }
    public TextAlign Align { get; }
    public float LineSpacing { get; }
    public float Scale { get; }
    public Vector4 Glow { get; }
    public float GlowSpread { get; }

    public TextStyle(FontRole role, Vector4 color, TextAlign align = TextAlign.Left, float lineSpacing = 1f,
        float scale = 1f, Vector4 glow = default, float glowSpread = 0f)
    {
        Role = role;
        Color = color;
        Align = align;
        LineSpacing = lineSpacing;
        Scale = scale > 0f ? scale : 1f;
        Glow = glow;
        GlowSpread = glowSpread;
    }

    public TextStyle With(Vector4 color) => new(Role, color, Align, LineSpacing, Scale, Glow, GlowSpread);

    public TextStyle With(TextAlign align) => new(Role, Color, align, LineSpacing, Scale, Glow, GlowSpread);
}

public interface ITextPainter
{
    Vector2 Measure(ReadOnlySpan<char> text, FontRole role);

    Vector2 MeasureWrapped(ReadOnlySpan<char> text, FontRole role, float wrapWidth);

    float LineHeight(FontRole role);

    void Draw(Vector2 origin, ReadOnlySpan<char> text, in TextStyle style);

    void DrawIn(Rect area, ReadOnlySpan<char> text, in TextStyle style);

    void DrawWrapped(Rect area, ReadOnlySpan<char> text, in TextStyle style);

    void DrawEllipsized(Rect area, ReadOnlySpan<char> text, in TextStyle style);

    void DrawFitted(Rect area, ReadOnlySpan<char> text, in TextStyle style);
}
