namespace Linkpearl.Media;

public readonly struct WallpaperPlate
{
    public readonly string Id;
    public readonly string Label;
    public readonly string DayFile;
    public readonly string NightFile;
    public readonly float DayLuminance;
    public readonly float NightLuminance;

    public WallpaperPlate(string id, string label, string dayFile, string nightFile, float dayLuminance,
        float nightLuminance)
    {
        Id = id;
        Label = label;
        DayFile = dayFile;
        NightFile = nightFile;
        DayLuminance = dayLuminance;
        NightLuminance = nightLuminance;
    }
}

public enum AppearanceMode : byte
{
    FollowClock = 0,
    Day = 1,
    Night = 2,
}

// Bundled plates live under Wallpapers/ next to the plugin dll. Luminance is measured from the
// generated files (a 32-sample grid) so the scrim does not have to decode GPU textures.
public static class WallpaperCatalog
{
    public const string Folder = "Wallpapers";
    public const string DefaultId = "lagoon";

    public static IReadOnlyList<WallpaperPlate> All { get; } = new[]
    {
        new WallpaperPlate("lagoon", "Lagoon", "lagoon-day.png", "lagoon-night.png", 0.20f, 0.11f),
        new WallpaperPlate("vine", "Vine", "vine-fiber.png", "vine-fiber.png", 0.216f, 0.216f),
        new WallpaperPlate("grove", "Grove", "grove.png", "grove.png", 0.434f, 0.434f),
    };

    public static WallpaperPlate Default => All[0];

    public static WallpaperPlate Resolve(string id)
    {
        for (var index = 0; index < All.Count; index++)
        {
            if (string.Equals(All[index].Id, id, StringComparison.Ordinal))
            {
                return All[index];
            }
        }

        return Default;
    }
}
