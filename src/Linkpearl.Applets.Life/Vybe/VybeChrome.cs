using System.IO;
using Linkpearl.Applets;
using Linkpearl.Geometry;
using Linkpearl.Layout;
using Linkpearl.Media;
using Linkpearl.Painting;

namespace Linkpearl.Applets.Life.Vybe;

internal readonly record struct NightPalette(
    Vector4 Ground, Vector4 Card, Vector4 CardHi, Vector4 Accent, Vector4 AccentDim, Vector4 AccentInk, Vector4 Ink,
    Vector4 Mute, Vector4 Faint, Vector4 Danger);

internal static class VybeChrome
{
    public const float WashSeconds = 0.4f;
    public const float HoldSeconds = 1f;

    public static readonly Vector4 Online = new(0.000f, 0.902f, 0.463f, 1f);
    public static readonly Vector4 PlusViolet = new(0.620f, 0.280f, 0.920f, 1f);
    public const byte LalafellRace = 3;

    public static readonly NightPalette Night = new(
        new Vector4(0f, 0f, 0f, 1f),
        new Vector4(0.090f, 0.090f, 0.098f, 1f),
        new Vector4(0.130f, 0.130f, 0.140f, 1f),
        new Vector4(0.950f, 0.320f, 0.480f, 1f),
        new Vector4(0.950f, 0.320f, 0.480f, 0.16f),
        new Vector4(1.000f, 1.000f, 1.000f, 1f),
        new Vector4(0.960f, 0.960f, 0.970f, 1f),
        new Vector4(0.560f, 0.560f, 0.580f, 1f),
        new Vector4(1.000f, 1.000f, 1.000f, 0.08f),
        new Vector4(0.910f, 0.361f, 0.400f, 1f));

    public static readonly NightPalette Day = new(
        new Vector4(0f, 0f, 0f, 1f),
        new Vector4(0.090f, 0.090f, 0.098f, 1f),
        new Vector4(0.130f, 0.130f, 0.140f, 1f),
        new Vector4(0.900f, 0.260f, 0.400f, 1f),
        new Vector4(0.900f, 0.260f, 0.400f, 0.12f),
        new Vector4(1.000f, 1.000f, 1.000f, 1f),
        new Vector4(0.960f, 0.960f, 0.970f, 1f),
        new Vector4(0.560f, 0.560f, 0.580f, 1f),
        new Vector4(1.000f, 1.000f, 1.000f, 0.08f),
        new Vector4(0.780f, 0.280f, 0.320f, 1f));

    public static NightPalette Tone(bool night) => night ? Night : Day;

    public static bool IsLalafell(byte raceId, string raceName) =>
        raceId == LalafellRace ||
        raceName.Contains("Lalafell", StringComparison.OrdinalIgnoreCase) ||
        raceName.Contains("ララフェル", StringComparison.Ordinal);

    public static void Stage(in AppletFrame frame)
    {
        var black = new Vector4(0f, 0f, 0f, 1f);
        frame.Paint.Fill(frame.Content, black);
        var path = frame.Paths.Asset(Path.Combine(WallpaperCatalog.Folder, "vybe-wash.png"));
        var texture = frame.Textures.FromFile(path);
        if (texture is { IsReady: true })
        {
            var uv = CoverFit.Uv(texture.Size, frame.Content.Size);
            frame.Paint.Image(texture, frame.Content, uv.Min, uv.Max, Vector4.One);
        }

        var blue = new Vector4(0.10f, 0.24f, 0.78f, 0.22f);
        var dusk = new Vector4(0f, 0f, 0f, 0.94f);
        frame.Paint.FillGradient(frame.Content, blue, dusk, GradientAxis.Vertical);
    }

    public static void Wash(in AppletFrame frame, float elapsed, bool toNight)
    {
        if (elapsed <= 0f)
        {
            return;
        }

        var alpha = Math.Clamp(elapsed / WashSeconds, 0f, 1f);
        var ground = Tone(toNight).Ground;
        frame.Paint.Fill(frame.Content, ground with { W = alpha });
    }

    public static void Plate(in AppletFrame frame, Rect area, float radius, bool night)
    {
        var tone = Tone(night);
        frame.Paint.Fill(area, tone.Card, radius);
        frame.Paint.Stroke(area, tone.Faint, frame.Units(1f), radius);
    }

    public static Rect FieldWell(in AppletFrame frame, Rect area, bool night)
    {
        _ = night;
        var radius = frame.Units(16f);
        frame.Paint.Fill(area, new Vector4(0.05f, 0.05f, 0.06f, 0.92f), radius);
        frame.Paint.Stroke(area, new Vector4(1f, 1f, 1f, 0.10f), frame.Units(1f), radius);
        return area.Inset(new Edges(frame.Units(12f), frame.Units(10f)));
    }

    public static void ComposeVeil(in AppletFrame frame, Rect area) =>
        frame.Paint.Fill(area, new Vector4(0.02f, 0.02f, 0.03f, 0.55f));

    public static bool QuietTool(in AppletFrame frame, Rect area, string label, bool on, bool night)
    {
        var tone = Tone(night);
        var radius = area.Height * 0.5f;
        var fill = on ? tone.Accent with { W = 0.20f } : new Vector4(1f, 1f, 1f, 0.06f);
        var rim = on ? tone.Accent with { W = 0.55f } : new Vector4(1f, 1f, 1f, 0.10f);
        frame.Paint.Fill(area, fill, radius);
        frame.Paint.Stroke(area, rim, frame.Units(1f), radius);
        frame.Text.DrawIn(area, label,
            new TextStyle(FontRole.CaptionStrong, on ? Vector4.One : tone.Mute, TextAlign.Center));
        return frame.Input.ConsumeClick(area);
    }

    public static bool ComposeSend(in AppletFrame frame, Rect area, bool ready, bool plus) =>
        ComposeSend(frame, area, plus ? "Post to VYBE+" : "Post to VYBE", ready, plus);

    public static bool ComposeSend(in AppletFrame frame, Rect area, string label, bool ready, bool plus,
        bool hit = true)
    {
        var radius = area.Height * 0.5f;
        var fill = !ready
            ? new Vector4(1f, 1f, 1f, 0.12f)
            : plus ? PlusViolet : Night.Accent;
        frame.Paint.Fill(area, fill, radius);
        frame.Text.DrawIn(area, label,
            new TextStyle(FontRole.CaptionStrong, ready ? Vector4.One : new Vector4(1f, 1f, 1f, 0.38f),
                TextAlign.Center));
        return hit && ready && frame.Input.ConsumeClick(area);
    }

    public static bool DestCard(in AppletFrame frame, Rect area, string title, string line, bool on, bool plus,
        bool hit = true)
    {
        var accent = plus ? PlusViolet : Night.Accent;
        var radius = frame.Units(14f);
        frame.Paint.Fill(area, on ? accent with { W = 0.22f } : new Vector4(1f, 1f, 1f, 0.05f), radius);
        frame.Paint.Stroke(area, on ? accent : new Vector4(1f, 1f, 1f, 0.10f), frame.Units(1.4f), radius);
        var inner = area.Inset(new Edges(frame.Units(10f), frame.Units(8f)));
        frame.Text.DrawIn(inner.TopSlice(frame.Units(18f)), title,
            new TextStyle(FontRole.CaptionStrong, on ? Vector4.One : Night.Mute));
        frame.Text.DrawIn(inner.BottomSlice(frame.Units(16f)), line,
            new TextStyle(FontRole.Caption, on ? new Vector4(1f, 1f, 1f, 0.86f) : Night.Mute));
        return hit && frame.Input.ConsumeClick(area);
    }

    public static void LaneBadge(in AppletFrame frame, Rect area, bool plus)
    {
        var accent = plus ? PlusViolet : Night.Accent;
        var radius = area.Height * 0.5f;
        frame.Paint.Fill(area, accent with { W = 0.18f }, radius);
        frame.Paint.Stroke(area, accent, frame.Units(1.2f), radius);
        frame.Text.DrawIn(area, plus ? "VYBE+  •  18+" : "VYBE  •  SFW",
            new TextStyle(FontRole.CaptionStrong, Vector4.One, TextAlign.Center));
    }

    public static void TagChip(in AppletFrame frame, Rect area, string label, bool plus)
    {
        var accent = plus ? PlusViolet : Night.Accent;
        var radius = area.Height * 0.5f;
        frame.Paint.Fill(area, accent with { W = 0.14f }, radius);
        frame.Paint.Stroke(area, accent with { W = 0.55f }, frame.Units(1.1f), radius);
        frame.Text.DrawIn(area, label,
            new TextStyle(FontRole.CaptionStrong, Vector4.One, TextAlign.Center));
    }

    public static void PostSheet(in AppletFrame frame, Rect area)
    {
        frame.Paint.Fill(area, new Vector4(0.055f, 0.055f, 0.06f, 0.94f), frame.Units(16f));
        frame.Paint.Stroke(area, new Vector4(1f, 1f, 1f, 0.08f), frame.Units(1f), frame.Units(16f));
    }

    public static float OutlineChipWidth(in AppletFrame frame, string label) =>
        frame.Text.Measure(label, FontRole.CaptionStrong).X + frame.Units(22f);

    public static bool OutlineChip(in AppletFrame frame, Rect area, string label, bool on, bool plus,
        bool hit = true)
    {
        var accent = plus ? PlusViolet : Night.Accent;
        var radius = area.Height * 0.5f;
        if (on)
        {
            frame.Paint.Fill(area, accent with { W = 0.16f }, radius);
        }

        frame.Paint.Stroke(area, on ? accent : new Vector4(1f, 1f, 1f, 0.22f), frame.Units(1.1f), radius);
        frame.Text.DrawIn(area, label,
            new TextStyle(FontRole.CaptionStrong, on ? Vector4.One : new Vector4(1f, 1f, 1f, 0.82f),
                TextAlign.Center));
        return hit && frame.Input.ConsumeClick(area);
    }

    public static void Hairline(in AppletFrame frame, Rect area) =>
        frame.Paint.Fill(area, new Vector4(1f, 1f, 1f, 0.08f));

    public const string LikeGlyph = "vybe-like.png";
    public const string CommentGlyph = "vybe-comment.png";
    public const string RepostGlyph = "vybe-repost.png";
    public const string SaveGlyph = "vybe-save.png";

    public static bool StoryAction(in AppletFrame frame, Rect area, string glyph, string count, bool on)
    {
        var ink = on ? Night.Accent : Vector4.One;
        frame.Text.DrawIn(area.TopSlice(area.Height * 0.58f), glyph,
            new TextStyle(FontRole.Title, ink, TextAlign.Center));
        frame.Text.DrawIn(area.BottomSlice(area.Height * 0.42f), count,
            new TextStyle(FontRole.CaptionStrong, Vector4.One, TextAlign.Center));
        return frame.Input.ConsumeClick(area);
    }

    public static bool StoryGlyphAction(in AppletFrame frame, Rect area, string file, string count, bool on,
        string fallback)
    {
        var ink = on ? Night.Accent : Vector4.One;
        ActionGlyph(frame, area.TopSlice(area.Height * 0.58f).Inset(frame.Units(4f)), file, ink, fallback,
            FontRole.Title);
        frame.Text.DrawIn(area.BottomSlice(area.Height * 0.42f), count,
            new TextStyle(FontRole.CaptionStrong, Vector4.One, TextAlign.Center));
        return frame.Input.ConsumeClick(area);
    }

    public static void PostGlyph(in AppletFrame frame, Rect area, string file, string count, bool on,
        NightPalette tone, string fallback)
    {
        var ink = on ? tone.Accent : tone.Mute;
        var icon = area.LeftSlice(area.Height).Inset(frame.Units(2f));
        ActionGlyph(frame, icon, file, ink, fallback, FontRole.Caption);
        if (count.Length > 0)
        {
            frame.Text.DrawIn(area.Inset(new Edges(area.Height, 0f, 0f, 0f)), count,
                new TextStyle(FontRole.Caption, ink, TextAlign.Left));
        }
    }

    private static void ActionGlyph(in AppletFrame frame, Rect area, string file, Vector4 ink, string fallback,
        FontRole role)
    {
        var icon = CoverFit.InscribedSquare(area);
        if (TryPacked(frame, icon, file, ink))
        {
            return;
        }

        frame.Text.DrawIn(icon, fallback, new TextStyle(role, ink, TextAlign.Center));
    }

    public static bool CheckRow(in AppletFrame frame, Rect area, string label, bool on, bool plus,
        bool hit = true)
    {
        var mark = plus ? PlusViolet : Night.Accent;
        var box = Rect.FromSize(new Vector2(area.Min.X, area.Center.Y - frame.Units(8f)),
            new Vector2(frame.Units(16f), frame.Units(16f)));
        frame.Paint.Stroke(box, on ? mark : new Vector4(1f, 1f, 1f, 0.35f), frame.Units(1.4f), frame.Units(3f));
        if (on)
        {
            frame.Paint.Fill(box.Inset(frame.Units(3.2f)), mark, frame.Units(2f));
        }

        frame.Text.DrawIn(area.Inset(new Edges(frame.Units(24f), 0f, 0f, 0f)), label,
            new TextStyle(FontRole.BodyStrong, Vector4.One));
        return hit && frame.Input.ConsumeClick(area);
    }

    public static bool CheckWrap(in AppletFrame frame, Rect area, string label, bool on, bool plus)
    {
        var mark = plus ? PlusViolet : Night.Accent;
        var box = Rect.FromSize(new Vector2(area.Min.X, area.Min.Y + frame.Units(2f)),
            new Vector2(frame.Units(16f), frame.Units(16f)));
        frame.Paint.Stroke(box, on ? mark : new Vector4(1f, 1f, 1f, 0.35f), frame.Units(1.4f), frame.Units(3f));
        if (on)
        {
            frame.Paint.Fill(box.Inset(frame.Units(3.2f)), mark, frame.Units(2f));
        }

        frame.Text.DrawWrapped(area.Inset(new Edges(frame.Units(24f), 0f, 0f, 0f)), label,
            new TextStyle(FontRole.CaptionStrong, Vector4.One));
        return frame.Input.ConsumeClick(area);
    }

    public static void PhotoMark(in AppletFrame frame, Rect area, Vector4 ink, bool on)
    {
        var mark = on ? Night.Accent : ink;
        var box = CoverFit.InscribedSquare(area.Inset(area.Height * 0.18f));
        if (TryGlyph(frame, box, "vybe-gallery.png", mark))
        {
            return;
        }

        var plate = box.Inset(box.Height * 0.08f);
        frame.Paint.Stroke(plate, mark, MathF.Max(1.4f, frame.Units(1.4f)), frame.Units(3f));
        frame.Paint.StrokeCircle(plate.Center + new Vector2(plate.Width * 0.12f, -plate.Height * 0.06f),
            plate.Width * 0.16f, mark, MathF.Max(1.2f, frame.Units(1.2f)));
    }

    public static int LaneTrack(in AppletFrame frame, Rect area, bool plus)
    {
        var tone = Tone(plus);
        var radius = area.Height * 0.5f;
        frame.Paint.Fill(area, new Vector4(0f, 0f, 0f, 0.42f), radius);
        frame.Paint.Stroke(area, new Vector4(1f, 1f, 1f, 0.08f), frame.Units(1f), radius);
        var inner = area.Inset(frame.Units(3f));
        var thumb = plus ? inner.RightSlice(inner.Width * 0.5f) : inner.LeftSlice(inner.Width * 0.5f);
        frame.Paint.Fill(thumb, plus ? PlusViolet : Night.Accent, thumb.Height * 0.5f);
        var day = inner.LeftSlice(inner.Width * 0.5f);
        var dusk = inner.RightSlice(inner.Width * 0.5f);
        frame.Text.DrawIn(day, "VYBE",
            new TextStyle(FontRole.CaptionStrong, plus ? tone.Mute : Vector4.One, TextAlign.Center));
        frame.Text.DrawIn(dusk, "VYBE+",
            new TextStyle(FontRole.CaptionStrong, plus ? Vector4.One : tone.Mute, TextAlign.Center));
        if (frame.Input.ConsumeClick(day))
        {
            return 0;
        }

        if (frame.Input.ConsumeClick(dusk))
        {
            return 1;
        }

        return -1;
    }

    public static void WashFill(in AppletFrame frame, Rect area, float radius)
    {
        frame.Paint.FillSquircleGradient(area, Night.Accent, PlusViolet, PlusViolet, Night.Accent, radius);
    }

    public static void Glow(in AppletFrame frame, Rect area, float radius, bool on, bool night)
    {
        var tone = Tone(night);
        if (on)
        {
            WashFill(frame, area, radius);
        }
        else
        {
            frame.Paint.Fill(area, tone.Card, radius);
            frame.Paint.Stroke(area, tone.Faint, frame.Units(1.2f), radius);
        }
    }

    public static void Primary(in AppletFrame frame, Rect area, string label, bool night)
    {
        _ = night;
        WashFill(frame, area, frame.Units(12f));
        frame.Text.DrawIn(area, label, new TextStyle(FontRole.BodyStrong, Vector4.One, TextAlign.Center));
    }

    public static void PlusButton(in AppletFrame frame, Rect area, string label, bool enabled)
    {
        var radius = area.Height * 0.5f;
        if (enabled)
        {
            WashFill(frame, area, radius);
        }
        else
        {
            frame.Paint.Fill(area, Night.CardHi, radius);
        }

        frame.Text.DrawIn(area, label,
            new TextStyle(FontRole.BodyStrong, enabled ? Vector4.One : Night.Mute, TextAlign.Center));
    }

    public static void Brand(in AppletFrame frame, Rect area, bool plus)
    {
        var tone = Tone(plus);
        var word = "VYBE";
        var wide = frame.Text.Measure(word, FontRole.Title).X;
        frame.Text.DrawIn(area, word, new TextStyle(FontRole.Title, tone.Ink));
        if (!plus)
        {
            return;
        }

        var mark = Rect.FromSize(new Vector2(area.Min.X + wide + frame.Units(3f), area.Min.Y),
            new Vector2(frame.Units(18f), area.Height));
        frame.Text.DrawIn(mark, "+", new TextStyle(FontRole.Title, tone.Accent, TextAlign.Center));
    }

    public static float PlusTagWidth(in AppletFrame frame) =>
        frame.Text.Measure("VYBE+", FontRole.CaptionStrong).X + frame.Units(12f);

    public static void PlusTag(in AppletFrame frame, Rect area)
    {
        if (area.Width < frame.Units(8f) || area.Height < frame.Units(8f))
        {
            return;
        }

        WashFill(frame, area, area.Height * 0.5f);
        frame.Text.DrawIn(area, "VYBE+",
            new TextStyle(FontRole.CaptionStrong, Vector4.One, TextAlign.Center));
    }

    public static bool Pill(in AppletFrame frame, Rect area, string label, bool on, bool night)
    {
        var tone = Tone(night);
        var radius = area.Height * 0.5f;
        if (on)
        {
            WashFill(frame, area, radius);
        }
        else
        {
            frame.Paint.Fill(area, tone.CardHi, radius);
        }

        frame.Text.DrawEllipsized(area.Inset(new Edges(frame.Units(4f), 0f)), label,
            new TextStyle(FontRole.CaptionStrong, on ? Vector4.One : tone.Mute, TextAlign.Center));
        return frame.Input.ConsumeClick(area);
    }

    public static int ModeSlider(in AppletFrame frame, Rect area, bool plus)
    {
        var tone = Tone(plus);
        var radius = area.Height * 0.5f;
        frame.Paint.Fill(area, new Vector4(1f, 1f, 1f, 0.05f), radius);
        frame.Paint.Stroke(area, new Vector4(1f, 1f, 1f, 0.10f), frame.Units(1.1f), radius);
        var pad = frame.Units(3f);
        var inner = area.Inset(pad);
        var thumb = (plus ? inner.RightSlice(inner.Width * 0.5f) : inner.LeftSlice(inner.Width * 0.5f));
        WashFill(frame, thumb, thumb.Height * 0.5f);
        var day = inner.LeftSlice(inner.Width * 0.5f);
        var night = inner.RightSlice(inner.Width * 0.5f);
        frame.Text.DrawIn(day, "VYBE",
            new TextStyle(FontRole.CaptionStrong, plus ? tone.Mute : Vector4.One, TextAlign.Center));
        frame.Text.DrawIn(night, "VYBE+",
            new TextStyle(FontRole.CaptionStrong, plus ? Vector4.One : tone.Mute, TextAlign.Center));
        if (frame.Input.ConsumeClick(day))
        {
            return 0;
        }

        if (frame.Input.ConsumeClick(night))
        {
            return 1;
        }

        return -1;
    }

    public static bool Segment(in AppletFrame frame, Rect area, string label, bool on, bool night)
    {
        var tone = Tone(night);
        frame.Text.DrawIn(area, label,
            new TextStyle(on ? FontRole.BodyStrong : FontRole.Caption, on ? tone.Ink : tone.Mute, TextAlign.Center));
        if (on)
        {
            var wide = frame.Text.Measure(label, FontRole.BodyStrong).X;
            var bar = area.BottomSlice(frame.Units(2.6f));
            var inset = MathF.Max(0f, (bar.Width - wide) * 0.5f);
            frame.Paint.Fill(bar.Inset(new Edges(inset, 0f)), tone.Accent, frame.Units(1.2f));
        }

        return frame.Input.ConsumeClick(area);
    }

    public static int ProfileMarks(in AppletFrame frame, Rect area, int selected, bool night)
    {
        var tone = Tone(night);
        var count = 4;
        var cellW = area.Width / count;
        var mark = frame.Units(22f);
        for (var index = 0; index < count; index++)
        {
            var cell = Rect.FromSize(new Vector2(area.Min.X + cellW * index, area.Min.Y),
                new Vector2(cellW, area.Height));
            var on = selected == index;
            var ink = on ? tone.Accent : tone.Mute;
            var glyph = Rect.FromSize(cell.Center - new Vector2(mark * 0.5f, mark * 0.5f + frame.Units(1f)),
                new Vector2(mark, mark));
            DrawProfileMark(frame, glyph, index, ink);
            if (on)
            {
                var bar = cell.BottomSlice(frame.Units(2.2f));
                var inset = MathF.Max(0f, (bar.Width - mark) * 0.5f);
                frame.Paint.Fill(bar.Inset(new Edges(inset, 0f)), tone.Accent, frame.Units(1.1f));
            }

            if (frame.Input.ConsumeClick(cell))
            {
                selected = index;
            }
        }

        return selected;
    }

    private static void DrawProfileMark(in AppletFrame frame, Rect area, int mark, Vector4 ink)
    {
        switch (mark)
        {
            case 1:
                DrawGridMark(frame, area, ink);
                break;
            case 2:
                DrawGroupMark(frame, area, ink);
                break;
            case 3:
                DrawFavoriteMark(frame, area, ink);
                break;
            default:
                DrawListMark(frame, area, ink);
                break;
        }
    }

    private static void DrawListMark(in AppletFrame frame, Rect area, Vector4 ink)
    {
        area = area.Inset(area.Height * 0.10f);
        var stroke = MathF.Max(1.3f, area.Height * 0.10f);
        var pad = area.Width * 0.08f;
        var row = area.Height / 3.4f;
        for (var index = 0; index < 3; index++)
        {
            var y = area.Min.Y + area.Height * 0.18f + row * index;
            var dot = new Vector2(area.Min.X + pad + stroke, y);
            frame.Paint.FillCircle(dot, stroke * 0.72f, ink);
            frame.Paint.Line(new Vector2(area.Min.X + pad + stroke * 3.2f, y),
                new Vector2(area.Max.X - pad, y), ink, stroke);
        }
    }

    private static void DrawGridMark(in AppletFrame frame, Rect area, Vector4 ink)
    {
        area = area.Inset(area.Height * 0.10f);
        var gap = area.Width * 0.12f;
        var cell = (area.Width - gap) * 0.5f;
        var radius = MathF.Max(1.4f, cell * 0.16f);
        var stroke = MathF.Max(1.2f, cell * 0.08f);
        for (var row = 0; row < 2; row++)
        {
            for (var col = 0; col < 2; col++)
            {
                var box = Rect.FromSize(
                    new Vector2(area.Min.X + col * (cell + gap), area.Min.Y + row * (cell + gap)),
                    new Vector2(cell, cell));
                frame.Paint.Stroke(box, ink, stroke, radius);
            }
        }
    }

    private static void DrawGroupMark(in AppletFrame frame, Rect area, Vector4 ink)
    {
        area = area.Inset(area.Height * 0.10f);
        var stroke = MathF.Max(1.3f, area.Height * 0.10f);
        var back = area.Center + new Vector2(area.Width * 0.16f, -area.Height * 0.08f);
        var front = area.Center + new Vector2(-area.Width * 0.12f, 0f);
        var head = MathF.Min(area.Width, area.Height) * 0.16f;
        frame.Paint.StrokeCircle(back + new Vector2(0f, -area.Height * 0.18f), head * 0.85f, ink, stroke);
        frame.Paint.StrokeCircle(front + new Vector2(0f, -area.Height * 0.16f), head, ink, stroke);
        frame.Paint.Stroke(
            Rect.FromSize(back + new Vector2(-head * 1.15f, head * 0.2f), new Vector2(head * 2.3f, head * 1.55f)),
            ink, stroke, head);
        frame.Paint.Stroke(
            Rect.FromSize(front + new Vector2(-head * 1.35f, head * 0.35f), new Vector2(head * 2.7f, head * 1.7f)),
            ink, stroke, head);
    }

    private static void DrawFavoriteMark(in AppletFrame frame, Rect area, Vector4 ink)
    {
        if (TryPacked(frame, area, RepostGlyph, ink))
        {
            return;
        }

        DrawSolidRepost(frame, area, ink);
    }

    private static void DrawSolidRepost(in AppletFrame frame, Rect area, Vector4 ink)
    {
        var stroke = MathF.Max(2.2f, area.Height * 0.18f);
        var box = area.Inset(area.Height * 0.10f);
        var top = box.Min.Y + box.Height * 0.30f;
        var bot = box.Max.Y - box.Height * 0.30f;
        var left = box.Min.X + box.Width * 0.08f;
        var right = box.Max.X - box.Width * 0.08f;
        var mid = box.Width * 0.22f;
        frame.Paint.Line(new Vector2(left, top + box.Height * 0.18f), new Vector2(left, top), ink, stroke);
        frame.Paint.Line(new Vector2(left, top), new Vector2(right - mid, top), ink, stroke);
        frame.Paint.Line(new Vector2(right - mid * 1.15f, top - box.Height * 0.12f), new Vector2(right, top), ink,
            stroke);
        frame.Paint.Line(new Vector2(right - mid * 1.15f, top + box.Height * 0.12f), new Vector2(right, top), ink,
            stroke);
        frame.Paint.Line(new Vector2(right, bot - box.Height * 0.18f), new Vector2(right, bot), ink, stroke);
        frame.Paint.Line(new Vector2(right, bot), new Vector2(left + mid, bot), ink, stroke);
        frame.Paint.Line(new Vector2(left + mid * 1.15f, bot - box.Height * 0.12f), new Vector2(left, bot), ink,
            stroke);
        frame.Paint.Line(new Vector2(left + mid * 1.15f, bot + box.Height * 0.12f), new Vector2(left, bot), ink,
            stroke);
    }

    public static int TextTabs(in AppletFrame frame, Rect area, string[] labels, int selected, bool night)
    {
        var tone = Tone(night);
        var cellW = area.Width / Math.Max(labels.Length, 1);
        for (var index = 0; index < labels.Length; index++)
        {
            var on = selected == index;
            var cell = Rect.FromSize(new Vector2(area.Min.X + cellW * index, area.Min.Y),
                new Vector2(cellW, area.Height));
            var role = on ? FontRole.BodyStrong : FontRole.Caption;
            frame.Text.DrawIn(cell.TopSlice(area.Height - frame.Units(4f)), labels[index],
                new TextStyle(role, on ? tone.Ink : tone.Mute, TextAlign.Center));
            if (on)
            {
                var wide = MathF.Min(cell.Width - frame.Units(8f),
                    frame.Text.Measure(labels[index], FontRole.BodyStrong).X);
                var bar = cell.BottomSlice(frame.Units(2.6f));
                var inset = MathF.Max(0f, (bar.Width - wide) * 0.5f);
                frame.Paint.Fill(bar.Inset(new Edges(inset, 0f)), tone.Accent, frame.Units(1.2f));
            }

            if (frame.Input.ConsumeClick(cell))
            {
                selected = index;
            }
        }

        return selected;
    }

    public static bool Chip(in AppletFrame frame, Rect area, string label, bool on, bool night)
    {
        var tone = Tone(night);
        var radius = frame.Units(12f);
        if (on)
        {
            WashFill(frame, area, radius);
        }
        else
        {
            frame.Paint.Fill(area, tone.CardHi, radius);
        }

        frame.Text.DrawIn(area, label,
            new TextStyle(FontRole.CaptionStrong, on ? Vector4.One : tone.Mute, TextAlign.Center));
        return frame.Input.ConsumeClick(area);
    }

    public static bool Tag(in AppletFrame frame, Rect area, string label, bool on, bool night)
    {
        var tone = Tone(night);
        var radius = area.Height * 0.5f;
        if (on)
        {
            frame.Paint.Fill(area, tone.Accent with { W = 0.22f }, radius);
            frame.Paint.Stroke(area, tone.Accent with { W = 0.88f }, frame.Units(1.2f), radius);
        }
        else
        {
            frame.Paint.Fill(area, new Vector4(1f, 1f, 1f, 0.05f), radius);
            frame.Paint.Stroke(area, new Vector4(1f, 1f, 1f, 0.12f), frame.Units(1f), radius);
        }

        frame.Text.DrawIn(area.Inset(new Edges(frame.Units(10f), 0f)), label,
            new TextStyle(FontRole.CaptionStrong, on ? Vector4.One : tone.Ink with { W = 0.78f }, TextAlign.Center));
        return frame.Input.ConsumeClick(area);
    }

    public static void FlowMany(in AppletFrame frame, ref Stack stack, string[] options, List<string> selected,
        bool night)
    {
        var area = stack.Take(FlowHeight(frame, stack.Remaining.Width, options));
        var gap = frame.Units(6f);
        var h = frame.Units(28f);
        var x = 0f;
        var y = 0f;
        for (var index = 0; index < options.Length; index++)
        {
            var tag = options[index];
            var cell = NextTag(frame, area, tag, ref x, ref y, gap, h);
            if (Tag(frame, cell, tag, selected.Contains(tag), night) && !selected.Remove(tag))
            {
                selected.Add(tag);
            }
        }
    }

    public static void FlowOne(in AppletFrame frame, ref Stack stack, string[] options, string current,
        Action<string> pick, bool night)
    {
        var area = stack.Take(FlowHeight(frame, stack.Remaining.Width, options));
        var gap = frame.Units(6f);
        var h = frame.Units(28f);
        var x = 0f;
        var y = 0f;
        for (var index = 0; index < options.Length; index++)
        {
            var tag = options[index];
            var cell = NextTag(frame, area, tag, ref x, ref y, gap, h);
            if (Tag(frame, cell, tag, string.Equals(current, tag, StringComparison.Ordinal), night))
            {
                pick(tag);
            }
        }
    }

    public static int Pair(in AppletFrame frame, Rect area, string left, string right, int selected, bool night)
    {
        var tone = Tone(night);
        var radius = area.Height * 0.5f;
        frame.Paint.Fill(area, new Vector4(1f, 1f, 1f, 0.05f), radius);
        frame.Paint.Stroke(area, new Vector4(1f, 1f, 1f, 0.10f), frame.Units(1.1f), radius);
        var inner = area.Inset(frame.Units(3f));
        var thumb = selected == 0 ? inner.LeftSlice(inner.Width * 0.5f) : inner.RightSlice(inner.Width * 0.5f);
        frame.Paint.Fill(thumb, tone.Accent with { W = 0.26f }, thumb.Height * 0.5f);
        frame.Paint.Stroke(thumb, tone.Accent with { W = 0.86f }, frame.Units(1.1f), thumb.Height * 0.5f);
        var leftHit = inner.LeftSlice(inner.Width * 0.5f);
        var rightHit = inner.RightSlice(inner.Width * 0.5f);
        frame.Text.DrawIn(leftHit, left,
            new TextStyle(FontRole.CaptionStrong, selected == 0 ? Vector4.One : tone.Mute, TextAlign.Center));
        frame.Text.DrawIn(rightHit, right,
            new TextStyle(FontRole.CaptionStrong, selected == 1 ? Vector4.One : tone.Mute, TextAlign.Center));
        if (frame.Input.ConsumeClick(leftHit))
        {
            return 0;
        }

        if (frame.Input.ConsumeClick(rightHit))
        {
            return 1;
        }

        return -1;
    }

    private static float TagWidth(in AppletFrame frame, string label) =>
        frame.Text.Measure(label, FontRole.CaptionStrong).X + frame.Units(22f);

    private static float FlowHeight(in AppletFrame frame, float width, string[] options)
    {
        var area = Rect.FromSize(Vector2.Zero, new Vector2(MathF.Max(width, 1f), 1f));
        var gap = frame.Units(6f);
        var h = frame.Units(28f);
        var x = 0f;
        var y = 0f;
        var last = h;
        for (var index = 0; index < options.Length; index++)
        {
            var cell = NextTag(frame, area, options[index], ref x, ref y, gap, h);
            last = cell.Max.Y;
        }

        return MathF.Max(h, last);
    }

    private static Rect NextTag(in AppletFrame frame, Rect area, string label, ref float x, ref float y, float gap,
        float h)
    {
        var w = Math.Clamp(TagWidth(frame, label), frame.Units(32f), MathF.Max(area.Width, 1f));
        if (x > 0f && x + w > area.Width + 0.5f)
        {
            x = 0f;
            y += h + gap;
        }

        var cell = Rect.FromSize(new Vector2(area.Min.X + x, area.Min.Y + y), new Vector2(w, h));
        x += w + gap;
        return cell;
    }

    public static void Kicker(in AppletFrame frame, Rect area, string label, bool night) =>
        frame.Text.DrawIn(area, label, new TextStyle(FontRole.CaptionStrong, Tone(night).Accent));

    public static void Title(in AppletFrame frame, Rect area, string label, bool night) =>
        frame.Text.DrawEllipsized(area, label, new TextStyle(FontRole.Title, Tone(night).Ink));

    public static void Mute(in AppletFrame frame, Rect area, string label, bool night) =>
        frame.Text.DrawEllipsized(area, label, new TextStyle(FontRole.Caption, Tone(night).Mute));

    public static void Note(in AppletFrame frame, Rect area, string label, bool night) =>
        frame.Text.DrawWrapped(area, label, new TextStyle(FontRole.Caption, Tone(night).Ink));

    public static Rect Wordmark(in AppletFrame frame, Rect area, bool night, float hold)
    {
        var tone = Tone(night);
        var mark = area.LeftSlice(frame.Units(28f));
        var progress = Math.Clamp(hold / HoldSeconds, 0f, 1f);
        if (progress > 0f)
        {
            frame.Paint.StrokeCircle(mark.Center, frame.Units(12f), tone.Accent with { W = 0.25f + progress * 0.75f },
                frame.Units(2.2f));
        }

        if (night)
        {
            frame.Paint.StrokeCircle(mark.Center + new Vector2(frame.Units(2f), 0f), frame.Units(7f), tone.Accent,
                frame.Units(1.6f));
            frame.Paint.StrokeCircle(mark.Center + new Vector2(frame.Units(6f), -frame.Units(2f)), frame.Units(5f),
                tone.Accent with { W = 0.35f }, frame.Units(1.4f));
        }
        else
        {
            frame.Paint.StrokeCircle(mark.Center, frame.Units(5f), tone.Accent, frame.Units(1.6f));
            for (var ray = 0; ray < 6; ray++)
            {
                var angle = ray * MathF.PI / 3f;
                var dir = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
                frame.Paint.Line(mark.Center + dir * frame.Units(8f), mark.Center + dir * frame.Units(11f), tone.Accent,
                    frame.Units(1.4f));
            }
        }

        Brand(frame, area.Inset(new Edges(frame.Units(28f), 0f, 0f, 0f)), night);
        return mark;
    }

    public static void StackedMark(in AppletFrame frame, Rect area, bool night)
    {
        var tone = Tone(night);
        var top = area.TopSlice(area.Height * 0.48f);
        var bot = area.BottomSlice(area.Height * 0.52f);
        frame.Text.DrawIn(top, night ? "AFTER" : "VY",
            new TextStyle(FontRole.Display, tone.Ink, TextAlign.Center));
        frame.Text.DrawIn(bot, night ? "DARK" : "BE",
            new TextStyle(FontRole.Display, tone.Accent, TextAlign.Center));
    }

    public static bool Back(in AppletFrame frame, Rect row, string label, bool night)
    {
        var tone = Tone(night);
        var hit = row.LeftSlice(frame.Units(28f));
        frame.Text.DrawIn(hit, "‹", new TextStyle(FontRole.Title, tone.Accent, TextAlign.Center));
        frame.Text.DrawEllipsized(row.Inset(new Edges(frame.Units(28f), 0f, 0f, 0f)), label,
            new TextStyle(FontRole.BodyStrong, tone.Ink));
        return frame.Input.ConsumeClick(row.LeftSlice(frame.Units(90f)));
    }

    public static void Wheel(in AppletFrame frame, Rect area, VybeState state, float content)
    {
        var max = MathF.Max(0f, content - area.Height);
        var next = state.Scroll;
        ScrollSlider.Steer(frame, area, ref next, max);
        state.Scroll = Math.Clamp(next, 0f, max);
    }

    public static void ReportFlag(in AppletFrame frame, Rect area, Vector4 ink)
    {
        if (TryPacked(frame, CoverFit.InscribedSquare(area), "music-report.png", ink) ||
            TryGlyph(frame, area, "music-report.png", ink))
        {
            return;
        }

        frame.Text.DrawIn(area, "⚑", new TextStyle(FontRole.Title, ink, TextAlign.Center));
    }

    public static void ReportTick(in AppletFrame frame, Rect area, Vector4 ink)
    {
        if (TryPacked(frame, CoverFit.InscribedSquare(area.Inset(area.Height * 0.12f)), "music-report.png", ink))
        {
            return;
        }

        frame.Text.DrawIn(area, "⚑", new TextStyle(FontRole.Title, ink, TextAlign.Center));
    }

    public static void SaveMark(in AppletFrame frame, Rect area, Vector4 ink)
    {
        var mark = ToolInk(frame, area, ink);
        // Bookmark art already has canvas padding; use a larger box so the mark
        // matches home_edit's on-screen size while staying inside the disk.
        var box = CoverFit.InscribedSquare(area.Inset(area.Height * 0.16f));
        if (TryPacked(frame, box, "vybe-save.png", mark))
        {
            return;
        }

        BookmarkShape(frame, ToolGlyphBox(area), mark);
    }

    public static void SaveTick(in AppletFrame frame, Rect area, Vector4 ink, bool on)
    {
        var tone = on ? new Vector4(1f, 0.84f, 0.28f, 1f) : ink;
        if (TryPacked(frame, CoverFit.InscribedSquare(area.Inset(area.Height * 0.12f)), "vybe-save.png", tone))
        {
            return;
        }

        BookmarkShape(frame, CoverFit.InscribedSquare(area.Inset(area.Height * 0.18f)), tone);
    }

    public static void EditMark(in AppletFrame frame, Rect area, Vector4 ink)
    {
        var mark = ToolInk(frame, area, ink);
        var box = ToolGlyphBox(area);
        if (TryPacked(frame, box, AppIconCatalog.HomeEditAsset, mark))
        {
            return;
        }

        var c = box.Center;
        var s = MathF.Min(box.Width, box.Height) * 0.38f;
        var stroke = MathF.Max(1.6f, s * 0.22f);
        frame.Paint.Line(c + new Vector2(-s * 0.55f, s * 0.55f), c + new Vector2(s * 0.28f, -s * 0.28f), mark, stroke);
        frame.Paint.Line(c + new Vector2(s * 0.12f, -s * 0.44f), c + new Vector2(s * 0.44f, -s * 0.12f), mark, stroke);
        frame.Paint.Line(c + new Vector2(-s * 0.62f, s * 0.22f), c + new Vector2(-s * 0.22f, s * 0.62f), mark, stroke);
    }

    private static Vector4 ToolInk(in AppletFrame frame, Rect area, Vector4 ink)
    {
        var hover = frame.Input.IsHovering(area);
        ToolDisk(frame, area, hover);
        return hover ? new Vector4(1f, 1f, 1f, 1f) : ink;
    }

    private static void ToolDisk(in AppletFrame frame, Rect area, bool hover)
    {
        var radius = MathF.Min(area.Width, area.Height) * 0.5f;
        var fill = hover ? new Vector4(0.16f, 0.16f, 0.18f, 0.72f) : new Vector4(0f, 0f, 0f, 0.38f);
        frame.Paint.FillCircle(area.Center, radius, fill);
        if (hover)
        {
            frame.Paint.StrokeCircle(area.Center, radius - 0.6f, new Vector4(1f, 1f, 1f, 0.62f),
                MathF.Max(1.2f, frame.Units(1.2f)));
        }
    }

    private static Rect ToolGlyphBox(Rect area) =>
        CoverFit.InscribedSquare(area.Inset(area.Height * 0.30f));

    private static bool TryGlyph(in AppletFrame frame, Rect area, string file, Vector4 ink) =>
        TryFile(frame, area, AppIconCatalog.Glyph(frame.Paths, file), ink);

    private static bool TryPacked(in AppletFrame frame, Rect area, string file, Vector4 ink) =>
        TryFile(frame, area, AppIconCatalog.Glyph(frame.Paths, file), ink) ||
        TryFile(frame, area, AppIconCatalog.Absolute(frame.Paths, file), ink) ||
        TryFile(frame, area, AppIconCatalog.Original(frame.Paths, file), ink);

    private static bool TryFile(in AppletFrame frame, Rect area, string path, Vector4 ink)
    {
        if (path.Length == 0 || !File.Exists(path))
        {
            return false;
        }

        var texture = frame.Textures.FromFile(path);
        if (texture is not { IsReady: true } || texture.Handle == 0)
        {
            return false;
        }

        var dest = CoverFit.Contained(texture.Size, CoverFit.InscribedSquare(area));
        if (dest.IsEmpty)
        {
            return false;
        }

        frame.Paint.Image(texture, dest, ink);
        return true;
    }

    private static void BookmarkShape(in AppletFrame frame, Rect area, Vector4 ink)
    {
        var w = MathF.Min(area.Width, area.Height) * 0.42f;
        var h = w * 1.35f;
        var c = area.Center;
        var top = c + new Vector2(0f, -h * 0.48f);
        var left = c + new Vector2(-w * 0.5f, -h * 0.48f);
        var right = c + new Vector2(w * 0.5f, -h * 0.48f);
        var botL = c + new Vector2(-w * 0.5f, h * 0.52f);
        var botR = c + new Vector2(w * 0.5f, h * 0.52f);
        var notch = c + new Vector2(0f, h * 0.12f);
        var stroke = MathF.Max(1.6f, w * 0.18f);
        frame.Paint.Line(left, right, ink, stroke);
        frame.Paint.Line(right, botR, ink, stroke);
        frame.Paint.Line(botR, notch, ink, stroke);
        frame.Paint.Line(notch, botL, ink, stroke);
        frame.Paint.Line(botL, left, ink, stroke);
        _ = top;
    }

    public static void Portrait(in AppletFrame frame, Vector2 center, float radius, Vector4 fill, bool night)
    {
        frame.Paint.FillCircle(center, radius, fill);
        frame.Paint.StrokeCircle(center, radius, Tone(night).Accent with { W = 0.85f }, MathF.Max(1.4f, radius * 0.08f));
    }

    public static void StoryRing(in AppletFrame frame, Vector2 center, float radius, bool seen, bool night)
    {
        var tone = Tone(night);
        frame.Paint.StrokeCircle(center, radius + frame.Units(3f), seen ? tone.Mute : tone.Accent, frame.Units(2f));
    }

    public static void Bubble(in AppletFrame frame, Rect area, string body, bool mine, bool night)
    {
        var tone = Tone(night);
        var fill = mine ? tone.Accent : tone.CardHi;
        var ink = mine ? tone.AccentInk : tone.Ink;
        frame.Paint.Fill(area, fill, frame.Units(12f));
        frame.Text.DrawWrapped(area.Inset(frame.Units(8f)), body, new TextStyle(FontRole.Caption, ink));
    }
}
