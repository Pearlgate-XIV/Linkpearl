using System.Text.Json;
using Linkpearl.Modules;

namespace Linkpearl.Media;

public readonly struct GalleryShot
{
    public readonly string Title;
    public readonly string Path;

    public GalleryShot(string title, string path)
    {
        Title = title;
        Path = path;
    }
}

public static class GalleryFiles
{
    public static IReadOnlyList<GalleryShot> List(HostPaths paths)
    {
        var root = paths.State("photos");
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
