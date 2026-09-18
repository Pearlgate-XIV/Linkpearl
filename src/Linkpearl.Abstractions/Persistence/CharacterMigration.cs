using System.Globalization;
using System.Text.Json;
using Linkpearl.Diagnostics;
using Linkpearl.Modules;
using Linkpearl.Time;

namespace Linkpearl.Persistence;

public enum CharacterStoreMark : byte
{
    Unclaimed = 0,
    Copied = 1,
    Exists = 2,
    Missing = 3,
    Malformed = 4,
    Failed = 5,
}

public sealed class CharacterMigration
{
    public const int Schema = 1;

    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
    };

    private readonly HostPaths paths;
    private readonly IClock clock;
    private readonly ILinkpearlLog log;
    private readonly string manifestPath;
    private JsonCorruptHold corrupt;
    private readonly Record file;
    private bool snooze;

    public CharacterMigration(HostPaths paths, IClock clock, ILinkpearlLog log)
    {
        this.paths = paths;
        this.clock = clock;
        this.log = log;
        manifestPath = CharacterStatePaths.Manifest(paths);
        file = LoadRecord();
    }

    public event Action? Changed;

    public bool Refused => file.Refused;

    public bool Deferred => file.Deferred;

    public bool Complete => file.Complete;

    public string Claimant => file.Claimant ?? string.Empty;

    public string CalendarMark => file.Calendar ?? "unclaimed";

    public string PearlsMark => file.Pearls ?? "unclaimed";

    public string HandsetLineMark => file.HandsetLine ?? "unclaimed";

    public bool HasClaimant => Claimant.Length == 16;

    public bool NeedsPrompt(ulong contentId) =>
        CharacterStatePaths.TryHex(contentId, out _) &&
        !HasClaimant &&
        !file.Refused &&
        !snooze;

    public bool SettingsOpen => !HasClaimant;

    public void Later(ulong contentId)
    {
        if (!CharacterStatePaths.TryHex(contentId, out _))
        {
            return;
        }

        snooze = true;
        file.Deferred = true;
        file.AtUnix = clock.UtcNow.ToUnixTimeSeconds();
        Persist();
        Changed?.Invoke();
    }

    public void Never()
    {
        snooze = true;
        file.Refused = true;
        file.Deferred = false;
        file.Claimant = string.Empty;
        file.Calendar = "unclaimed";
        file.Pearls = "unclaimed";
        file.HandsetLine = "unclaimed";
        file.Complete = true;
        file.AtUnix = clock.UtcNow.ToUnixTimeSeconds();
        Persist();
        Changed?.Invoke();
    }

    public void Claim(ulong contentId)
    {
        if (!CharacterStatePaths.TryHex(contentId, out var hex))
        {
            return;
        }

        if (HasClaimant)
        {
            Resume(contentId);
            return;
        }

        snooze = false;
        file.Refused = false;
        file.Deferred = false;
        CopyAll(hex);
    }

    public void Resume(ulong contentId)
    {
        if (!HasClaimant || !CharacterStatePaths.TryHex(contentId, out var hex) ||
            !string.Equals(file.Claimant, hex, StringComparison.Ordinal) || file.Complete)
        {
            return;
        }

        CopyAll(hex);
    }

    private void CopyAll(string hex)
    {
        if (!ulong.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var contentId))
        {
            return;
        }

        file.Claimant = hex;
        file.AtUnix = clock.UtcNow.ToUnixTimeSeconds();
        var failures = new List<string>();
        file.Calendar = CopyStore(CharacterStatePaths.CalendarName,
            CharacterStatePaths.Legacy(paths, CharacterStatePaths.CalendarName),
            CharacterStatePaths.Calendar(paths, contentId), failures);
        file.Pearls = CopyStore(CharacterStatePaths.PearlsName,
            CharacterStatePaths.Legacy(paths, CharacterStatePaths.PearlsName),
            CharacterStatePaths.Pearls(paths, contentId), failures);
        file.HandsetLine = CopyStore(CharacterStatePaths.HandsetLineName,
            CharacterStatePaths.Legacy(paths, CharacterStatePaths.HandsetLineName),
            CharacterStatePaths.HandsetLine(paths, contentId), failures);
        file.Failures = failures.Count == 0 ? null : failures.ToArray();
        file.Complete = failures.Count == 0;
        Persist();
        Changed?.Invoke();
    }

    public void Wake() => snooze = false;

    private string CopyStore(string name, string source, string dest, List<string> failures)
    {
        if (dest.Length == 0)
        {
            failures.Add(name);
            Note(name, "ArgumentException");
            return Mark(CharacterStoreMark.Failed);
        }

        if (File.Exists(dest))
        {
            return Mark(CharacterStoreMark.Exists);
        }

        if (!File.Exists(source))
        {
            return Mark(CharacterStoreMark.Missing);
        }

        JsonElement document;
        try
        {
            document = JsonSerializer.Deserialize<JsonElement>(File.ReadAllText(source), Json);
            if (document.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
            {
                Note(name, "JsonException");
                return Mark(CharacterStoreMark.Malformed);
            }
        }
        catch (JsonException)
        {
            Note(name, "JsonException");
            return Mark(CharacterStoreMark.Malformed);
        }
        catch (IOException failure)
        {
            Note(name, failure.GetType().Name);
            failures.Add(name);
            return Mark(CharacterStoreMark.Failed);
        }

        if (!AtomicJson.TryWrite(dest, document, Json, log))
        {
            failures.Add(name);
            return Mark(CharacterStoreMark.Failed);
        }

        try
        {
            using var check = JsonDocument.Parse(File.ReadAllText(dest));
            if (check.RootElement.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
            {
                failures.Add(name);
                return Mark(CharacterStoreMark.Failed);
            }
        }
        catch (JsonException)
        {
            failures.Add(name);
            return Mark(CharacterStoreMark.Failed);
        }
        catch (IOException)
        {
            failures.Add(name);
            return Mark(CharacterStoreMark.Failed);
        }

        return Mark(CharacterStoreMark.Copied);
    }

    private Record LoadRecord()
    {
        if (!AtomicJson.TryRead(manifestPath, Json, out Record? loaded, ref corrupt, log) || loaded is null)
        {
            return new Record { Schema = Schema };
        }

        loaded.Schema = Schema;
        loaded.Claimant = NormalizeHex(loaded.Claimant);
        return loaded;
    }

    private void Persist()
    {
        file.Schema = Schema;
        AtomicJson.TrySave(manifestPath, file, Json, ref corrupt, log);
    }

    private void Note(string name, string category) =>
        log.Write(LogSeverity.Warning, name + " " + category);

    private static string NormalizeHex(string? value)
    {
        var hex = (value ?? string.Empty).Trim().ToLowerInvariant();
        return hex.Length == 16 ? hex : string.Empty;
    }

    private static string Mark(CharacterStoreMark mark) => mark switch
    {
        CharacterStoreMark.Copied => "copied",
        CharacterStoreMark.Exists => "exists",
        CharacterStoreMark.Missing => "missing",
        CharacterStoreMark.Malformed => "malformed",
        CharacterStoreMark.Failed => "failed",
        _ => "unclaimed",
    };

    private sealed class Record
    {
        public int Schema { get; set; } = CharacterMigration.Schema;

        public string? Claimant { get; set; }

        public bool Refused { get; set; }

        public bool Deferred { get; set; }

        public string? Calendar { get; set; }

        public string? Pearls { get; set; }

        public string? HandsetLine { get; set; }

        public string? Backup { get; set; }

        public long AtUnix { get; set; }

        public string[]? Failures { get; set; }

        public bool Complete { get; set; }
    }
}
