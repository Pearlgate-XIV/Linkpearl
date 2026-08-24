using Linkpearl.Theming;

namespace Linkpearl.Canvas.Theming;

// Crystal chassis language: a magitek communicator, not a pocket phone OS. Deep indigo glass,
// pearl-white ink, a single violet accent that reads as an enchanted core rather than a brand chip.
public static class HandsetPalette
{
    public static Palette Dark { get; } = new()
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
        Separator = new Vector4(1.00f, 1.00f, 1.00f, 0.08f),
        Positive = new Vector4(0.298f, 0.784f, 0.529f, 1.00f),
        Caution = new Vector4(0.945f, 0.706f, 0.259f, 1.00f),
        Negative = new Vector4(0.910f, 0.361f, 0.400f, 1.00f),
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
}
