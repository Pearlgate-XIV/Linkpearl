namespace Linkpearl.Net;

public sealed partial class PearlHub
{
    public async Task<(byte[]? Body, int Status)> FetchGifsAsync(string query, int offset,
        CancellationToken token)
    {
        var needle = query.Trim();
        var start = Math.Max(0, offset).ToString(System.Globalization.CultureInfo.InvariantCulture);
        string[] paths;
        if (needle.Length == 0)
        {
            paths =
            [
                "gifs?limit=24&offset=" + start,
                "gifs/trending?limit=24&offset=" + start,
            ];
        }
        else
        {
            var q = Uri.EscapeDataString(needle);
            paths =
            [
                "gifs?q=" + q + "&limit=24&offset=" + start,
                "gifs/search?q=" + q + "&limit=24&offset=" + start,
            ];
        }

        (byte[]? Body, int Status) last = (null, 404);
        for (var index = 0; index < paths.Length; index++)
        {
            last = await client.GetBytesAsync(paths[index], token).ConfigureAwait(false);
            if (last.Status != 404)
            {
                return last;
            }
        }

        return last;
    }
}
