using Linkpearl.Geometry;
using Linkpearl.Painting;

namespace Linkpearl.Destinations.Home;

internal enum HomeMark : byte
{
    Messages = 0,
    Party = 1,
    Retainer = 2,
    Friends = 3,
    Market = 4,
    Event = 5,
    Bell = 6,
    Shield = 7,
    Chevron = 8,
    Moon = 9,
    Sun = 10,
    Clock = 11,
    Mask = 12,
    Pouch = 13,
    Diamond = 14,
    Cloud = 15,
    Rain = 16,
    Place = 17,
}

internal static class HomeMarks
{
    public static void Draw(IPaintSurface paint, Rect area, HomeMark mark, Vector4 color)
    {
        var size = MathF.Min(area.Width, area.Height) * 0.38f;
        Draw(paint, area.Center, size, mark, color);
    }

    public static void Draw(IPaintSurface paint, Vector2 center, float size, HomeMark mark, Vector4 color)
    {
        var stroke = MathF.Max(1.2f, size * 0.16f);
        switch (mark)
        {
            case HomeMark.Messages:
                DrawBubble(paint, center + new Vector2(-size * 0.18f, -size * 0.12f), size * 0.78f, color, stroke);
                DrawBubble(paint, center + new Vector2(size * 0.22f, size * 0.18f), size * 0.62f, color, stroke);
                break;
            case HomeMark.Party:
                paint.StrokeCircle(center + new Vector2(0f, -size * 0.28f), size * 0.28f, color, stroke);
                paint.StrokeCircle(center + new Vector2(-size * 0.42f, size * 0.22f), size * 0.28f, color, stroke);
                paint.StrokeCircle(center + new Vector2(size * 0.42f, size * 0.22f), size * 0.28f, color, stroke);
                break;
            case HomeMark.Retainer:
                paint.StrokeCircle(center + new Vector2(0f, -size * 0.28f), size * 0.32f, color, stroke);
                paint.Stroke(new Rect(center + new Vector2(-size * 0.55f, size * 0.12f),
                    center + new Vector2(size * 0.55f, size * 0.72f)), color, stroke, size * 0.55f);
                break;
            case HomeMark.Friends:
                paint.StrokeCircle(center + new Vector2(-size * 0.28f, 0f), size * 0.38f, color, stroke);
                paint.StrokeCircle(center + new Vector2(size * 0.32f, 0f), size * 0.32f, color, stroke);
                break;
            case HomeMark.Market:
                Span<Vector2> chart = stackalloc Vector2[4]
                {
                    center + new Vector2(-size * 0.7f, size * 0.35f),
                    center + new Vector2(-size * 0.18f, size * 0.05f),
                    center + new Vector2(size * 0.12f, size * 0.28f),
                    center + new Vector2(size * 0.7f, -size * 0.4f),
                };
                paint.Polyline(chart, color, stroke, closed: false);
                break;
            case HomeMark.Event:
                var cal = Rect.FromSize(center - new Vector2(size * 0.7f, size * 0.55f),
                    new Vector2(size * 1.4f, size * 1.2f));
                paint.Stroke(cal, color, stroke, size * 0.22f);
                paint.Line(new Vector2(cal.Min.X + size * 0.18f, cal.Min.Y + size * 0.38f),
                    new Vector2(cal.Max.X - size * 0.18f, cal.Min.Y + size * 0.38f), color, stroke);
                break;
            case HomeMark.Bell:
                paint.Stroke(Rect.FromSize(center - new Vector2(size * 0.55f, size * 0.45f),
                    new Vector2(size * 1.1f, size * 0.95f)), color, stroke, size * 0.55f);
                paint.FillCircle(center + new Vector2(0f, size * 0.62f), stroke * 0.7f, color);
                break;
            case HomeMark.Shield:
                Span<Vector2> shield = stackalloc Vector2[6]
                {
                    center + new Vector2(0f, -size * 0.75f),
                    center + new Vector2(size * 0.62f, -size * 0.35f),
                    center + new Vector2(size * 0.48f, size * 0.28f),
                    center + new Vector2(0f, size * 0.78f),
                    center + new Vector2(-size * 0.48f, size * 0.28f),
                    center + new Vector2(-size * 0.62f, -size * 0.35f),
                };
                paint.Polyline(shield, color, stroke, closed: true);
                break;
            case HomeMark.Chevron:
                paint.Line(center + new Vector2(-size * 0.25f, -size * 0.55f),
                    center + new Vector2(size * 0.35f, 0f), color, stroke);
                paint.Line(center + new Vector2(-size * 0.25f, size * 0.55f),
                    center + new Vector2(size * 0.35f, 0f), color, stroke);
                break;
            case HomeMark.Moon:
                paint.StrokeCircle(center, size * 0.7f, color, stroke);
                paint.StrokeCircle(center + new Vector2(size * 0.28f, -size * 0.08f), size * 0.48f,
                    color with { W = color.W * 0.35f }, stroke);
                break;
            case HomeMark.Sun:
                paint.StrokeCircle(center, size * 0.42f, color, stroke);
                break;
            case HomeMark.Clock:
                paint.StrokeCircle(center, size * 0.72f, color, stroke);
                paint.Line(center, center + new Vector2(0f, -size * 0.38f), color, stroke);
                paint.Line(center, center + new Vector2(size * 0.32f, size * 0.12f), color, stroke);
                break;
            case HomeMark.Mask:
                paint.StrokeCircle(center, size * 0.78f, color, stroke);
                paint.StrokeCircle(center + new Vector2(-size * 0.22f, -size * 0.08f), size * 0.16f, color, stroke);
                paint.StrokeCircle(center + new Vector2(size * 0.22f, -size * 0.08f), size * 0.16f, color, stroke);
                break;
            case HomeMark.Pouch:
                paint.Stroke(Rect.FromSize(center - new Vector2(size * 0.55f, -size * 0.05f),
                    new Vector2(size * 1.1f, size * 0.75f)), color, stroke, size * 0.35f);
                paint.Stroke(Rect.FromSize(center - new Vector2(size * 0.22f, size * 0.45f),
                    new Vector2(size * 0.44f, size * 0.4f)), color, stroke, size * 0.18f);
                break;
            case HomeMark.Diamond:
                Span<Vector2> diamond = stackalloc Vector2[4]
                {
                    center + new Vector2(0f, -size * 0.72f),
                    center + new Vector2(size * 0.52f, 0f),
                    center + new Vector2(0f, size * 0.72f),
                    center + new Vector2(-size * 0.52f, 0f),
                };
                paint.Polyline(diamond, color, stroke, closed: true);
                break;
            case HomeMark.Cloud:
                paint.StrokeCircle(center + new Vector2(-size * 0.28f, size * 0.06f), size * 0.34f, color, stroke);
                paint.StrokeCircle(center + new Vector2(size * 0.22f, size * 0.02f), size * 0.40f, color, stroke);
                paint.StrokeCircle(center + new Vector2(-size * 0.02f, -size * 0.22f), size * 0.30f, color, stroke);
                break;
            case HomeMark.Rain:
                paint.StrokeCircle(center + new Vector2(0f, -size * 0.22f), size * 0.38f, color, stroke);
                paint.Line(center + new Vector2(-size * 0.18f, size * 0.22f),
                    center + new Vector2(-size * 0.08f, size * 0.58f), color, stroke);
                paint.Line(center + new Vector2(size * 0.12f, size * 0.22f),
                    center + new Vector2(size * 0.22f, size * 0.58f), color, stroke);
                break;
            case HomeMark.Place:
                paint.Stroke(Rect.FromSize(center - new Vector2(size * 0.52f, size * 0.48f),
                    new Vector2(size * 1.04f, size * 0.96f)), color, stroke, size * 0.16f);
                paint.Line(center + new Vector2(0f, -size * 0.12f),
                    center + new Vector2(0f, size * 0.36f), color, stroke);
                break;
        }
    }

    private static void DrawBubble(IPaintSurface paint, Vector2 center, float size, Vector4 color, float stroke)
    {
        paint.Stroke(Rect.FromSize(center - new Vector2(size, size * 0.7f), new Vector2(size * 2f, size * 1.4f)),
            color, stroke, size * 0.55f);
    }
}
