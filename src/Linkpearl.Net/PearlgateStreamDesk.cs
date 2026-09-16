using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using Linkpearl.Modules;

namespace Linkpearl.Net;

public sealed class PearlgateStreamDesk : IStreamDesk
{
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private static readonly TimeSpan FreshFor = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan MinGap = TimeSpan.FromSeconds(20);

    private readonly Func<string> token;
    private readonly HttpClient http;
    private readonly object gate = new();
    private StreamProviderAccount own;
    private StreamInfo[] live = [];
    private string notice = "Twitch link is held on Pearlgate. No secrets live on the phone.";
    private bool busy;
    private DateTimeOffset lastAttempt;
    private DateTimeOffset? fetchedAt;
    private bool missingRoutes;

    public PearlgateStreamDesk(Func<string> token, HostPaths? paths = null, string? baseUrl = null)
    {
        this.token = token;
        http = new HttpClient
        {
            BaseAddress = new Uri((baseUrl ?? GateClient.DefaultBaseUrl).TrimEnd('/') + "/"),
            Timeout = TimeSpan.FromSeconds(18),
        };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("Linkpearl/0.1.0");
        http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        _ = paths;
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

    public IReadOnlyList<StreamScheduleMark> CommunitySchedule { get; } = [];

    public StreamProviderAccount Own
    {
        get
        {
            lock (gate)
            {
                return own;
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
        lock (gate)
        {
            if (busy || missingRoutes)
            {
                return;
            }

            if (fetchedAt is { } at && now - at < FreshFor)
            {
                return;
            }

            if (now - lastAttempt < MinGap)
            {
                return;
            }

            lastAttempt = now;
            busy = true;
        }

        _ = PullAsync();
    }

    public void RequestConnect()
    {
        _ = ConnectAsync();
    }

    public void Disconnect()
    {
        _ = DropAsync();
    }

    public StreamInfo? Find(string idOrLogin)
    {
        var key = StreamChrome.LoginOf(idOrLogin);
        lock (gate)
        {
            for (var index = 0; index < live.Length; index++)
            {
                var row = live[index];
                if (string.Equals(row.Id, idOrLogin, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(row.Username, key, StringComparison.OrdinalIgnoreCase))
                {
                    return row;
                }
            }
        }

        return null;
    }

    public void Dispose() => http.Dispose();

    private async Task PullAsync()
    {
        try
        {
            BindToken();
            LiveWire? board = null;
            try
            {
                board = await ReadJson<LiveWire>("radio/streams/live").ConfigureAwait(false);
            }
            catch (StreamRouteMissingException)
            {
                MarkMissing(
                    "Pearlgate has no Twitch live route yet. Community Twitch stations still come from Rolladeck.",
                    stayDown: true);
            }

            MeWire? me = null;
            var bearer = token()?.Trim() ?? string.Empty;
            if (bearer.Length > 0)
            {
                try
                {
                    me = await ReadJson<MeWire>("me/streaming").ConfigureAwait(false);
                }
                catch (StreamRouteMissingException)
                {
                    lock (gate)
                    {
                        if (!missingRoutes)
                        {
                            notice =
                                "Connect Twitch when Pearlgate exposes the OAuth route. Community Twitch stations still come from Rolladeck.";
                        }
                    }
                }
                catch (UnauthorizedAccessException)
                {
                    lock (gate)
                    {
                        notice = "Sign in on You to link Twitch through Pearlgate.";
                    }
                }
            }

            lock (gate)
            {
                if (me != null)
                {
                    own = new StreamProviderAccount(StreamProviderKind.Twitch, me.ExternalUserId ?? string.Empty,
                        me.Username ?? string.Empty, me.DisplayName ?? me.Username ?? string.Empty, me.Verified,
                        me.Connected);
                }

                if (board != null)
                {
                    live = MapLive(board);
                    missingRoutes = false;
                }

                fetchedAt = DateTimeOffset.UtcNow;
                if (!missingRoutes)
                {
                    notice = own.Connected
                        ? "Twitch connected through Pearlgate."
                        : bearer.Length == 0
                            ? "Sign in on You to link Twitch through Pearlgate."
                            : "Connect Twitch through Pearlgate. The phone never stores Twitch tokens.";
                }
            }
        }
        catch (TaskCanceledException)
        {
            MarkMissing("Pearlgate Twitch timed out.", stayDown: false);
        }
        catch
        {
            MarkMissing("Pearlgate Twitch is quiet. Community Twitch stations still come from Rolladeck.",
                stayDown: false);
        }
        finally
        {
            lock (gate)
            {
                busy = false;
            }
        }
    }

    private async Task ConnectAsync()
    {
        var bearer = token()?.Trim() ?? string.Empty;
        if (bearer.Length == 0)
        {
            lock (gate)
            {
                notice = "Sign in on You first, then connect Twitch through Pearlgate.";
            }

            return;
        }

        try
        {
            BindToken();
            var reply = await PostJson<ConnectWire>("me/streaming/twitch/connect", new { }).ConfigureAwait(false);
            var url = reply?.AuthorizeUrl ?? string.Empty;
            if (url.Length > 0)
            {
                StreamChrome.OpenWatch(url);
                lock (gate)
                {
                    notice = "Finish linking on Twitch. Pearlgate keeps the tokens.";
                }

                return;
            }

            lock (gate)
            {
                notice = "Pearlgate did not return a Twitch authorize URL yet.";
            }
        }
        catch (StreamRouteMissingException)
        {
            MarkMissing("Connect Twitch when Pearlgate exposes the OAuth route.", stayDown: false);
        }
        catch
        {
            MarkMissing("Connect Twitch when Pearlgate exposes the OAuth route.", stayDown: false);
        }
    }

    private async Task DropAsync()
    {
        try
        {
            BindToken();
            using var request = new HttpRequestMessage(HttpMethod.Delete, "me/streaming/twitch");
            using var reply = await http.SendAsync(request).ConfigureAwait(false);
            lock (gate)
            {
                own = default;
                notice = "Twitch disconnected. Your station and events stay.";
            }
        }
        catch
        {
            lock (gate)
            {
                own = default;
                notice = "Twitch disconnect is local until Pearlgate accepts it.";
            }
        }
    }

    private void BindToken()
    {
        var bearer = token()?.Trim() ?? string.Empty;
        http.DefaultRequestHeaders.Authorization = bearer.Length == 0
            ? null
            : new AuthenticationHeaderValue("Bearer", bearer);
    }

    private async Task<T?> ReadJson<T>(string path)
    {
        using var reply = await http.GetAsync(path).ConfigureAwait(false);
        ThrowIfRouteGone(reply);
        reply.EnsureSuccessStatusCode();
        await using var stream = await reply.Content.ReadAsStreamAsync().ConfigureAwait(false);
        return await JsonSerializer.DeserializeAsync<T>(stream, Json).ConfigureAwait(false);
    }

    private async Task<T?> PostJson<T>(string path, object body)
    {
        using var reply = await http.PostAsync(path,
            new StringContent(JsonSerializer.Serialize(body, Json), System.Text.Encoding.UTF8, "application/json"))
            .ConfigureAwait(false);
        ThrowIfRouteGone(reply);
        reply.EnsureSuccessStatusCode();
        await using var stream = await reply.Content.ReadAsStreamAsync().ConfigureAwait(false);
        return await JsonSerializer.DeserializeAsync<T>(stream, Json).ConfigureAwait(false);
    }

    private static void ThrowIfRouteGone(HttpResponseMessage reply)
    {
        if ((int)reply.StatusCode is 404 or 501)
        {
            throw new StreamRouteMissingException();
        }

        if ((int)reply.StatusCode is 401 or 403)
        {
            throw new UnauthorizedAccessException();
        }
    }

    private void MarkMissing(string copy, bool stayDown)
    {
        lock (gate)
        {
            if (stayDown)
            {
                missingRoutes = true;
                live = [];
                own = default;
            }

            notice = copy;
        }
    }

    private sealed class StreamRouteMissingException : Exception
    {
    }

    private static StreamInfo[] MapLive(LiveWire? board)
    {
        var rows = board?.Streams;
        if (rows is not { Length: > 0 })
        {
            return [];
        }

        var mapped = new List<StreamInfo>(rows.Length);
        for (var index = 0; index < rows.Length; index++)
        {
            var row = rows[index];
            var login = StreamChrome.LoginOf(row.Username ?? row.WatchUrl ?? string.Empty);
            if (login.Length == 0)
            {
                continue;
            }

            var display = row.DisplayName ?? string.Empty;
            var watch = row.WatchUrl ?? string.Empty;
            mapped.Add(new StreamInfo(
                StreamChrome.StreamId(login),
                StreamProviderKind.Twitch,
                row.Live ? StreamStatus.Live : StreamStatus.Offline,
                row.ExternalUserId ?? string.Empty,
                login,
                display.Length > 0 ? display : login,
                row.Title ?? string.Empty,
                watch.Length > 0 ? watch : StreamChrome.WatchUrl(login),
                row.ArtUrl ?? string.Empty,
                Math.Max(0, row.Viewers),
                row.Genre ?? string.Empty,
                row.Bio ?? string.Empty,
                row.VenueLine ?? string.Empty,
                row.Lifestream ?? string.Empty,
                "pearlgate"));
        }

        return mapped.ToArray();
    }

    private sealed class MeWire
    {
        public string? ExternalUserId { get; set; }
        public string? Username { get; set; }
        public string? DisplayName { get; set; }
        public bool Verified { get; set; }
        public bool Connected { get; set; }
    }

    private sealed class ConnectWire
    {
        public string? AuthorizeUrl { get; set; }
        public string? State { get; set; }
    }

    private sealed class LiveWire
    {
        public StreamRow[]? Streams { get; set; }
    }

    private sealed class StreamRow
    {
        public string? Username { get; set; }
        public string? DisplayName { get; set; }
        public string? ExternalUserId { get; set; }
        public string? Title { get; set; }
        public string? WatchUrl { get; set; }
        public string? ArtUrl { get; set; }
        public string? Genre { get; set; }
        public string? Bio { get; set; }
        public string? VenueLine { get; set; }
        public string? Lifestream { get; set; }
        public int Viewers { get; set; }
        public bool Live { get; set; } = true;
    }
}
