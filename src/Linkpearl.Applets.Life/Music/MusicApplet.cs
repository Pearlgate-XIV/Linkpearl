using System.Linq;
using Linkpearl.Applets;
using Linkpearl.Applets.Life.Venues;
using Linkpearl.Audio;
using Linkpearl.Badges;
using Linkpearl.Feedback;
using Linkpearl.Geometry;
using Linkpearl.Layout;
using Linkpearl.Media;
using Linkpearl.Modules;
using Linkpearl.Net;
using Linkpearl.Notices;
using Linkpearl.Painting;
using Linkpearl.Platform;
using Linkpearl.Preferences;
using Linkpearl.Time;

namespace Linkpearl.Applets.Life.Music;

public sealed partial class MusicApplet : IApplet, IHandsetProfileSink, IStationMarks, IEchoMixStation
{
    public static readonly AppletManifest Manifest = new()
    {
        Id = "music",
        DisplayNameKey = "Music",
        Family = AppletFamily.Media,
        Glyph = "♫",
        HomeOrder = 8,
        Capabilities = AppletCapabilities.PlaysAudio | AppletCapabilities.BackgroundWork,
    };

    private static readonly string[] Tabs = { "Home", "Feed", "Search", "Library", "You" };
    private static readonly string[] TabMarks = { "⌂", "▤", "⌕", "|||", "●" };
    private static readonly string?[] TabGlyphs =
        { "music-home.png", "music-feed.png", "music-search.png", null, "music-profile.png" };

    private readonly IGameSession game;
    private readonly HostPaths paths;
    private readonly DisplayPreferences display;
    private readonly IHandsetAudio audio;
    private readonly IPublicRadio publicRadio;
    private readonly ICommunityRadio community;
    private readonly IPearlHub pearl;
    private readonly IFilePicker files;
    private readonly BadgeBook badges;
    private readonly IBroadcastSense sense;
    private readonly IBroadcastPush push;
    private readonly IAudioPorts ports;
    private readonly IFeedbackDesk desk;
    private readonly HostEnvironment environment;
    private readonly INoticeTray notices;
    private readonly IClock clock;
    private readonly IEchoMixBooth booth;
    private readonly IStreamDesk streams;
    private readonly ILifestream lifestream;
    private readonly HashSet<string> liveSeen = new(StringComparer.OrdinalIgnoreCase);
    private bool liveSeeded;
    private readonly MusicState state;
    private readonly MusicBook book;
    private int profileStamp;
    private float nameClock;
    private long lastCaptureTry;
    private long lastPushTry;
    private long lastCommunityRefresh;
    private long lastFollowWatch;
    private long lastPortScan;
    private long mergedBoardAt;
    private CommunityStation[] mergedBoard = [];
    private long twitchBoardAt;
    private CommunityStation[] twitchBoard = [];
    private long broadcastBoardAt;
    private CommunityStation[] broadcastBoard = [];
    private long followedBoardAt;
    private CommunityStation[] followedBoard = [];
    private string lastSoundTap = string.Empty;
    private bool photoDrag;
    private string playerWaveId = string.Empty;
    private float playerWaveScroll;
    private float playerPlayed;
    private bool playerVolumeDrag;
    private ImagePick imagePick;

    private enum ImagePick : byte
    {
        None = 0,
        Profile = 1,
        StationArt = 2,
    }

    public MusicApplet(IGameSession game, HostPaths paths, DisplayPreferences display, IHandsetAudio audio,
        IPublicRadio publicRadio, ICommunityRadio community, IPearlHub pearl, IFilePicker files, BadgeBook badges,
        IBroadcastSense sense, IBroadcastPush push, IAudioPorts ports, IFeedbackDesk desk,
        HandsetProfileDesk profiles, HostEnvironment environment, INoticeTray notices, IClock clock,
        IFrameLoop frames, IEchoMixBooth booth, IStreamDesk streams, ILifestream lifestream)
    {
        this.game = game;
        this.paths = paths;
        this.display = display;
        this.audio = audio;
        this.publicRadio = publicRadio;
        this.community = community;
        this.pearl = pearl;
        this.files = files;
        this.badges = badges;
        this.sense = sense;
        this.push = push;
        this.ports = ports;
        this.desk = desk;
        this.environment = environment;
        this.notices = notices;
        this.clock = clock;
        this.booth = booth;
        this.streams = streams;
        this.lifestream = lifestream;
        state = MusicState.Load(paths, game.Character.Name);
        book = MusicBook.Load(paths);
        state.Sit(book);
        if (state.HasAccount)
        {
            FillFromCharacter();
            if (state.UsesHandsetIdentity || state.UsesHandsetProfile)
            {
                AcceptHandsetProfile(HandsetName(), HandsetLook.Honorific(display));
            }
        }
        else if (state.JoinName.Length == 0)
        {
            state.JoinName = HandsetName();
        }

        profiles.Add(this);

        sense.CapturedPcm += push.WritePcm;
        sense.RefreshPoints();
        if (state.CaptureId.Length > 0)
        {
            sense.Select(state.CaptureId);
        }

        publicRadio.Ensure(state.Genre);
        WarmStations();
        if (state.StationName.Length > 0)
        {
            PublishStation();
        }

        community.Refresh();
        streams.Refresh();
        frames.Tick += OnFrame;
    }

    private string HandsetName()
    {
        var linked = ShownName.Linked(game.Character.Name, pearl.Current.MeName);
        var name = HandsetLook.Name(display, linked,
            GlassName.IsPatron(badges, pearl.Current, display, environment.IsDevelopment));
        return name.Length > 0 ? name : game.Character.Name;
    }

    private void FillFromCharacter()
    {
        var name = game.Character.Name.Trim();
        if (name.Length == 0)
        {
            return;
        }

        if (state.DisplayName.Length == 0)
        {
            state.DisplayName = name;
        }

        if (state.Handle.Length <= 1)
        {
            state.Handle = "@" + state.DisplayName.Replace(" ", string.Empty, StringComparison.Ordinal).ToLowerInvariant();
        }
    }

    private string SharedName()
    {
        var linked = ShownName.Linked(game.Character.Name, pearl.Current.MeName);
        return ShownName.Preferred(display, linked, state.DisplayName, "Listener", FancyName());
    }

    private string EditableName()
    {
        var linked = ShownName.Linked(game.Character.Name, pearl.Current.MeName);
        var handset = ShownName.Source(display, linked, FancyName());
        var stored = state.DisplayName.Trim();
        if (stored.Length > 0)
        {
            return stored;
        }

        return handset.Length > 0 ? handset : "Listener";
    }

    private string SharedHonorific() => state.Honorific.Trim();

    private bool FancyName() =>
        GlassName.IsPatron(badges, pearl.Current, display, environment.IsDevelopment);

    private void DrawFlowName(in AppletFrame frame, Rect area, string name) =>
        NameMark.DrawName(frame, area, name, display, MusicChrome.Ink, FancyName(), nameClock);

    private string MarkedName()
    {
        var name = SharedName();
        var honor = SharedHonorific();
        return honor.Length > 0 ? honor + " " + name : name;
    }

    private void SetSharedName(string value)
    {
        if (value == EditableName())
        {
            return;
        }

        state.DisplayName = value;
    }

    private void SetSharedHonorific(string value)
    {
        var next = ShownName.ClampTitle(value);
        if (next == SharedHonorific())
        {
            return;
        }

        state.Honorific = next;
    }

    public void AcceptHandsetProfile(string name, string honorific)
    {
        var shown = name.Trim();
        if (shown.Length == 0)
        {
            shown = HandsetName();
        }

        if (shown.Length > 0)
        {
            if (state.DjName.Length == 0 ||
                string.Equals(state.DjName, state.DisplayName, StringComparison.Ordinal))
            {
                state.DjName = shown;
            }

            state.DisplayName = shown;
        }

        var title = ShownName.ClampTitle(honorific).Trim();
        if (title.Length > 0)
        {
            state.Honorific = title;
        }
        var face = HandsetLook.CopyPortrait(paths, state.ProfileFacePath, badges, "music-profile-face");
        state.ProfileFacePath = face.Path;
        state.FaceZoom = face.Zoom;
        state.FaceFocusX = face.FocusX;
        state.FaceFocusY = face.FocusY;
        var banner = HandsetLook.CopyBanner(paths, state.ProfileBannerPath, display, "music-profile-banner");
        state.ProfileBannerPath = banner.Path;
        state.BannerZoom = banner.Zoom;
        state.BannerFocusX = banner.FocusX;
        state.BannerFocusY = banner.FocusY;

        state.UsesHandsetProfile = false;
        state.UsesHandsetIdentity = false;
        profileStamp++;
        state.Save(paths);
    }

    AppletManifest IApplet.Manifest => Manifest;

    public AppletBadge Badge => AppletBadge.None;

    public string Place => state.Page == MusicPage.Tabs ? state.Genre : state.Page.ToString();

    public void Enter(AppletEntry entry)
    {
        display.Landscape = false;
        publicRadio.Ensure(state.Genre);
        WarmStations();
        community.Refresh();
        streams.Refresh();
        if (!state.HasAccount)
        {
            state.Page = state.OnAuthSheet ? state.Page : MusicPage.Auth;
            return;
        }

        if (!state.Onboarded)
        {
            return;
        }

        if (TryOpenLivePlace(entry.RouteHint))
        {
            return;
        }

        if (TryOpenPlayerPlace(entry.RouteHint))
        {
            return;
        }

        if (TryOpenSearchPlace(entry.RouteHint))
        {
            return;
        }

        if (IsRadioPlace(entry.RouteHint))
        {
            OpenDiscover(live: false);
        }
    }

    private static bool IsRadioPlace(string? hint) =>
        hint is "radio" or "stations" or "discover" or "discover-radio";

    private bool TryOpenLivePlace(string? hint)
    {
        if (hint is null || !hint.StartsWith("live:", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var id = hint["live:".Length..].Trim();
        var station = FindCommunity(id);
        if (!string.IsNullOrEmpty(station.Id))
        {
            OpenStation(station);
            return true;
        }

        state.Open(MusicPage.Player);
        return true;
    }

    private void OnFrame(float _)
    {
        var now = Environment.TickCount64;
        if (now - lastCommunityRefresh > 8000)
        {
            lastCommunityRefresh = now;
            community.Refresh();
            streams.Refresh();
        }

        if (now - lastFollowWatch > 2000)
        {
            lastFollowWatch = now;
            WatchFollowedLive();
        }
    }

    private void WatchFollowedLive()
    {
        var live = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var rows = MergedLive();
        for (var index = 0; index < rows.Length; index++)
        {
            var station = rows[index];
            if (!station.Live)
            {
                continue;
            }

            var id = MusicState.BareStationId(station.Id);
            if (id.Length == 0 || !live.Add(id))
            {
                continue;
            }

            if (!liveSeeded)
            {
                liveSeen.Add(id);
                continue;
            }

            if (!liveSeen.Add(id) || OwnStation(id) || !state.FollowsStationId(id))
            {
                continue;
            }

            if (!state.FollowNotifications)
            {
                continue;
            }

            var name = station.Name.Length > 0 ? station.Name : "A station you follow";
            var host = station.Host.Length > 0 ? station.Host : "Live";
            notices.PostMusic(id, name, host + " just went live.", clock);
        }

        if (!liveSeeded)
        {
            liveSeeded = true;
            return;
        }

        liveSeen.RemoveWhere(id => !live.Contains(id));
    }

    private bool TryOpenPlayerPlace(string? hint)
    {
        if (hint is not ("player" or "now" or "live"))
        {
            return false;
        }

        state.Open(MusicPage.Player);
        return true;
    }

    private bool TryOpenSearchPlace(string? hint)
    {
        if (hint is null || !hint.StartsWith("search:", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var rest = hint["search:".Length..];
        var cut = rest.LastIndexOf('|');
        var genre = cut < 0 ? rest.Trim() : rest[..cut].Trim();
        var stationId = cut < 0 ? string.Empty : rest[(cut + 1)..].Trim();
        if (LocatePublic(stationId, audio.Now.Title, audio.Now.StreamUrl, out _, out var station))
        {
            ShowPublicInGenre(station);
            return true;
        }

        ShowPublicInGenre(IndexOfGenre(genre), stationId, audio.Now.Title ?? string.Empty);
        return true;
    }

    private void ShowPublicInGenre(PublicStation station) =>
        ShowPublicInGenre(IndexOfGenre(station.Genre), station.Id, station.Title);

    private void ShowNowPlayingInGenre()
    {
        var now = audio.Now;
        if (now.Live)
        {
            state.Open(MusicPage.Player);
            return;
        }

        if (LocatePublic(now.Id, now.Title, now.StreamUrl, out var genreIndex, out var station))
        {
            ShowPublicInGenre(station);
            return;
        }

        ShowPublicInGenre(genreIndex < 0 ? IndexOfGenreFromDetail(now.Detail) : genreIndex, now.Id, now.Title);
    }

    private void ShowPublicInGenre(int genreIndex, string stationId, string title)
    {
        if (genreIndex < 0 && stationId.Length > 0)
        {
            genreIndex = IndexOfGenreHolding(stationId);
        }

        if (genreIndex < 0 && title.Length > 0)
        {
            genreIndex = IndexOfGenreHoldingTitle(title);
        }

        OpenGenre(genreIndex < 0 ? 0 : genreIndex);
        state.RevealStationId = stationId ?? string.Empty;
        state.RevealStationTitle = title ?? string.Empty;
    }

    private static int IndexOfGenreFromDetail(string? detail)
    {
        var text = detail ?? string.Empty;
        var cut = text.IndexOf(" · ", StringComparison.Ordinal);
        return IndexOfGenre(cut > 0 ? text[..cut] : text);
    }

    private static int IndexOfGenre(string genre)
    {
        for (var index = 0; index < MusicState.Genres.Length; index++)
        {
            if (MusicState.Genres[index].Equals(genre, StringComparison.OrdinalIgnoreCase))
            {
                return index;
            }
        }

        return -1;
    }

    private int IndexOfGenreHolding(string stationId)
    {
        for (var index = 0; index < MusicState.Genres.Length; index++)
        {
            var stations = publicRadio.Stations(MusicState.Genres[index]);
            for (var row = 0; row < stations.Count; row++)
            {
                if (stations[row].Id.Equals(stationId, StringComparison.OrdinalIgnoreCase))
                {
                    return index;
                }
            }
        }

        return -1;
    }

    public void Leave()
    {
        display.Landscape = false;
        if (!community.Broadcasting)
        {
            sense.Stop();
        }

        state.Save(paths);
    }

    public bool CanGoBack =>
        !state.HasAccount
            ? state.Page is MusicPage.AuthLogin or MusicPage.AuthCreate
            : state.Page != MusicPage.Tabs && state.Page != MusicPage.Onboard;

    public bool Back()
    {
        if (state.ReportOpen)
        {
            CloseReport();
            return true;
        }

        if (!state.HasAccount)
        {
            if (state.Page is MusicPage.AuthLogin or MusicPage.AuthCreate)
            {
                state.AuthNote = string.Empty;
                state.Page = MusicPage.Auth;
                state.Scroll = 0f;
                return true;
            }

            return false;
        }

        if (state.Page == MusicPage.Tabs || state.Page == MusicPage.Onboard)
        {
            return false;
        }

        if (state.Page == MusicPage.SetupVenue)
        {
            state.Page = state.Dj ? MusicPage.SetupDj : state.Listener ? MusicPage.SetupListener : MusicPage.Onboard;
            return true;
        }

        if (state.Page == MusicPage.SetupDj)
        {
            state.Page = state.Listener ? MusicPage.SetupListener : MusicPage.Onboard;
            return true;
        }

        if (state.Page == MusicPage.SetupListener)
        {
            state.Page = MusicPage.Onboard;
            return true;
        }

        if (state.Page == MusicPage.EditProfile || state.Page == MusicPage.PlacePhoto)
        {
            state.Page = MusicPage.Tabs;
            state.Tab = MusicTab.Profile;
            state.Save(paths);
            return true;
        }

        if (state.Page == MusicPage.PickPlaylist)
        {
            state.Pending = null;
        }

        state.Back();
        return true;
    }

    public void Compose(in AppletFrame frame)
    {
        nameClock += frame.DeltaSeconds;
        try
        {
            publicRadio.Ensure(state.Genre);
            SyncBroadcastTap();
            PullOwnedStation();
        }
        catch (Exception)
        {
        }

        try
        {
            ApplyImagePick(frame);
            if (!state.HasAccount && !state.OnAuthSheet)
            {
                state.Page = MusicPage.Auth;
            }
            else if (state.HasAccount && state.OnAuthSheet)
            {
                state.Page = state.Onboarded ? MusicPage.Tabs : MusicPage.Onboard;
            }

            MusicChrome.PaintGround(frame, frame.Content);
            if (state.OnAuthSheet)
            {
                DrawAuth(frame, frame.Content.Inset(frame.Units(16f)));
                return;
            }

            if (state.Page == MusicPage.Onboard)
            {
                DrawOnboard(frame, frame.Content.Inset(frame.Units(16f)));
                return;
            }

            if (state.Page is MusicPage.SetupListener or MusicPage.SetupDj or MusicPage.SetupVenue)
            {
                DrawSetup(frame, frame.Content.Inset(frame.Units(16f)));
                return;
            }

            if (state.Page == MusicPage.PlacePhoto)
            {
                DrawPlacePhoto(frame, frame.Content);
                return;
            }

            if (state.Page != MusicPage.Tabs)
            {
                DrawStackPage(frame, frame.Content.Inset(frame.Units(14f)));
                return;
            }

            DrawTabs(frame);
        }
        catch (Exception)
        {
            frame.Text.DrawIn(frame.Content.Inset(frame.Units(16f)), "Music hit a layout error. Back out and open it again.",
                new TextStyle(FontRole.Caption, MusicChrome.Mute));
        }
    }

    private void DrawTabs(in AppletFrame frame)
    {
        var nav = frame.Units(64f);
        var live = audio.Phase is HandsetAudioPhase.Playing or HandsetAudioPhase.Buffering or HandsetAudioPhase.Paused
            or HandsetAudioPhase.Failed;
        var mini = live ? frame.Units(58f) : 0f;
        var strip = frame.Content.BottomSlice(nav);
        DrawNav(frame, strip);
        frame.Input.Claim(strip);
        if (live)
        {
            var dock = frame.Content.BottomSlice(nav + mini).TopSlice(mini)
                .Inset(new Edges(frame.Units(10f), frame.Units(4f)));
            DrawMini(frame, dock);
            frame.Input.Claim(dock);
        }

        var gutter = frame.Units(14f);
        var floor = nav + mini + frame.Units(6f);
        var body = state.Tab == MusicTab.Profile
            ? frame.Content.Inset(new Edges(0f, 0f, 0f, floor))
            : frame.Content.Inset(new Edges(gutter, frame.Units(8f), gutter, floor));
        if (body.IsEmpty)
        {
            return;
        }

        frame.Paint.PushClip(body);
        try
        {
            switch (state.Tab)
            {
                case MusicTab.Feed:
                    DrawFeed(frame, body);
                    break;
                case MusicTab.Search:
                    DrawDiscover(frame, body);
                    break;
                case MusicTab.Library:
                    DrawLibrary(frame, body);
                    break;
                case MusicTab.Profile:
                    DrawProfileTab(frame, body);
                    break;
                default:
                    DrawHome(frame, body);
                    break;
            }
        }
        finally
        {
            frame.Paint.PopClip();
        }
    }

    private void DrawHome(in AppletFrame frame, Rect area)
    {
        var stack = MusicChrome.BeginSheet(frame, area, state, frame.Units(12f));
        try
        {
            var head = stack.Take(frame.Units(40f));
            var faceMark = frame.Units(32f);
            var gearMark = frame.Units(24f);
            var tools = head.RightSlice(faceMark + gearMark + frame.Units(8f));
            var face = CoverFit.InscribedSquare(tools.RightSlice(faceMark));
            var gear = CoverFit.InscribedSquare(tools.LeftSlice(gearMark));
            var copy = head.Inset(new Edges(0f, 0f, tools.Width + frame.Units(8f), 0f));
            MusicChrome.Title(frame, copy.TopSlice(frame.Units(22f)), "WELCOME BACK");
            frame.Text.DrawEllipsized(copy.BottomSlice(frame.Units(16f)), SharedName(),
                new TextStyle(FontRole.CaptionStrong, MusicChrome.Ink));
            DrawProfileFace(frame, face, SharedName());
            MusicChrome.HomeGear(frame, gear);
            if (frame.Input.ConsumeClick(face))
            {
                OpenTab(MusicTab.Profile);
                return;
            }

            if (frame.Input.ConsumeClick(gear))
            {
                state.Open(MusicPage.Settings);
                return;
            }

            var likes = LikedStations();
            var likeHead = stack.Take(frame.Units(22f));
            frame.Text.DrawIn(likeHead, "Your likes",
                new TextStyle(FontRole.BodyStrong, MusicChrome.Ink));
            if (likes.Count == 0)
            {
                DrawHint(frame, ref stack, "Heart a station and it shows up here.");
            }
            else
            {
                DrawLikeGrid(frame, stack.Take(frame.Units(112f)), likes);
            }

            var livePicks = RecommendedLive();
            if (MusicChrome.Section(frame, stack.Take(frame.Units(24f)), "Recommended LIVE"))
            {
                state.FeedPane = MusicFeedPane.Live;
                OpenTab(MusicTab.Feed);
                return;
            }

            if (livePicks.Length == 0)
            {
                DrawHint(frame, ref stack, "When DJs go live on a Linkpearl broadcast, their stations land here.");
            }
            else
            {
                var liveCard = area.Width * 0.42f;
                DrawFollowShelf(frame, stack.Take(liveCard + frame.Units(28f)), livePicks, compact: false);
            }

            var twitchPicks = RecommendedTwitch();
            if (MusicChrome.Section(frame, stack.Take(frame.Units(24f)), "Recommended Twitch DJs"))
            {
                state.FeedPane = MusicFeedPane.Twitch;
                OpenTab(MusicTab.Feed);
                return;
            }

            if (twitchPicks.Length == 0)
            {
                DrawHint(frame, ref stack, "Live Twitch DJs from Rolladeck land here.");
            }
            else
            {
                var twitchCard = area.Width * 0.42f;
                DrawFollowShelf(frame, stack.Take(twitchCard + frame.Units(28f)), twitchPicks, compact: false);
            }

            var more = MoreLikeStations();
            WarmStationArt(more);
            if (MusicChrome.Section(frame, stack.Take(frame.Units(24f)), "More of what you like"))
            {
                OpenTab(MusicTab.Library);
                return;
            }

            if (more.Count == 0)
            {
                DrawHint(frame, ref stack,
                    publicRadio.Busy
                        ? "Finding stations like yours…"
                        : "Save a station or add one to a playlist and we'll pick more like it.");
            }
            else
            {
                DrawCoverShelf(frame, stack.Take(frame.Units(164f)), more);
            }

            var followed = FollowedBoard().Where(static row => row.Live).ToArray();
            if (MusicChrome.Section(frame, stack.Take(frame.Units(24f)), "Stations you follow"))
            {
                state.FeedPane = MusicFeedPane.Following;
                OpenTab(MusicTab.Feed);
                return;
            }

            if (followed.Length == 0)
            {
                var empty = stack.Take(frame.Units(56f));
                MusicChrome.Plate(frame, empty, frame.Units(12f));
                frame.Text.DrawIn(empty.Inset(frame.Units(12f)), "When a station you follow goes live, it shows up here.",
                    new TextStyle(FontRole.Caption, MusicChrome.Mute));
                if (frame.Input.ConsumeClick(empty))
                {
                    state.FeedPane = MusicFeedPane.Live;
                    OpenTab(MusicTab.Feed);
                }
            }
            else
            {
                DrawFollowShelf(frame, stack.Take(frame.Units(164f)), followed, compact: true);
            }
        }
        finally
        {
            MusicChrome.EndSheet(frame, area, state, ref stack);
        }
    }

    private void DrawCoverShelf(in AppletFrame frame, Rect area, IReadOnlyList<PublicStation> stations)
    {
        var gap = frame.Units(8f);
        var cards = 3;
        var w = (area.Width - gap * (cards - 1)) / cards;
        var count = Math.Min(stations.Count, cards);
        for (var index = 0; index < count; index++)
        {
            var card = Rect.FromSize(new Vector2(area.Min.X + (w + gap) * index, area.Min.Y),
                new Vector2(w, area.Height));
            var station = stations[index];
            var art = card.TopSlice(w);
            DrawStationArt(frame, art, station.ArtUrl, station.Id, station.Title);
            var heart = art.BottomSlice(frame.Units(28f)).RightSlice(frame.Units(36f))
                .Inset(new Edges(0f, 0f, frame.Units(4f), frame.Units(4f)));
            DrawRadioLike(frame, heart, station);
            var copy = card.BottomSlice(frame.Units(28f));
            frame.Text.DrawEllipsized(copy.TopSlice(frame.Units(14f)), station.Title,
                new TextStyle(FontRole.CaptionStrong, MusicChrome.Ink));
            frame.Text.DrawEllipsized(copy.BottomSlice(frame.Units(12f)),
                station.Genre.Length > 0 ? station.Genre : station.Place,
                new TextStyle(FontRole.Caption, MusicChrome.Mute));
            if (TapRadioLike(frame, heart, station))
            {
                continue;
            }

            if (frame.Input.ConsumeClick(card))
            {
                TunePublic(station);
                state.Open(MusicPage.Player);
            }
        }
    }

    private void DrawLikeGrid(in AppletFrame frame, Rect area, IReadOnlyList<PublicStation> likes)
    {
        var gap = frame.Units(8f);
        var cellW = (area.Width - gap) * 0.5f;
        var cellH = (area.Height - gap) * 0.5f;
        var count = Math.Min(likes.Count, 4);
        for (var index = 0; index < count; index++)
        {
            var col = index % 2;
            var row = index / 2;
            var tile = Rect.FromSize(
                new Vector2(area.Min.X + (cellW + gap) * col, area.Min.Y + (cellH + gap) * row),
                new Vector2(cellW, cellH));
            var station = likes[index];
            MusicChrome.Plate(frame, tile, frame.Units(10f));
            var art = tile.LeftSlice(tile.Height).Inset(frame.Units(6f));
            DrawStationArt(frame, art, station.ArtUrl, station.Id, station.Title);
            var copy = tile.Inset(new Edges(tile.Height, frame.Units(8f), frame.Units(8f), frame.Units(8f)));
            frame.Text.DrawEllipsized(copy.TopSlice(frame.Units(16f)), station.Title,
                new TextStyle(FontRole.CaptionStrong, MusicChrome.Ink));
            frame.Text.DrawEllipsized(copy.BottomSlice(frame.Units(14f)),
                station.Genre.Length > 0 ? station.Genre : station.Place,
                new TextStyle(FontRole.Caption, MusicChrome.Mute));
            if (frame.Input.ConsumeClick(tile))
            {
                TunePublic(station);
                state.Open(MusicPage.Player);
            }
        }
    }

    private void DrawFollowShelf(in AppletFrame frame, Rect area, IReadOnlyList<CommunityStation> stations, bool compact)
    {
        var gap = frame.Units(8f);
        var cards = compact ? 3 : 4;
        var w = compact ? (area.Width - gap * (cards - 1)) / cards : area.Width * 0.42f;
        var count = Math.Min(stations.Count, cards);
        for (var index = 0; index < count; index++)
        {
            var card = Rect.FromSize(new Vector2(area.Min.X + (w + gap) * index, area.Min.Y),
                new Vector2(w, area.Height));
            var station = stations[index];
            var copyH = frame.Units(28f);
            var artSide = MathF.Min(w, MathF.Max(8f, card.Height - copyH));
            var art = card.TopSlice(artSide);
            DrawStationArt(frame, art, station.ArtPath, station.Id, station.Name);
            var live = art.TopSlice(frame.Units(18f)).LeftSlice(frame.Units(40f))
                .Inset(new Edges(frame.Units(4f), frame.Units(4f), 0f, 0f));
            frame.Text.DrawIn(live, "Live",
                new TextStyle(FontRole.CaptionStrong, MusicChrome.LiveOn));
            var hearN = ShownListeners(station.Listeners, station.Live);
            var hearH = frame.Units(18f);
            var hear = Rect.FromSize(
                new Vector2(art.Min.X + frame.Units(4f), art.Max.Y - frame.Units(4f) - hearH),
                new Vector2(MathF.Min(art.Width - frame.Units(8f),
                    StationAudienceWidth(frame, station with { Listeners = hearN }, true)),
                    hearH));
            DrawStationAudience(frame, hear, station with { Listeners = hearN }, compact: true);
            var heartW = frame.Units(36f) * 0.95f;
            var heartH = frame.Units(28f) * 0.95f;
            var heart = Rect.FromSize(
                new Vector2(art.Max.X - frame.Units(4f) - heartW, art.Max.Y - frame.Units(4f) - heartH),
                new Vector2(heartW, heartH));
            DrawStationLike(frame, heart, station.Id, MusicChrome.Ink);
            var copy = new Rect(new Vector2(card.Min.X, art.Max.Y), new Vector2(card.Max.X, card.Max.Y));
            frame.Text.DrawEllipsized(copy.TopSlice(frame.Units(14f)),
                station.Name.Length > 0 ? station.Name : "Station",
                new TextStyle(FontRole.CaptionStrong, MusicChrome.Ink));
            frame.Text.DrawEllipsized(copy.BottomSlice(frame.Units(12f)),
                ShownStationGenre(station).Length > 0 ? ShownStationGenre(station) : station.Host,
                new TextStyle(FontRole.Caption, MusicChrome.Mute));
            if (TapStationLike(frame, heart, station.Id))
            {
                continue;
            }

            if (frame.Input.ConsumeClick(card))
            {
                OpenStation(station);
            }
        }
    }

    private void DrawFeedLiveCard(in AppletFrame frame, Rect area, CommunityStation station)
    {
        var head = area.TopSlice(frame.Units(22f));
        var follow = head.RightSlice(frame.Units(72f));
        var on = state.FollowsStationId(station.Id);
        frame.Paint.Fill(follow, on ? MusicChrome.Purple : MusicChrome.CardHi, frame.Units(10f));
        frame.Text.DrawIn(follow, on ? "Following" : "Follow",
            new TextStyle(FontRole.CaptionStrong, on ? MusicChrome.GroundHi : MusicChrome.Purple, TextAlign.Center));
        frame.Text.DrawEllipsized(head.Inset(new Edges(0f, 0f, follow.Width + frame.Units(8f), 0f)),
            (station.Host.Length > 0 ? station.Host : "DJ") + (station.Live ? " is live" : " · station"),
            new TextStyle(FontRole.Caption, MusicChrome.Mute));
        var card = area.Inset(new Edges(0f, frame.Units(26f), 0f, 0f));
        DrawStationArt(frame, card, station.ArtPath, station.Id, station.Name);
        if (station.Live)
        {
            MusicChrome.LiveMark(frame, card.TopSlice(frame.Units(20f)).LeftSlice(frame.Units(44f))
                .Inset(new Edges(frame.Units(8f), frame.Units(8f), 0f, 0f)));
        }

        var rail = card.RightSlice(frame.Units(44f)).Inset(new Edges(0f, frame.Units(18f), frame.Units(8f),
            frame.Units(56f)));
        var heart = rail.TopSlice(frame.Units(42f));
        DrawStationLike(frame, heart, station.Id);
        var copy = card.BottomSlice(frame.Units(48f)).Inset(new Edges(frame.Units(12f), 0f, frame.Units(56f),
            frame.Units(8f)));
        frame.Text.DrawEllipsized(copy.TopSlice(frame.Units(18f)),
            station.Name.Length > 0 ? station.Name : "Station",
            new TextStyle(FontRole.BodyStrong, MusicChrome.Ink));
        frame.Text.DrawEllipsized(copy.BottomSlice(frame.Units(16f)),
            (station.Host.Length > 0 ? station.Host : "DJ") +
            (ShownStationGenre(station).Length > 0 ? " · " + ShownStationGenre(station) : string.Empty),
            new TextStyle(FontRole.Caption, MusicChrome.Mute));
        var hearN = ShownListeners(station.Listeners, station.Live);
        var shown = station with { Listeners = hearN };
        DrawStationAudience(frame,
            card.TopSlice(frame.Units(22f)).RightSlice(MathF.Min(card.Width * 0.72f,
                StationAudienceWidth(frame, shown, false))).Inset(new Edges(0f, frame.Units(8f), frame.Units(8f), 0f)),
            shown, compact: false);
        var play = card.BottomSlice(frame.Units(48f)).RightSlice(frame.Units(48f)).Inset(frame.Units(4f));
        frame.Paint.FillCircle(play.Center, frame.Units(16f), new Vector4(1f, 1f, 1f, 0.88f));
        frame.Text.DrawIn(play, "▶", new TextStyle(FontRole.CaptionStrong, MusicChrome.Ground, TextAlign.Center));
        if (frame.Input.ConsumeClick(follow))
        {
            ToggleFollowLive(station);
            return;
        }

        if (TapStationLike(frame, heart, station.Id))
        {
            return;
        }

        if (frame.Input.ConsumeClick(play) || frame.Input.ConsumeClick(card))
        {
            OpenStation(station);
        }
    }

    private static float GenreTileHeight(in AppletFrame frame, int index)
    {
        return (index % 5) switch
        {
            0 => frame.Units(118f),
            1 => frame.Units(168f),
            2 => frame.Units(86f),
            3 => frame.Units(148f),
            _ => frame.Units(96f),
        };
    }

    private static float GenreMasonryHeight(in AppletFrame frame)
    {
        var gap = frame.Units(8f);
        var left = 0f;
        var right = 0f;
        for (var index = 0; index < MusicState.Genres.Length; index++)
        {
            var h = GenreTileHeight(frame, index);
            if (left <= right)
            {
                left += h + gap;
            }
            else
            {
                right += h + gap;
            }
        }

        return MathF.Max(left, right);
    }

    private void DrawGenreMasonry(in AppletFrame frame, Rect area)
    {
        var gap = frame.Units(8f);
        var col = (area.Width - gap) * 0.5f;
        var left = 0f;
        var right = 0f;
        for (var index = 0; index < MusicState.Genres.Length; index++)
        {
            var h = GenreTileHeight(frame, index);
            var useLeft = left <= right;
            var x = useLeft ? area.Min.X : area.Min.X + col + gap;
            var y = area.Min.Y + (useLeft ? left : right);
            var tile = Rect.FromSize(new Vector2(x, y), new Vector2(col, h));
            var on = state.GenreIndex == index;
            MusicChrome.GenreTile(frame, tile, MusicState.Genres[index], MusicChrome.GenreTint(index), on);
            if (useLeft)
            {
                left += h + gap;
            }
            else
            {
                right += h + gap;
            }

            if (frame.Input.ConsumeClick(tile))
            {
                OpenGenre(index);
            }
        }
    }

    private void WarmStations()
    {
        publicRadio.Ensure(state.Genre);
        for (var index = 0; index < MusicState.Genres.Length; index++)
        {
            publicRadio.Ensure(MusicState.Genres[index]);
        }
    }

    private string moreLikeKey = string.Empty;
    private PublicStation[] moreLike = [];
    private string recommendedLiveKey = string.Empty;
    private CommunityStation[] recommendedLive = [];
    private string recommendedTwitchKey = string.Empty;
    private CommunityStation[] recommendedTwitch = [];

    private CommunityStation[] SavedLiveStations()
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var rows = new List<CommunityStation>();
        void Put(CommunityStation station)
        {
            if (string.IsNullOrEmpty(station.Id) || !seen.Add(MusicState.BareStationId(station.Id)))
            {
                return;
            }

            rows.Add(station);
        }

        foreach (var id in state.Favorites)
        {
            Put(FindCommunity(id));
        }

        foreach (var snap in state.SavedLive)
        {
            Put(snap.ToStation(FindCommunity(snap.Id).Live, 0, community.StationLikes(snap.Id),
                community.StationLiked(snap.Id)));
        }

        return rows.ToArray();
    }

    private IReadOnlyList<PublicStation> LikedStations()
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var rows = new List<PublicStation>();
        void Put(PublicStation station)
        {
            if (station.Id.Length == 0 || !community.StationLiked(station.Id) || !seen.Add(station.Id))
            {
                return;
            }

            rows.Add(station);
        }

        foreach (var mark in state.LikedRadio)
        {
            Put(mark.ToPublic());
        }

        foreach (var genre in publicRadio.Genres)
        {
            var stations = publicRadio.Stations(genre);
            for (var index = 0; index < stations.Count; index++)
            {
                Put(stations[index]);
            }
        }

        return rows;
    }

    private IReadOnlyList<PublicStation> SavedRadioStations()
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var rows = new List<PublicStation>();
        void Put(PublicStation station)
        {
            if (station.Id.Length == 0 || !state.IsFavorite(station.Id) || !seen.Add(station.Id))
            {
                return;
            }

            rows.Add(station);
        }

        foreach (var mark in state.SavedRadio)
        {
            Put(mark.ToPublic());
        }

        foreach (var genre in publicRadio.Genres)
        {
            var stations = publicRadio.Stations(genre);
            for (var index = 0; index < stations.Count; index++)
            {
                Put(stations[index]);
            }
        }

        return rows;
    }

    private CommunityStation[] RecommendedLive() =>
        PickRecommended(BroadcastLive(), ref recommendedLiveKey, ref recommendedLive);

    private CommunityStation[] RecommendedTwitch() =>
        PickRecommended(TwitchBoard(), ref recommendedTwitchKey, ref recommendedTwitch);

    private CommunityStation[] PickRecommended(IReadOnlyList<CommunityStation> source, ref string cachedKey,
        ref CommunityStation[] cached)
    {
        var pool = source
            .Where(row => row.Id.Length > 0 && !OwnStation(row))
            .GroupBy(row => row.Id, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToArray();
        var key = string.Join(",", pool.Select(static row => row.Id).OrderBy(static id => id, StringComparer.Ordinal));
        if (string.Equals(key, cachedKey, StringComparison.Ordinal) && cached.Length > 0)
        {
            return cached;
        }

        cachedKey = key;
        var pick = Math.Min(3, pool.Length);
        if (pick == 0)
        {
            cached = [];
            return cached;
        }

        var order = Enumerable.Range(0, pool.Length).ToArray();
        var rng = new Random(unchecked((int)(uint)key.GetHashCode(StringComparison.Ordinal)));
        for (var index = order.Length - 1; index > 0; index--)
        {
            var swap = rng.Next(index + 1);
            (order[index], order[swap]) = (order[swap], order[index]);
        }

        cached = new CommunityStation[pick];
        for (var index = 0; index < pick; index++)
        {
            cached[index] = pool[order[index]];
        }

        return cached;
    }

    private IReadOnlyList<PublicStation> MoreLikeStations()
    {
        var known = new HashSet<string>(state.Favorites, StringComparer.OrdinalIgnoreCase);
        var genres = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < state.Playlists.Count; index++)
        {
            var list = state.Playlists[index].Stations;
            for (var row = 0; row < list.Count; row++)
            {
                var mark = list[row];
                if (mark.Id.Length > 0)
                {
                    known.Add(mark.Id);
                }

                if (mark.Genre.Length > 0)
                {
                    genres.Add(mark.Genre);
                }
            }
        }

        foreach (var liked in LikedStations())
        {
            if (liked.Genre.Length > 0)
            {
                genres.Add(liked.Genre);
            }
        }

        if (known.Count == 0)
        {
            moreLikeKey = string.Empty;
            moreLike = [];
            return moreLike;
        }

        if (genres.Count == 0)
        {
            genres.Add(state.Genre);
        }

        foreach (var genre in genres)
        {
            publicRadio.Ensure(genre);
        }

        var pool = genres
            .SelectMany(publicRadio.Stations)
            .Where(row => row.Id.Length > 0 && !known.Contains(row.Id))
            .GroupBy(row => row.Id, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToArray();
        var key = known.Count + ":" + string.Join(",", genres.OrderBy(static name => name, StringComparer.Ordinal)) +
                  ":" + pool.Length;
        if (string.Equals(key, moreLikeKey, StringComparison.Ordinal) && moreLike.Length > 0)
        {
            return moreLike;
        }

        moreLikeKey = key;
        var pick = Math.Min(3, pool.Length);
        if (pick == 0)
        {
            moreLike = [];
            return moreLike;
        }

        var order = Enumerable.Range(0, pool.Length).ToArray();
        var rng = new Random(unchecked((int)(uint)key.GetHashCode(StringComparison.Ordinal)));
        for (var index = order.Length - 1; index > 0; index--)
        {
            var swap = rng.Next(index + 1);
            (order[index], order[swap]) = (order[swap], order[index]);
        }

        moreLike = new PublicStation[pick];
        for (var index = 0; index < pick; index++)
        {
            moreLike[index] = pool[order[index]];
        }

        return moreLike;
    }

    private static string FirstName(string name)
    {
        var space = name.Trim().IndexOf(' ');
        return space > 0 ? name[..space] : name.Trim();
    }

    private static string Initial(string name)
    {
        var trim = name.Trim();
        return trim.Length == 0 ? "♪" : trim[..1].ToUpperInvariant();
    }

    private void OpenTab(MusicTab tab)
    {
        state.Tab = tab;
        state.Page = MusicPage.Tabs;
        state.Scroll = 0f;
        state.SheetHeight = 1600f;
        if (tab == MusicTab.Profile)
        {
            state.ViewingId = MusicRoster.SelfId(pearl.Current);
        }
        else if (tab == MusicTab.Feed)
        {
            community.Refresh();
            streams.Refresh();
        }
        else if (tab is MusicTab.Search or MusicTab.Home or MusicTab.Library)
        {
            WarmStations();
        }
    }

    private void ShowProfile(string id)
    {
        var self = MusicRoster.SelfId(pearl.Current);
        if (id.Length == 0 || string.Equals(id, self, StringComparison.OrdinalIgnoreCase))
        {
            OpenTab(MusicTab.Profile);
            return;
        }

        state.OpenProfile(id);
    }

    private void OpenDiscover(bool live)
    {
        state.FeedPane = live ? MusicFeedPane.Live : MusicFeedPane.Radio;
        OpenTab(live ? MusicTab.Feed : MusicTab.Search);
    }

    private bool RevealGenreStation(in AppletFrame frame, IReadOnlyList<PublicStation> stations, float viewHeight)
    {
        if (state.RevealStationId.Length == 0 && state.RevealStationTitle.Length == 0)
        {
            return false;
        }

        if (!LocatePublic(state.RevealStationId, state.RevealStationTitle, audio.Now.StreamUrl, out var genreIndex,
                out var station) &&
            stations.Count > 0)
        {
            var local = IndexOfPublic(stations, state.RevealStationId, state.RevealStationTitle, audio.Now.StreamUrl);
            if (local >= 0)
            {
                station = stations[local];
                genreIndex = state.GenreIndex;
            }
        }

        if (genreIndex >= 0 && genreIndex != state.GenreIndex)
        {
            state.GenreIndex = genreIndex;
            publicRadio.Ensure(state.Genre);
            stations = publicRadio.Stations(state.Genre);
        }

        if (stations.Count == 0)
        {
            return true;
        }

        var index = IndexOfPublic(stations, station.Id.Length > 0 ? station.Id : state.RevealStationId,
            state.RevealStationTitle.Length > 0 ? state.RevealStationTitle : station.Title, audio.Now.StreamUrl);
        if (index < 0)
        {
            return true;
        }

        var stride = frame.Units(52f) + frame.Units(10f);
        var max = MathF.Max(0f, stations.Count * stride - MathF.Max(viewHeight, 1f));
        state.Scroll = Math.Clamp(index * stride, 0f, max);
        if (viewHeight >= frame.Units(48f))
        {
            state.RevealStationId = string.Empty;
            state.RevealStationTitle = string.Empty;
        }

        return true;
    }

    private bool LocatePublic(string? id, string? title, string? streamUrl, out int genreIndex,
        out PublicStation station)
    {
        genreIndex = -1;
        station = default;
        for (var genre = 0; genre < MusicState.Genres.Length; genre++)
        {
            publicRadio.Ensure(MusicState.Genres[genre]);
            var stations = publicRadio.Stations(MusicState.Genres[genre]);
            var index = IndexOfPublic(stations, id, title, streamUrl);
            if (index < 0)
            {
                continue;
            }

            genreIndex = genre;
            station = stations[index];
            return true;
        }

        return false;
    }

    private static int IndexOfPublic(IReadOnlyList<PublicStation> stations, string? id, string? title,
        string? streamUrl)
    {
        var bare = MusicState.BareStationId(id);
        var name = (title ?? string.Empty).Trim();
        var pack = streamUrl ?? string.Empty;
        for (var index = 0; index < stations.Count; index++)
        {
            var row = stations[index];
            if (bare.Length > 0 &&
                (row.Id.Equals(bare, StringComparison.OrdinalIgnoreCase) ||
                 row.Id.Equals(id, StringComparison.OrdinalIgnoreCase)))
            {
                return index;
            }

            if (name.Length > 0 && row.Title.Equals(name, StringComparison.OrdinalIgnoreCase))
            {
                return index;
            }

            if (pack.Length > 0 &&
                (pack.Contains(row.StreamUrl, StringComparison.OrdinalIgnoreCase) ||
                 row.AlternateUrl.Length > 0 && pack.Contains(row.AlternateUrl, StringComparison.OrdinalIgnoreCase)))
            {
                return index;
            }
        }

        return -1;
    }

    private int IndexOfGenreHoldingTitle(string title)
    {
        for (var index = 0; index < MusicState.Genres.Length; index++)
        {
            if (IndexOfStationTitle(publicRadio.Stations(MusicState.Genres[index]), title) >= 0)
            {
                return index;
            }
        }

        return -1;
    }

    private static int IndexOfStationTitle(IReadOnlyList<PublicStation> stations, string title)
    {
        for (var index = 0; index < stations.Count; index++)
        {
            if (stations[index].Title.Equals(title, StringComparison.OrdinalIgnoreCase))
            {
                return index;
            }
        }

        return -1;
    }

    private void OpenGenre(int index)
    {
        state.GenreIndex = Math.Clamp(index, 0, MusicState.Genres.Length - 1);
        state.Search = string.Empty;
        state.ScrollRailHeld = false;
        publicRadio.Ensure(state.Genre);
        state.Open(MusicPage.GenreList);
    }

    private void OpenPlaylist(string id)
    {
        state.ViewingPlaylistId = id;
        state.Open(MusicPage.Playlist);
    }

    private void OfferPlaylist(PublicStation station)
    {
        state.Pending = MusicStationMark.From(station);
        state.Open(MusicPage.PickPlaylist);
    }

    private void PlayMarks(IReadOnlyList<MusicStationMark> stations, int index)
    {
        if (stations.Count == 0)
        {
            return;
        }

        state.ArmQueue(stations, index);
        TunePublic(stations[state.QueueIndex].ToPublic(), keepQueue: true);
        state.Open(MusicPage.Player);
    }

    private void DrawFeed(in AppletFrame frame, Rect area)
    {
        var stack = MusicChrome.BeginSheet(frame, area, state, frame.Units(10f));
        try
        {
            var panes = stack.Take(frame.Units(32f));
            var gap = frame.Units(4f);
            var cell = (panes.Width - gap * 3f) / 4f;
            var radio = Rect.FromSize(panes.Min, new Vector2(cell, panes.Height));
            var following = Rect.FromSize(new Vector2(radio.Max.X + gap, panes.Min.Y), new Vector2(cell, panes.Height));
            var live = Rect.FromSize(new Vector2(following.Max.X + gap, panes.Min.Y), new Vector2(cell, panes.Height));
            var twitch = Rect.FromSize(new Vector2(live.Max.X + gap, panes.Min.Y), new Vector2(cell, panes.Height));
            if (MusicChrome.SoftPill(frame, radio, "Radio", state.FeedPane == MusicFeedPane.Radio))
            {
                state.FeedPane = MusicFeedPane.Radio;
                state.Scroll = 0f;
            }

            if (MusicChrome.SoftPill(frame, following, "Following", state.FeedPane == MusicFeedPane.Following))
            {
                state.FeedPane = MusicFeedPane.Following;
                state.Scroll = 0f;
            }

            if (MusicChrome.SoftPill(frame, live, "LIVE", state.FeedPane == MusicFeedPane.Live))
            {
                state.FeedPane = MusicFeedPane.Live;
                state.Scroll = 0f;
            }

            if (MusicChrome.SoftPill(frame, twitch, "Twitch DJs", state.FeedPane == MusicFeedPane.Twitch))
            {
                state.FeedPane = MusicFeedPane.Twitch;
                state.Scroll = 0f;
            }

            if (state.FeedPane == MusicFeedPane.Live)
            {
                DrawFeedLive(frame, ref stack);
            }
            else if (state.FeedPane == MusicFeedPane.Twitch)
            {
                DrawFeedTwitch(frame, ref stack);
            }
            else if (state.FeedPane == MusicFeedPane.Following)
            {
                DrawFeedFollowing(frame, ref stack);
            }
            else
            {
                DrawFeedRadio(frame, ref stack);
            }
        }
        finally
        {
            MusicChrome.EndSheet(frame, area, state, ref stack);
        }
    }

    private void DrawFeedRadio(in AppletFrame frame, ref Stack stack)
    {
        publicRadio.Ensure(state.Genre);
        var stations = publicRadio.Stations(state.Genre);
        var genre = state.Genre.Length > 0 ? state.Genre : "Radio";
        frame.Text.DrawIn(stack.Take(frame.Units(16f)),
            stations.Count == 0
                ? genre
                : genre + "  ·  " + (stations.Count == 1 ? "1 station" : stations.Count + " stations"),
            new TextStyle(FontRole.Caption, MusicChrome.Mute));
        if (stations.Count == 0)
        {
            DrawHint(frame, ref stack, publicRadio.Busy ? "Finding stations…" : "No stations yet. Try Search.");
            return;
        }

        WarmStationArt(stations);
        var take = Math.Min(stations.Count, 24);
        for (var index = 0; index < take; index++)
        {
            DrawRadioRow(frame, stack.Take(frame.Units(76f)), stations[index]);
        }
    }

    private void DrawRadioRow(in AppletFrame frame, Rect area, PublicStation station)
    {
        var current = string.Equals(audio.Now.Id, station.Id, StringComparison.OrdinalIgnoreCase);
        var playing = current && audio.Phase is HandsetAudioPhase.Playing or HandsetAudioPhase.Buffering;
        MusicChrome.Plate(frame, area, frame.Units(16f));
        var inset = area.Inset(frame.Units(10f));
        var art = CoverFit.InscribedSquare(inset.LeftSlice(inset.Height));
        DrawStationArt(frame, art, station.ArtUrl, station.Id, station.Title);
        var like = inset.RightSlice(frame.Units(28f)).TopSlice(frame.Units(24f));
        var hearN = ShownListeners(station.Listeners, current);
        var hear = inset.RightSlice(MathF.Min(inset.Width * 0.42f, MusicChrome.ListenerWidth(frame, hearN, true)))
            .BottomSlice(frame.Units(18f));
        var body = inset.Inset(new Edges(art.Width + frame.Units(10f), 0f, like.Width + frame.Units(8f), 0f));
        frame.Text.DrawEllipsized(body.TopSlice(frame.Units(18f)), station.Title,
            new TextStyle(FontRole.BodyStrong, MusicChrome.Ink));
        frame.Text.DrawEllipsized(body.Inset(new Edges(0f, frame.Units(20f), 0f, frame.Units(18f))),
            station.Place + (station.Bitrate > 0 ? "  ·  " + station.Bitrate + "k" : string.Empty) +
            (playing ? "  ·  Playing" : current ? "  ·  Paused" : string.Empty),
            new TextStyle(FontRole.Caption, MusicChrome.Mute));
        frame.Text.DrawEllipsized(body.BottomSlice(frame.Units(16f)),
            station.Genre.Length > 0 ? station.Genre : string.Empty,
            new TextStyle(FontRole.Caption, MusicChrome.Mute));
        MusicChrome.ListenerCount(frame, hear, hearN, compact: true);
        var liked = community.StationLiked(station.Id);
        frame.Text.DrawIn(like, liked ? "♥" : "♡",
            new TextStyle(FontRole.BodyStrong, liked ? MusicChrome.LikePink : MusicChrome.Mute, TextAlign.Center));
        if (TapStationLike(frame, like, station.Id))
        {
            return;
        }

        if (frame.Input.ConsumeClick(area))
        {
            TunePublic(station);
            state.Open(MusicPage.Player);
        }
    }

    private void DrawFeedLive(in AppletFrame frame, ref Stack stack)
    {
        var board = BroadcastBoard();
        var liveN = 0;
        for (var index = 0; index < board.Length; index++)
        {
            if (board[index].Live)
            {
                liveN++;
            }
        }

        frame.Text.DrawIn(stack.Take(frame.Units(16f)),
            board.Length == 0
                ? "No stations yet"
                : board.Length == 1
                    ? (liveN == 1 ? "1 station · live" : "1 station")
                    : board.Length + " stations" + (liveN > 0 ? "  ·  " + liveN + " live" : string.Empty),
            new TextStyle(FontRole.Caption, MusicChrome.Mute));
        if (board.Length == 0)
        {
            DrawHint(frame, ref stack, "Create a station and it stays listed here, live or off air.");
            return;
        }

        for (var index = 0; index < board.Length; index++)
        {
            var row = stack.Take(frame.Units(84f));
            if (!RowOnScreen(frame, row))
            {
                continue;
            }

            DrawBroadcastRow(frame, row, board[index]);
        }
    }

    private void DrawFeedTwitch(in AppletFrame frame, ref Stack stack)
    {
        var live = TwitchBoard();
        frame.Text.DrawIn(stack.Take(frame.Units(16f)),
            live.Length == 0
                ? "No one live · Rolladeck"
                : (live.Length == 1 ? "1 live DJ" : live.Length + " live DJs") + " · Rolladeck",
            new TextStyle(FontRole.Caption, MusicChrome.Mute));
        if (live.Length == 0)
        {
            DrawHint(frame, ref stack, "When a community DJ goes live on Twitch, they land here.");
        }
        else
        {
            for (var index = 0; index < live.Length; index++)
            {
                var row = stack.Take(frame.Units(84f));
                if (!RowOnScreen(frame, row))
                {
                    continue;
                }

                DrawTwitchDjRow(frame, row, live[index]);
            }
        }

        DrawRolladeckCredit(frame, stack.Take(frame.Units(22f)));
    }

    private void DrawFeedFollowing(in AppletFrame frame, ref Stack stack)
    {
        var board = FollowedBoard();
        if (board.Length == 0)
        {
            DrawHint(frame, ref stack, "Follow a live broadcast and it lands here.");
            var go = stack.Take(frame.Units(42f));
            MusicChrome.Primary(frame, go, "Browse live stations");
            if (frame.Input.ConsumeClick(go))
            {
                state.FeedPane = MusicFeedPane.Live;
            }

            return;
        }

        for (var index = 0; index < board.Length; index++)
        {
            var row = stack.Take(frame.Units(220f));
            if (!RowOnScreen(frame, row))
            {
                continue;
            }

            DrawFeedLiveCard(frame, row, board[index]);
        }
    }

    private bool FollowsStation(CommunityStation station)
    {
        if (string.IsNullOrEmpty(station.Id))
        {
            return false;
        }

        return state.FollowsStationId(station.Id) ||
               (station.Host.Length > 0 && state.Following.Contains(station.Host));
    }

    private CommunityStation[] FollowedBoard()
    {
        var now = Environment.TickCount64;
        if (followedBoardAt != 0 && now - followedBoardAt < 1000)
        {
            return followedBoard;
        }

        var byId = new Dictionary<string, CommunityStation>(StringComparer.OrdinalIgnoreCase);
        void Put(CommunityStation station)
        {
            var id = MusicState.BareStationId(station.Id);
            if (id.Length == 0 || !FollowsStation(station with { Id = id }))
            {
                return;
            }

            if (byId.TryGetValue(id, out var prior) && prior.Live && !station.Live)
            {
                return;
            }

            byId[id] = station with { Id = id };
        }

        foreach (var row in MergedBoard())
        {
            Put(row);
        }

        foreach (var snap in state.FollowedStations)
        {
            if (snap.Id.Length == 0 || byId.ContainsKey(snap.Id) || !state.FollowsStationId(snap.Id))
            {
                continue;
            }

            byId[snap.Id] = snap.ToStation(false, 0, community.StationLikes(snap.Id), community.StationLiked(snap.Id));
        }

        followedBoard = byId.Values
            .OrderByDescending(static row => row.Live)
            .ThenBy(static row => row.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        followedBoardAt = now;
        return followedBoard;
    }

    private CommunityStation FindCommunity(string id)
    {
        var bare = MusicState.BareStationId(id);
        if (bare.Length == 0)
        {
            return EmptyCommunity();
        }

        foreach (var row in MergedBoard())
        {
            if (string.Equals(row.Id, bare, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(row.TwitchLogin, StreamChrome.LoginOf(bare), StringComparison.OrdinalIgnoreCase))
            {
                return row;
            }
        }

        foreach (var snap in state.FollowedStations)
        {
            if (string.Equals(snap.Id, bare, StringComparison.OrdinalIgnoreCase))
            {
                return snap.ToStation(false, 0, community.StationLikes(bare), community.StationLiked(bare));
            }
        }

        return EmptyCommunity();
    }

    private static CommunityStation EmptyCommunity() =>
        new(string.Empty, string.Empty, string.Empty, string.Empty, false, string.Empty, 0);

    private bool TryFollowableNow(out CommunityStation station)
    {
        station = FindCommunity(audio.Now.Id);
        if (!string.IsNullOrEmpty(station.Id))
        {
            return true;
        }

        if (audio.Now.Live && !string.IsNullOrEmpty(audio.Now.Id))
        {
            station = new CommunityStation(audio.Now.Id, audio.Now.Title, audio.Now.Detail, state.Genre, true,
                audio.Now.StreamUrl, 0, string.Empty, audio.Now.ArtPath);
            return true;
        }

        return false;
    }

    private void FollowPerson(MusicPerson person)
    {
        if (person.Id.StartsWith("live:", StringComparison.Ordinal))
        {
            var station = FindCommunity(person.Id);
            if (string.IsNullOrEmpty(station.Id))
            {
                station = new CommunityStation(MusicState.BareStationId(person.Id),
                    person.Station.Length > 0 ? person.Station : person.Name, person.Name, person.Genre, person.Live,
                    person.StreamUrl, person.Listeners, person.Bio);
            }

            ToggleFollowLive(station);
            return;
        }

        var nowFollow = state.ToggleFollow(person.Id);
        pearl.Follow(person.Id, nowFollow);
        state.Save(paths);
    }

    private void ToggleFollowLive(CommunityStation station)
    {
        var snap = FollowedStationSnap.From(station);
        if (snap.Id.Length == 0)
        {
            return;
        }

        state.ToggleFollowStation(snap);
        state.Save(paths);
    }

    public bool HasAccount => state.HasAccount;

    public bool Liked(string id) => community.StationLiked(id);

    public int LikeCount(HandsetTune now)
    {
        var id = MusicState.BareStationId(now.Id);
        if (id.Length == 0)
        {
            return 0;
        }

        if (now.Live)
        {
            return community.StationLikes(id);
        }

        var station = FindPublic(id) ?? TuneAsPublic(now);
        return RadioLikes(station);
    }

    public void ToggleLike(HandsetTune now)
    {
        var id = MusicState.BareStationId(now.Id);
        if (id.Length == 0)
        {
            return;
        }

        if (now.Live)
        {
            community.ToggleStationLike(id);
            return;
        }

        var station = FindPublic(id) ?? TuneAsPublic(now);
        community.ToggleStationLike(id, Math.Max(0, station.Votes));
        if (community.StationLiked(id))
        {
            state.RememberLikedRadio(MusicStationMark.From(station with { Id = id }));
        }
        else
        {
            state.ForgetLikedRadio(id);
        }

        state.Save(paths);
    }

    public bool Saved(string id) => state.IsFavorite(id);

    public void ToggleSave(HandsetTune now)
    {
        var id = MusicState.BareStationId(now.Id);
        if (id.Length == 0)
        {
            return;
        }

        state.ToggleFavorite(id);
        if (state.IsFavorite(id))
        {
            if (now.Live)
            {
                var station = FindCommunity(id);
                if (string.IsNullOrEmpty(station.Id))
                {
                    station = new CommunityStation(id, now.Title, now.Detail, state.Genre, true, now.StreamUrl, 0,
                        string.Empty, now.ArtPath);
                }

                state.RememberSavedLive(FollowedStationSnap.From(station));
            }
            else
            {
                var station = FindPublic(id) ?? TuneAsPublic(now);
                state.RememberSavedRadio(MusicStationMark.From(station with { Id = id }));
            }
        }

        state.Save(paths);
    }

    private PublicStation? FindPublic(string id)
    {
        var bare = MusicState.BareStationId(id);
        if (bare.Length == 0)
        {
            return null;
        }

        foreach (var genre in publicRadio.Genres)
        {
            var stations = publicRadio.Stations(genre);
            for (var index = 0; index < stations.Count; index++)
            {
                if (string.Equals(stations[index].Id, bare, StringComparison.OrdinalIgnoreCase))
                {
                    return stations[index];
                }
            }
        }

        foreach (var mark in state.SavedRadio.Concat(state.LikedRadio))
        {
            if (string.Equals(mark.Id, bare, StringComparison.OrdinalIgnoreCase))
            {
                return mark.ToPublic();
            }
        }

        return null;
    }

    private static PublicStation TuneAsPublic(HandsetTune now)
    {
        var detail = now.Detail ?? string.Empty;
        var cut = detail.IndexOf(" · ", StringComparison.Ordinal);
        var genre = cut > 0 ? detail[..cut] : detail;
        var place = cut > 0 ? detail[(cut + 3)..] : string.Empty;
        return new PublicStation(MusicState.BareStationId(now.Id), now.Title ?? string.Empty, genre, place,
            now.StreamUrl ?? string.Empty, 0, now.ArtPath ?? string.Empty);
    }

    public bool Followed(string id) => state.FollowsStationId(id);

    public bool CanFollow(HandsetTune now) => now.Live && (now.Id ?? string.Empty).Length > 0;

    public void ToggleFollow(HandsetTune now)
    {
        if (!CanFollow(now))
        {
            return;
        }

        var id = now.Id ?? string.Empty;
        var station = FindCommunity(id);
        if (string.IsNullOrEmpty(station.Id))
        {
            station = new CommunityStation(id, now.Title, now.Detail, state.Genre, true, now.StreamUrl, 0,
                string.Empty, now.ArtPath);
        }

        FollowPerson(MusicRoster.FromLive(station));
    }

    internal static readonly string[] ReportReasons =
    {
        "Select reason",
        "Spam",
        "Harassment or bullying",
        "Hate speech",
        "Inappropriate content",
        "Impersonation",
        "Scam or fraud",
        "Something else",
    };

    private void OpenPersonReport(MusicPerson person)
    {
        if (person.Id.StartsWith("live:", StringComparison.OrdinalIgnoreCase))
        {
            OpenReport("radio_station", MusicState.BareStationId(person.Id), person.Name);
            return;
        }

        OpenReport("user", person.Id.Length > 0 ? person.Id : person.Name, person.Name);
    }

    private void OpenReport(string stationId, string title) =>
        OpenReport("radio_station", stationId, title);

    private void OpenReport(string kind, string targetId, string title)
    {
        state.ReportOpen = true;
        state.ReportFresh = true;
        state.ReportReason = 0;
        state.ReportDetail = string.Empty;
        state.ReportKind = kind;
        state.ReportTarget = targetId;
        state.ReportTitle = title;
    }

    private void CloseReport()
    {
        state.ReportOpen = false;
        state.ReportFresh = false;
        state.ReportReason = 0;
        state.ReportDetail = string.Empty;
        state.ReportKind = "radio_station";
        state.ReportTarget = string.Empty;
        state.ReportTitle = string.Empty;
    }

    private void SubmitReport()
    {
        if (state.ReportReason <= 0)
        {
            return;
        }

        var target = state.ReportKind == "radio_station"
            ? MusicState.BareStationId(state.ReportTarget)
            : state.ReportTarget.Trim();
        var reason = StaffReports.ReasonAt(state.ReportReason);
        var kind = state.ReportKind.Length > 0 ? state.ReportKind : "radio_station";
        var detail = kind + ": " + (state.ReportTitle.Length > 0 ? state.ReportTitle : "Unknown") +
                     "\nId: " + target +
                     (state.ReportDetail.Trim().Length > 0 ? "\n\n" + state.ReportDetail.Trim() : string.Empty);
        if (target.Length == 0)
        {
            CloseReport();
            return;
        }

        StaffReportDispatch.File(pearl, desk, game.Character.Name, game.Character.WorldName, kind, target, reason,
            detail);
        CloseReport();
    }

    private void DrawRadioLike(in AppletFrame frame, Rect area, PublicStation station)
    {
        var id = MusicState.BareStationId(station.Id);
        if (id.Length == 0 || area.Width < 8f || area.Height < 8f)
        {
            return;
        }

        var liked = community.StationLiked(id);
        var color = liked ? MusicChrome.LikePink : MusicChrome.Ink;
        frame.Paint.Fill(area, new Vector4(0f, 0f, 0f, 0.55f), frame.Units(8f));
        frame.Text.DrawIn(area.TopSlice(area.Height * 0.55f), liked ? "♥" : "♡",
            new TextStyle(FontRole.CaptionStrong, color, TextAlign.Center));
        frame.Text.DrawIn(area.BottomSlice(area.Height * 0.45f), RadioLikes(station).ToString(),
            new TextStyle(FontRole.Caption, color, TextAlign.Center));
    }

    private bool TapRadioLike(in AppletFrame frame, Rect area, PublicStation station) =>
        TapStationLike(frame, area, station.Id);

    private int RadioLikes(PublicStation station)
    {
        var id = MusicState.BareStationId(station.Id);
        var votes = Math.Max(0, station.Votes);
        if (id.Length == 0)
        {
            return votes;
        }

        var local = community.StationLikes(id);
        if (community.StationLiked(id))
        {
            return Math.Max(local, votes + 1);
        }

        return local > 0 ? local : votes;
    }

    private void DrawStationLike(in AppletFrame frame, Rect area, string stationId, Vector4? ink = null)
    {
        var id = MusicState.BareStationId(stationId);
        if (id.Length == 0)
        {
            return;
        }

        var liked = community.StationLiked(id);
        var color = ink ?? (liked ? MusicChrome.LikePink : MusicChrome.LikePink with { W = 0.72f });
        frame.Text.DrawIn(area.TopSlice(area.Height * 0.58f), liked ? "♥" : "♡",
            new TextStyle(FontRole.Title, color, TextAlign.Center));
        frame.Text.DrawIn(area.BottomSlice(area.Height * 0.42f), community.StationLikes(id).ToString(),
            new TextStyle(FontRole.Caption, color, TextAlign.Center));
    }

    private bool TapStationLike(in AppletFrame frame, Rect area, string stationId)
    {
        var id = MusicState.BareStationId(stationId);
        if (id.Length == 0 || !frame.Input.ConsumeClick(area))
        {
            return false;
        }

        community.ToggleStationLike(id);
        return true;
    }

    private void DrawDiscover(in AppletFrame frame, Rect area)
    {
        var stack = MusicChrome.BeginSheet(frame, area, state, frame.Units(10f));
        try
        {
            stack.Take(frame.Units(12f));
            var hunt = stack.Take(frame.Units(38f));
            var query = state.Search.Trim();
            if (query.Length > 0)
            {
                var liveHits = SearchLiveHits(query);
                var hits = SearchHits(query);
                WarmStationArt(hits);
                if (liveHits.Length > 0)
                {
                    MusicChrome.Kicker(frame, stack.Take(frame.Units(18f)),
                        liveHits.Length == 1 ? "1 live station" : liveHits.Length + " live stations");
                    for (var index = 0; index < liveHits.Length; index++)
                    {
                        DrawCommunityRow(frame, ref stack, liveHits[index]);
                    }
                }

                MusicChrome.Kicker(frame, stack.Take(frame.Units(18f)), hits.Count + " stations");
                DrawPublicRows(frame, ref stack, hits, Math.Min(hits.Count, 16));
            }
            else
            {
                MusicChrome.Kicker(frame, stack.Take(frame.Units(20f)), "Browse stations");
                DrawGenreMasonry(frame, stack.Take(GenreMasonryHeight(frame)));
            }

            frame.Paint.Fill(hunt, MusicChrome.CardHi, hunt.Height * 0.5f);
            state.Search = frame.TextField.Draw("music-search", hunt.Inset(new Edges(frame.Units(14f), 0f)),
                state.Search, "Search");
        }
        finally
        {
            MusicChrome.EndSheet(frame, area, state, ref stack);
        }
    }

    private void DrawDiscoverLive(in AppletFrame frame, Rect area)
    {
        var stack = new Stack(area, StackAxis.Vertical, frame.Units(8f));
        var board = MergedBoard();
        var live = board.Where(static row => row.Live).ToArray();
        var dark = board.Where(static row => !row.Live).ToArray();
        var status = stack.Take(frame.Units(16f));
        frame.Text.DrawIn(status.LeftSlice(status.Width * 0.62f),
            "Icecast and Twitch. Listening and watching stay separate.",
            new TextStyle(FontRole.Caption, MusicChrome.Mute));
        frame.Text.DrawIn(status.RightSlice(status.Width * 0.38f), live.Length + " on air",
            new TextStyle(FontRole.Caption, MusicChrome.Mute, TextAlign.Right));

        var go = stack.Take(frame.Units(42f));
        if (state.StationName.Length > 0)
        {
            MusicChrome.Primary(frame, go, community.Broadcasting ? "You are live · Manage" : "Go live");
            if (frame.Input.ConsumeClick(go))
            {
                state.Open(MusicPage.DjDash);
            }
        }
        else
        {
            MusicChrome.Plate(frame, go, frame.Units(12f));
            frame.Text.DrawIn(go, "Create your station",
                new TextStyle(FontRole.BodyStrong, MusicChrome.Purple, TextAlign.Center));
            if (frame.Input.ConsumeClick(go))
            {
                state.Dj = true;
                state.Open(MusicPage.SetupDj);
            }
        }

        if (live.Length > 0)
        {
            MusicChrome.Kicker(frame, stack.Take(frame.Units(14f)), "FEATURED");
            DrawLiveFeature(frame, stack.Take(frame.Units(88f)), live[0]);
            MusicChrome.Kicker(frame, stack.Take(frame.Units(14f)), "ON AIR");
            DrawLiveBoard(frame, stack.Take(frame.Units(live.Length * 64f)), live);
        }

        MusicChrome.Kicker(frame, stack.Take(frame.Units(14f)), "STATIONS");
        if (dark.Length == 0 && live.Length == 0)
        {
            var empty = stack.TakeRemaining();
            MusicChrome.Plate(frame, empty, frame.Units(14f));
            var copy = empty.Inset(frame.Units(14f));
            frame.Text.DrawIn(copy.TopSlice(frame.Units(22f)), "No community stations yet",
                new TextStyle(FontRole.BodyStrong, MusicChrome.Ink));
            frame.Text.DrawIn(copy.Inset(new Edges(0f, frame.Units(26f), 0f, 0f)),
                community.Notice.Length > 0
                    ? community.Notice
                    : streams.Notice.Length > 0
                        ? streams.Notice
                        : "Create your station, then Go live. Community Twitch stations from Rolladeck land here too.",
                new TextStyle(FontRole.Caption, MusicChrome.Mute));
            if (frame.Input.ConsumeClick(empty) && state.StationName.Length == 0)
            {
                state.Dj = true;
                state.Open(MusicPage.SetupDj);
            }

            return;
        }

        if (dark.Length == 0)
        {
            DrawHint(frame, ref stack, "Every other station is on air.");
            return;
        }

        DrawLiveBoard(frame, stack.TakeRemaining(), dark);
    }

    private void DrawLiveFeature(in AppletFrame frame, Rect area, CommunityStation station)
    {
        MusicChrome.GlowPlate(frame, area, frame.Units(14f), station.Live);
        var inset = area.Inset(frame.Units(12f));
        DrawStationArt(frame, inset.LeftSlice(frame.Units(56f)).Inset(new Edges(0f, 0f, frame.Units(8f), 0f)),
            station.ArtPath, station.Id, station.Name);
        var body = inset.Inset(new Edges(frame.Units(60f), 0f, 0f, 0f));
        if (station.Live)
        {
            MusicChrome.LiveMark(frame, body.TopSlice(frame.Units(16f)).LeftSlice(frame.Units(40f)));
        }
        else
        {
            MusicChrome.OffMark(frame, body.TopSlice(frame.Units(16f)).LeftSlice(frame.Units(56f)));
        }

        frame.Text.DrawEllipsized(body.Inset(new Edges(0f, frame.Units(20f), 0f, frame.Units(20f))),
            station.Name.Length > 0 ? station.Name : "Station",
            new TextStyle(FontRole.Title, MusicChrome.Ink));
        var hearN = ShownListeners(station.Listeners, station.Live);
        var shown = station with { Listeners = hearN };
        var hear = body.BottomSlice(frame.Units(16f)).RightSlice(
            MathF.Min(body.Width * 0.5f, StationAudienceWidth(frame, shown, true)));
        frame.Text.DrawEllipsized(body.BottomSlice(frame.Units(16f)).Inset(new Edges(0f, 0f, hear.Width + frame.Units(4f), 0f)),
            (station.Host.Length > 0 ? station.Host : "DJ") +
            (ShownStationGenre(station).Length > 0 ? " · " + ShownStationGenre(station) : string.Empty) +
            (station.VenueLine.Length > 0 ? " · " + station.VenueLine : string.Empty),
            new TextStyle(FontRole.Caption, MusicChrome.Mute));
        DrawStationAudience(frame, hear, shown, compact: true);
        var profile = body.TopSlice(frame.Units(16f)).RightSlice(frame.Units(72f));
        frame.Text.DrawIn(profile, "Profile",
            new TextStyle(FontRole.CaptionStrong, MusicChrome.Purple, TextAlign.Right));
        if (frame.Input.ConsumeClick(profile))
        {
            ShowProfile("live:" + station.Id);
            return;
        }

        if (frame.Input.ConsumeClick(area))
        {
            OpenStation(station);
        }
    }

    private void DrawLiveBoard(in AppletFrame frame, Rect area, IReadOnlyList<CommunityStation> live)
    {
        if (area.IsEmpty)
        {
            return;
        }

        var rowH = frame.Units(56f);
        var gap = frame.Units(8f);
        var stride = rowH + gap;
        var content = live.Count * stride;
        MusicChrome.Wheel(frame, area, state, content);
        frame.Paint.PushClip(area);
        try
        {
            for (var index = 0; index < live.Count; index++)
            {
                var y = area.Min.Y - state.Scroll + index * stride;
                var row = Rect.FromSize(new Vector2(area.Min.X, y), new Vector2(area.Width, rowH));
                if (row.Max.Y < area.Min.Y || row.Min.Y > area.Max.Y)
                {
                    continue;
                }

                DrawCommunityRow(frame, row, live[index]);
            }
        }
        finally
        {
            frame.Paint.PopClip();
        }
    }

    private void DrawLibrary(in AppletFrame frame, Rect area)
    {
        var stack = MusicChrome.BeginSheet(frame, area, state, frame.Units(10f));
        try
        {
            MusicChrome.Title(frame, stack.Take(frame.Units(28f)), "Library");
            DrawPlaylistShelf(frame, ref stack);
            MusicChrome.Kicker(frame, stack.Take(frame.Units(20f)), "Following");
            var liveFollows = FollowedBoard().Where(static row => row.Live).ToArray();
            if (liveFollows.Length == 0)
            {
                DrawHint(frame, ref stack, "Follow a live station and it stays here while they are on air.");
            }
            else
            {
                for (var index = 0; index < liveFollows.Length; index++)
                {
                    DrawCommunityRow(frame, ref stack, liveFollows[index]);
                }
            }

            var savedLive = SavedLiveStations();
            var savedRadio = SavedRadioStations();
            if (savedLive.Length > 0 || savedRadio.Count > 0)
            {
                MusicChrome.Kicker(frame, stack.Take(frame.Units(20f)), "Saved");
                for (var index = 0; index < savedLive.Length; index++)
                {
                    DrawCommunityRow(frame, ref stack, savedLive[index]);
                }

                if (savedRadio.Count > 0)
                {
                    DrawPublicRows(frame, ref stack, savedRadio, savedRadio.Count, openPlayer: true);
                }
            }

            MusicChrome.Kicker(frame, stack.Take(frame.Units(20f)), "Likes");
            var favorites = LikedStations();
            if (favorites.Count == 0)
            {
                DrawHint(frame, ref stack, "Heart a station on Feed or Search to keep it here.");
            }
            else
            {
                DrawPublicRows(frame, ref stack, favorites, favorites.Count, openPlayer: true);
            }

            MusicChrome.Kicker(frame, stack.Take(frame.Units(20f)), "Your genres");
            var picks = state.Interests.Count > 0 ? state.Interests : MusicState.Genres.Take(3).ToList();
            for (var index = 0; index < picks.Count; index++)
            {
                publicRadio.Ensure(picks[index]);
                var stations = publicRadio.Stations(picks[index]);
                if (stations.Count == 0)
                {
                    continue;
                }

                if (MusicChrome.Section(frame, stack.Take(frame.Units(22f)), picks[index]))
                {
                    var genre = Array.IndexOf(MusicState.Genres, picks[index]);
                    OpenGenre(genre < 0 ? 0 : genre);
                    return;
                }

                DrawPublicRows(frame, ref stack, stations, 3, openPlayer: true);
            }
        }
        finally
        {
            MusicChrome.EndSheet(frame, area, state, ref stack);
        }
    }

    private void DrawNav(in AppletFrame frame, Rect strip)
    {
        frame.Paint.Fill(strip, MusicChrome.GroundHi);
        var items = strip.Inset(new Edges(0f, frame.Units(4f), 0f, frame.Units(12f)));
        var w = items.Width / Tabs.Length;
        for (var index = 0; index < Tabs.Length; index++)
        {
            var cell = Rect.FromSize(new Vector2(items.Min.X + w * index, items.Min.Y),
                new Vector2(w, items.Height));
            var on = (int)state.Tab == index;
            var ink = on ? MusicChrome.Purple : MusicChrome.Mute;
            var mark = cell.TopSlice(frame.Units(28f));
            if (!DrawTabGlyph(frame, mark, TabGlyphs[index], ink))
            {
                frame.Text.DrawIn(mark, TabMarks[index],
                    new TextStyle(FontRole.Body, ink, TextAlign.Center));
            }

            frame.Text.DrawIn(cell.BottomSlice(frame.Units(18f)), Tabs[index],
                new TextStyle(FontRole.Caption, on ? MusicChrome.Ink : MusicChrome.Mute, TextAlign.Center));

            if (frame.Input.ConsumeClick(cell))
            {
                OpenTab((MusicTab)index);
            }
        }
    }

    private static bool DrawTabGlyph(in AppletFrame frame, Rect area, string? file, Vector4 ink)
    {
        if (string.IsNullOrWhiteSpace(file))
        {
            return false;
        }

        var texture = frame.Textures.FromFile(AppIconCatalog.Glyph(frame.Paths, file));
        if (texture is not { IsReady: true })
        {
            return false;
        }

        var side = MathF.Min(area.Width, area.Height) * 0.567f;
        var dest = Rect.FromSize(area.Center - new Vector2(side * 0.5f, side * 0.5f), new Vector2(side, side));
        frame.Paint.Image(texture, dest, ink);
        return true;
    }

    private void DrawMini(in AppletFrame frame, Rect row)
    {
        var now = audio.Now;
        var playing = audio.Phase == HandsetAudioPhase.Playing;
        frame.Paint.Fill(row, MusicChrome.Card, row.Height * 0.5f);
        var play = row.LeftSlice(row.Height);
        MusicChrome.PlayRing(frame, play, playing, playing ? 0.35f : 0f);
        var heart = row.RightSlice(frame.Units(40f));
        var hearN = NowListenerCount(now.Live, now.Live ? FindCommunity(now.Id) : default);
        var hear = row.Inset(new Edges(0f, frame.Units(6f), heart.Width + frame.Units(4f), frame.Units(6f)))
            .RightSlice(MathF.Min(frame.Units(52f), MusicChrome.ListenerWidth(frame, hearN, true)));
        var saved = now.Id.Length > 0 && state.IsFavorite(now.Id);
        frame.Text.DrawIn(heart, saved ? "♥" : "♡",
            new TextStyle(FontRole.Title, saved ? MusicChrome.Purple : MusicChrome.Ink, TextAlign.Center));
        var copy = row.Inset(new Edges(row.Height, frame.Units(8f), heart.Width + hear.Width + frame.Units(8f),
            frame.Units(8f)));
        if (now.Id.Length > 0)
        {
            MusicChrome.ListenerCount(frame, hear, hearN, compact: true);
        }
        frame.Text.DrawEllipsized(copy.TopSlice(frame.Units(16f)),
            now.Title.Length > 0 ? now.Title : "Radio", new TextStyle(FontRole.CaptionStrong, MusicChrome.Ink));
        var line = audio.Phase == HandsetAudioPhase.Failed ? audio.Notice :
            audio.Phase == HandsetAudioPhase.Paused ? "Paused" :
            audio.Phase == HandsetAudioPhase.Buffering ? "Connecting…" : now.Detail;
        frame.Text.DrawEllipsized(copy.BottomSlice(frame.Units(14f)), line,
            new TextStyle(FontRole.Caption, MusicChrome.Mute));
        if (frame.Input.ConsumeClick(play))
        {
            audio.Toggle();
        }
        else if (now.Id.Length > 0 && frame.Input.ConsumeClick(heart))
        {
            ToggleSave(now);
        }
        else if (frame.Input.ConsumeClick(row))
        {
            ShowNowPlayingInGenre();
        }
    }

    private IReadOnlyList<PublicStation> FilteredStations()
    {
        var stations = publicRadio.Stations(state.Genre);
        var query = state.Search.Trim();
        if (query.Length == 0)
        {
            return stations;
        }

        return stations.Where(row =>
                row.Title.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                row.Place.Contains(query, StringComparison.OrdinalIgnoreCase))
            .ToArray();
    }

    private IReadOnlyList<PublicStation> SearchHits(string query) =>
        MusicState.Genres
            .SelectMany(publicRadio.Stations)
            .Where(row =>
                row.Title.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                row.Place.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                row.Genre.Contains(query, StringComparison.OrdinalIgnoreCase))
            .GroupBy(row => row.Id, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToArray();

    private void DrawStationList(in AppletFrame frame, Rect area, IReadOnlyList<PublicStation> stations)
    {
        if (area.Height < frame.Units(48f))
        {
            return;
        }

        var rowH = frame.Units(54f);
        var gap = frame.Units(8f);
        var stride = rowH + gap;
        var step = frame.Units(28f);
        var cycle = area.RightSlice(step);
        var list = area.Inset(new Edges(0f, 0f, step + frame.Units(4f), 0f));
        if (stations.Count == 0)
        {
            var empty = new Stack(list, StackAxis.Vertical, gap);
            DrawHint(frame, ref empty,
                publicRadio.Busy ? "Loading stations…" : "No stations in this genre yet.");
            return;
        }

        var content = stations.Count * stride;
        MusicChrome.Wheel(frame, list, state, content);
        var maxScroll = MathF.Max(0f, content - list.Height);
        var up = cycle.TopSlice(frame.Units(32f));
        var down = cycle.BottomSlice(frame.Units(32f));
        MusicChrome.GlowPlate(frame, up, frame.Units(8f), false);
        MusicChrome.GlowPlate(frame, down, frame.Units(8f), false);
        frame.Text.DrawIn(up, "▲", new TextStyle(FontRole.CaptionStrong, MusicChrome.Purple, TextAlign.Center));
        frame.Text.DrawIn(down, "▼", new TextStyle(FontRole.CaptionStrong, MusicChrome.Purple, TextAlign.Center));
        if (frame.Input.ConsumeClick(up))
        {
            state.Scroll = Math.Clamp(state.Scroll - stride, 0f, maxScroll);
        }
        else if (frame.Input.ConsumeClick(down))
        {
            state.Scroll = Math.Clamp(state.Scroll + stride, 0f, maxScroll);
        }
        var first = (int)(state.Scroll / stride);
        var last = Math.Min(stations.Count - 1, first + (int)(list.Height / stride) + 1);
        var dragging = frame.Input.IsHeld() && MathF.Abs(frame.Input.PointerDelta.Y) > 4f;
        if (!list.IsEmpty)
        {
            frame.Paint.PushClip(list);
            try
            {
                for (var index = Math.Max(0, first); index <= last; index++)
                {
                    var y = list.Min.Y - state.Scroll + index * stride;
                    var row = Rect.FromSize(new Vector2(list.Min.X, y), new Vector2(list.Width, rowH));
                    DrawPublicRow(frame, row, stations[index], !dragging);
                }
            }
            finally
            {
                frame.Paint.PopClip();
            }
        }
        var shown = Math.Min(stations.Count, first + 1);
        frame.Text.DrawIn(cycle.Inset(new Edges(0f, frame.Units(36f))),
            shown + "/" + stations.Count,
            new TextStyle(FontRole.Caption, MusicChrome.Mute, TextAlign.Center));
    }

    private static float GenreStripHeight(in AppletFrame frame)
    {
        var rows = (MusicState.Genres.Length + 4) / 5;
        return rows * frame.Units(28f) + Math.Max(0, rows - 1) * frame.Units(6f);
    }

    private void DrawGenreChips(in AppletFrame frame, Rect chips)
    {
        const int cols = 5;
        var chipW = chips.Width / cols;
        var chipH = frame.Units(28f);
        var gap = frame.Units(6f);
        for (var index = 0; index < MusicState.Genres.Length; index++)
        {
            var col = index % cols;
            var row = index / cols;
            var chip = Rect.FromSize(
                new Vector2(chips.Min.X + chipW * col + frame.Units(2f), chips.Min.Y + (chipH + gap) * row),
                new Vector2(chipW - frame.Units(4f), chipH));
            var tag = MusicState.Genres[index];
            if (MusicChrome.Chip(frame, chip, tag, state.GenreIndex == index))
            {
                state.GenreIndex = index;
                publicRadio.Ensure(tag);
                state.Scroll = 0f;
            }
        }
    }

    private void DrawPublicRows(in AppletFrame frame, ref Stack stack, IReadOnlyList<PublicStation> stations, int max,
        bool openPlayer = false)
    {
        var count = Math.Min(stations.Count, max);
        for (var index = 0; index < count; index++)
        {
            DrawPublicRow(frame, stack.Take(frame.Units(52f)), stations[index], true, openPlayer: openPlayer);
        }
    }

    private void DrawPublicRow(in AppletFrame frame, Rect row, PublicStation station, bool allowOpen,
        string? removeFrom = null, bool openPlayer = false)
    {
        var current = string.Equals(audio.Now.Id, station.Id, StringComparison.OrdinalIgnoreCase);
        var playing = current && audio.Phase is HandsetAudioPhase.Playing or HandsetAudioPhase.Buffering;
        var saved = state.IsFavorite(station.Id);
        MusicChrome.GlowPlate(frame, row, frame.Units(12f), current);
        var inset = row.Inset(frame.Units(8f));
        var play = inset.LeftSlice(frame.Units(36f));
        DrawStationArt(frame, play, station.ArtUrl, station.Id, station.Title);
        frame.Paint.FillCircle(play.Center, frame.Units(9f), current ? MusicChrome.Purple : new Vector4(0f, 0f, 0f, 0.45f));
        frame.Text.DrawIn(play, playing ? "❚❚" : "▶",
            new TextStyle(FontRole.CaptionStrong, MusicChrome.Ink, TextAlign.Center, scale: 0.82f));
        var star = inset.RightSlice(frame.Units(30f));
        var extra = inset.Inset(new Edges(0f, 0f, star.Width + frame.Units(4f), 0f)).RightSlice(frame.Units(30f));
        var hearN = ShownListeners(station.Listeners, current);
        var hearW = MathF.Min(frame.Units(52f), MusicChrome.ListenerWidth(frame, hearN, true));
        var hear = inset.Inset(new Edges(0f, frame.Units(6f), star.Width + extra.Width + frame.Units(6f),
            frame.Units(6f))).RightSlice(hearW);
        var body = inset.Inset(new Edges(frame.Units(42f), 0f,
            star.Width + extra.Width + hearW + frame.Units(10f), 0f));
        frame.Text.DrawEllipsized(body.TopSlice(frame.Units(18f)), station.Title,
            new TextStyle(FontRole.BodyStrong, MusicChrome.Ink));
        frame.Text.DrawEllipsized(body.BottomSlice(frame.Units(16f)),
            station.Place + (station.Bitrate > 0 ? " · " + station.Bitrate + "k" : string.Empty) +
            (current ? playing ? " · Playing" : " · Paused" : string.Empty),
            new TextStyle(FontRole.Caption, MusicChrome.Mute));
        MusicChrome.ListenerCount(frame, hear, hearN, compact: true);
        frame.Paint.Fill(star, saved ? MusicChrome.PurpleDim : MusicChrome.CardHi, frame.Units(8f));
        frame.Text.DrawIn(star, saved ? "♥" : "♡",
            new TextStyle(FontRole.BodyStrong, saved ? MusicChrome.Purple : MusicChrome.Mute, TextAlign.Center));
        frame.Paint.Fill(extra, MusicChrome.CardHi, frame.Units(8f));
        frame.Text.DrawIn(extra, removeFrom is { Length: > 0 } ? "×" : "+",
            new TextStyle(FontRole.BodyStrong, MusicChrome.Ink, TextAlign.Center));
        if (frame.Input.ConsumeClick(star))
        {
            state.ToggleFavorite(station.Id);
            if (state.IsFavorite(station.Id))
            {
                state.RememberSavedRadio(MusicStationMark.From(station));
            }

            state.Save(paths);
            return;
        }

        if (frame.Input.ConsumeClick(extra))
        {
            if (removeFrom is { Length: > 0 })
            {
                state.FindPlaylist(removeFrom)?.Drop(station.Id);
                state.Save(paths);
            }
            else
            {
                OfferPlaylist(station);
            }

            return;
        }

        var ring = Rect.FromSize(play.Center - new Vector2(frame.Units(11f)), new Vector2(frame.Units(22f)));
        if (frame.Input.ConsumeClick(ring))
        {
            if (current)
            {
                audio.Toggle();
            }
            else
            {
                TunePublic(station);
            }

            return;
        }

        if (allowOpen && (frame.Input.ConsumeClick(play) || frame.Input.ConsumeClick(row)))
        {
            if (openPlayer)
            {
                OpenPublicPlayer(station);
                return;
            }

            ShowPublicInGenre(station);
        }
    }

    private void OpenPublicPlayer(PublicStation station)
    {
        TunePublic(station);
        state.Open(MusicPage.Player);
    }

    private void DrawCommunityCard(in AppletFrame frame, Rect area, CommunityStation station)
    {
        MusicChrome.GlowPlate(frame, area, frame.Units(14f), station.Live);
        var inset = area.Inset(frame.Units(10f));
        if (station.Live)
        {
            MusicChrome.LiveMark(frame, inset.TopSlice(frame.Units(16f)).LeftSlice(frame.Units(40f)));
        }

        frame.Text.DrawEllipsized(inset.Inset(new Edges(0f, frame.Units(20f), 0f, frame.Units(16f))), station.Name,
            new TextStyle(FontRole.BodyStrong, MusicChrome.Ink));
        var hearN = ShownListeners(station.Listeners, station.Live);
        var shown = station with { Listeners = hearN };
        var hear = inset.BottomSlice(frame.Units(16f)).RightSlice(
            MathF.Min(inset.Width * 0.45f, StationAudienceWidth(frame, shown, true)));
        frame.Text.DrawIn(inset.BottomSlice(frame.Units(14f)).Inset(new Edges(0f, 0f, hear.Width + frame.Units(4f), 0f)),
            ShownStationGenre(station).Length > 0 ? ShownStationGenre(station) : station.Host,
            new TextStyle(FontRole.Caption, MusicChrome.Mute));
        DrawStationAudience(frame, hear, shown, compact: true);
        if (frame.Input.ConsumeClick(area))
        {
            OpenStation(station);
        }
    }

    private void DrawCommunityRow(in AppletFrame frame, ref Stack stack, CommunityStation station) =>
        DrawCommunityRow(frame, stack.Take(frame.Units(52f)), station);

    private void DrawCommunityRow(in AppletFrame frame, Rect row, CommunityStation station)
    {
        var face = row.LeftSlice(frame.Units(52f)).Inset(frame.Units(6f));
        MusicChrome.GlowPlate(frame, row, frame.Units(12f), station.Live);
        DrawStationArt(frame, face, station.ArtPath, station.Id, station.Name);
        var follow = row.Inset(frame.Units(8f)).RightSlice(frame.Units(78f)).BottomSlice(frame.Units(22f));
        var badge = row.Inset(frame.Units(8f)).RightSlice(station.Live ? frame.Units(40f) : frame.Units(56f))
            .TopSlice(frame.Units(18f));
        var body = row.Inset(new Edges(frame.Units(56f), frame.Units(8f), frame.Units(86f),
            frame.Units(8f)));
        frame.Text.DrawEllipsized(body.TopSlice(frame.Units(18f)), station.Name,
            new TextStyle(FontRole.BodyStrong, MusicChrome.Ink));
        var hearN = ShownListeners(station.Listeners,
            string.Equals(audio.Now.Id, station.Id, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(MusicState.BareStationId(audio.Now.Id), station.Id, StringComparison.OrdinalIgnoreCase));
        var shown = station with { Listeners = hearN };
        var hear = body.RightSlice(MathF.Min(body.Width * 0.42f, StationAudienceWidth(frame, shown, true)))
            .BottomSlice(frame.Units(16f));
        frame.Text.DrawEllipsized(body.Inset(new Edges(0f, 0f, hear.Width + frame.Units(4f), 0f)).BottomSlice(frame.Units(16f)),
            (station.Host.Length > 0 ? station.Host : "DJ") +
            (ShownStationGenre(station).Length > 0 ? " · " + ShownStationGenre(station) : string.Empty) +
            (station.VenueLine.Length > 0 ? " · " + station.VenueLine : string.Empty),
            new TextStyle(FontRole.Caption, MusicChrome.Mute));
        DrawStationAudience(frame, hear, shown, compact: true);
        if (station.Live)
        {
            MusicChrome.LiveMark(frame, badge);
        }
        else
        {
            MusicChrome.OffMark(frame, badge);
        }

        if (MusicChrome.FollowChip(frame, follow, state.FollowsStationId(station.Id)))
        {
            ToggleFollowLive(station);
            return;
        }

        if (frame.Input.ConsumeClick(face))
        {
            ShowProfile("live:" + station.Id);
            return;
        }

        if (frame.Input.ConsumeClick(row))
        {
            OpenStation(station);
        }
    }

    private static void DrawHint(in AppletFrame frame, ref Stack stack, string copy)
    {
        if (copy.Length == 0)
        {
            return;
        }

        frame.Text.DrawIn(stack.Take(frame.Units(36f)), copy, new TextStyle(FontRole.Caption, MusicChrome.Mute));
    }

    private void DrawOnboard(in AppletFrame frame, Rect area)
    {
        var stack = MusicChrome.BeginSheet(frame, area, state, frame.Units(10f));
        try
        {
        MusicChrome.Title(frame, stack.Take(frame.Units(28f)), "Choose Your Roles");
        frame.Text.DrawIn(stack.Take(frame.Units(32f)), "Listener for radio by genre. DJ to go live on Pearlgate.",
            new TextStyle(FontRole.Caption, MusicChrome.Mute));
        DrawRolePick(frame, stack.Take(frame.Units(64f)), "LISTENER",
            "Tune US stations by genre and save favorites.", isListener: true);
        DrawRolePick(frame, stack.Take(frame.Units(64f)), "DJ",
            "Name a station and go live for the community.", isListener: false, isDj: true);
        DrawRolePick(frame, stack.Take(frame.Units(64f)), "VENUE",
            "Hold a house name for later event tools.", isVenue: true);
        var go = stack.Take(frame.Units(44f));
        MusicChrome.Primary(frame, go, "Continue to Setup");
        if (frame.Input.ConsumeClick(go) && (state.Listener || state.Dj || state.Venue))
        {
            state.Scroll = 0f;
            state.SheetHeight = 0f;
            if (state.Listener)
            {
                state.Page = MusicPage.SetupListener;
            }
            else if (state.Dj)
            {
                state.Page = MusicPage.SetupDj;
            }
            else
            {
                state.Onboarded = true;
                state.Page = MusicPage.Tabs;
                state.Save(paths);
            }
        }
        }
        finally
        {
            MusicChrome.EndSheet(frame, area, state, ref stack);
        }
    }

    private void DrawRolePick(in AppletFrame frame, Rect row, string title, string detail, bool isListener = false,
        bool isDj = false, bool isVenue = false)
    {
        var on = isListener ? state.Listener : isDj ? state.Dj : state.Venue;
        DrawRoleCard(frame, row, title, detail, on);
        if (frame.Input.ConsumeClick(row))
        {
            if (isListener)
            {
                state.Listener = !state.Listener;
            }
            else if (isDj)
            {
                state.Dj = !state.Dj;
            }
            else
            {
                state.Venue = !state.Venue;
            }
        }
    }

    public static void DrawRoleCard(in AppletFrame frame, Rect row, string title, string detail, bool on)
    {
        MusicChrome.GlowPlate(frame, row, frame.Units(14f), on);
        var inset = row.Inset(frame.Units(12f));
        frame.Text.DrawIn(inset.TopSlice(frame.Units(18f)), title,
            new TextStyle(FontRole.CaptionStrong, MusicChrome.Purple));
        frame.Text.DrawEllipsized(inset.Inset(new Edges(0f, 0f, frame.Units(28f), 0f)).BottomSlice(frame.Units(22f)),
            detail, new TextStyle(FontRole.Caption, MusicChrome.Mute));
        var radio = inset.RightSlice(frame.Units(18f)).TopSlice(frame.Units(18f));
        frame.Paint.StrokeCircle(radio.Center, frame.Units(7f), MusicChrome.Purple, frame.Units(1.4f));
        if (on)
        {
            frame.Paint.FillCircle(radio.Center, frame.Units(4f), MusicChrome.Purple);
        }
    }

    private void TunePublic(PublicStation station, bool keepQueue = false)
    {
        if (!keepQueue)
        {
            state.ClearQueue();
        }

        var packed = PackUrls(station.StreamUrl, station.AlternateUrl);
        audio.Play(new HandsetTune(station.Id, station.Title, station.Genre + " · " + station.Place, packed, false,
            station.ArtUrl));
        _ = Task.Run(() =>
        {
            var urls = publicRadio.PlayUrls(station);
            if (urls.Count == 0 || !string.Equals(audio.Now.Id, station.Id, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var next = PackUrls(urls);
            if (next.Length == 0 || string.Equals(next, packed, StringComparison.Ordinal))
            {
                return;
            }

            audio.Play(new HandsetTune(station.Id, station.Title, station.Genre + " · " + station.Place, next, false,
                station.ArtUrl));
        });
    }

    private static string PackUrls(params string[] urls) => PackUrls((IReadOnlyList<string>)urls);

    private static string PackUrls(IReadOnlyList<string> urls)
    {
        var packed = new List<string>();
        for (var index = 0; index < urls.Count; index++)
        {
            var url = urls[index]?.Trim() ?? string.Empty;
            if (url.Length == 0)
            {
                continue;
            }

            var seen = false;
            for (var prior = 0; prior < packed.Count; prior++)
            {
                if (string.Equals(packed[prior], url, StringComparison.OrdinalIgnoreCase))
                {
                    seen = true;
                    break;
                }
            }

            if (!seen)
            {
                packed.Add(url);
            }
        }

        return string.Join('\n', packed);
    }

    private void TuneCommunity(CommunityStation station) =>
        audio.Play(new HandsetTune(station.Id, station.Name, CommunityTuneDetail(station), station.ListenUrl,
            station.Live, station.ArtPath));

    private void OpenStation(CommunityStation station)
    {
        if (station.WatchUrl.Length > 0)
        {
            VenuesChrome.OpenUrl(station.WatchUrl);
            return;
        }

        var url = station.ListenUrl.Length > 0 ? station.ListenUrl : OwnStation(station) ? community.OwnedListenUrl : string.Empty;
        if (url.Length > 0)
        {
            audio.Play(new HandsetTune(station.Id, station.Name, CommunityTuneDetail(station), url, station.Live,
                station.ArtPath));
            state.Open(MusicPage.Player);
            return;
        }

        if (OwnStation(station))
        {
            TuneOwn(station);
            return;
        }

        if (station.Live)
        {
            state.Open(MusicPage.Player);
            return;
        }

        ShowProfile("live:" + station.Id);
    }

    private void TogglePlayback()
    {
        if (audio.Phase is HandsetAudioPhase.Playing or HandsetAudioPhase.Buffering)
        {
            audio.Pause();
            if (audio.Now.StreamUrl.Length == 0)
            {
                sense.StopMonitor();
            }

            return;
        }

        if (audio.Phase == HandsetAudioPhase.Paused)
        {
            if (audio.Now.StreamUrl.Length > 0)
            {
                audio.Resume();
                return;
            }

            HearOwn();
            return;
        }

        var now = audio.Now;
        if (OwnStation(now.Id) || now.Id.Length == 0)
        {
            HearOwn();
            return;
        }

        audio.Toggle();
    }

    private void HearOwn()
    {
        var station = OwnedStation();
        if (string.IsNullOrEmpty(station.Id) && !string.IsNullOrEmpty(audio.Now.Id) &&
            string.IsNullOrEmpty(audio.Now.StreamUrl))
        {
            station = new CommunityStation(audio.Now.Id, audio.Now.Title, audio.Now.Detail, state.Genre, true,
                string.Empty, 0, state.StationBio, state.StationArtPath);
        }

        if (string.IsNullOrEmpty(station.Id))
        {
            return;
        }

        TuneOwn(station);
    }

    private CommunityStation OwnedStation()
    {
        foreach (var row in community.Mine)
        {
            if (OwnStation(row))
            {
                return row;
            }
        }

        foreach (var row in community.Directory)
        {
            if (OwnStation(row))
            {
                return row;
            }
        }

        if (state.StationId.Length == 0 && state.StationName.Length == 0)
        {
            return EmptyCommunity();
        }

        return new CommunityStation(
            state.StationId.Length > 0 ? state.StationId : community.OwnedId,
            state.StationName,
            state.DjName.Length > 0 ? state.DjName : state.DisplayName,
            state.StationGenreLine,
            community.Broadcasting,
            community.OwnedListenUrl,
            0,
            state.StationBio,
            state.StationArtPath);
    }

    private static bool RowOnScreen(in AppletFrame frame, Rect row) =>
        row.Max.Y >= frame.Content.Min.Y - 12f && row.Min.Y <= frame.Content.Max.Y + 12f;

    private bool WantsCapture() =>
        community.Broadcasting || booth.Mixing || state.Page == MusicPage.SetupDj;

    private void SyncBroadcastTap()
    {
        community.UseIcecast(state.IcecastHost, "source", state.IcecastPassword);
        audio.UseSpeaker(display.SpeakerId);

        if (!community.Broadcasting)
        {
            push.Stop();
        }

        if (!WantsCapture())
        {
            if (sense.Listening)
            {
                sense.Stop();
            }

            return;
        }

        if (booth.Mixing)
        {
            if (sense.Monitoring)
            {
                sense.StopMonitor();
            }

            sense.Select(IBroadcastSense.DefaultMixId);
            sense.RoutePhone(display.SpeakerId, display.MicrophoneId);
            var boothNow = Environment.TickCount64;
            if (!sense.Listening && boothNow - lastCaptureTry > 1500)
            {
                lastCaptureTry = boothNow;
                sense.Start();
            }

            var boothIngest = community.OwnedIngestUrl;
            if (community.Broadcasting && boothIngest.Length > 0 && !push.Sending && boothNow - lastPushTry > 1500)
            {
                lastPushTry = boothNow;
                push.Start(
                    boothIngest,
                    state.StationName.Length > 0 ? state.StationName : "Linkpearl",
                    state.StationGenreLine);
            }

            return;
        }

        var tapId = state.CaptureId.Length > 0 ? state.CaptureId : IBroadcastSense.DefaultMixId;
        if (tapId == IBroadcastSense.DefaultMicId)
        {
            tapId = lastSoundTap.Length > 0 ? lastSoundTap : IBroadcastSense.DefaultMixId;
        }

        state.CaptureApp = "sound";

        sense.Select(tapId);
        sense.RoutePhone(display.SpeakerId, display.MicrophoneId);
        if (state.CaptureName.Length == 0 && sense.SelectedName.Length > 0)
        {
            state.CaptureName = sense.SelectedName;
        }

        RouteListenThrough();

        var now = Environment.TickCount64;
        if (!sense.Listening && now - lastCaptureTry > 1500)
        {
            lastCaptureTry = now;
            sense.Start();
        }

        var ingest = community.OwnedIngestUrl;
        if (community.Broadcasting && ingest.Length > 0 && !push.Sending && now - lastPushTry > 1500)
        {
            lastPushTry = now;
            push.Start(
                ingest,
                state.StationName.Length > 0 ? state.StationName : "Linkpearl",
                state.StationGenreLine);
        }
    }

    private void RouteListenThrough()
    {
        sense.MicGain = 1f;
        sense.MonitorGain = 1f;
        sense.StreamGain = 1f;
        audio.UseSpeaker(display.SpeakerId);
        sense.RoutePhone(display.SpeakerId, display.MicrophoneId);

        var url = community.OwnedListenUrl;
        var now = audio.Now;
        var holdingOther = now.StreamUrl.Length > 0 && !OwnStation(now.Id);
        if (community.Broadcasting && url.Length > 0 && !holdingOther)
        {
            if (sense.Monitoring)
            {
                sense.StopMonitor();
            }

            if (audio.Phase == HandsetAudioPhase.Paused)
            {
                return;
            }

            var same = string.Equals(now.StreamUrl, url, StringComparison.Ordinal);
            if (!same || audio.Phase is HandsetAudioPhase.Idle or HandsetAudioPhase.Failed)
            {
                audio.Play(new HandsetTune(
                    community.OwnedId.Length > 0 ? community.OwnedId : state.StationId,
                    state.StationName.Length > 0 ? state.StationName : "Your station",
                    CommunityTuneDetail(OwnedStation()),
                    url,
                    true,
                    state.StationArtPath));
            }

            return;
        }

        if (!sense.Listening)
        {
            return;
        }

        if (!sense.Monitoring)
        {
            sense.StartMonitor();
        }
    }

    private bool OwnStation(CommunityStation station) => OwnStation(station.Id);

    private bool OwnStation(string id) =>
        id.Length > 0 &&
        (string.Equals(id, community.OwnedId, StringComparison.OrdinalIgnoreCase) ||
         string.Equals(id, state.StationId, StringComparison.OrdinalIgnoreCase));

    private string ShownStationGenre(CommunityStation station)
    {
        if (OwnStation(station) && state.StationTags.Count > 0)
        {
            return state.StationGenreLine;
        }

        return MusicState.FormatGenreLine(station.Genre);
    }

    private string CommunityTuneDetail(CommunityStation station)
    {
        var host = station.Host;
        var tags = ShownStationGenre(station);
        if (host.Length > 0 && tags.Length > 0)
        {
            return host + " · " + tags;
        }

        return tags.Length > 0 ? tags : host;
    }

    private void TuneOwn(CommunityStation station)
    {
        community.UseIcecast(state.IcecastHost, "source", state.IcecastPassword);
        audio.UseSpeaker(display.SpeakerId);
        sense.RoutePhone(display.SpeakerId, display.MicrophoneId);
        if (!sense.Listening)
        {
            sense.Start();
        }

        var url = community.OwnedListenUrl.Length > 0 ? community.OwnedListenUrl : station.ListenUrl;
        if (url.Length > 0)
        {
            if (sense.Monitoring)
            {
                sense.StopMonitor();
            }

            audio.Play(new HandsetTune(station.Id, station.Name.Length > 0 ? station.Name : "Your station",
                CommunityTuneDetail(station), url, true, station.ArtPath));
        }
        else
        {
            if (!sense.Monitoring)
            {
                sense.StartMonitor();
            }

            audio.PlayLocal(new HandsetTune(station.Id, station.Name.Length > 0 ? station.Name : "Your station",
                "Hearing your capture on this phone while Icecast gets a listen URL.",
                string.Empty, true, station.ArtPath));
        }

        state.Open(MusicPage.Player);
    }

    private void PickCapture(string id, string app)
    {
        sense.Select(id);
        lastCaptureTry = 0;
        state.CaptureId = id;
        state.CaptureName = sense.SelectedName;
        state.CaptureApp = "sound";
        lastSoundTap = id;

        sense.RoutePhone(display.SpeakerId, display.MicrophoneId);
        sense.Start();
        state.Save(paths);
    }

    private void PullOwnedStation()
    {
        CommunityStation owned = default;
        foreach (var row in community.Mine)
        {
            owned = row;
            break;
        }

        if (owned.Id.Length == 0)
        {
            return;
        }

        state.StationId = owned.Id;
        if (owned.Mount.Length > 0)
        {
            state.StationMount = owned.Mount;
        }

        if (owned.Name.Length > 0 && state.StationName.Length == 0)
        {
            state.StationName = owned.Name;
        }

        if (owned.Host.Length > 0 && state.DjName.Length == 0)
        {
            state.DjName = owned.Host;
        }
    }

    private void PublishStation()
    {
        if (state.StationName.Length == 0)
        {
            if (state.DisplayName.Length == 0 && game.Character.Name.Length > 0)
            {
                state.DisplayName = game.Character.Name;
            }

            return;
        }

        state.Dj = true;
        if (state.DisplayName.Length == 0 && game.Character.Name.Length > 0)
        {
            state.DisplayName = game.Character.Name;
        }

        if (state.DjName.Length == 0)
        {
            state.DjName = state.DisplayName;
        }

        if (state.StationMount.Length == 0)
        {
            state.StationMount = MusicState.SlugMount(state.StationName);
        }

        community.EnsureStation(
            state.StationName,
            state.DjName,
            state.StationGenreLine,
            state.StationBio,
            state.StationArtPath,
            state.StationMount);
        if (community.OwnedId.Length > 0)
        {
            state.StationId = community.OwnedId;
        }

        state.Save(paths);
    }

    string IEchoMixStation.StationTitle =>
        state.StationName.Length > 0 ? state.StationName : SharedName();

    string IEchoMixStation.DjName =>
        state.DjName.Length > 0 ? state.DjName : SharedName();

    string IEchoMixStation.Genre => state.StationGenreLine;

    bool IEchoMixStation.Broadcasting => community.Broadcasting;

    string IEchoMixStation.ListenUrl => community.OwnedListenUrl;

    string IEchoMixStation.Notice =>
        community.Notice.Length > 0 ? community.Notice : push.Notice;

    void IEchoMixStation.Prepare()
    {
        if (state.StationName.Length == 0)
        {
            var name = SharedName();
            state.StationName = name.Length > 0 ? name + " FM" : "Linkpearl FM";
        }

        PublishStation();
    }

    void IEchoMixStation.GoLive()
    {
        ((IEchoMixStation)this).Prepare();
        community.UseIcecast(state.IcecastHost, "source", state.IcecastPassword);
        community.GoLive(
            state.StationName.Length > 0 ? state.StationName : SharedName() + " FM",
            state.StationGenreLine);
    }

    void IEchoMixStation.EndLive() => community.EndLive();

    public void OpenEchoMix() => booth.Open();

    private void PickStationArt()
    {
        files.BeginImagePick();
        imagePick = ImagePick.StationArt;
    }

    private void ApplyImagePick(in AppletFrame frame)
    {
        if (imagePick == ImagePick.None || !files.TryTakeImages(out var picked))
        {
            return;
        }

        var job = imagePick;
        imagePick = ImagePick.None;
        if (picked.Count == 0)
        {
            return;
        }

        if (job == ImagePick.Profile)
        {
            if (ImportProfilePhoto(frame, picked[0]))
            {
                state.Open(MusicPage.PlacePhoto);
            }

            return;
        }

        ImportStationArt(picked[0]);
    }

    private void ImportStationArt(string source)
    {
        try
        {
            var ext = Path.GetExtension(source);
            if (ext.Length == 0)
            {
                ext = ".png";
            }

            var dest = paths.State("music-station-art-" + Guid.NewGuid().ToString("N") + ext);
            Directory.CreateDirectory(paths.StateDirectory);
            File.Copy(source, dest, false);
            state.StationArtPath = dest;
            state.Save(paths);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private void WarmStationArt(IReadOnlyList<PublicStation> stations)
    {
        var count = Math.Min(stations.Count, 24);
        for (var index = 0; index < count; index++)
        {
            var url = stations[index].ArtUrl;
            if (url.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            {
                pearl.PrefetchMedia(url);
            }
        }
    }

    private ITextureHandle? StationTexture(in AppletFrame frame, string path)
    {
        if (path.Length == 0)
        {
            return null;
        }

        if (path.StartsWith("http", StringComparison.OrdinalIgnoreCase))
        {
            pearl.PrefetchMedia(path);
            var local = pearl.LocalMedia(path);
            return local is { Length: > 0 } ? frame.Textures.FromFile(local) : null;
        }

        return frame.Textures.FromFile(path);
    }

    private void DrawStationArt(in AppletFrame frame, Rect area, string path, string seed = "",
        string label = "")
    {
        _ = seed;
        _ = label;
        MusicChrome.ArtShadow(frame, area);
        var texture = StationTexture(frame, path);
        if (texture is { IsReady: true })
        {
            frame.Paint.ImageRounded(texture, area, Vector2.Zero, Vector2.One, Vector4.One, frame.Units(8f));
            return;
        }

        frame.Paint.Fill(area, MusicChrome.PurpleDim, frame.Units(8f));
        frame.Text.DrawIn(area, "♪", new TextStyle(FontRole.CaptionStrong, MusicChrome.Purple, TextAlign.Center));
    }
}
