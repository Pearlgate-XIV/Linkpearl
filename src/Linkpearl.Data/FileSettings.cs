using System.Text.Json;
using Linkpearl.Diagnostics;
using Linkpearl.Modules;
using Linkpearl.Persistence;

namespace Linkpearl.Data;

public static class AtomicJson
{
    public static bool TryRead<T>(string path, JsonSerializerOptions? options, out T? document,
        ref JsonCorruptHold corrupt, ILinkpearlLog? log)
        where T : class =>
        Persistence.AtomicJson.TryRead(path, options, out document, ref corrupt, log);

    public static bool TrySave<T>(string path, T document, JsonSerializerOptions? options,
        ref JsonCorruptHold corrupt, ILinkpearlLog? log, CancellationToken cancellation = default) =>
        Persistence.AtomicJson.TrySave(path, document, options, ref corrupt, log, cancellation);

    public static bool TryWrite<T>(string path, T document, JsonSerializerOptions? options = null,
        ILinkpearlLog? log = null, CancellationToken cancellation = default) =>
        Persistence.AtomicJson.TryWrite(path, document, options, log, cancellation);

    public static bool TryWriteText(string path, string text, ILinkpearlLog? log = null,
        CancellationToken cancellation = default) =>
        Persistence.AtomicJson.TryWriteText(path, text, log, cancellation);

    public static bool ParkCorrupt(string path, ILinkpearlLog? log = null) =>
        Persistence.AtomicJson.ParkCorrupt(path, log);
}

public static class FileSettings
{
    public static FileSettings<TSection> Load<TSection>(HostPaths paths, params ISettingsMigration[] migrations)
        where TSection : class, ISettingsSection, new() =>
        FileSettings<TSection>.Open(paths, null, migrations);

    public static FileSettings<TSection> Load<TSection>(HostPaths paths, ILinkpearlLog? log,
        params ISettingsMigration[] migrations)
        where TSection : class, ISettingsSection, new() =>
        FileSettings<TSection>.Open(paths, log, migrations);
}

public sealed class FileSettings<TSection> : ISettings<TSection>, IDisposable
    where TSection : class, ISettingsSection, new()
{
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
    };

    private readonly string path;
    private readonly IReadOnlyList<ISettingsMigration> migrations;
    private readonly ILinkpearlLog? log;
    private TSection value;
    private bool dirty;
    private JsonCorruptHold corrupt;

    private FileSettings(string path, IReadOnlyList<ISettingsMigration> migrations, ILinkpearlLog? log)
    {
        this.path = path;
        this.migrations = migrations;
        this.log = log;
        value = new TSection();
    }

    public TSection Value => value;

    public event Action<TSection>? Changed;

    internal static FileSettings<TSection> Open(HostPaths paths, ILinkpearlLog? log,
        IReadOnlyList<ISettingsMigration> migrations)
    {
        var section = new TSection();
        var settings = new FileSettings<TSection>(paths.State(section.SectionId + ".json"), migrations, log);
        settings.Reload();
        return settings;
    }

    public void Mutate(Action<TSection> change)
    {
        change(value);
        dirty = true;
        Save();
        Changed?.Invoke(value);
    }

    public void Save()
    {
        value.SchemaVersion = new TSection().SchemaVersion;
        if (AtomicJson.TrySave(path, value, Json, ref corrupt, log))
        {
            dirty = false;
        }
    }

    public void Reload()
    {
        if (!File.Exists(path))
        {
            value = new TSection();
            dirty = false;
            return;
        }

        try
        {
            var json = File.ReadAllText(path);
            var document = JsonSettingsDocument.Parse(json);
            var version = 0;
            if (document.TryRead("SchemaVersion", out int schema))
            {
                version = schema;
            }
            else if (document.TryRead("schemaVersion", out int lower))
            {
                version = lower;
            }

            var current = new TSection().SchemaVersion;
            while (version < current)
            {
                var step = FindMigration(version);
                if (step is null)
                {
                    break;
                }

                step.Apply(document);
                version = step.ToVersion;
                document.Write("SchemaVersion", version);
            }

            var loaded = JsonSerializer.Deserialize<TSection>(document.ToJson(Json), Json);
            if (loaded is null)
            {
                value = new TSection();
                dirty = false;
                corrupt.Mark();
                log?.Write(LogSeverity.Warning, Path.GetFileName(path) + " JsonException");
                return;
            }

            value = loaded;
            dirty = false;
        }
        catch (JsonException failure)
        {
            log?.Write(LogSeverity.Warning, Path.GetFileName(path) + " " + failure.GetType().Name);
            value = new TSection();
            dirty = false;
            corrupt.Mark();
        }
        catch (IOException)
        {
            value = new TSection();
            dirty = false;
        }
    }

    public void Dispose()
    {
        if (dirty)
        {
            Save();
        }
    }

    private ISettingsMigration? FindMigration(int fromVersion)
    {
        var sectionId = new TSection().SectionId;
        for (var index = 0; index < migrations.Count; index++)
        {
            var migration = migrations[index];
            if (migration.FromVersion == fromVersion &&
                string.Equals(migration.SectionId, sectionId, StringComparison.Ordinal))
            {
                return migration;
            }
        }

        return null;
    }
}
