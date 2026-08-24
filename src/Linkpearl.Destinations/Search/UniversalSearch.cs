using System.Linq;

namespace Linkpearl.Destinations;

public readonly record struct SearchResult(string Kind, string Title, string Subtitle);

// The public face of DemoData's search index — everything behind it is placeholder content
// (see DemoData.cs), but this is the real, stable surface other projects (the shell's search
// overlay) call into, so swapping in a live backend later only touches this one file.
public static class UniversalSearch
{
    public static IReadOnlyList<SearchResult> RecentSearches { get; } = DemoData.RecentSearches
        .Select(entry => new SearchResult(entry.Kind, entry.Title, entry.Subtitle)).ToList();

    public static IReadOnlyList<SearchResult> Search(string query) => DemoData.Search(query)
        .Select(entry => new SearchResult(entry.Kind, entry.Title, entry.Subtitle)).ToList();
}
