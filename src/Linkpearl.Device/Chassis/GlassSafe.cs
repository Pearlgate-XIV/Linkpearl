using Linkpearl.Geometry;

namespace Linkpearl.Device.Chassis;

// Keep glyphs inside the visible glass. Chassis PNGs round the hole; a flush status
// row or notice chip is cropped by the rim.
public static class GlassSafe
{
    public static float Pad(Rect screen, float scale)
    {
        var byWidth = screen.Width * 0.12f;
        var byScale = 8f * MathF.Max(scale, 0.25f);
        return MathF.Max(4f, MathF.Min(byWidth, byScale));
    }

    public static Rect Inner(Rect screen, float scale) => screen.Inset(Pad(screen, scale));

    public static Edges StripEdges(Rect screen, float scale, float reservedRight)
    {
        var pad = Pad(screen, scale);
        var top = MathF.Max(scale * 3.2f, pad * 0.45f);
        return new Edges(pad, top, pad + reservedRight, top);
    }
}
