using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Linkpearl.Talk;

internal sealed class TalkShelf
{
    private const int MaxLines = 400;

    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly string directory;

    public TalkShelf(string directory)
    {
        this.directory = directory;
    }

    public ShelfFile Load(ulong contentId)
    {
        if (contentId == 0UL)
        {
            return new ShelfFile();
        }

        var path = PathFor(contentId);
        if (!File.Exists(path))
        {
            return new ShelfFile { ContentId = contentId.ToString(CultureInfo.InvariantCulture) };
        }

        try
        {
            var json = File.ReadAllText(path);
            var file = JsonSerializer.Deserialize<ShelfFile>(json, Json);
            return file ?? new ShelfFile { ContentId = contentId.ToString(CultureInfo.InvariantCulture) };
        }
        catch (JsonException)
        {
            return new ShelfFile { ContentId = contentId.ToString(CultureInfo.InvariantCulture) };
        }
        catch (IOException)
        {
            return new ShelfFile { ContentId = contentId.ToString(CultureInfo.InvariantCulture) };
        }
    }

    public void Save(ulong contentId, IEnumerable<RoomSnapshot> tells, IReadOnlyDictionary<string, string> extraNotes,
        IReadOnlyCollection<string> hidden)
    {
        if (contentId == 0UL)
        {
            return;
        }

        Directory.CreateDirectory(directory);
        var threads = new List<ShelfThread>();
        foreach (var room in tells)
        {
            if (room.Kind != TalkKind.Tell)
            {
                continue;
            }

            if (room.Lines.Count == 0 && room.Note.Length == 0 && room.LastAt == DateTimeOffset.MinValue)
            {
                continue;
            }

            var start = Math.Max(room.Lines.Count - MaxLines, 0);
            var lines = new ShelfLine[room.Lines.Count - start];
            for (var index = start; index < room.Lines.Count; index++)
            {
                var line = room.Lines[index];
                lines[index - start] = new ShelfLine(line.Sender, line.Body, line.At.ToUnixTimeSeconds(), line.Mine);
            }

            threads.Add(new ShelfThread(room.Id, room.Title, room.World, room.Note, room.Preview,
                room.LastAt == DateTimeOffset.MinValue ? 0L : room.LastAt.ToUnixTimeSeconds(), room.Unread, lines));
        }

        var notes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in extraNotes)
        {
            if (pair.Value.Trim().Length > 0)
            {
                notes[pair.Key] = pair.Value;
            }
        }

        var hid = new List<string>();
        foreach (var id in hidden)
        {
            if (id.Length > 0)
            {
                hid.Add(id);
            }
        }

        var file = new ShelfFile
        {
            ContentId = contentId.ToString(CultureInfo.InvariantCulture),
            Threads = threads.ToArray(),
            Notes = notes.Count > 0 ? notes : null,
            Hidden = hid.Count > 0 ? hid.ToArray() : null,
        };
        var path = PathFor(contentId);
        var temp = path + ".tmp";
        try
        {
            File.WriteAllText(temp, JsonSerializer.Serialize(file, Json));
            File.Move(temp, path, overwrite: true);
        }
        catch (IOException)
        {
            try
            {
                if (File.Exists(temp))
                {
                    File.Delete(temp);
                }
            }
            catch (IOException)
            {
            }
        }
    }

    private string PathFor(ulong contentId) =>
        Path.Combine(directory, contentId.ToString("x16", CultureInfo.InvariantCulture) + ".json");

    internal readonly record struct RoomSnapshot(
        string Id,
        TalkKind Kind,
        string Title,
        string World,
        string Note,
        string Preview,
        DateTimeOffset LastAt,
        int Unread,
        IReadOnlyList<TalkLine> Lines);

    internal sealed class ShelfFile
    {
        public string? ContentId { get; set; }

        public ShelfThread[]? Threads { get; set; }

        public Dictionary<string, string>? Notes { get; set; }

        public string[]? Hidden { get; set; }
    }

    internal sealed record ShelfThread(
        string? Id,
        string? Title,
        string? World,
        string? Note,
        string? Preview,
        long LastAtUnix,
        int Unread,
        ShelfLine[]? Lines);

    internal sealed record ShelfLine(string? Sender, string? Body, long AtUnix, bool Mine);
}
