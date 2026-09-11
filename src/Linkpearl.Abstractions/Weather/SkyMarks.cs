using Linkpearl.Geometry;
using Linkpearl.Painting;

namespace Linkpearl.Weather;

public static class SkyMarks
{
    public static void Draw(IPaintSurface paint, Rect area, string? name, bool night)
    {
        if (area.IsEmpty)
        {
            return;
        }

        var time = Environment.TickCount64 * 0.001f;
        var size = MathF.Min(area.Width, area.Height);
        var center = area.Center;
        if (SkyChrome.Snow(name))
        {
            Cloud(paint, center, size, night, time);
            Flakes(paint, center, size, time, SkyChrome.HasStorm(name));
            return;
        }

        if (SkyChrome.HasStorm(name) || SkyChrome.Has(name, "thunder"))
        {
            Cloud(paint, center + new Vector2(0f, -size * 0.06f), size * 1.02f, night, time);
            Bolt(paint, center + new Vector2(size * 0.04f, size * 0.18f), size, time);
            Drops(paint, center, size, time, 5);
            return;
        }

        if (SkyChrome.Rain(name))
        {
            Cloud(paint, center + new Vector2(0f, -size * 0.10f), size, night, time);
            Drops(paint, center, size, time, SkyChrome.Has(name, "shower") ? 8 : 6);
            return;
        }

        if (SkyChrome.Fog(name))
        {
            Fog(paint, center, size, night, time);
            return;
        }

        if (SkyChrome.Overcast(name))
        {
            if (!night && !SkyChrome.Has(name, "cloud"))
            {
                Sun(paint, center + new Vector2(-size * 0.16f, -size * 0.18f), size * 0.72f, time, faint: true);
            }

            Cloud(paint, center + new Vector2(size * 0.06f, size * 0.08f), size * 1.05f, night, time);
            return;
        }

        if (SkyChrome.Has(name, "natural"))
        {
            Arrow(paint, center, size, night);
            return;
        }

        if (night)
        {
            Moon(paint, center, size, time);
            return;
        }

        Sun(paint, center, size, time, faint: false);
    }

    public static void ForecastTab(IPaintSurface paint, Rect area, bool on, bool night)
    {
        var size = MathF.Min(area.Width, area.Height);
        var center = area.Center;
        Sun(paint, center + new Vector2(-size * 0.10f, -size * 0.12f), size * 0.78f, Environment.TickCount64 * 0.001f,
            faint: !on);
        Cloud(paint, center + new Vector2(size * 0.10f, size * 0.16f), size * 0.82f, night,
            Environment.TickCount64 * 0.001f);
    }

    public static void ControlTab(IPaintSurface paint, Rect area, bool on, bool night)
    {
        var ink = on
            ? (night ? new Vector4(1f, 1f, 1f, 0.96f) : new Vector4(0.12f, 0.16f, 0.22f, 0.94f))
            : (night ? new Vector4(1f, 1f, 1f, 0.55f) : new Vector4(0.22f, 0.28f, 0.36f, 0.62f));
        var knob = on
            ? new Vector4(0.28f, 0.62f, 1f, 1f)
            : ink;
        var w = MathF.Min(area.Width, area.Height);
        var left = area.Center.X - w * 0.38f;
        var right = area.Center.X + w * 0.38f;
        var y0 = area.Center.Y - w * 0.16f;
        var y1 = area.Center.Y + w * 0.16f;
        var thick = MathF.Max(2.2f, w * 0.10f);
        paint.Line(new Vector2(left, y0), new Vector2(right, y0), ink with { W = ink.W * 0.55f }, thick);
        paint.Line(new Vector2(left, y1), new Vector2(right, y1), ink with { W = ink.W * 0.55f }, thick);
        paint.FillCircle(new Vector2(left + (right - left) * 0.72f, y0), thick * 1.15f, knob);
        paint.FillCircle(new Vector2(left + (right - left) * 0.32f, y1), thick * 1.15f, knob);
    }

    private static void Sun(IPaintSurface paint, Vector2 center, float size, float time, bool faint)
    {
        var gold = faint
            ? new Vector4(1f, 0.86f, 0.32f, 0.72f)
            : new Vector4(1f, 0.84f, 0.22f, 1f);
        var core = faint
            ? new Vector4(1f, 0.96f, 0.62f, 0.88f)
            : new Vector4(1f, 0.96f, 0.55f, 1f);
        var radius = size * 0.22f;
        var spin = time * 0.55f;
        var ray = size * 0.38f;
        var stroke = MathF.Max(2f, size * 0.055f);
        for (var index = 0; index < 8; index++)
        {
            var angle = spin + index * (MathF.PI / 4f);
            var inner = radius * 1.35f;
            var outer = ray * (0.92f + 0.08f * MathF.Sin(time * 3.2f + index));
            var dir = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
            paint.Line(center + dir * inner, center + dir * outer, gold, stroke);
        }

        paint.Glow(Rect.FromSize(center - new Vector2(radius * 2.2f), new Vector2(radius * 4.4f)),
            gold with { W = gold.W * 0.35f }, radius * 1.8f, radius);
        paint.FillCircle(center, radius, gold);
        paint.FillCircle(center, radius * 0.62f, core);
    }

    private static void Moon(IPaintSurface paint, Vector2 center, float size, float time)
    {
        var body = new Vector4(0.96f, 0.97f, 1f, 0.98f);
        var shade = new Vector4(0.16f, 0.22f, 0.40f, 0.96f);
        var radius = size * 0.28f;
        paint.Glow(Rect.FromSize(center - new Vector2(radius * 2f), new Vector2(radius * 4f)),
            new Vector4(0.82f, 0.88f, 1f, 0.28f), radius * 1.6f, radius);
        paint.FillCircle(center, radius, body);
        paint.FillCircle(center + new Vector2(radius * 0.42f, -radius * 0.12f), radius * 0.82f, shade);
        var twinkle = 0.35f + 0.45f * (0.5f + 0.5f * MathF.Sin(time * 2.4f));
        paint.FillCircle(center + new Vector2(-radius * 0.85f, -radius * 0.70f), size * 0.035f,
            new Vector4(1f, 1f, 1f, twinkle));
    }

    private static void Cloud(IPaintSurface paint, Vector2 center, float size, bool night, float time)
    {
        var drift = MathF.Sin(time * 1.1f) * size * 0.03f;
        var fill = night
            ? new Vector4(0.90f, 0.93f, 0.98f, 0.94f)
            : new Vector4(1f, 1f, 1f, 0.96f);
        var edge = night
            ? new Vector4(0.70f, 0.76f, 0.86f, 0.90f)
            : new Vector4(0.82f, 0.86f, 0.92f, 0.95f);
        var c = center + new Vector2(drift, 0f);
        var w = size * 0.42f;
        paint.FillCircle(c + new Vector2(-w * 0.42f, w * 0.10f), w * 0.42f, edge);
        paint.FillCircle(c + new Vector2(w * 0.08f, -w * 0.18f), w * 0.50f, fill);
        paint.FillCircle(c + new Vector2(w * 0.48f, w * 0.08f), w * 0.40f, fill);
        paint.Fill(
            Rect.FromSize(new Vector2(c.X - w * 0.72f, c.Y + w * 0.02f), new Vector2(w * 1.44f, w * 0.42f)),
            fill, w * 0.22f);
    }

    private static void Fog(IPaintSurface paint, Vector2 center, float size, bool night, float time)
    {
        var tint = night
            ? new Vector4(0.88f, 0.90f, 0.94f, 0.82f)
            : new Vector4(1f, 1f, 1f, 0.88f);
        for (var index = 0; index < 3; index++)
        {
            var slide = MathF.Sin(time * 0.9f + index * 1.3f) * size * 0.08f;
            var y = center.Y - size * 0.16f + index * size * 0.16f;
            var width = size * (0.62f - index * 0.06f);
            paint.Fill(
                Rect.FromSize(new Vector2(center.X - width * 0.5f + slide, y), new Vector2(width, size * 0.09f)),
                tint with { W = tint.W * (0.95f - index * 0.12f) }, size * 0.05f);
        }
    }

    private static void Drops(IPaintSurface paint, Vector2 center, float size, float time, int count)
    {
        var color = new Vector4(0.35f, 0.62f, 0.98f, 0.95f);
        var thick = MathF.Max(1.8f, size * 0.045f);
        var length = size * 0.16f;
        for (var index = 0; index < count; index++)
        {
            var seed = index * 0.37f;
            var x = center.X - size * 0.28f + size * 0.56f * Fract(seed);
            var y = center.Y + size * 0.02f + size * 0.28f * Fract(seed + time * 0.85f);
            paint.Line(new Vector2(x, y), new Vector2(x - length * 0.18f, y + length), color, thick);
        }
    }

    private static void Flakes(IPaintSurface paint, Vector2 center, float size, float time, bool heavy)
    {
        var count = heavy ? 9 : 6;
        var color = new Vector4(1f, 1f, 1f, 0.96f);
        for (var index = 0; index < count; index++)
        {
            var seed = index * 0.29f;
            var x = center.X - size * 0.30f + size * 0.60f * Fract(seed * 1.7f) +
                    MathF.Sin(time * 1.6f + seed * 6f) * size * 0.04f;
            var y = center.Y + size * 0.28f * Fract(seed + time * 0.22f);
            paint.FillCircle(new Vector2(x, y), MathF.Max(1.6f, size * 0.035f), color);
        }
    }

    private static void Bolt(IPaintSurface paint, Vector2 center, float size, float time)
    {
        var flash = 0.55f + 0.45f * MathF.Abs(MathF.Sin(time * 8.2f));
        var gold = new Vector4(1f, 0.88f, 0.28f, flash);
        var s = size * 0.22f;
        paint.Polyline(
        [
            center + new Vector2(-s * 0.15f, -s * 0.85f),
            center + new Vector2(s * 0.22f, -s * 0.10f),
            center + new Vector2(-s * 0.08f, -s * 0.02f),
            center + new Vector2(s * 0.18f, s * 0.85f),
            center + new Vector2(-s * 0.05f, s * 0.08f),
            center + new Vector2(s * 0.02f, s * 0.02f),
        ], gold, MathF.Max(2.2f, size * 0.055f), false);
    }

    private static void Arrow(IPaintSurface paint, Vector2 center, float size, bool night)
    {
        var ink = night ? new Vector4(1f, 1f, 1f, 0.90f) : new Vector4(0.20f, 0.28f, 0.40f, 0.90f);
        var r = size * 0.28f;
        var stroke = MathF.Max(2f, size * 0.07f);
        paint.StrokeCircle(center, r, ink, stroke);
        paint.Line(center + new Vector2(r * 0.15f, -r * 1.05f), center + new Vector2(r * 0.85f, -r * 0.35f), ink,
            stroke);
        paint.Line(center + new Vector2(r * 0.85f, -r * 0.35f), center + new Vector2(r * 0.20f, -r * 0.20f), ink,
            stroke);
    }

    private static float Fract(float value) => value - MathF.Floor(value);
}
