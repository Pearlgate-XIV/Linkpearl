using System.Text.Json;
using Linkpearl.Diagnostics;
using Linkpearl.Modules;
using Linkpearl.Persistence;

namespace Linkpearl.Destinations;

public enum NoticeDismissalScope : byte
{
    Device = 0,
    Account = 1,
    Character = 2,
}

public sealed class NoticeDismissalBook
{
    public const int SchemaVersion = 1;
    public const int CapPerOwner = 200;
    public const string FileName = "notices.json";

    private static readonly JsonSerializerOptions Json = new() { WriteIndented = false };

    private readonly string path;
    private readonly ILinkpearlLog log;
    private readonly List<Row> rows = [];
    private JsonCorruptHold corrupt;
    private bool loaded;

    public NoticeDismissalBook(HostPaths paths, ILinkpearlLog log)
    {
        this.log = log;
        path = paths.State(FileName);
        Load();
    }

    public string Path => path;

    public int Count => rows.Count;

    public bool Holds(string fingerprint, NoticeDismissalScope scope, string ownerKey)
    {
        if (fingerprint.Length == 0)
        {
            return false;
        }

        var owner = NormalizeOwner(scope, ownerKey);
        for (var index = 0; index < rows.Count; index++)
        {
            var row = rows[index];
            if (row.Scope == scope &&
                string.Equals(row.Fingerprint, fingerprint, StringComparison.Ordinal) &&
                string.Equals(row.OwnerKey, owner, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    public bool Remember(string fingerprint, NoticeDismissalScope scope, string ownerKey, long dismissedAtUnix)
    {
        if (!TryOwner(scope, ownerKey, out var owner) || !ValidFingerprint(fingerprint))
        {
            return false;
        }

        for (var index = 0; index < rows.Count; index++)
        {
            var row = rows[index];
            if (row.Scope == scope &&
                string.Equals(row.Fingerprint, fingerprint, StringComparison.Ordinal) &&
                string.Equals(row.OwnerKey, owner, StringComparison.Ordinal))
            {
                return true;
            }
        }

        rows.Add(new Row
        {
            Fingerprint = fingerprint,
            Scope = scope,
            OwnerKey = owner,
            DismissedAt = dismissedAtUnix,
        });
        Prune(scope, owner);
        return Save();
    }

    public IReadOnlyList<(string Fingerprint, NoticeDismissalScope Scope, string OwnerKey, long DismissedAt)> Snapshot()
    {
        var copy = new (string, NoticeDismissalScope, string, long)[rows.Count];
        for (var index = 0; index < rows.Count; index++)
        {
            var row = rows[index];
            copy[index] = (row.Fingerprint, row.Scope, row.OwnerKey, row.DismissedAt);
        }

        return copy;
    }

    public static bool TryOwner(NoticeDismissalScope scope, string ownerKey, out string owner)
    {
        owner = NormalizeOwner(scope, ownerKey);
        if (scope == NoticeDismissalScope.Device)
        {
            return owner.Length == 0;
        }

        if (scope == NoticeDismissalScope.Character)
        {
            return owner.Length == 16 && IsHex(owner);
        }

        return owner.Length > 0 && owner.Length <= 64 && owner.IndexOf(' ') < 0 && owner.IndexOf('.') < 0;
    }

    public static string NormalizeOwner(NoticeDismissalScope scope, string ownerKey)
    {
        if (scope == NoticeDismissalScope.Device)
        {
            return string.Empty;
        }

        return (ownerKey ?? string.Empty).Trim();
    }

    public static bool ValidFingerprint(string fingerprint)
    {
        if (fingerprint.Length == 0 || fingerprint.Length > 160)
        {
            return false;
        }

        for (var index = 0; index < fingerprint.Length; index++)
        {
            var ch = fingerprint[index];
            if (ch == ':' || ch == '-' || ch == '_' || ch is >= '0' and <= '9' ||
                ch is >= 'A' and <= 'Z' || ch is >= 'a' and <= 'z')
            {
                continue;
            }

            return false;
        }

        return true;
    }

    private void Load()
    {
        loaded = true;
        rows.Clear();
        if (path.Length == 0 || !File.Exists(path))
        {
            return;
        }

        if (!AtomicJson.TryRead(path, Json, out FileDto? dto, ref corrupt, log) || dto is null)
        {
            AtomicJson.ParkCorrupt(path, log);
            corrupt.Clear();
            rows.Clear();
            return;
        }

        if (dto.SchemaVersion != SchemaVersion)
        {
            AtomicJson.ParkCorrupt(path, log);
            corrupt.Clear();
            rows.Clear();
            return;
        }

        var list = dto.Rows;
        if (list is null)
        {
            return;
        }

        for (var index = 0; index < list.Length; index++)
        {
            var item = list[index];
            if (item is null || !TryParseScope(item.Scope, out var scope) ||
                !TryOwner(scope, item.OwnerKey ?? string.Empty, out var owner) ||
                !ValidFingerprint(item.Fingerprint ?? string.Empty))
            {
                continue;
            }

            rows.Add(new Row
            {
                Fingerprint = item.Fingerprint ?? string.Empty,
                Scope = scope,
                OwnerKey = owner,
                DismissedAt = item.DismissedAt,
            });
        }
    }

    private void Prune(NoticeDismissalScope scope, string owner)
    {
        var count = 0;
        for (var index = 0; index < rows.Count; index++)
        {
            if (rows[index].Scope == scope &&
                string.Equals(rows[index].OwnerKey, owner, StringComparison.Ordinal))
            {
                count++;
            }
        }

        while (count > CapPerOwner)
        {
            var oldest = -1;
            var stamp = long.MaxValue;
            for (var index = 0; index < rows.Count; index++)
            {
                var row = rows[index];
                if (row.Scope != scope ||
                    !string.Equals(row.OwnerKey, owner, StringComparison.Ordinal))
                {
                    continue;
                }

                if (row.DismissedAt < stamp)
                {
                    stamp = row.DismissedAt;
                    oldest = index;
                }
            }

            if (oldest < 0)
            {
                return;
            }

            rows.RemoveAt(oldest);
            count--;
        }
    }

    private bool Save()
    {
        if (!loaded || path.Length == 0)
        {
            return false;
        }

        var dto = new FileDto
        {
            SchemaVersion = SchemaVersion,
            Rows = new RowDto[rows.Count],
        };
        for (var index = 0; index < rows.Count; index++)
        {
            var row = rows[index];
            dto.Rows[index] = new RowDto
            {
                Fingerprint = row.Fingerprint,
                Scope = ScopeName(row.Scope),
                OwnerKey = row.OwnerKey,
                DismissedAt = row.DismissedAt,
            };
        }

        return AtomicJson.TrySave(path, dto, Json, ref corrupt, log);
    }

    private static bool TryParseScope(string? text, out NoticeDismissalScope scope)
    {
        if (string.Equals(text, "Device", StringComparison.OrdinalIgnoreCase))
        {
            scope = NoticeDismissalScope.Device;
            return true;
        }

        if (string.Equals(text, "Account", StringComparison.OrdinalIgnoreCase))
        {
            scope = NoticeDismissalScope.Account;
            return true;
        }

        if (string.Equals(text, "Character", StringComparison.OrdinalIgnoreCase))
        {
            scope = NoticeDismissalScope.Character;
            return true;
        }

        scope = default;
        return false;
    }

    private static string ScopeName(NoticeDismissalScope scope) => scope switch
    {
        NoticeDismissalScope.Account => "Account",
        NoticeDismissalScope.Character => "Character",
        _ => "Device",
    };

    private static bool IsHex(string value)
    {
        for (var index = 0; index < value.Length; index++)
        {
            var ch = value[index];
            if (ch is >= '0' and <= '9' || ch is >= 'a' and <= 'f')
            {
                continue;
            }

            return false;
        }

        return true;
    }

    private sealed class Row
    {
        public string Fingerprint { get; set; } = string.Empty;

        public NoticeDismissalScope Scope { get; set; }

        public string OwnerKey { get; set; } = string.Empty;

        public long DismissedAt { get; set; }
    }

    private sealed class FileDto
    {
        public int SchemaVersion { get; set; }

        public RowDto[]? Rows { get; set; }
    }

    private sealed class RowDto
    {
        public string? Fingerprint { get; set; }

        public string? Scope { get; set; }

        public string? OwnerKey { get; set; }

        public long DismissedAt { get; set; }
    }
}
