using Linkpearl.Geometry;
using Linkpearl.Painting;

namespace Linkpearl.Device.Shell.Studio;

internal enum StudioMark : byte
{
    Note = 0,
    Play = 1,
    Pause = 2,
    Chat = 3,
    Friends = 4,
    Camera = 5,
    Places = 6,
    Phone = 7,
    Person = 8,
    Mail = 9,
    Globe = 10,
    Sun = 11,
    Cloud = 12,
    Rain = 13,
    Party = 14,
    Market = 15,
    Event = 16,
    Retainer = 17,
    Pin = 18,
    Moon = 19,
    Clock = 20,
    Prev = 21,
    Next = 22,
    Bell = 23,
    Gear = 24,
    Shuffle = 25,
    Speaker = 26,
    Heart = 27,
    Star = 28,
    Follow = 29,
}

internal static class StudioMarks
{
    public static void Draw(IPaintSurface paint, Rect area, StudioMark mark, Vector4 color)
    {
        var size = MathF.Min(area.Width, area.Height) * 0.36f;
        Draw(paint, area.Center, size, mark, color);
    }

    public static void Draw(IPaintSurface paint, Vector2 center, float size, StudioMark mark, Vector4 color)
    {
        var stroke = MathF.Max(1.1f, size * 0.14f);
        switch (mark)
        {
            case StudioMark.Note:
                paint.FillCircle(center + new Vector2(-size * 0.22f, size * 0.32f), size * 0.22f, color);
                paint.Line(center + new Vector2(size * 0.00f, size * 0.32f),
                    center + new Vector2(size * 0.00f, -size * 0.58f), color, stroke);
                paint.Line(center + new Vector2(size * 0.00f, -size * 0.58f),
                    center + new Vector2(size * 0.40f, -size * 0.34f), color, stroke);
                break;
            case StudioMark.Play:
                FillTriangle(paint, center + new Vector2(size * 0.46f, 0f),
                    center + new Vector2(-size * 0.36f, -size * 0.50f),
                    center + new Vector2(-size * 0.36f, size * 0.50f), color);
                break;
            case StudioMark.Next:
                DrawSkip(paint, center, size, color, forward: true);
                break;
            case StudioMark.Prev:
                DrawSkip(paint, center, size, color, forward: false);
                break;
            case StudioMark.Pause:
                paint.Stroke(Rect.FromSize(center + new Vector2(-size * 0.36f, -size * 0.42f),
                    new Vector2(size * 0.22f, size * 0.84f)), color, stroke, size * 0.06f);
                paint.Stroke(Rect.FromSize(center + new Vector2(size * 0.14f, -size * 0.42f),
                    new Vector2(size * 0.22f, size * 0.84f)), color, stroke, size * 0.06f);
                break;
            case StudioMark.Chat:
                paint.Fill(Rect.FromSize(center - new Vector2(size * 0.62f, size * 0.48f),
                    new Vector2(size * 1.24f, size * 0.86f)), color, size * 0.32f);
                Span<Vector2> tail = stackalloc Vector2[3]
                {
                    center + new Vector2(-size * 0.12f, size * 0.36f),
                    center + new Vector2(size * 0.12f, size * 0.36f),
                    center + new Vector2(0f, size * 0.68f),
                };
                paint.Polyline(tail, color, stroke, closed: true);
                break;
            case StudioMark.Friends:
            case StudioMark.Person:
                paint.FillCircle(center + new Vector2(0f, -size * 0.28f), size * 0.30f, color);
                paint.Fill(Rect.FromSize(center + new Vector2(-size * 0.55f, size * 0.10f),
                    new Vector2(size * 1.10f, size * 0.58f)), color, size * 0.52f);
                break;
            case StudioMark.Camera:
                paint.Fill(Rect.FromSize(center - new Vector2(size * 0.62f, size * 0.38f),
                    new Vector2(size * 1.24f, size * 0.84f)), color, size * 0.18f);
                paint.FillCircle(center + new Vector2(size * 0.06f, 0.04f * size), size * 0.22f,
                    color with { W = color.W * 0.35f });
                paint.Fill(Rect.FromSize(center + new Vector2(-size * 0.22f, -size * 0.58f),
                    new Vector2(size * 0.28f, size * 0.16f)), color, size * 0.06f);
                break;
            case StudioMark.Places:
                paint.FillCircle(center + new Vector2(0f, -size * 0.16f), size * 0.36f, color);
                Span<Vector2> pin = stackalloc Vector2[3]
                {
                    center + new Vector2(-size * 0.28f, 0.06f * size),
                    center + new Vector2(size * 0.28f, 0.06f * size),
                    center + new Vector2(0f, size * 0.72f),
                };
                paint.Polyline(pin, color, stroke, closed: true);
                break;
            case StudioMark.Phone:
                paint.Stroke(Rect.FromSize(center - new Vector2(size * 0.42f, size * 0.62f),
                    new Vector2(size * 0.84f, size * 1.24f)), color, stroke, size * 0.42f);
                paint.Line(center + new Vector2(-size * 0.16f, size * 0.48f),
                    center + new Vector2(size * 0.16f, size * 0.48f), color, stroke);
                break;
            case StudioMark.Mail:
                var mail = Rect.FromSize(center - new Vector2(size * 0.70f, size * 0.42f),
                    new Vector2(size * 1.40f, size * 0.84f));
                paint.Stroke(mail, color, stroke, size * 0.10f);
                paint.Line(mail.Min, mail.Center + new Vector2(0f, size * 0.08f), color, stroke);
                paint.Line(new Vector2(mail.Max.X, mail.Min.Y), mail.Center + new Vector2(0f, size * 0.08f), color,
                    stroke);
                break;
            case StudioMark.Globe:
                paint.StrokeCircle(center, size * 0.70f, color, stroke);
                paint.Line(center + new Vector2(0f, -size * 0.70f), center + new Vector2(0f, size * 0.70f), color,
                    stroke);
                paint.Line(center + new Vector2(-size * 0.70f, 0f), center + new Vector2(size * 0.70f, 0f), color,
                    stroke);
                paint.Stroke(Rect.FromSize(center - new Vector2(size * 0.32f, size * 0.70f),
                    new Vector2(size * 0.64f, size * 1.40f)), color, stroke, size * 0.32f);
                break;
            case StudioMark.Sun:
                paint.StrokeCircle(center, size * 0.34f, color, stroke);
                for (var ray = 0; ray < 8; ray++)
                {
                    var angle = ray * (MathF.PI / 4f);
                    var dir = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
                    paint.Line(center + dir * (size * 0.48f), center + dir * (size * 0.72f), color, stroke);
                }

                break;
            case StudioMark.Cloud:
                paint.StrokeCircle(center + new Vector2(-size * 0.28f, size * 0.08f), size * 0.32f, color, stroke);
                paint.StrokeCircle(center + new Vector2(size * 0.22f, size * 0.04f), size * 0.38f, color, stroke);
                paint.StrokeCircle(center + new Vector2(-size * 0.02f, -size * 0.20f), size * 0.28f, color, stroke);
                break;
            case StudioMark.Rain:
                paint.StrokeCircle(center + new Vector2(0f, -size * 0.18f), size * 0.36f, color, stroke);
                paint.Line(center + new Vector2(-size * 0.16f, size * 0.22f),
                    center + new Vector2(-size * 0.06f, size * 0.56f), color, stroke);
                paint.Line(center + new Vector2(size * 0.14f, size * 0.22f),
                    center + new Vector2(size * 0.24f, size * 0.56f), color, stroke);
                break;
            case StudioMark.Party:
                paint.FillCircle(center + new Vector2(0f, -size * 0.28f), size * 0.26f, color);
                paint.FillCircle(center + new Vector2(-size * 0.40f, size * 0.20f), size * 0.26f, color);
                paint.FillCircle(center + new Vector2(size * 0.40f, size * 0.20f), size * 0.26f, color);
                break;
            case StudioMark.Market:
                Span<Vector2> chart = stackalloc Vector2[4]
                {
                    center + new Vector2(-size * 0.68f, size * 0.32f),
                    center + new Vector2(-size * 0.16f, size * 0.02f),
                    center + new Vector2(size * 0.12f, size * 0.26f),
                    center + new Vector2(size * 0.68f, -size * 0.38f),
                };
                paint.Polyline(chart, color, stroke, closed: false);
                break;
            case StudioMark.Event:
                var cal = Rect.FromSize(center - new Vector2(size * 0.68f, size * 0.52f),
                    new Vector2(size * 1.36f, size * 1.16f));
                paint.Fill(cal, color, size * 0.18f);
                paint.Line(new Vector2(cal.Min.X + size * 0.16f, cal.Min.Y + size * 0.36f),
                    new Vector2(cal.Max.X - size * 0.16f, cal.Min.Y + size * 0.36f),
                    color with { W = color.W * 0.35f }, stroke);
                break;
            case StudioMark.Retainer:
                paint.Stroke(Rect.FromSize(center - new Vector2(size * 0.52f, -size * 0.02f),
                    new Vector2(size * 1.04f, size * 0.70f)), color, stroke, size * 0.32f);
                paint.Stroke(Rect.FromSize(center - new Vector2(size * 0.20f, size * 0.42f),
                    new Vector2(size * 0.40f, size * 0.38f)), color, stroke, size * 0.16f);
                break;
            case StudioMark.Pin:
                paint.FillCircle(center + new Vector2(0f, -size * 0.16f), size * 0.34f, color);
                Span<Vector2> drop = stackalloc Vector2[3]
                {
                    center + new Vector2(-size * 0.26f, 0f),
                    center + new Vector2(size * 0.26f, 0f),
                    center + new Vector2(0f, size * 0.70f),
                };
                paint.Polyline(drop, color, stroke, closed: true);
                break;
            case StudioMark.Moon:
                paint.StrokeCircle(center, size * 0.62f, color, stroke);
                paint.StrokeCircle(center + new Vector2(size * 0.26f, -size * 0.08f), size * 0.44f,
                    color with { W = color.W * 0.35f }, stroke);
                break;
            case StudioMark.Clock:
                paint.StrokeCircle(center, size * 0.68f, color, stroke);
                paint.Line(center, center + new Vector2(0f, -size * 0.36f), color, stroke);
                paint.Line(center, center + new Vector2(size * 0.30f, size * 0.12f), color, stroke);
                break;
            case StudioMark.Bell:
                paint.Fill(Rect.FromSize(center - new Vector2(size * 0.42f, size * 0.18f),
                    new Vector2(size * 0.84f, size * 0.62f)), color, size * 0.42f);
                paint.FillCircle(center + new Vector2(0f, -size * 0.38f), size * 0.22f, color);
                paint.FillCircle(center + new Vector2(0f, size * 0.48f), size * 0.12f, color);
                break;
            case StudioMark.Shuffle:
                paint.Line(center + new Vector2(-size * 0.58f, -size * 0.28f),
                    center + new Vector2(size * 0.22f, size * 0.28f), color, stroke);
                paint.Line(center + new Vector2(-size * 0.58f, size * 0.28f),
                    center + new Vector2(size * 0.22f, -size * 0.28f), color, stroke);
                paint.Line(center + new Vector2(size * 0.22f, size * 0.28f),
                    center + new Vector2(size * 0.58f, size * 0.28f), color, stroke);
                paint.Line(center + new Vector2(size * 0.22f, size * 0.28f),
                    center + new Vector2(size * 0.22f, size * 0.58f), color, stroke);
                paint.Line(center + new Vector2(size * 0.22f, -size * 0.28f),
                    center + new Vector2(size * 0.58f, -size * 0.28f), color, stroke);
                paint.Line(center + new Vector2(size * 0.22f, -size * 0.28f),
                    center + new Vector2(size * 0.22f, -size * 0.58f), color, stroke);
                break;
            case StudioMark.Speaker:
            {
                var body = Rect.FromSize(center + new Vector2(-size * 0.52f, -size * 0.26f),
                    new Vector2(size * 0.22f, size * 0.52f));
                paint.Fill(body, color, size * 0.10f);
                var neck = body.Max.X - size * 0.02f;
                var mouthX = center.X + size * 0.06f;
                FillTrapezoid(paint, neck, center.Y - size * 0.22f, center.Y + size * 0.22f,
                    mouthX, center.Y - size * 0.58f, center.Y + size * 0.58f, color);
                var origin = new Vector2(mouthX + size * 0.04f, center.Y);
                var wave = MathF.Max(2.2f, size * 0.15f);
                DrawArc(paint, origin, size * 0.32f, color, wave);
                DrawArc(paint, origin, size * 0.52f, color, wave);
                DrawArc(paint, origin, size * 0.72f, color, wave);
                break;
            }
            case StudioMark.Gear:
                paint.FillCircle(center, size * 0.28f, color);
                paint.StrokeCircle(center, size * 0.52f, color, stroke);
                for (var tooth = 0; tooth < 6; tooth++)
                {
                    var angle = tooth * (MathF.PI / 3f);
                    var dir = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
                    paint.FillCircle(center + dir * (size * 0.58f), size * 0.12f, color);
                }

                break;
            case StudioMark.Heart:
            {
                var left = center + new Vector2(-size * 0.22f, -size * 0.08f);
                var right = center + new Vector2(size * 0.22f, -size * 0.08f);
                paint.FillCircle(left, size * 0.22f, color);
                paint.FillCircle(right, size * 0.22f, color);
                paint.Fill(Rect.FromSize(center + new Vector2(-size * 0.36f, -size * 0.04f),
                    new Vector2(size * 0.72f, size * 0.28f)), color);
                paint.Line(center + new Vector2(-size * 0.38f, 0.02f * size),
                    center + new Vector2(0f, size * 0.48f), color, stroke);
                paint.Line(center + new Vector2(size * 0.38f, 0.02f * size),
                    center + new Vector2(0f, size * 0.48f), color, stroke);
                break;
            }
            case StudioMark.Star:
            {
                for (var tip = 0; tip < 5; tip++)
                {
                    var a = -MathF.PI / 2f + tip * (MathF.PI * 2f / 5f);
                    var b = a + MathF.PI * 2f / 5f;
                    var outer = center + new Vector2(MathF.Cos(a), MathF.Sin(a)) * (size * 0.52f);
                    var inner = center + new Vector2(MathF.Cos(a + MathF.PI / 5f), MathF.Sin(a + MathF.PI / 5f)) *
                        (size * 0.22f);
                    var next = center + new Vector2(MathF.Cos(b), MathF.Sin(b)) * (size * 0.52f);
                    paint.Line(outer, inner, color, stroke);
                    paint.Line(inner, next, color, stroke);
                }

                paint.FillCircle(center, size * 0.12f, color);
                break;
            }
            case StudioMark.Follow:
                paint.FillCircle(center + new Vector2(-size * 0.10f, -size * 0.16f), size * 0.18f, color);
                paint.Fill(Rect.FromSize(center + new Vector2(-size * 0.36f, size * 0.04f),
                    new Vector2(size * 0.52f, size * 0.32f)), color, size * 0.16f);
                paint.Fill(Rect.FromSize(center + new Vector2(size * 0.12f, -size * 0.04f),
                    new Vector2(size * 0.36f, stroke * 1.1f)), color, stroke * 0.5f);
                paint.Fill(Rect.FromSize(center + new Vector2(size * 0.26f - stroke * 0.55f, -size * 0.18f),
                    new Vector2(stroke * 1.1f, size * 0.36f)), color, stroke * 0.5f);
                break;
        }
    }

    private static void DrawSkip(IPaintSurface paint, Vector2 center, float size, Vector4 color, bool forward)
    {
        var half = size * 0.52f;
        var bar = MathF.Max(3.4f, size * 0.18f);
        var gap = MathF.Max(1.6f, size * 0.08f);
        var tri = size * 0.80f;
        var left = center.X - (tri + gap + bar) * 0.5f;
        var tipX = forward ? left + tri : left + bar + gap;
        var baseX = forward ? left : left + bar + gap + tri;
        var barX = forward ? left + tri + gap : left;
        FillTriangle(paint, new Vector2(baseX, center.Y - half),
            new Vector2(baseX, center.Y + half),
            new Vector2(tipX, center.Y), color);
        paint.Fill(Rect.FromSize(new Vector2(barX, center.Y - half), new Vector2(bar, half * 2f)), color);
    }

    private static void DrawArc(IPaintSurface paint, Vector2 origin, float radius, Vector4 color, float stroke)
    {
        const int steps = 11;
        Span<Vector2> points = stackalloc Vector2[steps];
        var start = -1.05f;
        var sweep = 2.10f;
        for (var index = 0; index < steps; index++)
        {
            var angle = start + sweep * (index / (float)(steps - 1));
            points[index] = origin + new Vector2(MathF.Cos(angle) * radius, MathF.Sin(angle) * radius);
        }

        paint.Polyline(points, color, stroke, closed: false);
        paint.FillCircle(points[0], stroke * 0.5f, color);
        paint.FillCircle(points[steps - 1], stroke * 0.5f, color);
    }

    private static void FillTrapezoid(IPaintSurface paint, float leftX, float leftTop, float leftBottom,
        float rightX, float rightTop, float rightBottom, Vector4 color)
    {
        FillTriangle(paint, new Vector2(leftX, leftTop), new Vector2(leftX, leftBottom),
            new Vector2(rightX, rightTop), color);
        FillTriangle(paint, new Vector2(leftX, leftBottom), new Vector2(rightX, rightTop),
            new Vector2(rightX, rightBottom), color);
    }

    private static void FillTriangle(IPaintSurface paint, Vector2 a, Vector2 b, Vector2 c, Vector4 color)
    {
        var minY = MathF.Min(a.Y, MathF.Min(b.Y, c.Y));
        var maxY = MathF.Max(a.Y, MathF.Max(b.Y, c.Y));
        var height = maxY - minY;
        if (height < 0.5f)
        {
            return;
        }

        var step = 0.55f;
        for (var y = minY; y <= maxY + 0.01f; y += step)
        {
            var left = float.MaxValue;
            var right = float.MinValue;
            Cross(a, b, y, ref left, ref right);
            Cross(b, c, y, ref left, ref right);
            Cross(c, a, y, ref left, ref right);
            if (right < left)
            {
                continue;
            }

            paint.Line(new Vector2(left, y), new Vector2(right, y), color, step + 0.85f);
        }
    }

    private static void Cross(Vector2 from, Vector2 to, float y, ref float left, ref float right)
    {
        if ((from.Y <= y && to.Y <= y) || (from.Y > y && to.Y > y) || MathF.Abs(to.Y - from.Y) < 0.0001f)
        {
            return;
        }

        var x = from.X + (to.X - from.X) * ((y - from.Y) / (to.Y - from.Y));
        left = MathF.Min(left, x);
        right = MathF.Max(right, x);
    }
}
