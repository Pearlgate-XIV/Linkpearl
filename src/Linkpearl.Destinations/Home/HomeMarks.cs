using Linkpearl.Applets;
using Linkpearl.Geometry;
using Linkpearl.Media;
using Linkpearl.Modules;
using Linkpearl.Painting;
using Linkpearl.Platform;

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
    Chat = 18,
    Pin = 19,
    Gear = 20,
}

internal static class HomeMarks
{
    // Same pixel box the solid bell occupies inside a header cell.
    public static Rect MatchBell(Rect area)
    {
        var size = MathF.Min(area.Width, area.Height) * 0.414f;
        var side = size * 1.49f;
        return Rect.FromSize(area.Center - new Vector2(side * 0.5f, side * 0.5f), new Vector2(side, side));
    }

    public static void Draw(in AppletFrame frame, Rect area, HomeMark mark, Vector4 color)
    {
        if (mark == HomeMark.Gear &&
            TryDrawGlyph(frame.Paint, frame.Textures, frame.Paths, area, "settings.png", color))
        {
            return;
        }

        if (AppIconCatalog.UseOriginalArt &&
            TryDrawAsset(frame.Paint, frame.Textures, frame.Paths, area, FileFor(mark), color))
        {
            return;
        }

        Draw(frame.Paint, area, mark, color);
    }

    public static void Draw(IPaintSurface paint, Rect area, HomeMark mark, Vector4 color)
    {
        var size = MathF.Min(area.Width, area.Height) *
            (mark is HomeMark.Bell ? 0.35f : mark is HomeMark.Chat or HomeMark.Gear ? 0.414f : 0.38f);
        Draw(paint, area.Center, size, mark, color);
    }

    public static bool TryDrawAsset(IPaintSurface paint, ITextureSource textures, HostPaths paths, Rect area,
        string? fileName, Vector4 color)
    {
        if (string.IsNullOrEmpty(fileName) || area.IsEmpty)
        {
            return false;
        }

        var texture = textures.FromFile(AppIconCatalog.Original(paths, fileName));
        if (texture is not { IsReady: true } || texture.Handle == 0)
        {
            return false;
        }

        var dest = CoverFit.Contained(texture.Size, area);
        if (dest.IsEmpty)
        {
            return false;
        }

        paint.Image(texture, dest, color);
        return true;
    }

    public static bool TryDrawGlyph(IPaintSurface paint, ITextureSource textures, HostPaths paths, Rect area,
        string fileName, Vector4 color)
    {
        if (string.IsNullOrEmpty(fileName) || area.IsEmpty)
        {
            return false;
        }

        var texture = textures.FromFile(AppIconCatalog.Glyph(paths, fileName));
        if (texture is not { IsReady: true } || texture.Handle == 0)
        {
            return false;
        }

        var dest = CoverFit.Contained(texture.Size, CoverFit.InscribedSquare(area));
        if (dest.IsEmpty)
        {
            return false;
        }

        paint.Image(texture, dest, color);
        return true;
    }

    public static string? FileFor(HomeMark mark) =>
        mark switch
        {
            HomeMark.Gear => AppIconCatalog.HomeGearAsset,
            HomeMark.Messages => AppIconCatalog.HomeMessagesAsset,
            HomeMark.Market => AppIconCatalog.HomeMarketAsset,
            HomeMark.Event => AppIconCatalog.HomeEventAsset,
            HomeMark.Friends => AppIconCatalog.HomeFriendsAsset,
            HomeMark.Party => AppIconCatalog.HomePartyAsset,
            HomeMark.Retainer => AppIconCatalog.HomeRetainerAsset,
            HomeMark.Place => AppIconCatalog.HomePlaceAsset,
            HomeMark.Pin => AppIconCatalog.HomePinAsset,
            HomeMark.Mask => AppIconCatalog.AnnouncementAsset,
            _ => null,
        };

    public static void Draw(IPaintSurface paint, Vector2 center, float size, HomeMark mark, Vector4 color)
    {
        var stroke = MathF.Max(1.2f, size * 0.16f);
        switch (mark)
        {
            case HomeMark.Messages:
                DrawBubble(paint, center + new Vector2(-size * 0.18f, -size * 0.12f), size * 0.94f, color, stroke);
                DrawBubble(paint, center + new Vector2(size * 0.22f, size * 0.18f), size * 0.74f, color, stroke);
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
                DrawSolidBell(paint, center, size, color);
                break;
            case HomeMark.Chat:
                DrawChat(paint, center, size, color, stroke);
                break;
            case HomeMark.Gear:
                DrawGear(paint, center, size, color, stroke);
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
            case HomeMark.Pin:
                paint.FillCircle(center + new Vector2(0f, -size * 0.18f), size * 0.38f, color);
                Span<Vector2> pin = stackalloc Vector2[3]
                {
                    center + new Vector2(-size * 0.28f, -size * 0.02f),
                    center + new Vector2(size * 0.28f, -size * 0.02f),
                    center + new Vector2(0f, size * 0.72f),
                };
                paint.Polyline(pin, color, stroke, closed: true);
                break;
        }
    }

    private static void DrawSolidBell(IPaintSurface paint, Vector2 center, float size, Vector4 color)
    {
        paint.FillCircle(center + new Vector2(0f, -size * 0.68f), size * 0.11f, color);
        var body = Rect.FromSize(center - new Vector2(size * 0.52f, size * 0.58f),
            new Vector2(size * 1.04f, size * 1.02f));
        paint.Fill(body, color, size * 0.52f, Corner.Top);
        var lip = Rect.FromSize(center - new Vector2(size * 0.66f, -size * 0.30f),
            new Vector2(size * 1.32f, size * 0.22f));
        paint.Fill(lip, color, size * 0.10f);
        paint.FillCircle(center + new Vector2(0f, size * 0.58f), size * 0.12f, color);
    }

    private static void DrawGear(IPaintSurface paint, Vector2 center, float size, Vector4 color, float stroke)
    {
        paint.StrokeCircle(center, size * 0.36f, color, stroke);
        paint.StrokeCircle(center, size * 0.14f, color, stroke);
        for (var tooth = 0; tooth < 6; tooth++)
        {
            var angle = tooth * (MathF.PI / 3f);
            var dir = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
            paint.Line(center + dir * (size * 0.46f), center + dir * (size * 0.78f), color, stroke);
        }
    }

    private static void DrawChat(IPaintSurface paint, Vector2 center, float size, Vector4 color, float stroke)
    {
        var body = Rect.FromSize(center - new Vector2(size * 0.62f, size * 0.50f),
            new Vector2(size * 1.24f, size * 0.90f));
        paint.Stroke(body, color, stroke, size * 0.36f);
        Span<Vector2> tail = stackalloc Vector2[3]
        {
            new Vector2(center.X - size * 0.14f, body.Max.Y - stroke * 0.5f),
            new Vector2(center.X + size * 0.14f, body.Max.Y - stroke * 0.5f),
            new Vector2(center.X, body.Max.Y + size * 0.34f),
        };
        paint.Polyline(tail, color, stroke, closed: true);
    }

    private static void DrawBubble(IPaintSurface paint, Vector2 center, float size, Vector4 color, float stroke)
    {
        paint.Stroke(Rect.FromSize(center - new Vector2(size, size * 0.7f), new Vector2(size * 2f, size * 1.4f)),
            color, stroke, size * 0.55f);
    }
}
