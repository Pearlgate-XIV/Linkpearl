using Linkpearl.Modules;

namespace Linkpearl.Media;

public static class PlateFiles
{
    public const string Folder = "plates";
    public const string CustomId = "custom";
    public const int MaxSlots = 4;

    public static string DirectoryOf(HostPaths paths) => Path.Combine(paths.ConfigDirectory, Folder);

    public static string Absolute(HostPaths paths, string fileName) =>
        Path.Combine(DirectoryOf(paths), Path.GetFileName(fileName));

    public static bool TryImport(HostPaths paths, string sourcePath, out string fileName)
    {
        fileName = string.Empty;
        var source = sourcePath.Trim().Trim('"');
        if (source.Length == 0 || !File.Exists(source))
        {
            return false;
        }

        var ext = Path.GetExtension(source);
        if (!IsImage(ext))
        {
            return false;
        }

        Directory.CreateDirectory(DirectoryOf(paths));
        fileName = "yours-" + Guid.NewGuid().ToString("N") + ext.ToLowerInvariant();
        try
        {
            File.Copy(source, Absolute(paths, fileName), overwrite: false);
            return true;
        }
        catch (IOException)
        {
            fileName = string.Empty;
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            fileName = string.Empty;
            return false;
        }
    }

    public static void Delete(HostPaths paths, string fileName)
    {
        var name = Path.GetFileName(fileName);
        if (name.Length == 0)
        {
            return;
        }

        try
        {
            File.Delete(Absolute(paths, name));
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    public static bool IsImage(string ext)
    {
        return ext.Equals(".png", StringComparison.OrdinalIgnoreCase) ||
               ext.Equals(".jpg", StringComparison.OrdinalIgnoreCase) ||
               ext.Equals(".jpeg", StringComparison.OrdinalIgnoreCase) ||
               ext.Equals(".webp", StringComparison.OrdinalIgnoreCase) ||
               ext.Equals(".bmp", StringComparison.OrdinalIgnoreCase) ||
               ext.Equals(".gif", StringComparison.OrdinalIgnoreCase);
    }
}
