namespace Linkpearl.Persistence;

public interface ISettingsSection
{
    string SectionId { get; }

    int SchemaVersion { get; set; }
}

public interface ISettings<TSection> where TSection : class, ISettingsSection, new()
{
    TSection Value { get; }

    event Action<TSection>? Changed;

    void Mutate(Action<TSection> change);

    void Save();

    void Reload();
}

public interface ISettingsMigration
{
    string SectionId { get; }

    int FromVersion { get; }

    int ToVersion { get; }

    void Apply(ISettingsDocument document);
}

public interface ISettingsDocument
{
    bool TryRead<TValue>(string key, out TValue value);

    void Write<TValue>(string key, TValue value);

    void Remove(string key);

    bool Contains(string key);

    void Rename(string fromKey, string toKey);
}
