namespace Linkpearl.Theming;

public readonly struct Palette
{
    public required Vector4 Surface { get; init; }

    public required Vector4 SurfaceRaised { get; init; }

    public required Vector4 SurfaceSunken { get; init; }

    public required Vector4 SurfaceOverlay { get; init; }

    public required Vector4 Ink { get; init; }

    public required Vector4 InkMuted { get; init; }

    public required Vector4 InkFaint { get; init; }

    public required Vector4 Accent { get; init; }

    public required Vector4 AccentInk { get; init; }

    public required Vector4 Separator { get; init; }

    public required Vector4 Positive { get; init; }

    public required Vector4 Caution { get; init; }

    public required Vector4 Negative { get; init; }
}

public readonly struct Metrics
{
    public required float Hairline { get; init; }

    public required float GutterTight { get; init; }

    public required float Gutter { get; init; }

    public required float GutterWide { get; init; }

    public required float RowHeight { get; init; }

    public required float ControlHeight { get; init; }

    public required float CardRadius { get; init; }

    public required float ChipRadius { get; init; }
}

public interface ITheme
{
    Palette Palette { get; }

    Metrics Metrics { get; }

    bool IsDark { get; }

    float Scale { get; }

    Vector4 AccentFor(string appletId);
}
