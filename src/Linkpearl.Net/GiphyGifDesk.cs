using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using Linkpearl.Chat;
using Linkpearl.Media;
using Linkpearl.Modules;

namespace Linkpearl.Net;

public sealed class GiphyGifDesk : IGifDesk, IDisposable
{
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly HttpClient http = new(new SocketsHttpHandler
    {
        AutomaticDecompression = DecompressionMethods.All,
        AllowAutoRedirect = true,
    })
    { Timeout = TimeSpan.FromSeconds(18) };
    private readonly HostPaths paths;
    private readonly Func<string, int, CancellationToken, Task<(byte[]? Body, int Status)>> search;
    private readonly object gate = new();
    private readonly HashSet<string> fetching = new(StringComparer.Ordinal);
    private GifHit[] hits = [];
    private string notice = string.Empty;
    private string pending = "\u0001";
    private bool busy;
    private bool warmed;
    private bool hasMore;
    private int stamp;
    private int loaded;

    public GiphyGifDesk(HostPaths paths,
        Func<string, int, CancellationToken, Task<(byte[]? Body, int Status)>> search)
    {
        this.paths = paths;
        this.search = search;
        http.DefaultRequestHeaders.UserAgent.ParseAdd("Linkpearl/0.1.0");
    }

    public void Dispose() => http.Dispose();

    public string Brand => "GIPHY";

    public string Notice
    {
        get
        {
            lock (gate)
            {
                return notice;
            }
        }
    }

    public bool Busy
    {
        get
        {
            lock (gate)
            {
                return busy;
            }
        }
    }

    public IReadOnlyList<GifHit> Hits
    {
        get
        {
            lock (gate)
            {
                return hits;
            }
        }
    }

    public bool CanMore
    {
        get
        {
            lock (gate)
            {
                return hasMore && !busy;
            }
        }
    }

    public void More()
    {
        string needle;
        int start;
        int mine;
        lock (gate)
        {
            if (!hasMore || busy)
            {
                return;
            }

            busy = true;
            needle = pending;
            start = loaded;
            mine = stamp;
        }

        _ = Task.Run(() => SearchAsync(needle, mine, start));
    }

    public void Warm()
    {
        if (warmed)
        {
            return;
        }

        warmed = true;
        NoteQuery(string.Empty);
    }

    public void NoteQuery(string query)
    {
        var needle = query.Trim();
        lock (gate)
        {
            if (needle == pending)
            {
                return;
            }

            pending = needle;
            loaded = 0;
            hasMore = false;
        }

        var mine = Interlocked.Increment(ref stamp);
        _ = Task.Run(() => SearchAsync(needle, mine, 0));
    }

    public string PathFor(string url) => GifCache.PathFor(paths, url);

    public void Ensure(string url)
    {
        if (!GifCache.IsUrl(url))
        {
            return;
        }

        var path = PathFor(url);
        if (File.Exists(path))
        {
            return;
        }

        lock (gate)
        {
            if (!fetching.Add(url))
            {
                return;
            }
        }

        _ = Task.Run(() => DownloadAsync(url, path));
    }

    private async Task SearchAsync(string needle, int mine, int start)
    {
        if (start == 0)
        {
            lock (gate)
            {
                busy = true;
            }
        }

        try
        {
            var (bytes, status) = await search(needle, start, CancellationToken.None).ConfigureAwait(false);
            if (mine != Volatile.Read(ref stamp))
            {
                return;
            }

            if (status is < 200 or >= 300 || bytes is null || bytes.Length == 0)
            {
                lock (gate)
                {
                    if (start == 0)
                    {
                        hits = [];
                    }

                    notice = status == 401
                        ? "Sign in to search GIFs."
                        : status == 404
                            ? "GIF search is not on Pearlgate yet."
                            : "GIF search is not answering right now.";
                    busy = false;
                    hasMore = false;
                }

                return;
            }

            var body = System.Text.Encoding.UTF8.GetString(bytes);
            var page = JsonSerializer.Deserialize<GiphyPage>(body, Json);
            var gatePage = JsonSerializer.Deserialize(body, GateJson.Default.GifPageDto);
            var next = new List<GifHit>();
            if (start > 0)
            {
                lock (gate)
                {
                    next.AddRange(hits);
                }
            }

            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (var index = 0; index < next.Count; index++)
            {
                seen.Add(next[index].Id);
            }

            foreach (var row in page?.Data ?? [])
            {
                var id = row.Id ?? string.Empty;
                var send = id.Length > 0
                    ? "https://media.giphy.com/media/" + id + "/200w.gif"
                    : FirstUrl(row.Images?.FixedWidth, row.Images?.Downsized, row.Images?.Preview);
                var preview = FirstUrl(row.Images?.Preview, row.Images?.FixedWidth, row.Images?.Still,
                    row.Images?.PreviewStill);
                AddHit(next, seen, id, row.Title, preview, send);
            }

            foreach (var row in gatePage?.Items ?? [])
            {
                var send = FirstText(row.SendUrl, row.Send, row.Url);
                var preview = FirstText(row.PreviewUrl, row.Preview, send);
                AddHit(next, seen, row.Id, row.Title, preview, send);
            }

            var total = page?.Pagination?.TotalCount ?? (gatePage is { Total: > 0 } ? gatePage.Total : next.Count);
            lock (gate)
            {
                hits = next.ToArray();
                loaded = hits.Length;
                hasMore = hits.Length < total;
                notice = hits.Length == 0
                    ? (needle.Length > 0 ? "No GIFs match." : "No trending GIFs.")
                    : string.Empty;
                busy = false;
            }
        }
        catch (Exception)
        {
            if (mine != Volatile.Read(ref stamp))
            {
                return;
            }

            lock (gate)
            {
                if (start == 0)
                {
                    hits = [];
                }

                notice = "Could not reach GIF search.";
                busy = false;
                hasMore = false;
            }
        }
    }

    private async Task DownloadAsync(string url, string path)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path) ?? paths.CacheDirectory);
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Referrer = new Uri("https://giphy.com/");
            request.Headers.Accept.ParseAdd("image/*,*/*");
            using var response = await http.SendAsync(request).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return;
            }

            var bytes = await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
            if (bytes.Length == 0 || bytes.Length > 8_000_000)
            {
                return;
            }

            if (!GifStill.IsGif(bytes) && !IsPng(bytes) && !IsJpeg(bytes))
            {
                return;
            }

            await File.WriteAllBytesAsync(path, bytes).ConfigureAwait(false);
        }
        catch (Exception)
        {
            // Preview tiles stay blank until a later frame can fetch the still.
        }
        finally
        {
            lock (gate)
            {
                fetching.Remove(url);
            }
        }
    }

    private static bool IsPng(byte[] bytes) =>
        bytes.Length >= 8 && bytes[0] == 0x89 && bytes[1] == (byte)'P' && bytes[2] == (byte)'N' &&
        bytes[3] == (byte)'G';

    private static bool IsJpeg(byte[] bytes) =>
        bytes.Length >= 3 && bytes[0] == 0xFF && bytes[1] == 0xD8;

    private static void AddHit(List<GifHit> next, HashSet<string> seen, string? id, string? title, string preview,
        string send)
    {
        if (send.Length == 0)
        {
            return;
        }

        var key = id is { Length: > 0 } ? id : send;
        if (!seen.Add(key))
        {
            return;
        }

        next.Add(new GifHit(key, title is { Length: > 0 } ? title : "GIF", preview.Length > 0 ? preview : send, send));
    }

    private static string FirstText(params string?[] values)
    {
        for (var index = 0; index < values.Length; index++)
        {
            var text = values[index]?.Trim();
            if (text is { Length: > 0 })
            {
                return text;
            }
        }

        return string.Empty;
    }

    private static string FirstUrl(params GiphyFile?[] files)
    {
        for (var index = 0; index < files.Length; index++)
        {
            var url = files[index]?.Url;
            if (url is { Length: > 0 })
            {
                return url;
            }
        }

        return string.Empty;
    }

    private sealed class GiphyPage
    {
        public GiphyRow[]? Data { get; set; }

        public GiphyPaging? Pagination { get; set; }
    }

    private sealed class GiphyPaging
    {
        [JsonPropertyName("total_count")]
        public int TotalCount { get; set; }

        public int Count { get; set; }

        public int Offset { get; set; }
    }

    private sealed class GiphyRow
    {
        public string? Id { get; set; }

        public string? Title { get; set; }

        public GiphyImages? Images { get; set; }
    }

    private sealed class GiphyImages
    {
        [JsonPropertyName("fixed_width")]
        public GiphyFile? FixedWidth { get; set; }

        [JsonPropertyName("fixed_width_small_still")]
        public GiphyFile? Still { get; set; }

        [JsonPropertyName("downsized_still")]
        public GiphyFile? PreviewStill { get; set; }

        [JsonPropertyName("preview_gif")]
        public GiphyFile? Preview { get; set; }

        [JsonPropertyName("downsized")]
        public GiphyFile? Downsized { get; set; }
    }

    private sealed class GiphyFile
    {
        public string? Url { get; set; }
    }
}
