using Linkpearl.Modules;

namespace Linkpearl.Media;

public static class PlateFiles
{
    public const string Folder = "plates";
    public const string CustomId = "custom";

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
        fileName = "yours" + ext.ToLowerInvariant();
        File.Copy(source, Absolute(paths, fileName), overwrite: true);
        return true;
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
