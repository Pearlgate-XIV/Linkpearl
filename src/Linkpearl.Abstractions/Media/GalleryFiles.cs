using System.Text.Json;
using Linkpearl.Modules;

namespace Linkpearl.Media;

public readonly struct GalleryShot
{
    public string Title { get; }
    public string Path { get; }

    public GalleryShot(string title, string path)
    {
        Title = title;
        Path = path;
    }
}

public static class GalleryFiles
{
    public static string DefaultRoot(HostPaths paths)
    {
        var root = paths.State("photos");
        Directory.CreateDirectory(root);
        return Path.GetFullPath(root);
    }

    public static string Root(HostPaths paths)
    {
        var preferred = string.Empty;
        var pointer = PointerFile(paths);
        try
        {
            if (File.Exists(pointer))
            {
                preferred = File.ReadAllText(pointer).Trim();
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }

        return Resolve(paths, preferred);
    }

    public static string Resolve(HostPaths paths, string preferred)
    {
        var custom = preferred.Trim();
        if (custom.Length > 0)
        {
            try
            {
                Directory.CreateDirectory(custom);
                if (Directory.Exists(custom))
                {
                    return Path.GetFullPath(custom);
                }
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        return DefaultRoot(paths);
    }

    public static void Remember(HostPaths paths, string folder)
    {
        var fallback = DefaultRoot(paths);
        var pointer = PointerFile(paths);
        var custom = folder.Trim();
        if (custom.Length == 0 ||
            string.Equals(Path.GetFullPath(custom), fallback, StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                if (File.Exists(pointer))
                {
                    File.Delete(pointer);
                }
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }

            return;
        }

        try
        {
            File.WriteAllText(pointer, Path.GetFullPath(custom));
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    public static bool UsesDefault(HostPaths paths) =>
        string.Equals(Root(paths), DefaultRoot(paths), StringComparison.OrdinalIgnoreCase);

    private static string PointerFile(HostPaths paths) =>
        Path.Combine(DefaultRoot(paths), "store.path");

    public static IReadOnlyList<GalleryShot> List(HostPaths paths)
    {
        var root = Root(paths);
        var catalog = Path.Combine(root, "library.json");
        if (!File.Exists(catalog))
        {
            return Array.Empty<GalleryShot>();
        }

        try
        {
            var dto = JsonSerializer.Deserialize<GallerySave>(File.ReadAllText(catalog));
            if (dto?.Photos is null || dto.Photos.Length == 0)
            {
                return Array.Empty<GalleryShot>();
            }

            var shots = new List<GalleryShot>(dto.Photos.Length);
            for (var index = 0; index < dto.Photos.Length; index++)
            {
                var row = dto.Photos[index];
                if (row is null || string.IsNullOrWhiteSpace(row.Relative))
                {
                    continue;
                }

                var absolute = Path.Combine(root, row.Relative.Replace('/', Path.DirectorySeparatorChar));
                if (!File.Exists(absolute))
                {
                    continue;
                }

                var title = row.Title is { Length: > 0 } ? row.Title : Path.GetFileName(row.Relative);
                shots.Add(new GalleryShot(title, absolute));
            }

            return shots;
        }
        catch (JsonException)
        {
            return Array.Empty<GalleryShot>();
        }
        catch (IOException)
        {
            return Array.Empty<GalleryShot>();
        }
    }

    private sealed class GallerySave
    {
        public GalleryRow[]? Photos { get; set; }
    }

    private sealed class GalleryRow
    {
        public string? Relative { get; set; }

        public string? Title { get; set; }
    }
}
