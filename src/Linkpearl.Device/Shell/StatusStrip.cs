using Linkpearl.Applets;
using Linkpearl.Device.Chassis;
using Linkpearl.Geometry;
using Linkpearl.Painting;

namespace Linkpearl.Device.Shell;

// Flush status chrome: time + crystal + world on the left, decorative radio glyphs on the right.
// Signal / wifi / battery are chassis marks, not live telemetry.
public static class StatusStrip
{
    private const float HeightUnits = 30f;

    public static float Height(float scale) => HeightUnits * scale;

    public static Rect StripArea(Rect screen, float scale) => screen.TopSlice(Height(scale));

    public static void Draw(in AppletFrame frame, Rect screen, string clockText, string worldName, bool showWorld,
        bool showMarks)
    {
        var strip = StripArea(screen, frame.Scale);
        frame.Paint.Fill(strip, frame.Theme.Palette.SurfaceSunken with { W = 0.22f });
        var inset = strip.Inset(GlassSafe.StripEdges(screen, frame.Scale, LockButton.ReservedRight(frame.Scale)));
        var ink = frame.Theme.Palette.Ink;
        var muted = frame.Theme.Palette.InkMuted;
        var gold = frame.Theme.Palette.WarmAccent;

        var clockWidth = frame.Text.Measure(clockText, FontRole.Caption).X;
        frame.Text.DrawIn(inset.LeftSlice(clockWidth), clockText,
            new TextStyle(FontRole.Caption, ink, TextAlign.Left));

        var cursor = inset.Min.X + clockWidth + frame.Units(8f);
        var crystal = Rect.FromSize(new Vector2(cursor, inset.Min.Y),
            new Vector2(frame.Units(12f), inset.Height));
        DrawCrystal(frame, crystal, gold);
        cursor = crystal.Max.X + frame.Units(6f);

        if (showWorld && worldName.Length > 0)
        {
            var world = new Rect(new Vector2(cursor, inset.Min.Y),
                new Vector2(inset.Max.X - (showMarks ? frame.Units(72f) : 0f), inset.Max.Y));
            frame.Text.DrawEllipsized(world, worldName, new TextStyle(FontRole.Caption, muted));
        }

        if (!showMarks)
        {
            return;
        }

        var glyphs = inset.RightSlice(MathF.Min(frame.Units(68f), inset.Width * 0.42f));
        var cell = glyphs.Width / 3f;
        DrawSignal(frame, glyphs.LeftSlice(cell), ink with { W = 0.78f });
        DrawWifi(frame, new Rect(new Vector2(glyphs.Min.X + cell, glyphs.Min.Y),
            new Vector2(glyphs.Min.X + cell * 2f, glyphs.Max.Y)), ink with { W = 0.78f });
        DrawBattery(frame, glyphs.RightSlice(cell), ink with { W = 0.78f });
    }

    private static void DrawCrystal(in AppletFrame frame, Rect area, Vector4 color)
    {
        var center = area.Center;
        var radius = MathF.Min(area.Width, area.Height) * 0.28f;
        Span<Vector2> points = stackalloc Vector2[4]
        {
            center + new Vector2(0f, -radius),
            center + new Vector2(radius, 0f),
            center + new Vector2(0f, radius),
            center + new Vector2(-radius, 0f),
        };
        frame.Paint.Polyline(points, color, MathF.Max(1.2f, radius * 0.22f), closed: true);
    }

    private static void DrawSignal(in AppletFrame frame, Rect area, Vector4 color)
    {
        var mark = FitMark(frame, area);
        var bottom = area.Center.Y + mark * 0.5f;
        var stroke = MathF.Max(1.2f, frame.Units(1.4f));
        var pitch = MathF.Min(frame.Units(3.2f), area.Width / 5.2f);
        var baseX = area.Center.X - pitch * 1.5f;
        for (var index = 0; index < 4; index++)
        {
            var height = mark * (0.28f + index * 0.24f);
            var x = baseX + index * pitch;
            frame.Paint.Line(new Vector2(x, bottom), new Vector2(x, bottom - height), color, stroke);
        }
    }

    private static void DrawWifi(in AppletFrame frame, Rect area, Vector4 color)
    {
        var mark = FitMark(frame, area);
        var center = area.Center;
        var radius = MathF.Min(mark * 0.42f, MathF.Min(area.Width, area.Height) * 0.38f);
        var stroke = MathF.Max(1.1f, frame.Units(1.3f));
        frame.Paint.StrokeCircle(center, radius, color, stroke);
        frame.Paint.StrokeCircle(center, radius * 0.62f, color with { W = color.W * 0.7f }, stroke);
        frame.Paint.FillCircle(center, MathF.Max(1.2f, radius * 0.22f), color);
    }

    private static void DrawBattery(in AppletFrame frame, Rect area, Vector4 color)
    {
        var mark = FitMark(frame, area);
        var nub = MathF.Min(frame.Units(1.8f), MathF.Max(1f, area.Width * 0.12f));
        var width = MathF.Min(frame.Units(12f), MathF.Max(4f, area.Width - nub - 1f));
        var body = Rect.FromSize(
            new Vector2(area.Center.X - (width + nub) * 0.5f, area.Center.Y - mark * 0.5f),
            new Vector2(width, mark));
        var stroke = MathF.Max(1.1f, frame.Units(1.2f));
        frame.Paint.Stroke(body, color, stroke, frame.Units(1.4f));
        var nubH = mark * 0.46f;
        frame.Paint.Fill(Rect.FromSize(new Vector2(body.Max.X, body.Center.Y - nubH * 0.5f),
            new Vector2(nub, nubH)), color, frame.Units(0.6f));
        frame.Paint.Fill(body.Inset(frame.Units(1.6f)), color with { W = color.W * 0.55f }, frame.Units(0.8f));
    }

    private static float MarkHeight(in AppletFrame frame) =>
        MathF.Min(frame.Units(7f), Height(frame.Scale) * 0.42f);

    private static float FitMark(in AppletFrame frame, Rect area) =>
        MathF.Min(MarkHeight(frame), MathF.Max(4f, area.Height * 0.62f));
}
