using Linkpearl.Theming;

namespace Linkpearl.Canvas.Theming;

public sealed class HandsetTheme : ITheme
{
    private static readonly Dictionary<string, Vector4> Accents = new(StringComparer.Ordinal)
    {
        ["clock"] = new Vector4(0.910f, 0.361f, 0.400f, 1.00f),
        ["message"] = new Vector4(0.361f, 0.749f, 0.463f, 1.00f),
        ["settings"] = new Vector4(0.635f, 0.663f, 0.706f, 1.00f),
    };

    public HandsetTheme(float scale)
    {
        Scale = scale;
    }

    public Palette Palette => HandsetPalette.Dark;

    public Metrics Metrics => Scaled(HandsetPalette.Reference, Scale);

    public bool IsDark => true;

    public float Scale { get; set; }

    public Vector4 AccentFor(string appletId) =>
        Accents.TryGetValue(appletId, out var accent) ? accent : Palette.Accent;

    private static Metrics Scaled(Metrics reference, float scale) => new()
    {
        Hairline = MathF.Max(reference.Hairline * scale, 1f),
        GutterTight = reference.GutterTight * scale,
        Gutter = reference.Gutter * scale,
        GutterWide = reference.GutterWide * scale,
        RowHeight = reference.RowHeight * scale,
        ControlHeight = reference.ControlHeight * scale,
        CardRadius = reference.CardRadius * scale,
        ChipRadius = reference.ChipRadius,
    };
}
