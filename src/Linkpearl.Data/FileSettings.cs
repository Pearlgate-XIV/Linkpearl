using System.Text.Json;
using Linkpearl.Modules;
using Linkpearl.Persistence;

namespace Linkpearl.Data;

public static class FileSettings
{
    public static FileSettings<TSection> Load<TSection>(HostPaths paths, params ISettingsMigration[] migrations)
        where TSection : class, ISettingsSection, new() =>
        FileSettings<TSection>.Open(paths, migrations);
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
    private TSection value;
    private bool dirty;

    private FileSettings(string path, IReadOnlyList<ISettingsMigration> migrations)
    {
        this.path = path;
        this.migrations = migrations;
        value = new TSection();
    }

    public TSection Value => value;

    public event Action<TSection>? Changed;

    internal static FileSettings<TSection> Open(HostPaths paths, IReadOnlyList<ISettingsMigration> migrations)
    {
        var section = new TSection();
        var settings = new FileSettings<TSection>(paths.State(section.SectionId + ".json"), migrations);
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
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");
            value.SchemaVersion = new TSection().SchemaVersion;
            File.WriteAllText(path, JsonSerializer.Serialize(value, Json));
            dirty = false;
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
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
            value = loaded ?? new TSection();
            dirty = false;
        }
        catch (JsonException)
        {
            value = new TSection();
            dirty = false;
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
