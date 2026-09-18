using System.Globalization;
using Linkpearl.Diagnostics;
using Linkpearl.Media;
using Linkpearl.Modules;
using Linkpearl.Persistence;

namespace Linkpearl.Applets.Life.Camera;

internal sealed class PhotoShot
{
    public required string Id { get; init; }

    public required string Album { get; set; }

    public string Folder { get; set; } = string.Empty;

    public required string Relative { get; set; }

    public required string Title { get; init; }
}

internal sealed class PhotoFolder
{
    public required string Id { get; init; }

    public required string Title { get; set; }
}

internal sealed class PhotoLibrary
{
    private static readonly HashSet<string> ImageKinds = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png", ".jpg", ".jpeg", ".bmp", ".webp", ".gif",
    };

    private string root;
    private string catalog;
    private readonly ILinkpearlLog? log;
    private readonly List<PhotoShot> shots = new();
    private readonly List<PhotoFolder> folders = new();
    private JsonCorruptHold corrupt;
    private bool dirty;

    private PhotoLibrary(string root, string catalog, ILinkpearlLog? log)
    {
        this.root = root;
        this.catalog = catalog;
        this.log = log;
    }

    public string Root => root;

    public bool StorageReady => Directory.Exists(root);

    public IReadOnlyList<PhotoShot> Shots => shots;

    public IReadOnlyList<PhotoFolder> Folders => folders;

    public string GposeFolder { get; private set; } = string.Empty;

    public static PhotoLibrary Load(HostPaths paths, ILinkpearlLog? log = null)
    {
        var root = GalleryFiles.Root(paths);
        var catalog = Path.Combine(root, "library.json");
        var library = new PhotoLibrary(root, catalog, log);
        library.ReadCatalog();
        return library;
    }

    public bool Relocate(string nextRoot)
    {
        var dest = nextRoot.Trim();
        if (dest.Length == 0)
        {
            return false;
        }

        try
        {
            Directory.CreateDirectory(dest);
            dest = Path.GetFullPath(dest);
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }

        if (!Directory.Exists(dest))
        {
            return false;
        }

        if (string.Equals(Path.GetFullPath(root), dest, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var destCatalog = Path.Combine(dest, "library.json");
        if (File.Exists(destCatalog))
        {
            root = dest;
            catalog = destCatalog;
            ReadCatalog();
            return true;
        }

        for (var index = 0; index < shots.Count; index++)
        {
            var source = Absolute(shots[index]);
            var relative = shots[index].Relative.Replace('/', Path.DirectorySeparatorChar);
            var copy = Path.Combine(dest, relative);
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(copy) ?? dest);
                if (File.Exists(source) && !File.Exists(copy))
                {
                    File.Copy(source, copy, overwrite: false);
                }
            }
            catch (IOException)
            {
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
        }

        root = dest;
        catalog = destCatalog;
        Save();
        return true;
    }

    public string Absolute(PhotoShot shot) =>
        Path.Combine(root, shot.Relative.Replace('/', Path.DirectorySeparatorChar));

    public PhotoShot? Find(string id)
    {
        for (var index = 0; index < shots.Count; index++)
        {
            if (string.Equals(shots[index].Id, id, StringComparison.Ordinal))
            {
                return shots[index];
            }
        }

        return null;
    }

    public PhotoFolder? FolderOf(string id)
    {
        for (var index = 0; index < folders.Count; index++)
        {
            if (string.Equals(folders[index].Id, id, StringComparison.Ordinal))
            {
                return folders[index];
            }
        }

        return null;
    }

    public List<PhotoShot> InFolder(string folder)
    {
        var list = new List<PhotoShot>();
        for (var index = 0; index < shots.Count; index++)
        {
            if (string.Equals(shots[index].Folder, folder, StringComparison.Ordinal))
            {
                list.Add(shots[index]);
            }
        }

        return list;
    }

    public List<PhotoShot> InAlbum(string album)
    {
        var list = new List<PhotoShot>();
        for (var index = 0; index < shots.Count; index++)
        {
            var shot = shots[index];
            if (shot.Folder.Length == 0 && string.Equals(shot.Album, album, StringComparison.Ordinal))
            {
                list.Add(shot);
            }
        }

        return list;
    }

    public string NextAlbumTitle()
    {
        var taken = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < folders.Count; index++)
        {
            taken.Add(folders[index].Title);
        }

        if (!taken.Contains("Album"))
        {
            return "Album";
        }

        for (var number = 2; number < 100; number++)
        {
            var name = "Album " + number.ToString(CultureInfo.InvariantCulture);
            if (!taken.Contains(name))
            {
                return name;
            }
        }

        return "Album";
    }

    public PhotoFolder? CreateFolder(string title)
    {
        var name = title.Trim();
        if (name.Length == 0)
        {
            name = NextAlbumTitle();
        }

        var folder = new PhotoFolder { Id = Guid.NewGuid().ToString("N")[..10], Title = name };
        folders.Add(folder);
        Save();
        return folder;
    }

    public void RenameFolder(string id, string title)
    {
        var folder = FolderOf(id);
        var name = title.Trim();
        if (folder is null || name.Length == 0)
        {
            return;
        }

        folder.Title = name;
        Save();
    }

    public void DropFolder(string id)
    {
        if (!HasFolder(id))
        {
            return;
        }

        folders.RemoveAll(row => string.Equals(row.Id, id, StringComparison.Ordinal));
        for (var index = 0; index < shots.Count; index++)
        {
            if (string.Equals(shots[index].Folder, id, StringComparison.Ordinal))
            {
                shots[index].Folder = string.Empty;
            }
        }

        Save();
    }

    public bool Remove(string id, out string path)
    {
        path = string.Empty;
        var shot = Find(id);
        if (shot is null)
        {
            return false;
        }

        path = Absolute(shot);
        shots.Remove(shot);
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }

        Save();
        return true;
    }

    public int RemoveMany(IReadOnlyList<string> ids, List<string> forgotten)
    {
        var drop = new HashSet<string>(ids, StringComparer.Ordinal);
        if (drop.Count == 0)
        {
            return 0;
        }

        var removed = 0;
        for (var index = shots.Count - 1; index >= 0; index--)
        {
            var shot = shots[index];
            if (!drop.Contains(shot.Id))
            {
                continue;
            }

            var path = Absolute(shot);
            shots.RemoveAt(index);
            forgotten.Add(path);
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }

            removed++;
        }

        if (removed > 0)
        {
            Save();
        }

        return removed;
    }

    public int Import(IReadOnlyList<string> sources, DateTimeOffset now, string folder)
    {
        var added = 0;
        for (var index = 0; index < sources.Count; index++)
        {
            if (CopyIn(sources[index], now, folder, Path.GetFileName(sources[index])) is not null)
            {
                added++;
            }
        }

        if (added > 0)
        {
            Save();
        }

        return added;
    }

    private PhotoShot? CopyIn(string source, DateTimeOffset now, string folder, string title)
    {
        var kind = Path.GetExtension(source);
        if (!ImageKinds.Contains(kind) || !File.Exists(source))
        {
            return null;
        }

        var album = now.ToLocalTime().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var id = Guid.NewGuid().ToString("N");
        var ext = kind.ToLowerInvariant();
        var relative = album + "/" + id + ext;
        var dest = Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar));
        if (!StorageReady)
        {
            return null;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(dest) ?? root);
        try
        {
            File.Copy(source, dest, overwrite: false);
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }

        var placed = folder.Length > 0 && HasFolder(folder) ? folder : string.Empty;
        var shot = new PhotoShot
        {
            Id = id,
            Album = album,
            Folder = placed,
            Relative = relative,
            Title = title.Length > 0 ? title : Path.GetFileName(source),
        };
        shots.Insert(0, shot);
        return shot;
    }

    public void MoveToFolder(string id, string folder)
    {
        var shot = Find(id);
        if (shot is null)
        {
            return;
        }

        shot.Folder = folder.Length > 0 && HasFolder(folder) ? folder : string.Empty;
        Save();
    }

    public PhotoShot? CopyEdited(PhotoShot source, DateTimeOffset now, float x, float y, float width, float height,
        int turns)
    {
        var title = source.Title;
        if (!title.EndsWith(" copy", StringComparison.OrdinalIgnoreCase))
        {
            title += " copy";
        }

        return ImportEdited(Absolute(source), now, source.Folder, x, y, width, height, turns, title);
    }

    public PhotoShot? ImportEdited(string source, DateTimeOffset now, string folder, float x, float y, float width,
        float height, int turns, string? title = null)
    {
        var kind = Path.GetExtension(source);
        if (!ImageKinds.Contains(kind) || !File.Exists(source))
        {
            return null;
        }

        var album = now.ToLocalTime().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var id = Guid.NewGuid().ToString("N");
        var ext = string.Equals(kind, ".jpg", StringComparison.OrdinalIgnoreCase) ||
                  string.Equals(kind, ".jpeg", StringComparison.OrdinalIgnoreCase)
            ? ".jpg"
            : ".png";
        var relative = album + "/" + id + ext;
        var dest = Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar));
        if (!PhotoEdit.Write(source, dest, x, y, width, height, turns))
        {
            return null;
        }

        var placed = folder.Length > 0 && HasFolder(folder) ? folder : string.Empty;
        var shot = new PhotoShot
        {
            Id = id,
            Album = album,
            Folder = placed,
            Relative = relative,
            Title = title is { Length: > 0 } ? title : Path.GetFileName(source),
        };
        shots.Insert(0, shot);
        Save();
        return shot;
    }

    public bool Rewrite(PhotoShot shot, float x, float y, float width, float height, int turns, out string previous)
    {
        previous = Absolute(shot);
        var ext = Path.GetExtension(shot.Relative);
        if (ext.Length == 0)
        {
            ext = ".png";
        }

        var stamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);
        var relative = shot.Album + "/" + shot.Id + "_" + stamp + ext.ToLowerInvariant();
        var dest = Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar));
        if (!PhotoEdit.Write(previous, dest, x, y, width, height, turns))
        {
            return false;
        }

        try
        {
            if (!string.Equals(previous, dest, StringComparison.OrdinalIgnoreCase) && File.Exists(previous))
            {
                File.Delete(previous);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }

        shot.Relative = relative;
        Save();
        return true;
    }

    public void Save()
    {
        dirty = true;
        Persist();
    }

    public void Flush()
    {
        if (dirty)
        {
            Persist();
        }
    }

    private void Persist()
    {
        if (!StorageReady)
        {
            return;
        }

        var rows = new PhotoDto[shots.Count];
        for (var index = 0; index < shots.Count; index++)
        {
            var shot = shots[index];
            rows[index] = new PhotoDto
            {
                Id = shot.Id,
                Album = shot.Album,
                Folder = shot.Folder,
                Relative = shot.Relative,
                Title = shot.Title,
            };
        }

        var books = new FolderDto[folders.Count];
        for (var index = 0; index < folders.Count; index++)
        {
            books[index] = new FolderDto { Id = folders[index].Id, Title = folders[index].Title };
        }

        if (AtomicJson.TrySave(catalog, new PhotoSave
        {
            Photos = rows,
            Folders = books,
            GposeFolder = GposeFolder,
        }, null, ref corrupt, log))
        {
            dirty = false;
        }
    }

    public void SetGposeFolder(string path)
    {
        GposeFolder = path.Trim();
        Save();
    }

    public bool GposeFolderReady() =>
        GposeFolder.Length > 0 && Directory.Exists(GposeFolder);

    public static string SuggestedGposeFolder()
    {
        var documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        if (documents.Length == 0)
        {
            return string.Empty;
        }

        var shots = Path.Combine(documents, "My Games", "FINAL FANTASY XIV - A Realm Reborn", "screenshots");
        return Directory.Exists(shots) ? shots : string.Empty;
    }

    public static bool IsPicture(string path) => ImageKinds.Contains(Path.GetExtension(path));

    private void ReadCatalog()
    {
        shots.Clear();
        folders.Clear();
        GposeFolder = string.Empty;
        if (!AtomicJson.TryRead(catalog, null, out PhotoSave? dto, ref corrupt, log) || dto is null)
        {
            return;
        }

        if (dto.Folders is not null)
        {
            for (var index = 0; index < dto.Folders.Length; index++)
            {
                var row = dto.Folders[index];
                if (row is null || string.IsNullOrWhiteSpace(row.Id) || string.IsNullOrWhiteSpace(row.Title))
                {
                    continue;
                }

                folders.Add(new PhotoFolder { Id = row.Id, Title = row.Title.Trim() });
            }
        }

        GposeFolder = (dto.GposeFolder ?? string.Empty).Trim();
        if (dto.Photos is null)
        {
            return;
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

            var folder = row.Folder ?? string.Empty;
            if (folder.Length > 0 && !HasFolder(folder))
            {
                folder = string.Empty;
            }

            shots.Add(new PhotoShot
            {
                Id = row.Id,
                Album = row.Album ?? Path.GetDirectoryName(row.Relative)?.Replace('\\', '/') ?? string.Empty,
                Folder = folder,
                Relative = row.Relative.Replace('\\', '/'),
                Title = row.Title ?? Path.GetFileName(row.Relative),
            });
        }
    }

    private bool HasFolder(string id)
    {
        for (var index = 0; index < folders.Count; index++)
        {
            if (string.Equals(folders[index].Id, id, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private sealed class PhotoSave
    {
        public PhotoDto[]? Photos { get; set; }

        public FolderDto[]? Folders { get; set; }

        public string? GposeFolder { get; set; }
    }

    private sealed class PhotoDto
    {
        public string? Id { get; set; }

        public string? Album { get; set; }

        public string? Folder { get; set; }

        public string? Relative { get; set; }

        public string? Title { get; set; }
    }

    private sealed class FolderDto
    {
        public string? Id { get; set; }

        public string? Title { get; set; }
    }
}
