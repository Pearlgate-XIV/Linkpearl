using Linkpearl.Geometry;
using Linkpearl.Media;
using Linkpearl.Painting;
using Linkpearl.Theming;

namespace Linkpearl.Applets;

public static class AppGround
{
    public static void Paint(in AppletFrame frame, Rect area, string appletId, float radius = 0f)
    {
        if (appletId == "music" && radius <= 0.5f)
        {
            MusicWash(frame, area);
            return;
        }

        Pair(appletId, out var top, out var bottom);
        Wash(frame.Paint, area, top, bottom, radius);
    }

    public static Vector4 Brand(string appletId) => appletId switch
    {
        "pearlchat" => new Vector4(0.12f, 0.70f, 0.64f, 1f),
        "phone" => new Vector4(0.20f, 0.66f, 0.33f, 1f),
        "music" => new Vector4(0.10f, 0.36f, 0.70f, 1f),
        "afterdark" or "daylight" => new Vector4(0.96f, 0.22f, 0.52f, 1f),
        "weather" => new Vector4(0.12f, 0.64f, 0.90f, 1f),
        "calendar" => new Vector4(0.90f, 0.32f, 0.28f, 1f),
        "wallet" => new Vector4(0.96f, 0.74f, 0.16f, 1f),
        "camera" => new Vector4(0.32f, 0.34f, 0.38f, 1f),
        "friends" => new Vector4(0.91f, 0.26f, 0.48f, 1f),
        "market" => new Vector4(0.00f, 0.54f, 0.48f, 1f),
        "appstore" or "announcements" => new Vector4(0.46f, 0.36f, 0.88f, 1f),
        "events" => new Vector4(0.96f, 0.45f, 0.16f, 1f),
        "eorzea" => new Vector4(0.22f, 0.38f, 0.72f, 1f),
        "settings" => new Vector4(0.42f, 0.45f, 0.48f, 1f),
        "feedback" => new Vector4(0.35f, 0.40f, 0.90f, 1f),
        "notes" => new Vector4(0.98f, 0.74f, 0.22f, 1f),
        "alarms" => new Vector4(0.90f, 0.22f, 0.21f, 1f),
        "clock" => new Vector4(0.25f, 0.52f, 0.96f, 1f),
        "calculator" => new Vector4(0.38f, 0.42f, 0.52f, 1f),
        "timer" => new Vector4(0.00f, 0.74f, 0.83f, 1f),
        "stopwatch" => new Vector4(0.90f, 0.32f, 0.12f, 1f),
        "party" => new Vector4(0.76f, 0.23f, 0.56f, 1f),
        _ => new Vector4(0.22f, 0.24f, 0.28f, 1f),
    };

    public static void Pair(string appletId, out Vector4 top, out Vector4 bottom)
    {
        switch (appletId)
        {
            case "calendar":
                top = new Vector4(0.16f, 0.10f, 0.16f, 1f);
                bottom = new Vector4(0.08f, 0.07f, 0.12f, 1f);
                return;
            case "pearlchat":
                top = new Vector4(0.07f, 0.15f, 0.14f, 1f);
                bottom = new Vector4(0.04f, 0.07f, 0.08f, 1f);
                return;
            case "phone":
                top = Palette.AppGround;
                bottom = Palette.AppGround;
                return;
            case "music":
                top = new Vector4(0.06f, 0.18f, 0.28f, 1f);
                bottom = new Vector4(0f, 0f, 0f, 1f);
                return;
            case "afterdark":
            case "daylight":
                top = new Vector4(0.08f, 0.16f, 0.42f, 1f);
                bottom = new Vector4(0f, 0f, 0f, 1f);
                return;
            case "weather":
                top = new Vector4(0.07f, 0.13f, 0.20f, 1f);
                bottom = new Vector4(0.04f, 0.07f, 0.11f, 1f);
                return;
            case "wallet":
                top = new Vector4(0.18f, 0.14f, 0.06f, 1f);
                bottom = new Vector4(0.09f, 0.07f, 0.04f, 1f);
                return;
            case "camera":
                top = new Vector4(0.12f, 0.12f, 0.14f, 1f);
                bottom = new Vector4(0.06f, 0.06f, 0.07f, 1f);
                return;
            case "friends":
                top = new Vector4(0.20f, 0.08f, 0.12f, 1f);
                bottom = new Vector4(0.10f, 0.05f, 0.07f, 1f);
                return;
            case "market":
                top = new Vector4(0.05f, 0.15f, 0.13f, 1f);
                bottom = new Vector4(0.03f, 0.08f, 0.07f, 1f);
                return;
            case "appstore":
            case "announcements":
                top = new Vector4(0.13f, 0.09f, 0.20f, 1f);
                bottom = new Vector4(0.07f, 0.05f, 0.11f, 1f);
                return;
            case "events":
                top = new Vector4(0.20f, 0.11f, 0.06f, 1f);
                bottom = new Vector4(0.10f, 0.06f, 0.04f, 1f);
                return;
            case "eorzea":
                top = new Vector4(0.08f, 0.10f, 0.20f, 1f);
                bottom = new Vector4(0.04f, 0.06f, 0.11f, 1f);
                return;
            case "settings":
                top = new Vector4(0.12f, 0.13f, 0.14f, 1f);
                bottom = new Vector4(0.07f, 0.07f, 0.08f, 1f);
                return;
            case "feedback":
                top = new Vector4(0.10f, 0.11f, 0.22f, 1f);
                bottom = new Vector4(0.05f, 0.06f, 0.12f, 1f);
                return;
            case "notes":
                top = new Vector4(0.18f, 0.14f, 0.06f, 1f);
                bottom = new Vector4(0.09f, 0.07f, 0.04f, 1f);
                return;
            case "alarms":
                top = new Vector4(0.20f, 0.08f, 0.08f, 1f);
                bottom = new Vector4(0.10f, 0.05f, 0.05f, 1f);
                return;
            case "clock":
                top = new Vector4(0.07f, 0.11f, 0.22f, 1f);
                bottom = new Vector4(0.04f, 0.06f, 0.12f, 1f);
                return;
            case "calculator":
                top = new Vector4(0.11f, 0.12f, 0.16f, 1f);
                bottom = new Vector4(0.06f, 0.07f, 0.09f, 1f);
                return;
            case "timer":
                top = new Vector4(0.05f, 0.15f, 0.17f, 1f);
                bottom = new Vector4(0.03f, 0.08f, 0.09f, 1f);
                return;
            case "stopwatch":
                top = new Vector4(0.20f, 0.10f, 0.06f, 1f);
                bottom = new Vector4(0.10f, 0.05f, 0.04f, 1f);
                return;
            case "party":
                top = new Vector4(0.18f, 0.07f, 0.14f, 1f);
                bottom = new Vector4(0.09f, 0.04f, 0.08f, 1f);
                return;
            default:
                var brand = Brand(appletId);
                top = Shade(brand, 0.18f, 0.07f);
                bottom = Shade(brand, 0.08f, 0.05f);
                return;
        }
    }

    public static void MusicWash(in AppletFrame frame, Rect area, float radius = 0f)
    {
        _ = radius;
        var black = new Vector4(0f, 0f, 0f, 1f);
        frame.Paint.Fill(area, black);
        MusicSky(frame.Paint, area, black);
        DrawMusicLagoon(frame, area);
    }

    public static void MusicWash(IPaintSurface paint, Rect area, float radius = 0f)
    {
        _ = radius;
        var black = new Vector4(0f, 0f, 0f, 1f);
        paint.Fill(area, black);
        MusicSky(paint, area, black);
    }

    private static void DrawMusicLagoon(in AppletFrame frame, Rect area)
    {
        var path = frame.Paths.Asset(Path.Combine(WallpaperCatalog.Folder, "music-lagoon.png"));
        var texture = frame.Textures.FromFile(path);
        if (texture is not { IsReady: true })
        {
            return;
        }

        var crop = CoverFit.Uv(texture.Size, area.Size);
        frame.Paint.Image(texture, area, new Vector2(crop.Max.X, crop.Min.Y), new Vector2(crop.Min.X, crop.Max.Y),
            Vector4.One);
        frame.Paint.FillCorners(area, new Vector4(0f, 0f, 0f, 0f), new Vector4(0f, 0f, 0f, 0f),
            new Vector4(0f, 0f, 0f, 0.90f), new Vector4(0f, 0f, 0f, 0.12f));
    }

    private static void MusicSky(IPaintSurface paint, Rect area, Vector4 black)
    {
        var blue = new Vector4(0.16f, 0.58f, 0.82f, 1f);
        var sky = area.TopSlice(area.Height * 0.56f);
        if (sky.Height < 8f)
        {
            return;
        }

        var origin = new Vector2(area.Max.X, area.Min.Y);
        var reach = sky.Height * 1.06f;
        const int steps = 52;
        for (var step = steps; step >= 1; step--)
        {
            var t = step / (float)steps;
            var mix = MathF.Pow(1f - t, 2.2f);
            if (mix < 0.012f)
            {
                continue;
            }

            paint.FillCircle(origin, reach * t, black + (blue - black) * mix);
        }
    }

    private static Vector4 Shade(Vector4 brand, float mix, float floor) =>
        new(brand.X * mix + floor, brand.Y * mix + floor, brand.Z * mix + floor, 1f);

    private static void Wash(IPaintSurface paint, Rect area, Vector4 top, Vector4 bottom, float radius)
    {
        if (radius <= 0.5f)
        {
            paint.FillGradient(area, top, bottom, GradientAxis.Vertical);
            return;
        }

        const int bands = 20;
        var height = area.Height;
        var overlap = MathF.Max(1.2f, height / bands * 0.35f);
        for (var index = 0; index < bands; index++)
        {
            var start = index / (float)bands;
            var end = (index + 1) / (float)bands;
            var slice = new Rect(
                new Vector2(area.Min.X, area.Min.Y + height * start),
                new Vector2(area.Max.X, MathF.Min(area.Max.Y, area.Min.Y + height * end + overlap)));
            var color = top + (bottom - top) * ((start + end) * 0.5f);
            if (index == 0)
            {
                paint.Fill(slice, color, radius, Corner.Top);
                continue;
            }

            if (index == bands - 1)
            {
                paint.Fill(slice, color, radius, Corner.Bottom);
                continue;
            }

            paint.Fill(slice, color);
        }
    }
}
