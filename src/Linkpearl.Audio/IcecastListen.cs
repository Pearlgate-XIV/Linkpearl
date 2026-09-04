namespace Linkpearl.Audio;

internal static class IcecastListen
{
    public static IEnumerable<string> Candidates(string streamUrl)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var url in Expand(streamUrl))
        {
            if (url.Length > 0 && seen.Add(url))
            {
                yield return url;
            }
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
        };
        if (uri.IsDefaultPort)
        {
            builder.Port = string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
                ? 443
                : 8000;
        }

        yield return builder.Uri.ToString();

        if (builder.Query.Length == 0)
        {
            builder.Query = "type=.mp3";
            yield return builder.Uri.ToString();
            builder.Query = string.Empty;
        }

        var path = builder.Path;
        if (path.Length > 1 && path.AsSpan().LastIndexOf('.') < 0)
        {
            builder.Path = path.TrimEnd('/') + ".mp3";
            yield return builder.Uri.ToString();
        }
    }
}
