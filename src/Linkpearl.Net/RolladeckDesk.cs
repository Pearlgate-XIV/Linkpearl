using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using Linkpearl.Modules;

namespace Linkpearl.Net;

public sealed class RolladeckDesk : IDisposable
{
    public const string BaseUrl = "https://us-central1-xiv-rolladeck.cloudfunctions.net/";
    public const string Brand = "Rolladeck";

    private static readonly TimeSpan LiveFresh = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan ScheduleFresh = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan MinGap = TimeSpan.FromSeconds(30);

    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNameCaseInsensitive = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
    };

    private readonly HttpClient http = new(new SocketsHttpHandler
    {
        AutomaticDecompression = DecompressionMethods.All,
        AllowAutoRedirect = true,
        ConnectTimeout = TimeSpan.FromSeconds(20),
    })
    {
        BaseAddress = new Uri(BaseUrl),
        Timeout = TimeSpan.FromSeconds(40),
        DefaultRequestVersion = HttpVersion.Version11,
        DefaultVersionPolicy = HttpVersionPolicy.RequestVersionOrLower,
    };

    private readonly string livePath;
    private readonly string schedulePath;
    private readonly object gate = new();
    private StreamInfo[] live = [];
    private StreamScheduleMark[] schedule = [];
    private string notice = "Community Twitch stations from Rolladeck.";
    private bool busy;
    private DateTimeOffset lastLiveTry;
    private DateTimeOffset lastScheduleTry;
    private DateTimeOffset? liveAt;
    private DateTimeOffset? scheduleAt;

    public RolladeckDesk(HostPaths paths)
    {
        var folder = paths.Cache("rolladeck");
        livePath = Path.Combine(folder, "live.json");
        schedulePath = Path.Combine(folder, "schedule.json");
        http.DefaultRequestHeaders.UserAgent.ParseAdd(
            "Linkpearl/0.1.0 (+https://github.com/Pearlgate-XIV/Linkpearl)");
        http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        try
        {
            Directory.CreateDirectory(folder);
        }
        catch
        {
            // Cache is optional.
        }

        TryLoad();
    }

    public IReadOnlyList<StreamInfo> Live
    {
        get
        {
            lock (gate)
            {
                return live;
            }
        }
    }

    public IReadOnlyList<StreamScheduleMark> Schedule
    {
        get
        {
            lock (gate)
            {
                return schedule;
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

    public void Refresh()
    {
        var now = DateTimeOffset.UtcNow;
        var wantLive = false;
        var wantSchedule = false;
        lock (gate)
        {
            if (busy)
            {
                return;
            }

            if (liveAt is not { } liveFresh || now - liveFresh >= LiveFresh)
            {
                if (now - lastLiveTry >= MinGap)
                {
                    lastLiveTry = now;
                    wantLive = true;
                }
            }

            if (scheduleAt is not { } scheduleFresh || now - scheduleFresh >= ScheduleFresh)
            {
                if (now - lastScheduleTry >= MinGap)
                {
                    lastScheduleTry = now;
                    wantSchedule = true;
                }
            }

            if (!wantLive && !wantSchedule)
            {
                return;
            }

            busy = true;
        }

        _ = PullAsync(wantLive, wantSchedule);
    }

    public void Dispose() => http.Dispose();

    private async Task PullAsync(bool wantLive, bool wantSchedule)
    {
        try
        {
            if (wantLive)
            {
                var body = await http.GetStringAsync("apiV1Live").ConfigureAwait(false);
                var parsed = JsonSerializer.Deserialize<LivePack>(body, Json);
                var rows = MapLive(parsed);
                lock (gate)
                {
                    live = rows;
                    liveAt = DateTimeOffset.UtcNow;
                    notice = rows.Length == 1
                        ? "1 community Twitch station from Rolladeck."
                        : rows.Length + " community Twitch stations from Rolladeck.";
                }

                TryWrite(livePath, body);
            }

            if (wantSchedule)
            {
                var body = await http.GetStringAsync("apiV1Schedule?days=14").ConfigureAwait(false);
                var parsed = JsonSerializer.Deserialize<SchedulePack>(body, Json);
                var rows = MapSchedule(parsed);
                lock (gate)
                {
                    schedule = rows;
                    scheduleAt = DateTimeOffset.UtcNow;
                }

                TryWrite(schedulePath, body);
            }
        }
        catch
        {
            lock (gate)
            {
                if (live.Length == 0)
                {
                    notice = "Rolladeck community board is quiet.";
                }
            }
        }
        finally
        {
            lock (gate)
            {
                busy = false;
            }
        }
    }

    private void TryLoad()
    {
        try
        {
            if (File.Exists(livePath))
            {
                var parsed = JsonSerializer.Deserialize<LivePack>(File.ReadAllText(livePath), Json);
                live = MapLive(parsed);
            }

            if (File.Exists(schedulePath))
            {
                var parsed = JsonSerializer.Deserialize<SchedulePack>(File.ReadAllText(schedulePath), Json);
                schedule = MapSchedule(parsed);
            }
        }
        catch
        {
            // Stale cache is skipped.
        }
    }

    private static void TryWrite(string path, string body)
    {
        try
        {
            File.WriteAllText(path, body);
        }
        catch
        {
            // Cache write is optional.
        }
    }

    private static StreamInfo[] MapLive(LivePack? pack)
    {
        var rows = pack?.LiveDjs;
        if (rows is not { Length: > 0 })
        {
            return [];
        }

        var mapped = new List<StreamInfo>(rows.Length);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < rows.Length; index++)
        {
            var row = rows[index];
            var login = StreamChrome.LoginOf(row.TwitchUrl ?? string.Empty);
            if (login.Length == 0 || !seen.Add(login))
            {
                continue;
            }

            var genres = row.Genres is { Length: > 0 } ? string.Join(" · ", row.Genres) : string.Empty;
            var venue = row.VenueName ?? string.Empty;
            var place = venue.Length > 0
                ? venue + (StreamChrome.PlaceLine(row.Server, row.District, row.Ward, row.Plot) is { Length: > 0 } rest
                    ? " · " + rest
                    : string.Empty)
                : StreamChrome.PlaceLine(row.Server, row.District, row.Ward, row.Plot);
            var watch = row.TwitchUrl ?? string.Empty;
            var li = row.Lifestream ?? StreamChrome.LiCommand(row.Server, row.District, row.Ward, row.Plot);
            var name = row.DjName ?? string.Empty;
            mapped.Add(new StreamInfo(
                StreamChrome.StreamId(login),
                StreamProviderKind.Twitch,
                StreamStatus.Live,
                string.Empty,
                login,
                name.Length > 0 ? name : login,
                row.StreamTitle ?? string.Empty,
                watch.Length > 0 ? watch : StreamChrome.WatchUrl(login),
                row.AvatarUrl ?? string.Empty,
                Math.Max(0, row.ViewerCount),
                genres,
                row.Bio ?? string.Empty,
                place,
                li,
                "community"));
        }

        return mapped.ToArray();
    }

    private static StreamScheduleMark[] MapSchedule(SchedulePack? pack)
    {
        var rows = pack?.Schedule;
        if (rows is not { Length: > 0 })
        {
            return [];
        }

        var mapped = new List<StreamScheduleMark>(rows.Length);
        for (var index = 0; index < rows.Length; index++)
        {
            var row = rows[index];
            if (!TryTime(row.StartDate, out var starts))
            {
                continue;
            }

            TryTime(row.EndDate, out var ends);
            var title = row.Name ?? string.Empty;
            var dj = row.DjName ?? string.Empty;
            mapped.Add(new StreamScheduleMark(
                row.Id ?? ("set-" + index.ToString(CultureInfo.InvariantCulture)),
                title.Length > 0 ? title : dj,
                dj,
                row.VenueName ?? string.Empty,
                StreamChrome.PlaceLine(row.Server, row.District, row.Ward, row.Plot),
                string.Empty,
                string.Empty,
                StreamChrome.LiCommand(row.Server, row.District, row.Ward, row.Plot),
                starts,
                ends,
                row.Type ?? "set"));
        }

        return mapped
            .OrderBy(static row => row.Starts)
            .ToArray();
    }

    private static bool TryTime(string? text, out DateTimeOffset stamp)
    {
        stamp = default;
        return !string.IsNullOrWhiteSpace(text) &&
               DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out stamp);
    }

    private sealed class LivePack
    {
        [JsonPropertyName("liveDJs")]
        public LiveRow[]? LiveDjs { get; set; }
    }

    private sealed class LiveRow
    {
        public string DjName { get; set; } = string.Empty;
        public string? TwitchUrl { get; set; }
        public string? AvatarUrl { get; set; }
        public string[]? Genres { get; set; }
        public int ViewerCount { get; set; }
        public string? StreamTitle { get; set; }
        public string? VenueName { get; set; }
        public string? Server { get; set; }
        public string? District { get; set; }
        public int? Ward { get; set; }
        public int? Plot { get; set; }
        public string? Lifestream { get; set; }
        public string? Bio { get; set; }
    }

    private sealed class SchedulePack
    {
        public SlotRow[]? Schedule { get; set; }
    }

    private sealed class SlotRow
    {
        public string? Type { get; set; }
        public string? Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? DjName { get; set; }
        public string? DjSlug { get; set; }
        public string? VenueName { get; set; }
        public string? StartDate { get; set; }
        public string? EndDate { get; set; }
        public string? Server { get; set; }
        public string? District { get; set; }
        public int? Ward { get; set; }
        public int? Plot { get; set; }
    }
}
