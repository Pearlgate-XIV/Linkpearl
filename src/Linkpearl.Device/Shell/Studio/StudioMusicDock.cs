using Linkpearl.Applets;
using Linkpearl.Audio;
using Linkpearl.Geometry;
using Linkpearl.Media;
using Linkpearl.Net;
using Linkpearl.Painting;

namespace Linkpearl.Device.Shell.Studio;

internal sealed class StudioMusicDock
{
    private readonly IHandsetAudio audio;
    private readonly IPublicRadio radio;
    private readonly IPearlHub pearl;
    private readonly IStationMarks marks;
    private readonly Action<Rect, string> openStations;
    private readonly Action<string, Rect> openApplet;
    private bool volumeOpen;
    private bool volumeDrag;
    private int genreIndex;
    private string waveId = string.Empty;
    private float playedSeconds;
    private float scroll;
    private bool markBusy;

    public StudioMusicDock(IHandsetAudio audio, IPublicRadio radio, IPearlHub pearl, IStationMarks marks,
        Action<Rect, string> openStations, Action<string, Rect> openApplet)
    {
        this.audio = audio;
        this.radio = radio;
        this.pearl = pearl;
        this.marks = marks;
        this.openStations = openStations;
        this.openApplet = openApplet;
    }

    public bool BlocksPager => volumeOpen || volumeDrag || markBusy;

    public static float Height(in AppletFrame frame) => frame.Units(90f);

    public void Draw(in AppletFrame frame, Rect row)
    {
        if (row.Width < 8f || row.Height < 8f)
        {
            markBusy = false;
            return;
        }

        markBusy = false;
        if (!marks.HasAccount)
        {
            DrawLocked(frame, row);
            return;
        }

        var genres = radio.Genres;
        var genre = ResolveGenre(genres);
        radio.Ensure(genre);
        var stations = radio.Stations(genre);
        var now = audio.Now;
        var title = now.Title ?? string.Empty;
        var detail = now.Detail ?? string.Empty;
        var id = now.Id ?? string.Empty;
        var artPath = now.ArtPath ?? string.Empty;
        var accent = new Vector4(0.18f, 0.86f, 1f, 1f);
        var ink = frame.Theme.Palette.Ink;
        var muted = frame.Theme.Palette.InkMuted;
        var playing = audio.Phase is HandsetAudioPhase.Playing or HandsetAudioPhase.Buffering;
        var tuned = id.Length > 0;
        var radius = frame.Units(14f);
        frame.Paint.Fill(row, frame.Theme.Palette.SurfaceOverlay with { W = 0.82f }, radius);
        frame.Paint.Stroke(row, accent with { W = 0.22f }, frame.Theme.Metrics.Hairline, radius);
        var art = row.LeftSlice(row.Height);
        DrawArt(frame, art, artPath, accent);
        var pane = row.Inset(new Edges(art.Width + frame.Units(8f), frame.Units(5f), frame.Units(8f),
            frame.Units(5f)));
        if (pane.Width < 8f || pane.Height < 8f)
        {
            return;
        }

        var play = pane.LeftSlice(frame.Units(26f)).TopSlice(frame.Units(26f));
        var speaker = pane.RightSlice(frame.Units(26f)).TopSlice(frame.Units(26f));
        var titles = new Rect(new Vector2(play.Max.X + frame.Units(8f), pane.Min.Y),
            new Vector2(speaker.Min.X - frame.Units(6f), play.Max.Y));
        var transport = pane.BottomSlice(frame.Units(22f));
        var small = frame.Units(22f);
        var gap = frame.Units(5f);
        var prev = Rect.FromSize(transport.Min, new Vector2(small, transport.Height));
        var next = Rect.FromSize(new Vector2(prev.Max.X + gap, transport.Min.Y), new Vector2(small, transport.Height));
        var shuffle = Rect.FromSize(new Vector2(next.Max.X + gap, transport.Min.Y),
            new Vector2(small, transport.Height));
        var liveFollow = marks.CanFollow(now);
        var follow = liveFollow
            ? Rect.FromSize(new Vector2(pane.Max.X - small, transport.Min.Y), new Vector2(small, transport.Height))
            : Rect.Empty;
        var save = Rect.FromSize(
            new Vector2((liveFollow ? follow.Min.X : pane.Max.X) - (liveFollow ? gap + small : small),
                transport.Min.Y), new Vector2(small, transport.Height));
        var like = Rect.FromSize(new Vector2(save.Min.X - gap - small, transport.Min.Y),
            new Vector2(small, transport.Height));
        var markBand = new Rect(like.Min, liveFollow ? follow.Max : save.Max);
        var markLane = new Rect(new Vector2(like.Min.X - gap, speaker.Max.Y + frame.Units(2f)),
            new Vector2(pane.Max.X, pane.Max.Y));
        if (markLane.Width < markBand.Width || markLane.Height < markBand.Height)
        {
            markLane = Pad(frame, markBand);
        }

        var wave = new Rect(new Vector2(art.Max.X, titles.Max.Y + frame.Units(4f)),
            new Vector2(row.Max.X, transport.Min.Y - frame.Units(3f)));
        var sliderH = frame.Units(28f);
        var slider = volumeOpen
            ? new Rect(new Vector2(pane.Min.X, pane.Center.Y - sliderH * 0.5f),
                new Vector2(pane.Max.X, pane.Center.Y + sliderH * 0.5f))
            : default;
        if (!volumeOpen)
        {
            var name = tuned && title.Length > 0
                ? title
                : stations.Count > 0
                    ? "Ready to tune"
                    : radio.Busy
                        ? "Finding stations"
                        : "Open Music";
            var blurb = tuned && detail.Length > 0
                ? detail
                : audio.Phase == HandsetAudioPhase.Failed
                    ? audio.Notice ?? string.Empty
                    : now.Live
                        ? "Live"
                        : "Radio · " + genre;
            frame.Text.DrawEllipsized(titles.TopSlice(frame.Units(12f)), blurb,
                new TextStyle(FontRole.Caption, muted));
            frame.Text.DrawEllipsized(titles.BottomSlice(frame.Units(15f)), name,
                new TextStyle(FontRole.CaptionStrong, ink));
            RememberWave(id, playing, frame.DeltaSeconds);
            DrawWave(frame, wave, id, Glow(), scroll, accent);
            DrawTime(frame, wave, now.Live ? "LIVE" : Clock(playedSeconds), ink);
            DrawKey(frame, play, playing ? StudioMark.Pause : StudioMark.Play, accent, true);
            DrawGhost(frame, prev, StudioMark.Prev, muted);
            DrawGhost(frame, next, StudioMark.Next, muted);
            DrawGhost(frame, shuffle, StudioMark.Shuffle, muted);
            var liked = tuned && marks.Liked(id);
            var saved = tuned && marks.Saved(id);
            var followed = tuned && marks.Followed(id);
            var likes = tuned ? marks.LikeCount(now) : 0;
            DrawMark(frame, like, "♥", null, liked ? new Vector4(1f, 0.36f, 0.56f, 1f) : muted, liked);
            if (likes > 0)
            {
                frame.Text.DrawIn(like.Inset(new Edges(0f, like.Height * 0.52f, 0f, 0f)), likes.ToString(),
                    new TextStyle(FontRole.Caption, liked ? new Vector4(1f, 0.36f, 0.56f, 1f) : muted,
                        TextAlign.Center, scale: 0.72f));
            }
            DrawMark(frame, save, "★", "music-save.png", saved ? new Vector4(1f, 0.84f, 0.18f, 1f) : muted, saved);
            if (liveFollow)
            {
                DrawMark(frame, follow, "+", "music-follow.png",
                    followed ? new Vector4(0.28f, 0.82f, 0.42f, 1f) : muted, followed);
            }
        }

        DrawGhost(frame, speaker, StudioMark.Speaker, volumeOpen ? accent : muted);

        var onMarks = !volumeOpen && markLane.Width > 4f &&
                      (frame.Input.IsHovering(markLane) || markLane.Contains(frame.Input.Pointer));
        markBusy = onMarks || volumeDrag;
        if (onMarks)
        {
            if (id.Length > 0 && TapMark(frame, like, markLane))
            {
                marks.ToggleLike(now);
                frame.Input.Claim(markLane);
                return;
            }

            if (id.Length > 0 && TapMark(frame, save, markLane))
            {
                marks.ToggleSave(now);
                frame.Input.Claim(markLane);
                return;
            }

            if (liveFollow && id.Length > 0 && TapMark(frame, follow, markLane))
            {
                marks.ToggleFollow(now);
                frame.Input.Claim(markLane);
                return;
            }

            frame.Input.Claim(markLane);
            if (frame.Input.WasClicked(markLane))
            {
                return;
            }
        }

        if (Hit(frame, speaker))
        {
            volumeOpen = !volumeOpen;
            volumeDrag = false;
            return;
        }

        if (volumeOpen && slider.Width > frame.Units(20f))
        {
            ShadeSlider.Draw(frame.Paint, frame.Input, frame.Theme, slider, Math.Clamp(audio.Volume, 0f, 1f),
                ref volumeDrag, value => audio.Volume = value, sun: false);
            if (volumeDrag)
            {
                return;
            }
        }

        if (Hit(frame, shuffle))
        {
            StepGenre(genres);
            return;
        }

        if (Hit(frame, prev))
        {
            Step(stations, -1);
            return;
        }

        if (Hit(frame, next))
        {
            Step(stations, 1);
            return;
        }

        if (Hit(frame, play))
        {
            Toggle(stations);
            return;
        }

        if (onMarks)
        {
            return;
        }

        if (Hit(frame, art) || Hit(frame, titles))
        {
            openStations(row, now.Live ? "player" : RadioPlace(genre, id));
            return;
        }

        if (wave.Width > 4f && frame.Input.ConsumeClick(wave))
        {
            return;
        }

        if (volumeOpen && frame.Input.ConsumeClick(row))
        {
            volumeOpen = false;
            volumeDrag = false;
        }
    }

    private string RadioPlace(string genre, string stationId)
    {
        if (stationId.Length == 0)
        {
            return genre.Length == 0 ? "radio" : "search:" + genre;
        }

        var found = FindPublic(stationId);
        var named = found is { Genre.Length: > 0 } ? found.Value.Genre : genre;
        return "search:" + named + "|" + stationId;
    }

    private PublicStation? FindPublic(string stationId)
    {
        var genres = radio.Genres;
        for (var genre = 0; genre < genres.Count; genre++)
        {
            var stations = radio.Stations(genres[genre]);
            var index = IndexOf(stations, stationId);
            if (index >= 0)
            {
                return stations[index];
            }
        }

        return null;
    }

    private void Toggle(IReadOnlyList<PublicStation> stations)
    {
        var id = audio.Now.Id ?? string.Empty;
        if (id.Length > 0 &&
            audio.Phase is HandsetAudioPhase.Playing or HandsetAudioPhase.Buffering or HandsetAudioPhase.Paused)
        {
            audio.Toggle();
            return;
        }

        if (stations.Count > 0)
        {
            Tune(stations[0]);
        }
    }

    private void Step(IReadOnlyList<PublicStation> stations, int delta)
    {
        if (stations.Count == 0)
        {
            return;
        }

        var index = IndexOf(stations, audio.Now.Id ?? string.Empty);
        var next = index < 0 ? 0 : (index + delta + stations.Count) % stations.Count;
        Tune(stations[next]);
    }

    private void StepGenre(IReadOnlyList<string> genres)
    {
        if (genres.Count == 0)
        {
            return;
        }

        genreIndex = (IndexOfGenre(genres, ResolveGenre(genres)) + 1) % genres.Count;
        var next = genres[genreIndex];
        radio.Ensure(next);
        var stations = radio.Stations(next);
        if (stations.Count > 0)
        {
            Tune(stations[0]);
        }
    }

    private string ResolveGenre(IReadOnlyList<string> genres)
    {
        if (genres.Count == 0)
        {
            return "Lo-Fi";
        }

        var id = audio.Now.Id ?? string.Empty;
        if (id.Length > 0)
        {
            for (var index = 0; index < genres.Count; index++)
            {
                if (IndexOf(radio.Stations(genres[index]), id) >= 0)
                {
                    genreIndex = index;
                    return genres[index];
                }
            }
        }

        var detail = audio.Now.Detail ?? string.Empty;
        var cut = detail.IndexOf(" · ", StringComparison.Ordinal);
        var tag = cut > 0 ? detail[..cut] : detail;
        var named = IndexOfGenre(genres, tag);
        if (named >= 0)
        {
            genreIndex = named;
            return genres[named];
        }

        genreIndex = Math.Clamp(genreIndex, 0, genres.Count - 1);
        return genres[genreIndex];
    }

    private static int IndexOfGenre(IReadOnlyList<string> genres, string name)
    {
        if (name.Length == 0)
        {
            return -1;
        }

        for (var index = 0; index < genres.Count; index++)
        {
            if (string.Equals(genres[index], name, StringComparison.OrdinalIgnoreCase))
            {
                return index;
            }
        }

        return -1;
    }

    private void RememberWave(string id, bool playing, float delta)
    {
        if (!string.Equals(waveId, id, StringComparison.Ordinal))
        {
            waveId = id;
            playedSeconds = 0f;
            scroll = 0f;
        }

        if (id.Length == 0)
        {
            playedSeconds = 0f;
            scroll = 0f;
            return;
        }

        if (playing)
        {
            var step = Math.Clamp(delta, 0f, 0.05f);
            playedSeconds += step;
            scroll += step * 9f;
        }
    }

    private float Glow() => Math.Clamp(playedSeconds / 2.4f, 0f, 0.5f);

    private static void DrawWave(in AppletFrame frame, Rect area, string id, float glow, float travel, Vector4 gold)
    {
        if (area.Width < 8f || area.Height < 6f)
        {
            return;
        }

        var count = Math.Clamp((int)(area.Width / frame.Units(3.2f)), 18, 120);
        var pitch = area.Width / count;
        var barW = MathF.Max(1.2f, pitch * 0.52f);
        var mid = area.Center.Y;
        var seed = unchecked((uint)StringComparer.Ordinal.GetHashCode(id.Length > 0 ? id : "idle"));
        glow = Math.Clamp(glow, 0f, 0.5f);
        var first = (int)MathF.Floor(travel) - 1;
        var last = first + count + 3;
        frame.Paint.PushClip(area);
        try
        {
            for (var sample = first; sample <= last; sample++)
            {
                var n = seed * 1664525u + unchecked((uint)sample) * 1013904223u;
                n ^= n >> 13;
                n *= 2246822519u;
                var shape = 0.22f + 0.78f * ((n & 255u) / 255f);
                var half = area.Height * 0.5f * shape;
                var x = area.Min.X + (sample - travel) * pitch + (pitch - barW) * 0.5f;
                if (x + barW < area.Min.X || x > area.Max.X)
                {
                    continue;
                }

                var along = (x + barW * 0.5f - area.Min.X) / area.Width;
                var mix = 1f - Smooth01((along - (glow - 0.04f)) / 0.05f);
                var color = gold with { W = 0.22f + 0.70f * mix };
                frame.Paint.Fill(new Rect(new Vector2(x, mid - half), new Vector2(x + barW, mid + half)), color,
                    barW * 0.45f);
            }
        }
        finally
        {
            frame.Paint.PopClip();
        }
    }

    private static float Smooth01(float t)
    {
        t = Math.Clamp(t, 0f, 1f);
        return t * t * (3f - 2f * t);
    }

    private void Tune(PublicStation station)
    {
        var packed = station.StreamUrl;
        if (station.AlternateUrl.Length > 0 &&
            !string.Equals(station.AlternateUrl, station.StreamUrl, StringComparison.OrdinalIgnoreCase))
        {
            packed += "\n" + station.AlternateUrl;
        }

        audio.Play(new HandsetTune(station.Id, station.Title, station.Genre + " · " + station.Place, packed, false,
            station.ArtUrl ?? string.Empty));
        _ = Task.Run(() =>
        {
            var urls = radio.PlayUrls(station);
            if (urls.Count == 0 || !string.Equals(audio.Now.Id, station.Id, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var next = string.Join('\n', urls);
            audio.Play(new HandsetTune(station.Id, station.Title, station.Genre + " · " + station.Place, next, false,
                station.ArtUrl ?? string.Empty));
        });
    }

    private void DrawLocked(in AppletFrame frame, Rect row)
    {
        var accent = new Vector4(0.18f, 0.86f, 1f, 1f);
        var radius = frame.Units(14f);
        frame.Paint.Fill(row, frame.Theme.Palette.SurfaceOverlay with { W = 0.82f }, radius);
        frame.Paint.Stroke(row, accent with { W = 0.22f }, frame.Theme.Metrics.Hairline, radius);
        var art = row.LeftSlice(row.Height);
        frame.Paint.Fill(art, frame.Theme.Palette.SurfaceSunken with { W = 1f });
        AppMarks.DrawFace(frame, art.Inset(frame.Units(8f)), "music", false);
        var pane = row.Inset(new Edges(art.Width + frame.Units(8f), frame.Units(10f), frame.Units(10f),
            frame.Units(10f)));
        frame.Text.DrawEllipsized(pane.TopSlice(frame.Units(14f)), "Create an account",
            new TextStyle(FontRole.CaptionStrong, frame.Theme.Palette.Ink));
        frame.Text.DrawWrapped(pane.Inset(new Edges(0f, frame.Units(16f), 0f, 0f)),
            "You must first create an account before using.",
            new TextStyle(FontRole.Caption, frame.Theme.Palette.InkMuted));
        if (frame.Input.IsHovering(row) || row.Contains(frame.Input.Pointer))
        {
            markBusy = true;
        }

        if (frame.Input.ConsumeClick(row))
        {
            openApplet("music", row);
        }
    }

    private void DrawArt(in AppletFrame frame, Rect area, string path, Vector4 gold)
    {
        if (area.Width < 4f || area.Height < 4f)
        {
            return;
        }

        var plate = frame.Theme.Palette.SurfaceSunken with { W = 1f };
        frame.Paint.Fill(area, plate);
        ITextureHandle? texture = null;
        try
        {
            if (path.Length > 0 && File.Exists(path))
            {
                texture = frame.Textures.FromFile(path);
            }
            else if (path.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            {
                pearl.PrefetchMedia(path);
                var local = pearl.LocalMedia(path);
                if (local is { Length: > 0 })
                {
                    texture = frame.Textures.FromFile(local);
                }
            }
        }
        catch
        {
            texture = null;
        }

        if (texture is { IsReady: true })
        {
            var crop = CoverFit.Uv(texture.Size, area.Size);
            frame.Paint.Image(texture, area, crop.Min, crop.Max, Vector4.One);
            return;
        }

        StudioMarks.Draw(frame.Paint, area.Inset(area.Width * 0.22f), StudioMark.Note, gold);
    }

    private static void DrawTime(in AppletFrame frame, Rect wave, string label, Vector4 ink)
    {
        if (wave.Width < frame.Units(36f) || wave.Height < frame.Units(10f))
        {
            return;
        }

        var tag = wave.RightSlice(frame.Units(34f)).Inset(new Edges(0f, wave.Height * 0.18f));
        frame.Paint.Fill(tag, frame.Theme.Palette.SurfaceRaised with { W = 0.92f }, frame.Units(3f));
        frame.Text.DrawIn(tag, label, new TextStyle(FontRole.Caption, ink, TextAlign.Center, scale: 0.82f));
    }

    private static string Clock(float seconds)
    {
        var total = Math.Max(0, (int)seconds);
        return (total / 60).ToString(System.Globalization.CultureInfo.InvariantCulture) + ":" +
               (total % 60).ToString("00", System.Globalization.CultureInfo.InvariantCulture);
    }

    private static void DrawGhost(in AppletFrame frame, Rect cell, StudioMark mark, Vector4 ink)
    {
        if (cell.Width < 4f || cell.Height < 4f)
        {
            return;
        }

        var hover = frame.Input.IsHovering(cell);
        var side = MathF.Min(cell.Width, cell.Height);
        if (hover)
        {
            frame.Paint.FillCircle(cell.Center, side * 0.54f, new Vector4(1f, 1f, 1f, 0.18f));
            frame.Paint.StrokeCircle(cell.Center, side * 0.54f, new Vector4(1f, 1f, 1f, 0.40f),
                frame.Theme.Metrics.Hairline);
        }

        var size = side * (hover ? 0.56f : 0.50f);
        StudioMarks.Draw(frame.Paint, cell.Center, size, mark, hover ? frame.Theme.Palette.Ink : ink);
    }

    private static void DrawKey(in AppletFrame frame, Rect cell, StudioMark mark, Vector4 gold, bool primary)
    {
        if (cell.Width < 4f || cell.Height < 4f)
        {
            return;
        }

        var hover = frame.Input.IsHovering(cell);
        var side = MathF.Min(cell.Width, cell.Height);
        var fill = primary
            ? gold with { W = hover ? 1f : 0.96f }
            : gold with { W = hover ? 0.62f : 0.42f };
        var ink = primary ? frame.Theme.Palette.SurfaceSunken : gold with { W = 0.92f };
        var radius = side * (primary ? (hover ? 0.52f : 0.46f) : (hover ? 0.46f : 0.40f));
        if (hover)
        {
            frame.Paint.FillCircle(cell.Center, radius + frame.Units(2f), new Vector4(1f, 1f, 1f, 0.16f));
        }

        frame.Paint.FillCircle(cell.Center, radius, fill);
        frame.Paint.StrokeCircle(cell.Center, radius, gold with { W = hover ? 0.88f : 0.55f },
            frame.Theme.Metrics.Hairline);
        StudioMarks.Draw(frame.Paint,
            Rect.FromSize(cell.Center - new Vector2(side * 0.36f), new Vector2(side * 0.72f)), mark, ink);
    }

    private static bool TapMark(in AppletFrame frame, Rect cell, Rect lane)
    {
        if (cell.Width < 4f || cell.Height < 4f || lane.Height < 4f)
        {
            return false;
        }

        var column = new Rect(new Vector2(cell.Min.X - frame.Units(3f), lane.Min.Y),
            new Vector2(cell.Max.X + frame.Units(3f), lane.Max.Y));
        return frame.Input.WasClicked(column);
    }

    private static bool Hit(in AppletFrame frame, Rect cell)
    {
        if (cell.Width < 4f || cell.Height < 4f)
        {
            return false;
        }

        return frame.Input.ConsumeClick(cell);
    }

    private static Rect Pad(in AppletFrame frame, Rect cell)
    {
        if (cell.Width < 4f || cell.Height < 4f)
        {
            return cell;
        }

        var grow = frame.Units(4f);
        return new Rect(cell.Min - new Vector2(grow), cell.Max + new Vector2(grow));
    }

    private static void DrawMark(in AppletFrame frame, Rect cell, string fallback, string? glyph, Vector4 ink,
        bool on)
    {
        if (cell.Width < 4f || cell.Height < 4f)
        {
            return;
        }

        var hover = frame.Input.IsHovering(Pad(frame, cell));
        var side = MathF.Min(cell.Width, cell.Height);
        if (hover || on)
        {
            frame.Paint.FillCircle(cell.Center, side * 0.54f, new Vector4(1f, 1f, 1f, on ? 0.16f : 0.10f));
        }

        if (glyph is { Length: > 0 } &&
            TryGlyph(frame, cell.Inset(cell.Width * 0.12f), glyph, ink))
        {
            return;
        }

        frame.Text.DrawIn(cell, fallback, new TextStyle(FontRole.BodyStrong, ink, TextAlign.Center));
    }

    private static bool TryGlyph(in AppletFrame frame, Rect area, string file, Vector4 ink)
    {
        var texture = frame.Textures.FromFile(AppIconCatalog.Glyph(frame.Paths, file));
        if (texture is not { IsReady: true } || texture.Handle == 0)
        {
            return false;
        }

        frame.Paint.Image(texture, area, ink);
        return true;
    }

    private static int IndexOf(IReadOnlyList<PublicStation> stations, string id)
    {
        if (id.Length == 0)
        {
            return -1;
        }

        for (var index = 0; index < stations.Count; index++)
        {
            if (string.Equals(stations[index].Id, id, StringComparison.OrdinalIgnoreCase))
            {
                return index;
            }
        }

        return -1;
    }
}
