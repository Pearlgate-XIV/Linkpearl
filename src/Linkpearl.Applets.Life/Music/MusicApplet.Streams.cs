using System.Globalization;
using Linkpearl.Applets.Life.Venues;
using Linkpearl.Audio;
using Linkpearl.Geometry;
using Linkpearl.Layout;
using Linkpearl.Media;
using Linkpearl.Net;
using Linkpearl.Painting;

namespace Linkpearl.Applets.Life.Music;

public sealed partial class MusicApplet
{
    private CommunityStation[] MergedLive() =>
        MergedBoard().Where(static row => row.Live).ToArray();

    private CommunityStation[] BroadcastLive() =>
        BroadcastBoard().Where(static row => row.Live).ToArray();

    private CommunityStation[] BroadcastBoard()
    {
        var now = Environment.TickCount64;
        if (broadcastBoardAt != 0 && now - broadcastBoardAt < 1000)
        {
            return broadcastBoard;
        }

        var byId = new Dictionary<string, CommunityStation>(StringComparer.OrdinalIgnoreCase);
        void Put(CommunityStation station)
        {
            if (!IsBroadcast(station))
            {
                return;
            }

            var id = MusicState.BareStationId(station.Id);
            if (id.Length == 0 || id.StartsWith("twitch:", StringComparison.OrdinalIgnoreCase))
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

        var owned = OwnedStation();
        if (owned.Id.Length > 0)
        {
            Put(owned);
        }

        broadcastBoard = byId.Values
            .OrderByDescending(static row => row.Live)
            .ThenBy(static row => row.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        broadcastBoardAt = now;
        return broadcastBoard;
    }

    private CommunityStation[] TwitchBoard()
    {
        var now = Environment.TickCount64;
        if (twitchBoardAt != 0 && now - twitchBoardAt < 1000)
        {
            return twitchBoard;
        }

        var board = streams.Live;
        if (board.Count == 0)
        {
            twitchBoard = [];
            twitchBoardAt = now;
            return twitchBoard;
        }

        var rows = new List<CommunityStation>(board.Count);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < board.Count; index++)
        {
            var station = FromStream(board[index]);
            if (station.Id.Length == 0 || !station.Live || !seen.Add(station.Id))
            {
                continue;
            }

            rows.Add(station);
        }

        twitchBoard = rows
            .OrderByDescending(static row => row.Viewers)
            .ThenBy(static row => row.Host, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        twitchBoardAt = now;
        return twitchBoard;
    }

    private static bool IsBroadcast(CommunityStation station) =>
        station.ListenUrl.Length > 0 ||
        string.Equals(station.Source, "linkpearl", StringComparison.OrdinalIgnoreCase) ||
        station.Source.Length == 0;

    private CommunityStation[] MergedBoard()
    {
        var now = Environment.TickCount64;
        if (mergedBoardAt != 0 && now - mergedBoardAt < 1000)
        {
            return mergedBoard;
        }

        var byId = new Dictionary<string, CommunityStation>(StringComparer.OrdinalIgnoreCase);
        var claimed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void Put(CommunityStation station)
        {
            var id = MusicState.BareStationId(station.Id);
            if (id.Length == 0)
            {
                return;
            }

            if (MatchStream(station) is { } stream)
            {
                var login = StreamChrome.LoginOf(stream.Username);
                if (login.Length > 0)
                {
                    claimed.Add(login);
                }

                station = Enrich(station, stream);
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

        var board = streams.Live;
        for (var index = 0; index < board.Count; index++)
        {
            var row = board[index];
            var login = StreamChrome.LoginOf(row.Username);
            if (login.Length == 0 || !claimed.Add(login))
            {
                continue;
            }

            var station = FromStream(row);
            if (station.Id.Length == 0 || byId.ContainsKey(station.Id))
            {
                continue;
            }

            byId[station.Id] = station;
        }

        mergedBoard = byId.Values
            .OrderByDescending(static row => row.Live)
            .ThenByDescending(static row => row.Listeners)
            .ThenByDescending(static row => row.Viewers)
            .ThenBy(static row => row.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        mergedBoardAt = now;
        return mergedBoard;
    }

    private StreamInfo? MatchStream(CommunityStation station)
    {
        var keys = new[]
        {
            station.TwitchLogin,
            station.WatchUrl,
            station.Host,
            station.Name,
        };
        for (var index = 0; index < keys.Length; index++)
        {
            var hit = streams.Find(keys[index]);
            if (hit is { } found)
            {
                return found;
            }
        }

        var board = streams.Live;
        for (var index = 0; index < board.Count; index++)
        {
            var row = board[index];
            if (string.Equals(row.DisplayName, station.Host, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(row.DisplayName, station.Name, StringComparison.OrdinalIgnoreCase))
            {
                return row;
            }
        }

        return null;
    }

    private static CommunityStation Enrich(CommunityStation station, StreamInfo stream)
    {
        return station with
        {
            WatchUrl = station.WatchUrl.Length > 0 ? station.WatchUrl : stream.WatchUrl,
            Viewers = Math.Max(station.Viewers, stream.Viewers),
            TwitchLogin = station.TwitchLogin.Length > 0 ? station.TwitchLogin : stream.Username,
            VenueLine = station.VenueLine.Length > 0 ? station.VenueLine : stream.VenueLine,
            Lifestream = station.Lifestream.Length > 0 ? station.Lifestream : stream.Lifestream,
            ArtPath = station.ArtPath.Length > 0 ? station.ArtPath : stream.ArtUrl,
            Bio = station.Bio.Length > 0 ? station.Bio : stream.Bio,
            Source = station.Source.Length > 0 ? station.Source : "linkpearl",
        };
    }

    private static CommunityStation FromStream(StreamInfo stream)
    {
        var login = StreamChrome.LoginOf(stream.Username);
        var name = stream.DisplayName.Length > 0 ? stream.DisplayName : login;
        return new CommunityStation(
            StreamChrome.StreamId(login),
            stream.Title.Length > 0 ? stream.Title : name,
            name,
            stream.Genre,
            stream.Status == StreamStatus.Live,
            string.Empty,
            0,
            stream.Bio,
            stream.ArtUrl,
            string.Empty,
            0,
            false,
            stream.WatchUrl.Length > 0 ? stream.WatchUrl : StreamChrome.WatchUrl(login),
            Math.Max(0, stream.Viewers),
            login,
            stream.VenueLine,
            stream.Lifestream,
            stream.Source.Length > 0 ? stream.Source : "twitch");
    }

    private static void DrawStationAudience(in AppletFrame frame, Rect area, CommunityStation station, bool compact)
    {
        MusicChrome.Audience(frame, area, Math.Max(0, station.Listeners), Math.Max(0, station.Viewers), compact);
    }

    private static float StationAudienceWidth(in AppletFrame frame, CommunityStation station, bool compact) =>
        MusicChrome.AudienceWidth(frame, Math.Max(0, station.Listeners), Math.Max(0, station.Viewers), compact);

    private void DrawConnectedServices(in AppletFrame frame, ref LayoutFlow stack)
    {
        MusicChrome.Kicker(frame, stack.Take(frame.Units(16f)), "CONNECTED SERVICES");
        var own = streams.Own;
        if (streams.Notice.Contains("Mock", StringComparison.OrdinalIgnoreCase))
        {
            MusicChrome.FitCopy(frame, ref stack,
                "Developer mock Twitch (alyxfm / lunawave / nyxdeck). Not Pearlgate.",
                MusicChrome.Mute);
        }

        if (own.Connected)
        {
            var label = own.DisplayName.Length > 0 ? own.DisplayName : own.Username;
            MusicChrome.FitCopy(frame, ref stack,
                own.Verified
                    ? (label.Length > 0 ? label + " · " : string.Empty) + "Twitch Account Verified"
                    : (label.Length > 0 ? label + " · " : string.Empty) + "Twitch connected",
                MusicChrome.Ink, FontRole.BodyStrong);
            MusicChrome.FitCopy(frame, ref stack,
                "OAuth ownership only. Disconnect keeps your station, posts, and events.",
                MusicChrome.Mute);
            var drop = stack.Take(frame.Units(40f));
            MusicChrome.Ghost(frame, drop, "Disconnect Twitch");
            if (frame.Input.ConsumeClick(drop))
            {
                streams.Disconnect();
            }
        }
        else
        {
            MusicChrome.FitCopy(frame, ref stack,
                "Link Twitch through Pearlgate. The phone never stores Twitch tokens.",
                MusicChrome.Mute);
            var go = stack.Take(frame.Units(42f));
            MusicChrome.Primary(frame, go, streams.Busy ? "Connecting…" : "Connect Twitch");
            if (frame.Input.ConsumeClick(go) && !streams.Busy)
            {
                streams.RequestConnect();
            }
        }

        MusicChrome.FitCopy(frame, ref stack, streams.Notice, MusicChrome.Mute);
    }

    private void DrawBroadcastRow(in AppletFrame frame, Rect area, CommunityStation station)
    {
        MusicChrome.Plate(frame, area, frame.Units(16f));
        var inset = area.Inset(frame.Units(10f));
        var face = CoverFit.InscribedSquare(inset.LeftSlice(inset.Height));
        DrawDjFace(frame, face, station);
        if (station.Live)
        {
            var pulse = frame.Units(5f);
            var mark = new Vector2(face.Max.X - pulse, face.Max.Y - pulse);
            frame.Paint.FillCircle(mark, pulse, MusicChrome.LiveOn);
            frame.Paint.StrokeCircle(mark, pulse, MusicChrome.Card, frame.Units(1.4f));
        }

        var follow = inset.RightSlice(frame.Units(78f)).TopSlice(frame.Units(24f));
        var hearN = ShownListeners(station.Listeners,
            station.Live ||
            string.Equals(audio.Now.Id, station.Id, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(MusicState.BareStationId(audio.Now.Id), station.Id, StringComparison.OrdinalIgnoreCase));
        var hear = inset.RightSlice(MathF.Min(inset.Width * 0.42f,
                MusicChrome.ListenerWidth(frame, hearN, true)))
            .BottomSlice(frame.Units(18f));
        var body = inset.Inset(new Edges(face.Width + frame.Units(10f), 0f, follow.Width + frame.Units(8f), 0f));
        var title = station.Name.Length > 0 ? station.Name : "Station";
        frame.Text.DrawEllipsized(body.TopSlice(frame.Units(18f)), title,
            new TextStyle(FontRole.BodyStrong, MusicChrome.Ink));

        var mid = body.Inset(new Edges(0f, frame.Units(20f), 0f, frame.Units(18f)));
        var flag = station.Live ? "LIVE" : "Offline";
        var flagW = frame.Units(station.Live ? 32f : 48f);
        frame.Text.DrawIn(mid.LeftSlice(flagW), flag,
            new TextStyle(FontRole.CaptionStrong, station.Live ? MusicChrome.LiveOn : MusicChrome.Mute));
        var host = station.Host.Length > 0 ? station.Host : "DJ";
        frame.Text.DrawEllipsized(mid.Inset(new Edges(flagW + frame.Units(6f), 0f, 0f, 0f)), host,
            new TextStyle(FontRole.Caption, MusicChrome.Mute));
        frame.Text.DrawEllipsized(body.BottomSlice(frame.Units(16f)), ShownStationGenre(station),
            new TextStyle(FontRole.Caption, MusicChrome.Mute));
        MusicChrome.ListenerCount(frame, hear, hearN, compact: true);

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

        if (frame.Input.ConsumeClick(area))
        {
            OpenStation(station);
        }
    }

    private void DrawTwitchDjRow(in AppletFrame frame, Rect area, CommunityStation station)
    {
        MusicChrome.Plate(frame, area, frame.Units(16f));
        var inset = area.Inset(frame.Units(10f));
        var face = CoverFit.InscribedSquare(inset.LeftSlice(inset.Height));
        DrawDjFace(frame, face, station);
        if (station.Live)
        {
            var pulse = frame.Units(5f);
            var mark = new Vector2(face.Max.X - pulse, face.Max.Y - pulse);
            frame.Paint.FillCircle(mark, pulse, MusicChrome.LiveOn);
            frame.Paint.StrokeCircle(mark, pulse, MusicChrome.Card, frame.Units(1.4f));
        }

        var follow = inset.RightSlice(frame.Units(78f)).TopSlice(frame.Units(24f));
        var body = inset.Inset(new Edges(face.Width + frame.Units(10f), 0f, follow.Width + frame.Units(8f), 0f));
        var name = station.Host.Length > 0 ? station.Host : station.Name;
        frame.Text.DrawEllipsized(body.TopSlice(frame.Units(18f)), name.Length > 0 ? name : "DJ",
            new TextStyle(FontRole.BodyStrong, MusicChrome.Ink));

        var mid = body.Inset(new Edges(0f, frame.Units(20f), 0f, frame.Units(18f)));
        var liveW = station.Live ? frame.Units(32f) : 0f;
        if (station.Live)
        {
            frame.Text.DrawIn(mid.LeftSlice(liveW), "LIVE",
                new TextStyle(FontRole.CaptionStrong, MusicChrome.LiveOn));
        }

        var login = station.TwitchLogin.Length > 0 ? "@" + station.TwitchLogin : string.Empty;
        var watchers = station.Viewers > 0
            ? (station.Viewers == 1 ? "1 watching" : station.Viewers + " watching")
            : string.Empty;
        var meta = login.Length > 0 && watchers.Length > 0
            ? login + "  ·  " + watchers
            : login.Length > 0 ? login : watchers;
        frame.Text.DrawEllipsized(mid.Inset(new Edges(liveW + (liveW > 0 ? frame.Units(6f) : 0f), 0f, 0f, 0f)),
            meta, new TextStyle(FontRole.Caption, MusicChrome.Mute));

        var place = station.VenueLine.Length > 0
            ? station.VenueLine
            : ShownStationGenre(station).Length > 0
                ? ShownStationGenre(station)
                : !string.Equals(station.Name, name, StringComparison.OrdinalIgnoreCase)
                    ? station.Name
                    : string.Empty;
        frame.Text.DrawEllipsized(body.BottomSlice(frame.Units(16f)), place,
            new TextStyle(FontRole.Caption, MusicChrome.Mute));

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

        if (frame.Input.ConsumeClick(area))
        {
            OpenStation(station);
        }
    }

    private void DrawCommunitySchedule(in AppletFrame frame, ref LayoutFlow stack)
    {
        MusicChrome.Kicker(frame, stack.Take(frame.Units(16f)), "Community schedule");
        var rows = UpcomingSchedule();
        if (rows.Length == 0)
        {
            DrawHint(frame, ref stack, "No upcoming community sets.");
        }
        else
        {
            var take = Math.Min(rows.Length, 8);
            for (var index = 0; index < take; index++)
            {
                DrawScheduleRow(frame, stack.Take(frame.Units(60f)), rows[index]);
            }
        }

        DrawRolladeckCredit(frame, stack.Take(frame.Units(22f)));
    }

    private static void DrawRolladeckCredit(in AppletFrame frame, Rect area)
    {
        frame.Text.DrawIn(area, "Community listings from Rolladeck",
            new TextStyle(FontRole.Caption, MusicChrome.Mute, TextAlign.Center));
        if (frame.Input.ConsumeClick(area))
        {
            VenuesChrome.OpenUrl("https://xivrolladeck.com");
        }
    }

    private StreamScheduleMark[] UpcomingSchedule()
    {
        var now = DateTimeOffset.UtcNow;
        return streams.CommunitySchedule
            .Where(row => row.Ends == default ? row.Starts >= now.AddHours(-2) : row.Ends >= now)
            .OrderBy(static row => row.Starts)
            .ToArray();
    }

    private void DrawScheduleRow(in AppletFrame frame, Rect area, StreamScheduleMark mark)
    {
        MusicChrome.Plate(frame, area, frame.Units(14f));
        var inset = area.Inset(new Edges(frame.Units(12f), frame.Units(8f)));
        var watch = ResolveWatch(mark);
        var canVisit = StreamChrome.TryParseLi(mark.Lifestream, out _, out _, out _, out _);
        var action = canVisit ? "Visit" : watch.Length > 0 ? "Watch" : string.Empty;
        var trail = action.Length > 0 ? inset.RightSlice(frame.Units(48f)) : Rect.Empty;
        var copy = inset.Inset(new Edges(0f, 0f, trail.Width > 0 ? trail.Width + frame.Units(8f) : 0f, 0f));
        frame.Text.DrawEllipsized(copy.TopSlice(frame.Units(18f)),
            mark.Title.Length > 0 ? mark.Title : mark.DjName,
            new TextStyle(FontRole.BodyStrong, MusicChrome.Ink));
        var where = mark.VenueName.Length > 0
            ? mark.VenueName + (mark.Place.Length > 0 ? " · " + mark.Place : string.Empty)
            : mark.Place;
        var who = mark.DjName.Length > 0 ? mark.DjName : "Community set";
        frame.Text.DrawEllipsized(copy.BottomSlice(frame.Units(16f)),
            WhenLine(mark.Starts, mark.Ends) + "  ·  " + who + (where.Length > 0 ? "  ·  " + where : string.Empty),
            new TextStyle(FontRole.Caption, MusicChrome.Mute));
        if (trail.Width > 0)
        {
            frame.Text.DrawIn(trail, action,
                new TextStyle(FontRole.CaptionStrong, MusicChrome.Purple, TextAlign.Right));
        }

        if (frame.Input.ConsumeClick(trail) && canVisit)
        {
            VisitHouse(mark.Lifestream);
            return;
        }

        if (frame.Input.ConsumeClick(area))
        {
            if (watch.Length > 0)
            {
                VenuesChrome.OpenUrl(watch);
                return;
            }

            if (canVisit)
            {
                VisitHouse(mark.Lifestream);
            }
        }
    }

    private string ResolveWatch(StreamScheduleMark mark)
    {
        if (mark.WatchUrl.Length > 0)
        {
            return mark.WatchUrl;
        }

        if (mark.TwitchLogin.Length > 0)
        {
            return StreamChrome.WatchUrl(mark.TwitchLogin);
        }

        if (mark.DjName.Length == 0)
        {
            return string.Empty;
        }

        var board = streams.Live;
        for (var index = 0; index < board.Count; index++)
        {
            var row = board[index];
            if (string.Equals(row.DisplayName, mark.DjName, StringComparison.OrdinalIgnoreCase))
            {
                return row.WatchUrl.Length > 0 ? row.WatchUrl : StreamChrome.WatchUrl(row.Username);
            }
        }

        return string.Empty;
    }

    private string WhenLine(DateTimeOffset starts, DateTimeOffset ends)
    {
        var local = starts.ToLocalTime();
        var time = display.Use24HourClock
            ? local.ToString("ddd HH:mm", CultureInfo.CurrentCulture)
            : local.ToString("ddd h:mm tt", CultureInfo.CurrentCulture);
        var now = DateTimeOffset.UtcNow;
        if (starts <= now && (ends == default || ends >= now))
        {
            return "Now · " + time;
        }

        return time;
    }

    private void VisitHouse(string command)
    {
        if (!StreamChrome.TryParseLi(command, out var world, out var district, out var ward, out var plot))
        {
            return;
        }

        if (!lifestream.Ready)
        {
            notices.PostMusic("lifestream", "Lifestream", "Install and enable Lifestream, then try again.", clock);
            return;
        }

        if (lifestream.TryGoHome(world, district, ward, plot, 0, false))
        {
            notices.PostMusic("lifestream", "Lifestream", "Heading to " + world + " · " + district + ".", clock);
            return;
        }

        notices.PostMusic("lifestream", "Lifestream", "Lifestream could not start that trip.", clock);
    }

    private CommunityStation[] SearchLiveHits(string query)
    {
        var text = query.Trim();
        if (text.Length == 0)
        {
            return [];
        }

        return MergedBoard()
            .Where(row =>
                row.Name.Contains(text, StringComparison.OrdinalIgnoreCase) ||
                row.Host.Contains(text, StringComparison.OrdinalIgnoreCase) ||
                row.Genre.Contains(text, StringComparison.OrdinalIgnoreCase) ||
                row.TwitchLogin.Contains(text, StringComparison.OrdinalIgnoreCase) ||
                row.VenueLine.Contains(text, StringComparison.OrdinalIgnoreCase))
            .Take(8)
            .ToArray();
    }
}
