using Linkpearl.Badges;
using Linkpearl.Media;
using Linkpearl.Modules;

namespace Linkpearl.Preferences;

public static class HandsetLook
{
    public static string Name(DisplayPreferences display, string linked, bool patron)
    {
        var name = GlassName.ProfileName(display, linked, patron);
        return name;
    }

    public static string Name(DisplayPreferences display, string linked) =>
        Name(display, linked, false);

    public static string Honorific(DisplayPreferences display) =>
        GlassName.Honorific(display);

    public static string PortraitFile(HostPaths paths, BadgeBook book) =>
        book.PortraitFile.Length == 0 ? string.Empty : PortraitFiles.Absolute(paths, book.PortraitFile);

    public static string BannerFile(HostPaths paths, DisplayPreferences display) =>
        display.UsingBanner ? BannerFiles.Absolute(paths, display.CustomBannerFile) : string.Empty;

    public static string ReplaceStill(HostPaths paths, string previous, string source, string stem)
    {
        var dest = CopyStill(paths, source, stem);
        ForgetOwned(previous, dest, stem);
        return dest;
    }

    private static string CopyStill(HostPaths paths, string source, string stem)
    {
        var file = (source ?? string.Empty).Trim().Trim('"');
        if (file.Length == 0 || !File.Exists(file))
        {
            return string.Empty;
        }

        var ext = Path.GetExtension(file);
        if (ext.Length == 0)
        {
            ext = ".png";
        }

        Directory.CreateDirectory(paths.StateDirectory);
        var dest = paths.State(stem + "-" + Guid.NewGuid().ToString("N") + ext.ToLowerInvariant());
        try
        {
            File.Copy(file, dest, overwrite: false);
            return dest;
        }
        catch (IOException)
        {
            return string.Empty;
        }
        catch (UnauthorizedAccessException)
        {
            return string.Empty;
        }
    }

    private static void ForgetOwned(string previous, string dest, string stem)
    {
        if (previous.Length == 0 ||
            string.Equals(previous, dest, StringComparison.OrdinalIgnoreCase) ||
            !File.Exists(previous))
        {
            return;
        }

        var name = Path.GetFileName(previous);
        if (!name.StartsWith(stem, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        try
        {
            File.Delete(previous);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
