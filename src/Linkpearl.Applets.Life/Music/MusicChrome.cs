using System.Globalization;
using Linkpearl.Applets;
using Linkpearl.Geometry;
using Linkpearl.Input;
using Linkpearl.Layout;
using Linkpearl.Media;
using Linkpearl.Painting;

namespace Linkpearl.Applets.Life.Music;

internal static class MusicChrome
{
    public static readonly Vector4 Ground = new(0.16f, 0.58f, 0.82f, 1f);
    public static readonly Vector4 GroundMid = new(0.06f, 0.18f, 0.28f, 1f);
    public static readonly Vector4 GroundHi = new(0f, 0f, 0f, 1f);
    public static readonly Vector4 Card = new(0.08f, 0.08f, 0.08f, 0.96f);
    public static readonly Vector4 CardHi = new(0.12f, 0.12f, 0.12f, 1f);
    public static readonly Vector4 Purple = new(1f, 1f, 1f, 1f);
    public static readonly Vector4 PurpleDim = new(1f, 1f, 1f, 0.18f);
    public static readonly Vector4 DockBlue = new(0.18f, 0.86f, 1f, 1f);
    public static readonly Vector4 Live = new(1f, 1f, 1f, 1f);
    public static readonly Vector4 LiveOn = new(0.28f, 0.82f, 0.42f, 1f);
    public static readonly Vector4 LiveOff = new(0.62f, 0.62f, 0.68f, 1f);
    public static readonly Vector4 LikePink = new(1f, 0.36f, 0.56f, 1f);
    public static readonly Vector4 StarGold = new(1f, 0.84f, 0.18f, 1f);
    public static readonly Vector4 FollowGreen = new(0.28f, 0.82f, 0.42f, 1f);
    public static readonly Vector4 Ink = new(0.96f, 0.96f, 0.98f, 0.96f);
    public static readonly Vector4 Mute = new(0.70f, 0.70f, 0.76f, 0.88f);
    public static readonly Vector4 Faint = new(0.96f, 0.96f, 0.98f, 0.08f);

    public static void ArtShadow(in AppletFrame frame, Rect area)
    {
        if (area.Width < 12f || area.Height < 12f)
        {
            return;
        }

        var radius = frame.Units(8f);
        var drop = area.Translate(new Vector2(0f, frame.Units(4f)));
        frame.Paint.Glow(drop, new Vector4(0f, 0f, 0f, 0.18f), radius, frame.Units(10f));
        frame.Paint.Fill(drop, new Vector4(0f, 0f, 0f, 0.08f), radius);
    }

    public static void PaintGround(in AppletFrame frame, Rect area) =>
        AppGround.MusicWash(frame, area);

    public static void Plate(in AppletFrame frame, Rect area, float radius)
    {
        frame.Paint.Fill(area, Card, radius);
        frame.Paint.Stroke(area, Faint, frame.Units(1f), radius);
    }

    public static void GlowPlate(in AppletFrame frame, Rect area, float radius, bool on)
    {
        frame.Paint.Fill(area, on ? PurpleDim : Card, radius);
        frame.Paint.Stroke(area, on ? Purple : Faint, frame.Units(1.2f), radius);
    }

    public static void Primary(in AppletFrame frame, Rect area, string label)
    {
        frame.Paint.Fill(area, Purple, frame.Units(12f));
        frame.Text.DrawIn(area, label, new TextStyle(FontRole.BodyStrong, GroundHi, TextAlign.Center));
    }

    public static bool FollowAction(in AppletFrame frame, Rect area, bool on)
    {
        if (on)
        {
            Plate(frame, area, frame.Units(12f));
            frame.Text.DrawIn(area, "Following", new TextStyle(FontRole.BodyStrong, Ink, TextAlign.Center));
        }
        else
        {
            Primary(frame, area, "Follow");
        }

        return frame.Input.ConsumeClick(area);
    }

    public static bool FollowChip(in AppletFrame frame, Rect area, bool on)
    {
        frame.Paint.Fill(area, on ? CardHi : Purple, frame.Units(10f));
        frame.Text.DrawIn(area, on ? "Following" : "Follow",
            new TextStyle(FontRole.CaptionStrong, on ? Ink : GroundHi, TextAlign.Center));
        return frame.Input.ConsumeClick(area);
    }

    public static void ListenerCount(in AppletFrame frame, Rect area, int count, bool compact = false)
    {
        if (area.Width < 8f || area.Height < 8f)
        {
            return;
        }

        var n = Math.Max(0, count);
        var label = compact ? CompactCount(n) : n == 1 ? "1 listening" : n + " listening";
        frame.Paint.Fill(area, CardHi, area.Height * 0.5f);
        var icon = area.LeftSlice(MathF.Min(area.Height, frame.Units(22f)));
        DrawEars(frame.Paint, icon, Ink);
        frame.Text.DrawIn(area.Inset(new Edges(icon.Width, 0f, frame.Units(6f), 0f)), label,
            new TextStyle(compact ? FontRole.CaptionStrong : FontRole.Caption, Ink, TextAlign.Center));
    }

    public static float ListenerWidth(in AppletFrame frame, int count, bool compact)
    {
        var n = Math.Max(0, count);
        var text = compact ? CompactCount(n) : n == 1 ? "1 listening" : n + " listening";
        return frame.Units(compact ? 36f : 28f) + text.Length * frame.Units(6.2f);
    }

    private static string CompactCount(int count) =>
        count >= 1000000 ? (count / 1000000f).ToString("0.#") + "M" :
        count >= 1000 ? (count / 1000f).ToString("0.#") + "k" : count.ToString();

    private static void DrawEars(IPaintSurface paint, Rect area, Vector4 ink)
    {
        var c = area.Center;
        var s = MathF.Min(area.Width, area.Height) * 0.28f;
        var stroke = MathF.Max(1.2f, s * 0.28f);
        paint.StrokeCircle(c + new Vector2(0f, s * 0.12f), s * 0.72f, ink, stroke);
        paint.Fill(Rect.FromSize(c + new Vector2(-s * 1.05f, -s * 0.18f), new Vector2(s * 0.42f, s * 0.72f)), ink,
            s * 0.18f);
        paint.Fill(Rect.FromSize(c + new Vector2(s * 0.63f, -s * 0.18f), new Vector2(s * 0.42f, s * 0.72f)), ink,
            s * 0.18f);
    }

    public static void LiveMark(in AppletFrame frame, Rect area)
    {
        frame.Paint.Fill(area, Live with { W = 0.22f }, frame.Units(8f));
        frame.Text.DrawIn(area, "LIVE", new TextStyle(FontRole.CaptionStrong, Live, TextAlign.Center));
    }

    public static void Meter(in AppletFrame frame, Rect area, float level)
    {
        Plate(frame, area, frame.Units(8f));
        var fill = Math.Clamp(level, 0f, 1f);
        if (fill <= 0.01f)
        {
            return;
        }

        var bar = area.Inset(frame.Units(4f));
        var hot = Rect.FromSize(bar.Min, new Vector2(bar.Width * fill, bar.Height));
        frame.Paint.Fill(hot, fill > 0.08f ? Live : Mute, frame.Units(6f));
    }

    public static void OffMark(in AppletFrame frame, Rect area)
    {
        frame.Paint.Fill(area, Faint, frame.Units(8f));
        frame.Text.DrawIn(area, "OFFLINE", new TextStyle(FontRole.CaptionStrong, Mute, TextAlign.Center));
    }

    public static bool Chip(in AppletFrame frame, Rect area, string label, bool on)
    {
        frame.Paint.Fill(area, on ? Purple : CardHi, frame.Units(12f));
        frame.Text.DrawIn(area, label,
            new TextStyle(FontRole.CaptionStrong, on ? GroundHi : Mute, TextAlign.Center));
        return frame.Input.PressedInside(area) || frame.Input.ConsumeClick(area);
    }

    public static bool Row(in AppletFrame frame, Rect area, string title, string detail, bool live)
    {
        Plate(frame, area, frame.Units(12f));
        var inset = area.Inset(frame.Units(10f));
        var thumb = inset.LeftSlice(frame.Units(36f));
        frame.Paint.Fill(thumb, PurpleDim, frame.Units(8f));
        var body = inset.Inset(new Edges(frame.Units(44f), 0f, live ? frame.Units(44f) : 0f, 0f));
        frame.Text.DrawEllipsized(body.TopSlice(frame.Units(18f)), title, new TextStyle(FontRole.BodyStrong, Ink));
        frame.Text.DrawEllipsized(body.BottomSlice(frame.Units(16f)), detail, new TextStyle(FontRole.Caption, Mute));
        if (live)
        {
            LiveMark(frame, inset.RightSlice(frame.Units(40f)).TopSlice(frame.Units(18f)));
        }

        return frame.Input.PressedInside(area) || frame.Input.ConsumeClick(area);
    }

    public static void Kicker(in AppletFrame frame, Rect area, string label)
    {
        frame.Text.DrawEllipsized(area, label, new TextStyle(FontRole.BodyStrong, Ink));
    }

    public static void FitCopy(in AppletFrame frame, ref Stack stack, string text, Vector4 color,
        FontRole role = FontRole.Caption)
    {
        if (text.Length == 0)
        {
            return;
        }

        var width = MathF.Max(8f, stack.Remaining.Width);
        var size = frame.Text.MeasureWrapped(text, role, width);
        var line = MathF.Max(frame.Text.LineHeight(role), frame.Units(14f));
        var height = Math.Clamp(size.Y + frame.Units(2f), line, line * 4f);
        frame.Text.DrawWrapped(stack.Take(height), text, new TextStyle(role, color));
    }

    public static bool SeeAll(in AppletFrame frame, Rect area)
    {
        frame.Paint.Fill(area, CardHi, area.Height * 0.5f);
        frame.Text.DrawIn(area, "See all", new TextStyle(FontRole.Caption, Ink, TextAlign.Center));
        return frame.Input.ConsumeClick(area);
    }

    public static bool Section(in AppletFrame frame, Rect area, string title)
    {
        var see = area.RightSlice(frame.Units(58f));
        Kicker(frame, area.Inset(new Edges(0f, 0f, see.Width + frame.Units(8f), 0f)), title);
        return SeeAll(frame, see);
    }

    public static bool ToggleRow(in AppletFrame frame, Rect area, string title, string detail, bool on)
    {
        Plate(frame, area, frame.Units(12f));
        var inset = area.Inset(frame.Units(10f));
        var knob = inset.RightSlice(frame.Units(52f)).Inset(new Edges(0f, frame.Units(6f)));
        var copy = inset.Inset(new Edges(0f, 0f, knob.Width + frame.Units(8f), 0f));
        frame.Text.DrawEllipsized(copy.TopSlice(frame.Units(16f)), title,
            new TextStyle(FontRole.CaptionStrong, Ink));
        frame.Text.DrawEllipsized(copy.BottomSlice(frame.Units(14f)), detail,
            new TextStyle(FontRole.Caption, Mute));
        frame.Paint.Fill(knob, on ? Purple : CardHi, knob.Height * 0.5f);
        frame.Text.DrawIn(knob, on ? "On" : "Off",
            new TextStyle(FontRole.CaptionStrong, on ? GroundHi : Mute, TextAlign.Center));
        return frame.Input.ConsumeClick(area);
    }

    public static bool SoftPill(in AppletFrame frame, Rect area, string label, bool on)
    {
        if (on)
        {
            frame.Paint.Fill(area, CardHi, area.Height * 0.5f);
        }

        frame.Text.DrawIn(area, label, new TextStyle(FontRole.BodyStrong, on ? Ink : Mute, TextAlign.Center));
        return frame.Input.ConsumeClick(area);
    }

    public static void GenreTile(in AppletFrame frame, Rect area, string label, Vector4 tint, bool on)
    {
        frame.Paint.Fill(area, new Vector4(0.04f, 0.04f, 0.04f, 1f), frame.Units(12f));
        var waves = area.Inset(frame.Units(4f));
        var origin = new Vector2(waves.Max.X + waves.Width * 0.08f, waves.Max.Y + waves.Height * 0.12f);
        var span = MathF.Max(waves.Width, waves.Height);
        for (var ring = 0; ring < 6; ring++)
        {
            frame.Paint.StrokeCircle(origin, span * (0.28f + ring * 0.16f), tint with { W = on ? 0.42f : 0.22f },
                MathF.Max(1.2f, frame.Units(1.4f)));
        }

        frame.Paint.FillCircle(area.Center + new Vector2(area.Width * 0.18f, area.Height * 0.10f),
            MathF.Min(area.Width, area.Height) * 0.28f, tint with { W = 0.55f });
        frame.Paint.Stroke(area, tint, frame.Units(1.8f), frame.Units(12f));
        frame.Text.DrawEllipsized(area.Inset(frame.Units(10f)).TopSlice(frame.Units(22f)), label,
            new TextStyle(FontRole.CaptionStrong, Ink));
    }

    public static Vector4 GenreTint(int index)
    {
        return (index % 12) switch
        {
            0 => new Vector4(0.62f, 0.28f, 1f, 1f),
            1 => new Vector4(1f, 0.32f, 0.62f, 1f),
            2 => new Vector4(1f, 0.86f, 0.16f, 1f),
            3 => new Vector4(0.18f, 0.82f, 0.78f, 1f),
            4 => new Vector4(1f, 0.42f, 0.08f, 1f),
            5 => new Vector4(0.12f, 0.72f, 0.68f, 1f),
            6 => new Vector4(0.42f, 0.92f, 0.22f, 1f),
            7 => new Vector4(1f, 0.18f, 0.55f, 1f),
            8 => new Vector4(0.92f, 0.28f, 0.72f, 1f),
            9 => new Vector4(0.18f, 0.86f, 1f, 1f),
            10 => new Vector4(1f, 0.55f, 0.12f, 1f),
            _ => new Vector4(0.72f, 1f, 0.32f, 1f),
        };
    }

    public static void PlayRing(in AppletFrame frame, Rect area, bool playing, float progress)
    {
        var radius = MathF.Min(area.Width, area.Height) * 0.42f;
        frame.Paint.StrokeCircle(area.Center, radius, Purple, MathF.Max(1.4f, radius * 0.12f));
        frame.Paint.FillCircle(area.Center, radius * 0.78f, Ink);
        frame.Text.DrawIn(area, playing ? "❚❚" : "▶",
            new TextStyle(FontRole.CaptionStrong, GroundHi, TextAlign.Center));
        _ = progress;
    }

    public static void Title(in AppletFrame frame, Rect area, string label)
    {
        frame.Text.DrawEllipsized(area, label, new TextStyle(FontRole.Title, Ink));
    }

    public static void HomeGear(in AppletFrame frame, Rect area)
    {
        var texture = frame.Textures.FromFile(AppIconCatalog.Glyph(frame.Paths, "settings.png"));
        if (texture is not { IsReady: true })
        {
            texture = frame.Textures.FromFile(AppIconCatalog.Absolute(frame.Paths, "settings.png"));
        }

        if (texture is { IsReady: true })
        {
            var side = MathF.Min(area.Width, area.Height) * 0.86f;
            var dest = Rect.FromSize(area.Center - new Vector2(side * 0.5f, side * 0.5f), new Vector2(side, side));
            frame.Paint.Image(texture, dest, Ink);
            return;
        }

        var center = area.Center;
        var radius = MathF.Min(area.Width, area.Height) * 0.28f;
        var stroke = MathF.Max(1.2f, frame.Units(1.5f));
        frame.Paint.StrokeCircle(center, radius, Ink, stroke);
        frame.Paint.StrokeCircle(center, radius * 0.42f, Ink, stroke);
        for (var tooth = 0; tooth < 8; tooth++)
        {
            var angle = tooth * (MathF.PI * 2f / 8f);
            var tip = center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * (radius * 1.22f);
            frame.Paint.FillCircle(tip, stroke * 0.9f, Ink);
        }
    }

    public static bool Back(in AppletFrame frame, Rect row, string label)
    {
        var hit = row.LeftSlice(frame.Units(28f));
        frame.Text.DrawIn(hit, "‹", new TextStyle(FontRole.Title, Purple, TextAlign.Center));
        frame.Text.DrawEllipsized(row.Inset(new Edges(frame.Units(28f), 0f, 0f, 0f)), label,
            new TextStyle(FontRole.BodyStrong, Ink));
        return frame.Input.ConsumeClick(row.LeftSlice(frame.Units(90f)));
    }

    public static Stack BeginSheet(in AppletFrame frame, Rect view, MusicState state, float gap)
    {
        var content = MathF.Max(view.Height + frame.Units(8f), state.SheetHeight);
        Wheel(frame, view, state, content);
        var sheet = Rect.FromSize(new Vector2(view.Min.X, view.Min.Y - state.Scroll),
            new Vector2(view.Width, content));
        frame.Paint.PushClip(view);
        return new Stack(sheet, StackAxis.Vertical, gap);
    }

    public static void EndSheet(in AppletFrame frame, Rect view, MusicState state, ref Stack stack)
    {
        var used = stack.Remaining.Min.Y - (view.Min.Y - state.Scroll);
        state.SheetHeight = MathF.Max(used + frame.Units(16f), view.Height);
        frame.Paint.PopClip();
    }

    public static void Wheel(in AppletFrame frame, Rect area, MusicState state, float content)
    {
        var max = MathF.Max(0f, content - area.Height);
        var next = state.Scroll;
        ScrollSlider.Steer(frame, area, ref next, max);
        state.Scroll = Math.Clamp(next, 0f, max);
    }

    public static void Wave(in AppletFrame frame, Rect area, float pulse, Vector4 ink)
    {
        DockWave(frame, area, string.Empty, pulse, pulse * 9f, ink);
    }

    public static void DockWave(in AppletFrame frame, Rect area, string id, float glow, float travel, Vector4 ink)
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
                var t = Math.Clamp((along - (glow - 0.04f)) / 0.05f, 0f, 1f);
                var mix = 1f - t * t * (3f - 2f * t);
                frame.Paint.Fill(new Rect(new Vector2(x, mid - half), new Vector2(x + barW, mid + half)),
                    ink with { W = 0.22f + 0.70f * mix }, barW * 0.45f);
            }
        }
        finally
        {
            frame.Paint.PopClip();
        }
    }

    public static bool LiveMarkHit(in AppletFrame frame, Rect area)
    {
        return area.Width > 4f && area.Height > 4f && frame.Input.ConsumeClick(area);
    }

    public static void SaveStar(in AppletFrame frame, Rect area, bool on)
    {
        if (area.IsEmpty)
        {
            return;
        }

        var ink = on ? StarGold : Ink;
        if (TryGlyph(frame, area, "music-save.png", ink))
        {
            return;
        }

        frame.Text.DrawIn(area, "★", new TextStyle(FontRole.Title, ink, TextAlign.Center));
    }

    public static void FollowPerson(in AppletFrame frame, Rect area, bool on)
    {
        if (area.IsEmpty)
        {
            return;
        }

        var ink = on ? FollowGreen : Ink;
        if (TryGlyph(frame, area, "music-follow.png", ink))
        {
            return;
        }

        var side = MathF.Min(area.Width, area.Height);
        var head = new Vector2(area.Center.X - side * 0.16f, area.Center.Y - side * 0.18f);
        frame.Paint.FillCircle(head, side * 0.13f, ink);
        var torso = Rect.FromSize(new Vector2(head.X - side * 0.22f, head.Y + side * 0.12f),
            new Vector2(side * 0.44f, side * 0.28f));
        frame.Paint.Fill(torso, ink, side * 0.22f, Corner.Bottom);
        var plus = new Vector2(area.Center.X + side * 0.22f, area.Center.Y + side * 0.04f);
        var bar = MathF.Max(1.6f, side * 0.07f);
        var arm = side * 0.12f;
        frame.Paint.Fill(Rect.FromSize(new Vector2(plus.X - arm, plus.Y - bar * 0.5f), new Vector2(arm * 2f, bar)), ink,
            bar * 0.5f);
        frame.Paint.Fill(Rect.FromSize(new Vector2(plus.X - bar * 0.5f, plus.Y - arm), new Vector2(bar, arm * 2f)), ink,
            bar * 0.5f);
    }

    public static void DockVolume(in AppletFrame frame, Rect row, float value, ref bool dragging, Action<float> set)
    {
        if (row.Width < 8f || row.Height < 8f)
        {
            return;
        }

        var amount = Math.Clamp(value, 0f, 1f);
        var radius = row.Height * 0.5f;
        var track = new Vector4(0.16f, 0.16f, 0.18f, 0.90f);
        var fill = new Vector4(0.97f, 0.97f, 0.99f, 0.98f);
        var knob = new Vector4(0.86f, 0.86f, 0.89f, 1f);
        var markOn = new Vector4(0.14f, 0.14f, 0.16f, 1f);
        var markOff = new Vector4(0.94f, 0.94f, 0.96f, 1f);
        frame.Paint.Fill(row, track, radius);
        frame.Paint.Fill(row.LeftSlice(MathF.Max(row.Height, row.Width * amount)), fill, radius);
        var knobX = Math.Clamp(row.Min.X + row.Width * amount, row.Min.X + radius, row.Max.X - radius);
        var center = new Vector2(knobX, row.Center.Y);
        frame.Paint.FillCircle(center, row.Height * 0.20f, knob);
        frame.Paint.FillCircle(center + new Vector2(-row.Height * 0.04f, -row.Height * 0.05f), row.Height * 0.07f,
            new Vector4(1f, 1f, 1f, 0.72f));
        var inset = row.Height * 0.18f;
        var mark = row.Height * 0.558f;
        var left = Rect.FromSize(new Vector2(row.Min.X + inset, row.Center.Y - mark * 0.5f), new Vector2(mark, mark));
        var right = Rect.FromSize(new Vector2(row.Max.X - inset - mark, row.Center.Y - mark * 0.5f),
            new Vector2(mark, mark));
        DockNote(frame.Paint, left, amount > 0.12f ? markOn : markOff);
        DockSpeaker(frame.Paint, right, amount > 0.88f ? markOn : markOff);
        var input = frame.Input;
        if (input.WasPressed(row) || (input.IsHeld() && row.Contains(input.Pointer) && !dragging))
        {
            dragging = true;
        }

        if (dragging && input.IsHeld())
        {
            var next = Math.Clamp((input.Pointer.X - row.Min.X) / MathF.Max(row.Width, 1f), 0f, 1f);
            set(next);
            input.Claim(row);
            input.ConsumeClick(row);
            return;
        }

        dragging = false;
        input.ConsumeClick(row);
    }

    private static void DockNote(IPaintSurface paint, Rect area, Vector4 ink)
    {
        var c = area.Center;
        var s = MathF.Min(area.Width, area.Height) * 0.32f;
        var stroke = MathF.Max(1.3f, s * 0.22f);
        paint.FillCircle(c + new Vector2(-s * 0.28f, s * 0.42f), s * 0.22f, ink);
        paint.Line(c + new Vector2(-s * 0.08f, s * 0.42f), c + new Vector2(-s * 0.08f, -s * 0.62f), ink, stroke);
        paint.Line(c + new Vector2(-s * 0.08f, -s * 0.62f), c + new Vector2(s * 0.55f, -s * 0.38f), ink, stroke);
    }

    private static void DockSpeaker(IPaintSurface paint, Rect area, Vector4 ink)
    {
        var c = area.Center;
        var s = MathF.Min(area.Width, area.Height) * 0.28f;
        var stroke = MathF.Max(1.1f, s * 0.22f);
        paint.Fill(Rect.FromSize(c + new Vector2(-s * 0.72f, -s * 0.28f), new Vector2(s * 0.42f, s * 0.56f)), ink,
            s * 0.08f);
        paint.Line(c + new Vector2(-s * 0.30f, -s * 0.28f), c + new Vector2(s * 0.18f, -s * 0.72f), ink, stroke);
        paint.Line(c + new Vector2(-s * 0.30f, s * 0.28f), c + new Vector2(s * 0.18f, s * 0.72f), ink, stroke);
        paint.StrokeCircle(c + new Vector2(s * 0.12f, 0f), s * 0.42f, ink, stroke);
        paint.StrokeCircle(c + new Vector2(s * 0.12f, 0f), s * 0.68f, ink with { W = ink.W * 0.7f }, stroke);
    }

    public static void ReportFlag(in AppletFrame frame, Rect area)
    {
        if (TryGlyph(frame, area, "music-report.png", Ink))
        {
            return;
        }

        frame.Text.DrawIn(area, "⚑", new TextStyle(FontRole.Title, Ink, TextAlign.Center));
    }

    private static bool TryGlyph(in AppletFrame frame, Rect area, string file, Vector4 ink)
    {
        var texture = frame.Textures.FromFile(AppIconCatalog.Glyph(frame.Paths, file));
        if (texture is not { IsReady: true } || texture.Handle == 0)
        {
            return false;
        }

        var dest = CoverFit.Contained(texture.Size, CoverFit.InscribedSquare(area.Inset(area.Height * 0.04f)));
        if (dest.IsEmpty)
        {
            return false;
        }

        frame.Paint.Image(texture, dest, ink);
        return true;
    }

    public static void Rail(in AppletFrame frame, Rect track, float offset, float content, float view)
    {
        var dragging = false;
        DragRail(frame, track, ref offset, content, view, ref dragging);
    }

    public static void DragRail(in AppletFrame frame, Rect track, ref float offset, float content, float view,
        ref bool dragging)
    {
        if (content <= view + 1f || track.Height < 8f)
        {
            dragging = false;
            return;
        }

        var max = MathF.Max(0f, content - view);
        var thumbH = MathF.Max(frame.Units(22f), track.Height * (view / content));
        var travel = MathF.Max(0f, track.Height - thumbH);
        var y = track.Min.Y + travel * (offset / MathF.Max(max, 1f));
        var thumb = Rect.FromSize(new Vector2(track.Min.X, y), new Vector2(track.Width, thumbH));
        var radius = MathF.Min(track.Width, frame.Units(6f)) * 0.5f;
        frame.Paint.Fill(track, Faint, radius);
        frame.Paint.Fill(thumb, Purple, radius);
        var input = frame.Input;
        if (input.WasPressed(track) || (dragging && input.IsHeld()))
        {
            dragging = true;
            var t = travel <= 0f ? 0f : (input.Pointer.Y - track.Min.Y - thumbH * 0.5f) / travel;
            offset = Math.Clamp(t, 0f, 1f) * max;
            input.Claim(track);
            input.ConsumeClick(track);
            return;
        }

        if (!input.IsHeld())
        {
            dragging = false;
        }
    }

    public static bool MixFader(in AppletFrame frame, Rect area, string label, float value, ref bool dragging,
        Action<float> set)
    {
        if (area.Width < 8f || area.Height < 8f)
        {
            return false;
        }

        var amount = Math.Clamp(value, 0f, 1f);
        var caption = area.BottomSlice(frame.Units(32f));
        var well = area.Inset(new Edges(frame.Units(10f), frame.Units(6f), frame.Units(10f),
            caption.Height + frame.Units(4f)));
        var width = MathF.Min(frame.Units(10f), well.Width);
        var bar = Rect.FromSize(new Vector2(well.Center.X - width * 0.5f, well.Min.Y),
            new Vector2(width, well.Height));
        frame.Paint.Fill(bar, new Vector4(0.16f, 0.16f, 0.18f, 0.94f), width * 0.5f);
        var fill = MathF.Max(width, bar.Height * amount);
        frame.Paint.Fill(new Rect(new Vector2(bar.Min.X, bar.Max.Y - fill), bar.Max), LiveOn with { W = 0.92f },
            width * 0.5f);
        var knobH = frame.Units(14f);
        var knobW = MathF.Min(well.Width, frame.Units(22f));
        var knobY = Math.Clamp(bar.Max.Y - bar.Height * amount - knobH * 0.5f, bar.Min.Y, bar.Max.Y - knobH);
        frame.Paint.Fill(Rect.FromSize(new Vector2(well.Center.X - knobW * 0.5f, knobY), new Vector2(knobW, knobH)),
            new Vector4(0.96f, 0.96f, 0.98f, 1f), knobH * 0.22f);
        frame.Text.DrawIn(caption.TopSlice(frame.Units(16f)), label,
            new TextStyle(FontRole.CaptionStrong, Ink, TextAlign.Center));
        frame.Text.DrawIn(caption.BottomSlice(frame.Units(14f)),
            ((int)MathF.Round(amount * 100f)).ToString(CultureInfo.InvariantCulture) + "%",
            new TextStyle(FontRole.Caption, Mute, TextAlign.Center));
        var input = frame.Input;
        if (input.WasPressed(area) || input.IsHeld() && area.Contains(input.Pointer) && !dragging)
        {
            dragging = true;
        }

        if (dragging && input.IsHeld())
        {
            var next = Math.Clamp((bar.Max.Y - input.Pointer.Y) / MathF.Max(bar.Height, 1f), 0f, 1f);
            var changed = Math.Abs(next - amount) > 0.001f;
            if (changed)
            {
                set(next);
            }

            input.Claim(area);
            input.ConsumeClick(area);
            return changed;
        }

        dragging = false;
        input.ConsumeClick(area);
        return false;
    }
}
