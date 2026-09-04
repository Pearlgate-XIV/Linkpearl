using Linkpearl.Badges;
using Linkpearl.Geometry;
using Linkpearl.Painting;

namespace Linkpearl.Destinations.Profile;

internal static class BadgeMarks
{
    public static void Draw(IPaintSurface paint, Rect area, BadgeMark mark, Vector4 ink)
    {
        var size = MathF.Min(area.Width, area.Height) * 0.36f;
        Draw(paint, area.Center, size, mark, ink);
    }

    public static void Draw(IPaintSurface paint, Vector2 c, float s, BadgeMark mark, Vector4 ink)
    {
        var stroke = MathF.Max(1.2f, s * 0.16f);
        switch (mark)
        {
            case BadgeMark.Pearl:
                paint.StrokeCircle(c, s * 0.55f, ink, stroke);
                paint.StrokeCircle(c, s * 0.28f, ink, stroke);
                break;
            case BadgeMark.Founder:
                paint.FillCircle(c, s * 0.42f, new Vector4(0.08f, 0.08f, 0.10f, 0.92f));
                paint.StrokeCircle(c, s * 0.42f, ink, stroke);
                paint.StrokeCircle(c, s * 0.72f, ink with { W = 0.85f }, stroke * 0.85f);
                StrokeEllipse(paint, c, s * 0.92f, s * 0.28f, ink, stroke);
                StrokeEllipse(paint, c, s * 0.28f, s * 0.92f, ink with { W = 0.7f }, stroke);
                break;
            case BadgeMark.Moon:
                paint.StrokeCircle(c + new Vector2(-s * 0.08f, 0f), s * 0.55f, ink, stroke);
                paint.FillCircle(c + new Vector2(s * 0.18f, -s * 0.08f), s * 0.42f, new Vector4(0.06f, 0.07f, 0.09f, 0.9f));
                paint.FillCircle(c + new Vector2(s * 0.42f, -s * 0.48f), s * 0.08f, ink);
                paint.FillCircle(c + new Vector2(s * 0.58f, s * 0.12f), s * 0.06f, ink);
                paint.FillCircle(c + new Vector2(-s * 0.52f, -s * 0.38f), s * 0.05f, ink);
                break;
            case BadgeMark.Moth:
                paint.StrokeCircle(c + new Vector2(-s * 0.32f, -s * 0.08f), s * 0.38f, ink, stroke);
                paint.StrokeCircle(c + new Vector2(s * 0.32f, -s * 0.08f), s * 0.38f, ink, stroke);
                paint.Line(c + new Vector2(0f, -s * 0.42f), c + new Vector2(0f, s * 0.48f), ink, stroke);
                break;
            case BadgeMark.Skull:
                paint.StrokeCircle(c + new Vector2(0f, -s * 0.12f), s * 0.52f, ink, stroke);
                paint.StrokeCircle(c + new Vector2(-s * 0.18f, -s * 0.12f), s * 0.12f, ink, stroke);
                paint.StrokeCircle(c + new Vector2(s * 0.18f, -s * 0.12f), s * 0.12f, ink, stroke);
                paint.Line(c + new Vector2(-s * 0.22f, s * 0.38f), c + new Vector2(s * 0.22f, s * 0.38f), ink, stroke);
                paint.Line(c + new Vector2(-s * 0.38f, -s * 0.62f), c + new Vector2(-s * 0.18f, -s * 0.42f), ink, stroke);
                paint.Line(c + new Vector2(s * 0.38f, -s * 0.62f), c + new Vector2(s * 0.18f, -s * 0.42f), ink, stroke);
                break;
            case BadgeMark.Camera:
                paint.Stroke(Rect.FromSize(c - new Vector2(s * 0.62f, s * 0.28f), new Vector2(s * 1.24f, s * 0.78f)),
                    ink, stroke, s * 0.14f);
                paint.StrokeCircle(c + new Vector2(0.06f * s, 0.08f * s), s * 0.22f, ink, stroke);
                paint.Stroke(Rect.FromSize(c + new Vector2(-s * 0.16f, -s * 0.48f), new Vector2(s * 0.32f, s * 0.20f)),
                    ink, stroke, s * 0.08f);
                break;
            case BadgeMark.Forge:
                paint.StrokeCircle(c, s * 0.32f, ink, stroke);
                paint.Line(c + new Vector2(0f, -s * 0.72f), c + new Vector2(0f, -s * 0.40f), ink, stroke);
                paint.Line(c + new Vector2(-s * 0.22f, s * 0.18f), c + new Vector2(s * 0.42f, s * 0.58f), ink, stroke);
                paint.Line(c + new Vector2(s * 0.18f, s * 0.08f), c + new Vector2(s * 0.62f, s * 0.42f), ink, stroke);
                break;
            case BadgeMark.Note:
                paint.FillCircle(c + new Vector2(-s * 0.22f, s * 0.32f), s * 0.22f, ink);
                paint.Line(c + new Vector2(-s * 0.04f, s * 0.32f), c + new Vector2(-s * 0.04f, -s * 0.52f), ink, stroke);
                paint.Line(c + new Vector2(-s * 0.04f, -s * 0.52f), c + new Vector2(s * 0.48f, -s * 0.32f), ink, stroke);
                break;
            case BadgeMark.Ticket:
                paint.Stroke(Rect.FromSize(c - new Vector2(s * 0.70f, s * 0.32f), new Vector2(s * 1.40f, s * 0.64f)),
                    ink, stroke, s * 0.10f);
                paint.Line(c + new Vector2(-s * 0.28f, -s * 0.22f), c + new Vector2(-s * 0.28f, s * 0.22f), ink, stroke);
                break;
            case BadgeMark.People:
                paint.StrokeCircle(c + new Vector2(-s * 0.22f, -s * 0.22f), s * 0.24f, ink, stroke);
                paint.StrokeCircle(c + new Vector2(s * 0.28f, -s * 0.16f), s * 0.18f, ink, stroke);
                break;
            case BadgeMark.Heart:
                paint.StrokeCircle(c + new Vector2(-s * 0.22f, -s * 0.12f), s * 0.28f, ink, stroke);
                paint.StrokeCircle(c + new Vector2(s * 0.22f, -s * 0.12f), s * 0.28f, ink, stroke);
                paint.Line(c + new Vector2(-s * 0.48f, 0f), c + new Vector2(0f, s * 0.62f), ink, stroke);
                paint.Line(c + new Vector2(s * 0.48f, 0f), c + new Vector2(0f, s * 0.62f), ink, stroke);
                break;
            case BadgeMark.Hourglass:
                paint.Line(c + new Vector2(-s * 0.38f, -s * 0.62f), c + new Vector2(s * 0.38f, -s * 0.62f), ink, stroke);
                paint.Line(c + new Vector2(-s * 0.38f, s * 0.62f), c + new Vector2(s * 0.38f, s * 0.62f), ink, stroke);
                paint.Line(c + new Vector2(-s * 0.32f, -s * 0.58f), c + new Vector2(s * 0.32f, s * 0.58f), ink, stroke);
                paint.Line(c + new Vector2(s * 0.32f, -s * 0.58f), c + new Vector2(-s * 0.32f, s * 0.58f), ink, stroke);
                break;
            default:
                paint.FillCircle(c, s * 0.12f, ink);
                paint.Line(c + new Vector2(0f, -s * 0.62f), c + new Vector2(0f, s * 0.62f), ink, stroke);
                paint.Line(c + new Vector2(-s * 0.62f, 0f), c + new Vector2(s * 0.62f, 0f), ink, stroke);
                break;
        }
    }

    private static void StrokeEllipse(IPaintSurface paint, Vector2 c, float rx, float ry, Vector4 ink, float stroke)
    {
        var points = new Vector2[18];
        for (var i = 0; i < points.Length; i++)
        {
            var t = i * (MathF.PI * 2f / points.Length);
            points[i] = c + new Vector2(MathF.Cos(t) * rx, MathF.Sin(t) * ry);
        }

        paint.Polyline(points, ink, stroke, true);
    }
}
