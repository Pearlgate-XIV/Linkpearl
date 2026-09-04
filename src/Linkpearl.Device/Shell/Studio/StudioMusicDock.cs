using Linkpearl.Applets;
using Linkpearl.Audio;
using Linkpearl.Geometry;
using Linkpearl.Net;
using Linkpearl.Painting;

namespace Linkpearl.Device.Shell.Studio;

internal sealed class StudioMusicDock
{
    private readonly IHandsetAudio audio;
    private readonly IPublicRadio radio;
    private readonly IPearlHub pearl;
    private readonly Action<Rect> openStations;
    private bool volumeOpen;
    private bool volumeDrag;
    private int genreIndex;
    private string waveId = string.Empty;
    private float playedSeconds;
    private float scroll;

    public StudioMusicDock(IHandsetAudio audio, IPublicRadio radio, IPearlHub pearl, Action<Rect> openStations)
    {
        this.audio = audio;
        this.radio = radio;
        this.pearl = pearl;
        this.openStations = openStations;
    }

    public bool BlocksPager => volumeOpen || volumeDrag;

    public static float Height(in AppletFrame frame) => frame.Units(96f);

    public void Draw(in AppletFrame frame, Rect row)
    {
        if (row.Width < 8f || row.Height < 8f)
        {
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
        var gold = frame.Theme.Palette.WarmAccent;
        var ink = frame.Theme.Palette.Ink;
        var muted = frame.Theme.Palette.InkMuted;
        var playing = audio.Phase is HandsetAudioPhase.Playing or HandsetAudioPhase.Buffering;
        var tuned = id.Length > 0;
        StudioChrome.DrawPanel(frame, row);
        var inset = row.Inset(new Edges(frame.Units(10f), frame.Units(8f), frame.Units(10f), frame.Units(8f)));
        var speakerW = frame.Units(28f);
        var volumeGap = frame.Units(16f);
        var head = inset.TopSlice(frame.Units(13f));
        StudioChrome.DrawHeader(frame, new Rect(head.Min, new Vector2(head.Max.X - speakerW - volumeGap, head.Max.Y)),
            tuned ? now.Live ? "NOW · LIVE" : "NOW PLAYING" : "MUSIC", false, out _);
        var speaker = Rect.FromSize(new Vector2(inset.Max.X - speakerW, inset.Min.Y - frame.Units(1f)),
            new Vector2(speakerW, speakerW));
        DrawKey(frame, speaker, StudioMark.Speaker, gold, volumeOpen);

        var body = inset.Inset(new Edges(0f, frame.Units(16f), speakerW + volumeGap, 0f));
        if (body.Width < 8f || body.Height < 8f)
        {
            return;
        }

        var artSide = MathF.Min(body.Height, frame.Units(44f));
        var art = body.LeftSlice(artSide);
        DrawArt(frame, art, artPath, gold);
        var copy = new Rect(new Vector2(art.Max.X + frame.Units(8f), body.Min.Y), body.Max);
        var gap = frame.Units(4f);
        var playW = frame.Units(28f);
        var small = frame.Units(22f);
        var keyW = small * 3f + playW + gap * 3f;
        var keyH = frame.Units(26f);
        var waveH = frame.Units(18f);
        var floor = copy.BottomSlice(MathF.Max(keyH, waveH));
        var keys = floor.RightSlice(MathF.Min(keyW, MathF.Max(8f, copy.Width * 0.48f)));
        var wave = new Rect(
            new Vector2(copy.Min.X, floor.Center.Y - waveH * 0.5f),
            new Vector2(keys.Min.X - frame.Units(8f), floor.Center.Y + waveH * 0.5f));
        var sliderH = frame.Units(28f);
        var slider = volumeOpen
            ? new Rect(new Vector2(copy.Min.X, body.Center.Y - sliderH * 0.5f),
                new Vector2(body.Max.X, body.Center.Y + sliderH * 0.5f))
            : default;
        if (!volumeOpen && copy.Width > frame.Units(16f) && copy.Height > 4f)
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
                    : "Radio · " + genre;
            var text = new Rect(copy.Min, new Vector2(copy.Max.X, floor.Min.Y - frame.Units(2f)));
            frame.Text.DrawEllipsized(text.TopSlice(frame.Units(16f)), name,
                new TextStyle(FontRole.CaptionStrong, ink));
            frame.Text.DrawEllipsized(text.BottomSlice(frame.Units(14f)), blurb,
                new TextStyle(FontRole.Caption, muted));
            RememberWave(id, playing, frame.DeltaSeconds);
            DrawWave(frame, wave, id, Glow(), scroll, gold);
        }

        var shuffle = Rect.FromSize(keys.Min, new Vector2(small, keys.Height));
        var prev = Rect.FromSize(new Vector2(shuffle.Max.X + gap, keys.Min.Y), new Vector2(small, keys.Height));
        var play = Rect.FromSize(new Vector2(prev.Max.X + gap, keys.Min.Y), new Vector2(playW, keys.Height));
        var next = Rect.FromSize(new Vector2(play.Max.X + gap, keys.Min.Y), new Vector2(small, keys.Height));
        if (!volumeOpen)
        {
            DrawKey(frame, shuffle, StudioMark.Shuffle, gold, false);
            DrawKey(frame, prev, StudioMark.Prev, gold, false);
            DrawKey(frame, play, playing ? StudioMark.Pause : StudioMark.Play, gold, true);
            DrawKey(frame, next, StudioMark.Next, gold, false);
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

        if (wave.Width > 4f && frame.Input.ConsumeClick(wave))
        {
            return;
        }

        if (volumeOpen && frame.Input.ConsumeClick(row))
        {
            volumeOpen = false;
            volumeDrag = false;
            return;
        }

        if (frame.Input.ConsumeClick(row))
        {
            openStations(row);
        }
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

        var count = Math.Clamp((int)(area.Width / frame.Units(3.2f)), 18, 56);
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

    private void DrawArt(in AppletFrame frame, Rect area, string path, Vector4 gold)
    {
        if (area.Width < 4f || area.Height < 4f)
        {
            return;
        }

        var radius = frame.Units(10f);
        frame.Paint.Fill(area, frame.Theme.Palette.SurfaceRaised with { W = 0.55f }, radius);
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
            frame.Paint.ImageRounded(texture, area, Vector2.Zero, Vector2.One, Vector4.One, radius);
            return;
        }

        StudioMarks.Draw(frame.Paint, area.Inset(area.Width * 0.22f), StudioMark.Note, gold);
    }

    private static void DrawKey(in AppletFrame frame, Rect cell, StudioMark mark, Vector4 gold, bool primary)
    {
        if (cell.Width < 4f || cell.Height < 4f)
        {
            return;
        }

        var side = MathF.Min(cell.Width, cell.Height);
        var fill = primary ? gold with { W = 0.94f } : gold with { W = 0.42f };
        var ink = primary ? new Vector4(0.08f, 0.07f, 0.06f, 1f) : new Vector4(1f, 0.93f, 0.74f, 1f);
        var radius = side * (primary ? 0.46f : 0.40f);
        frame.Paint.FillCircle(cell.Center, radius, fill);
        frame.Paint.StrokeCircle(cell.Center, radius, gold with { W = 0.88f }, frame.Theme.Metrics.Hairline);
        StudioMarks.Draw(frame.Paint,
            Rect.FromSize(cell.Center - new Vector2(side * 0.36f), new Vector2(side * 0.72f)), mark, ink);
    }

    private static bool Hit(in AppletFrame frame, Rect cell)
    {
        if (cell.Width < 4f || cell.Height < 4f)
        {
            return false;
        }

        return frame.Input.ConsumeClick(cell);
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
