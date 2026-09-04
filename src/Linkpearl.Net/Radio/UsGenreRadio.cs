using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Linkpearl.Audio;

namespace Linkpearl.Net.Radio;

public sealed class UsGenreRadio : IPublicRadio, IDisposable
{
    private static readonly (string Name, string[] Tags)[] Map =
    {
        ("Lo-Fi", ["lofi", "lo-fi"]),
        ("Rock", ["rock"]),
        ("Metal", ["metal"]),
        ("Electronic", ["electronic", "edm"]),
        ("Bass", ["bass"]),
        ("Dubstep", ["dubstep"]),
        ("Techno", ["techno"]),
        ("Chill", ["chill", "chillout"]),
        ("House", ["house"]),
        ("Ambient", ["ambient"]),
    };

    private static readonly string[] Hosts =
    {
        "https://de1.api.radio-browser.info/",
        "https://de2.api.radio-browser.info/",
        "https://at1.api.radio-browser.info/",
        "https://nl1.api.radio-browser.info/",
        "https://all.api.radio-browser.info/",
    };

    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly HttpClient http = new() { Timeout = TimeSpan.FromSeconds(22) };
    private readonly object gate = new();
    private readonly Dictionary<string, Cache> caches = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> inflight = new(StringComparer.OrdinalIgnoreCase);
    private string notice = string.Empty;
    private int hostIndex;

    public UsGenreRadio()
    {
        http.DefaultRequestHeaders.UserAgent.ParseAdd("Linkpearl/0.1.0 (handset radio; +https://pearlgate.local)");
        http.DefaultRequestHeaders.Accept.ParseAdd("application/json");
    }

    public IReadOnlyList<string> Genres { get; } = Map.Select(static row => row.Name).ToArray();

    public bool Busy
    {
        get
        {
            lock (gate)
            {
                return inflight.Count > 0;
            }
        }
    }

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

    public IReadOnlyList<PublicStation> Stations(string genre)
    {
        lock (gate)
        {
            return caches.TryGetValue(genre, out var cache) ? cache.Stations : [];
        }
    }

    public void Ensure(string genre)
    {
        if (genre.Length == 0)
        {
            return;
        }

        lock (gate)
        {
            if (caches.TryGetValue(genre, out var cache) &&
                DateTimeOffset.UtcNow - cache.At < TimeSpan.FromMinutes(12) &&
                cache.Stations.Count > 1)
            {
                return;
            }

            if (!inflight.Add(genre))
            {
                return;
            }
        }

        _ = Task.Run(() => Load(genre));
    }

    public void Dispose() => http.Dispose();

    private async Task Load(string genre)
    {
        try
        {
            var tags = TagsFor(genre);
            var merged = new Dictionary<string, PublicStation>(StringComparer.OrdinalIgnoreCase);
            for (var index = 0; index < tags.Length; index++)
            {
                var rows = await Fetch(tags[index]).ConfigureAwait(false);
                foreach (var row in rows)
                {
                    var url = (row.UrlResolved ?? row.Url ?? string.Empty).Trim();
                    if (url.Length == 0)
                    {
                        continue;
                    }

                    var id = row.StationUuid ?? url;
                    merged[id] = new PublicStation(id, Clean(row.Name), genre, PlaceOf(row), url, row.Bitrate,
                        (row.Favicon ?? string.Empty).Trim());
                }
            }

            var list = merged.Values
                .OrderByDescending(static station => station.Bitrate)
                .ThenBy(static station => station.Title, StringComparer.OrdinalIgnoreCase)
                .Take(80)
                .ToArray();
            lock (gate)
            {
                caches[genre] = new Cache(DateTimeOffset.UtcNow, list);
                notice = list.Length == 0
                    ? "No stations for this genre right now."
                    : list.Length + " stations loaded.";
            }
        }
        catch (Exception)
        {
            lock (gate)
            {
                notice = "Could not reach the radio directory.";
            }
        }
        finally
        {
            lock (gate)
            {
                inflight.Remove(genre);
            }
        }
    }

    private async Task<List<Row>> Fetch(string tag)
    {
        var body = new Search
        {
            Tag = tag,
            HideBroken = true,
            Order = "clickcount",
            Reverse = true,
            Limit = 80,
        };

        for (var attempt = 0; attempt < Hosts.Length; attempt++)
        {
            var host = Hosts[(hostIndex + attempt) % Hosts.Length];
            try
            {
                using var response = await http.PostAsJsonAsync(host + "json/stations/search", body, Json)
                    .ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                {
                    continue;
                }

                var rows = await response.Content.ReadFromJsonAsync<List<Row>>(Json).ConfigureAwait(false);
                if (rows is { Count: > 0 })
                {
                    hostIndex = (hostIndex + attempt) % Hosts.Length;
                    return rows;
                }
            }
            catch (HttpRequestException)
            {
            }
            catch (TaskCanceledException)
            {
            }
            catch (JsonException)
            {
            }
        }

        return [];
    }

    private static string[] TagsFor(string genre)
    {
        for (var index = 0; index < Map.Length; index++)
        {
            if (string.Equals(Map[index].Name, genre, StringComparison.OrdinalIgnoreCase))
            {
                return Map[index].Tags;
            }
        }

        return [genre];
    }

    private static string PlaceOf(Row row)
    {
        var country = (row.Country ?? string.Empty).Trim();
        return country.Length > 0 ? country : "Live";
    }

    private static string Clean(string? value)
    {
        var text = (value ?? string.Empty).Trim();
        return text.Length == 0 ? "Station" : text;
    }

    private sealed record Cache(DateTimeOffset At, IReadOnlyList<PublicStation> Stations);

    private sealed class Search
    {
        [JsonPropertyName("tag")]
        public string Tag { get; set; } = string.Empty;

        [JsonPropertyName("hidebroken")]
        public bool HideBroken { get; set; }

        [JsonPropertyName("order")]
        public string Order { get; set; } = "clickcount";

        [JsonPropertyName("reverse")]
        public bool Reverse { get; set; }

        [JsonPropertyName("limit")]
        public int Limit { get; set; }
    }

    private sealed class Row
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("url")]
        public string? Url { get; set; }

        [JsonPropertyName("url_resolved")]
        public string? UrlResolved { get; set; }

        [JsonPropertyName("stationuuid")]
        public string? StationUuid { get; set; }

        [JsonPropertyName("bitrate")]
        public int Bitrate { get; set; }

        [JsonPropertyName("country")]
        public string? Country { get; set; }

        [JsonPropertyName("favicon")]
        public string? Favicon { get; set; }
    }
}
