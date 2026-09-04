namespace Linkpearl.Audio;

internal static class RadioPlaylist
{
    public static IReadOnlyList<string> Unwrap(string url, HttpClient http)
    {
        var seed = url.Trim();
        if (seed.Length == 0)
        {
            return [];
        }

        if (!LooksLikePlaylist(seed))
        {
            return [seed];
        }

        try
        {
            using var response = HttpWire.Get(http, seed, TimeSpan.FromSeconds(8));
            if (!response.IsSuccessStatusCode)
            {
                return [seed];
            }

            var type = response.Content.Headers.ContentType?.MediaType ?? string.Empty;
            if (!LooksLikePlaylist(seed) && !IsPlaylistType(type))
            {
                return [seed];
            }

            using var reader = new StreamReader(HttpWire.Body(response));
            var text = reader.ReadToEnd();
            if (text.Length > 64_000)
            {
                text = text[..64_000];
            }

            var found = Parse(text);
            return found.Count > 0 ? found : [seed];
        }
        catch (Exception)
        {
            return [seed];
        }
    }

    public static bool LooksLikePlaylist(string url)
    {
        var path = url;
        if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            path = uri.AbsolutePath;
        }

        return path.EndsWith(".m3u", StringComparison.OrdinalIgnoreCase) ||
               path.EndsWith(".m3u8", StringComparison.OrdinalIgnoreCase) ||
               path.EndsWith(".pls", StringComparison.OrdinalIgnoreCase) ||
               path.EndsWith(".asx", StringComparison.OrdinalIgnoreCase) ||
               path.EndsWith(".xspf", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsPlaylistType(string mediaType) =>
        mediaType.Contains("mpegurl", StringComparison.OrdinalIgnoreCase) ||
        mediaType.Contains("scpls", StringComparison.OrdinalIgnoreCase) ||
        mediaType.Contains("xspf", StringComparison.OrdinalIgnoreCase);

    private static List<string> Parse(string text)
    {
        var found = new List<string>();
        using var reader = new StringReader(text);
        while (reader.ReadLine() is { } line)
        {
            var trimmed = line.Trim();
            if (trimmed.Length == 0 || trimmed.StartsWith('#') || trimmed.StartsWith('<'))
            {
                continue;
            }

            if (trimmed.StartsWith("File", StringComparison.OrdinalIgnoreCase))
            {
                var split = trimmed.IndexOf('=');
                if (split > 0)
                {
                    trimmed = trimmed[(split + 1)..].Trim();
                }
            }

            if (trimmed.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                trimmed.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                found.Add(trimmed);
            }
        }

        return found;
    }
}
