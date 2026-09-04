using System.Linq;
using Linkpearl.Applets;
using Linkpearl.Audio;
using Linkpearl.Geometry;
using Linkpearl.Layout;
using Linkpearl.Modules;
using Linkpearl.Net;
using Linkpearl.Painting;
using Linkpearl.Platform;
using Linkpearl.Preferences;

namespace Linkpearl.Applets.Life.Music;

public sealed partial class MusicApplet : IApplet
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

    private static readonly string[] Tabs = { "HOME", "DISCOVER", "LIBRARY", "PROFILE" };
    private static readonly string[] TabMarks = { "*", "+", "=", "o" };

    private readonly IGameSession game;
    private readonly HostPaths paths;
    private readonly DisplayPreferences display;
    private readonly IHandsetAudio audio;
    private readonly IPublicRadio publicRadio;
    private readonly ICommunityRadio community;
    private readonly IPearlHub pearl;
    private readonly IFilePicker files;
    private readonly IBroadcastSense sense;
    private readonly IBroadcastPush push;
    private readonly IAudioPorts ports;
    private readonly MusicState state;
    private long lastCaptureTry;
    private long lastPushTry;
    private long lastCommunityRefresh;

    public MusicApplet(IGameSession game, HostPaths paths, DisplayPreferences display, IHandsetAudio audio,
        IPublicRadio publicRadio, ICommunityRadio community, IPearlHub pearl, IFilePicker files, IBroadcastSense sense,
        IBroadcastPush push, IAudioPorts ports)
    {
        this.game = game;
        this.paths = paths;
        this.display = display;
        this.audio = audio;
        this.publicRadio = publicRadio;
        this.community = community;
        this.pearl = pearl;
        this.files = files;
        this.sense = sense;
        this.push = push;
        this.ports = ports;
        state = MusicState.Load(paths, game.Character.Name);
        sense.CapturedPcm += push.WritePcm;
        sense.RefreshPoints();
        if (state.CaptureId.Length > 0)
        {
            sense.Select(state.CaptureId);
        }

        publicRadio.Ensure(state.Genre);
        if (state.StationName.Length > 0)
        {
            PublishStation();
        }

        community.Refresh();
    }

    AppletManifest IApplet.Manifest => Manifest;

    public AppletBadge Badge => AppletBadge.None;

    public string Place => state.Page == MusicPage.Tabs ? state.Genre : state.Page.ToString();

    public void Enter(AppletEntry entry)
    {
        display.Landscape = false;
        publicRadio.Ensure(state.Genre);
        community.Refresh();
        if (state.Onboarded && IsRadioPlace(entry.RouteHint))
        {
            OpenDiscover(live: false);
        }
    }

    private static bool IsRadioPlace(string? hint) =>
        hint is "radio" or "stations" or "discover" or "discover-radio";

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

        if (state.Page == MusicPage.EditProfile)
        {
            state.Page = MusicPage.Tabs;
            state.Tab = MusicTab.Profile;
            return true;
        }

        state.Back();
        return true;
    }

    public void Compose(in AppletFrame frame)
    {
        frame.Paint.Fill(frame.Content, MusicChrome.Bg);
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
        var nav = frame.Units(56f);
        var live = audio.Phase is HandsetAudioPhase.Playing or HandsetAudioPhase.Buffering or HandsetAudioPhase.Paused
            or HandsetAudioPhase.Failed;
        var mini = live ? frame.Units(46f) : 0f;
        var strip = frame.Content.BottomSlice(nav);
        DrawNav(frame, strip);
        frame.Input.Claim(strip);
        if (live)
        {
            DrawMini(frame, frame.Content.BottomSlice(nav + mini).TopSlice(mini).Inset(new Edges(frame.Units(12f), 0f)));
        }

        var body = frame.Content.Inset(new Edges(frame.Units(14f), frame.Units(10f), frame.Units(14f),
            nav + mini + frame.Units(8f)));
        if (body.IsEmpty)
        {
            return;
        }

        frame.Paint.PushClip(body);
        try
        {
            switch (state.Tab)
            {
                case MusicTab.Discover:
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
        var stack = MusicChrome.BeginSheet(frame, area, state, frame.Units(10f));
        try
        {
        var head = stack.Take(frame.Units(28f));
        MusicChrome.Title(frame, head.LeftSlice(head.Width * 0.78f), "Linkpearl Music");
        var me = head.RightSlice(frame.Units(28f));
        frame.Paint.FillCircle(me.Center, frame.Units(12f), MusicChrome.Purple);
        if (frame.Input.ConsumeClick(me))
        {
            OpenTab(MusicTab.Profile);
        }

        frame.Text.DrawIn(stack.Take(frame.Units(32f)),
            "Radio is on-demand stations. LIVE is community broadcasts happening now.",
            new TextStyle(FontRole.Caption, MusicChrome.Mute));
        var lanes = stack.Take(frame.Units(118f));
        var gap = frame.Units(8f);
        var radio = lanes.LeftSlice((lanes.Width - gap) * 0.5f);
        var liveLane = lanes.RightSlice((lanes.Width - gap) * 0.5f);
        var liveCount = community.Live.Count;
        DrawHomeLane(frame, radio, "Stations", "Tune any time",
            publicRadio.Busy ? "Loading…" : publicRadio.Stations(state.Genre).Count + " in " + state.Genre, false);
        DrawHomeLane(frame, liveLane, "Broadcasts", "Happening now",
            liveCount > 0 ? liveCount + " on air" : "No one live right now", true);
        if (frame.Input.ConsumeClick(radio))
        {
            OpenDiscover(live: false);
        }
        else if (frame.Input.ConsumeClick(liveLane))
        {
            OpenDiscover(live: true);
        }

        if (audio.Now.Id.Length > 0)
        {
            MusicChrome.Kicker(frame, stack.Take(frame.Units(14f)), audio.Now.Live ? "NOW · LIVE" : "NOW · RADIO");
            var now = stack.Take(frame.Units(56f));
            MusicChrome.GlowPlate(frame, now, frame.Units(12f), audio.Phase == HandsetAudioPhase.Playing);
            var inset = now.Inset(frame.Units(10f));
            frame.Text.DrawEllipsized(inset.TopSlice(frame.Units(18f)),
                audio.Now.Title.Length > 0 ? audio.Now.Title : "Tuned",
                new TextStyle(FontRole.BodyStrong, MusicChrome.Ink));
            frame.Text.DrawEllipsized(inset.BottomSlice(frame.Units(16f)),
                audio.Phase == HandsetAudioPhase.Failed ? audio.Notice : audio.Now.Detail,
                new TextStyle(FontRole.Caption, MusicChrome.Mute));
            if (frame.Input.ConsumeClick(now))
            {
                state.Open(MusicPage.Player);
            }
        }

        if (state.Favorites.Count > 0)
        {
            MusicChrome.Kicker(frame, stack.Take(frame.Units(14f)), "LIBRARY");
            var saved = stack.Take(frame.Units(44f));
            MusicChrome.Plate(frame, saved, frame.Units(12f));
            frame.Text.DrawIn(saved.Inset(frame.Units(12f)),
                state.Favorites.Count + " saved station" + (state.Favorites.Count == 1 ? "" : "s"),
                new TextStyle(FontRole.BodyStrong, MusicChrome.Ink));
            if (frame.Input.ConsumeClick(saved))
            {
                OpenTab(MusicTab.Library);
            }
        }
        }
        finally
        {
            MusicChrome.EndSheet(frame, area, state, ref stack);
        }
    }

    private void DrawHomeLane(in AppletFrame frame, Rect area, string title, string detail, string count, bool live)
    {
        MusicChrome.GlowPlate(frame, area, frame.Units(14f), live && community.Live.Count > 0);
        var inset = area.Inset(frame.Units(12f));
        if (live)
        {
            MusicChrome.LiveMark(frame, inset.TopSlice(frame.Units(16f)).LeftSlice(frame.Units(40f)));
        }
        else
        {
            frame.Text.DrawIn(inset.TopSlice(frame.Units(16f)), "RADIO",
                new TextStyle(FontRole.CaptionStrong, MusicChrome.Purple));
        }

        frame.Text.DrawIn(inset.Inset(new Edges(0f, frame.Units(22f), 0f, frame.Units(28f))), title,
            new TextStyle(FontRole.Title, MusicChrome.Ink));
        frame.Text.DrawEllipsized(inset.BottomSlice(frame.Units(28f)).TopSlice(frame.Units(14f)), detail,
            new TextStyle(FontRole.Caption, MusicChrome.Mute));
        frame.Text.DrawEllipsized(inset.BottomSlice(frame.Units(14f)), count,
            new TextStyle(FontRole.Caption, MusicChrome.Mute));
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
        else if (tab == MusicTab.Discover)
        {
            if (state.DiscoverLive)
            {
                community.Refresh();
            }
            else
            {
                publicRadio.Ensure(state.Genre);
            }
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
        state.DiscoverLive = live;
        OpenTab(MusicTab.Discover);
    }

    private void DrawDiscover(in AppletFrame frame, Rect area)
    {
        var stack = new Stack(area, StackAxis.Vertical, frame.Units(8f));
        var head = stack.Take(frame.Units(24f));
        MusicChrome.Title(frame, head, "Discover");
        var panes = stack.Take(frame.Units(34f));
        var radioHit = panes.LeftSlice(panes.Width * 0.5f).Inset(new Edges(0f, 0f, frame.Units(4f), 0f));
        var liveHit = panes.RightSlice(panes.Width * 0.5f).Inset(new Edges(frame.Units(4f), 0f, 0f, 0f));
        if (MusicChrome.Chip(frame, radioHit, "Radio", !state.DiscoverLive))
        {
            OpenDiscover(live: false);
        }

        if (MusicChrome.Chip(frame, liveHit, "LIVE", state.DiscoverLive))
        {
            OpenDiscover(live: true);
        }

        if (state.DiscoverLive)
        {
            DrawDiscoverLive(frame, stack.TakeRemaining());
            return;
        }

        DrawDiscoverRadio(frame, ref stack);
    }

    private void DrawDiscoverRadio(in AppletFrame frame, ref Stack stack)
    {
        frame.Text.DrawIn(stack.Take(frame.Units(16f)), "On-demand stations. Tune in whenever.",
            new TextStyle(FontRole.Caption, MusicChrome.Mute));
        var search = stack.Take(frame.Units(32f));
        MusicChrome.Plate(frame, search, frame.Units(10f));
        state.Search = frame.TextField.Draw("music-search", search.Inset(new Edges(frame.Units(8f), 0f)), state.Search,
            "Search radio stations");
        DrawGenreChips(frame, stack.Take(GenreStripHeight(frame)));
        var stations = FilteredStations();
        MusicChrome.Kicker(frame, stack.Take(frame.Units(14f)),
            "RADIO · " + stations.Count + " · " + state.Genre.ToUpperInvariant());
        DrawStationList(frame, stack.TakeRemaining(), stations);
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
            station.ArtPath);
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
            (station.Genre.Length > 0 ? " · " + station.Genre : string.Empty) +
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
        var stack = new Stack(area, StackAxis.Vertical, frame.Units(10f));
        MusicChrome.Title(frame, stack.Take(frame.Units(24f)), "Library");
        MusicChrome.Kicker(frame, stack.Take(frame.Units(14f)), "FAVORITES");
        var favorites = publicRadio.Genres
            .SelectMany(publicRadio.Stations)
            .Where(row => state.Favorites.Contains(row.Id))
            .GroupBy(row => row.Id, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToArray();
        if (favorites.Length == 0)
        {
            DrawHint(frame, ref stack, "Star a station to keep it here.");
        }
        else
        {
            DrawStationList(frame, stack.TakeRemaining(), favorites);
            return;
        }

        MusicChrome.Kicker(frame, stack.Take(frame.Units(14f)), "YOUR GENRES");
        var picks = state.Interests.Count > 0 ? state.Interests : MusicState.Genres.Take(3).ToList();
        for (var index = 0; index < picks.Count; index++)
        {
            publicRadio.Ensure(picks[index]);
            var stations = publicRadio.Stations(picks[index]);
            if (stations.Count == 0)
            {
                continue;
            }

            MusicChrome.Kicker(frame, stack.Take(frame.Units(14f)), picks[index].ToUpperInvariant());
            DrawPublicRows(frame, ref stack, stations, 3);
        }
    }

    private void DrawNav(in AppletFrame frame, Rect strip)
    {
        frame.Paint.Fill(strip, MusicChrome.Card);
        var w = strip.Width / Tabs.Length;
        for (var index = 0; index < Tabs.Length; index++)
        {
            var cell = Rect.FromSize(new Vector2(strip.Min.X + w * index, strip.Min.Y), new Vector2(w, strip.Height));
            var on = (int)state.Tab == index;
            frame.Text.DrawIn(cell.TopSlice(frame.Units(28f)), TabMarks[index],
                new TextStyle(FontRole.Body, on ? MusicChrome.Purple : MusicChrome.Mute, TextAlign.Center));
            frame.Text.DrawIn(cell.BottomSlice(frame.Units(18f)), Tabs[index],
                new TextStyle(FontRole.Caption, on ? MusicChrome.Purple : MusicChrome.Mute, TextAlign.Center));
            if (frame.Input.ConsumeClick(cell))
            {
                OpenTab((MusicTab)index);
            }
        }
    }

    private void DrawMini(in AppletFrame frame, Rect row)
    {
        var now = audio.Now;
        MusicChrome.GlowPlate(frame, row, frame.Units(12f), audio.Phase == HandsetAudioPhase.Playing);
        var inset = row.Inset(frame.Units(10f));
        frame.Text.DrawEllipsized(inset.Inset(new Edges(0f, 0f, frame.Units(36f), 0f)).TopSlice(frame.Units(16f)),
            now.Title.Length > 0 ? now.Title : "Radio", new TextStyle(FontRole.CaptionStrong, MusicChrome.Ink));
        var line = audio.Phase == HandsetAudioPhase.Failed ? audio.Notice :
            audio.Phase == HandsetAudioPhase.Buffering ? "Connecting…" : now.Detail;
        frame.Text.DrawEllipsized(inset.Inset(new Edges(0f, 0f, frame.Units(36f), 0f)).BottomSlice(frame.Units(14f)),
            line, new TextStyle(FontRole.Caption, MusicChrome.Mute));
        var play = inset.RightSlice(frame.Units(28f));
        frame.Text.DrawIn(play, audio.Phase == HandsetAudioPhase.Playing ? "❚❚" : "▶",
            new TextStyle(FontRole.BodyStrong, MusicChrome.Purple, TextAlign.Center));
        if (frame.Input.ConsumeClick(play))
        {
            audio.Toggle();
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

    private void DrawPublicRow(in AppletFrame frame, Rect row, PublicStation station, bool allowOpen)
    {
        var current = string.Equals(audio.Now.Id, station.Id, StringComparison.OrdinalIgnoreCase);
        var playing = current && audio.Phase is HandsetAudioPhase.Playing or HandsetAudioPhase.Buffering;
        var saved = state.Favorites.Contains(station.Id);
        MusicChrome.GlowPlate(frame, row, frame.Units(12f), current);
        var inset = row.Inset(frame.Units(8f));
        var play = inset.LeftSlice(frame.Units(32f));
        frame.Paint.FillCircle(play.Center, frame.Units(12f), current ? MusicChrome.Purple : MusicChrome.PurpleDim);
        frame.Text.DrawIn(play, playing ? "❚❚" : "▶",
            new TextStyle(FontRole.CaptionStrong, MusicChrome.Ink, TextAlign.Center));
        var star = inset.RightSlice(frame.Units(34f));
        var body = inset.Inset(new Edges(frame.Units(38f), 0f, frame.Units(34f), 0f));
        frame.Text.DrawEllipsized(body.TopSlice(frame.Units(18f)), station.Title,
            new TextStyle(FontRole.BodyStrong, MusicChrome.Ink));
        frame.Text.DrawEllipsized(body.BottomSlice(frame.Units(16f)),
            station.Place + (station.Bitrate > 0 ? " · " + station.Bitrate + "k" : string.Empty) +
            (current ? playing ? " · Playing" : " · Paused" : string.Empty),
            new TextStyle(FontRole.Caption, MusicChrome.Mute));
        frame.Paint.Fill(star, saved ? MusicChrome.PurpleDim : MusicChrome.CardHi, frame.Units(8f));
        frame.Text.DrawIn(star, saved ? "★" : "☆",
            new TextStyle(FontRole.Title, MusicChrome.Purple, TextAlign.Center));
        if (frame.Input.ConsumeClick(star))
        {
            state.ToggleFavorite(station.Id);
            state.Save(paths);
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
            station.Host + (station.Listeners > 0 ? " · " + station.Listeners : string.Empty),
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
        DrawStationArt(frame, face, station.ArtPath);
        var badge = row.Inset(frame.Units(8f)).RightSlice(station.Live ? frame.Units(40f) : frame.Units(56f))
            .TopSlice(frame.Units(18f));
        var body = row.Inset(new Edges(frame.Units(56f), frame.Units(8f), badge.Width + frame.Units(8f),
            frame.Units(8f)));
        frame.Text.DrawEllipsized(body.TopSlice(frame.Units(18f)), station.Name,
            new TextStyle(FontRole.BodyStrong, MusicChrome.Ink));
        frame.Text.DrawEllipsized(body.BottomSlice(frame.Units(16f)),
            (station.Host.Length > 0 ? station.Host : "DJ") +
            (station.Genre.Length > 0 ? " · " + station.Genre : string.Empty) +
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
            state.Page = state.Listener ? MusicPage.SetupListener :
                state.Dj ? MusicPage.SetupDj : MusicPage.SetupVenue;
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

    private void TunePublic(PublicStation station)
    {
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
            var url = urls[index].Trim();
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
        audio.Play(new HandsetTune(station.Id, station.Name, station.Host, station.ListenUrl, station.Live,
            station.ArtPath));

    private void OpenStation(CommunityStation station)
    {
        var url = station.ListenUrl.Length > 0 ? station.ListenUrl : OwnStation(station) ? community.OwnedListenUrl : string.Empty;
        if (url.Length > 0)
        {
            audio.Play(new HandsetTune(station.Id, station.Name, station.Host, url, station.Live, station.ArtPath));
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
        var now = audio.Now;
        var local = now.Id.Length > 0 && now.StreamUrl.Length == 0;
        if (local && audio.Phase == HandsetAudioPhase.Playing)
        {
            audio.Pause();
            sense.StopMonitor();
            return;
        }

        if (local || OwnStation(now.Id) || now.Id.Length == 0)
        {
            HearOwn();
            return;
        }

        audio.Toggle();
    }

    private void HearOwn()
    {
        var station = OwnedStation();
        if (station.Id.Length == 0 && audio.Now.Id.Length > 0 && audio.Now.StreamUrl.Length == 0)
        {
            station = new CommunityStation(audio.Now.Id, audio.Now.Title, audio.Now.Detail, state.Genre, true,
                string.Empty, 0, state.StationBio, state.StationArtPath);
        }

        if (station.Id.Length == 0)
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
            return default;
        }

        return new CommunityStation(
            state.StationId.Length > 0 ? state.StationId : community.OwnedId,
            state.StationName,
            state.DjName.Length > 0 ? state.DjName : state.DisplayName,
            state.Genre,
            community.Broadcasting,
            community.OwnedListenUrl,
            0,
            state.StationBio,
            state.StationArtPath);
    }

    private void SyncBroadcastTap()
    {
        community.UseIcecast(state.IcecastHost, "source", state.IcecastPassword);
        var preview = state.Page == MusicPage.DjDash;
        if (!community.Broadcasting && !preview)
        {
            push.Stop();
            sense.Stop();
            sense.StopMonitor();
            lastCaptureTry = 0;
            lastPushTry = 0;
            return;
        }

        if (!community.Broadcasting)
        {
            push.Stop();
        }

        if (state.CaptureId.Length > 0)
        {
            sense.Select(state.CaptureId);
            if (!string.Equals(state.CaptureId, sense.SelectedId, StringComparison.Ordinal))
            {
                state.CaptureId = sense.SelectedId;
                state.CaptureName = sense.SelectedName;
            }
        }

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
                state.Genre);
        }
    }

    private bool OwnStation(CommunityStation station) => OwnStation(station.Id);

    private bool OwnStation(string id) =>
        id.Length > 0 &&
        (string.Equals(id, community.OwnedId, StringComparison.OrdinalIgnoreCase) ||
         string.Equals(id, state.StationId, StringComparison.OrdinalIgnoreCase));

    private void TuneOwn(CommunityStation station)
    {
        community.UseIcecast(state.IcecastHost, "source", state.IcecastPassword);
        var url = community.OwnedListenUrl.Length > 0 ? community.OwnedListenUrl : station.ListenUrl;
        if (url.Length > 0)
        {
            audio.Play(new HandsetTune(station.Id, station.Name.Length > 0 ? station.Name : "Your station",
                station.Mount.Length > 0 ? "/" + station.Mount : station.Host, url, true, station.ArtPath));
            state.Open(MusicPage.Player);
            return;
        }

        audio.PlayLocal(new HandsetTune(station.Id, station.Name.Length > 0 ? station.Name : "Your station",
            community.Notice.Length > 0
                ? community.Notice
                : "Pearlgate never returned a listen URL. The Icecast mount is created on the server, not on this phone.",
            string.Empty, true, station.ArtPath));
        state.Open(MusicPage.Player);
    }

    private void PickCapture(string id, string app)
    {
        sense.Select(id);
        lastCaptureTry = 0;
        state.CaptureId = sense.SelectedId;
        state.CaptureName = sense.SelectedName;
        state.CaptureApp = MusicState.NormalizeCapture(app);
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
            state.Genre,
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
        var picked = files.PickImageFiles();
        if (picked.Count == 0)
        {
            return;
        }

        try
        {
            var source = picked[0];
            var ext = Path.GetExtension(source);
            if (ext.Length == 0)
            {
                ext = ".png";
            }

            var dest = paths.State("music-station-art" + ext);
            Directory.CreateDirectory(paths.StateDirectory);
            File.Copy(source, dest, true);
            state.StationArtPath = dest;
            state.Save(paths);
        }
        catch (IOException)
        {
        }
    }

    private static void DrawStationArt(in AppletFrame frame, Rect area, string path)
    {
        if (path.Length > 0)
        {
            var texture = frame.Textures.FromFile(path);
            if (texture is not null)
            {
                frame.Paint.ImageRounded(texture, area, Vector2.Zero, Vector2.One, Vector4.One, frame.Units(8f));
                return;
            }
        }

        frame.Paint.Fill(area, MusicChrome.PurpleDim, frame.Units(8f));
        frame.Text.DrawIn(area, "♪", new TextStyle(FontRole.CaptionStrong, MusicChrome.Purple, TextAlign.Center));
    }
}
