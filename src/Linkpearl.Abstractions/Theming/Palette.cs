namespace Linkpearl.Theming;

public readonly struct Palette
{
    // Open apps sit on this wash so wallpaper still reads through a little.
    public static readonly Vector4 AppGround = new(0f, 0f, 0f, 0.90f);

    public required Vector4 Surface { get; init; }

    public required Vector4 SurfaceRaised { get; init; }

    public required Vector4 SurfaceSunken { get; init; }

    public required Vector4 SurfaceOverlay { get; init; }

    public required Vector4 Ink { get; init; }

    public required Vector4 InkMuted { get; init; }

    public required Vector4 InkFaint { get; init; }

    public required Vector4 Accent { get; init; }

    public required Vector4 AccentInk { get; init; }

    // A restrained warm gold/ivory used sparingly for small details (the Linkpearl crystal,
    // rare highlights) — never the dominant palette. See the hardware/software design split in
    // docs/STATUS.md: the case can be ornate gold, the software stays calm aetherglass.
    public required Vector4 WarmAccent { get; init; }

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
