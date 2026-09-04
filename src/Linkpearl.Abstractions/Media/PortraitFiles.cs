using Linkpearl.Modules;

namespace Linkpearl.Media;

public static class PortraitFiles
{
    public const string Folder = "portraits";

    public static string DirectoryOf(HostPaths paths) => Path.Combine(paths.ConfigDirectory, Folder);

    public static string Absolute(HostPaths paths, string fileName) =>
        Path.Combine(DirectoryOf(paths), Path.GetFileName(fileName));

    public static bool TryImport(HostPaths paths, string sourcePath, out string fileName)
    {
        fileName = string.Empty;
        var source = sourcePath.Trim().Trim('"');
        if (source.Length == 0 || !File.Exists(source) || !PlateFiles.IsImage(Path.GetExtension(source)))
        {
            return false;
        }

        Directory.CreateDirectory(DirectoryOf(paths));
        fileName = "face-" + Guid.NewGuid().ToString("N") + Path.GetExtension(source).ToLowerInvariant();
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
}
