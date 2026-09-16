using Linkpearl.Modules;
using Linkpearl.Platform;
using Linkpearl.Time;
using System.Text.Json;

namespace Linkpearl.Badges;

public sealed class BadgeBook
{
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

    private readonly string path;
    private readonly IClock clock;
    private readonly List<BadgeOwn> owned = new();
    private readonly string[] slots = new string[BadgeCatalog.SlotCount];
    private string featuredId = string.Empty;
    private string portraitFile = string.Empty;
    private float portraitZoom = 1f;
    private float portraitFocusX = 0.5f;
    private float portraitFocusY = 0.5f;
    private string featuredFile = string.Empty;
    private readonly string[] slotFiles = new string[BadgeCatalog.SlotCount];

    private BadgeBook(string path, IClock clock)
    {
        this.path = path;
        this.clock = clock;
    }

    public IReadOnlyList<BadgeOwn> Owned => owned;

    public IReadOnlyList<string> Slots => slots;

    public string FeaturedId => featuredId;

    public string PortraitFile => portraitFile;

    public float PortraitZoom => portraitZoom;

    public Vector2 PortraitFocus => new(portraitFocusX, portraitFocusY);

    public static string FeaturedFile => string.Empty;

    public IReadOnlyList<string> SlotFiles => slotFiles;

    public static BadgeBook Load(HostPaths paths, IClock clock)
    {
        var path = paths.State("badges.json");
        var book = new BadgeBook(path, clock);
        Directory.CreateDirectory(Path.GetDirectoryName(path) ?? paths.StateDirectory);
        if (!File.Exists(path))
        {
            return book;
        }

        try
        {
            var save = JsonSerializer.Deserialize<Save>(File.ReadAllText(path));
            if (save is null)
            {
                return book;
            }

            book.portraitFile = Path.GetFileName(save.PortraitFile ?? string.Empty);
            book.portraitZoom = save.PortraitZoom > 0.99f ? save.PortraitZoom : 1f;
            book.portraitFocusX = save.PortraitFocusX ?? 0.5f;
            book.portraitFocusY = save.PortraitFocusY ?? 0.5f;
            book.featuredId = save.FeaturedId ?? string.Empty;
            var wipeCustom = !string.IsNullOrWhiteSpace(save.FeaturedFile);
            book.featuredFile = string.Empty;
            var incoming = save.Slots ?? [];
            for (var index = 0; index < book.slots.Length; index++)
            {
                book.slots[index] = index < incoming.Length ? incoming[index] ?? string.Empty : string.Empty;
            }

            var incomingFiles = save.SlotFiles ?? [];
            for (var index = 0; index < book.slotFiles.Length; index++)
            {
                if (index < incomingFiles.Length && !string.IsNullOrWhiteSpace(incomingFiles[index]))
                {
                    wipeCustom = true;
                }

                book.slotFiles[index] = string.Empty;
            }

            if (save.Owned is not null)
            {
                for (var index = 0; index < save.Owned.Length; index++)
                {
                    var row = save.Owned[index];
                    if (row is null || string.IsNullOrWhiteSpace(row.Id) || BadgeCatalog.Find(row.Id) is null)
                    {
                        continue;
                    }

                    book.owned.Add(new BadgeOwn
                    {
                        Id = row.Id,
                        EarnedAtUnix = row.EarnedAtUnix,
                        Source = row.Source ?? string.Empty,
                    });
                }
            }

            if (wipeCustom)
            {
                book.Persist();
            }
        }
        catch (JsonException)
        {
        }
        catch (IOException)
        {
        }

        return book;
    }

    public void Sync(bool founderWindow, string jobName, bool development, bool patron = false)
    {
        var first = owned.Count == 0;
        var dirty = first;
        dirty |= Grant("linkpearl", "Issued with the communicator.");
        dirty |= Grant("night-watch", "Personality.");
        dirty |= Grant("moth", "Community.");
        dirty |= Grant("still", "Participation.");
        dirty |= Grant("chorus", "Music.");
        dirty |= Grant("muster", "Events.");
        dirty |= Grant("circle", "Social.");
        if (development)
        {
            dirty |= Grant("forge", "Development.");
            dirty |= Grant("founder", "Local development grant.");
        }

        if (founderWindow)
        {
            dirty |= Grant("founder", "First 500 Pearlgate accounts.");
        }

        if (patron)
        {
            dirty |= Grant("patron", "Patreon.");
        }

        if (jobName.Contains("reaper", StringComparison.OrdinalIgnoreCase))
        {
            dirty |= Grant("reaper", "Job record.");
        }

        if (first)
        {
            dirty |= Grant("relic", "Founding window.", ignoreWindow: true);
        }

        if (!dirty)
        {
            return;
        }

        if (first)
        {
            EquipDefaults();
        }
        else if (featuredId.Length == 0)
        {
            EquipDefaults();
        }

        Sanitize();
        Persist();
    }

    public bool Owns(string id)
    {
        for (var index = 0; index < owned.Count; index++)
        {
            if (string.Equals(owned[index].Id, id, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    public BadgeOwn? Ownership(string id)
    {
        for (var index = 0; index < owned.Count; index++)
        {
            if (string.Equals(owned[index].Id, id, StringComparison.Ordinal))
            {
                return owned[index];
            }
        }

        return null;
    }

    public void SetPortrait(string fileName)
    {
        portraitFile = Path.GetFileName(fileName);
        portraitZoom = 1f;
        portraitFocusX = 0.5f;
        portraitFocusY = 0.5f;
        Persist();
    }

    public void AdjustPortrait(float zoom, Vector2 focus)
    {
        portraitZoom = Math.Clamp(zoom, 1f, 4.5f);
        portraitFocusX = Math.Clamp(focus.X, 0f, 1f);
        portraitFocusY = Math.Clamp(focus.Y, 0f, 1f);
    }

    public void CommitPortrait() => Persist();

    public void SetFeatured(string id)
    {
        if (id.Length > 0 && !Owns(id))
        {
            return;
        }

        featuredId = id;
        featuredFile = string.Empty;
        Persist();
    }

    public void Equip(int slot, string id)
    {
        if (slot < 0 || slot >= slots.Length)
        {
            return;
        }

        if (id.Length > 0 && !Owns(id))
        {
            return;
        }

        for (var index = 0; index < slots.Length; index++)
        {
            if (index != slot && id.Length > 0 && string.Equals(slots[index], id, StringComparison.Ordinal))
            {
                slots[index] = string.Empty;
            }
        }

        slots[slot] = id;
        slotFiles[slot] = string.Empty;
        Persist();
    }

    public void ClearSlot(int slot) => Equip(slot, string.Empty);

    public bool Offer(string id, string source, bool ignoreWindow = false)
    {
        if (!Grant(id, source, ignoreWindow))
        {
            return false;
        }

        Persist();
        return true;
    }

    public bool TryPurchase(string id)
    {
        if (Owns(id) || BadgeCatalog.Find(id) is not { } spec)
        {
            return false;
        }

        if (!spec.Purchasable || spec.Kind != BadgeKind.Cosmetic)
        {
            return false;
        }

        owned.Add(new BadgeOwn
        {
            Id = id,
            EarnedAtUnix = clock.UtcNow.ToUnixTimeSeconds(),
            Source = "Purchased with Pearls.",
        });
        Persist();
        return true;
    }

    private bool Grant(string id, string source, bool ignoreWindow = false)
    {
        if (Owns(id) || BadgeCatalog.Find(id) is not { } spec)
        {
            return false;
        }

        if (!ignoreWindow && !spec.ObtainableAt(clock.UtcNow))
        {
            return false;
        }

        owned.Add(new BadgeOwn
        {
            Id = id,
            EarnedAtUnix = clock.UtcNow.ToUnixTimeSeconds(),
            Source = source,
        });
        return true;
    }

    private void EquipDefaults()
    {
        var seed = new[] { "night-watch", "moth", "reaper", "still", "forge" };
        for (var index = 0; index < slots.Length; index++)
        {
            var id = index < seed.Length ? seed[index] : string.Empty;
            slots[index] = Owns(id) ? id : string.Empty;
        }

        if (featuredId.Length == 0)
        {
            featuredId = Owns("founder") ? "founder" : Owns("linkpearl") ? "linkpearl" : string.Empty;
        }
    }

    private void Sanitize()
    {
        featuredFile = string.Empty;
        if (featuredId.Length > 0 && !Owns(featuredId))
        {
            featuredId = string.Empty;
        }

        for (var index = 0; index < slots.Length; index++)
        {
            slotFiles[index] = string.Empty;
            if (slots[index].Length > 0 && !Owns(slots[index]))
            {
                slots[index] = string.Empty;
            }
        }
    }

    private void Persist()
    {
        var save = new Save
        {
            PortraitFile = portraitFile,
            PortraitZoom = portraitZoom,
            PortraitFocusX = portraitFocusX,
            PortraitFocusY = portraitFocusY,
            FeaturedId = featuredId,
            FeaturedFile = featuredFile,
            Slots = (string[])slots.Clone(),
            SlotFiles = (string[])slotFiles.Clone(),
            Owned = owned.Select(row => new OwnSave
            {
                Id = row.Id,
                EarnedAtUnix = row.EarnedAtUnix,
                Source = row.Source,
            }).ToArray(),
        };

        try
        {
            File.WriteAllText(path, JsonSerializer.Serialize(save, Json));
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private sealed class Save
    {
        public string? PortraitFile { get; set; }

        public float PortraitZoom { get; set; } = 1f;

        public float? PortraitFocusX { get; set; }

        public float? PortraitFocusY { get; set; }

        public string? FeaturedId { get; set; }

        public string? FeaturedFile { get; set; }

        public string[]? Slots { get; set; }

        public string[]? SlotFiles { get; set; }

        public OwnSave[]? Owned { get; set; }
    }

    private sealed class OwnSave
    {
        public string? Id { get; set; }

        public long EarnedAtUnix { get; set; }

        public string? Source { get; set; }
    }
}
