using Linkpearl.Modules;

namespace Linkpearl.Chat;

public readonly record struct GifHit(string Id, string Title, string PreviewUrl, string SendUrl);

public interface IGifDesk
{
    string Brand { get; }

    string Notice { get; }

    bool Busy { get; }

    IReadOnlyList<GifHit> Hits { get; }

    void Warm();

    void NoteQuery(string query);

    void Ensure(string url);

    string PathFor(string url);

    bool CanMore { get; }

    void More();
}

public static class GifCache
{
    public static string PathFor(HostPaths paths, string url)
    {
        var bytes = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(url));
        var name = Convert.ToHexString(bytes.AsSpan(0, 8)).ToLowerInvariant();
        return paths.Cache(Path.Combine("gifs", name + ".gif"));
    }

    public static bool IsUrl(string body) =>
        body.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
        body.StartsWith("http://", StringComparison.OrdinalIgnoreCase);
}
