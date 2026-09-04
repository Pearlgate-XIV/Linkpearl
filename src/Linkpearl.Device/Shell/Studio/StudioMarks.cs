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
            case StudioMark.Next:
                FillTriangle(paint, center + new Vector2(size * 0.40f, 0f),
                    center + new Vector2(-size * 0.32f, -size * 0.44f),
                    center + new Vector2(-size * 0.32f, size * 0.44f), color, stroke);
                if (mark == StudioMark.Next)
                {
                    paint.Fill(Rect.FromSize(center + new Vector2(size * 0.40f, -size * 0.44f),
                        new Vector2(MathF.Max(2.2f, stroke * 1.4f), size * 0.88f)), color, stroke * 0.4f);
                }

                break;
            case StudioMark.Prev:
                FillTriangle(paint, center + new Vector2(-size * 0.40f, 0f),
                    center + new Vector2(size * 0.32f, -size * 0.44f),
                    center + new Vector2(size * 0.32f, size * 0.44f), color, stroke);
                paint.Fill(Rect.FromSize(center + new Vector2(-size * 0.52f, -size * 0.44f),
                    new Vector2(MathF.Max(2.2f, stroke * 1.4f), size * 0.88f)), color, stroke * 0.4f);
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
                paint.Fill(Rect.FromSize(center + new Vector2(-size * 0.62f, -size * 0.18f),
                    new Vector2(size * 0.28f, size * 0.36f)), color, size * 0.08f);
                FillTriangle(paint, center + new Vector2(size * 0.10f, 0f),
                    center + new Vector2(-size * 0.28f, -size * 0.42f),
                    center + new Vector2(-size * 0.28f, size * 0.42f), color, stroke);
                Span<Vector2> near = stackalloc Vector2[3]
                {
                    center + new Vector2(size * 0.22f, -size * 0.22f),
                    center + new Vector2(size * 0.38f, 0f),
                    center + new Vector2(size * 0.22f, size * 0.22f),
                };
                Span<Vector2> far = stackalloc Vector2[3]
                {
                    center + new Vector2(size * 0.38f, -size * 0.40f),
                    center + new Vector2(size * 0.62f, 0f),
                    center + new Vector2(size * 0.38f, size * 0.40f),
                };
                paint.Polyline(near, color, stroke, closed: false);
                paint.Polyline(far, color, stroke, closed: false);
                break;
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
        }
    }

    private static void FillTriangle(IPaintSurface paint, Vector2 a, Vector2 b, Vector2 c, Vector4 color, float stroke)
    {
        var thick = MathF.Max(2.4f, stroke * 1.8f);
        paint.Polyline(stackalloc Vector2[] { a, b, c }, color, thick, closed: true);
        paint.FillCircle(a, thick * 0.42f, color);
        paint.FillCircle(b, thick * 0.42f, color);
        paint.FillCircle(c, thick * 0.42f, color);
        paint.FillCircle((a + b + c) / 3f, thick * 0.55f, color);
    }
}
