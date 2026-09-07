using System.Collections.Generic;
using System.Linq;
using Linkpearl.Applets;
using Linkpearl.Audio;
using Linkpearl.Geometry;
using Linkpearl.Layout;
using Linkpearl.Media;
using Linkpearl.Painting;
using Linkpearl.Preferences;

namespace Linkpearl.Applets.Life.Music;

public sealed partial class MusicApplet
{
    private void DrawSetup(in AppletFrame frame, Rect area)
    {
        var stack = MusicChrome.BeginSheet(frame, area, state, frame.Units(10f));
        try
        {
            var title = state.Page == MusicPage.SetupListener ? "Listener Setup" :
                state.Page == MusicPage.SetupDj
                    ? state.Onboarded ? "Edit station" : "DJ Setup"
                    : "Venue Setup";
            if (MusicChrome.Back(frame, stack.Take(frame.Units(28f)), title))
            {
                if (state.Onboarded)
                {
                    if (state.Page == MusicPage.SetupDj && state.StationName.Length > 0)
                    {
                        state.Back();
                        return;
                    }

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
                SetSharedName(frame.TextField.Draw("music-name-" + profileStamp, stack.Take(frame.Units(34f)),
                    EditableName(), "Name"));
                MusicChrome.Kicker(frame, stack.Take(frame.Units(16f)), "GENRES YOU WANT");
                DrawGenreGrid(frame, stack.Take(frame.Units(220f)));
            }
            else if (state.Page == MusicPage.SetupDj)
            {
                var art = stack.Take(frame.Units(72f));
                DrawStationArt(frame, art.LeftSlice(frame.Units(72f)), state.StationArtPath, state.StationId,
                    state.StationName);
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
                MusicChrome.FitCopy(frame, ref stack,
                    state.StationMount.Length > 0
                        ? "Pearlgate will list /" + state.StationMount + " for everyone. Icecast host below is only if you run your own relay."
                        : "Name the mount. Save publishes the station so others can find it on LIVE.",
                    MusicChrome.Mute);
                DrawIcecastWire(frame, ref stack);
                DrawAudioSource(frame, ref stack);
                DrawCaptureMeter(frame, stack.Take(frame.Units(12f)));
                MusicChrome.FitCopy(frame, ref stack, sense.Notice, MusicChrome.Mute);
                MusicChrome.FitCopy(frame, ref stack, push.Notice, MusicChrome.Mute);
                MusicChrome.Kicker(frame, stack.Take(frame.Units(16f)), "STATION GENRE");
                DrawHashtagEditor(frame, stack.Take(frame.Units(118f)), state.StationTags, "music-station-tag",
                    station: true);
                MusicChrome.Kicker(frame, stack.Take(frame.Units(16f)), "ABOUT THE STATION");
                var about = stack.Take(frame.Units(140f));
                MusicChrome.Plate(frame, about, frame.Units(10f));
                state.StationBio = frame.TextField.Write("music-station-bio", about.Inset(frame.Units(8f)),
                    state.StationBio, "What you play, when you go live", MusicState.StationBioCap);
                frame.Text.DrawIn(stack.Take(frame.Units(16f)),
                    state.StationBio.Length + " / " + MusicState.StationBioCap,
                    new TextStyle(FontRole.Caption, MusicChrome.Mute, TextAlign.Right));
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
            var slug = MusicState.NormalizeHashtag(tag);
            var on = state.Interests.Exists(item =>
                string.Equals(MusicState.NormalizeHashtag(item), slug, StringComparison.Ordinal));
            if (MusicChrome.Chip(frame, cell, tag, on) || Tap(frame, cell))
            {
                if (state.Interests.RemoveAll(item =>
                        string.Equals(MusicState.NormalizeHashtag(item), slug, StringComparison.Ordinal)) == 0
                    && slug.Length > 0)
                {
                    state.Interests.Add(slug);
                }

                state.Save(paths);
            }
        }
    }

    private void DrawHashtagEditor(in AppletFrame frame, Rect area) =>
        DrawHashtagEditor(frame, area, state.Interests, "music-edit-tag", station: false);

    private void DrawHashtagEditor(in AppletFrame frame, Rect area, List<string> tags, string fieldId, bool station)
    {
        var gap = frame.Units(6f);
        var chipH = frame.Units(28f);
        var x = area.Min.X;
        var y = area.Min.Y;
        for (var index = 0; index < tags.Count; index++)
        {
            var label = MusicState.FormatHashtag(tags[index]);
            if (label.Length == 0)
            {
                continue;
            }

            var width = Math.Min(area.Width,
                Math.Max(frame.Units(52f), frame.Text.Measure(label, FontRole.CaptionStrong).X + frame.Units(22f)));
            if (x > area.Min.X && x + width > area.Max.X)
            {
                x = area.Min.X;
                y += chipH + gap;
            }

            if (y + chipH > area.Max.Y - frame.Units(38f))
            {
                break;
            }

            var cell = Rect.FromSize(new Vector2(x, y), new Vector2(width, chipH));
            if (MusicChrome.Chip(frame, cell, label, true))
            {
                tags.RemoveAt(index);
                return;
            }

            x += width + gap;
        }

        var fieldTop = Math.Min(area.Max.Y - frame.Units(34f),
            x > area.Min.X ? y + chipH + gap : y);
        var row = Rect.FromSize(new Vector2(area.Min.X, fieldTop),
            new Vector2(area.Width, frame.Units(34f)));
        var add = row.RightSlice(frame.Units(56f));
        var input = Rect.FromSize(row.Min, new Vector2(add.Min.X - gap - row.Min.X, row.Height));
        var draft = station ? state.StationTagDraft : state.GenreTagDraft;
        draft = frame.TextField.Draw(fieldId, input, draft, "#genre");
        if (station)
        {
            state.StationTagDraft = draft;
        }
        else
        {
            state.GenreTagDraft = draft;
        }

        if (MusicChrome.Chip(frame, add, "Add", draft.Length > 0) || Tap(frame, add))
        {
            if (station)
            {
                state.TryAddStationTag(draft);
            }
            else
            {
                state.TryAddHashtag(draft);
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
            case MusicPage.GenreList:
                DrawGenreList(frame, area);
                break;
            case MusicPage.Playlist:
                DrawPlaylistPage(frame, area);
                break;
            case MusicPage.PickPlaylist:
                DrawPickPlaylist(frame, area);
                break;
            case MusicPage.PlacePhoto:
                DrawPlacePhoto(frame, area);
                break;
            case MusicPage.PickPhoto:
                DrawPickPhoto(frame, area);
                break;
            case MusicPage.Settings:
                DrawSettings(frame, area);
                break;
            default:
                DrawUnified(frame, area);
                break;
        }
    }

    private void DrawPlayer(in AppletFrame frame, Rect area)
    {
        if (state.ReportOpen)
        {
            frame.Input.Claim(area);
        }

        state.Scroll = 0f;
        var gap = frame.Units(8f);
        var top = area.TopSlice(frame.Units(28f));
        var now = audio.Now;
        var name = now.Title ?? string.Empty;
        var blurb = now.Detail ?? string.Empty;
        var id = now.Id ?? string.Empty;
        var live = TryFollowableNow(out var liveStation);
        if (MusicChrome.Back(frame, top, string.Empty))
        {
            state.Back();
            return;
        }
        var playing = audio.Phase is HandsetAudioPhase.Playing or HandsetAudioPhase.Buffering;
        TickPlayerWave(id, playing, frame.DeltaSeconds);

        var even = frame.Units(16f);
        var actionsH = live ? frame.Units(28f) : frame.Units(28f) + even + frame.Units(40f);
        var floorPad = live ? frame.Units(52f) : 0f;
        var actions = new Rect(new Vector2(area.Min.X, area.Max.Y - floorPad - actionsH),
            new Vector2(area.Max.X, area.Max.Y - floorPad));
        var stage = new Rect(new Vector2(area.Min.X, top.Max.Y + gap),
            new Vector2(area.Max.X, actions.Min.Y - even));
        var title = stage.TopSlice(frame.Units(24f));
        var detail = new Rect(new Vector2(stage.Min.X, title.Max.Y + frame.Units(2f)),
            new Vector2(stage.Max.X, title.Max.Y + frame.Units(18f)));
        var heading = title;
        if (live)
        {
            var flagSide = frame.Units(28f) * 1.15f;
            var flag = Rect.FromSize(
                new Vector2(title.Max.X - flagSide, title.Center.Y - flagSide * 0.5f),
                new Vector2(flagSide));
            heading = title.Inset(new Edges(0f, 0f, flag.Width + frame.Units(6f), 0f));
            MusicChrome.ReportFlag(frame, flag);
            if (MusicChrome.LiveMarkHit(frame, flag))
            {
                OpenReport(liveStation.Id.Length > 0 ? liveStation.Id : id, name);
            }
        }
        else
        {
            var likeW = frame.Units(40f);
            var likeH = frame.Units(28f);
            var like = Rect.FromSize(
                new Vector2(title.Max.X - likeW, title.Center.Y - likeH * 0.5f),
                new Vector2(likeW, likeH));
            heading = title.Inset(new Edges(0f, 0f, like.Width + frame.Units(6f), 0f));
            DrawLiveLike(frame, like, id);
        }

        MusicChrome.Title(frame, heading, name.Length > 0 ? name : "Nothing playing");
        var tags = live ? ShownStationGenre(liveStation) : string.Empty;
        if (tags.Length == 0 && live && OwnStation(liveStation))
        {
            tags = state.StationGenreLine;
        }

        var failed = audio.Phase == HandsetAudioPhase.Failed && audio.Notice.Length > 0;
        var caption = failed
            ? audio.Notice
            : live && liveStation.Host.Length > 0
                ? liveStation.Host
                : blurb.Length > 0 ? blurb : "Pick a station";
        frame.Text.DrawEllipsized(detail, caption, new TextStyle(FontRole.Caption, MusicChrome.Mute));
        var tagBand = Rect.Empty;
        if (!failed && tags.Length > 0)
        {
            tagBand = Rect.FromSize(new Vector2(detail.Min.X, detail.Max.Y + frame.Units(2f)),
                new Vector2(detail.Width, frame.Units(16f)));
            frame.Text.DrawEllipsized(tagBand, tags,
                new TextStyle(FontRole.CaptionStrong, MusicChrome.Purple));
        }

        var transport = new Rect(new Vector2(stage.Min.X, stage.Max.Y - frame.Units(56f)),
            new Vector2(stage.Max.X, stage.Max.Y));
        var wave = new Rect(new Vector2(stage.Min.X, transport.Min.Y - even - frame.Units(36f)),
            new Vector2(stage.Max.X, transport.Min.Y - even));
        var artTop = tagBand.Height > 0 ? tagBand.Max.Y + gap : detail.Max.Y + gap;
        var artBand = new Rect(new Vector2(stage.Min.X, artTop),
            new Vector2(stage.Max.X, wave.Min.Y - gap));
        if (artBand.Height > 8f && artBand.Width > 8f)
        {
            var marks = live ? frame.Units(30f) : 0f;
            var artArea = live
                ? new Rect(artBand.Min, new Vector2(artBand.Max.X, artBand.Max.Y - marks - gap))
                : artBand;
            var side = MathF.Min(artArea.Width, MathF.Max(8f, artArea.Height)) * 0.65f;
            var art = Rect.FromSize(artArea.Center - new Vector2(side * 0.5f), new Vector2(side));
            DrawStationArt(frame, art, now.ArtPath ?? string.Empty, id, name);
            if (live)
            {
                DrawLiveStationMarks(frame, art, liveStation, id);
            }
        }

        var glow = Math.Clamp(playerPlayed / 2.4f, 0f, 0.5f);
        MusicChrome.DockWave(frame, wave, id, glow, playerWaveScroll, MusicChrome.DockBlue);
        DrawTransport(frame, transport);
        DrawPlayerActions(frame, actions, now);
        LandscapeHold.Draw(frame.WithContent(area), display);
        if (state.ReportOpen)
        {
            DrawReportSheet(frame, area);
        }
    }

    private void DrawLiveStationMarks(in AppletFrame frame, Rect art, CommunityStation station, string tuneId)
    {
        var side = frame.Units(28f);
        var like = frame.Units(36f);
        var gap = frame.Units(6f);
        var row = Rect.FromSize(
            new Vector2(art.Max.X - like - side * 2f - gap * 2f, art.Max.Y + frame.Units(4f)),
            new Vector2(like + side * 2f + gap * 2f, side));
        var heart = row.LeftSlice(like);
        var save = row.Inset(new Edges(like + gap, 0f, side + gap, 0f));
        var follow = row.RightSlice(side);
        DrawLiveLike(frame, heart, tuneId.Length > 0 ? tuneId : station.Id);
        var saveId = !string.IsNullOrEmpty(tuneId) ? tuneId : station.Id;
        var saved = saveId.Length > 0 &&
                    (state.Favorites.Contains(saveId) || state.Favorites.Contains(station.Id));
        var following = state.FollowsStationId(station.Id);
        MusicChrome.SaveStar(frame, save, saved);
        MusicChrome.FollowPerson(frame, follow, following);
        if (MusicChrome.LiveMarkHit(frame, save) && saveId.Length > 0)
        {
            state.ToggleFavorite(saveId);
            state.Save(paths);
        }

        if (MusicChrome.LiveMarkHit(frame, follow) && !string.IsNullOrEmpty(station.Id))
        {
            ToggleFollowLive(station);
        }
    }

    private void DrawLiveLike(in AppletFrame frame, Rect area, string stationId)
    {
        var id = MusicState.BareStationId(stationId);
        if (id.Length == 0)
        {
            return;
        }

        var liked = community.StationLiked(id);
        var color = liked ? MusicChrome.LikePink : MusicChrome.LikePink with { W = 0.72f };
        var glyph = area.LeftSlice(area.Height);
        frame.Text.DrawIn(glyph, liked ? "♥" : "♡",
            new TextStyle(FontRole.Body, color, TextAlign.Center));
        frame.Text.DrawIn(area.Inset(new Edges(glyph.Width, 0f, 0f, 0f)), community.StationLikes(id).ToString(),
            new TextStyle(FontRole.Caption, color, TextAlign.Center));
        TapStationLike(frame, area, id);
    }

    private void DrawReportSheet(in AppletFrame frame, Rect area)
    {
        frame.Paint.Fill(area, new Vector4(0f, 0f, 0f, 0.62f));
        var card = Rect.FromSize(
            new Vector2(area.Min.X + frame.Units(16f), area.Center.Y - frame.Units(168f)),
            new Vector2(area.Width - frame.Units(32f), frame.Units(300f)));
        MusicChrome.Plate(frame, card, frame.Units(16f));
        var stack = new Stack(card.Inset(frame.Units(14f)), StackAxis.Vertical, frame.Units(8f));
        MusicChrome.Title(frame, stack.Take(frame.Units(26f)), "Report");
        frame.Text.DrawIn(stack.Take(frame.Units(16f)), "Select reason",
            new TextStyle(FontRole.Caption, MusicChrome.Mute));
        var reason = stack.Take(frame.Units(40f));
        MusicChrome.Plate(frame, reason, frame.Units(10f));
        state.ReportReason = frame.TextField.Combo("music-report-reason", reason.Inset(frame.Units(6f)),
            ReportReasons, state.ReportReason);
        frame.Text.DrawIn(stack.Take(frame.Units(28f)), "Add additional information/details (optional)",
            new TextStyle(FontRole.Caption, MusicChrome.Mute));
        var note = stack.Take(frame.Units(72f));
        MusicChrome.Plate(frame, note, frame.Units(10f));
        state.ReportDetail = frame.TextField.Write("music-report-detail", note.Inset(frame.Units(8f)),
            state.ReportDetail, "Add additional information/details (optional)", 800);
        var row = stack.Take(frame.Units(40f));
        var cancel = row.LeftSlice(row.Width * 0.48f);
        var send = row.RightSlice(row.Width * 0.48f);
        MusicChrome.Plate(frame, cancel, frame.Units(12f));
        frame.Text.DrawIn(cancel, "Cancel",
            new TextStyle(FontRole.CaptionStrong, MusicChrome.Ink, TextAlign.Center));
        var ready = state.ReportReason > 0 && !desk.Busy;
        MusicChrome.Primary(frame, send, desk.Busy ? "Sending…" : "Submit");
        // The player already claimed the screen so this sheet can sit on top. ConsumeClick
        // would fail; a raw click on Cancel (or the dimmer) drops the report.
        if (frame.Input.WasClicked(cancel) ||
            (!card.Contains(frame.Input.Pointer) && frame.Input.WasClicked(area)))
        {
            frame.TextField.Release();
            CloseReport();
            return;
        }

        if (ready && Tap(frame, send))
        {
            SubmitReport();
        }
    }

    private void DrawPlayerActions(in AppletFrame frame, Rect area, HandsetTune now)
    {
        var gap = frame.Units(8f);
        var live = TryFollowableNow(out _);
        var vol = live ? area : area.TopSlice(frame.Units(28f));
        MusicChrome.DockVolume(frame, vol, Math.Clamp(audio.Volume, 0f, 1f), ref playerVolumeDrag,
            value => audio.Volume = value);
        if (live || string.IsNullOrEmpty(now.Id))
        {
            return;
        }

        var row = area.BottomSlice(frame.Units(40f));
        var add = row.LeftSlice((row.Width - gap) * 0.5f);
        var save = row.RightSlice((row.Width - gap) * 0.5f);
        MusicChrome.Plate(frame, add, frame.Units(12f));
        frame.Text.DrawIn(add, "Add to playlist",
            new TextStyle(FontRole.CaptionStrong, MusicChrome.Ink, TextAlign.Center));
        if (Tap(frame, add))
        {
            OfferPlaylist(new PublicStation(now.Id, now.Title, state.Genre, now.Detail, now.StreamUrl, 0,
                now.ArtPath));
        }

        MusicChrome.Primary(frame, save, state.Favorites.Contains(now.Id) ? "Saved" : "Save station");
        if (Tap(frame, save))
        {
            state.ToggleFavorite(now.Id);
            state.Save(paths);
        }
    }

    private void TickPlayerWave(string id, bool playing, float delta)
    {
        if (!string.Equals(playerWaveId, id, StringComparison.Ordinal))
        {
            playerWaveId = id ?? string.Empty;
            playerPlayed = 0f;
            playerWaveScroll = 0f;
        }

        if (string.IsNullOrEmpty(id))
        {
            playerPlayed = 0f;
            playerWaveScroll = 0f;
            return;
        }

        if (playing)
        {
            var step = Math.Clamp(delta, 0f, 0.05f);
            playerPlayed += step;
            playerWaveScroll += step * 9f;
        }
    }

    private void DrawTransport(in AppletFrame frame, Rect row)
    {
        var play = Rect.FromSize(row.Center - new Vector2(frame.Units(24f), frame.Units(24f)),
            new Vector2(frame.Units(48f), frame.Units(48f)));
        var prev = Rect.FromSize(new Vector2(play.Min.X - frame.Units(56f), row.Center.Y - frame.Units(18f)),
            new Vector2(frame.Units(36f), frame.Units(36f)));
        var next = Rect.FromSize(new Vector2(play.Max.X + frame.Units(20f), row.Center.Y - frame.Units(18f)),
            new Vector2(frame.Units(36f), frame.Units(36f)));
        var skipOn = CanStepStation();
        frame.Paint.FillCircle(prev.Center, frame.Units(16f), MusicChrome.CardHi);
        frame.Paint.FillCircle(next.Center, frame.Units(16f), MusicChrome.CardHi);
        frame.Text.DrawIn(prev, "‹",
            new TextStyle(FontRole.Title, skipOn ? MusicChrome.Ink : MusicChrome.Mute, TextAlign.Center));
        frame.Paint.FillCircle(play.Center, frame.Units(24f), MusicChrome.Purple);
        frame.Text.DrawIn(play,
            audio.Phase is HandsetAudioPhase.Playing or HandsetAudioPhase.Buffering ? "❚❚" : "▶",
            new TextStyle(FontRole.Title, MusicChrome.GroundHi, TextAlign.Center));
        frame.Text.DrawIn(next, "›",
            new TextStyle(FontRole.Title, skipOn ? MusicChrome.Ink : MusicChrome.Mute, TextAlign.Center));
        if (Tap(frame, play))
        {
            TogglePlayback();
        }
        else if (Tap(frame, prev))
        {
            TryStepStation(-1);
        }
        else if (Tap(frame, next))
        {
            TryStepStation(1);
        }
    }

    private bool CanStepStation() =>
        state.Queue.Count > 1 || publicRadio.Stations(state.Genre).Count > 1;

    private void TryStepStation(int delta)
    {
        if (state.StepQueue(delta) is { } queued)
        {
            TunePublic(queued.ToPublic(), keepQueue: true);
            return;
        }

        var list = publicRadio.Stations(state.Genre);
        if (list.Count == 0)
        {
            return;
        }

        var index = 0;
        for (var i = 0; i < list.Count; i++)
        {
            if (string.Equals(list[i].Id, audio.Now.Id, StringComparison.OrdinalIgnoreCase))
            {
                index = i;
                break;
            }
        }

        var count = list.Count;
        TunePublic(list[((index + delta) % count + count) % count]);
    }

    private void DrawGenreList(in AppletFrame frame, Rect area)
    {
        publicRadio.Ensure(state.Genre);
        var stations = publicRadio.Stations(state.Genre);
        RevealGenreStation(frame, stations);
        var stack = MusicChrome.BeginSheet(frame, area, state, frame.Units(10f));
        try
        {
            if (MusicChrome.Back(frame, stack.Take(frame.Units(28f)), state.Genre))
            {
                state.Back();
                return;
            }

            WarmStationArt(stations);
            frame.Text.DrawIn(stack.Take(frame.Units(16f)),
                publicRadio.Busy && stations.Count == 0
                    ? "Finding stations…"
                    : stations.Count + " stations",
                new TextStyle(FontRole.Caption, MusicChrome.Mute));

            var actions = stack.Take(frame.Units(40f));
            var play = actions.LeftSlice(actions.Width * 0.5f).Inset(new Edges(0f, 0f, frame.Units(4f), 0f));
            var save = actions.RightSlice(actions.Width * 0.5f).Inset(new Edges(frame.Units(4f), 0f, 0f, 0f));
            MusicChrome.Primary(frame, play, "Play all");
            MusicChrome.Plate(frame, save, frame.Units(12f));
            frame.Text.DrawIn(save, "Save playlist",
                new TextStyle(FontRole.CaptionStrong, MusicChrome.Ink, TextAlign.Center));
            if (Tap(frame, play) && stations.Count > 0)
            {
                PlayMarks(stations.Select(MusicStationMark.From).ToArray(), 0);
                return;
            }

            if (Tap(frame, save) && stations.Count > 0)
            {
                var list = state.CreatePlaylist(state.Genre);
                foreach (var station in stations)
                {
                    list.Put(MusicStationMark.From(station));
                }

                state.Save(paths);
                OpenPlaylist(list.Id);
                return;
            }

            if (stations.Count == 0)
            {
                DrawHint(frame, ref stack,
                    publicRadio.Busy ? "Loading this genre…" : "No stations in " + state.Genre + " yet.");
            }
            else
            {
                DrawPublicRows(frame, ref stack, stations, stations.Count);
            }
        }
        finally
        {
            MusicChrome.EndSheet(frame, area, state, ref stack);
        }
    }

    private void DrawPlaylistShelf(in AppletFrame frame, ref Stack stack)
    {
        var head = stack.Take(frame.Units(22f));
        frame.Text.DrawIn(head, "Playlists", new TextStyle(FontRole.BodyStrong, MusicChrome.Ink));
        var make = stack.Take(frame.Units(38f));
        var field = make.Inset(new Edges(0f, 0f, frame.Units(76f), 0f));
        var go = make.RightSlice(frame.Units(70f));
        frame.Paint.Fill(field, MusicChrome.CardHi, field.Height * 0.5f);
        state.DraftName = frame.TextField.Draw("music-playlist-name", field.Inset(new Edges(frame.Units(12f), 0f)),
            state.DraftName, "New playlist");
        MusicChrome.Primary(frame, go, "Create");
        if (Tap(frame, go) && state.DraftName.Trim().Length > 0)
        {
            var list = state.CreatePlaylist(state.DraftName);
            state.Save(paths);
            OpenPlaylist(list.Id);
            return;
        }

        if (state.Playlists.Count == 0)
        {
            DrawHint(frame, ref stack, "Create a playlist, or tap + on a station.");
            return;
        }

        for (var index = 0; index < state.Playlists.Count; index++)
        {
            var list = state.Playlists[index];
            var row = stack.Take(frame.Units(52f));
            MusicChrome.Plate(frame, row, frame.Units(12f));
            var inset = row.Inset(frame.Units(10f));
            var play = inset.LeftSlice(frame.Units(32f));
            var drop = inset.RightSlice(frame.Units(28f));
            frame.Text.DrawIn(play, "▶", new TextStyle(FontRole.BodyStrong, MusicChrome.Purple, TextAlign.Center));
            frame.Text.DrawIn(drop, "×", new TextStyle(FontRole.Body, MusicChrome.Mute, TextAlign.Center));
            var copy = inset.Inset(new Edges(frame.Units(36f), 0f, frame.Units(32f), 0f));
            frame.Text.DrawEllipsized(copy.TopSlice(frame.Units(18f)), list.Name,
                new TextStyle(FontRole.BodyStrong, MusicChrome.Ink));
            frame.Text.DrawEllipsized(copy.BottomSlice(frame.Units(16f)),
                list.Stations.Count == 1 ? "1 station" : list.Stations.Count + " stations",
                new TextStyle(FontRole.Caption, MusicChrome.Mute));
            if (Tap(frame, drop))
            {
                state.Playlists.RemoveAt(index);
                state.Save(paths);
                return;
            }

            if (Tap(frame, play) && list.Stations.Count > 0)
            {
                PlayMarks(list.Stations, 0);
                return;
            }

            if (Tap(frame, row))
            {
                OpenPlaylist(list.Id);
                return;
            }
        }
    }

    private void DrawPlaylistPage(in AppletFrame frame, Rect area)
    {
        var list = state.FindPlaylist(state.ViewingPlaylistId);
        if (list is null)
        {
            state.Back();
            return;
        }

        var stack = MusicChrome.BeginSheet(frame, area, state, frame.Units(10f));
        try
        {
            if (MusicChrome.Back(frame, stack.Take(frame.Units(28f)), list.Name))
            {
                state.Tab = MusicTab.Library;
                state.Back();
                return;
            }

            frame.Text.DrawIn(stack.Take(frame.Units(16f)),
                list.Stations.Count == 1 ? "1 radio station" : list.Stations.Count + " radio stations",
                new TextStyle(FontRole.Caption, MusicChrome.Mute));

            var play = stack.Take(frame.Units(40f));
            MusicChrome.Primary(frame, play, list.Stations.Count > 0 ? "Play playlist" : "Empty playlist");
            if (Tap(frame, play) && list.Stations.Count > 0)
            {
                PlayMarks(list.Stations, 0);
                return;
            }

            if (list.Stations.Count == 0)
            {
                DrawHint(frame, ref stack, "Open a genre and tap + to add stations here.");
                return;
            }

            for (var index = 0; index < list.Stations.Count; index++)
            {
                DrawPublicRow(frame, stack.Take(frame.Units(52f)), list.Stations[index].ToPublic(), true, list.Id);
            }
        }
        finally
        {
            MusicChrome.EndSheet(frame, area, state, ref stack);
        }
    }

    private void DrawPickPlaylist(in AppletFrame frame, Rect area)
    {
        var stack = MusicChrome.BeginSheet(frame, area, state, frame.Units(10f));
        try
        {
            if (MusicChrome.Back(frame, stack.Take(frame.Units(28f)), "Add to playlist"))
            {
                state.Pending = null;
                state.Back();
                return;
            }

            if (state.Pending is { } pending)
            {
                frame.Text.DrawEllipsized(stack.Take(frame.Units(18f)), pending.Title,
                    new TextStyle(FontRole.BodyStrong, MusicChrome.Ink));
                frame.Text.DrawEllipsized(stack.Take(frame.Units(16f)), pending.Place,
                    new TextStyle(FontRole.Caption, MusicChrome.Mute));
            }

            var make = stack.Take(frame.Units(38f));
            var field = make.Inset(new Edges(0f, 0f, frame.Units(76f), 0f));
            var go = make.RightSlice(frame.Units(70f));
            frame.Paint.Fill(field, MusicChrome.CardHi, field.Height * 0.5f);
            state.DraftName = frame.TextField.Draw("music-playlist-pick", field.Inset(new Edges(frame.Units(12f), 0f)),
                state.DraftName, "New playlist");
            MusicChrome.Primary(frame, go, "Create");
            if (Tap(frame, go) && state.DraftName.Trim().Length > 0)
            {
                var list = state.CreatePlaylist(state.DraftName);
                state.AddPending(list.Id);
                state.Save(paths);
                state.Back();
                return;
            }

            if (state.Playlists.Count == 0)
            {
                DrawHint(frame, ref stack, "Name a playlist and tap Create.");
                return;
            }

            MusicChrome.Kicker(frame, stack.Take(frame.Units(18f)), "Your playlists");
            for (var index = 0; index < state.Playlists.Count; index++)
            {
                var list = state.Playlists[index];
                var row = stack.Take(frame.Units(48f));
                MusicChrome.Plate(frame, row, frame.Units(12f));
                frame.Text.DrawEllipsized(row.Inset(new Edges(frame.Units(12f), frame.Units(8f), frame.Units(12f),
                    frame.Units(20f))), list.Name, new TextStyle(FontRole.BodyStrong, MusicChrome.Ink));
                frame.Text.DrawIn(row.Inset(frame.Units(12f)).BottomSlice(frame.Units(16f)),
                    list.Stations.Count + " stations",
                    new TextStyle(FontRole.Caption, MusicChrome.Mute));
                if (Tap(frame, row))
                {
                    state.AddPending(list.Id);
                    state.Save(paths);
                    state.Back();
                    return;
                }
            }
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
        frame.Text.DrawIn(play,
            audio.Phase is HandsetAudioPhase.Playing or HandsetAudioPhase.Buffering ? "❚❚" : "▶",
            new TextStyle(FontRole.Title, MusicChrome.GroundHi, TextAlign.Center));
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
            DrawStationArt(frame, head.LeftSlice(frame.Units(64f)), state.StationArtPath, state.StationId,
                state.StationName);
            var copy = head.Inset(new Edges(frame.Units(72f), 0f, 0f, 0f));
            MusicChrome.Title(frame, copy.TopSlice(frame.Units(26f)),
                state.StationName.Length > 0 ? state.StationName : "Your station");
            frame.Text.DrawEllipsized(copy.BottomSlice(frame.Units(18f)),
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
                            state.DjName.Length > 0 ? state.DjName : state.DisplayName, state.StationGenreLine, true,
                            community.OwnedListenUrl, 0, state.StationBio, state.StationArtPath, mount);
                    }

                    TuneOwn(owned);
                }
            }

            var go = stack.Take(frame.Units(44f));
            MusicChrome.Primary(frame, go, community.Broadcasting ? "End live" : "Go live");
            if (Tap(frame, go))
            {
                if (community.Broadcasting)
                {
                    community.EndLive();
                }
                else
                {
                    if (mount.Length == 0)
                    {
                        state.StationMount = MusicState.SlugMount(
                            state.StationName.Length > 0 ? state.StationName : state.DisplayName);
                    }

                    community.UseIcecast(state.IcecastHost, "source", state.IcecastPassword);
                    PublishStation();
                    community.GoLive(
                        state.StationName.Length > 0 ? state.StationName : state.DisplayName + " FM",
                        state.StationGenreLine);
                }
            }

            MusicChrome.FitCopy(frame, ref stack,
                !community.Broadcasting
                    ? "Off air"
                    : onWire
                        ? "Live · Icecast is up"
                        : "Live · waiting for the listen URL",
                community.Broadcasting && onWire ? MusicChrome.Live : MusicChrome.Mute, FontRole.BodyStrong);
            MusicChrome.FitCopy(frame, ref stack,
                mount.Length > 0 ? "Mount /" + mount : "Add an Icecast mount in Edit station.",
                MusicChrome.Purple);
            if (onWire)
            {
                MusicChrome.FitCopy(frame, ref stack, community.OwnedListenUrl, MusicChrome.Mute);
            }

            if (state.StationBio.Length > 0)
            {
                MusicChrome.FitCopy(frame, ref stack, state.StationBio, MusicChrome.Mute);
            }

            if (state.StationGenreLine.Length > 0)
            {
                MusicChrome.FitCopy(frame, ref stack, state.StationGenreLine, MusicChrome.Purple);
            }

            MusicChrome.FitCopy(frame, ref stack, community.Notice, MusicChrome.Mute);
            if (!pearl.Current.SignedIn)
            {
                MusicChrome.FitCopy(frame, ref stack,
                    "Sign in on You so Pearlgate lists this station for everyone else.",
                    MusicChrome.Mute);
            }
        }
        finally
        {
            MusicChrome.EndSheet(frame, area, state, ref stack);
        }
    }

    private void DrawSettings(in AppletFrame frame, Rect area)
    {
        var stack = MusicChrome.BeginSheet(frame, area, state, frame.Units(10f));
        try
        {
            if (MusicChrome.Back(frame, stack.Take(frame.Units(28f)), "Music settings"))
            {
                state.Back();
                return;
            }

            MusicChrome.Kicker(frame, stack.Take(frame.Units(16f)), "PLAYBACK");
            var vol = stack.Take(frame.Units(40f));
            MusicChrome.Plate(frame, vol, frame.Units(10f));
            var fill = Math.Clamp(audio.Volume, 0f, 1f);
            frame.Paint.Fill(vol.LeftSlice(vol.Width * fill), MusicChrome.DockBlue with { W = 0.72f }, frame.Units(10f));
            frame.Text.DrawIn(vol, "Volume", new TextStyle(FontRole.CaptionStrong, MusicChrome.Ink, TextAlign.Center));
            if (vol.Contains(frame.Input.Pointer) && frame.Input.IsHeld())
            {
                audio.Volume = Math.Clamp((frame.Input.Pointer.X - vol.Min.X) / MathF.Max(1f, vol.Width), 0f, 1f);
            }

            MusicChrome.Kicker(frame, stack.Take(frame.Units(16f)), "HOME");
            if (MusicChrome.Row(frame, stack.Take(frame.Units(48f)), "Your profile", MarkedName(), false))
            {
                OpenTab(MusicTab.Profile);
                return;
            }

            if (MusicChrome.Row(frame, stack.Take(frame.Units(48f)), "Roles",
                    (state.Listener ? "Listener" : "") + (state.Dj ? (state.Listener ? " · DJ" : "DJ") : "") +
                    (state.Venue ? (state.Listener || state.Dj ? " · Venue" : "Venue") : ""), false))
            {
                state.Open(MusicPage.Roles);
                return;
            }

            MusicChrome.Kicker(frame, stack.Take(frame.Units(16f)), "STATION");
            if (MusicChrome.Row(frame, stack.Take(frame.Units(48f)),
                    state.StationName.Length > 0 ? state.StationName : "Create a station",
                    community.Broadcasting ? "You are live" : "Artwork, name, and go live", community.Broadcasting))
            {
                if (state.StationName.Length > 0)
                {
                    state.Open(MusicPage.DjDash);
                }
                else
                {
                    state.Dj = true;
                    state.Open(MusicPage.SetupDj);
                }

                return;
            }

            MusicChrome.Kicker(frame, stack.Take(frame.Units(16f)), "PREFERRED GENRES");
            DrawGenreGrid(frame, stack.Take(frame.Units(220f)));
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
                     MusicRoster.Self(state, pearl.Current, community.Broadcasting, MarkedName());
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
                new TextStyle(FontRole.Title, MusicChrome.GroundHi, TextAlign.Center));
            var copy = head.Inset(new Edges(frame.Units(72f), 0f, 0f, 0f));
            var honor = mine ? SharedHonorific() : string.Empty;
            if (honor.Length > 0)
            {
                DrawFlowName(frame, copy.TopSlice(frame.Units(26f)), SharedName());
                frame.Text.DrawEllipsized(copy.Inset(new Edges(0f, frame.Units(26f), 0f, 0f)).TopSlice(frame.Units(16f)),
                    honor, new TextStyle(FontRole.CaptionStrong, MusicChrome.Ink));
            }
            else if (mine)
            {
                DrawFlowName(frame, copy.TopSlice(frame.Units(26f)), SharedName());
            }
            else
            {
                MusicChrome.Title(frame, copy.TopSlice(frame.Units(26f)), person.Name);
            }
            frame.Text.DrawEllipsized(copy.Inset(new Edges(0f, honor.Length > 0 ? frame.Units(42f) : frame.Units(26f),
                    0f, frame.Units(28f))),
                person.Handle + " · " + person.Role,
                new TextStyle(FontRole.Caption, MusicChrome.Mute));
            if (person.Live)
            {
                MusicChrome.LiveMark(frame, copy.BottomSlice(frame.Units(18f)).LeftSlice(frame.Units(40f)));
            }

            var stats = stack.Take(frame.Units(44f));
            DrawStat(frame, stats.LeftSlice(stats.Width / 3f), state.FollowingCount().ToString(), "Following");
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
                    frame.Text.DrawIn(stack.Take(frame.Units(20f)),
                        string.Join(" · ", state.Interests.Select(MusicState.FormatHashtag)),
                        new TextStyle(FontRole.Caption, MusicChrome.Mute));
                }

                return;
            }

            var follow = stack.Take(frame.Units(40f));
            var on = person.Id.StartsWith("live:", StringComparison.Ordinal)
                ? state.FollowsStationId(person.Id)
                : state.Following.Contains(person.Id);
            MusicChrome.Primary(frame, follow, on ? "Following" : "Follow");
            if (Tap(frame, follow))
            {
                FollowPerson(person);
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
                        person.Name + (MusicState.FormatGenreLine(person.Genre).Length > 0
                            ? " · " + MusicState.FormatGenreLine(person.Genre)
                            : string.Empty), person.StreamUrl, true));
                    state.Open(MusicPage.Player);
                }
            }

            if (person.Station.Length > 0)
            {
                MusicChrome.Kicker(frame, stack.Take(frame.Units(14f)), "STATION");
                var card = stack.Take(frame.Units(56f));
                var listed = community.Directory.FirstOrDefault(row =>
                    string.Equals("live:" + row.Id, person.Id, StringComparison.OrdinalIgnoreCase));
                DrawStationArt(frame, card.LeftSlice(frame.Units(56f)), listed.ArtPath, person.Id, person.Station);
                var info = card.Inset(new Edges(frame.Units(64f), 0f, 0f, 0f));
                frame.Text.DrawEllipsized(info.TopSlice(frame.Units(20f)),
                    person.Station + (MusicState.FormatGenreLine(person.Genre).Length > 0
                        ? " · " + MusicState.FormatGenreLine(person.Genre)
                        : string.Empty),
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
            SetSharedName(frame.TextField.Draw("music-edit-name-" + profileStamp, stack.Take(frame.Units(34f)),
                EditableName(), "Name"));
            frame.Text.DrawIn(stack.Take(frame.Units(14f)), "Honorific",
                new TextStyle(FontRole.Caption, MusicChrome.Mute));
            SetSharedHonorific(frame.TextField.Draw("music-edit-honor-" + profileStamp, stack.Take(frame.Units(34f)),
                SharedHonorific(), "Shown below your name"));
            frame.Text.DrawIn(stack.Take(frame.Units(14f)), "Handle",
                new TextStyle(FontRole.Caption, MusicChrome.Mute));
            state.Handle = frame.TextField.Draw("music-edit-handle", stack.Take(frame.Units(34f)), state.Handle, "@handle");
            frame.Text.DrawIn(stack.Take(frame.Units(14f)), "Bio",
                new TextStyle(FontRole.Caption, MusicChrome.Mute));
            state.Bio = frame.TextField.Draw("music-edit-bio", stack.Take(frame.Units(48f)), state.Bio,
                "A line about your taste");
            MusicChrome.Kicker(frame, stack.Take(frame.Units(16f)), "GENRES");
            DrawHashtagEditor(frame, stack.Take(frame.Units(118f)));
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
        var person = MusicRoster.Self(state, pearl.Current, community.Broadcasting, MarkedName());
        var stack = MusicChrome.BeginSheet(frame, area, state, frame.Units(10f));
        try
        {
            var banner = stack.Take(frame.Units(148f));
            DrawProfileBanner(frame, banner);
            var faceSide = frame.Units(104f);
            var face = Rect.FromSize(
                new Vector2(banner.Min.X + frame.Units(16f), banner.Max.Y - faceSide * 0.58f),
                new Vector2(faceSide, faceSide));
            DrawProfileFace(frame, face, SharedName());
            var editSize = frame.Units(36f);
            var edit = Rect.FromSize(
                new Vector2(banner.Max.X - frame.Units(14f) - editSize, banner.Max.Y + frame.Units(8f)),
                new Vector2(editSize, editSize));
            frame.Paint.FillCircle(edit.Center, editSize * 0.5f, MusicChrome.Card);
            frame.Paint.StrokeCircle(edit.Center, editSize * 0.5f, MusicChrome.Faint, frame.Units(1.1f));
            frame.Text.DrawIn(edit, "✎", new TextStyle(FontRole.BodyStrong, MusicChrome.Ink, TextAlign.Center));
            if (Tap(frame, edit))
            {
                state.Open(MusicPage.EditProfile);
                return;
            }

            if (Tap(frame, face))
            {
                OpenProfilePhoto(frame, MusicPhotoKind.Face);
                return;
            }

            if (Tap(frame, banner))
            {
                OpenProfilePhoto(frame, MusicPhotoKind.Banner);
                return;
            }

            var rest = stack.Remaining;
            var gutter = frame.Units(14f);
            var top = MathF.Max(rest.Min.Y, face.Max.Y + frame.Units(8f));
            stack = new Stack(
                new Rect(new Vector2(rest.Min.X + gutter, top),
                    new Vector2(rest.Max.X - gutter, rest.Max.Y)),
                StackAxis.Vertical,
                frame.Units(10f));
            var honor = SharedHonorific();
            var honorH = honor.Length > 0 ? frame.Units(16f) : 0f;
            var nameH = MathF.Max(frame.Units(28f), frame.Text.LineHeight(FontRole.Display));
            var band = stack.Take(honorH + nameH);
            DrawFlowName(frame, band.TopSlice(nameH), SharedName());
            if (honorH > 0f)
            {
                frame.Text.DrawEllipsized(band.BottomSlice(honorH), honor,
                    new TextStyle(FontRole.CaptionStrong, MusicChrome.Ink));
            }
            if (state.Interests.Count > 0)
            {
                MusicChrome.Kicker(frame, stack.Take(frame.Units(14f)), "GENRES");
                frame.Text.DrawIn(stack.Take(frame.Units(20f)),
                    string.Join(" · ", state.Interests.Select(MusicState.FormatHashtag)),
                    new TextStyle(FontRole.Caption, MusicChrome.Mute));
            }

            var bio = person.Bio.Length > 0 ? person.Bio : "Add a short bio so listeners know your sound.";
            frame.Text.DrawIn(stack.Take(frame.Units(40f)), bio,
                new TextStyle(FontRole.Caption, MusicChrome.Mute));

            DrawCreateStation(frame, ref stack);

            var likes = LikedStations();
            if (MusicChrome.Section(frame, stack.Take(frame.Units(22f)), "Top liked stations"))
            {
                OpenTab(MusicTab.Library);
                return;
            }
            if (likes.Count == 0)
            {
                DrawHint(frame, ref stack, "Heart a station and it lands here.");
            }
            else
            {
                DrawPublicRows(frame, ref stack, likes, likes.Count);
            }

            var people = Roster();
            var following = people.Where(row => !row.Mine && state.FollowsStationId(row.Id)).ToArray();
            if (MusicChrome.Section(frame, stack.Take(frame.Units(22f)), "FOLLOWING"))
            {
                OpenTab(MusicTab.Library);
                return;
            }

            if (following.Length == 0)
            {
                DrawHint(frame, ref stack, "Follow DJs from LIVE to keep them on your profile.");
            }
            else
            {
                var take = Math.Min(following.Length, 3);
                for (var index = 0; index < take; index++)
                {
                    DrawPersonRow(frame, stack.Take(frame.Units(56f)), following[index]);
                }
            }
        }
        finally
        {
            MusicChrome.EndSheet(frame, area, state, ref stack);
        }
    }

    private void DrawCreateStation(in AppletFrame frame, ref Stack stack)
    {
        MusicChrome.Kicker(frame, stack.Take(frame.Units(14f)), "MY LIVE STATION");
        var card = stack.Take(frame.Units(state.StationGenreLine.Length > 0 ? 104f : 88f));
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

        DrawStationArt(frame, inset.LeftSlice(frame.Units(64f)), state.StationArtPath, state.StationId,
            state.StationName);
        var body = inset.Inset(new Edges(frame.Units(72f), 0f, 0f, 0f));
        if (community.Broadcasting)
        {
            MusicChrome.LiveMark(frame, body.TopSlice(frame.Units(16f)).LeftSlice(frame.Units(40f)));
        }
        else
        {
            MusicChrome.OffMark(frame, body.TopSlice(frame.Units(16f)).LeftSlice(frame.Units(56f)));
        }

        frame.Text.DrawEllipsized(body.Inset(new Edges(0f, frame.Units(18f), 0f,
                frame.Units(state.StationGenreLine.Length > 0 ? 38f : 22f))),
            state.StationName,
            new TextStyle(FontRole.BodyStrong, MusicChrome.Ink));
        if (state.StationGenreLine.Length > 0)
        {
            frame.Text.DrawEllipsized(body.BottomSlice(frame.Units(36f)).TopSlice(frame.Units(16f)),
                state.StationGenreLine, new TextStyle(FontRole.Caption, MusicChrome.Purple));
        }

        frame.Text.DrawEllipsized(body.BottomSlice(frame.Units(18f)),
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
        frame.Text.DrawIn(stack.Take(frame.Units(14f)), "Your Icecast (optional)",
            new TextStyle(FontRole.Caption, MusicChrome.Mute));
        state.IcecastHost = frame.TextField.Draw("music-dash-ice-host", stack.Take(frame.Units(34f)),
            state.IcecastHost, "leave blank for Pearlgate");
        frame.Text.DrawIn(stack.Take(frame.Units(14f)), "Source password (optional)",
            new TextStyle(FontRole.Caption, MusicChrome.Mute));
        state.IcecastPassword = frame.TextField.Draw("music-dash-ice-pass", stack.Take(frame.Units(34f)),
            state.IcecastPassword, "source password");
        if (state.IcecastHost.Trim().Length == 0)
        {
            MusicChrome.FitCopy(frame, ref stack,
                pearl.Current.SignedIn
                    ? "Pearlgate will hand this phone a listen URL when Icecast is configured on the API."
                    : "Sign in on You so Pearlgate can list your station for everyone.",
                MusicChrome.Mute);
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
        EnsurePorts();
        MusicChrome.Kicker(frame, stack.Take(frame.Units(14f)), "CAPTURE");
        MusicChrome.FitCopy(frame, ref stack,
            "System sound is the playback device to send on air. Listen through is the headset you hear Music on.",
            MusicChrome.Mute);
        DrawPortMenu(frame, ref stack, "music-capture-sound", "System sound", ports.Speakers,
            ports.DefaultSpeakerId, PortFromList(ports.Speakers, PortFromTap(state.CaptureId, sound: true),
                state.CaptureName), "Windows default sound",
            ApplyCapture);
        MusicChrome.FitCopy(frame, ref stack,
            (sense.Listening ? "Open · " : "Closed · ") +
            (sense.SelectedName.Length > 0 ? sense.SelectedName : "no device"),
            sense.Listening ? MusicChrome.Live : MusicChrome.Mute, FontRole.CaptionStrong);
        DrawPortMenu(frame, ref stack, "music-listen-speaker", "Listen through", ports.Speakers,
            ports.DefaultSpeakerId, PortFromList(ports.Speakers, display.SpeakerId, string.Empty),
            "Windows default sound", ApplyListen);
        var scan = stack.Take(frame.Units(32f));
        MusicChrome.Plate(frame, scan, frame.Units(8f));
        frame.Text.DrawEllipsized(scan, "Rescan · " + ports.Status,
            new TextStyle(FontRole.CaptionStrong, MusicChrome.Purple, TextAlign.Center));
        if (Tap(frame, scan))
        {
            lastPortScan = 0;
            EnsurePorts();
        }
    }

    private void EnsurePorts()
    {
        var now = Environment.TickCount64;
        if (lastPortScan != 0 && now - lastPortScan < 2500)
        {
            return;
        }

        lastPortScan = now;
        ports.Refresh();
        sense.RescanPoints();
    }

    private void DrawPortMenu(in AppletFrame frame, ref Stack stack, string id, string title,
        IReadOnlyList<AudioPort> list, string defaultId, string currentId, string defaultLabel, Action<string> set)
    {
        frame.Text.DrawIn(stack.Take(frame.Units(16f)), title,
            new TextStyle(FontRole.Caption, MusicChrome.Mute));
        if (list.Count == 0)
        {
            frame.Text.DrawIn(stack.Take(frame.Units(28f)), "No playback devices. Tap Rescan.",
                new TextStyle(FontRole.Caption, MusicChrome.Mute));
            return;
        }

        var row = stack.Take(frame.Units(40f));
        MusicChrome.Plate(frame, row, frame.Units(10f));
        var labels = new string[list.Count + 1];
        labels[0] = defaultLabel;
        var selected = 0;
        for (var index = 0; index < list.Count; index++)
        {
            var port = list[index];
            labels[index + 1] = PortLabel(port, defaultId);
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

    private static string PortLabel(AudioPort port, string defaultId)
    {
        var name = string.IsNullOrWhiteSpace(port.Label) ? "Device" : port.Label.Replace('#', ' ');
        var vendor = name.IndexOf(" (VB-Audio", StringComparison.OrdinalIgnoreCase);
        if (vendor > 0)
        {
            name = name[..vendor];
        }

        if (port.Id == defaultId && !name.Contains("default", StringComparison.OrdinalIgnoreCase))
        {
            name += " · default";
        }

        return name;
    }

    private void ApplyCapture(string portId) => PickCapture(CaptureTapId("sound", portId), "sound");

    private void ApplyListen(string speakerId)
    {
        display.SpeakerId = speakerId;
        audio.UseSpeaker(speakerId);
        sense.RoutePhone(speakerId, display.MicrophoneId);
        RouteListenThrough();
        state.Save(paths);
    }

    private static string PortFromList(IReadOnlyList<AudioPort> list, string id, string name)
    {
        if (id.Length == 0)
        {
            return string.Empty;
        }

        for (var index = 0; index < list.Count; index++)
        {
            if (string.Equals(list[index].Id, id, StringComparison.Ordinal))
            {
                return list[index].Id;
            }
        }

        if (name.Length == 0)
        {
            return id;
        }

        for (var index = 0; index < list.Count; index++)
        {
            var label = list[index].Label;
            if (label.StartsWith(name, StringComparison.OrdinalIgnoreCase) ||
                name.StartsWith(label, StringComparison.OrdinalIgnoreCase) ||
                label.Contains("CABLE Input", StringComparison.OrdinalIgnoreCase) &&
                name.Contains("CABLE Input", StringComparison.OrdinalIgnoreCase))
            {
                return list[index].Id;
            }
        }

        return id;
    }

    private static string CaptureTapId(string app, string portId)
    {
        if (app == "mic")
        {
            if (portId.Length == 0)
            {
                return IBroadcastSense.DefaultMicId;
            }

            return WindowsAudioIds.Native(portId) ? portId : "in:" + portId;
        }

        if (portId.Length == 0)
        {
            return IBroadcastSense.DefaultMixId;
        }

        return WindowsAudioIds.Native(portId) ? portId : "out:" + portId;
    }

    private static string PortFromTap(string tapId, bool sound)
    {
        if (tapId.Length == 0 || tapId == IBroadcastSense.DefaultMixId ||
            tapId == IBroadcastSense.DefaultMicId)
        {
            return string.Empty;
        }

        if (sound)
        {
            if (tapId.StartsWith("out:", StringComparison.Ordinal))
            {
                return tapId[4..];
            }

            return WindowsAudioIds.Playback(tapId) ? tapId : string.Empty;
        }

        if (tapId.StartsWith("in:", StringComparison.Ordinal))
        {
            return tapId[3..];
        }

        return WindowsAudioIds.Capture(tapId) ? tapId : string.Empty;
    }

    private static bool Tap(in AppletFrame frame, Rect area) => frame.Input.ConsumeClick(area);

    private void OpenProfilePhoto(in AppletFrame frame, MusicPhotoKind kind)
    {
        state.Placing = kind;
        var path = kind == MusicPhotoKind.Face ? state.ProfileFacePath : state.ProfileBannerPath;
        if (path.Length == 0 || !File.Exists(path))
        {
            PickProfilePhoto();
            return;
        }

        state.Open(MusicPage.PlacePhoto);
    }

    private void PickProfilePhoto()
    {
        state.Open(MusicPage.PickPhoto);
    }

    private void PickProfilePhotoFromFiles()
    {
        files.BeginImagePick();
        imagePick = ImagePick.Profile;
    }

    private void DrawPickPhoto(in AppletFrame frame, Rect area)
    {
        var stack = MusicChrome.BeginSheet(frame, area, state, frame.Units(10f));
        try
        {
            var face = state.Placing == MusicPhotoKind.Face;
            if (MusicChrome.Back(frame, stack.Take(frame.Units(28f)), face ? "Choose photo" : "Choose banner"))
            {
                state.Back();
                return;
            }

            var filesRow = stack.Take(frame.Units(40f));
            MusicChrome.Primary(frame, filesRow, "From files");
            if (Tap(frame, filesRow))
            {
                PickProfilePhotoFromFiles();
                return;
            }

            MusicChrome.Kicker(frame, stack.Take(frame.Units(16f)), "FROM GALLERY");
            var shots = GalleryFiles.List(paths);
            if (shots.Count == 0)
            {
                frame.Text.DrawIn(stack.Take(frame.Units(48f)),
                    "No photos in Gallery yet. Take one in Camera, or pick from files.",
                    new TextStyle(FontRole.Caption, MusicChrome.Mute));
                return;
            }

            for (var index = 0; index < shots.Count; index += 3)
            {
                var row = stack.Take(frame.Units(96f));
                var gap = frame.Units(6f);
                var cell = (row.Width - gap * 2f) / 3f;
                for (var col = 0; col < 3 && index + col < shots.Count; col++)
                {
                    var shot = Rect.FromSize(
                        new Vector2(row.Min.X + (cell + gap) * col, row.Min.Y),
                        new Vector2(cell, row.Height));
                    var path = shots[index + col].Path;
                    DrawGalleryStill(frame, shot, path);
                    if (!Tap(frame, shot))
                    {
                        continue;
                    }

                    if (ImportProfilePhoto(frame, path))
                    {
                        state.Open(MusicPage.PlacePhoto);
                    }

                    return;
                }
            }
        }
        finally
        {
            MusicChrome.EndSheet(frame, area, state, ref stack);
        }
    }

    private static void DrawGalleryStill(in AppletFrame frame, Rect area, string path)
    {
        MusicChrome.Plate(frame, area, frame.Units(8f));
        var texture = frame.Textures.FromFile(path);
        if (texture is not { IsReady: true })
        {
            frame.Paint.Fill(area, MusicChrome.CardHi, frame.Units(8f));
            return;
        }

        var uv = CoverFit.Uv(texture.Size, area.Size);
        frame.Paint.Image(texture, area, uv.Min, uv.Max, Vector4.One);
    }

    private bool ImportProfilePhoto(in AppletFrame frame, string sourcePath)
    {
        try
        {
            var source = sourcePath.Trim().Trim('"');
            if (source.Length == 0 || !File.Exists(source))
            {
                return false;
            }

            var ext = Path.GetExtension(source);
            if (!PlateFiles.IsImage(ext))
            {
                ext = ".png";
            }

            var stem = state.Placing == MusicPhotoKind.Face ? "music-profile-face" : "music-profile-banner";
            var dest = paths.State(stem + "-" + Guid.NewGuid().ToString("N") + ext.ToLowerInvariant());
            Directory.CreateDirectory(paths.StateDirectory);
            File.Copy(source, dest, false);
            var previous = state.Placing == MusicPhotoKind.Face ? state.ProfileFacePath : state.ProfileBannerPath;
            if (state.Placing == MusicPhotoKind.Face)
            {
                state.ProfileFacePath = dest;
                state.FaceZoom = 1f;
                state.FaceFocusX = 0.5f;
                state.FaceFocusY = 0.5f;
            }
            else
            {
                state.ProfileBannerPath = dest;
                state.BannerZoom = 1f;
                state.BannerFocusX = 0.5f;
                state.BannerFocusY = 0.5f;
            }

            frame.Textures.ForgetFile(previous);
            frame.Textures.ForgetFile(dest);
            if (previous.Length > 0 &&
                !string.Equals(previous, dest, StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    File.Delete(previous);
                }
                catch (IOException)
                {
                }
                catch (UnauthorizedAccessException)
                {
                }
            }

            state.Save(paths);
            return true;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    private void DrawProfileBanner(in AppletFrame frame, Rect area)
    {
        if (DrawProfilePhoto(frame, area, state.ProfileBannerPath, state.BannerZoom, state.BannerFocusX,
                state.BannerFocusY, circle: false))
        {
            return;
        }

        frame.Paint.Fill(area, MusicChrome.Card);
        frame.Text.DrawIn(area, "Tap to add a banner",
            new TextStyle(FontRole.Caption, MusicChrome.Mute, TextAlign.Center));
    }

    private void DrawProfileFace(in AppletFrame frame, Rect area, string name)
    {
        var box = CoverFit.InscribedSquare(area);
        var radius = MathF.Min(box.Width, box.Height) * 0.5f;
        if (DrawProfilePhoto(frame, box, state.ProfileFacePath, state.FaceZoom, state.FaceFocusX, state.FaceFocusY,
                circle: true))
        {
            frame.Paint.StrokeCircle(box.Center, radius, MusicChrome.Ink, MathF.Max(0.8f, frame.Units(1.05f)));
            return;
        }

        frame.Paint.FillCircle(box.Center, radius, MusicChrome.Purple);
        var letter = name.Length > 0 ? name[..1].ToUpperInvariant() : "♪";
        var role = box.Width < frame.Units(36f) ? FontRole.CaptionStrong : FontRole.Title;
        frame.Text.DrawIn(box, letter, new TextStyle(role, MusicChrome.GroundHi, TextAlign.Center));
        frame.Paint.StrokeCircle(box.Center, radius, MusicChrome.Ink, MathF.Max(0.8f, frame.Units(1.05f)));
    }

    private static bool DrawProfilePhoto(in AppletFrame frame, Rect area, string path, float zoom, float focusX,
        float focusY, bool circle)
    {
        if (path.Length == 0)
        {
            return false;
        }

        var texture = frame.Textures.FromFile(path);
        if (texture is not { IsReady: true })
        {
            return false;
        }

        var focus = new Vector2(focusX, focusY);
        if (zoom < 1f)
        {
            if (circle)
            {
                frame.Paint.FillCircle(area.Center, MathF.Min(area.Width, area.Height) * 0.5f, MusicChrome.Ground);
            }
            else
            {
                frame.Paint.Fill(area, MusicChrome.Ground);
            }

            var cover = CoverFit.Uv(texture.Size, area.Size);
            var fit = CoverFit.Contained(texture.Size, area);
            var t = (zoom - 0.28f) / (1f - 0.28f);
            t = Math.Clamp(t, 0f, 1f);
            var dest = new Rect(Vector2.Lerp(fit.Min, area.Min, t), Vector2.Lerp(fit.Max, area.Max, t));
            var uvMin = Vector2.Lerp(Vector2.Zero, cover.Min, t);
            var uvMax = Vector2.Lerp(Vector2.One, cover.Max, t);
            if (circle)
            {
                frame.Paint.ImageRounded(texture, dest, uvMin, uvMax, Vector4.One,
                    MathF.Min(dest.Width, dest.Height) * 0.5f);
            }
            else
            {
                frame.Paint.Image(texture, dest, uvMin, uvMax, Vector4.One);
            }

            return true;
        }

        var crop = CoverFit.Framed(texture.Size, area.Size, zoom, focus);
        if (circle)
        {
            frame.Paint.ImageRounded(texture, area, crop.Min, crop.Max, Vector4.One,
                MathF.Min(area.Width, area.Height) * 0.5f);
            return true;
        }

        frame.Paint.Image(texture, area, crop.Min, crop.Max, Vector4.One);
        return true;
    }

    private void DrawPlacePhoto(in AppletFrame frame, Rect area)
    {
        var face = state.Placing == MusicPhotoKind.Face;
        var path = face ? state.ProfileFacePath : state.ProfileBannerPath;
        frame.Paint.Fill(area, MusicChrome.Ground);
        var inner = area.Inset(frame.Units(16f));
        frame.Text.DrawIn(inner.TopSlice(frame.Units(24f)), face ? "Place photo" : "Place banner",
            new TextStyle(FontRole.Title, MusicChrome.Ink));
        frame.Text.DrawWrapped(inner.Inset(new Edges(0f, frame.Units(28f), 0f, 0f)).TopSlice(frame.Units(36f)),
            face
                ? "Drag to move. Scroll to zoom in or out so the whole photo or just part of it sits in the circle."
                : "Drag to move. Scroll to zoom in or out so the whole photo or just part of it sits in the banner.",
            new TextStyle(FontRole.Caption, MusicChrome.Mute));

        var actions = inner.BottomSlice(frame.Units(44f));
        var preview = new Rect(inner.Min + new Vector2(0f, frame.Units(72f)),
            new Vector2(inner.Max.X, actions.Min.Y - frame.Units(12f)));
        if (face)
        {
            var side = MathF.Min(preview.Width, preview.Height);
            preview = Rect.FromSize(preview.Center - new Vector2(side * 0.5f, side * 0.5f), new Vector2(side, side));
            DrawProfileFace(frame, preview, SharedName());
        }
        else
        {
            var height = MathF.Min(preview.Height, preview.Width * 0.42f);
            preview = Rect.FromSize(new Vector2(preview.Min.X, preview.Center.Y - height * 0.5f),
                new Vector2(preview.Width, height));
            DrawProfileBanner(frame, preview);
            frame.Paint.Stroke(preview, MusicChrome.Purple, MathF.Max(1.6f, frame.Units(2f)), 0f);
        }

        TickPhotoPlace(frame, preview, path);

        var other = actions.LeftSlice(actions.Width * 0.48f);
        var done = actions.RightSlice(actions.Width * 0.48f);
        MusicChrome.Plate(frame, other, frame.Units(12f));
        frame.Text.DrawIn(other, "Choose another",
            new TextStyle(FontRole.CaptionStrong, MusicChrome.Purple, TextAlign.Center));
        MusicChrome.Primary(frame, done, "Done");
        if (Tap(frame, other))
        {
            PickProfilePhoto();
            return;
        }

        if (Tap(frame, done))
        {
            photoDrag = false;
            state.Save(paths);
            OpenTab(MusicTab.Profile);
        }
    }

    private void TickPhotoPlace(in AppletFrame frame, Rect preview, string path)
    {
        if (path.Length == 0)
        {
            return;
        }

        var texture = frame.Textures.FromFile(path);
        if (texture is not { IsReady: true })
        {
            return;
        }

        var zoom = state.Placing == MusicPhotoKind.Face ? state.FaceZoom : state.BannerZoom;
        var focus = state.Placing == MusicPhotoKind.Face
            ? new Vector2(state.FaceFocusX, state.FaceFocusY)
            : new Vector2(state.BannerFocusX, state.BannerFocusY);
        if (frame.Input.WasPressed(preview))
        {
            photoDrag = true;
        }

        if (photoDrag && frame.Input.IsHeld())
        {
            var cover = CoverFit.Framed(texture.Size, preview.Size, zoom, focus);
            var visible = cover.Max - cover.Min;
            focus -= new Vector2(
                preview.Width > 1f ? frame.Input.PointerDelta.X / preview.Width * visible.X : 0f,
                preview.Height > 1f ? frame.Input.PointerDelta.Y / preview.Height * visible.Y : 0f);
            state.AdjustPlacing(zoom, focus.X, focus.Y);
        }

        if (!frame.Input.IsHeld())
        {
            photoDrag = false;
        }

        if (frame.Input.IsHovering(preview) && MathF.Abs(frame.Input.ScrollDelta) > 0.01f)
        {
            state.AdjustPlacing(zoom * (1f + frame.Input.ScrollDelta * 0.14f), focus.X, focus.Y);
        }
    }
}
