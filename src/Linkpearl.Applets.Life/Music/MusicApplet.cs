using System.Linq;
using Linkpearl.Applets;
using Linkpearl.Audio;
using Linkpearl.Badges;
using Linkpearl.Feedback;
using Linkpearl.Geometry;
using Linkpearl.Layout;
using Linkpearl.Media;
using Linkpearl.Modules;
using Linkpearl.Net;
using Linkpearl.Painting;
using Linkpearl.Platform;
using Linkpearl.Preferences;

namespace Linkpearl.Applets.Life.Music;

public sealed partial class MusicApplet : IApplet, IHandsetProfileSink
{
    public static readonly AppletManifest Manifest = new()
    {
        Id = "music",
        DisplayNameKey = "Music",
        Family = AppletFamily.Media,
        Glyph = "♫",
        HomeOrder = 8,
        Capabilities = AppletCapabilities.PlaysAudio,
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
    private readonly MusicState state;
    private int profileStamp;
    private float nameClock;
    private long lastCaptureTry;
    private long lastPushTry;
    private long lastCommunityRefresh;
    private long lastPortScan;
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
        HandsetProfileDesk profiles)
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
        state = MusicState.Load(paths, game.Character.Name);
        FillFromCharacter();
        profiles.Add(this);
        if (state.UsesHandsetIdentity || state.UsesHandsetProfile)
        {
            AcceptHandsetProfile(HandsetName(), HandsetLook.Honorific(display));
        }

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
    }

    private string HandsetName()
    {
        var linked = ShownName.Linked(game.Character.Name, pearl.Current.MeName);
        var name = HandsetLook.Name(display, linked);
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
        return ShownName.Preferred(display, linked, state.DisplayName, "Listener");
    }

    private string EditableName()
    {
        var linked = ShownName.Linked(game.Character.Name, pearl.Current.MeName);
        var handset = ShownName.Source(display, linked);
        var stored = state.DisplayName.Trim();
        if (stored.Length > 0)
        {
            return stored;
        }

        return handset.Length > 0 ? handset : "Listener";
    }

    private string SharedHonorific() => state.Honorific.Trim();

    private bool FancyName() =>
        GlassName.IsPatron(badges, pearl.Current, display.TestingAccount);

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

        state.Honorific = ShownName.ClampTitle(honorific).Trim();
        state.ProfileFacePath = HandsetLook.ReplaceStill(paths, state.ProfileFacePath,
            HandsetLook.PortraitFile(paths, badges), "music-profile-face");
        if (state.ProfileFacePath.Length > 0)
        {
            state.FaceZoom = badges.PortraitZoom;
            state.FaceFocusX = badges.PortraitFocus.X;
            state.FaceFocusY = badges.PortraitFocus.Y;
        }

        state.ProfileBannerPath = HandsetLook.ReplaceStill(paths, state.ProfileBannerPath,
            HandsetLook.BannerFile(paths, display), "music-profile-banner");
        if (state.ProfileBannerPath.Length > 0)
        {
            state.BannerZoom = 1f;
            state.BannerFocusX = 0.5f;
            state.BannerFocusY = 0.5f;
        }

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
        if (!state.Onboarded)
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
        OpenTab(MusicTab.Search);
        var genreIndex = -1;
        for (var index = 0; index < MusicState.Genres.Length; index++)
        {
            if (MusicState.Genres[index].Equals(genre, StringComparison.OrdinalIgnoreCase))
            {
                genreIndex = index;
                break;
            }
        }

        if (genreIndex >= 0)
        {
            OpenGenre(genreIndex);
        }

        if (stationId.Length > 0)
        {
            state.RevealStationId = stationId;
        }

        return true;
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

    public bool CanGoBack => state.Page != MusicPage.Tabs && state.Page != MusicPage.Onboard;

    public bool Back()
    {
        if (state.ReportOpen)
        {
            CloseReport();
            return true;
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
            if (Environment.TickCount64 - lastCommunityRefresh > 8000)
            {
                lastCommunityRefresh = Environment.TickCount64;
                community.Refresh();
            }
        }
        catch (Exception)
        {
        }

        try
        {
            ApplyImagePick(frame);
            MusicChrome.PaintGround(frame, frame.Content);
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
                DrawHint(frame, ref stack, "When DJs go live on Pearlgate, their stations land here.");
            }
            else
            {
                var liveCard = area.Width * 0.42f;
                DrawFollowShelf(frame, stack.Take(liveCard + frame.Units(28f)), livePicks, compact: false);
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
            var copy = card.BottomSlice(frame.Units(28f));
            frame.Text.DrawEllipsized(copy.TopSlice(frame.Units(14f)), station.Title,
                new TextStyle(FontRole.CaptionStrong, MusicChrome.Ink));
            frame.Text.DrawEllipsized(copy.BottomSlice(frame.Units(12f)),
                station.Genre.Length > 0 ? station.Genre : station.Place,
                new TextStyle(FontRole.Caption, MusicChrome.Mute));
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
            var hearW = frame.Units(72f);
            var hearH = frame.Units(16f);
            var hear = Rect.FromSize(
                new Vector2(art.Min.X + frame.Units(4f), art.Max.Y - frame.Units(4f) - hearH),
                new Vector2(hearW, hearH));
            frame.Text.DrawEllipsized(hear, Math.Max(0, station.Listeners).ToString() + " listening",
                new TextStyle(FontRole.Caption, MusicChrome.Ink));
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

    private void DrawFeedStationCard(in AppletFrame frame, Rect area, PublicStation station)
    {
        var head = area.TopSlice(frame.Units(18f));
        frame.Text.DrawEllipsized(head.Inset(new Edges(0f, 0f, frame.Units(28f), 0f)),
            station.Place + " · station",
            new TextStyle(FontRole.Caption, MusicChrome.Mute));
        var card = area.Inset(new Edges(0f, frame.Units(22f), 0f, 0f));
        DrawStationArt(frame, card, station.ArtUrl, station.Id, station.Title);
        var rail = card.RightSlice(frame.Units(44f)).Inset(new Edges(0f, frame.Units(18f), frame.Units(8f),
            frame.Units(56f)));
        var heart = rail.TopSlice(frame.Units(42f));
        DrawStationLike(frame, heart, station.Id);
        var copy = card.BottomSlice(frame.Units(48f)).Inset(new Edges(frame.Units(12f), 0f, frame.Units(56f),
            frame.Units(8f)));
        frame.Text.DrawEllipsized(copy.TopSlice(frame.Units(18f)),
            station.Title + (station.Bitrate > 0 ? " · " + station.Bitrate + "k" : string.Empty),
            new TextStyle(FontRole.BodyStrong, MusicChrome.Ink));
        frame.Text.DrawEllipsized(copy.BottomSlice(frame.Units(16f)), station.Place,
            new TextStyle(FontRole.Caption, MusicChrome.Mute));
        var play = card.BottomSlice(frame.Units(48f)).RightSlice(frame.Units(48f)).Inset(frame.Units(4f));
        frame.Paint.FillCircle(play.Center, frame.Units(16f), new Vector4(1f, 1f, 1f, 0.88f));
        frame.Text.DrawIn(play, "▶", new TextStyle(FontRole.CaptionStrong, MusicChrome.Ground, TextAlign.Center));
        if (TapStationLike(frame, heart, station.Id))
        {
            return;
        }

        if (frame.Input.ConsumeClick(play) || frame.Input.ConsumeClick(card))
        {
            TunePublic(station);
            state.Open(MusicPage.Player);
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
            (ShownStationGenre(station).Length > 0 ? " · " + ShownStationGenre(station) : string.Empty) +
            (station.Listeners > 0 ? " · " + station.Listeners + " listening" : string.Empty),
            new TextStyle(FontRole.Caption, MusicChrome.Mute));
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

    private CommunityStation[] SavedLiveStations()
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var rows = new List<CommunityStation>();
        foreach (var id in state.Favorites)
        {
            var station = FindCommunity(id);
            if (string.IsNullOrEmpty(station.Id) || !seen.Add(MusicState.BareStationId(station.Id)))
            {
                continue;
            }

            rows.Add(station);
        }

        return rows.ToArray();
    }

    private IReadOnlyList<PublicStation> LikedStations() =>
        MusicState.Genres
            .SelectMany(publicRadio.Stations)
            .Where(row => state.Favorites.Contains(row.Id))
            .GroupBy(row => row.Id, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToArray();

    private CommunityStation[] RecommendedLive()
    {
        var pool = community.Live
            .Where(row => row.Id.Length > 0 && !OwnStation(row))
            .GroupBy(row => row.Id, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToArray();
        var key = string.Join(",", pool.Select(static row => row.Id).OrderBy(static id => id, StringComparer.Ordinal));
        if (string.Equals(key, recommendedLiveKey, StringComparison.Ordinal) && recommendedLive.Length > 0)
        {
            return recommendedLive;
        }

        recommendedLiveKey = key;
        var pick = Math.Min(3, pool.Length);
        if (pick == 0)
        {
            recommendedLive = [];
            return recommendedLive;
        }

        var order = Enumerable.Range(0, pool.Length).ToArray();
        var rng = new Random(unchecked((int)(uint)key.GetHashCode(StringComparison.Ordinal)));
        for (var index = order.Length - 1; index > 0; index--)
        {
            var swap = rng.Next(index + 1);
            (order[index], order[swap]) = (order[swap], order[index]);
        }

        recommendedLive = new CommunityStation[pick];
        for (var index = 0; index < pick; index++)
        {
            recommendedLive[index] = pool[order[index]];
        }

        return recommendedLive;
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

    private void RevealGenreStation(in AppletFrame frame, IReadOnlyList<PublicStation> stations)
    {
        var id = state.RevealStationId;
        if (id.Length == 0)
        {
            return;
        }

        if (stations.Count == 0)
        {
            return;
        }

        for (var index = 0; index < stations.Count; index++)
        {
            if (!stations[index].Id.Equals(id, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var gap = frame.Units(10f);
            var head = frame.Units(28f) + gap + frame.Units(16f) + gap + frame.Units(40f) + gap;
            state.Scroll = head + index * (frame.Units(52f) + gap);
            state.RevealStationId = string.Empty;
            return;
        }

        state.RevealStationId = string.Empty;
    }

    private void OpenGenre(int index)
    {
        state.GenreIndex = Math.Clamp(index, 0, MusicState.Genres.Length - 1);
        state.Search = string.Empty;
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
            var cell = (panes.Width - gap * 2f) / 3f;
            var radio = Rect.FromSize(panes.Min, new Vector2(cell, panes.Height));
            var following = Rect.FromSize(new Vector2(radio.Max.X + gap, panes.Min.Y), new Vector2(cell, panes.Height));
            var live = Rect.FromSize(new Vector2(following.Max.X + gap, panes.Min.Y), new Vector2(cell, panes.Height));
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

            if (state.FeedPane == MusicFeedPane.Live)
            {
                DrawFeedLive(frame, ref stack);
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
        var stations = publicRadio.Stations(state.Genre);
        if (stations.Count == 0)
        {
            DrawHint(frame, ref stack, publicRadio.Busy ? "Finding stations…" : "No stations yet. Try Search.");
            return;
        }

        var take = Math.Min(stations.Count, 6);
        for (var index = 0; index < take; index++)
        {
            DrawFeedStationCard(frame, stack.Take(frame.Units(220f)), stations[index]);
        }
    }

    private void DrawFeedLive(in AppletFrame frame, ref Stack stack)
    {
        var live = community.Live;
        if (live.Count == 0)
        {
            DrawHint(frame, ref stack, "No one is live on Pearlgate right now.");
            return;
        }

        for (var index = 0; index < live.Count; index++)
        {
            DrawFeedLiveCard(frame, stack.Take(frame.Units(220f)), live[index]);
        }
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
            DrawFeedLiveCard(frame, stack.Take(frame.Units(220f)), board[index]);
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

        foreach (var row in community.Live)
        {
            Put(row);
        }

        foreach (var row in community.Directory)
        {
            Put(row);
        }

        foreach (var row in community.Mine)
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

        return byId.Values
            .OrderByDescending(static row => row.Live)
            .ThenBy(static row => row.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private CommunityStation FindCommunity(string id)
    {
        var bare = MusicState.BareStationId(id);
        if (bare.Length == 0)
        {
            return EmptyCommunity();
        }

        foreach (var row in community.Live)
        {
            if (string.Equals(row.Id, bare, StringComparison.OrdinalIgnoreCase))
            {
                return row;
            }
        }

        foreach (var row in community.Directory)
        {
            if (string.Equals(row.Id, bare, StringComparison.OrdinalIgnoreCase))
            {
                return row;
            }
        }

        foreach (var row in community.Mine)
        {
            if (string.Equals(row.Id, bare, StringComparison.OrdinalIgnoreCase))
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

    private void OpenReport(string stationId, string title)
    {
        state.ReportOpen = true;
        state.ReportReason = 0;
        state.ReportDetail = string.Empty;
        state.ReportTarget = stationId;
        state.ReportTitle = title;
    }

    private void CloseReport()
    {
        state.ReportOpen = false;
        state.ReportReason = 0;
        state.ReportDetail = string.Empty;
        state.ReportTarget = string.Empty;
        state.ReportTitle = string.Empty;
    }

    private void SubmitReport()
    {
        if (state.ReportReason <= 0 || desk.Busy)
        {
            return;
        }

        var reason = ReportReasons[Math.Clamp(state.ReportReason, 1, ReportReasons.Length - 1)];
        var body = "Station: " + (state.ReportTitle.Length > 0 ? state.ReportTitle : "Unknown") +
                   "\nId: " + state.ReportTarget +
                   (state.ReportDetail.Trim().Length > 0 ? "\n\n" + state.ReportDetail.Trim() : string.Empty);
        desk.Send(new FeedbackNote("Station report · " + reason, body, game.Character.Name, game.Character.WorldName));
        CloseReport();
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
                var hits = SearchHits(query);
                WarmStationArt(hits);
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
        var board = community.Directory;
        var live = board.Where(static row => row.Live).ToArray();
        var dark = board.Where(static row => !row.Live).ToArray();
        var status = stack.Take(frame.Units(16f));
        frame.Text.DrawIn(status.LeftSlice(status.Width * 0.62f), "Community stations. Offline until a DJ goes live.",
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
                    : "Create your station, then Go live. Everyone on LIVE can tune it.",
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
        frame.Text.DrawEllipsized(body.BottomSlice(frame.Units(16f)),
            (station.Host.Length > 0 ? station.Host : "DJ") +
            (ShownStationGenre(station).Length > 0 ? " · " + ShownStationGenre(station) : string.Empty) +
            (station.Listeners > 0 ? " · " + station.Listeners + " listening" : string.Empty),
            new TextStyle(FontRole.Caption, MusicChrome.Mute));
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
            if (savedLive.Length > 0)
            {
                MusicChrome.Kicker(frame, stack.Take(frame.Units(20f)), "Saved");
                for (var index = 0; index < savedLive.Length; index++)
                {
                    DrawCommunityRow(frame, ref stack, savedLive[index]);
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
                DrawPublicRows(frame, ref stack, favorites, favorites.Count);
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

                DrawPublicRows(frame, ref stack, stations, 3);
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
        var saved = now.Id.Length > 0 && state.Favorites.Contains(now.Id);
        frame.Text.DrawIn(heart, saved ? "♥" : "♡",
            new TextStyle(FontRole.Title, saved ? MusicChrome.Purple : MusicChrome.Ink, TextAlign.Center));
        var copy = row.Inset(new Edges(row.Height, frame.Units(8f), frame.Units(44f), frame.Units(8f)));
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
            state.ToggleFavorite(now.Id);
            state.Save(paths);
        }
        else if (frame.Input.ConsumeClick(row))
        {
            state.Open(MusicPage.Player);
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
        var railW = frame.Units(12f);
        var step = frame.Units(28f);
        var rail = area.RightSlice(railW);
        var rest = area.Inset(new Edges(0f, 0f, railW, 0f));
        var cycle = rest.RightSlice(step);
        var list = rest.Inset(new Edges(0f, 0f, step + frame.Units(4f), 0f));
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

        MusicChrome.Rail(frame, rail.Inset(new Edges(frame.Units(4f), frame.Units(4f))), state.Scroll, content,
            list.Height);
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

    private void DrawPublicRows(in AppletFrame frame, ref Stack stack, IReadOnlyList<PublicStation> stations, int max)
    {
        var count = Math.Min(stations.Count, max);
        for (var index = 0; index < count; index++)
        {
            DrawPublicRow(frame, stack.Take(frame.Units(52f)), stations[index], true);
        }
    }

    private void DrawPublicRow(in AppletFrame frame, Rect row, PublicStation station, bool allowOpen,
        string? removeFrom = null)
    {
        var current = string.Equals(audio.Now.Id, station.Id, StringComparison.OrdinalIgnoreCase);
        var playing = current && audio.Phase is HandsetAudioPhase.Playing or HandsetAudioPhase.Buffering;
        var saved = state.Favorites.Contains(station.Id);
        MusicChrome.GlowPlate(frame, row, frame.Units(12f), current);
        var inset = row.Inset(frame.Units(8f));
        var play = inset.LeftSlice(frame.Units(36f));
        DrawStationArt(frame, play, station.ArtUrl, station.Id, station.Title);
        frame.Paint.FillCircle(play.Center, frame.Units(9f), current ? MusicChrome.Purple : new Vector4(0f, 0f, 0f, 0.45f));
        frame.Text.DrawIn(play, playing ? "❚❚" : "▶",
            new TextStyle(FontRole.CaptionStrong, MusicChrome.Ink, TextAlign.Center, scale: 0.82f));
        var star = inset.RightSlice(frame.Units(30f));
        var extra = inset.Inset(new Edges(0f, 0f, star.Width + frame.Units(4f), 0f)).RightSlice(frame.Units(30f));
        var body = inset.Inset(new Edges(frame.Units(42f), 0f, star.Width + extra.Width + frame.Units(8f), 0f));
        frame.Text.DrawEllipsized(body.TopSlice(frame.Units(18f)), station.Title,
            new TextStyle(FontRole.BodyStrong, MusicChrome.Ink));
        frame.Text.DrawEllipsized(body.BottomSlice(frame.Units(16f)),
            station.Place + (station.Bitrate > 0 ? " · " + station.Bitrate + "k" : string.Empty) +
            (current ? playing ? " · Playing" : " · Paused" : string.Empty),
            new TextStyle(FontRole.Caption, MusicChrome.Mute));
        frame.Paint.Fill(star, saved ? MusicChrome.PurpleDim : MusicChrome.CardHi, frame.Units(8f));
        frame.Text.DrawIn(star, saved ? "♥" : "♡",
            new TextStyle(FontRole.BodyStrong, saved ? MusicChrome.Purple : MusicChrome.Mute, TextAlign.Center));
        frame.Paint.Fill(extra, MusicChrome.CardHi, frame.Units(8f));
        frame.Text.DrawIn(extra, removeFrom is { Length: > 0 } ? "×" : "+",
            new TextStyle(FontRole.BodyStrong, MusicChrome.Ink, TextAlign.Center));
        if (frame.Input.ConsumeClick(star))
        {
            state.ToggleFavorite(station.Id);
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

        if (frame.Input.ConsumeClick(play))
        {
            if (current)
            {
                audio.Toggle();
            }
            else
            {
                TunePublic(station);
            }

            state.Open(MusicPage.Player);
            return;
        }

        if (allowOpen && frame.Input.ConsumeClick(row))
        {
            if (!current)
            {
                TunePublic(station);
            }

            state.Open(MusicPage.Player);
        }
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
        frame.Text.DrawIn(inset.BottomSlice(frame.Units(14f)),
            (ShownStationGenre(station).Length > 0 ? ShownStationGenre(station) : station.Host) +
            (station.Listeners > 0 ? " · " + station.Listeners : string.Empty),
            new TextStyle(FontRole.Caption, MusicChrome.Mute));
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
        var badge = row.Inset(frame.Units(8f)).RightSlice(station.Live ? frame.Units(40f) : frame.Units(56f))
            .TopSlice(frame.Units(18f));
        var body = row.Inset(new Edges(frame.Units(56f), frame.Units(8f), badge.Width + frame.Units(8f),
            frame.Units(8f)));
        frame.Text.DrawEllipsized(body.TopSlice(frame.Units(18f)), station.Name,
            new TextStyle(FontRole.BodyStrong, MusicChrome.Ink));
        frame.Text.DrawEllipsized(body.BottomSlice(frame.Units(16f)),
            (station.Host.Length > 0 ? station.Host : "DJ") +
            (ShownStationGenre(station).Length > 0 ? " · " + ShownStationGenre(station) : string.Empty) +
            (station.Bio.Length > 0 ? " · " + station.Bio : string.Empty),
            new TextStyle(FontRole.Caption, MusicChrome.Mute));
        if (station.Live)
        {
            MusicChrome.LiveMark(frame, badge);
        }
        else
        {
            MusicChrome.OffMark(frame, badge);
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

    private void SyncBroadcastTap()
    {
        community.UseIcecast(state.IcecastHost, "source", state.IcecastPassword);
        audio.UseSpeaker(display.SpeakerId);

        if (!community.Broadcasting)
        {
            push.Stop();
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
