using Linkpearl.Preferences;
using Linkpearl.Theming;

namespace Linkpearl.Canvas.Theming;

public static class HandsetPalette
{
    public static Palette Dark { get; } = Crystal;

    public static Palette Crystal { get; } = new()
    {
        Surface = new Vector4(0.071f, 0.078f, 0.118f, 1.00f),
        SurfaceRaised = new Vector4(0.106f, 0.114f, 0.161f, 1.00f),
        SurfaceSunken = new Vector4(0.047f, 0.051f, 0.082f, 1.00f),
        SurfaceOverlay = new Vector4(0.031f, 0.035f, 0.059f, 0.86f),
        Ink = new Vector4(0.965f, 0.969f, 0.984f, 1.00f),
        InkMuted = new Vector4(0.965f, 0.969f, 0.984f, 0.62f),
        InkFaint = new Vector4(0.965f, 0.969f, 0.984f, 0.34f),
        Accent = new Vector4(0.514f, 0.400f, 0.973f, 1.00f),
        AccentInk = new Vector4(1.00f, 1.00f, 1.00f, 1.00f),
        WarmAccent = new Vector4(0.851f, 0.737f, 0.518f, 1.00f),
        Separator = new Vector4(1.00f, 1.00f, 1.00f, 0.08f),
        Positive = new Vector4(0.298f, 0.784f, 0.529f, 1.00f),
        Caution = new Vector4(0.945f, 0.706f, 0.259f, 1.00f),
        Negative = new Vector4(0.910f, 0.361f, 0.400f, 1.00f),
    };

    public static Palette Pearl { get; } = new()
    {
        Surface = new Vector4(0.93f, 0.94f, 0.96f, 1.00f),
        SurfaceRaised = new Vector4(0.98f, 0.98f, 0.99f, 1.00f),
        SurfaceSunken = new Vector4(0.84f, 0.86f, 0.90f, 1.00f),
        SurfaceOverlay = new Vector4(1.00f, 1.00f, 1.00f, 0.78f),
        Ink = new Vector4(0.12f, 0.13f, 0.18f, 1.00f),
        InkMuted = new Vector4(0.12f, 0.13f, 0.18f, 0.62f),
        InkFaint = new Vector4(0.12f, 0.13f, 0.18f, 0.34f),
        Accent = new Vector4(0.42f, 0.28f, 0.78f, 1.00f),
        AccentInk = new Vector4(1.00f, 1.00f, 1.00f, 1.00f),
        WarmAccent = new Vector4(0.72f, 0.56f, 0.28f, 1.00f),
        Separator = new Vector4(0.10f, 0.10f, 0.12f, 0.12f),
        Positive = new Vector4(0.18f, 0.62f, 0.40f, 1.00f),
        Caution = new Vector4(0.78f, 0.52f, 0.12f, 1.00f),
        Negative = new Vector4(0.78f, 0.22f, 0.28f, 1.00f),
    };

    public static Palette Ember { get; } = new()
    {
        Surface = new Vector4(0.12f, 0.07f, 0.06f, 1.00f),
        SurfaceRaised = new Vector4(0.18f, 0.10f, 0.08f, 1.00f),
        SurfaceSunken = new Vector4(0.07f, 0.04f, 0.03f, 1.00f),
        SurfaceOverlay = new Vector4(0.06f, 0.03f, 0.03f, 0.86f),
        Ink = new Vector4(0.98f, 0.94f, 0.88f, 1.00f),
        InkMuted = new Vector4(0.98f, 0.94f, 0.88f, 0.62f),
        InkFaint = new Vector4(0.98f, 0.94f, 0.88f, 0.34f),
        Accent = new Vector4(0.92f, 0.42f, 0.22f, 1.00f),
        AccentInk = new Vector4(1.00f, 1.00f, 1.00f, 1.00f),
        WarmAccent = new Vector4(0.90f, 0.70f, 0.38f, 1.00f),
        Separator = new Vector4(1.00f, 0.90f, 0.80f, 0.10f),
        Positive = new Vector4(0.42f, 0.78f, 0.40f, 1.00f),
        Caution = new Vector4(0.95f, 0.70f, 0.26f, 1.00f),
        Negative = new Vector4(0.91f, 0.36f, 0.40f, 1.00f),
    };

    public static Palette Night { get; } = new()
    {
        Surface = new Vector4(0.03f, 0.04f, 0.07f, 1.00f),
        SurfaceRaised = new Vector4(0.06f, 0.07f, 0.11f, 1.00f),
        SurfaceSunken = new Vector4(0.015f, 0.018f, 0.03f, 1.00f),
        SurfaceOverlay = new Vector4(0.02f, 0.02f, 0.04f, 0.90f),
        Ink = new Vector4(0.88f, 0.90f, 0.96f, 1.00f),
        InkMuted = new Vector4(0.88f, 0.90f, 0.96f, 0.55f),
        InkFaint = new Vector4(0.88f, 0.90f, 0.96f, 0.28f),
        Accent = new Vector4(0.40f, 0.62f, 0.98f, 1.00f),
        AccentInk = new Vector4(0.04f, 0.06f, 0.10f, 1.00f),
        WarmAccent = new Vector4(0.70f, 0.78f, 0.92f, 1.00f),
        Separator = new Vector4(1.00f, 1.00f, 1.00f, 0.07f),
        Positive = new Vector4(0.30f, 0.78f, 0.53f, 1.00f),
        Caution = new Vector4(0.95f, 0.71f, 0.26f, 1.00f),
        Negative = new Vector4(0.91f, 0.36f, 0.40f, 1.00f),
    };

    public static Metrics Reference { get; } = new()
    {
        Hairline = 1f,
        GutterTight = 6f,
        Gutter = 12f,
        GutterWide = 20f,
        RowHeight = 44f,
        ControlHeight = 36f,
        CardRadius = 14f,
        ChipRadius = 999f,
    };

    public static Palette Resolve(string colorway, string core)
    {
        var palette = ColorwayId.Sanitize(colorway) switch
        {
            ColorwayId.Pearl => Pearl,
            ColorwayId.Ember => Ember,
            ColorwayId.Night => Night,
            _ => Crystal,
        };

        var tint = CoreId.Sanitize(core) switch
        {
            CoreId.Violet => new Vector4(0.54f, 0.40f, 0.97f, 1f),
            CoreId.Rose => new Vector4(0.91f, 0.36f, 0.52f, 1f),
            CoreId.Sage => new Vector4(0.36f, 0.75f, 0.48f, 1f),
            _ => palette.WarmAccent,
        };

        return palette with { Accent = tint, WarmAccent = tint };
    }

    public static bool IsDark(string colorway) =>
        !string.Equals(ColorwayId.Sanitize(colorway), ColorwayId.Pearl, StringComparison.Ordinal);
}
