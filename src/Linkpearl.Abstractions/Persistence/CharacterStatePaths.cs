using System.Globalization;
using Linkpearl.Modules;

namespace Linkpearl.Persistence;

public static class CharacterStatePaths
{
    public const string ManifestName = "migration-v13.json";
    public const string CalendarName = "calendar.json";
    public const string PearlsName = "pearls.json";
    public const string HandsetLineName = "handset-line.json";

    public static bool TryHex(ulong contentId, out string hex)
    {
        if (contentId == 0UL)
        {
            hex = string.Empty;
            return false;
        }

        hex = contentId.ToString("x16", CultureInfo.InvariantCulture);
        return true;
    }

    public static string Hex(ulong contentId) =>
        TryHex(contentId, out var hex) ? hex : string.Empty;

    public static string ShortHex(ulong contentId)
    {
        var hex = Hex(contentId);
        return hex.Length <= 6 ? hex : hex[..6];
    }

    public static string Manifest(HostPaths paths) => paths.State(ManifestName);

    public static string Legacy(HostPaths paths, string fileName) => paths.State(fileName);

    public static string Folder(HostPaths paths, ulong contentId)
    {
        if (!TryHex(contentId, out var hex))
        {
            return string.Empty;
        }

        return paths.State(Path.Combine("characters", hex));
    }

    public static string File(HostPaths paths, ulong contentId, string fileName)
    {
        var folder = Folder(paths, contentId);
        return folder.Length == 0 ? string.Empty : Path.Combine(folder, fileName);
    }

    public static string Calendar(HostPaths paths, ulong contentId) =>
        File(paths, contentId, CalendarName);

    public static string Pearls(HostPaths paths, ulong contentId) =>
        File(paths, contentId, PearlsName);

    public static string HandsetLine(HostPaths paths, ulong contentId) =>
        File(paths, contentId, HandsetLineName);
}
