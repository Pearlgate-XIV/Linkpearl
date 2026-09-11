using System.Text.Json;
using Linkpearl.Modules;

namespace Linkpearl.Applets.Life.Venues;

public sealed class VenuesBook
{
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly string path;
    private readonly object gate = new();
    private readonly HashSet<string> held = new(StringComparer.Ordinal);
    private readonly HashSet<string> notify = new(StringComparer.Ordinal);
    private readonly HashSet<string> schedule = new(StringComparer.Ordinal);
    private string[] saved = [];
    private string[] watches = [];
    private string[] pins = [];
    private int revision;

    public VenuesBook(HostPaths paths)
    {
        path = paths.State("venues.json");
        Load();
    }

    public int Revision
    {
        get
        {
            lock (gate)
            {
                return revision;
            }
        }
    }

    public IReadOnlyList<string> Saved
    {
        get
        {
            lock (gate)
            {
                return saved;
            }
        }
    }

    public IReadOnlyList<string> Watches
    {
        get
        {
            lock (gate)
            {
                return watches;
            }
        }
    }

    public IReadOnlyList<string> Pins
    {
        get
        {
            lock (gate)
            {
                return pins;
            }
        }
    }

    public bool Holds(string id)
    {
        if (id.Length == 0)
        {
            return false;
        }

        lock (gate)
        {
            return held.Contains(id);
        }
    }

    public void Toggle(string id)
    {
        if (id.Length == 0)
        {
            return;
        }

        lock (gate)
        {
            var at = IndexOf(saved, id);
            if (at >= 0)
            {
                var next = new string[saved.Length - 1];
                Array.Copy(saved, 0, next, 0, at);
                Array.Copy(saved, at + 1, next, at, saved.Length - at - 1);
                saved = next;
                held.Remove(id);
            }
            else
            {
                var next = new string[saved.Length + 1];
                saved.CopyTo(next, 0);
                next[^1] = id;
                saved = next;
                held.Add(id);
            }

            revision++;
        }

        Persist();
    }

    public bool WatchesId(string id) => HoldsSet(notify, id);

    public bool PinsId(string id) => HoldsSet(schedule, id);

    public void SetWatch(string id, bool on) => SetSet(notify, ref watches, id, on);

    public void SetPin(string id, bool on) => SetSet(schedule, ref pins, id, on);

    private void Load()
    {
        try
        {
            if (!File.Exists(path))
            {
                return;
            }

            var blob = JsonSerializer.Deserialize<BookWire>(File.ReadAllText(path), Json);
            saved = blob?.Saved ?? [];
            watches = blob?.Notify ?? [];
            pins = blob?.Schedule ?? [];
            RememberHeld();
            Remember(notify, watches);
            Remember(schedule, pins);
        }
        catch
        {
            saved = [];
            watches = [];
            pins = [];
            held.Clear();
            notify.Clear();
            schedule.Clear();
        }
    }

    private void Persist()
    {
        try
        {
            var folder = Path.GetDirectoryName(path);
            if (folder is { Length: > 0 })
            {
                Directory.CreateDirectory(folder);
            }

            BookWire blob;
            lock (gate)
            {
                blob = new BookWire { Saved = saved, Notify = watches, Schedule = pins };
            }

            File.WriteAllText(path, JsonSerializer.Serialize(blob, Json));
        }
        catch
        {
            // Local favorites stay in memory if the write is refused.
        }
    }

    private void RememberHeld() => Remember(held, saved);

    private static void Remember(HashSet<string> into, string[] ids)
    {
        into.Clear();
        for (var index = 0; index < ids.Length; index++)
        {
            if (ids[index].Length > 0)
            {
                into.Add(ids[index]);
            }
        }
    }

    private bool HoldsSet(HashSet<string> set, string id)
    {
        if (id.Length == 0)
        {
            return false;
        }

        lock (gate)
        {
            return set.Contains(id);
        }
    }

    private void SetSet(HashSet<string> set, ref string[] store, string id, bool on)
    {
        if (id.Length == 0)
        {
            return;
        }

        lock (gate)
        {
            if (on)
            {
                if (!set.Add(id))
                {
                    return;
                }
            }
            else if (!set.Remove(id))
            {
                return;
            }

            store = [.. set];
            revision++;
        }

        Persist();
    }

    private static int IndexOf(IReadOnlyList<string> ids, string id)
    {
        for (var index = 0; index < ids.Count; index++)
        {
            if (string.Equals(ids[index], id, StringComparison.Ordinal))
            {
                return index;
            }
        }

        return -1;
    }

    private sealed class BookWire
    {
        public string[]? Saved { get; set; }

        public string[]? Notify { get; set; }

        public string[]? Schedule { get; set; }
    }
}
