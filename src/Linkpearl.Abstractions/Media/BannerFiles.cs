using Linkpearl.Modules;

namespace Linkpearl.Media;

public static class BannerFiles
{
    public const string Folder = "banners";

    public static string DirectoryOf(HostPaths paths) => Path.Combine(paths.ConfigDirectory, Folder);

    public static string Absolute(HostPaths paths, string fileName) =>
        Path.Combine(DirectoryOf(paths), fileName);

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

    public static void Clear(HostPaths paths)
    {
        var folder = DirectoryOf(paths);
        if (!Directory.Exists(folder))
        {
            return;
        }

        foreach (var file in Directory.GetFiles(folder))
        {
            try
            {
                File.Delete(file);
            }
            catch (IOException)
            {
            }
        }
    }

    private static bool IsImage(string ext)
    {
        return ext.Equals(".png", StringComparison.OrdinalIgnoreCase) ||
               ext.Equals(".jpg", StringComparison.OrdinalIgnoreCase) ||
               ext.Equals(".jpeg", StringComparison.OrdinalIgnoreCase) ||
               ext.Equals(".webp", StringComparison.OrdinalIgnoreCase);
    }
}
