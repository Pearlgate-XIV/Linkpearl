using System.Text.Json;
using System.Text.Json.Serialization;
using Linkpearl.Audio;
using Linkpearl.Media;
using Linkpearl.Modules;

namespace Linkpearl.Applets.Life.Music;

internal enum MusicTab : byte
{
    Home = 0,
    Feed = 1,
    Search = 2,
    Library = 3,
    Profile = 4,
}

internal enum MusicPage : byte
{
    Tabs = 0,
    Onboard = 1,
    SetupListener = 2,
    SetupDj = 3,
    SetupVenue = 4,
    Unified = 5,
    Player = 6,
    DjDash = 7,
    VenueDash = 8,
    Roles = 9,
    Switcher = 10,
    EditProfile = 11,
    GenreList = 12,
    Playlist = 13,
    PickPlaylist = 14,
    PlacePhoto = 15,
    Settings = 16,
    PickPhoto = 17,
    FollowList = 18,
    Auth = 19,
    AuthLogin = 20,
    AuthCreate = 21,
}

internal enum MusicPhotoKind : byte
{
    Banner = 0,
    Face = 1,
}

internal enum MusicFeedPane : byte
{
    Radio = 0,
    Following = 1,
    Live = 2,
    Twitch = 3,
}

internal sealed class MusicState
{
    public bool Onboarded { get; set; }

    public string AccountId { get; set; } = string.Empty;

    public bool HasAccount => AccountId.Length > 0;

    public bool OnAuthSheet =>
        Page is MusicPage.Auth or MusicPage.AuthLogin or MusicPage.AuthCreate;

    [JsonIgnore]
    public MusicBook? Ledger { get; set; }

    [JsonIgnore]
    public string EnterHandle { get; set; } = string.Empty;

    [JsonIgnore]
    public string EnterSecret { get; set; } = string.Empty;

    [JsonIgnore]
    public string JoinName { get; set; } = string.Empty;

    [JsonIgnore]
    public string JoinTitle { get; set; } = string.Empty;

    [JsonIgnore]
    public string JoinHandle { get; set; } = string.Empty;

    [JsonIgnore]
    public string JoinSecret { get; set; } = string.Empty;

    [JsonIgnore]
    public string JoinAgain { get; set; } = string.Empty;

    [JsonIgnore]
    public string AuthNote { get; set; } = string.Empty;

    [JsonIgnore]
    public bool DropConfirm { get; set; }

    [JsonIgnore]
    public string DropSeatId { get; set; } = string.Empty;

    public bool Listener { get; set; } = true;

    public bool Dj { get; set; }

    public bool Venue { get; set; }

    public bool FollowNotifications { get; set; } = true;

    public string DisplayName { get; set; } = string.Empty;

    public string Honorific { get; set; } = string.Empty;

    public string Handle { get; set; } = string.Empty;

    public string DjName { get; set; } = string.Empty;

    public string StationName { get; set; } = string.Empty;

    public string StationId { get; set; } = string.Empty;

    public const int StationBioCap = 1000;

    public string StationBio { get; set; } = string.Empty;

    public string StationArtPath { get; set; } = string.Empty;

    public string ProfileBannerPath { get; set; } = string.Empty;

    public string ProfileFacePath { get; set; } = string.Empty;

    public bool UsesHandsetProfile { get; set; } = true;

    public bool UsesHandsetIdentity { get; set; } = true;

    public float BannerZoom { get; set; } = 1f;

    public float BannerFocusX { get; set; } = 0.5f;

    public float BannerFocusY { get; set; } = 0.5f;

    public float FaceZoom { get; set; } = 1f;

    public float FaceFocusX { get; set; } = 0.5f;

    public float FaceFocusY { get; set; } = 0.5f;

    public MusicPhotoKind Placing { get; set; }

    public string StationMount { get; set; } = string.Empty;

    public string IcecastHost { get; set; } = string.Empty;

    public string IcecastPassword { get; set; } = string.Empty;

    public string CaptureId { get; set; } = string.Empty;

    public string CaptureName { get; set; } = string.Empty;

    public string CaptureApp { get; set; } = "sound";

    public float CaptureGain { get; set; } = 0.4f;

    public float MonitorGain { get; set; } = 0.75f;

    public float StreamGain { get; set; } = 0.5f;

    public string Bio { get; set; } = string.Empty;

    public string VenueName { get; set; } = string.Empty;

    public string VenuePlace { get; set; } = string.Empty;

    public string Search { get; set; } = string.Empty;

    public List<string> Interests { get; } = new();

    public List<string> StationTags { get; } = new();

    public string StationTagDraft { get; set; } = string.Empty;

    public HashSet<string> Favorites { get; } = new(StringComparer.OrdinalIgnoreCase);

    public List<MusicStationMark> SavedRadio { get; } = new();

    public List<MusicStationMark> LikedRadio { get; } = new();

    public List<FollowedStationSnap> SavedLive { get; } = new();

    public HashSet<string> Following { get; } = new(StringComparer.OrdinalIgnoreCase);

    public List<FollowedStationSnap> FollowedStations { get; } = new();

    public List<MusicPlaylist> Playlists { get; } = new();

    public List<MusicStationMark> Queue { get; } = new();

    public int QueueIndex { get; set; }

    public string DraftName { get; set; } = string.Empty;

    public string ViewingPlaylistId { get; set; } = string.Empty;

    public MusicStationMark? Pending { get; set; }

    public string ViewingId { get; set; } = string.Empty;

    public string PeopleQuery { get; set; } = string.Empty;

    public MusicTab Tab { get; set; } = MusicTab.Home;

    public MusicFeedPane FeedPane { get; set; }

    public MusicPage Page { get; set; } = MusicPage.Onboard;

    public MusicPage ReturnTo { get; set; }

    public int GenreIndex { get; set; }

    public float Scroll { get; set; }

    public float SheetHeight { get; set; } = 1600f;

    [JsonIgnore]
    public string RevealStationId { get; set; } = string.Empty;

    [JsonIgnore]
    public string RevealStationTitle { get; set; } = string.Empty;

    [JsonIgnore]
    public bool ScrollRailHeld { get; set; }

    [JsonIgnore]
    public bool FollowListFollowers { get; set; }

    [JsonIgnore]
    public string FollowListOwnerId { get; set; } = string.Empty;

    public static readonly string[] Genres =
    {
        "Pop", "Hip Hop", "Rock", "Metal", "Electronic", "Bass", "Chill", "Country", "Latin",
    };

    public string Genre => Genres[Math.Clamp(GenreIndex, 0, Genres.Length - 1)];

    public string StationGenreLine
    {
        get
        {
            if (StationTags.Count == 0)
            {
                return FormatHashtag(Genre);
            }

            return string.Join("  ", StationTags.Select(FormatHashtag).Where(static tag => tag.Length > 0));
        }
    }

    [JsonIgnore]
    public string GenreTagDraft { get; set; } = string.Empty;

    [JsonIgnore]
    public string ReportKind { get; set; } = "radio_station";

    [JsonIgnore]
    public bool ReportOpen { get; set; }

    [JsonIgnore]
    public bool ReportFresh { get; set; }

    [JsonIgnore]
    public int ReportReason { get; set; }

    [JsonIgnore]
    public string ReportDetail { get; set; } = string.Empty;

    [JsonIgnore]
    public string ReportTarget { get; set; } = string.Empty;

    [JsonIgnore]
    public string ReportTitle { get; set; } = string.Empty;

    public static string NormalizeHashtag(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return string.Empty;
        }

        var text = raw.Trim();
        while (text.Length > 0 && text[0] == '#')
        {
            text = text[1..].Trim();
        }

        var slug = new char[Math.Min(text.Length, 24)];
        var count = 0;
        for (var index = 0; index < text.Length && count < slug.Length; index++)
        {
            var glyph = text[index];
            if (glyph is >= 'A' and <= 'Z')
            {
                slug[count++] = (char)(glyph + 32);
                continue;
            }

            if (glyph is >= 'a' and <= 'z' or >= '0' and <= '9')
            {
                slug[count++] = glyph;
            }
        }

        return count == 0 ? string.Empty : new string(slug, 0, count);
    }

    public static string FormatHashtag(string tag)
    {
        var slug = NormalizeHashtag(tag);
        return slug.Length == 0 ? string.Empty : "#" + slug;
    }

    public static string FormatGenreLine(string? raw)
    {
        var text = raw?.Trim() ?? string.Empty;
        if (text.Length == 0)
        {
            return string.Empty;
        }

        for (var index = 0; index < Genres.Length; index++)
        {
            if (string.Equals(Genres[index], text, StringComparison.OrdinalIgnoreCase))
            {
                return FormatHashtag(text);
            }
        }

        var parts = text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        var tags = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < parts.Length; index++)
        {
            var tag = FormatHashtag(parts[index]);
            if (tag.Length > 0 && seen.Add(tag))
            {
                tags.Add(tag);
            }
        }

        return string.Join("  ", tags);
    }

    private static void AddMarks(List<MusicStationMark> dest, MusicStationMark[]? rows)
    {
        if (rows is not { Length: > 0 })
        {
            return;
        }

        foreach (var row in rows)
        {
            if (row is { Id.Length: > 0 })
            {
                dest.RemoveAll(item => string.Equals(item.Id, row.Id, StringComparison.OrdinalIgnoreCase));
                dest.Add(row);
            }
        }
    }

    private static void NormalizeTagList(List<string> tags)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (var index = tags.Count - 1; index >= 0; index--)
        {
            var slug = NormalizeHashtag(tags[index]);
            if (slug.Length == 0 || !seen.Add(slug))
            {
                tags.RemoveAt(index);
                continue;
            }

            tags[index] = slug;
        }
    }

    public bool TryAddHashtag(string? raw) => TryAddTag(Interests, raw, profile: true);

    public bool TryAddStationTag(string? raw) => TryAddTag(StationTags, raw, profile: false);

    public bool TryAddTag(List<string> tags, string? raw, bool profile)
    {
        var slug = NormalizeHashtag(raw);
        if (slug.Length == 0)
        {
            return false;
        }

        for (var index = 0; index < tags.Count; index++)
        {
            if (string.Equals(NormalizeHashtag(tags[index]), slug, StringComparison.Ordinal))
            {
                if (profile)
                {
                    GenreTagDraft = string.Empty;
                }
                else
                {
                    StationTagDraft = string.Empty;
                }

                return false;
            }
        }

        tags.Add(slug);
        if (profile)
        {
            GenreTagDraft = string.Empty;
        }
        else
        {
            StationTagDraft = string.Empty;
        }

        return true;
    }

    public static string ClampStationBio(string? raw)
    {
        var text = raw ?? string.Empty;
        return text.Length <= StationBioCap ? text : text[..StationBioCap];
    }

    public static string NormalizeCapture(string? app) =>
        string.Equals(app, "mic", StringComparison.OrdinalIgnoreCase) ? "mic" : "sound";

    public static string SlugMount(string value)
    {
        var text = value.Trim().ToLowerInvariant();
        if (text.StartsWith('/'))
        {
            text = text[1..];
        }

        var slug = new char[Math.Min(text.Length, 32)];
        var count = 0;
        var dash = false;
        for (var index = 0; index < text.Length && count < slug.Length; index++)
        {
            var glyph = text[index];
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

        return count == 0 ? string.Empty : new string(slug, 0, count).Trim('-');
    }

    public static MusicState Load(HostPaths paths, string fallbackName)
    {
        var state = new MusicState();
        var path = paths.State("music.json");
        if (File.Exists(path))
        {
            try
            {
                var dto = JsonSerializer.Deserialize<MusicSave>(File.ReadAllText(path));
                if (dto is not null)
                {
                    state.Onboarded = dto.Onboarded;
                    state.AccountId = dto.AccountId ?? string.Empty;
                    state.Listener = dto.Listener;
                    state.Dj = dto.Dj;
                    state.Venue = dto.Venue;
                    state.FollowNotifications = dto.FollowNotifications ?? true;
                    state.DisplayName = dto.DisplayName ?? string.Empty;
                    state.Honorific = dto.Honorific ?? string.Empty;
                    state.Handle = dto.Handle ?? string.Empty;
                    state.DjName = dto.DjName ?? string.Empty;
                    state.StationName = dto.StationName ?? string.Empty;
                    state.StationId = dto.StationId ?? string.Empty;
                    state.StationBio = ClampStationBio(dto.StationBio);
                    state.StationArtPath = dto.StationArtPath ?? string.Empty;
                    state.ProfileBannerPath = dto.ProfileBannerPath ?? string.Empty;
                    state.ProfileFacePath = dto.ProfileFacePath ?? string.Empty;
                    state.UsesHandsetProfile = dto.UsesHandsetProfile ??
                        (string.IsNullOrWhiteSpace(dto.ProfileFacePath) &&
                         string.IsNullOrWhiteSpace(dto.ProfileBannerPath));
                    state.UsesHandsetIdentity = dto.UsesHandsetIdentity ?? state.UsesHandsetProfile;
                    state.BannerZoom = dto.BannerZoom > 0f ? dto.BannerZoom : 1f;
                    state.BannerFocusX = dto.BannerFocusX == 0f && dto.BannerFocusY == 0f ? 0.5f : dto.BannerFocusX;
                    state.BannerFocusY = dto.BannerFocusX == 0f && dto.BannerFocusY == 0f ? 0.5f : dto.BannerFocusY;
                    state.FaceZoom = dto.FaceZoom > 0f ? dto.FaceZoom : 1f;
                    state.FaceFocusX = dto.FaceFocusX == 0f && dto.FaceFocusY == 0f ? 0.5f : dto.FaceFocusX;
                    state.FaceFocusY = dto.FaceFocusX == 0f && dto.FaceFocusY == 0f ? 0.5f : dto.FaceFocusY;
                    state.StationMount = SlugMount(dto.StationMount ?? string.Empty);
                    state.IcecastHost = (dto.IcecastHost ?? string.Empty).Trim();
                    state.IcecastPassword = dto.IcecastPassword ?? string.Empty;
                    state.CaptureId = dto.CaptureId ?? string.Empty;
                    state.CaptureName = dto.CaptureName ?? string.Empty;
                    state.CaptureApp = NormalizeCapture(dto.CaptureApp);
                    if (dto.CaptureGain is { } capture)
                    {
                        state.CaptureGain = Math.Clamp(capture, 0f, 1f);
                    }

                    if (dto.MonitorGain is { } cue)
                    {
                        state.MonitorGain = Math.Clamp(cue, 0f, 1f);
                    }

                    if (dto.StreamGain is { } stream)
                    {
                        state.StreamGain = Math.Clamp(stream, 0f, 1f);
                    }

                    state.Bio = dto.Bio ?? string.Empty;
                    state.VenueName = dto.VenueName ?? string.Empty;
                    state.VenuePlace = dto.VenuePlace ?? string.Empty;
                    state.GenreIndex = Math.Clamp(dto.GenreIndex, 0, Genres.Length - 1);
                    if (dto.Interests is { Length: > 0 })
                    {
                        state.Interests.AddRange(dto.Interests);
                    }

                    if (dto.StationTags is { Length: > 0 })
                    {
                        state.StationTags.AddRange(dto.StationTags);
                    }

                    if (dto.Favorites is { Length: > 0 })
                    {
                        foreach (var id in dto.Favorites)
                        {
                            if (!string.IsNullOrWhiteSpace(id))
                            {
                                state.Favorites.Add(id.Trim());
                            }
                        }
                    }

                    AddMarks(state.SavedRadio, dto.SavedRadio);
                    AddMarks(state.LikedRadio, dto.LikedRadio);
                    if (dto.SavedLive is { Length: > 0 })
                    {
                        foreach (var snap in dto.SavedLive)
                        {
                            if (snap is { Id.Length: > 0 })
                            {
                                state.RememberSavedLive(snap);
                            }
                        }
                    }

                    if (dto.Following is { Length: > 0 })
                    {
                        foreach (var id in dto.Following)
                        {
                            if (!string.IsNullOrWhiteSpace(id))
                            {
                                state.Following.Add(id.Trim());
                            }
                        }
                    }

                    if (dto.FollowedStations is { Length: > 0 })
                    {
                        foreach (var row in dto.FollowedStations)
                        {
                            if (row is { Id.Length: > 0 })
                            {
                                state.RememberFollowed(row);
                            }
                        }
                    }

                    if (dto.Playlists is { Length: > 0 })
                    {
                        foreach (var row in dto.Playlists)
                        {
                            var list = MusicPlaylist.FromSave(row);
                            if (list is not null)
                            {
                                state.Playlists.Add(list);
                            }
                        }
                    }
                }
            }
            catch (JsonException)
            {
            }
            catch (IOException)
            {
            }
        }

        if (state.HasAccount)
        {
            if (state.DisplayName.Length == 0)
            {
                state.DisplayName = fallbackName.Length > 0 ? fallbackName : "Listener";
            }

            if (state.Handle.Length == 0)
            {
                state.Handle = "@" + state.DisplayName.Replace(" ", string.Empty, StringComparison.Ordinal)
                    .ToLowerInvariant();
            }
        }

        NormalizeTagList(state.Interests);
        NormalizeTagList(state.StationTags);

        state.Page = !state.HasAccount
            ? MusicPage.Auth
            : state.Onboarded
                ? MusicPage.Tabs
                : MusicPage.Onboard;
        return state;
    }

    public void Save(HostPaths paths)
    {
        try
        {
            var path = paths.State("music.json");
            Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");
            File.WriteAllText(path, JsonSerializer.Serialize(new MusicSave
            {
                Onboarded = Onboarded,
                AccountId = AccountId,
                Listener = Listener,
                Dj = Dj,
                Venue = Venue,
                FollowNotifications = FollowNotifications,
                DisplayName = DisplayName,
                Honorific = Honorific,
                Handle = Handle,
                DjName = DjName,
                StationName = StationName,
                StationId = StationId,
                StationBio = StationBio,
                StationArtPath = StationArtPath,
                ProfileBannerPath = ProfileBannerPath,
                ProfileFacePath = ProfileFacePath,
                UsesHandsetProfile = UsesHandsetProfile,
                UsesHandsetIdentity = UsesHandsetIdentity,
                BannerZoom = BannerZoom,
                BannerFocusX = BannerFocusX,
                BannerFocusY = BannerFocusY,
                FaceZoom = FaceZoom,
                FaceFocusX = FaceFocusX,
                FaceFocusY = FaceFocusY,
                StationMount = StationMount,
                IcecastHost = IcecastHost,
                IcecastPassword = IcecastPassword,
                CaptureId = CaptureId,
                CaptureName = CaptureName,
                CaptureApp = CaptureApp,
                CaptureGain = CaptureGain,
                MonitorGain = MonitorGain,
                StreamGain = StreamGain,
                Bio = Bio,
                VenueName = VenueName,
                VenuePlace = VenuePlace,
                GenreIndex = GenreIndex,
                Interests = Interests.ToArray(),
                StationTags = StationTags.ToArray(),
                Favorites = Favorites.ToArray(),
                SavedRadio = SavedRadio.ToArray(),
                LikedRadio = LikedRadio.ToArray(),
                SavedLive = SavedLive.ToArray(),
                Following = Following.ToArray(),
                FollowedStations = FollowedStations.ToArray(),
                Playlists = Playlists.Select(static row => row.ToSave()).ToArray(),
            }));
        }
        catch (IOException)
        {
        }

        if (Ledger is not null && HasAccount)
        {
            Ledger.Keep(this);
            Ledger.Save(paths);
        }
    }

    public void Sit(MusicBook book)
    {
        Ledger = book;
        if (HasAccount && !book.Holds(AccountId))
        {
            AccountId = string.Empty;
        }

        if (!HasAccount)
        {
            Page = MusicPage.Auth;
        }
    }

    public void EnterSeat(MusicSeat seat)
    {
        AccountId = seat.Id;
        seat.Face.Apply(this);
        if (DisplayName.Length == 0)
        {
            DisplayName = seat.DisplayName;
        }

        if (Handle.Length == 0)
        {
            Handle = seat.Handle;
        }

        ClearAuthDrafts();
        Page = Onboarded ? MusicPage.Tabs : MusicPage.Onboard;
        Scroll = 0f;
    }

    public void ClearSession()
    {
        AccountId = string.Empty;
        Onboarded = false;
        Listener = true;
        Dj = false;
        Venue = false;
        DisplayName = string.Empty;
        Honorific = string.Empty;
        Handle = string.Empty;
        Bio = string.Empty;
        DjName = string.Empty;
        StationName = string.Empty;
        StationId = string.Empty;
        StationBio = string.Empty;
        StationArtPath = string.Empty;
        ProfileFacePath = string.Empty;
        ProfileBannerPath = string.Empty;
        FaceZoom = 1f;
        FaceFocusX = 0.5f;
        FaceFocusY = 0.5f;
        BannerZoom = 1f;
        BannerFocusX = 0.5f;
        BannerFocusY = 0.5f;
        UsesHandsetProfile = true;
        UsesHandsetIdentity = true;
        VenueName = string.Empty;
        VenuePlace = string.Empty;
        Interests.Clear();
        StationTags.Clear();
        DropConfirm = false;
        DropSeatId = string.Empty;
        ClearAuthDrafts();
        Page = MusicPage.Auth;
        ReturnTo = MusicPage.Auth;
        Tab = MusicTab.Home;
        Scroll = 0f;
    }

    public void ClearAuthDrafts()
    {
        EnterHandle = string.Empty;
        EnterSecret = string.Empty;
        JoinName = string.Empty;
        JoinTitle = string.Empty;
        JoinHandle = string.Empty;
        JoinSecret = string.Empty;
        JoinAgain = string.Empty;
        AuthNote = string.Empty;
    }

    public void AdjustPlacing(float zoom, float focusX, float focusY)
    {
        zoom = Math.Clamp(zoom, CoverFit.PlaceZoomMin, CoverFit.PlaceZoomMax);
        if (Placing == MusicPhotoKind.Face)
        {
            FaceZoom = zoom;
            FaceFocusX = focusX;
            FaceFocusY = focusY;
            return;
        }

        BannerZoom = zoom;
        BannerFocusX = focusX;
        BannerFocusY = focusY;
    }

    public void Open(MusicPage page)
    {
        ReturnTo = Page;
        Page = page;
        Scroll = 0f;
        SheetHeight = 1600f;
    }

    public void Back()
    {
        Page = ReturnTo == Page ? MusicPage.Tabs : ReturnTo;
        ReturnTo = MusicPage.Tabs;
        Scroll = 0f;
        SheetHeight = 1600f;
    }

    public void OpenProfile(string id)
    {
        ViewingId = id;
        Open(MusicPage.Unified);
    }

    public bool ToggleFollow(string id)
    {
        if (id.Length == 0)
        {
            return false;
        }

        if (!Following.Remove(id))
        {
            Following.Add(id);
            return true;
        }

        return false;
    }

    public static string BareStationId(string? id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return string.Empty;
        }

        var text = id.Trim();
        return text.StartsWith("live:", StringComparison.OrdinalIgnoreCase) ? text["live:".Length..] : text;
    }

    public bool FollowsStationId(string id)
    {
        var bare = BareStationId(id);
        return bare.Length > 0 &&
               (Following.Contains(bare) || Following.Contains("live:" + bare) || Following.Contains(id));
    }

    public bool FollowsProfile(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return false;
        }

        return Following.Contains(id) || FollowsStationId(id);
    }

    public static int PublicFollowers(string id, bool youFollow)
    {
        var bare = BareStationId(id);
        if (bare.Length == 0)
        {
            return youFollow ? 1 : 0;
        }

        return 8 + Math.Abs(Hash(bare)) % 72 + (youFollow ? 1 : 0);
    }

    public static int PublicFollowing(string id)
    {
        var bare = BareStationId(id);
        if (bare.Length == 0)
        {
            return 0;
        }

        return 4 + Math.Abs(Hash(bare + "/out")) % 32;
    }

    private static int Hash(string text) => StringComparer.OrdinalIgnoreCase.GetHashCode(text);

    public int FollowingCount()
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var id in Following)
        {
            var bare = BareStationId(id);
            if (bare.Length > 0)
            {
                seen.Add(bare);
            }
        }

        return seen.Count;
    }

    public bool ToggleFollowStation(FollowedStationSnap snap)
    {
        var bare = BareStationId(snap.Id);
        if (bare.Length == 0)
        {
            return false;
        }

        var liveId = "live:" + bare;
        if (FollowsStationId(bare))
        {
            Following.Remove(bare);
            Following.Remove(liveId);
            ForgetFollowed(bare);
            return false;
        }

        Following.Add(bare);
        Following.Add(liveId);
        snap.Id = bare;
        RememberFollowed(snap);
        return true;
    }

    public void RememberFollowed(FollowedStationSnap snap)
    {
        var bare = BareStationId(snap.Id);
        if (bare.Length == 0)
        {
            return;
        }

        snap.Id = bare;
        for (var index = 0; index < FollowedStations.Count; index++)
        {
            if (string.Equals(FollowedStations[index].Id, bare, StringComparison.OrdinalIgnoreCase))
            {
                FollowedStations[index] = snap;
                return;
            }
        }

        FollowedStations.Add(snap);
    }

    public void ForgetFollowed(string id)
    {
        var bare = BareStationId(id);
        FollowedStations.RemoveAll(row => string.Equals(row.Id, bare, StringComparison.OrdinalIgnoreCase));
    }

    public bool IsFavorite(string id)
    {
        var bare = BareStationId(id);
        return Favorites.Contains(id) || (bare.Length > 0 && Favorites.Contains(bare));
    }

    public void ToggleFavorite(string id)
    {
        var key = BareStationId(id);
        if (key.Length == 0)
        {
            key = id.Trim();
        }

        if (key.Length == 0)
        {
            return;
        }

        if (Favorites.Remove(key) | Favorites.Remove(id))
        {
            ForgetSavedRadio(key);
            ForgetSavedLive(key);
            return;
        }

        Favorites.Add(key);
    }

    public void RememberSavedRadio(MusicStationMark mark)
    {
        if (mark.Id.Length == 0)
        {
            return;
        }

        ForgetSavedRadio(mark.Id);
        SavedRadio.Add(mark);
    }

    public void ForgetSavedRadio(string id)
    {
        var bare = BareStationId(id);
        SavedRadio.RemoveAll(row =>
            string.Equals(row.Id, id, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(row.Id, bare, StringComparison.OrdinalIgnoreCase));
    }

    public void RememberLikedRadio(MusicStationMark mark)
    {
        if (mark.Id.Length == 0)
        {
            return;
        }

        ForgetLikedRadio(mark.Id);
        LikedRadio.Add(mark);
    }

    public void ForgetLikedRadio(string id)
    {
        var bare = BareStationId(id);
        LikedRadio.RemoveAll(row =>
            string.Equals(row.Id, id, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(row.Id, bare, StringComparison.OrdinalIgnoreCase));
    }

    public void RememberSavedLive(FollowedStationSnap snap)
    {
        var bare = BareStationId(snap.Id);
        if (bare.Length == 0)
        {
            return;
        }

        snap.Id = bare;
        ForgetSavedLive(bare);
        SavedLive.Add(snap);
    }

    public void ForgetSavedLive(string id)
    {
        var bare = BareStationId(id);
        SavedLive.RemoveAll(row =>
            string.Equals(row.Id, id, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(row.Id, bare, StringComparison.OrdinalIgnoreCase));
    }

    public MusicPlaylist? FindPlaylist(string id) =>
        Playlists.FirstOrDefault(row => string.Equals(row.Id, id, StringComparison.OrdinalIgnoreCase));

    public MusicPlaylist CreatePlaylist(string name)
    {
        var title = name.Trim();
        var list = new MusicPlaylist
        {
            Id = Guid.NewGuid().ToString("N"),
            Name = title.Length > 0 ? title : "Playlist " + (Playlists.Count + 1),
        };
        Playlists.Add(list);
        DraftName = string.Empty;
        return list;
    }

    public void AddPending(string playlistId)
    {
        if (Pending is null)
        {
            return;
        }

        FindPlaylist(playlistId)?.Put(Pending);
        Pending = null;
    }

    public void ArmQueue(IReadOnlyList<MusicStationMark> stations, int index)
    {
        Queue.Clear();
        Queue.AddRange(stations);
        QueueIndex = Queue.Count == 0 ? 0 : Math.Clamp(index, 0, Queue.Count - 1);
    }

    public void ClearQueue()
    {
        Queue.Clear();
        QueueIndex = 0;
    }

    public MusicStationMark? StepQueue(int delta)
    {
        if (Queue.Count == 0)
        {
            return null;
        }

        QueueIndex = (QueueIndex + delta + Queue.Count) % Queue.Count;
        return Queue[QueueIndex];
    }

    private sealed class MusicSave
    {
        public bool Onboarded { get; set; }

        public string? AccountId { get; set; }

        public bool Listener { get; set; }

        public bool Dj { get; set; }

        public bool Venue { get; set; }

        public bool? FollowNotifications { get; set; }

        public string? DisplayName { get; set; }

        public string? Honorific { get; set; }

        public string? Handle { get; set; }

        public string? DjName { get; set; }

        public string? StationName { get; set; }

        public string? StationId { get; set; }

        public string? StationBio { get; set; }

        public string? StationArtPath { get; set; }

        public string? ProfileBannerPath { get; set; }

        public string? ProfileFacePath { get; set; }

        public bool? UsesHandsetProfile { get; set; }

        public bool? UsesHandsetIdentity { get; set; }

        public float BannerZoom { get; set; } = 1f;

        public float BannerFocusX { get; set; } = 0.5f;

        public float BannerFocusY { get; set; } = 0.5f;

        public float FaceZoom { get; set; } = 1f;

        public float FaceFocusX { get; set; } = 0.5f;

        public float FaceFocusY { get; set; } = 0.5f;

        public string? StationMount { get; set; }

        public string? IcecastHost { get; set; }

        public string? IcecastPassword { get; set; }

        public string? CaptureId { get; set; }

        public string? CaptureName { get; set; }

        public string? CaptureApp { get; set; }

        public float? CaptureGain { get; set; }

        public float? MonitorGain { get; set; }

        public float? StreamGain { get; set; }

        public string? Bio { get; set; }

        public string? VenueName { get; set; }

        public string? VenuePlace { get; set; }

        public int GenreIndex { get; set; }

        public string[]? Interests { get; set; }

        public string[]? StationTags { get; set; }

        public string[]? Favorites { get; set; }

        public MusicStationMark[]? SavedRadio { get; set; }

        public MusicStationMark[]? LikedRadio { get; set; }

        public FollowedStationSnap[]? SavedLive { get; set; }

        public string[]? Following { get; set; }

        public FollowedStationSnap[]? FollowedStations { get; set; }

        public PlaylistSave[]? Playlists { get; set; }
    }
}

internal sealed class FollowedStationSnap
{
    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Host { get; set; } = string.Empty;

    public string Genre { get; set; } = string.Empty;

    public string Bio { get; set; } = string.Empty;

    public string ArtPath { get; set; } = string.Empty;

    public string ListenUrl { get; set; } = string.Empty;

    public string Mount { get; set; } = string.Empty;

    public string WatchUrl { get; set; } = string.Empty;

    public string TwitchLogin { get; set; } = string.Empty;

    public string VenueLine { get; set; } = string.Empty;

    public string Lifestream { get; set; } = string.Empty;

    public string Source { get; set; } = string.Empty;

    public CommunityStation ToStation(bool live, int listeners = 0, int likes = 0, bool liked = false,
        int viewers = 0) =>
        new(Id, Name.Length > 0 ? Name : "Station", Host, Genre, live, ListenUrl, listeners, Bio, ArtPath, Mount, likes,
            liked, WatchUrl, viewers, TwitchLogin, VenueLine, Lifestream, Source);

    public static FollowedStationSnap From(CommunityStation station) =>
        new()
        {
            Id = MusicState.BareStationId(station.Id),
            Name = station.Name,
            Host = station.Host,
            Genre = station.Genre,
            Bio = station.Bio,
            ArtPath = station.ArtPath,
            ListenUrl = station.ListenUrl,
            Mount = station.Mount,
            WatchUrl = station.WatchUrl,
            TwitchLogin = station.TwitchLogin,
            VenueLine = station.VenueLine,
            Lifestream = station.Lifestream,
            Source = station.Source,
        };
}

internal sealed class MusicStationMark
{
    public string Id { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string Genre { get; set; } = string.Empty;

    public string Place { get; set; } = string.Empty;

    public string StreamUrl { get; set; } = string.Empty;

    public int Bitrate { get; set; }

    public string ArtUrl { get; set; } = string.Empty;

    public string AlternateUrl { get; set; } = string.Empty;

    public int Listeners { get; set; }

    public int Votes { get; set; }

    public PublicStation ToPublic() =>
        new(Id, Title, Genre, Place, StreamUrl, Bitrate, ArtUrl, AlternateUrl, Listeners, Votes);

    public static MusicStationMark From(PublicStation station) =>
        new()
        {
            Id = station.Id,
            Title = station.Title,
            Genre = station.Genre,
            Place = station.Place,
            StreamUrl = station.StreamUrl,
            Bitrate = station.Bitrate,
            ArtUrl = station.ArtUrl,
            AlternateUrl = station.AlternateUrl,
            Listeners = station.Listeners,
            Votes = station.Votes,
        };
}

internal sealed class MusicPlaylist
{
    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public List<MusicStationMark> Stations { get; } = new();

    public void Put(MusicStationMark mark)
    {
        if (mark.Id.Length == 0)
        {
            return;
        }

        for (var index = 0; index < Stations.Count; index++)
        {
            if (string.Equals(Stations[index].Id, mark.Id, StringComparison.OrdinalIgnoreCase))
            {
                Stations[index] = mark;
                return;
            }
        }

        Stations.Add(mark);
    }

    public bool Drop(string stationId) =>
        Stations.RemoveAll(row => string.Equals(row.Id, stationId, StringComparison.OrdinalIgnoreCase)) > 0;

    public PlaylistSave ToSave() =>
        new()
        {
            Id = Id,
            Name = Name,
            Stations = Stations.ToArray(),
        };

    public static MusicPlaylist? FromSave(PlaylistSave? row)
    {
        if (row is null || string.IsNullOrWhiteSpace(row.Id))
        {
            return null;
        }

        var list = new MusicPlaylist
        {
            Id = row.Id.Trim(),
            Name = string.IsNullOrWhiteSpace(row.Name) ? "Playlist" : row.Name.Trim(),
        };
        if (row.Stations is { Length: > 0 })
        {
            foreach (var mark in row.Stations)
            {
                if (mark is { Id.Length: > 0 })
                {
                    list.Stations.Add(mark);
                }
            }
        }

        return list;
    }
}

internal sealed class PlaylistSave
{
    public string? Id { get; set; }

    public string? Name { get; set; }

    public MusicStationMark[]? Stations { get; set; }
}
