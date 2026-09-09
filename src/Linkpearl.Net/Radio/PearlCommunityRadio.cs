using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Linkpearl.Audio;
using Linkpearl.Modules;
using Linkpearl.Net;

namespace Linkpearl.Net.Radio;

// Local-first station book that syncs to Pearlgate when the radio API is up.
// Audio never goes peer-to-peer: the phone will push Opus to Pearlgate's relay,
// and listeners only ever receive a listen URL from that relay.
public sealed class PearlCommunityRadio : ICommunityRadio, IDisposable
{
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    private readonly Func<string> token;
    private readonly Func<bool> signedIn;
    private readonly string bookPath;
    private readonly string defaultIceListenBase;
    private readonly string defaultIceUser;
    private readonly string defaultIcePassword;
    private readonly SemaphoreSlim writeLock = new(1, 1);
    private readonly HttpClient http;
    private readonly object gate = new();
    private List<StationRow> rows = new();
    private readonly Dictionary<string, LikeMark> publicLikes = new(StringComparer.OrdinalIgnoreCase);
    private string ownedId = string.Empty;
    private bool broadcasting;
    private string notice = string.Empty;
    private string iceListenBase = string.Empty;
    private string iceUser = "source";
    private string icePassword = string.Empty;

    public PearlCommunityRadio(Func<string> token, Func<bool> signedIn, HostPaths? paths = null, string? baseUrl = null,
        string? iceListenBase = null, string? iceUser = null, string? icePassword = null)
    {
        this.token = token;
        this.signedIn = signedIn;
        defaultIceListenBase = iceListenBase?.Trim() ?? string.Empty;
        defaultIceUser = string.IsNullOrWhiteSpace(iceUser) ? "source" : iceUser.Trim();
        defaultIcePassword = icePassword?.Trim() ?? string.Empty;
        this.iceListenBase = defaultIceListenBase;
        this.iceUser = defaultIceUser;
        this.icePassword = defaultIcePassword;
        bookPath = paths is null ? string.Empty : paths.State("community-radio.json");
        http = new HttpClient
        {
            BaseAddress = new Uri((baseUrl ?? GateClient.DefaultBaseUrl).TrimEnd('/') + "/"),
            Timeout = TimeSpan.FromSeconds(18),
        };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("Linkpearl/0.1.0");
        http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        LoadBook();
        lock (gate)
        {
            DropMockLocked();
            SaveBookLocked();
        }
    }

    public IReadOnlyList<CommunityStation> Directory
    {
        get
        {
            lock (gate)
            {
                return Map(rows);
            }
        }
    }

    public IReadOnlyList<CommunityStation> Live
    {
        get
        {
            lock (gate)
            {
                return Map(rows.Where(static row => row.Live));
            }
        }
    }

    public IReadOnlyList<CommunityStation> Mine
    {
        get
        {
            lock (gate)
            {
                return Map(rows.Where(row => row.Owned || string.Equals(row.Id, ownedId, StringComparison.Ordinal)));
            }
        }
    }

    public bool Broadcasting
    {
        get
        {
            lock (gate)
            {
                return broadcasting;
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

    public bool SignedIn => signedIn();

    public string OwnedId
    {
        get
        {
            lock (gate)
            {
                return ownedId;
            }
        }
    }

    public string OwnedListenUrl
    {
        get
        {
            lock (gate)
            {
                return FindOwnedLocked()?.ListenUrl ?? string.Empty;
            }
        }
    }

    public string OwnedMount
    {
        get
        {
            lock (gate)
            {
                return FindOwnedLocked()?.Mount ?? string.Empty;
            }
        }
    }

    public string OwnedIngestUrl
    {
        get
        {
            lock (gate)
            {
                return FindOwnedLocked()?.IngestUrl ?? string.Empty;
            }
        }
    }

    public void UseIcecast(string listenBase, string sourceUser, string sourcePassword)
    {
        lock (gate)
        {
            var host = listenBase.Trim();
            var user = sourceUser.Trim().Length > 0 ? sourceUser.Trim() : "source";
            var pass = sourcePassword ?? string.Empty;
            if (host.Length == 0)
            {
                iceListenBase = defaultIceListenBase;
                iceUser = defaultIceUser;
                icePassword = defaultIcePassword;
                if (iceListenBase.Length > 0)
                {
                    ApplyRelayLocked(FindOwnedLocked());
                }

                return;
            }

            if (string.Equals(iceListenBase, host, StringComparison.Ordinal) &&
                string.Equals(iceUser, user, StringComparison.Ordinal) &&
                string.Equals(icePassword, pass, StringComparison.Ordinal))
            {
                ApplyRelayLocked(FindOwnedLocked());
                return;
            }

            iceListenBase = host;
            iceUser = user;
            icePassword = pass;
            ApplyRelayLocked(FindOwnedLocked());
            SaveBookLocked();
        }
    }

    public void Refresh() => _ = Task.Run(RefreshAsync);

    public void EnsureStation(string name, string host, string genre, string bio, string artPath, string mount)
    {
        UpsertOwnedLocal(name, host, genre, bio, artPath, mount);
        _ = Task.Run(() => PushOwnedAsync());
    }

    public void GoLive(string name, string genre) => _ = Task.Run(() => GoLiveAsync(name, genre));

    public void EndLive() => _ = Task.Run(EndLiveAsync);

    public int StationLikes(string stationId)
    {
        var id = BareStationId(stationId);
        if (id.Length == 0)
        {
            return 0;
        }

        lock (gate)
        {
            var row = rows.FirstOrDefault(item => string.Equals(item.Id, id, StringComparison.OrdinalIgnoreCase));
            if (row is not null)
            {
                return Math.Max(0, row.Likes);
            }

            return publicLikes.TryGetValue(id, out var mark) ? Math.Max(0, mark.Count) : 0;
        }
    }

    public bool StationLiked(string stationId)
    {
        var id = BareStationId(stationId);
        if (id.Length == 0)
        {
            return false;
        }

        lock (gate)
        {
            var row = rows.FirstOrDefault(item => string.Equals(item.Id, id, StringComparison.OrdinalIgnoreCase));
            if (row is not null)
            {
                return row.Liked;
            }

            return publicLikes.TryGetValue(id, out var mark) && mark.Mine;
        }
    }

    public void ToggleStationLike(string stationId) => ToggleStationLike(stationId, 0);

    public void ToggleStationLike(string stationId, int shownCount)
    {
        var id = BareStationId(stationId);
        if (id.Length == 0)
        {
            return;
        }

        bool liked;
        bool hub;
        lock (gate)
        {
            liked = !StationLikedLocked(id);
            var current = Math.Max(StationLikesLocked(id), Math.Max(0, shownCount));
            ApplyLikeLocked(id, liked, current + (liked ? 1 : -1));
            SaveBookLocked();
            hub = rows.Any(item => string.Equals(item.Id, id, StringComparison.OrdinalIgnoreCase));
        }

        if (hub)
        {
            _ = Task.Run(() => PushLikeAsync(id, liked));
        }
    }

    public void Dispose()
    {
        writeLock.Dispose();
        http.Dispose();
    }

    private async Task RefreshAsync()
    {
        try
        {
            ApplyAuth();
            var community = await GetList("radio/community").ConfigureAwait(false);
            var live = await GetList("radio/live").ConfigureAwait(false);
            var owned = SignedIn ? await GetList("radio/mine").ConfigureAwait(false) : [];
            lock (gate)
            {
                var keepLive = broadcasting;
                MergeRemote(community, owned: false);
                MergeRemote(live, owned: false);
                MergeRemote(owned, owned: true);
                if (ownedId.Length == 0)
                {
                    var mine = rows.FirstOrDefault(static row => row.Owned);
                    if (mine is not null)
                    {
                        ownedId = mine.Id;
                    }
                }

                if (keepLive)
                {
                    broadcasting = true;
                    var mine = FindOwnedLocked();
                    if (mine is not null)
                    {
                        mine.Live = true;
                    }
                }
                else
                {
                    broadcasting = rows.Any(row =>
                        row.Live && (row.Owned || string.Equals(row.Id, ownedId, StringComparison.Ordinal)));
                }

                if (!keepLive)
                {
                    notice = string.Empty;
                }

                DropMockLocked();
                SaveBookLocked();
            }
        }
        catch (Exception)
        {
            lock (gate)
            {
                DropMockLocked();
                notice = rows.Count > 0
                    ? string.Empty
                    : SignedIn
                        ? "Pearlgate radio is not up yet. Your station still lists here."
                        : "Sign in to publish a community station. You can still create one on this phone.";
                SaveBookLocked();
            }
        }
    }

    private void DropMockLocked()
    {
        rows.RemoveAll(static row =>
            string.Equals(row.Id, "pearlgate-test", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(row.Mount, "pearlgate-test", StringComparison.OrdinalIgnoreCase));
    }

    private StationRow UpsertOwnedLocal(string name, string host, string genre, string bio, string artPath, string mount)
    {
        var station = name.Trim().Length > 0 ? name.Trim() : "My Station";
        var dj = host.Trim().Length > 0 ? host.Trim() : "DJ";
        var slug = NormalizeMount(mount, station);
        lock (gate)
        {
            var row = FindOwnedLocked() ?? new StationRow
            {
                Id = "lp:" + Guid.NewGuid().ToString("N")[..10],
                Owned = true,
            };
            row.Name = station;
            row.Host = dj;
            row.Genre = genre;
            row.Bio = bio;
            row.ArtPath = artPath;
            row.Mount = slug;
            row.Owned = true;
            UpsertLocked(row);
            ownedId = row.Id;
            ApplyRelayLocked(row);
            notice = SignedIn
                ? "Publishing your station to Pearlgate so others can find it."
                : iceListenBase.Length > 0
                    ? "Station saved on this phone. Sign in so everyone else can see it."
                    : "Station saved. Sign in on You so Pearlgate lists it for the community.";
            SaveBookLocked();
            return row;
        }
    }

    private Task EnsureStationAsync(string name, string host, string genre, string bio, string artPath, string mount)
    {
        UpsertOwnedLocal(name, host, genre, bio, artPath, mount);
        return PushOwnedAsync();
    }

    private async Task PushOwnedAsync()
    {
        await writeLock.WaitAsync().ConfigureAwait(false);
        try
        {
            await PushOwnedUnlockedAsync().ConfigureAwait(false);
        }
        finally
        {
            writeLock.Release();
        }
    }

    private async Task PushOwnedUnlockedAsync()
    {
        StationRow row;
        lock (gate)
        {
            var owned = FindOwnedLocked();
            if (owned is null)
            {
                return;
            }

            row = owned;
        }

        if (!SignedIn)
        {
            return;
        }

        try
        {
            ApplyAuth();
            var created = await http.PostAsJsonAsync("radio/stations",
                    new CreateBody(row.Name, row.Genre, row.Host, row.Bio, row.Id, row.Mount))
                .ConfigureAwait(false);
            if (!created.IsSuccessStatusCode)
            {
                lock (gate)
                {
                    ApplyRelayLocked(FindOwnedLocked());
                    notice = created.StatusCode == System.Net.HttpStatusCode.NotFound
                        ? "Pearlgate radio is not on this API yet. Your station is on this phone until the server is updated."
                        : "Pearlgate did not save the station (" + (int)created.StatusCode + ").";
                    SaveBookLocked();
                }

                return;
            }

            var body = await created.Content.ReadFromJsonAsync<StationBody>().ConfigureAwait(false);
            lock (gate)
            {
                if (body?.Id is { Length: > 0 } && !string.Equals(body.Id, row.Id, StringComparison.Ordinal))
                {
                    rows.RemoveAll(item => string.Equals(item.Id, row.Id, StringComparison.Ordinal));
                    row.Id = body.Id;
                    ownedId = row.Id;
                }

                ApplyRemote(row, body);
                ApplyRelayLocked(row);
                notice = row.ListenUrl.Length > 0
                    ? "Mount /" + row.Mount + " is on Pearlgate. Go live when you are ready."
                    : "Station saved. Go live to have Pearlgate open the Icecast mount.";
                UpsertLocked(row);
                SaveBookLocked();
            }
        }
        catch (Exception)
        {
            lock (gate)
            {
                notice = "Could not reach Pearlgate. Your station is saved on this phone.";
            }
        }
    }

    private async Task GoLiveAsync(string name, string genre)
    {
        string host;
        string bio;
        string art;
        string station;
        string mount;
        lock (gate)
        {
            var existing = FindOwnedLocked();
            station = name.Trim().Length > 0 ? name.Trim() : existing?.Name ?? "My Station";
            host = existing is { Host.Length: > 0 } ? existing.Host : station;
            bio = existing?.Bio ?? string.Empty;
            art = existing?.ArtPath ?? string.Empty;
            genre = genre.Length > 0 ? genre : existing?.Genre ?? string.Empty;
            mount = existing?.Mount ?? string.Empty;
        }

        await EnsureStationAsync(station, host, genre, bio, art, mount).ConfigureAwait(false);
        string id;
        lock (gate)
        {
            var row = FindOwnedLocked();
            if (row is null)
            {
                notice = "Create a station and mount on your profile first.";
                return;
            }

            row.Live = true;
            broadcasting = true;
            id = row.Id;
            mount = row.Mount;
            ownedId = id;
            ApplyRelayLocked(row);
            notice = "Going live. Opening the Icecast mount and pushing from this phone.";
            SaveBookLocked();
        }

        if (!SignedIn)
        {
            lock (gate)
            {
                ApplyRelayLocked(FindOwnedLocked());
                notice = FindOwnedLocked()?.IngestUrl.Length > 0
                    ? "You are live on this phone. Sign in so Pearlgate can list the station for everyone."
                    : "Sign in on You so Pearlgate can create the live Icecast station.";
                SaveBookLocked();
            }

            return;
        }

        try
        {
            ApplyAuth();
            var start = await http.PostAsJsonAsync("radio/stations/" + Uri.EscapeDataString(id) + "/live",
                    new LiveStartBody(mount))
                .ConfigureAwait(false);
            if (!start.IsSuccessStatusCode)
            {
                lock (gate)
                {
                    ApplyRelayLocked(FindOwnedLocked());
                    notice = start.StatusCode == System.Net.HttpStatusCode.NotFound
                        ? (FindOwnedLocked()?.IngestUrl.Length > 0
                            ? "Pearlgate live route is down. Pushing to the Icecast URL we already have."
                            : "Pearlgate did not open the mount. Sign in, or set Icecast on DJ setup as a fallback.")
                        : "Pearlgate did not open the mount (" + (int)start.StatusCode + ").";
                    SaveBookLocked();
                }

                return;
            }

            var liveBody = await start.Content.ReadFromJsonAsync<LiveBody>().ConfigureAwait(false);
            lock (gate)
            {
                var row = FindOwnedLocked();
                if (row is not null)
                {
                    row.ListenUrl = liveBody?.ListenUrl ?? row.ListenUrl;
                    row.IngestUrl = liveBody?.IngestUrl ?? row.IngestUrl;
                    if (liveBody?.Mount is { Length: > 0 })
                    {
                        row.Mount = NormalizeMount(liveBody.Mount, row.Name);
                    }

                    ApplyRelayLocked(row);
                }

                notice = FindOwnedLocked()?.ListenUrl is { Length: > 0 }
                    ? "You are live. Anyone can tune this Icecast mount."
                    : "You are live. Waiting for a listen URL from Pearlgate or the Icecast host on DJ setup.";
                SaveBookLocked();
            }

            await RefreshAsync().ConfigureAwait(false);
        }
        catch (Exception)
        {
            lock (gate)
            {
                notice = "You are live on this phone. The Pearlgate relay is not reachable yet.";
            }
        }
    }

    private async Task EndLiveAsync()
    {
        string id;
        lock (gate)
        {
            var row = FindOwnedLocked();
            id = row?.Id ?? ownedId;
            if (row is not null)
            {
                row.Live = false;
            }

            broadcasting = false;
            notice = "You are off air. Your station stays on LIVE as offline.";
            SaveBookLocked();
        }

        try
        {
            ApplyAuth();
            if (id.Length > 0)
            {
                await http.DeleteAsync("radio/stations/" + Uri.EscapeDataString(id) + "/live").ConfigureAwait(false);
            }
        }
        catch (Exception)
        {
        }

        await RefreshAsync().ConfigureAwait(false);
    }

    private async Task<List<StationBody>> GetList(string path)
    {
        using var response = await http.GetAsync(path).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            return [];
        }

        return await response.Content.ReadFromJsonAsync<List<StationBody>>().ConfigureAwait(false) ?? [];
    }

    private void MergeRemote(List<StationBody> remote, bool owned)
    {
        for (var index = 0; index < remote.Count; index++)
        {
            var body = remote[index];
            var id = body.Id ?? string.Empty;
            if (id.Length == 0 ||
                string.Equals(id, "pearlgate-test", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(body.Mount, "pearlgate-test", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var existing = rows.FirstOrDefault(row => string.Equals(row.Id, id, StringComparison.OrdinalIgnoreCase));
            if (existing is null)
            {
                existing = new StationRow { Id = id };
                rows.Add(existing);
            }

            existing.Name = body.Name ?? existing.Name;
            existing.Host = body.Host ?? existing.Host;
            existing.Genre = body.Genre ?? existing.Genre;
            existing.Bio = body.Bio ?? existing.Bio;
            existing.ArtPath = string.IsNullOrWhiteSpace(body.ArtUrl) ? existing.ArtPath : body.ArtUrl;
            existing.Live = body.Live;
            existing.ListenUrl = body.ListenUrl ?? existing.ListenUrl;
            existing.IngestUrl = body.IngestUrl ?? existing.IngestUrl;
            existing.Mount = body.Mount is { Length: > 0 } ? NormalizeMount(body.Mount, existing.Name) : existing.Mount;
            existing.Listeners = body.Listeners;
            if (body.Likes is { } likes)
            {
                existing.Likes = Math.Max(0, likes);
                RememberLikeLocked(id, existing.Likes, body.Liked ?? existing.Liked);
            }

            if (body.Liked is { } liked)
            {
                existing.Liked = liked;
            }

            existing.Owned |= owned;
            if (owned)
            {
                ApplyRelayLocked(existing);
            }
        }
    }

    private void UpsertLocked(StationRow row)
    {
        rows.RemoveAll(item => string.Equals(item.Id, row.Id, StringComparison.OrdinalIgnoreCase));
        rows.Insert(0, row);
    }

    private StationRow? FindOwnedLocked()
    {
        if (ownedId.Length > 0)
        {
            var match = rows.FirstOrDefault(row => string.Equals(row.Id, ownedId, StringComparison.OrdinalIgnoreCase));
            if (match is not null)
            {
                return match;
            }
        }

        return rows.FirstOrDefault(static row => row.Owned);
    }

    private void LoadBook()
    {
        if (bookPath.Length == 0 || !File.Exists(bookPath))
        {
            return;
        }

        try
        {
            var book = JsonSerializer.Deserialize<StationBook>(File.ReadAllText(bookPath), Json);
            if (book is null)
            {
                return;
            }

            rows = book.Stations ?? new List<StationRow>();
            publicLikes.Clear();
            if (book.PublicLikes is { Count: > 0 })
            {
                foreach (var mark in book.PublicLikes)
                {
                    if (mark.Id is { Length: > 0 })
                    {
                        publicLikes[mark.Id] = new LikeMark { Count = Math.Max(0, mark.Count), Mine = mark.Mine };
                    }
                }
            }

            ownedId = book.OwnedId ?? string.Empty;
            broadcasting = rows.Any(row => row.Live && (row.Owned || string.Equals(row.Id, ownedId, StringComparison.Ordinal)));
        }
        catch (JsonException)
        {
        }
        catch (IOException)
        {
        }
    }

    private void SaveBookLocked()
    {
        if (bookPath.Length == 0)
        {
            return;
        }

        try
        {
            System.IO.Directory.CreateDirectory(Path.GetDirectoryName(bookPath) ?? ".");
            File.WriteAllText(bookPath, JsonSerializer.Serialize(new StationBook
            {
                OwnedId = ownedId,
                Stations = rows,
                PublicLikes = publicLikes.Select(static pair => new LikeRow
                {
                    Id = pair.Key,
                    Count = pair.Value.Count,
                    Mine = pair.Value.Mine,
                }).ToList(),
            }, Json));
        }
        catch (IOException)
        {
        }
    }

    private static CommunityStation[] Map(IEnumerable<StationRow> source) =>
        source.Select(static row => new CommunityStation(
            row.Id,
            row.Name.Length > 0 ? row.Name : "Station",
            row.Host,
            row.Genre,
            row.Live,
            row.ListenUrl,
            row.Listeners,
            row.Bio,
            row.ArtPath,
            row.Mount,
            row.Likes,
            row.Liked)).ToArray();

    private void ApplyAuth()
    {
        var bearer = token() ?? string.Empty;
        http.DefaultRequestHeaders.Authorization = bearer.Length > 0
            ? new AuthenticationHeaderValue("Bearer", bearer)
            : null;
    }

    private static void ApplyRemote(StationRow row, StationBody? body)
    {
        if (body is null)
        {
            return;
        }

        if (body.Name is { Length: > 0 })
        {
            row.Name = body.Name;
        }

        if (body.Host is { Length: > 0 })
        {
            row.Host = body.Host;
        }

        if (body.Genre is { Length: > 0 })
        {
            row.Genre = body.Genre;
        }

        if (body.Bio is not null)
        {
            row.Bio = body.Bio;
        }

        if (body.ArtUrl is { Length: > 0 })
        {
            row.ArtPath = body.ArtUrl;
        }

        if (body.ListenUrl is { Length: > 0 })
        {
            row.ListenUrl = body.ListenUrl;
        }

        if (body.IngestUrl is { Length: > 0 })
        {
            row.IngestUrl = body.IngestUrl;
        }

        if (body.Mount is { Length: > 0 })
        {
            row.Mount = NormalizeMount(body.Mount, row.Name);
        }

        if (body.Likes is { } likes)
        {
            row.Likes = Math.Max(0, likes);
        }

        if (body.Liked is { } liked)
        {
            row.Liked = liked;
        }
    }

    private static string BareStationId(string stationId)
    {
        var id = stationId.Trim();
        return id.StartsWith("live:", StringComparison.OrdinalIgnoreCase) ? id["live:".Length..] : id;
    }

    private int StationLikesLocked(string id)
    {
        var row = rows.FirstOrDefault(item => string.Equals(item.Id, id, StringComparison.OrdinalIgnoreCase));
        if (row is not null)
        {
            return Math.Max(0, row.Likes);
        }

        return publicLikes.TryGetValue(id, out var mark) ? Math.Max(0, mark.Count) : 0;
    }

    private bool StationLikedLocked(string id)
    {
        var row = rows.FirstOrDefault(item => string.Equals(item.Id, id, StringComparison.OrdinalIgnoreCase));
        if (row is not null)
        {
            return row.Liked;
        }

        return publicLikes.TryGetValue(id, out var mark) && mark.Mine;
    }

    private void ApplyLikeLocked(string id, bool liked, int count)
    {
        count = Math.Max(0, count);
        var row = rows.FirstOrDefault(item => string.Equals(item.Id, id, StringComparison.OrdinalIgnoreCase));
        if (row is not null)
        {
            row.Liked = liked;
            row.Likes = count;
        }

        RememberLikeLocked(id, count, liked);
    }

    private void RememberLikeLocked(string id, int count, bool mine)
    {
        publicLikes[id] = new LikeMark { Count = Math.Max(0, count), Mine = mine };
    }

    private async Task PushLikeAsync(string id, bool liked)
    {
        if (!SignedIn)
        {
            return;
        }

        try
        {
            ApplyAuth();
            var path = "radio/stations/" + Uri.EscapeDataString(id) + "/likes";
            using var response = liked
                ? await http.PostAsync(path, null).ConfigureAwait(false)
                : await http.DeleteAsync(path).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return;
            }

            var body = await response.Content.ReadFromJsonAsync<LikeBody>().ConfigureAwait(false);
            if (body is null)
            {
                return;
            }

            lock (gate)
            {
                ApplyLikeLocked(id, body.Liked ?? liked, body.Likes ?? StationLikesLocked(id));
                SaveBookLocked();
            }
        }
        catch (Exception)
        {
        }
    }

    private void ApplyRelayLocked(StationRow? row)
    {
        if (row is null || iceListenBase.Length == 0 || row.Mount.Length == 0)
        {
            return;
        }

        // Listen must match the Icecast mount the phone is already sourcing,
        // not a Pearlgate placeholder that has no audio.
        row.ListenUrl = JoinMount(iceListenBase, row.Mount);
        row.IngestUrl = WithSource(iceListenBase, iceUser, icePassword, row.Mount);
    }

    private static string JoinMount(string listenBase, string mount)
    {
        if (!TryIcecastRoot(listenBase, out var builder))
        {
            return string.Empty;
        }

        builder.UserName = string.Empty;
        builder.Password = string.Empty;
        builder.Path = "/" + mount.Trim('/');
        builder.Query = string.Empty;
        return builder.Uri.ToString();
    }

    private static string WithSource(string listenBase, string user, string password, string mount)
    {
        if (!TryIcecastRoot(listenBase, out var builder))
        {
            return string.Empty;
        }

        builder.UserName = user.Length > 0 ? user : "source";
        builder.Password = password;
        builder.Path = "/" + mount.Trim('/');
        builder.Query = string.Empty;
        return builder.Uri.ToString();
    }

    private static bool TryIcecastRoot(string listenBase, out UriBuilder builder)
    {
        var root = listenBase.Trim();
        if (root.Length == 0)
        {
            builder = new UriBuilder();
            return false;
        }

        if (!root.Contains("://", StringComparison.Ordinal))
        {
            root = "http://" + root;
        }

        if (!Uri.TryCreate(root, UriKind.Absolute, out var uri))
        {
            builder = new UriBuilder();
            return false;
        }

        builder = new UriBuilder(uri);
        if (uri.IsDefaultPort)
        {
            builder.Port = string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
                ? 443
                : 8000;
        }

        return true;
    }

    private static string NormalizeMount(string mount, string fallbackName)
    {
        var text = mount.Trim().Trim('/');
        if (text.Length == 0)
        {
            text = fallbackName;
        }

        var slug = new char[Math.Min(text.Length, 32)];
        var count = 0;
        var dash = false;
        var source = text.ToLowerInvariant();
        for (var index = 0; index < source.Length && count < slug.Length; index++)
        {
            var glyph = source[index];
            if (glyph is >= 'a' and <= 'z' or >= '0' and <= '9')
            {
                slug[count++] = glyph;
                dash = false;
                continue;
            }

            if (count > 0 && !dash)
            {
                slug[count++] = '-';
                dash = true;
            }
        }

        return count == 0 ? "live" : new string(slug, 0, count).Trim('-');
    }

    private sealed record CreateBody(string Name, string Genre, string Host, string Bio, string ClientId, string Mount);

    private sealed record LiveStartBody(string Mount);

    private sealed class StationBook
    {
        public string? OwnedId { get; set; }

        public List<StationRow>? Stations { get; set; }

        public List<LikeRow>? PublicLikes { get; set; }
    }

    private sealed class LikeRow
    {
        public string Id { get; set; } = string.Empty;

        public int Count { get; set; }

        public bool Mine { get; set; }
    }

    private sealed class LikeMark
    {
        public int Count { get; set; }

        public bool Mine { get; set; }
    }

    private sealed class LikeBody
    {
        [JsonPropertyName("likes")]
        public int? Likes { get; set; }

        [JsonPropertyName("liked")]
        public bool? Liked { get; set; }
    }

    private sealed class StationRow
    {
        public string Id { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        public string Host { get; set; } = string.Empty;

        public string Genre { get; set; } = string.Empty;

        public string Bio { get; set; } = string.Empty;

        public string ArtPath { get; set; } = string.Empty;

        public bool Live { get; set; }

        public string ListenUrl { get; set; } = string.Empty;

        public string IngestUrl { get; set; } = string.Empty;

        public string Mount { get; set; } = string.Empty;

        public int Listeners { get; set; }

        public int Likes { get; set; }

        public bool Liked { get; set; }

        public bool Owned { get; set; }
    }

    private sealed class StationBody
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("host")]
        public string? Host { get; set; }

        [JsonPropertyName("genre")]
        public string? Genre { get; set; }

        [JsonPropertyName("bio")]
        public string? Bio { get; set; }

        [JsonPropertyName("artUrl")]
        public string? ArtUrl { get; set; }

        [JsonPropertyName("live")]
        public bool Live { get; set; }

        [JsonPropertyName("listenUrl")]
        public string? ListenUrl { get; set; }

        [JsonPropertyName("ingestUrl")]
        public string? IngestUrl { get; set; }

        [JsonPropertyName("mount")]
        public string? Mount { get; set; }

        [JsonPropertyName("listeners")]
        public int Listeners { get; set; }

        [JsonPropertyName("likes")]
        public int? Likes { get; set; }

        [JsonPropertyName("liked")]
        public bool? Liked { get; set; }
    }

    private sealed class LiveBody
    {
        [JsonPropertyName("listenUrl")]
        public string? ListenUrl { get; set; }

        [JsonPropertyName("ingestUrl")]
        public string? IngestUrl { get; set; }

        [JsonPropertyName("mount")]
        public string? Mount { get; set; }
    }
}
