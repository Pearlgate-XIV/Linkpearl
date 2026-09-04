using System.Linq;
using Linkpearl.Applets;
using Linkpearl.Audio;
using Linkpearl.Geometry;
using Linkpearl.Layout;
using Linkpearl.Painting;

namespace Linkpearl.Applets.Life.Music;

public sealed partial class MusicApplet
{
    private void DrawSetup(in AppletFrame frame, Rect area)
    {
        var stack = MusicChrome.BeginSheet(frame, area, state, frame.Units(10f));
        try
        {
            var title = state.Page == MusicPage.SetupListener ? "Listener Setup" :
                state.Page == MusicPage.SetupDj ? "DJ Setup" : "Venue Setup";
            if (MusicChrome.Back(frame, stack.Take(frame.Units(28f)), title))
            {
                if (state.Onboarded)
                {
                    OpenTab(MusicTab.Profile);
                    return;
                }

                state.Page = MusicPage.Onboard;
                return;
            }

            if (state.Page == MusicPage.SetupListener)
            {
                frame.Text.DrawIn(stack.Take(frame.Units(14f)), "Display name",
                    new TextStyle(FontRole.Caption, MusicChrome.Mute));
                state.DisplayName = frame.TextField.Draw("music-name", stack.Take(frame.Units(34f)), state.DisplayName,
                    "Name");
                MusicChrome.Kicker(frame, stack.Take(frame.Units(16f)), "GENRES YOU WANT");
                DrawGenreGrid(frame, stack.Take(frame.Units(132f)));
            }
            else if (state.Page == MusicPage.SetupDj)
            {
                var art = stack.Take(frame.Units(72f));
                DrawStationArt(frame, art.LeftSlice(frame.Units(72f)), state.StationArtPath);
                var pick = art.Inset(new Edges(frame.Units(80f), frame.Units(16f), 0f, frame.Units(16f)));
                MusicChrome.Plate(frame, pick, frame.Units(10f));
                frame.Text.DrawIn(pick, state.StationArtPath.Length > 0 ? "Change artwork" : "Add artwork",
                    new TextStyle(FontRole.CaptionStrong, MusicChrome.Purple, TextAlign.Center));
                if (Tap(frame, pick) || Tap(frame, art.LeftSlice(frame.Units(72f))))
                {
                    PickStationArt();
                }

                frame.Text.DrawIn(stack.Take(frame.Units(14f)), "DJ name",
                    new TextStyle(FontRole.Caption, MusicChrome.Mute));
                state.DjName = frame.TextField.Draw("music-dj", stack.Take(frame.Units(34f)), state.DjName, "DJ name");
                frame.Text.DrawIn(stack.Take(frame.Units(14f)), "Station name",
                    new TextStyle(FontRole.Caption, MusicChrome.Mute));
                state.StationName = frame.TextField.Draw("music-station", stack.Take(frame.Units(34f)), state.StationName,
                    "Station name");
                if (state.StationMount.Length == 0 && state.StationName.Length > 0)
                {
                    state.StationMount = MusicState.SlugMount(state.StationName);
                }

                frame.Text.DrawIn(stack.Take(frame.Units(14f)), "Icecast mount",
                    new TextStyle(FontRole.Caption, MusicChrome.Mute));
                state.StationMount = MusicState.SlugMount(frame.TextField.Draw("music-mount", stack.Take(frame.Units(34f)),
                    state.StationMount, "your-mount"));
                frame.Text.DrawIn(stack.Take(frame.Units(28f)),
                    state.StationMount.Length > 0
                        ? "Pearlgate will create /" + state.StationMount + " when you save. Anyone can tune that mount."
                        : "Pick a mount name. Save sends it to Pearlgate so Icecast can create the station.",
                    new TextStyle(FontRole.Caption, MusicChrome.Mute));
                frame.Text.DrawIn(stack.Take(frame.Units(14f)), "Icecast host",
                    new TextStyle(FontRole.Caption, MusicChrome.Mute));
                state.IcecastHost = frame.TextField.Draw("music-ice-host", stack.Take(frame.Units(34f)),
                    state.IcecastHost, "host:8000");
                frame.Text.DrawIn(stack.Take(frame.Units(14f)), "Icecast source password",
                    new TextStyle(FontRole.Caption, MusicChrome.Mute));
                state.IcecastPassword = frame.TextField.Draw("music-ice-pass", stack.Take(frame.Units(34f)),
                    state.IcecastPassword, "source password");
                frame.Text.DrawIn(stack.Take(frame.Units(36f)),
                    "The phone encodes like BUTT and pushes to this Icecast. Pick what to capture below — the same Listen / Talk devices as Settings.",
                    new TextStyle(FontRole.Caption, MusicChrome.Mute));
                DrawAudioSource(frame, ref stack);
                frame.Text.DrawIn(stack.Take(frame.Units(14f)), "About the station",
                    new TextStyle(FontRole.Caption, MusicChrome.Mute));
                state.StationBio = frame.TextField.Draw("music-station-bio", stack.Take(frame.Units(48f)),
                    state.StationBio, "What you play, when you go live");
                MusicChrome.Kicker(frame, stack.Take(frame.Units(16f)), "STATION GENRE");
                DrawGenreGrid(frame, stack.Take(frame.Units(72f)));
            }
            else
            {
                frame.Text.DrawIn(stack.Take(frame.Units(14f)), "Venue name",
                    new TextStyle(FontRole.Caption, MusicChrome.Mute));
                state.VenueName = frame.TextField.Draw("music-venue", stack.Take(frame.Units(34f)), state.VenueName,
                    "Venue name");
                frame.Text.DrawIn(stack.Take(frame.Units(14f)), "World / ward / plot",
                    new TextStyle(FontRole.Caption, MusicChrome.Mute));
                state.VenuePlace = frame.TextField.Draw("music-place", stack.Take(frame.Units(34f)), state.VenuePlace,
                    game.ZoneName.Length > 0 ? game.ZoneName : "Ward and plot");
            }

            var go = stack.Take(frame.Units(44f));
            MusicChrome.Primary(frame, go, "Save & Continue");
            if (Tap(frame, go))
            {
                AdvanceSetup();
            }
        }
        finally
        {
            MusicChrome.EndSheet(frame, area, state, ref stack);
        }
    }

    private void AdvanceSetup()
    {
        if (state.Page == MusicPage.SetupListener && state.Dj)
        {
            state.Page = MusicPage.SetupDj;
            return;
        }

        if (state.Page is MusicPage.SetupListener or MusicPage.SetupDj && state.Venue)
        {
            state.Page = MusicPage.SetupVenue;
            return;
        }

        state.Onboarded = true;
        state.Page = MusicPage.Tabs;
        if (state.Dj || state.StationName.Length > 0)
        {
            if (state.StationName.Length == 0)
            {
                state.StationName = (state.DjName.Length > 0 ? state.DjName : state.DisplayName) + " FM";
            }

            if (state.StationMount.Length == 0)
            {
                state.StationMount = MusicState.SlugMount(state.StationName);
            }

            PublishStation();
            state.Tab = MusicTab.Profile;
        }

        state.Save(paths);
    }

    private void DrawGenreGrid(in AppletFrame frame, Rect area)
    {
        var cols = 4;
        var rows = Math.Max(1, (MusicState.Genres.Length + cols - 1) / cols);
        var w = area.Width / cols;
        var h = area.Height / rows;
        for (var index = 0; index < MusicState.Genres.Length; index++)
        {
            var cell = Rect.FromSize(
                new Vector2(area.Min.X + w * (index % cols) + frame.Units(2f),
                    area.Min.Y + h * (index / cols) + frame.Units(2f)),
                new Vector2(w - frame.Units(4f), h - frame.Units(4f)));
            var tag = MusicState.Genres[index];
            if (MusicChrome.Chip(frame, cell, tag, state.Interests.Contains(tag)) || Tap(frame, cell))
            {
                if (!state.Interests.Remove(tag))
                {
                    state.Interests.Add(tag);
                }
            }
        }
    }

    private void DrawStackPage(in AppletFrame frame, Rect area)
    {
        switch (state.Page)
        {
            case MusicPage.Player:
                DrawPlayer(frame, area);
                break;
            case MusicPage.DjDash:
                DrawDjDash(frame, area);
                break;
            case MusicPage.VenueDash:
                DrawVenueDash(frame, area);
                break;
            case MusicPage.Roles:
                DrawRoles(frame, area);
                break;
            case MusicPage.Switcher:
                DrawSwitcher(frame, area);
                break;
            case MusicPage.EditProfile:
                DrawEditProfile(frame, area);
                break;
            default:
                DrawUnified(frame, area);
                break;
        }
    }

    private void DrawPlayer(in AppletFrame frame, Rect area)
    {
        var stack = MusicChrome.BeginSheet(frame, area, state, frame.Units(10f));
        try
        {
            if (MusicChrome.Back(frame, stack.Take(frame.Units(28f)), "Now playing"))
            {
                state.Back();
                return;
            }

            var now = audio.Now;
            var art = stack.Take(frame.Units(180f));
            frame.Paint.Fill(art, MusicChrome.PurpleDim, frame.Units(18f));
            frame.Paint.Glow(art, MusicChrome.Purple with { W = 0.22f }, frame.Units(18f), frame.Units(10f));
            frame.Text.DrawIn(art, "♪", new TextStyle(FontRole.Display, MusicChrome.Purple, TextAlign.Center));
            MusicChrome.Title(frame, stack.Take(frame.Units(28f)), now.Title.Length > 0 ? now.Title : "Nothing playing");
            frame.Text.DrawIn(stack.Take(frame.Units(18f)),
                now.Detail.Length > 0 ? now.Detail : "Pick Radio or a LIVE broadcast in Discover",
                new TextStyle(FontRole.Caption, MusicChrome.Mute));
            if (now.Live && now.StreamUrl.Length == 0)
            {
                MusicChrome.Meter(frame, stack.Take(frame.Units(16f)), sense.Level);
                frame.Text.DrawIn(stack.Take(frame.Units(36f)),
                    sense.Notice.Length > 0
                        ? sense.Notice
                        : "Hearing the device you picked. Other people still need a Pearlgate listen URL.",
                    new TextStyle(FontRole.Caption, MusicChrome.Mute));
            }

            var bar = stack.Take(frame.Units(8f));
            frame.Paint.Fill(bar, MusicChrome.Faint, frame.Units(4f));
            if (now.Live)
            {
                frame.Paint.Fill(bar, MusicChrome.Live, frame.Units(4f));
                frame.Text.DrawIn(stack.Take(frame.Units(16f)),
                    audio.Phase == HandsetAudioPhase.Buffering ? "CONNECTING" :
                    audio.Phase == HandsetAudioPhase.Failed ? audio.Notice : "LIVE",
                    new TextStyle(FontRole.CaptionStrong, MusicChrome.Live));
            }

            DrawPlayButton(frame, stack.Take(frame.Units(56f)));
            var vol = stack.Take(frame.Units(36f));
            MusicChrome.Plate(frame, vol, frame.Units(10f));
            var fill = Math.Clamp(audio.Volume, 0f, 1f);
            frame.Paint.Fill(vol.LeftSlice(vol.Width * fill), MusicChrome.Purple with { W = 0.55f }, frame.Units(10f));
            frame.Text.DrawIn(vol, "Volume", new TextStyle(FontRole.CaptionStrong, MusicChrome.Ink, TextAlign.Center));
            if (vol.Contains(frame.Input.Pointer) && frame.Input.IsHeld())
            {
                var t = (frame.Input.Pointer.X - vol.Min.X) / MathF.Max(vol.Width, 1f);
                audio.Volume = Math.Clamp(t, 0f, 1f);
            }

            if (now.Id.Length > 0)
            {
                var star = stack.Take(frame.Units(40f));
                MusicChrome.Primary(frame, star, state.Favorites.Contains(now.Id) ? "Saved" : "Save station");
                if (Tap(frame, star))
                {
                    state.ToggleFavorite(now.Id);
                    state.Save(paths);
                }
            }

            LandscapeHold.Draw(frame.WithContent(area), display);
        }
        finally
        {
            MusicChrome.EndSheet(frame, area, state, ref stack);
        }
    }

    private void DrawPlayButton(in AppletFrame frame, Rect row)
    {
        var play = Rect.FromSize(row.Center - new Vector2(frame.Units(22f), frame.Units(22f)),
            new Vector2(frame.Units(44f), frame.Units(44f)));
        frame.Paint.FillCircle(play.Center, frame.Units(22f), MusicChrome.Purple);
        frame.Paint.Glow(play, MusicChrome.Purple with { W = 0.35f }, frame.Units(22f), frame.Units(8f));
        frame.Text.DrawIn(play, audio.Phase == HandsetAudioPhase.Playing ? "❚❚" : "▶",
            new TextStyle(FontRole.Title, MusicChrome.Ink, TextAlign.Center));
        if (Tap(frame, play))
        {
            TogglePlayback();
        }
    }

    private void DrawDjDash(in AppletFrame frame, Rect area)
    {
        var stack = MusicChrome.BeginSheet(frame, area, state, frame.Units(10f));
        try
        {
            if (MusicChrome.Back(frame, stack.Take(frame.Units(28f)), "DJ Dashboard"))
            {
                state.Back();
                return;
            }

            var head = stack.Take(frame.Units(64f));
            DrawStationArt(frame, head.LeftSlice(frame.Units(64f)), state.StationArtPath);
            var copy = head.Inset(new Edges(frame.Units(72f), 0f, 0f, 0f));
            MusicChrome.Title(frame, copy.TopSlice(frame.Units(26f)),
                state.StationName.Length > 0 ? state.StationName : "Your station");
            frame.Text.DrawIn(copy.BottomSlice(frame.Units(18f)),
                (state.DjName.Length > 0 ? state.DjName : state.DisplayName) +
                (state.StationBio.Length > 0 ? " · " + state.StationBio : string.Empty),
                new TextStyle(FontRole.Caption, MusicChrome.Mute));
            var edit = stack.Take(frame.Units(36f));
            MusicChrome.Plate(frame, edit, frame.Units(10f));
            frame.Text.DrawIn(edit, "Edit station",
                new TextStyle(FontRole.CaptionStrong, MusicChrome.Purple, TextAlign.Center));
            if (Tap(frame, edit))
            {
                state.Open(MusicPage.SetupDj);
            }

            var mount = state.StationMount.Length > 0 ? state.StationMount : community.OwnedMount;
            var onWire = community.OwnedListenUrl.Length > 0;
            frame.Text.DrawIn(stack.Take(frame.Units(20f)),
                !community.Broadcasting
                    ? "Off air"
                    : onWire
                        ? "Live · Icecast is up"
                        : "Live · waiting for the listen URL",
                new TextStyle(FontRole.BodyStrong,
                    community.Broadcasting && onWire ? MusicChrome.Live : MusicChrome.Mute));
            frame.Text.DrawIn(stack.Take(frame.Units(24f)),
                mount.Length > 0 ? "Mount /" + mount : "Add an Icecast mount in Edit station.",
                new TextStyle(FontRole.Caption, MusicChrome.Purple));
            if (onWire)
            {
                frame.Text.DrawEllipsized(stack.Take(frame.Units(20f)), community.OwnedListenUrl,
                    new TextStyle(FontRole.Caption, MusicChrome.Mute));
            }

            frame.Text.DrawIn(stack.Take(frame.Units(36f)),
                community.Broadcasting
                    ? "This phone captures the device below, encodes it, and sends it through Icecast. Anyone on LIVE tunes the same mount."
                    : "Pick Sound or Mic, choose the capture device, confirm the meter moves, then Go live.",
                new TextStyle(FontRole.Caption, MusicChrome.Mute));
            DrawAudioSource(frame, ref stack);
            DrawCaptureMeter(frame, stack.Take(frame.Units(12f)));
            if (sense.Notice.Length > 0)
            {
                frame.Text.DrawIn(stack.Take(frame.Units(28f)), sense.Notice,
                    new TextStyle(FontRole.Caption, MusicChrome.Mute));
            }

            if (push.Notice.Length > 0)
            {
                frame.Text.DrawIn(stack.Take(frame.Units(28f)), push.Notice,
                    new TextStyle(FontRole.Caption, MusicChrome.Mute));
            }

            if (community.Notice.Length > 0)
            {
                frame.Text.DrawIn(stack.Take(frame.Units(32f)), community.Notice,
                    new TextStyle(FontRole.Caption, MusicChrome.Mute));
            }

            DrawIcecastWire(frame, ref stack);

            if (community.Broadcasting && onWire)
            {
                var listen = stack.Take(frame.Units(40f));
                MusicChrome.Primary(frame, listen, "Tune my station");
                if (Tap(frame, listen))
                {
                    var owned = community.Mine.FirstOrDefault(static row => row.Live);
                    if (owned.Id.Length == 0)
                    {
                        owned = community.Mine.Count > 0 ? community.Mine[0] : default;
                    }

                    if (owned.Id.Length == 0)
                    {
                        owned = new CommunityStation(state.StationId, state.StationName,
                            state.DjName.Length > 0 ? state.DjName : state.DisplayName, state.Genre, true,
                            community.OwnedListenUrl, 0, state.StationBio, state.StationArtPath, mount);
                    }

                    TuneOwn(owned);
                }
            }

            if (!pearl.Current.SignedIn)
            {
                frame.Text.DrawIn(stack.Take(frame.Units(36f)), "Sign in on You so Pearlgate can create this Icecast mount.",
                    new TextStyle(FontRole.Caption, MusicChrome.Mute));
            }

            var go = stack.Take(frame.Units(44f));
            MusicChrome.Primary(frame, go, community.Broadcasting ? "End live" : "Go live");
            if (Tap(frame, go))
            {
                if (community.Broadcasting)
                {
                    community.EndLive();
                }
                else if (mount.Length == 0 || state.IcecastHost.Trim().Length == 0 ||
                         state.IcecastPassword.Length == 0)
                {
                    state.Save(paths);
                }
                else
                {
                    community.UseIcecast(state.IcecastHost, "source", state.IcecastPassword);
                    PublishStation();
                    community.GoLive(
                        state.StationName.Length > 0 ? state.StationName : state.DisplayName + " FM",
                        state.Genre);
                }
            }

            var mine = community.Mine;
            if (mine.Count > 0)
            {
                MusicChrome.Kicker(frame, stack.Take(frame.Units(14f)), "YOUR STATIONS");
                for (var index = 0; index < mine.Count; index++)
                {
                    DrawCommunityRow(frame, ref stack, mine[index]);
                }
            }
        }
        finally
        {
            MusicChrome.EndSheet(frame, area, state, ref stack);
        }
    }

    private void DrawVenueDash(in AppletFrame frame, Rect area)
    {
        var stack = MusicChrome.BeginSheet(frame, area, state, frame.Units(10f));
        try
        {
            if (MusicChrome.Back(frame, stack.Take(frame.Units(28f)), "Venue"))
            {
                state.Back();
                return;
            }

            MusicChrome.Title(frame, stack.Take(frame.Units(26f)),
                state.VenueName.Length > 0 ? state.VenueName : "Venue");
            frame.Text.DrawIn(stack.Take(frame.Units(40f)),
                state.VenuePlace.Length > 0 ? state.VenuePlace : "No house saved yet.",
                new TextStyle(FontRole.Caption, MusicChrome.Mute));
            frame.Text.DrawIn(stack.Take(frame.Units(48f)), "Events will show here when Pearlgate lists them.",
                new TextStyle(FontRole.Caption, MusicChrome.Mute));
        }
        finally
        {
            MusicChrome.EndSheet(frame, area, state, ref stack);
        }
    }

    private void DrawRoles(in AppletFrame frame, Rect area)
    {
        var stack = MusicChrome.BeginSheet(frame, area, state, frame.Units(10f));
        try
        {
            if (MusicChrome.Back(frame, stack.Take(frame.Units(28f)), "Manage roles"))
            {
                state.Back();
                return;
            }

            DrawRolePick(frame, stack.Take(frame.Units(56f)), "LISTENER", "Radio by genre and favorites.", isListener: true);
            DrawRolePick(frame, stack.Take(frame.Units(56f)), "DJ", "Go live on a community station.", isDj: true);
            DrawRolePick(frame, stack.Take(frame.Units(56f)), "VENUE", "House name for later events.", isVenue: true);
            var save = stack.Take(frame.Units(44f));
            MusicChrome.Primary(frame, save, "Save Changes");
            if (Tap(frame, save))
            {
                state.Save(paths);
                state.Back();
            }
        }
        finally
        {
            MusicChrome.EndSheet(frame, area, state, ref stack);
        }
    }

    private void DrawSwitcher(in AppletFrame frame, Rect area)
    {
        var stack = MusicChrome.BeginSheet(frame, area, state, frame.Units(10f));
        try
        {
            if (MusicChrome.Back(frame, stack.Take(frame.Units(28f)), "Views"))
            {
                state.Back();
                return;
            }

            if (MusicChrome.Row(frame, stack.Take(frame.Units(48f)), "Listener view", "Home and live radio", false))
            {
                state.Page = MusicPage.Tabs;
                state.Tab = MusicTab.Home;
            }

            if (state.Dj &&
                MusicChrome.Row(frame, stack.Take(frame.Units(48f)), "DJ dashboard", "Go live", false))
            {
                state.Open(MusicPage.DjDash);
            }

            if (state.Venue &&
                MusicChrome.Row(frame, stack.Take(frame.Units(48f)), "Venue", "House card", false))
            {
                state.Open(MusicPage.VenueDash);
            }

            if (MusicChrome.Row(frame, stack.Take(frame.Units(48f)), "Roles", "Listener, DJ, venue", false))
            {
                state.Open(MusicPage.Roles);
            }
        }
        finally
        {
            MusicChrome.EndSheet(frame, area, state, ref stack);
        }
    }

    private void DrawUnified(in AppletFrame frame, Rect area)
    {
        var people = Roster();
        var person = MusicRoster.Find(people, state.ViewingId) ??
                     MusicRoster.Self(state, pearl.Current, community.Broadcasting);
        var mine = person.Mine ||
                   string.Equals(person.Id, MusicRoster.SelfId(pearl.Current), StringComparison.OrdinalIgnoreCase);
        var stack = MusicChrome.BeginSheet(frame, area, state, frame.Units(10f));
        try
        {
            if (MusicChrome.Back(frame, stack.Take(frame.Units(28f)), mine ? "Your profile" : "Profile"))
            {
                state.ViewingId = string.Empty;
                state.Back();
                return;
            }

            var head = stack.Take(frame.Units(78f));
            var face = head.LeftSlice(frame.Units(64f));
            frame.Paint.FillCircle(face.Center, frame.Units(26f), MusicChrome.Purple);
            frame.Text.DrawIn(face, person.Name.Length > 0 ? person.Name[..1].ToUpperInvariant() : "♪",
                new TextStyle(FontRole.Title, MusicChrome.Ink, TextAlign.Center));
            var copy = head.Inset(new Edges(frame.Units(72f), 0f, 0f, 0f));
            MusicChrome.Title(frame, copy.TopSlice(frame.Units(26f)), person.Name);
            frame.Text.DrawEllipsized(copy.Inset(new Edges(0f, frame.Units(26f), 0f, frame.Units(28f))),
                person.Handle + " · " + person.Role,
                new TextStyle(FontRole.Caption, MusicChrome.Mute));
            if (person.Live)
            {
                MusicChrome.LiveMark(frame, copy.BottomSlice(frame.Units(18f)).LeftSlice(frame.Units(40f)));
            }

            var stats = stack.Take(frame.Units(44f));
            DrawStat(frame, stats.LeftSlice(stats.Width / 3f), state.Following.Count.ToString(), "Following");
            DrawStat(frame, stats.Inset(new Edges(stats.Width / 3f, 0f, stats.Width / 3f, 0f)),
                Math.Max(pearl.Current.Followers, 0).ToString(), "Followers");
            DrawStat(frame, stats.RightSlice(stats.Width / 3f),
                mine ? state.Favorites.Count.ToString() : person.Listeners.ToString(),
                mine ? "Saved" : "Listeners");

            var bio = person.Bio.Length > 0 ? person.Bio : mine
                ? "Add a short bio so listeners know your sound."
                : "No bio yet.";
            frame.Text.DrawIn(stack.Take(frame.Units(40f)), bio, new TextStyle(FontRole.Caption, MusicChrome.Mute));
            DrawRoleChips(frame, stack.Take(frame.Units(26f)), person, mine);

            if (mine)
            {
                var edit = stack.Take(frame.Units(40f));
                MusicChrome.Primary(frame, edit, "Edit profile");
                if (Tap(frame, edit))
                {
                    state.Open(MusicPage.EditProfile);
                }

                if (state.Dj && MusicChrome.Row(frame, stack.Take(frame.Units(48f)), "DJ Dashboard",
                        community.Broadcasting ? "On air" : "Go live", community.Broadcasting))
                {
                    state.Open(MusicPage.DjDash);
                }

                if (state.Venue && MusicChrome.Row(frame, stack.Take(frame.Units(48f)), "Venue",
                        state.VenueName.Length > 0 ? state.VenueName : "House card", false))
                {
                    state.Open(MusicPage.VenueDash);
                }

                if (MusicChrome.Row(frame, stack.Take(frame.Units(48f)), "Roles", "Listener, DJ, venue", false))
                {
                    state.Open(MusicPage.Roles);
                }

                if (state.Interests.Count > 0)
                {
                    MusicChrome.Kicker(frame, stack.Take(frame.Units(14f)), "GENRES");
                    frame.Text.DrawIn(stack.Take(frame.Units(20f)), string.Join(" · ", state.Interests),
                        new TextStyle(FontRole.Caption, MusicChrome.Mute));
                }

                return;
            }

            var follow = stack.Take(frame.Units(40f));
            var on = state.Following.Contains(person.Id);
            MusicChrome.Primary(frame, follow, on ? "Following" : "Follow");
            if (Tap(frame, follow))
            {
                var nowFollow = state.ToggleFollow(person.Id);
                if (!person.Id.StartsWith("live:", StringComparison.Ordinal))
                {
                    pearl.Follow(person.Id, nowFollow);
                }

                state.Save(paths);
            }

            if (person.Live && person.StreamUrl.Length > 0)
            {
                var tune = stack.Take(frame.Units(40f));
                MusicChrome.Plate(frame, tune, frame.Units(12f));
                frame.Text.DrawIn(tune, "Tune live station",
                    new TextStyle(FontRole.BodyStrong, MusicChrome.Purple, TextAlign.Center));
                if (Tap(frame, tune))
                {
                    audio.Play(new HandsetTune(person.Id, person.Station.Length > 0 ? person.Station : person.Name,
                        person.Name, person.StreamUrl, true));
                    state.Open(MusicPage.Player);
                }
            }

            if (person.Station.Length > 0)
            {
                MusicChrome.Kicker(frame, stack.Take(frame.Units(14f)), "STATION");
                var card = stack.Take(frame.Units(56f));
                var listed = community.Directory.FirstOrDefault(row =>
                    string.Equals("live:" + row.Id, person.Id, StringComparison.OrdinalIgnoreCase));
                DrawStationArt(frame, card.LeftSlice(frame.Units(56f)), listed.ArtPath);
                var info = card.Inset(new Edges(frame.Units(64f), 0f, 0f, 0f));
                frame.Text.DrawEllipsized(info.TopSlice(frame.Units(20f)),
                    person.Station + (person.Genre.Length > 0 ? " · " + person.Genre : string.Empty),
                    new TextStyle(FontRole.BodyStrong, MusicChrome.Ink));
                frame.Text.DrawEllipsized(info.BottomSlice(frame.Units(18f)),
                    person.Live ? "On air — tap Tune above" : "Offline",
                    new TextStyle(FontRole.Caption, MusicChrome.Mute));
            }
        }
        finally
        {
            MusicChrome.EndSheet(frame, area, state, ref stack);
        }
    }

    private static void DrawStat(in AppletFrame frame, Rect cell, string value, string label)
    {
        frame.Text.DrawIn(cell.TopSlice(frame.Units(20f)), value,
            new TextStyle(FontRole.BodyStrong, MusicChrome.Ink, TextAlign.Center));
        frame.Text.DrawIn(cell.BottomSlice(frame.Units(16f)), label,
            new TextStyle(FontRole.Caption, MusicChrome.Mute, TextAlign.Center));
    }

    private static void DrawRoleChips(in AppletFrame frame, Rect row, MusicPerson person, bool mine)
    {
        var tags = new List<string>();
        if (mine)
        {
            tags.Add("LISTENER");
            if (person.Role == "DJ")
            {
                tags.Add("DJ");
            }

            if (person.Role == "Venue")
            {
                tags.Add("VENUE");
            }
        }
        else
        {
            tags.Add(person.Role.ToUpperInvariant());
        }

        var w = row.Width / Math.Max(tags.Count, 1);
        for (var index = 0; index < tags.Count; index++)
        {
            var chip = Rect.FromSize(new Vector2(row.Min.X + w * index + 2f, row.Min.Y),
                new Vector2(w - 4f, row.Height));
            MusicChrome.Chip(frame, chip, tags[index], true);
        }
    }

    private void DrawEditProfile(in AppletFrame frame, Rect area)
    {
        var stack = MusicChrome.BeginSheet(frame, area, state, frame.Units(8f));
        try
        {
            if (MusicChrome.Back(frame, stack.Take(frame.Units(28f)), "Edit profile"))
            {
                OpenTab(MusicTab.Profile);
                return;
            }

            frame.Text.DrawIn(stack.Take(frame.Units(14f)), "Display name",
                new TextStyle(FontRole.Caption, MusicChrome.Mute));
            state.DisplayName = frame.TextField.Draw("music-edit-name", stack.Take(frame.Units(34f)), state.DisplayName,
                "Name");
            frame.Text.DrawIn(stack.Take(frame.Units(14f)), "Handle",
                new TextStyle(FontRole.Caption, MusicChrome.Mute));
            state.Handle = frame.TextField.Draw("music-edit-handle", stack.Take(frame.Units(34f)), state.Handle, "@handle");
            frame.Text.DrawIn(stack.Take(frame.Units(14f)), "Bio",
                new TextStyle(FontRole.Caption, MusicChrome.Mute));
            state.Bio = frame.TextField.Draw("music-edit-bio", stack.Take(frame.Units(48f)), state.Bio,
                "A line about your taste");
            MusicChrome.Kicker(frame, stack.Take(frame.Units(16f)), "GENRES");
            DrawGenreGrid(frame, stack.Take(frame.Units(110f)));
            var save = stack.Take(frame.Units(42f));
            MusicChrome.Primary(frame, save, "Save profile");
            if (Tap(frame, save))
            {
                if (state.Handle.Length > 0 && state.Handle[0] != '@')
                {
                    state.Handle = "@" + state.Handle.Trim();
                }

                state.Save(paths);
                OpenTab(MusicTab.Profile);
            }
        }
        finally
        {
            MusicChrome.EndSheet(frame, area, state, ref stack);
        }
    }

    private void DrawProfileTab(in AppletFrame frame, Rect area)
    {
        var person = MusicRoster.Self(state, pearl.Current, community.Broadcasting);
        var stack = MusicChrome.BeginSheet(frame, area, state, frame.Units(10f));
        try
        {
            var head = stack.Take(frame.Units(78f));
            var face = head.LeftSlice(frame.Units(64f));
            frame.Paint.FillCircle(face.Center, frame.Units(26f), MusicChrome.Purple);
            frame.Text.DrawIn(face, person.Name.Length > 0 ? person.Name[..1].ToUpperInvariant() : "♪",
                new TextStyle(FontRole.Title, MusicChrome.Ink, TextAlign.Center));
            var copy = head.Inset(new Edges(frame.Units(72f), 0f, 0f, 0f));
            MusicChrome.Title(frame, copy.TopSlice(frame.Units(26f)), person.Name);
            frame.Text.DrawEllipsized(copy.Inset(new Edges(0f, frame.Units(26f), 0f, frame.Units(28f))),
                person.Handle + " · " + person.Role,
                new TextStyle(FontRole.Caption, MusicChrome.Mute));
            if (person.Live)
            {
                MusicChrome.LiveMark(frame, copy.BottomSlice(frame.Units(18f)).LeftSlice(frame.Units(40f)));
            }

            var stats = stack.Take(frame.Units(44f));
            DrawStat(frame, stats.LeftSlice(stats.Width / 3f), state.Following.Count.ToString(), "Following");
            DrawStat(frame, stats.Inset(new Edges(stats.Width / 3f, 0f, stats.Width / 3f, 0f)),
                Math.Max(pearl.Current.Followers, 0).ToString(), "Followers");
            DrawStat(frame, stats.RightSlice(stats.Width / 3f), state.Favorites.Count.ToString(), "Saved");

            var bio = person.Bio.Length > 0 ? person.Bio : "Add a short bio so listeners know your sound.";
            frame.Text.DrawIn(stack.Take(frame.Units(40f)), bio, new TextStyle(FontRole.Caption, MusicChrome.Mute));
            DrawRoleChips(frame, stack.Take(frame.Units(26f)), person, true);

            var edit = stack.Take(frame.Units(40f));
            MusicChrome.Primary(frame, edit, "Edit profile");
            if (Tap(frame, edit))
            {
                state.Open(MusicPage.EditProfile);
            }

            DrawCreateStation(frame, ref stack);

            if (state.Interests.Count > 0)
            {
                MusicChrome.Kicker(frame, stack.Take(frame.Units(14f)), "GENRES");
                frame.Text.DrawIn(stack.Take(frame.Units(20f)), string.Join(" · ", state.Interests),
                    new TextStyle(FontRole.Caption, MusicChrome.Mute));
            }

            var people = Roster();
            var following = people.Where(row => !row.Mine && state.Following.Contains(row.Id)).ToArray();
            var live = people.Where(static row => row.Live && !row.Mine).ToArray();
            DrawPersonSection(frame, ref stack, "FOLLOWING", following);
            DrawPersonSection(frame, ref stack, "ON AIR", live);
            if (following.Length == 0 && live.Length == 0)
            {
                DrawHint(frame, ref stack, "Follow DJs from LIVE to keep them on your profile.");
            }
        }
        finally
        {
            MusicChrome.EndSheet(frame, area, state, ref stack);
        }
    }

    private void DrawCreateStation(in AppletFrame frame, ref Stack stack)
    {
        MusicChrome.Kicker(frame, stack.Take(frame.Units(14f)), "LIVE STATION");
        var card = stack.Take(frame.Units(88f));
        MusicChrome.GlowPlate(frame, card, frame.Units(12f), community.Broadcasting);
        var inset = card.Inset(frame.Units(10f));
        if (state.StationName.Length == 0)
        {
            frame.Text.DrawIn(inset.TopSlice(frame.Units(20f)), "Create a station",
                new TextStyle(FontRole.BodyStrong, MusicChrome.Ink));
            frame.Text.DrawEllipsized(inset.BottomSlice(frame.Units(48f)),
                "Artwork, name, and a short station bio. It appears on LIVE as offline until you go live from the phone.",
                new TextStyle(FontRole.Caption, MusicChrome.Mute));
            if (Tap(frame, card))
            {
                state.Dj = true;
                state.Open(MusicPage.SetupDj);
            }

            return;
        }

        DrawStationArt(frame, inset.LeftSlice(frame.Units(64f)), state.StationArtPath);
        var body = inset.Inset(new Edges(frame.Units(72f), 0f, 0f, 0f));
        if (community.Broadcasting)
        {
            MusicChrome.LiveMark(frame, body.TopSlice(frame.Units(16f)).LeftSlice(frame.Units(40f)));
        }
        else
        {
            MusicChrome.OffMark(frame, body.TopSlice(frame.Units(16f)).LeftSlice(frame.Units(56f)));
        }

        frame.Text.DrawEllipsized(body.Inset(new Edges(0f, frame.Units(18f), 0f, frame.Units(22f))),
            state.StationName,
            new TextStyle(FontRole.BodyStrong, MusicChrome.Ink));
        frame.Text.DrawEllipsized(body.BottomSlice(frame.Units(20f)),
            community.Broadcasting ? "You are live · Manage" : "Go live from the phone",
            new TextStyle(FontRole.CaptionStrong, MusicChrome.Purple));
        if (Tap(frame, card))
        {
            state.Open(MusicPage.DjDash);
        }
    }

    private void DrawIcecastWire(in AppletFrame frame, ref Stack stack)
    {
        var host = state.IcecastHost;
        var password = state.IcecastPassword;
        frame.Text.DrawIn(stack.Take(frame.Units(14f)), "Icecast host",
            new TextStyle(FontRole.Caption, MusicChrome.Mute));
        state.IcecastHost = frame.TextField.Draw("music-dash-ice-host", stack.Take(frame.Units(34f)),
            state.IcecastHost, "host:8000");
        frame.Text.DrawIn(stack.Take(frame.Units(14f)), "Icecast source password",
            new TextStyle(FontRole.Caption, MusicChrome.Mute));
        state.IcecastPassword = frame.TextField.Draw("music-dash-ice-pass", stack.Take(frame.Units(34f)),
            state.IcecastPassword, "source password");
        if (state.IcecastHost.Trim().Length == 0 || state.IcecastPassword.Length == 0)
        {
            frame.Text.DrawIn(stack.Take(frame.Units(36f)),
                "Pearlgate cannot create the mount yet. Host and source password are required so this phone can SOURCE like BUTT.",
                new TextStyle(FontRole.Caption, MusicChrome.Mute));
        }

        if (!string.Equals(host, state.IcecastHost, StringComparison.Ordinal) ||
            !string.Equals(password, state.IcecastPassword, StringComparison.Ordinal))
        {
            community.UseIcecast(state.IcecastHost, "source", state.IcecastPassword);
            state.Save(paths);
        }
    }

    private void DrawCaptureMeter(in AppletFrame frame, Rect area)
    {
        MusicChrome.Plate(frame, area, frame.Units(6f));
        var fill = Math.Clamp(sense.Level, 0f, 1f);
        if (fill > 0.02f)
        {
            frame.Paint.Fill(area.LeftSlice(Math.Max(frame.Units(8f), area.Width * fill)), MusicChrome.Live,
                frame.Units(6f));
        }
    }

    private void DrawAudioSource(in AppletFrame frame, ref Stack stack)
    {
        if (ports.Speakers.Count == 0 && ports.Microphones.Count == 0)
        {
            ports.Refresh();
        }

        sense.RefreshPoints();
        MusicChrome.Kicker(frame, stack.Take(frame.Units(14f)), "CAPTURE");
        frame.Text.DrawIn(stack.Take(frame.Units(32f)),
            "Sound captures what that playback device is playing. Mic captures Talk through. Listen through is where you hear the phone.",
            new TextStyle(FontRole.Caption, MusicChrome.Mute));
        var mic = state.CaptureApp == "mic";
        var chips = stack.Take(frame.Units(36f));
        var soundHit = chips.LeftSlice(chips.Width * 0.5f).Inset(new Edges(0f, 0f, frame.Units(4f), 0f));
        var micHit = chips.RightSlice(chips.Width * 0.5f).Inset(new Edges(frame.Units(4f), 0f, 0f, 0f));
        if (MusicChrome.Chip(frame, soundHit, "Sound", !mic))
        {
            PickCapture(CaptureTapId("sound", PortFromTap(state.CaptureId, sound: true)), "sound");
            return;
        }

        if (MusicChrome.Chip(frame, micHit, "Mic", mic))
        {
            var talk = PortFromTap(state.CaptureId, sound: false);
            if (talk.Length == 0)
            {
                talk = display.MicrophoneId.Length > 0 ? display.MicrophoneId : ports.DefaultMicrophoneId;
            }

            if (talk.Length == 0 && ports.Microphones.Count > 0)
            {
                talk = ports.Microphones[0].Id;
            }

            PickCapture(CaptureTapId("mic", talk), "mic");
            return;
        }

        DrawPortMenu(frame, ref stack, mic ? "music-capture-mic" : "music-capture-sound",
            mic ? "Talk through · capture" : "Capture device",
            mic ? ports.Microphones : ports.Speakers,
            mic ? ports.DefaultMicrophoneId : ports.DefaultSpeakerId,
            PortFromTap(state.CaptureId, !mic),
            value =>
            {
                if (mic)
                {
                    display.MicrophoneId = value;
                }

                PickCapture(CaptureTapId(mic ? "mic" : "sound", value), mic ? "mic" : "sound");
            });
        DrawPortMenu(frame, ref stack, "music-listen-speaker", "Listen through", ports.Speakers,
            ports.DefaultSpeakerId, display.SpeakerId, ApplyListen);
        var scan = stack.Take(frame.Units(32f));
        MusicChrome.Plate(frame, scan, frame.Units(8f));
        frame.Text.DrawIn(scan, "Rescan · " + ports.Status,
            new TextStyle(FontRole.CaptionStrong, MusicChrome.Purple, TextAlign.Center));
        if (Tap(frame, scan))
        {
            ports.Refresh();
            sense.RescanPoints();
        }
    }

    private void DrawPortMenu(in AppletFrame frame, ref Stack stack, string id, string title,
        IReadOnlyList<AudioPort> list, string defaultId, string currentId, Action<string> set)
    {
        frame.Text.DrawIn(stack.Take(frame.Units(16f)), title,
            new TextStyle(FontRole.Caption, MusicChrome.Mute));
        var row = stack.Take(frame.Units(40f));
        MusicChrome.Plate(frame, row, frame.Units(10f));
        var labels = new string[list.Count + 1];
        labels[0] = "Windows default";
        var selected = 0;
        for (var index = 0; index < list.Count; index++)
        {
            var port = list[index];
            labels[index + 1] = port.Id == defaultId ? port.Label + " · default" : port.Label;
            if (currentId.Length > 0 && string.Equals(currentId, port.Id, StringComparison.Ordinal))
            {
                selected = index + 1;
            }
        }

        var picked = frame.TextField.Combo(id, row.Inset(frame.Units(6f)), labels, selected);
        frame.Input.Claim(row);
        if (picked != selected)
        {
            set(picked == 0 ? string.Empty : list[picked - 1].Id);
        }
    }

    private void ApplyListen(string speakerId)
    {
        display.SpeakerId = speakerId;
        audio.UseSpeaker(speakerId);
        sense.RoutePhone(speakerId, display.MicrophoneId);
    }

    private static string CaptureTapId(string app, string portId)
    {
        if (app == "mic")
        {
            if (portId.Length == 0)
            {
                return string.Empty;
            }

            return portId.StartsWith("wavein:", StringComparison.Ordinal) ||
                   portId.StartsWith("in:", StringComparison.Ordinal)
                ? portId
                : "in:" + portId;
        }

        if (portId.Length == 0)
        {
            return IBroadcastSense.DefaultMixId;
        }

        return portId.StartsWith("waveout:", StringComparison.Ordinal) ||
               portId.StartsWith("out:", StringComparison.Ordinal)
            ? portId
            : "out:" + portId;
    }

    private static string PortFromTap(string tapId, bool sound)
    {
        if (tapId.Length == 0 || tapId == IBroadcastSense.DefaultMixId)
        {
            return string.Empty;
        }

        if (sound)
        {
            if (tapId.StartsWith("out:", StringComparison.Ordinal))
            {
                return tapId[4..];
            }

            return tapId.StartsWith("waveout:", StringComparison.Ordinal) ? tapId : string.Empty;
        }

        if (tapId.StartsWith("in:", StringComparison.Ordinal))
        {
            return tapId[3..];
        }

        return tapId.StartsWith("wavein:", StringComparison.Ordinal) ? tapId : string.Empty;
    }

    private static bool Tap(in AppletFrame frame, Rect area) =>
        frame.Input.PressedInside(area) || frame.Input.ConsumeClick(area);
}
