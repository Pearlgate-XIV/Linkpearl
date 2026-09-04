using Linkpearl.Applets;
using Linkpearl.Geometry;
using Linkpearl.Layout;
using Linkpearl.Painting;

namespace Linkpearl.Applets.Life.AfterDark;

internal readonly record struct NightPalette(
    Vector4 Ground, Vector4 Card, Vector4 CardHi, Vector4 Accent, Vector4 AccentDim, Vector4 AccentInk, Vector4 Ink,
    Vector4 Mute, Vector4 Faint, Vector4 Danger);

internal static class AfterDarkChrome
{
    public const float WashSeconds = 0.4f;
    public const float HoldSeconds = 1f;

    public static readonly Vector4 Online = new(0.000f, 0.902f, 0.463f, 1f);

    public static readonly NightPalette Night = new(
        new Vector4(0.020f, 0.020f, 0.022f, 1f),
        new Vector4(0.071f, 0.071f, 0.075f, 1f),
        new Vector4(0.102f, 0.102f, 0.110f, 1f),
        new Vector4(1.000f, 0.000f, 0.498f, 1f),
        new Vector4(1.000f, 0.000f, 0.498f, 0.18f),
        new Vector4(1.000f, 1.000f, 1.000f, 1f),
        new Vector4(1.000f, 1.000f, 1.000f, 1f),
        new Vector4(0.533f, 0.533f, 0.545f, 1f),
        new Vector4(1.000f, 1.000f, 1.000f, 0.08f),
        new Vector4(0.910f, 0.361f, 0.400f, 1f));

    public static readonly NightPalette Day = new(
        new Vector4(0.780f, 0.745f, 0.690f, 1f),
        new Vector4(0.860f, 0.830f, 0.780f, 1f),
        new Vector4(0.810f, 0.780f, 0.730f, 1f),
        new Vector4(0.280f, 0.470f, 0.580f, 1f),
        new Vector4(0.280f, 0.470f, 0.580f, 0.18f),
        new Vector4(1.000f, 1.000f, 1.000f, 1f),
        new Vector4(0.145f, 0.133f, 0.122f, 1f),
        new Vector4(0.380f, 0.360f, 0.340f, 1f),
        new Vector4(0.120f, 0.110f, 0.100f, 0.20f),
        new Vector4(0.780f, 0.280f, 0.320f, 1f));

    public static NightPalette Tone(bool night) => night ? Night : Day;

    public static void Fill(in AppletFrame frame, bool night) =>
        frame.Paint.Fill(frame.Content, Tone(night).Ground);

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

        frame.Text.DrawIn(area.Inset(new Edges(frame.Units(28f), 0f, 0f, 0f)), night ? "AFTERDARK" : "DAYLIGHT",
            new TextStyle(FontRole.CaptionStrong, tone.Accent));
        return mark;
    }

    public static void StackedMark(in AppletFrame frame, Rect area, bool night)
    {
        var tone = Tone(night);
        var top = area.TopSlice(area.Height * 0.48f);
        var bot = area.BottomSlice(area.Height * 0.52f);
        frame.Text.DrawIn(top, night ? "AFTER" : "DAY",
            new TextStyle(FontRole.Display, tone.Ink, TextAlign.Center));
        frame.Text.DrawIn(bot, night ? "DARK" : "LIGHT",
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

    public static void Wheel(in AppletFrame frame, Rect area, AfterDarkState state, float content)
    {
        if (frame.Input.IsHovering(area) && frame.Input.ScrollDelta != 0f)
        {
            state.Scroll = Math.Clamp(state.Scroll - frame.Input.ScrollDelta * frame.Units(18f), 0f,
                MathF.Max(0f, content - area.Height));
        }
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
