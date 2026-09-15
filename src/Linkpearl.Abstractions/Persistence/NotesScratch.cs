namespace Linkpearl.Persistence;

public sealed class NotesScratch : ISettingsSection
{
    public string SectionId => "notes";

    public int SchemaVersion { get; set; } = 1;

    public string[] Lines { get; set; } = [];
}
