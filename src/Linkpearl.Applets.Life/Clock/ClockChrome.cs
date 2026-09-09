using Linkpearl.Applets;
using Linkpearl.Geometry;
using Linkpearl.Painting;

namespace Linkpearl.Applets.Life.Clock;

internal static class ClockChrome
{
    public const float NavUnits = 62f;

    public static void Face(in AppletFrame frame, Vector2 center, float radius, float hour, float minute,
        float second, Vector4 ink, Vector4 hush, Vector4 accent, bool seconds)
    {
        if (radius < 8f)
        {
            return;
        }

        frame.Paint.FillCircle(center, radius, frame.Theme.Palette.SurfaceRaised with { W = 0.92f });
        frame.Paint.StrokeCircle(center, radius, hush with { W = 0.35f }, MathF.Max(1.1f, frame.Units(1.2f)));
        for (var tick = 0; tick < 60; tick++)
        {
            var hourTick = tick % 5 == 0;
            var angle = tick * (MathF.PI / 30f) - MathF.PI * 0.5f;
            var outer = radius * (hourTick ? 0.92f : 0.94f);
            var inner = radius * (hourTick ? 0.78f : 0.88f);
            var dir = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
            frame.Paint.Line(center + dir * inner, center + dir * outer,
                hourTick ? ink with { W = 0.88f } : hush with { W = 0.42f },
                hourTick ? MathF.Max(1.6f, radius * 0.028f) : MathF.Max(1f, radius * 0.014f));
        }

        if (radius >= 36f)
        {
            Mark(frame, center, radius, "12", new Vector2(0f, -radius * 0.62f), ink);
            Mark(frame, center, radius, "3", new Vector2(radius * 0.62f, 0f), ink);
            Mark(frame, center, radius, "6", new Vector2(0f, radius * 0.62f), ink);
            Mark(frame, center, radius, "9", new Vector2(-radius * 0.62f, 0f), ink);
        }

        Hand(frame, center, radius * 0.52f, hour % 12f * 30f, ink, radius * 0.045f);
        Hand(frame, center, radius * 0.74f, minute * 6f, ink, radius * 0.028f);
        if (seconds)
        {
            Hand(frame, center, radius * 0.80f, second * 6f, accent, MathF.Max(1.1f, radius * 0.016f));
        }

        frame.Paint.FillCircle(center, radius * 0.045f, accent);
        frame.Paint.FillCircle(center, radius * 0.018f, ink);
    }

    private static void Mark(in AppletFrame frame, Vector2 center, float radius, string label, Vector2 offset,
        Vector4 ink)
    {
        var box = radius * 0.22f;
        frame.Text.DrawIn(Rect.FromSize(center + offset - new Vector2(box * 0.5f), new Vector2(box)), label,
            new TextStyle(FontRole.CaptionStrong, ink, TextAlign.Center, scale: radius >= 70f ? 1f : 0.86f));
    }

    public static void MiniFace(in AppletFrame frame, Vector2 center, float radius, float hour, float minute,
        Vector4 ink, Vector4 hush)
    {
        if (radius < 6f)
        {
            return;
        }

        frame.Paint.StrokeCircle(center, radius, hush with { W = 0.55f }, MathF.Max(1.1f, frame.Units(1.1f)));
        for (var tick = 0; tick < 12; tick++)
        {
            var angle = tick * (MathF.PI / 6f) - MathF.PI * 0.5f;
            var dir = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
            frame.Paint.Line(center + dir * radius * 0.78f, center + dir * radius * 0.94f, hush,
                MathF.Max(1f, radius * 0.06f));
        }

        Hand(frame, center, radius * 0.50f, hour % 12f * 30f, ink, MathF.Max(1.4f, radius * 0.10f));
        Hand(frame, center, radius * 0.72f, minute * 6f, ink, MathF.Max(1.1f, radius * 0.07f));
        frame.Paint.FillCircle(center, MathF.Max(1.4f, radius * 0.10f), ink);
    }

    public static void Ring(in AppletFrame frame, Vector2 center, float radius, float amount, Vector4 track,
        Vector4 fill, float thickness)
    {
        frame.Paint.StrokeCircle(center, radius, track, thickness);
        var sweep = Math.Clamp(amount, 0f, 1f) * 360f;
        if (sweep <= 0.4f)
        {
            return;
        }

        Arc(frame, center, radius, -90f, -90f + sweep, fill, thickness);
    }

    public static void Arc(in AppletFrame frame, Vector2 center, float radius, float fromDeg, float toDeg,
        Vector4 color, float thickness)
    {
        var span = toDeg - fromDeg;
        if (MathF.Abs(span) < 0.2f || radius < 4f)
        {
            return;
        }

        var steps = Math.Clamp((int)(MathF.Abs(span) / 4f), 8, 90);
        var points = new Vector2[steps + 1];
        for (var index = 0; index <= steps; index++)
        {
            var deg = fromDeg + span * (index / (float)steps);
            var rad = deg * (MathF.PI / 180f);
            points[index] = center + new Vector2(MathF.Cos(rad), MathF.Sin(rad)) * radius;
        }

        frame.Paint.Polyline(points, color, thickness, false);
    }

    public static bool Switch(in AppletFrame frame, Rect area, bool on, Vector4 accent)
    {
        var h = MathF.Min(area.Height, frame.Units(28f));
        var w = MathF.Max(h * 1.72f, frame.Units(46f));
        var track = Rect.FromSize(new Vector2(area.Max.X - w, area.Center.Y - h * 0.5f), new Vector2(w, h));
        var fill = on ? accent : frame.Theme.Palette.SurfaceSunken;
        frame.Paint.Fill(track, fill, h * 0.5f);
        frame.Paint.Stroke(track, on ? accent : frame.Theme.Palette.Separator, frame.Units(1.1f), h * 0.5f);
        var pad = h * 0.14f;
        var thumbR = h * 0.36f;
        var thumbX = on ? track.Max.X - pad - thumbR : track.Min.X + pad + thumbR;
        frame.Paint.FillCircle(new Vector2(thumbX, track.Center.Y), thumbR, frame.Theme.Palette.AccentInk);
        return frame.Input.ConsumeClick(track);
    }

    public static bool Fab(in AppletFrame frame, Rect area, Vector4 fill, Vector4 ink)
    {
        var side = MathF.Min(area.Width, area.Height);
        var center = area.Center;
        var r = side * 0.5f;
        frame.Paint.FillCircle(center, r, fill);
        var arm = r * 0.36f;
        var bar = MathF.Max(1.8f, r * 0.12f);
        frame.Paint.Line(center + new Vector2(-arm, 0f), center + new Vector2(arm, 0f), ink, bar);
        frame.Paint.Line(center + new Vector2(0f, -arm), center + new Vector2(0f, arm), ink, bar);
        return frame.Input.ConsumeClick(Rect.FromSize(center - new Vector2(r), new Vector2(r * 2f)));
    }

    public static bool RoundAction(in AppletFrame frame, Rect area, string label, bool primary, Vector4 accent)
    {
        var side = MathF.Min(area.Width, area.Height);
        var center = area.Center;
        var r = side * 0.42f;
        if (primary)
        {
            frame.Paint.FillCircle(center, r, accent);
            frame.Text.DrawIn(area, label,
                new TextStyle(FontRole.CaptionStrong, frame.Theme.Palette.AccentInk, TextAlign.Center));
        }
        else
        {
            frame.Paint.StrokeCircle(center, r, frame.Theme.Palette.Separator, frame.Units(1.4f));
            frame.Text.DrawIn(area, label,
                new TextStyle(FontRole.CaptionStrong, frame.Theme.Palette.Ink, TextAlign.Center));
        }

        return frame.Input.ConsumeClick(Rect.FromSize(center - new Vector2(r), new Vector2(r * 2f)));
    }

    public static bool Chip(in AppletFrame frame, Rect area, string label, bool on, Vector4 accent)
    {
        var radius = area.Height * 0.5f;
        if (on)
        {
            frame.Paint.Fill(area, accent, radius);
            frame.Text.DrawIn(area, label,
                new TextStyle(FontRole.CaptionStrong, frame.Theme.Palette.AccentInk, TextAlign.Center));
        }
        else
        {
            frame.Paint.Fill(area, frame.Theme.Palette.SurfaceRaised, radius);
            frame.Paint.Stroke(area, frame.Theme.Palette.Separator, frame.Units(1f), radius);
            frame.Text.DrawIn(area, label,
                new TextStyle(FontRole.CaptionStrong, frame.Theme.Palette.Ink, TextAlign.Center));
        }

        return frame.Input.ConsumeClick(area);
    }

    public static void NavGlyph(in AppletFrame frame, Vector2 center, float size, ClockPane pane, Vector4 ink)
    {
        switch (pane)
        {
            case ClockPane.Alarm:
                frame.Paint.Stroke(Rect.FromSize(center + new Vector2(-size * 0.42f, -size * 0.18f),
                    new Vector2(size * 0.84f, size * 0.62f)), ink, MathF.Max(1.2f, size * 0.12f), size * 0.42f);
                frame.Paint.Line(center + new Vector2(0f, size * 0.44f), center + new Vector2(0f, size * 0.62f), ink,
                    MathF.Max(1.2f, size * 0.12f));
                frame.Paint.Line(center + new Vector2(-size * 0.22f, size * 0.62f),
                    center + new Vector2(size * 0.22f, size * 0.62f), ink, MathF.Max(1.2f, size * 0.12f));
                break;
            case ClockPane.World:
                frame.Paint.StrokeCircle(center, size * 0.62f, ink, MathF.Max(1.2f, size * 0.12f));
                frame.Paint.Line(center + new Vector2(0f, -size * 0.22f), center + new Vector2(0f, size * 0.08f), ink,
                    MathF.Max(1.2f, size * 0.12f));
                frame.Paint.Line(center + new Vector2(0f, size * 0.08f), center + new Vector2(size * 0.28f, size * 0.08f),
                    ink, MathF.Max(1.2f, size * 0.12f));
                break;
            case ClockPane.Timer:
                frame.Paint.StrokeCircle(center, size * 0.58f, ink, MathF.Max(1.2f, size * 0.12f));
                Arc(frame, center, size * 0.58f, -90f, 50f, ink, MathF.Max(1.6f, size * 0.16f));
                break;
            default:
                frame.Paint.StrokeCircle(center, size * 0.58f, ink, MathF.Max(1.2f, size * 0.12f));
                frame.Paint.Line(center, center + new Vector2(size * 0.28f, -size * 0.18f), ink,
                    MathF.Max(1.2f, size * 0.12f));
                break;
        }
    }

    private static void Hand(in AppletFrame frame, Vector2 center, float length, float degrees, Vector4 ink,
        float thickness)
    {
        var rad = degrees * (MathF.PI / 180f) - MathF.PI * 0.5f;
        var tip = center + new Vector2(MathF.Cos(rad), MathF.Sin(rad)) * length;
        var tail = center - new Vector2(MathF.Cos(rad), MathF.Sin(rad)) * length * 0.16f;
        frame.Paint.Line(tail, tip, ink, thickness);
    }
}
