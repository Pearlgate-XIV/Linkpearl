namespace Linkpearl.Audio;

internal static class IcecastListen
{
    public static IEnumerable<string> Candidates(string streamUrl)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var seed in Split(streamUrl))
        {
            foreach (var url in Expand(seed))
            {
                if (url.Length > 0 && seen.Add(url))
                {
                    yield return url;
                }
            }
        }
    }

    private static IEnumerable<string> Split(string streamUrl)
    {
        var raw = streamUrl.Trim();
        if (raw.Length == 0)
        {
            yield break;
        }

        foreach (var part in raw.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            yield return part;
        }
    }

    private static IEnumerable<string> Expand(string streamUrl)
    {
        var raw = streamUrl.Trim();
        if (raw.Length == 0)
        {
            yield break;
        }

        yield return raw;
        if (!Uri.TryCreate(raw.Contains("://", StringComparison.Ordinal) ? raw : "http://" + raw, UriKind.Absolute,
                out var uri))
        {
            yield break;
        }

        var builder = new UriBuilder(uri)
        {
            UserName = string.Empty,
            Password = string.Empty,
            Query = string.Empty,
        };
        yield return builder.Uri.ToString();

        if (builder.Path.Length <= 1)
        {
            builder.Path = "/;stream.mp3";
            yield return builder.Uri.ToString();
        }
    }
}
