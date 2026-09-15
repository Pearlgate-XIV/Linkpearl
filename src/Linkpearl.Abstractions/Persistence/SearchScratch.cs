namespace Linkpearl.Persistence;

public sealed class SearchScratch : ISettingsSection
{
    public string SectionId => "search";

    public int SchemaVersion { get; set; } = 1;

    public string Universal { get; set; } = string.Empty;

    public string Studio { get; set; } = string.Empty;
}
