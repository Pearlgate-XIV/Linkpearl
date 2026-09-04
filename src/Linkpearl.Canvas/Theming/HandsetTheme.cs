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
        ["music"] = new Vector4(0.659f, 0.333f, 0.969f, 1.00f),
        ["afterdark"] = new Vector4(0.220f, 0.741f, 0.973f, 1.00f),
        ["camera"] = new Vector4(0.620f, 0.447f, 0.502f, 1.00f),
        ["phone"] = new Vector4(0.298f, 0.784f, 0.529f, 1.00f),
        ["wallet"] = new Vector4(0.851f, 0.737f, 0.518f, 1.00f),
        ["message"] = new Vector4(0.361f, 0.749f, 0.463f, 1.00f),
        ["pearlchat"] = new Vector4(0.831f, 0.686f, 0.373f, 1.00f),
        ["friends"] = new Vector4(0.45f, 0.70f, 0.95f, 1.00f),
        ["market"] = new Vector4(0.38f, 0.86f, 0.52f, 1.00f),
        ["retainer"] = new Vector4(0.831f, 0.686f, 0.373f, 1.00f),
        ["events"] = new Vector4(0.72f, 0.48f, 0.92f, 1.00f),
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
