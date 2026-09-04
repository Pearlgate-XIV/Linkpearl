using System.Globalization;
using System.Text.Json;
using Linkpearl.Modules;

namespace Linkpearl.Applets.Life.Camera;

internal sealed class PhotoShot
{
    public required string Id { get; init; }

    public required string Album { get; init; }

    public required string Relative { get; init; }

    public required string Title { get; init; }
}

internal sealed class PhotoLibrary
{
    private static readonly HashSet<string> ImageKinds = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png", ".jpg", ".jpeg", ".bmp", ".webp", ".gif",
    };

    private readonly string root;
    private readonly string catalog;
    private readonly List<PhotoShot> shots = new();

    private PhotoLibrary(string root, string catalog)
    {
        this.root = root;
        this.catalog = catalog;
    }

    public IReadOnlyList<PhotoShot> Shots => shots;

    public static PhotoLibrary Load(HostPaths paths)
    {
        var root = paths.State("photos");
        var catalog = Path.Combine(root, "library.json");
        var library = new PhotoLibrary(root, catalog);
        Directory.CreateDirectory(root);
        if (!File.Exists(catalog))
        {
            return library;
        }

        try
        {
            var dto = JsonSerializer.Deserialize<PhotoSave>(File.ReadAllText(catalog));
            if (dto?.Photos is null)
            {
                return library;
            }

            for (var index = 0; index < dto.Photos.Length; index++)
            {
                var row = dto.Photos[index];
                if (row is null || string.IsNullOrWhiteSpace(row.Id) || string.IsNullOrWhiteSpace(row.Relative))
                {
                    continue;
                }

                var absolute = Path.Combine(root, row.Relative.Replace('/', Path.DirectorySeparatorChar));
                if (!File.Exists(absolute))
                {
                    continue;
                }

                library.shots.Add(new PhotoShot
                {
                    Id = row.Id,
                    Album = row.Album ?? Path.GetDirectoryName(row.Relative)?.Replace('\\', '/') ?? string.Empty,
                    Relative = row.Relative.Replace('\\', '/'),
                    Title = row.Title ?? Path.GetFileName(row.Relative),
                });
            }
        }
        catch (JsonException)
        {
        }
        catch (IOException)
        {
        }

        return library;
    }

    public string Absolute(PhotoShot shot) =>
        Path.Combine(root, shot.Relative.Replace('/', Path.DirectorySeparatorChar));

    public int Import(IReadOnlyList<string> sources, DateTimeOffset now)
    {
        var album = now.ToLocalTime().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var folder = Path.Combine(root, album);
        Directory.CreateDirectory(folder);
        var added = 0;
        for (var index = 0; index < sources.Count; index++)
        {
            var source = sources[index];
            var kind = Path.GetExtension(source);
            if (!ImageKinds.Contains(kind) || !File.Exists(source))
            {
                continue;
            }

            var id = Guid.NewGuid().ToString("N");
            var relative = album + "/" + id + kind.ToLowerInvariant();
            var dest = Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar));
            try
            {
                File.Copy(source, dest, overwrite: false);
            }
            catch (IOException)
            {
                continue;
            }
            catch (UnauthorizedAccessException)
            {
                continue;
            }

            shots.Insert(0, new PhotoShot
            {
                Id = id,
                Album = album,
                Relative = relative,
                Title = Path.GetFileName(source),
            });
            added++;
        }

        if (added > 0)
        {
            Save();
        }

        return added;
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(root);
            var rows = new PhotoDto[shots.Count];
            for (var index = 0; index < shots.Count; index++)
            {
                var shot = shots[index];
                rows[index] = new PhotoDto
                {
                    Id = shot.Id,
                    Album = shot.Album,
                    Relative = shot.Relative,
                    Title = shot.Title,
                };
            }

            File.WriteAllText(catalog, JsonSerializer.Serialize(new PhotoSave { Photos = rows }));
        }
        catch (IOException)
        {
        }
    }

    public static bool IsPicture(string path) => ImageKinds.Contains(Path.GetExtension(path));

    private sealed class PhotoSave
    {
        public PhotoDto[]? Photos { get; set; }
    }

    private sealed class PhotoDto
    {
        public string? Id { get; set; }

        public string? Album { get; set; }

        public string? Relative { get; set; }

        public string? Title { get; set; }
    }
}
