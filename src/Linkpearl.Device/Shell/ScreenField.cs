using Linkpearl.Geometry;
using Linkpearl.Media;
using Linkpearl.Modules;
using Linkpearl.Painting;
using Linkpearl.Platform;
using Linkpearl.Preferences;
using Linkpearl.Theming;
using Linkpearl.Time;

namespace Linkpearl.Device.Shell;

// Fills the glass with a cover-fit wallpaper pair, a slow day/night cross-fade, and a scrim
// whose strength follows the plate's luminance. The chassis still paints the fallback color
// underneath so a missing or still-loading file never leaves a hole.
public sealed class ScreenField
{
    private const float MixRate = 3.2f;

    private readonly ITextureSource textures;
    private readonly HostPaths paths;
    private readonly DisplayPreferences preferences;
    private readonly IClock clock;
    private float nightMix = 1f;

    public ScreenField(ITextureSource textures, HostPaths paths, DisplayPreferences preferences, IClock clock)
    {
        this.textures = textures;
        this.paths = paths;
        this.preferences = preferences;
        this.clock = clock;
    }

    public void Paint(IPaintSurface paint, ITheme theme, Rect screen, float deltaSeconds, float radius = 0f)
    {
        float luminance;
        if (preferences.UsingCustomPlate)
        {
            var drawn = DrawFile(paint, screen, PlateFiles.Absolute(paths, preferences.CustomPlateFile), 1f, radius);
            luminance = drawn ? 0.42f : (theme.IsDark ? 0.12f : 0.72f);
            nightMix = 0f;
        }
        else
        {
            var plate = WallpaperCatalog.Resolve(preferences.WallpaperId);
            var target = TargetDarkness();
            nightMix += (target - nightMix) * (1f - MathF.Exp(-MixRate * MathF.Max(deltaSeconds, 0f)));

            var day = DrawPlate(paint, screen, plate.DayFile, 1f, radius);
            var night = DrawPlate(paint, screen, plate.NightFile, nightMix, radius);
            luminance = plate.DayLuminance + (plate.NightLuminance - plate.DayLuminance) * nightMix;
            if (!day && !night)
            {
                luminance = theme.IsDark ? 0.12f : 0.72f;
            }
        }

        var scrim = theme.Palette.SurfaceSunken with { W = LegibilityScrim.Alpha(luminance) * preferences.ShadeMul };
        paint.Fill(screen, scrim, radius);
    }

    private float TargetDarkness() => preferences.Appearance switch
    {
        AppearanceMode.Day => 0f,
        AppearanceMode.Night => 1f,
        _ => DayNight.Darkness(clock.Now),
    };

    private bool DrawPlate(IPaintSurface paint, Rect screen, string fileName, float alpha, float radius) =>
        DrawFile(paint, screen, paths.Asset(Path.Combine(WallpaperCatalog.Folder, fileName)), alpha, radius);

    private bool DrawFile(IPaintSurface paint, Rect screen, string path, float alpha, float radius)
    {
        if (alpha <= 0.004f)
        {
            return false;
        }

        var texture = textures.FromFile(path);
        if (texture is null || !texture.IsReady)
        {
            return false;
        }

        var crop = CoverFit.Uv(texture.Size, screen.Size);
        var tint = new Vector4(1f, 1f, 1f, alpha);
        if (radius > 0.5f)
        {
            paint.ImageRounded(texture, screen, crop.Min, crop.Max, tint, radius);
        }
        else
        {
            paint.Image(texture, screen, crop.Min, crop.Max, tint);
        }

        return true;
    }
}
