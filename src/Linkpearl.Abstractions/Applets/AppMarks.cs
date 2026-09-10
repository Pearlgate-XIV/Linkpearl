using Linkpearl.Applets;
using Linkpearl.Geometry;
using Linkpearl.Media;
using Linkpearl.Modules;
using Linkpearl.Painting;
using Linkpearl.Platform;

namespace Linkpearl.Applets;

public static class AppMarks
{
    private static readonly Vector4 White = new(0.96f, 0.96f, 0.97f, 1f);
    private static readonly Vector4 Rim = new(1f, 1f, 1f, 0.42f);
    private static readonly Vector4 Glass = new(0.05f, 0.06f, 0.07f, 0.38f);
    private static readonly Vector4 GlassLit = new(0.09f, 0.10f, 0.12f, 0.48f);

    public static void Draw(IPaintSurface paint, ITextureSource textures, HostPaths paths, Rect icon, string appletId,
        Vector4 _) =>
        DrawFace(paint, textures, paths, icon, appletId, false);

    public static void DrawFace(in AppletFrame frame, Rect icon, string appletId, bool hover) =>
        DrawFace(frame.Paint, frame.Textures, frame.Paths, icon, appletId, hover);

    public static void DrawCount(in AppletFrame frame, Rect icon, int count)
    {
        if (count <= 0 || icon.Width < 8f || icon.Height < 8f)
        {
            return;
        }

        var radius = frame.Units(9f);
        var center = new Vector2(icon.Max.X - radius * 0.22f, icon.Min.Y + radius * 0.22f);
        frame.Paint.FillCircle(center, radius, frame.Theme.Palette.Negative);
        frame.Text.Draw(center, count > 9 ? "9+" : count.ToString(System.Globalization.CultureInfo.InvariantCulture),
            new TextStyle(FontRole.CaptionStrong, Vector4.One, TextAlign.Center, 1f, 0.92f));
    }

    public static void DrawFace(IPaintSurface paint, ITextureSource textures, HostPaths paths, Rect icon,
        string appletId, bool hover)
    {
        if (appletId.StartsWith("folder:", StringComparison.Ordinal))
        {
            DrawFolderFace(paint, textures, paths, icon, [], hover);
            return;
        }

        if (AppIconCatalog.UseOriginalArt && TryDrawAsset(paint, textures, paths, icon, appletId))
        {
            return;
        }

        DrawTile(paint, textures, paths, icon, appletId, hover);
    }

    public static void DrawFolderFace(in AppletFrame frame, Rect icon, IReadOnlyList<string> children, bool hover) =>
        DrawFolderFace(frame.Paint, frame.Textures, frame.Paths, icon, children, hover);

    public static void DrawFolderFace(IPaintSurface paint, ITextureSource textures, HostPaths paths, Rect icon,
        IReadOnlyList<string> children, bool hover)
    {
        var side = MathF.Min(icon.Width, icon.Height);
        if (side <= 1f)
        {
            return;
        }

        var area = Rect.FromSize(icon.Center - new Vector2(side * 0.5f, side * 0.5f), new Vector2(side, side));
        var radius = side * 0.22f;
        var fill = hover ? new Vector4(1f, 1f, 1f, 0.22f) : new Vector4(1f, 1f, 1f, 0.12f);
        var rim = hover ? new Vector4(1f, 1f, 1f, 0.88f) : new Vector4(1f, 1f, 1f, 0.62f);
        paint.Fill(area, fill, radius);
        paint.Stroke(area, rim, MathF.Max(1.1f, side * 0.035f), radius);

        var show = Math.Min(4, children.Count);
        if (show <= 0)
        {
            return;
        }

        var pad = side * 0.14f;
        var gap = side * 0.07f;
        var inner = area.Inset(pad);
        if (show == 1)
        {
            var mini = MathF.Min(inner.Width, inner.Height) * 0.72f;
            var box = Rect.FromSize(inner.Center - new Vector2(mini * 0.5f, mini * 0.5f), new Vector2(mini, mini));
            DrawTile(paint, textures, paths, box, children[0], false);
            return;
        }

        var cell = (MathF.Min(inner.Width, inner.Height) - gap) * 0.5f;
        for (var index = 0; index < show; index++)
        {
            var col = index % 2;
            var row = index / 2;
            var box = Rect.FromSize(
                new Vector2(inner.Min.X + col * (cell + gap), inner.Min.Y + row * (cell + gap)),
                new Vector2(cell, cell));
            DrawTile(paint, textures, paths, box, children[index], false);
        }
    }

    public static void DrawRoundFace(in AppletFrame frame, Rect icon, string appletId, bool hover)
    {
        if (AppIconCatalog.UseOriginalArt &&
            TryDrawRoundAsset(frame.Paint, frame.Textures, frame.Paths, icon, appletId))
        {
            return;
        }

        DrawTile(frame.Paint, frame.Textures, frame.Paths, icon, appletId, hover);
    }

    public static void DrawRoundAsset(in AppletFrame frame, Rect icon, string fileName, bool hover)
    {
        if (AppIconCatalog.UseOriginalArt &&
            TryDrawRoundFile(frame.Paint, frame.Textures, frame.Paths, icon, fileName))
        {
            return;
        }

        DrawTile(frame.Paint, frame.Textures, frame.Paths, icon, IdForAsset(fileName), hover);
    }

    private static bool TryDrawAsset(IPaintSurface paint, ITextureSource textures, HostPaths paths, Rect icon,
        string appletId)
    {
        if (AppShelf.Find(appletId) is not { } spec || spec.IconAsset.Length == 0)
        {
            return false;
        }

        var texture = textures.FromFile(AppIconCatalog.Original(paths, spec.IconAsset));
        if (texture is not { IsReady: true })
        {
            return false;
        }

        var side = MathF.Min(icon.Width, icon.Height);
        if (side <= 0f)
        {
            return true;
        }

        var area = Rect.FromSize(icon.Center - new Vector2(side * 0.5f, side * 0.5f), new Vector2(side, side));
        paint.Image(texture, area, Vector4.One);
        return true;
    }

    private static bool TryDrawRoundAsset(IPaintSurface paint, ITextureSource textures, HostPaths paths, Rect icon,
        string appletId)
    {
        if (AppShelf.Find(appletId) is not { } spec || spec.IconAsset.Length == 0)
        {
            return false;
        }

        return TryDrawRoundFile(paint, textures, paths, icon, spec.IconAsset);
    }

    private static bool TryDrawRoundFile(IPaintSurface paint, ITextureSource textures, HostPaths paths, Rect icon,
        string fileName)
    {
        var texture = textures.FromFile(AppIconCatalog.Original(paths, fileName));
        if (texture is not { IsReady: true })
        {
            return false;
        }

        var side = MathF.Min(icon.Width, icon.Height);
        if (side <= 0f)
        {
            return true;
        }

        var area = Rect.FromSize(icon.Center - new Vector2(side * 0.5f, side * 0.5f), new Vector2(side, side));
        paint.ImageRounded(texture, area, Vector2.Zero, Vector2.One, Vector4.One, side * 0.34f);
        return true;
    }

    private static void DrawTile(IPaintSurface paint, ITextureSource textures, HostPaths paths, Rect icon,
        string appletId, bool hover)
    {
        var side = MathF.Min(icon.Width, icon.Height);
        if (side <= 1f)
        {
            return;
        }

        var area = Rect.FromSize(icon.Center - new Vector2(side * 0.5f, side * 0.5f), new Vector2(side, side));
        Vector4 light;
        Vector4 dark;
        if (appletId is "music" or "afterdark")
        {
            var brand = TileBrand(appletId);
            light = hover ? Lift(brand) : brand;
            dark = light;
        }
        else
        {
            var brand = TileBrand(appletId);
            dark = hover ? Lift(brand) : brand;
            light = hover ? Lift(Wash(brand)) : Wash(brand);
        }

        paint.FillAppTile(area, light, dark);
        paint.StrokeAppTile(area, new Vector4(1f, 1f, 1f, hover ? 0.22f : 0.10f), MathF.Max(1f, side * 0.02f));
        if (!TryDrawPackedGlyph(paint, textures, paths, area, appletId))
        {
            DrawGlyph(paint, area, appletId);
        }
    }

    private static bool TryDrawPackedGlyph(IPaintSurface paint, ITextureSource textures, HostPaths paths, Rect tile,
        string appletId)
    {
        var file = GlyphFile(appletId);
        if (file is null)
        {
            return false;
        }

        var texture = textures.FromFile(AppIconCatalog.Glyph(paths, file));
        if (texture is not { IsReady: true })
        {
            return false;
        }

        var inset = appletId is "afterdark" or "music"
            ? MathF.Min(tile.Width, tile.Height) * 0.08f
            : MathF.Min(tile.Width, tile.Height) * 0.20f;
        var mark = tile.Inset(inset);
        var dest = CoverFit.Contained(texture.Size, CoverFit.InscribedSquare(mark));
        paint.Image(texture, dest, Vector4.One);
        return true;
    }

    private static Vector4 TileBrand(string appletId) => AppGround.Brand(appletId);

    private static Vector4 Wash(Vector4 brand) =>
        new(MathF.Min(1f, brand.X * 0.72f + 0.28f), MathF.Min(1f, brand.Y * 0.72f + 0.28f),
            MathF.Min(1f, brand.Z * 0.72f + 0.28f), 1f);

    private static Vector4 Lift(Vector4 color) =>
        new(MathF.Min(1f, color.X * 1.10f + 0.08f), MathF.Min(1f, color.Y * 1.10f + 0.08f),
            MathF.Min(1f, color.Z * 1.10f + 0.08f), 1f);

    private static string? GlyphFile(string appletId) => appletId switch
    {
        "phone" => "phone.png",
        "pearlchat" => "messages.png",
        "friends" => "friends.png",
        "camera" => "camera.png",
        "settings" => "settings.png",
        "wallet" => "wallet.png",
        "market" => "market.png",
        "weather" => "weather.png",
        "party" => "party.png",
        "events" => "events.png",
        "feedback" => "feedback.png",
        "afterdark" => "vybe.png",
        "music" => "music.png",
        _ => null,
    };

    private static string IdForAsset(string fileName) => fileName switch
    {
        AppIconCatalog.HomeMessagesAsset => "pearlchat",
        AppIconCatalog.HomePartyAsset => "party",
        AppIconCatalog.HomeFriendsAsset => "friends",
        AppIconCatalog.HomeRetainerAsset => "retainer",
        AppIconCatalog.HomeMarketAsset => "market",
        AppIconCatalog.HomeEventAsset => "events",
        AppIconCatalog.HomePlaceAsset => "place",
        AppIconCatalog.HomePinAsset => "place",
        AppIconCatalog.HomeGearAsset => "settings",
        AppIconCatalog.AnnouncementAsset => "feedback",
        AppIconCatalog.DaylightAsset => "afterdark",
        _ => Path.GetFileNameWithoutExtension(fileName),
    };

    public static void DrawGlass(IPaintSurface paint, Rect icon, bool hover)
    {
        var radius = MathF.Min(icon.Width, icon.Height) * 0.5f;
        var center = icon.Center;
        paint.FillCircle(center, radius, hover ? GlassLit : Glass);
        paint.StrokeCircle(center, radius * 0.98f, Rim, MathF.Max(1.2f, radius * 0.045f));
    }

    public static void DrawMark(in AppletFrame frame, Rect icon, string appletId)
    {
        if (TryDrawPackedGlyph(frame.Paint, frame.Textures, frame.Paths, icon, appletId))
        {
            return;
        }

        DrawGlyph(frame.Paint, icon, appletId);
    }

    public static void DrawGlyph(IPaintSurface paint, Rect icon, string appletId)
    {
        var size = MathF.Min(icon.Width, icon.Height) * 0.32f;
        var center = icon.Center;
        var stroke = MathF.Max(1.4f, size * 0.16f);
        switch (appletId)
        {
            case "pearlchat":
                DrawChat(paint, center, size, White, stroke);
                break;
            case "phone":
                DrawClassicHandset(paint, center, size, White, stroke);
                break;
            case "music":
                DrawNote(paint, center, size, White, stroke);
                break;
            case "afterdark":
                DrawVybeMark(paint, center, size * 1.2f, White);
                break;
            case "weather":
                DrawWeather(paint, center, size, White, White, stroke);
                break;
            case "calendar":
                DrawCalendar(paint, center, size, White, stroke);
                break;
            case "wallet":
                DrawWallet(paint, center, size, White, stroke);
                break;
            case "camera":
                DrawCamera(paint, center, size, White, stroke);
                break;
            case "friends":
            case "party":
                DrawPeople(paint, center, size, White, stroke);
                break;
            case "settings":
                DrawGear(paint, center, size, White, stroke);
                break;
            case "feedback":
                DrawChat(paint, center, size, White, stroke);
                break;
            case "market":
                DrawChart(paint, center, size, White, stroke);
                break;
            case "events":
            case "eorzea":
                DrawTickets(paint, center, size, White, stroke);
                break;
            case "notes":
                DrawNotes(paint, center, size, White, stroke);
                break;
            case "alarms":
                DrawAlarm(paint, center, size, White, stroke);
                break;
            case "clock":
                DrawClock(paint, center, size, White, stroke);
                break;
            case "calculator":
                DrawCalculator(paint, center, size, White, stroke);
                break;
            case "timer":
                DrawTimer(paint, center, size, White, stroke);
                break;
            case "stopwatch":
                DrawStopwatch(paint, center, size, White, stroke);
                break;
            case "appstore":
                DrawStore(paint, center, size, White, stroke);
                break;
            default:
                if (appletId.StartsWith("folder:", StringComparison.Ordinal))
                {
                    DrawFolder(paint, center, size, White, stroke);
                    break;
                }

                paint.StrokeCircle(center, size * 0.7f, White, stroke);
                break;
        }
    }

    private static void DrawChat(IPaintSurface paint, Vector2 c, float s, Vector4 ink, float stroke)
    {
        var back = Rect.FromSize(c + new Vector2(-s * 0.18f, -s * 0.62f), new Vector2(s * 1.15f, s * 0.82f));
        paint.Stroke(back, ink with { W = 0.72f }, stroke, s * 0.22f);
        var front = Rect.FromSize(c + new Vector2(-s * 0.92f, -s * 0.18f), new Vector2(s * 1.22f, s * 0.88f));
        paint.Stroke(front, ink, stroke, s * 0.22f);
        paint.Line(front.Min + new Vector2(s * 0.18f, front.Height),
            front.Min + new Vector2(s * 0.08f, front.Height + s * 0.28f), ink, stroke);
        paint.Line(front.Min + new Vector2(s * 0.08f, front.Height + s * 0.28f),
            front.Min + new Vector2(s * 0.42f, front.Height - s * 0.02f), ink, stroke);
    }

    private static void DrawClassicHandset(IPaintSurface paint, Vector2 c, float s, Vector4 ink, float stroke)
    {
        const float turn = -0.72f;
        var cos = MathF.Cos(turn);
        var sin = MathF.Sin(turn);
        Vector2 At(float x, float y) => c + new Vector2(x * cos - y * sin, x * sin + y * cos) * s;
        var points = new Vector2[11];
        for (var i = 0; i < points.Length; i++)
        {
            var t = 0.18f * MathF.PI + i * (0.64f * MathF.PI / (points.Length - 1));
            points[i] = At(MathF.Cos(t) * 0.78f, MathF.Sin(t) * 0.62f);
        }

        paint.Polyline(points, ink, stroke * 1.35f, false);
        paint.StrokeCircle(points[0], s * 0.22f, ink, stroke);
        paint.StrokeCircle(points[^1], s * 0.22f, ink, stroke);
    }

    private static void DrawVybeMark(IPaintSurface paint, Vector2 c, float s, Vector4 ink)
    {
        var thickness = s * 0.46f;
        var cap = thickness * 0.5f;
        var left = c + new Vector2(-s * 0.78f, -s * 0.22f);
        var right = c + new Vector2(s * 0.78f, -s * 0.22f);
        var tip = c + new Vector2(0f, s * 0.68f);
        paint.Line(left, tip, ink, thickness);
        paint.Line(right, tip, ink, thickness);
        paint.FillCircle(left, cap, ink);
        paint.FillCircle(right, cap, ink);
        paint.FillCircle(tip, cap, ink);
    }

    private static void DrawPeople(IPaintSurface paint, Vector2 c, float s, Vector4 ink, float stroke)
    {
        paint.StrokeCircle(c + new Vector2(-s * 0.28f, -s * 0.28f), s * 0.28f, ink, stroke);
        paint.StrokeCircle(c + new Vector2(s * 0.32f, -s * 0.22f), s * 0.22f, ink, stroke);
        paint.Stroke(Rect.FromSize(c + new Vector2(-s * 0.72f, s * 0.08f), new Vector2(s * 0.88f, s * 0.58f)), ink,
            stroke, s * 0.42f, Corner.Top);
        paint.Stroke(Rect.FromSize(c + new Vector2(s * 0.02f, s * 0.12f), new Vector2(s * 0.70f, s * 0.48f)), ink,
            stroke, s * 0.36f, Corner.Top);
    }

    private static void DrawChart(IPaintSurface paint, Vector2 c, float s, Vector4 ink, float stroke)
    {
        var a = c + new Vector2(-s * 0.62f, s * 0.38f);
        var b = c + new Vector2(-s * 0.18f, 0.02f * s);
        var d = c + new Vector2(s * 0.18f, s * 0.22f);
        var e = c + new Vector2(s * 0.62f, -s * 0.42f);
        paint.Line(a, b, ink, stroke);
        paint.Line(b, d, ink, stroke);
        paint.Line(d, e, ink, stroke);
        paint.FillCircle(a, s * 0.10f, ink);
        paint.FillCircle(b, s * 0.10f, ink);
        paint.FillCircle(d, s * 0.10f, ink);
        paint.FillCircle(e, s * 0.10f, ink);
    }

    private static void DrawTickets(IPaintSurface paint, Vector2 c, float s, Vector4 ink, float stroke)
    {
        var back = Rect.FromSize(c + new Vector2(-s * 0.22f, -s * 0.62f), new Vector2(s * 1.05f, s * 0.62f));
        paint.Stroke(back, ink with { W = 0.7f }, stroke, s * 0.10f);
        var front = Rect.FromSize(c + new Vector2(-s * 0.85f, -s * 0.12f), new Vector2(s * 1.18f, s * 0.70f));
        paint.Stroke(front, ink, stroke, s * 0.10f);
        paint.Line(front.Min + new Vector2(s * 0.28f, s * 0.12f),
            front.Min + new Vector2(s * 0.28f, front.Height - s * 0.12f), ink, stroke);
        paint.FillCircle(front.Min + new Vector2(s * 0.28f, s * 0.08f), s * 0.09f, ink);
        paint.FillCircle(front.Min + new Vector2(s * 0.28f, front.Height - s * 0.08f), s * 0.09f, ink);
    }

    private static void DrawFolder(IPaintSurface paint, Vector2 c, float s, Vector4 ink, float stroke)
    {
        var tab = Rect.FromSize(c + new Vector2(-s * 0.72f, -s * 0.42f), new Vector2(s * 0.55f, s * 0.28f));
        paint.Stroke(tab, ink, stroke, s * 0.10f, Corner.Top);
        var body = Rect.FromSize(c + new Vector2(-s * 0.78f, -s * 0.22f), new Vector2(s * 1.56f, s * 0.95f));
        paint.Stroke(body, ink, stroke, s * 0.14f);
    }

    private static void DrawStore(IPaintSurface paint, Vector2 c, float s, Vector4 ink, float stroke)
    {
        var gap = s * 0.16f;
        var tile = s * 0.48f;
        var origin = c + new Vector2(-tile - gap * 0.5f, -tile - gap * 0.5f);
        for (var row = 0; row < 2; row++)
        {
            for (var col = 0; col < 2; col++)
            {
                var box = Rect.FromSize(origin + new Vector2(col * (tile + gap), row * (tile + gap)),
                    new Vector2(tile, tile));
                paint.Stroke(box, ink, stroke, s * 0.10f);
            }
        }
    }

    private static void DrawAlarm(IPaintSurface paint, Vector2 c, float s, Vector4 ink, float stroke)
    {
        paint.StrokeCircle(c + new Vector2(0f, s * 0.08f), s * 0.62f, ink, stroke);
        paint.Line(c + new Vector2(0f, s * 0.08f), c + new Vector2(0f, -s * 0.22f), ink, stroke);
        paint.Line(c + new Vector2(0f, s * 0.08f), c + new Vector2(s * 0.32f, s * 0.22f), ink, stroke);
        paint.StrokeCircle(c + new Vector2(-s * 0.48f, -s * 0.52f), s * 0.16f, ink, stroke);
        paint.StrokeCircle(c + new Vector2(s * 0.48f, -s * 0.52f), s * 0.16f, ink, stroke);
        paint.Line(c + new Vector2(-s * 0.22f, s * 0.68f), c + new Vector2(s * 0.22f, s * 0.68f), ink, stroke);
    }

    private static void DrawClock(IPaintSurface paint, Vector2 c, float s, Vector4 ink, float stroke)
    {
        paint.StrokeCircle(c, s * 0.92f, ink, stroke);
        paint.Line(c, c + new Vector2(0f, -s * 0.48f), ink, stroke);
        paint.Line(c, c + new Vector2(s * 0.42f, s * 0.18f), ink, stroke);
    }

    private static void DrawNotes(IPaintSurface paint, Vector2 c, float s, Vector4 ink, float stroke)
    {
        var pad = Rect.FromSize(c - new Vector2(s * 0.62f, s * 0.72f), new Vector2(s * 1.24f, s * 1.44f));
        paint.Stroke(pad, ink, stroke, s * 0.12f);
        paint.Line(c + new Vector2(-s * 0.38f, -s * 0.22f), c + new Vector2(s * 0.38f, -s * 0.22f), ink, stroke);
        paint.Line(c + new Vector2(-s * 0.38f, 0.04f * s), c + new Vector2(s * 0.22f, 0.04f * s), ink, stroke);
        paint.Line(c + new Vector2(-s * 0.38f, s * 0.30f), c + new Vector2(s * 0.10f, s * 0.30f), ink, stroke);
    }

    private static void DrawCalendar(IPaintSurface paint, Vector2 c, float s, Vector4 ink, float stroke)
    {
        var cal = Rect.FromSize(c - new Vector2(s * 0.70f, s * 0.55f), new Vector2(s * 1.40f, s * 1.22f));
        paint.Stroke(cal, ink, stroke, s * 0.16f);
        paint.Line(new Vector2(cal.Min.X + s * 0.16f, cal.Min.Y + s * 0.38f),
            new Vector2(cal.Max.X - s * 0.16f, cal.Min.Y + s * 0.38f), ink, stroke);
        paint.Line(c + new Vector2(-s * 0.38f, -s * 0.78f), c + new Vector2(-s * 0.38f, -s * 0.42f), ink, stroke);
        paint.Line(c + new Vector2(s * 0.38f, -s * 0.78f), c + new Vector2(s * 0.38f, -s * 0.42f), ink, stroke);
        paint.FillCircle(c + new Vector2(-s * 0.22f, s * 0.12f), s * 0.08f, ink);
        paint.FillCircle(c + new Vector2(s * 0.08f, s * 0.12f), s * 0.08f, ink);
        paint.FillCircle(c + new Vector2(s * 0.36f, s * 0.12f), s * 0.08f, ink);
    }

    private static void DrawNote(IPaintSurface paint, Vector2 c, float s, Vector4 ink, float stroke)
    {
        paint.FillCircle(c + new Vector2(-s * 0.28f, s * 0.38f), s * 0.28f, ink);
        paint.Line(c + new Vector2(-s * 0.08f, s * 0.38f), c + new Vector2(-s * 0.08f, -s * 0.62f), ink, stroke);
        paint.Line(c + new Vector2(-s * 0.08f, -s * 0.62f), c + new Vector2(s * 0.55f, -s * 0.42f), ink, stroke);
        paint.Line(c + new Vector2(s * 0.55f, -s * 0.42f), c + new Vector2(s * 0.55f, s * 0.18f), ink, stroke);
        paint.FillCircle(c + new Vector2(s * 0.38f, s * 0.18f), s * 0.22f, ink);
    }

    private static void DrawCalculator(IPaintSurface paint, Vector2 c, float s, Vector4 ink, float stroke)
    {
        var box = Rect.FromSize(c - new Vector2(s * 0.70f, s * 0.78f), new Vector2(s * 1.40f, s * 1.56f));
        paint.Stroke(box, ink, stroke, s * 0.14f);
        var screen = Rect.FromSize(c + new Vector2(-s * 0.48f, -s * 0.58f), new Vector2(s * 0.96f, s * 0.32f));
        paint.Stroke(screen, ink, stroke, s * 0.08f);
        for (var i = 0; i < 4; i++)
        {
            var x = c.X - s * 0.36f + (i % 2) * s * 0.48f;
            var y = c.Y - s * 0.04f + (i / 2) * s * 0.36f;
            paint.StrokeCircle(new Vector2(x, y), s * 0.10f, ink, stroke);
        }
    }

    private static void DrawTimer(IPaintSurface paint, Vector2 c, float s, Vector4 ink, float stroke)
    {
        paint.Line(c + new Vector2(-s * 0.28f, -s * 0.85f), c + new Vector2(s * 0.28f, -s * 0.85f), ink, stroke);
        paint.StrokeCircle(c + new Vector2(0f, s * 0.08f), s * 0.72f, ink, stroke);
        paint.Line(c + new Vector2(0f, s * 0.08f), c + new Vector2(0.22f * s, -s * 0.28f), ink, stroke);
    }

    private static void DrawStopwatch(IPaintSurface paint, Vector2 c, float s, Vector4 ink, float stroke)
    {
        paint.StrokeCircle(c + new Vector2(0f, s * 0.10f), s * 0.70f, ink, stroke);
        paint.Line(c + new Vector2(0f, s * 0.10f), c + new Vector2(s * 0.32f, -s * 0.12f), ink, stroke);
        paint.Stroke(Rect.FromSize(c + new Vector2(-s * 0.18f, -s * 0.88f), new Vector2(s * 0.36f, s * 0.22f)), ink,
            stroke, s * 0.08f);
    }

    private static void DrawWeather(IPaintSurface paint, Vector2 c, float s, Vector4 cloud, Vector4 sun, float stroke)
    {
        paint.StrokeCircle(c + new Vector2(s * 0.28f, -s * 0.28f), s * 0.28f, sun, stroke);
        paint.StrokeCircle(c + new Vector2(-s * 0.28f, s * 0.12f), s * 0.34f, cloud, stroke);
        paint.StrokeCircle(c + new Vector2(s * 0.18f, s * 0.10f), s * 0.40f, cloud, stroke);
        paint.Line(c + new Vector2(-s * 0.55f, s * 0.22f), c + new Vector2(s * 0.52f, s * 0.22f), cloud, stroke);
    }

    private static void DrawCamera(IPaintSurface paint, Vector2 c, float s, Vector4 ink, float stroke)
    {
        var body = Rect.FromSize(c - new Vector2(s * 0.78f, s * 0.38f), new Vector2(s * 1.56f, s * 1.02f));
        paint.Stroke(body, ink, stroke, s * 0.18f);
        paint.Stroke(Rect.FromSize(c + new Vector2(-s * 0.22f, -s * 0.62f), new Vector2(s * 0.44f, s * 0.28f)), ink,
            stroke, s * 0.08f);
        paint.StrokeCircle(c + new Vector2(0.04f * s, 0.08f * s), s * 0.28f, ink, stroke);
        paint.FillCircle(c + new Vector2(0.04f * s, 0.08f * s), s * 0.10f, White);
    }

    private static void DrawWallet(IPaintSurface paint, Vector2 c, float s, Vector4 ink, float stroke)
    {
        var body = Rect.FromSize(c - new Vector2(s * 0.78f, s * 0.48f), new Vector2(s * 1.56f, s * 1.08f));
        paint.Stroke(body, ink, stroke, s * 0.16f);
        paint.Stroke(Rect.FromSize(c + new Vector2(s * 0.18f, -s * 0.12f), new Vector2(s * 0.42f, s * 0.36f)), ink,
            stroke, s * 0.10f);
        paint.FillCircle(c + new Vector2(s * 0.48f, 0.06f * s), s * 0.08f, White);
    }

    private static void DrawGear(IPaintSurface paint, Vector2 c, float s, Vector4 ink, float stroke)
    {
        paint.StrokeCircle(c, s * 0.42f, ink, stroke);
        paint.StrokeCircle(c, s * 0.16f, ink, stroke);
        for (var tooth = 0; tooth < 6; tooth++)
        {
            var angle = tooth * MathF.PI / 3f;
            var dir = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
            paint.Line(c + dir * s * 0.54f, c + dir * s * 0.82f, ink, stroke);
        }
    }
}
