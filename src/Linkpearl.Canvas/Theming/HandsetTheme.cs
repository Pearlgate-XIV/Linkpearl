using Linkpearl.Preferences;
using Linkpearl.Theming;

namespace Linkpearl.Canvas.Theming;

public sealed class HandsetTheme : ITheme
{
    private static readonly Dictionary<string, Vector4> Accents = new(StringComparer.Ordinal)
    {
        ["clock"] = new Vector4(0.910f, 0.361f, 0.400f, 1.00f),
        ["notes"] = new Vector4(0.980f, 0.745f, 0.275f, 1.00f),
        ["calculator"] = new Vector4(0.420f, 0.447f, 0.502f, 1.00f),
        ["timer"] = new Vector4(0.361f, 0.620f, 0.910f, 1.00f),
        ["stopwatch"] = new Vector4(0.510f, 0.400f, 0.910f, 1.00f),
        ["place"] = new Vector4(0.361f, 0.749f, 0.463f, 1.00f),
        ["alarms"] = new Vector4(0.910f, 0.420f, 0.280f, 1.00f),
        ["weather"] = new Vector4(0.400f, 0.620f, 0.910f, 1.00f),
        ["calendar"] = new Vector4(0.910f, 0.620f, 0.280f, 1.00f),
        ["camera"] = new Vector4(0.620f, 0.447f, 0.502f, 1.00f),
        ["wallet"] = new Vector4(0.851f, 0.737f, 0.518f, 1.00f),
        ["message"] = new Vector4(0.361f, 0.749f, 0.463f, 1.00f),
        ["settings"] = new Vector4(0.635f, 0.663f, 0.706f, 1.00f),
    };

    private readonly DisplayPreferences display;

    public HandsetTheme(float scale, DisplayPreferences display)
    {
        Scale = scale;
        this.display = display;
    }

    public Palette Palette => HandsetPalette.Resolve(display.Colorway, display.Core);

    public Metrics Metrics => Scaled(HandsetPalette.Reference, Scale);

    public bool IsDark => HandsetPalette.IsDark(display.Colorway);

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
