using Linkpearl.Painting;

namespace Linkpearl.Preferences;

public readonly struct MarkLook
{
    public Vector4 Ink { get; }
    public Vector4 Edge { get; }
    public bool Glow { get; }
    public TitleMotion Motion { get; }
    public NameGlowWeight Weight { get; }

    public MarkLook(Vector4 ink, Vector4 edge, bool glow, TitleMotion motion, NameGlowWeight weight)
    {
        Ink = ink;
        Edge = edge;
        Glow = glow;
        Motion = motion;
        Weight = weight;
    }

    public static MarkLook ForTitle(DisplayPreferences display) =>
        new(
            new Vector4(1f, 1f, 1f, 1f),
            new Vector4(display.TitleGlowR, display.TitleGlowG, display.TitleGlowB, 1f),
            false,
            display.TitleMotion,
            display.TitleGlowWeight);

    public static MarkLook ForName(DisplayPreferences display, Vector4 fallbackInk) =>
        new(
            display.NameInkCustom
                ? new Vector4(display.NameInkR, display.NameInkG, display.NameInkB, 1f)
                : fallbackInk,
            new Vector4(display.NameGlowR, display.NameGlowG, display.NameGlowB, 1f),
            display.NameGlow,
            display.NameMotion,
            display.NameGlowWeight);
}

public static class TitleFx
{
    public static Vector4 Fill(in MarkLook look, float time, int index, int count)
    {
        var ink = look.Ink with { W = 1f };
        if (!look.Glow || look.Motion != TitleMotion.Wave)
        {
            return ink;
        }

        return Mix(ink, look.Edge with { W = 1f }, Wave(time, index, count)) with { W = 1f };
    }

    public static Vector4 Fill(DisplayPreferences display, float time, int index, int count) =>
        Fill(MarkLook.ForTitle(display), time, index, count);

    public static Vector4 Glow(in MarkLook look, float time, int index, int count)
    {
        if (!look.Glow)
        {
            return default;
        }

        var strength = look.Motion switch
        {
            TitleMotion.Pulse => 0.03f + 0.97f * Pulse(time),
            TitleMotion.Wave => 0.03f + 0.97f * Wave(time, index, count),
            _ => 1f,
        };
        return look.Edge with { W = strength };
    }

    public static Vector4 Glow(DisplayPreferences display, float time, int index, int count, float unit) =>
        Glow(MarkLook.ForTitle(display), time, index, count);

    public static float Spread(in MarkLook look, float unit) =>
        GlassName.Spread(look.Weight) * unit;

    public static float Spread(DisplayPreferences display, float unit) =>
        Spread(MarkLook.ForTitle(display), unit);

    private static float Pulse(float time) => 0.5f + 0.5f * MathF.Sin(time * 2.20f);

    private static float Wave(float time, int index, int count)
    {
        var span = Math.Max(count, 1);
        return 0.5f + 0.5f * MathF.Sin(time * 2.20f - index * (MathF.PI * 1.15f / span));
    }

    private static Vector4 Mix(Vector4 a, Vector4 b, float t)
    {
        t = Math.Clamp(t, 0f, 1f);
        return a + (b - a) * t;
    }
}
