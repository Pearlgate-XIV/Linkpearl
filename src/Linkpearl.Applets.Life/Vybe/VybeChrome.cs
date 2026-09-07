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

    public static void Glow(in AppletFrame frame, Rect area, float radius, bool on, bool night)
    {
        var tone = Tone(night);
        frame.Paint.Fill(area, on ? tone.AccentDim : tone.Card, radius);
        frame.Paint.Stroke(area, on ? tone.Accent : tone.Faint, frame.Units(1.2f), radius);
    }

    public static void Primary(in AppletFrame frame, Rect area, string label, bool night)
    {
        var tone = Tone(night);
        frame.Paint.Fill(area, tone.Accent, frame.Units(12f));
        frame.Text.DrawIn(area, label, new TextStyle(FontRole.BodyStrong, tone.AccentInk, TextAlign.Center));
    }

    public static bool Pill(in AppletFrame frame, Rect area, string label, bool on, bool night)
    {
        var tone = Tone(night);
        frame.Paint.Fill(area, on ? tone.Accent : tone.CardHi, area.Height * 0.5f);
        frame.Text.DrawEllipsized(area.Inset(new Edges(frame.Units(4f), 0f)), label,
            new TextStyle(FontRole.CaptionStrong, on ? tone.AccentInk : tone.Mute, TextAlign.Center));
        return frame.Input.ConsumeClick(area);
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
        for (var index = 0; index < count; index++)
        {
            var cell = Rect.FromSize(new Vector2(area.Min.X + cellW * index, area.Min.Y),
                new Vector2(cellW, area.Height));
            var hover = frame.Input.IsHovering(cell);
            var on = selected == index;
            var ink = on ? tone.Accent : hover ? tone.Ink : tone.Mute;
            var glyph = CoverFit.InscribedSquare(cell.TopSlice(area.Height - frame.Units(6f)).Inset(frame.Units(8f)));
            DrawProfileMark(frame, glyph, index, ink);
            if (on)
            {
                var bar = cell.BottomSlice(frame.Units(2.2f));
                var inset = MathF.Max(0f, (bar.Width - frame.Units(22f)) * 0.5f);
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
                DrawStarMark(frame, area, ink);
                break;
            default:
                DrawListMark(frame, area, ink);
                break;
        }
    }

    private static void DrawListMark(in AppletFrame frame, Rect area, Vector4 ink)
    {
        var stroke = MathF.Max(1.3f, area.Height * 0.08f);
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
        var stroke = MathF.Max(1.3f, area.Height * 0.08f);
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

    private static void DrawStarMark(in AppletFrame frame, Rect area, Vector4 ink)
    {
        var stroke = MathF.Max(1.3f, area.Height * 0.08f);
        var c = area.Center;
        var outer = MathF.Min(area.Width, area.Height) * 0.48f;
        var inner = outer * 0.42f;
        var points = new Vector2[10];
        for (var index = 0; index < 10; index++)
        {
            var radius = index % 2 == 0 ? outer : inner;
            var angle = -MathF.PI * 0.5f + index * MathF.PI / 5f;
            points[index] = c + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * radius;
        }

        for (var index = 0; index < points.Length; index++)
        {
            frame.Paint.Line(points[index], points[(index + 1) % points.Length], ink, stroke);
        }
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
        frame.Paint.Fill(area, on ? tone.Accent : tone.CardHi, frame.Units(12f));
        frame.Text.DrawIn(area, label,
            new TextStyle(FontRole.CaptionStrong, on ? tone.AccentInk : tone.Mute, TextAlign.Center));
        return frame.Input.ConsumeClick(area);
    }

    public static void Kicker(in AppletFrame frame, Rect area, string label, bool night) =>
        frame.Text.DrawIn(area, label, new TextStyle(FontRole.CaptionStrong, Tone(night).Accent));

    public static void Title(in AppletFrame frame, Rect area, string label, bool night) =>
        frame.Text.DrawEllipsized(area, label, new TextStyle(FontRole.Title, Tone(night).Ink));

    public static void Mute(in AppletFrame frame, Rect area, string label, bool night) =>
        frame.Text.DrawEllipsized(area, label, new TextStyle(FontRole.Caption, Tone(night).Mute));

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

        frame.Text.DrawIn(area.Inset(new Edges(frame.Units(28f), 0f, 0f, 0f)), night ? "AFTERDARK" : "VYBE",
            new TextStyle(FontRole.CaptionStrong, tone.Accent));
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
        if (frame.Input.IsHovering(area) && frame.Input.ScrollDelta != 0f)
        {
            state.Scroll = Math.Clamp(state.Scroll - frame.Input.ScrollDelta * frame.Units(18f), 0f,
                MathF.Max(0f, content - area.Height));
        }
    }

    public static void ReportFlag(in AppletFrame frame, Rect area, Vector4 ink)
    {
        var mark = ToolInk(frame, area, ink);
        var box = ToolGlyphBox(area);
        if (TryGlyph(frame, box, "music-report.png", mark))
        {
            return;
        }

        frame.Text.DrawIn(box, "⚑", new TextStyle(FontRole.Title, mark, TextAlign.Center));
    }

    public static void EditMark(in AppletFrame frame, Rect area, Vector4 ink)
    {
        var mark = ToolInk(frame, area, ink);
        var box = ToolGlyphBox(area);
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
        CoverFit.InscribedSquare(area.Inset(area.Height * 0.22f));

    private static bool TryGlyph(in AppletFrame frame, Rect area, string file, Vector4 ink)
    {
        var texture = frame.Textures.FromFile(AppIconCatalog.Glyph(frame.Paths, file));
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
