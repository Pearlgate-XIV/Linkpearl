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
        var inset = strip.Inset(new Edges(frame.Units(16f), 0f, LockButton.ReservedRight(frame.Scale), 0f));
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

        var glyphs = inset.RightSlice(frame.Units(68f));
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
        var mark = MarkHeight(frame);
        var bottom = area.Center.Y + mark * 0.5f;
        var stroke = MathF.Max(1.2f, frame.Units(1.4f));
        var pitch = frame.Units(3.2f);
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
        var mark = MarkHeight(frame);
        var center = area.Center;
        var stroke = MathF.Max(1.2f, frame.Units(1.3f));
        frame.Paint.StrokeCircle(center, mark * 0.48f, color, stroke);
        frame.Paint.StrokeCircle(center, mark * 0.30f, color with { W = color.W * 0.7f }, stroke);
        frame.Paint.FillCircle(center, mark * 0.10f, color);
    }

    private static void DrawBattery(in AppletFrame frame, Rect area, Vector4 color)
    {
        var mark = MarkHeight(frame);
        var width = frame.Units(12f);
        var body = Rect.FromSize(area.Center - new Vector2(width * 0.5f, mark * 0.5f),
            new Vector2(width, mark));
        var stroke = MathF.Max(1.1f, frame.Units(1.2f));
        frame.Paint.Stroke(body, color, stroke, frame.Units(1.4f));
        var nubH = mark * 0.46f;
        frame.Paint.Fill(Rect.FromSize(new Vector2(body.Max.X, body.Center.Y - nubH * 0.5f),
            new Vector2(frame.Units(1.8f), nubH)), color, frame.Units(0.6f));
        frame.Paint.Fill(body.Inset(frame.Units(1.6f)), color with { W = color.W * 0.55f }, frame.Units(0.8f));
    }

    private static float MarkHeight(in AppletFrame frame) => frame.Units(7f);
}
