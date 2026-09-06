using System.IO;
using Linkpearl.Applets;
using Linkpearl.Geometry;
using Linkpearl.Media;
using Linkpearl.Modules;
using Linkpearl.Painting;
using Linkpearl.Preferences;
using Linkpearl.Time;

namespace Linkpearl.Device.Shell;

internal static class PlateFrost
{
    public static void Draw(in AppletFrame frame, Rect area, DisplayPreferences display, IClock clock,
        float reveal = 1f)
    {
        var paint = frame.Paint;
        var path = PathFor(frame.Paths, display, clock);
        var texture = path.Length > 0 ? frame.Textures.FromFile(path) : null;
        if (texture is { IsReady: true })
        {
            var radius = frame.Units(28f);
            var smear = frame.Units(5f);
            Bloom(paint, texture, area, area.Width * 0.04f, 0.22f * reveal, radius);
            Bloom(paint, texture, area, area.Width * 0.09f, 0.18f * reveal, radius);
            Bloom(paint, texture, area, area.Width * 0.16f, 0.14f * reveal, radius);
            Bloom(paint, texture, area, area.Width * 0.24f, 0.1f * reveal, radius);
            var cropFit = CoverFit.Uv(texture.Size, area.Size);
            var haze = new Vector4(0.5f, 0.5f, 0.54f, 0.1f * reveal);
            Shift(paint, texture, area, new Vector2(-smear, 0f), cropFit, haze, radius);
            Shift(paint, texture, area, new Vector2(smear, 0f), cropFit, haze, radius);
            Shift(paint, texture, area, new Vector2(0f, -smear), cropFit, haze, radius);
            Shift(paint, texture, area, new Vector2(0f, smear), cropFit, haze, radius);
            Shift(paint, texture, area, new Vector2(-smear, -smear), cropFit, haze, radius);
            Shift(paint, texture, area, new Vector2(smear, smear), cropFit, haze, radius);
        }

        paint.Fill(area, new Vector4(0.08f, 0.08f, 0.1f, 0.58f * reveal));
        paint.FillGradient(area, new Vector4(0.14f, 0.14f, 0.16f, 0.36f * reveal),
            new Vector4(0.03f, 0.03f, 0.04f, 0.7f * reveal), GradientAxis.Vertical);
        paint.Glow(area, new Vector4(0.18f, 0.18f, 0.2f, 0.22f * reveal), frame.Units(36f), frame.Units(18f));
    }

    private static string PathFor(HostPaths paths, DisplayPreferences display, IClock clock)
    {
        if (display.UsingCustomPlate && display.CustomPlateFile.Length > 0)
        {
            return PlateFiles.Absolute(paths, display.CustomPlateFile);
        }

        var plate = WallpaperCatalog.Resolve(display.WallpaperId);
        var night = display.Appearance == AppearanceMode.Night ||
                    (display.Appearance == AppearanceMode.FollowClock && DayNight.Darkness(clock.Now) >= 0.5f);
        return paths.Asset(Path.Combine(WallpaperCatalog.Folder, night ? plate.NightFile : plate.DayFile));
    }

    private static void Bloom(IPaintSurface paint, ITextureHandle texture, Rect area, float expand, float alpha,
        float radius)
    {
        var bloom = area.Expand(expand);
        var crop = CoverFit.Uv(texture.Size, bloom.Size);
        paint.ImageRounded(texture, bloom, crop.Min, crop.Max, new Vector4(0.62f, 0.62f, 0.66f, alpha), radius);
    }

    private static void Shift(IPaintSurface paint, ITextureHandle texture, Rect area, Vector2 shift, CoverUv crop,
        Vector4 tint, float radius)
    {
        paint.ImageRounded(texture, Rect.FromSize(area.Min + shift, area.Size), crop.Min, crop.Max, tint, radius);
    }
}
