using Linkpearl.Applets;
using Linkpearl.Geometry;
using Linkpearl.Media;
using Linkpearl.Painting;
using Linkpearl.Time;

namespace Linkpearl.Weather;

public static class SkyChrome
{
    private static readonly Vector2[] Stars =
    {
        new(0.10f, 0.18f),
        new(0.22f, 0.10f),
        new(0.34f, 0.24f),
        new(0.48f, 0.12f),
        new(0.67f, 0.16f),
        new(0.82f, 0.08f),
        new(0.91f, 0.22f),
    };

    public static bool IsNight(EorzeaTime bells) => bells.Hour is < 6 or >= 18;

    public static string Phase(EorzeaTime bells) => bells.Hour switch
    {
        < 4 => "Deep night",
        < 6 => "Small hours",
        < 9 => "Dawn",
        < 12 => "Morning",
        < 17 => "Afternoon",
        < 19 => "Dusk",
        _ => "Night",
    };

    public static bool Overcast(string? weather) =>
        Has(weather, "cloud") || Has(weather, "overcast") || Has(weather, "gloom") ||
        Has(weather, "dust") || Has(weather, "wind") || Has(weather, "gale");

    public static bool Fog(string? weather) =>
        Has(weather, "fog") || Has(weather, "mist");

    public static bool Snow(string? weather) =>
        Has(weather, "snow") || Has(weather, "blizzard") || Has(weather, "sleet");

    public static bool Rain(string? weather) =>
        !Snow(weather) && (Has(weather, "rain") || Has(weather, "shower") || Has(weather, "thunder") ||
            Has(weather, "storm"));

    public static bool Wet(string? weather) => Rain(weather) || Snow(weather);

    public static float Corner(in AppletFrame frame) => Corner(frame, frame.Content);

    public static float Corner(in AppletFrame frame, Rect area) =>
        MathF.Max(frame.Units(18f), MathF.Min(area.Width, area.Height) * 0.12f);

    public static void Paint(in AppletFrame frame, Rect area, string condition, bool night, float radius = 0f)
    {
        if (area.IsEmpty)
        {
            return;
        }

        Wash(condition, night, out var top, out var bottom);
        Blend(frame.Paint, area, top, bottom, radius);

        frame.Paint.PushClip(area);

        var time = Environment.TickCount64 * 0.001f;
        if (Has(condition, "thunder") || Has(condition, "storm"))
        {
            var flash = MathF.Max(0f, MathF.Sin(time * 9.4f) - 0.88f) * 6f;
            if (flash > 0.02f)
            {
                frame.Paint.Fill(area, new Vector4(0.92f, 0.94f, 1f, flash * 0.18f), radius);
            }
        }

        if (night && !Snow(condition) && !Fog(condition))
        {
            for (var index = 0; index < Stars.Length; index++)
            {
                var star = Stars[index];
                var twinkle = 0.28f + 0.42f * (0.5f + 0.5f * MathF.Sin(time * 2.1f + index * 1.7f));
                frame.Paint.FillCircle(new Vector2(area.Min.X + area.Width * star.X, area.Min.Y + area.Height * star.Y),
                    MathF.Max(0.8f, area.Height * 0.008f), new Vector4(0.94f, 0.96f, 1f, twinkle));
            }
        }

        if (Overcast(condition) || Fog(condition) || Wet(condition))
        {
            DrawClouds(frame.Paint, area, night, time);
        }

        if (Snow(condition))
        {
            DrawSnow(frame.Paint, area, time, Has(condition, "blizzard"));
        }
        else if (Rain(condition))
        {
            DrawRain(frame.Paint, area, time);
        }

        frame.Paint.PopClip();
    }

    private static void Blend(IPaintSurface paint, Rect area, Vector4 top, Vector4 bottom, float radius)
    {
        if (radius <= 0.5f)
        {
            paint.FillGradient(area, top, bottom, GradientAxis.Vertical);
            return;
        }

        const int bands = 24;
        var height = area.Height;
        var overlap = MathF.Max(1.2f, height / bands * 0.35f);
        for (var index = 0; index < bands; index++)
        {
            var start = index / (float)bands;
            var end = (index + 1) / (float)bands;
            var slice = new Rect(
                new Vector2(area.Min.X, area.Min.Y + height * start),
                new Vector2(area.Max.X, MathF.Min(area.Max.Y, area.Min.Y + height * end + overlap)));
            var from = Mix(top, bottom, start);
            var to = Mix(top, bottom, end);
            if (index == 0)
            {
                paint.Fill(slice, Mix(from, to, 0.5f), radius, Painting.Corner.Top);
                continue;
            }

            if (index == bands - 1)
            {
                paint.Fill(slice, Mix(from, to, 0.5f), radius, Painting.Corner.Bottom);
                continue;
            }

            paint.FillGradient(slice, from, to, GradientAxis.Vertical);
        }
    }

    private static Vector4 Mix(Vector4 from, Vector4 to, float amount)
    {
        var t = Math.Clamp(amount, 0f, 1f);
        return from + (to - from) * t;
    }

    private static void Wash(string condition, bool night, out Vector4 top, out Vector4 bottom)
    {
        if (Snow(condition))
        {
            top = night ? new Vector4(0.78f, 0.84f, 0.92f, 1f) : new Vector4(0.92f, 0.96f, 1f, 1f);
            bottom = night ? new Vector4(0.52f, 0.58f, 0.68f, 1f) : new Vector4(0.72f, 0.80f, 0.90f, 1f);
            return;
        }

        if (Fog(condition) || Rain(condition))
        {
            top = night ? new Vector4(0.58f, 0.60f, 0.64f, 1f) : new Vector4(0.76f, 0.78f, 0.82f, 1f);
            bottom = night ? new Vector4(0.28f, 0.30f, 0.34f, 1f) : new Vector4(0.48f, 0.50f, 0.54f, 1f);
            return;
        }

        if (night)
        {
            top = new Vector4(0.16f, 0.28f, 0.52f, 1f);
            bottom = new Vector4(0.03f, 0.05f, 0.12f, 1f);
            return;
        }

        if (Overcast(condition))
        {
            top = new Vector4(0.46f, 0.58f, 0.72f, 1f);
            bottom = new Vector4(0.18f, 0.26f, 0.38f, 1f);
            return;
        }

        top = new Vector4(0.46f, 0.72f, 0.98f, 1f);
        bottom = new Vector4(0.10f, 0.30f, 0.62f, 1f);
    }

    public static void Hero(in AppletFrame frame, Rect area, string? condition, bool night)
    {
        if (area.IsEmpty)
        {
            return;
        }

        var size = MathF.Min(area.Width, area.Height);
        var center = area.Center;
        var radius = size * 0.28f;
        if (night)
        {
            frame.Paint.Glow(Rect.FromSize(center - new Vector2(radius * 1.8f), new Vector2(radius * 3.6f)),
                new Vector4(0.82f, 0.88f, 1f, 0.22f), radius * 1.6f, radius);
            frame.Paint.FillCircle(center, radius, new Vector4(0.94f, 0.95f, 1f, 0.94f));
            frame.Paint.FillCircle(center + new Vector2(radius * 0.38f, -radius * 0.18f), radius * 0.86f,
                new Vector4(0.08f, 0.10f, 0.22f, 0.92f));
        }
        else
        {
            frame.Paint.Glow(Rect.FromSize(center - new Vector2(radius * 2.2f), new Vector2(radius * 4.4f)),
                new Vector4(1f, 0.86f, 0.38f, 0.36f), radius * 2f, radius * 1.5f);
            frame.Paint.FillCircle(center, radius, new Vector4(1f, 0.90f, 0.42f, 1f));
            frame.Paint.FillCircle(center, radius * 0.62f, new Vector4(1f, 0.96f, 0.70f, 1f));
        }

        if (Overcast(condition) || Wet(condition))
        {
            Puff(frame.Paint, center + new Vector2(-radius * 0.55f, radius * 0.55f), radius * 0.72f,
                new Vector4(1f, 1f, 1f, night ? 0.55f : 0.82f));
            Puff(frame.Paint, center + new Vector2(radius * 0.62f, radius * 0.42f), radius * 0.58f,
                new Vector4(0.92f, 0.94f, 0.98f, night ? 0.42f : 0.70f));
        }
    }

    public static void Pin(IPaintSurface paint, Rect area, Vector4 ink)
    {
        var size = MathF.Min(area.Width, area.Height) * 0.32f;
        paint.StrokeCircle(area.Center - new Vector2(0f, size * 0.12f), size, ink, MathF.Max(1.2f, size * 0.22f));
        paint.Line(area.Center + new Vector2(0f, size * 0.18f), area.Center + new Vector2(0f, size * 0.72f), ink,
            MathF.Max(1.2f, size * 0.22f));
    }

    public static string Uv(EorzeaTime bells) => bells.Hour switch
    {
        >= 10 and < 16 => "High",
        >= 8 and < 18 => "Moderate",
        >= 6 and < 20 => "Low",
        _ => "None",
    };

    public static string Precip(string? weather)
    {
        if (Has(weather, "blizzard") || Has(weather, "snow"))
        {
            return "Snow";
        }

        if (Wet(weather))
        {
            return "Rain";
        }

        return "None";
    }

    public static void Icon(in AppletFrame frame, Rect area, string? name, uint iconId, EorzeaTime bells)
    {
        if (iconId != 0)
        {
            var texture = frame.Textures.GameIcon(iconId);
            if (texture is { IsReady: true })
            {
                frame.Paint.Image(texture, CoverFit.Contained(texture.Size, area), Vector4.One);
                return;
            }
        }

        DrawMark(frame.Paint, area, name, bells);
    }

    public static string ClockLabel(int hour)
    {
        var wrapped = ((hour % 24) + 24) % 24;
        return wrapped.ToString("00", System.Globalization.CultureInfo.InvariantCulture);
    }

    private static void DrawMark(IPaintSurface paint, Rect area, string? name, EorzeaTime bells)
    {
        var size = MathF.Min(area.Width, area.Height) * 0.42f;
        var center = area.Center;
        var stroke = MathF.Max(1.4f, size * 0.14f);
        var ink = new Vector4(1f, 1f, 1f, 0.92f);
        if (Wet(name))
        {
            paint.StrokeCircle(center + new Vector2(-size * 0.10f, -size * 0.18f), size * 0.42f, ink, stroke);
            paint.Line(center + new Vector2(-size * 0.28f, size * 0.22f),
                center + new Vector2(-size * 0.12f, size * 0.52f), ink, stroke);
            paint.Line(center + new Vector2(0.06f * size, size * 0.18f),
                center + new Vector2(0.22f * size, size * 0.52f), ink, stroke);
            return;
        }

        if (Overcast(name))
        {
            paint.StrokeCircle(center + new Vector2(-size * 0.16f, 0f), size * 0.36f, ink, stroke);
            paint.StrokeCircle(center + new Vector2(size * 0.22f, size * 0.04f), size * 0.42f, ink, stroke);
            return;
        }

        if (IsNight(bells))
        {
            paint.FillCircle(center, size * 0.46f, ink);
            paint.FillCircle(center + new Vector2(size * 0.22f, -size * 0.10f), size * 0.40f,
                new Vector4(0.12f, 0.14f, 0.22f, 0.95f));
            return;
        }

        paint.FillCircle(center, size * 0.36f, ink);
    }

    private static void DrawClouds(IPaintSurface paint, Rect art, bool night, float time)
    {
        var tint = night ? new Vector4(0.78f, 0.82f, 0.92f, 0.22f) : new Vector4(1f, 1f, 1f, 0.28f);
        var drift = MathF.Sin(time * 0.22f) * art.Width * 0.04f;
        Puff(paint, new Vector2(art.Min.X + art.Width * 0.28f + drift, art.Min.Y + art.Height * 0.38f),
            art.Height * 0.12f, tint);
        Puff(paint, new Vector2(art.Min.X + art.Width * 0.58f - drift * 0.6f, art.Min.Y + art.Height * 0.30f),
            art.Height * 0.16f, tint);
        Puff(paint, new Vector2(art.Min.X + art.Width * 0.84f + drift * 0.35f, art.Min.Y + art.Height * 0.42f),
            art.Height * 0.11f, tint with { W = tint.W * 0.7f });
    }

    private static void Puff(IPaintSurface paint, Vector2 center, float size, Vector4 color)
    {
        paint.Fill(
            Rect.FromSize(new Vector2(center.X - size, center.Y + size * 0.16f), new Vector2(size * 2f, size * 0.52f)),
            color, size * 0.26f);
        paint.FillCircle(center, size * 0.62f, color);
        paint.FillCircle(center + new Vector2(-size * 0.62f, size * 0.26f), size * 0.44f, color);
        paint.FillCircle(center + new Vector2(size * 0.68f, size * 0.24f), size * 0.4f, color);
    }

    private static void DrawRain(IPaintSurface paint, Rect art, float time)
    {
        var color = new Vector4(0.86f, 0.92f, 1f, 0.42f);
        var thickness = MathF.Max(1f, art.Height * 0.007f);
        var length = art.Height * 0.09f;
        for (var index = 0; index < 24; index++)
        {
            var seed = index * 0.137f;
            var x = art.Min.X + art.Width * Fract(seed * 3.1f);
            var y = art.Min.Y + art.Height * Fract(seed + time * 0.62f);
            paint.Line(new Vector2(x, y), new Vector2(x - length * 0.24f, y + length), color, thickness);
        }
    }

    private static void DrawSnow(IPaintSurface paint, Rect art, float time, bool heavy)
    {
        var count = heavy ? 28 : 20;
        var fall = heavy ? 0.18f : 0.11f;
        var color = new Vector4(1f, 1f, 1f, heavy ? 0.82f : 0.70f);
        for (var index = 0; index < count; index++)
        {
            var seed = index * 0.173f;
            var drift = MathF.Sin(time * 1.35f + seed * 8f) * art.Width * 0.025f;
            var x = art.Min.X + art.Width * Fract(seed * 2.7f) + drift;
            var y = art.Min.Y + art.Height * Fract(seed + time * fall);
            var radius = MathF.Max(1.1f, art.Height * (0.006f + index % 3 * 0.0024f));
            paint.FillCircle(new Vector2(x, y), radius, color);
        }
    }

    private static float Fract(float value) => value - MathF.Floor(value);

    private static bool Has(string? haystack, string needle) =>
        !string.IsNullOrEmpty(haystack) && haystack.Contains(needle, StringComparison.OrdinalIgnoreCase);
}
