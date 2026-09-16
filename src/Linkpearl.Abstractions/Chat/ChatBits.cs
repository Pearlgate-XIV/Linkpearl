using System.IO;
using Linkpearl.Applets;
using Linkpearl.Emoji;
using Linkpearl.Geometry;
using Linkpearl.Media;
using Linkpearl.Painting;
using Linkpearl.Platform;

namespace Linkpearl.Chat;

public enum ChatBitKind : byte
{
    Text = 0,
    Place = 1,
    Pic = 2,
    Sticker = 3,
    Gif = 4,
}

public readonly record struct ChatBit(ChatBitKind Kind, string Body, string Path);

public readonly record struct ChatCite(string Who, string Preview);

public static class ChatBits
{
    public const string PlaceMark = "📍 ";
    public const string LocMark = "¶loc:";
    public const string PicMark = "¶img:";
    public const string StickerMark = "¶stk:";
    public const string GifMark = "¶gif:";
    public const string RefMark = "¶ref:";

    public static string Place(string zone, string world) =>
        Location(zone, world, 0, 0f, 0f, 0);

    public static string Location(string zone, string world, uint territory, float x, float y, uint aetheryte)
    {
        var here = zone.Length > 0 ? zone : "Unknown zone";
        var home = world.Length > 0 ? world : "";
        return LocMark + here.Replace('|', '/') + "|" + home.Replace('|', '/') + "|" +
               territory.ToString(System.Globalization.CultureInfo.InvariantCulture) + "|" +
               x.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + "|" +
               y.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + "|" +
               aetheryte.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    public static string Pic(string path) => PicMark + path;

    public static string Sticker(string id) => StickerMark + id;

    public static string Gif(string id) => GifMark + id;

    public static bool SplitRef(string body, out string who, out string preview, out string rest)
    {
        who = string.Empty;
        preview = string.Empty;
        rest = body ?? string.Empty;
        if (string.IsNullOrEmpty(body) || !body.StartsWith(RefMark, StringComparison.Ordinal))
        {
            return false;
        }

        var nl = body.IndexOf('\n');
        var head = nl < 0 ? body[RefMark.Length..] : body[RefMark.Length..nl];
        rest = nl < 0 ? string.Empty : body[(nl + 1)..];
        var bar = head.IndexOf('|');
        who = bar < 0 ? head : head[..bar];
        preview = bar < 0 ? string.Empty : head[(bar + 1)..];
        return true;
    }

    public static string ReplyName(string who)
    {
        var name = (who ?? string.Empty).Trim();
        if (name.Length == 0)
        {
            return "Them";
        }

        return string.Equals(name, "ME", StringComparison.OrdinalIgnoreCase) ? "You" : name;
    }

    public static string Visible(string body)
    {
        SplitRef(body, out _, out _, out var rest);
        return rest;
    }

    public static string Snippet(string body, int max = 72)
    {
        SplitRef(body ?? string.Empty, out _, out var quoted, out var rest);
        var source = rest.Length > 0 ? rest : quoted.Length > 0 ? quoted : body ?? string.Empty;
        var bit = Read(source);
        var text = bit.Kind switch
        {
            ChatBitKind.Place => "📍 " + bit.Body,
            ChatBitKind.Pic => "Photo",
            ChatBitKind.Sticker => "Sticker",
            ChatBitKind.Gif => "GIF",
            _ => Collapse(bit.Body),
        };
        if (text.Length <= max)
        {
            return text;
        }

        var take = Math.Max(1, max - 1);
        if (take < text.Length && char.IsLowSurrogate(text[take]))
        {
            take--;
        }

        if (take > 0 && char.IsHighSurrogate(text[take - 1]))
        {
            take--;
        }

        if (take <= 0)
        {
            take = char.IsHighSurrogate(text[0]) && text.Length > 1 ? 2 : 1;
        }

        return string.Concat(text.AsSpan(0, take), "…");
    }

    public static string Reply(string who, string preview, string body)
    {
        var name = ReplyName(who).Replace('|', '/');
        var clip = Snippet(preview ?? string.Empty, 48).Replace('|', '/');
        return RefMark + name + "|" + clip + "\n" + (body ?? string.Empty);
    }

    private static string Collapse(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        return string.Join(' ', value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }

    public static ChatBit Read(string? body)
    {
        SplitRef(body ?? string.Empty, out _, out _, out body);
        if (body.StartsWith(PicMark, StringComparison.Ordinal))
        {
            var path = body[PicMark.Length..];
            return new ChatBit(ChatBitKind.Pic, "Photo", path);
        }

        if (body.StartsWith(StickerMark, StringComparison.Ordinal))
        {
            return new ChatBit(ChatBitKind.Sticker, body[StickerMark.Length..], string.Empty);
        }

        if (body.StartsWith(GifMark, StringComparison.Ordinal))
        {
            return new ChatBit(ChatBitKind.Gif, body[GifMark.Length..].Trim(), string.Empty);
        }

        if (body.StartsWith(LocMark, StringComparison.Ordinal))
        {
            return new ChatBit(ChatBitKind.Place, PlaceLabel(body), body);
        }

        if (body.StartsWith(PlaceMark, StringComparison.Ordinal))
        {
            return new ChatBit(ChatBitKind.Place, body[PlaceMark.Length..], body);
        }

        return new ChatBit(ChatBitKind.Text, body, string.Empty);
    }

    public static string Preview(string? body)
    {
        body ??= string.Empty;
        if (SplitRef(body, out _, out _, out var rest) && rest.Length > 0)
        {
            return Snippet(rest);
        }

        var bit = Read(body);
        return bit.Kind switch
        {
            ChatBitKind.Place => "📍 " + bit.Body,
            ChatBitKind.Pic => "Sent a photo",
            ChatBitKind.Sticker => "Sent a sticker",
            ChatBitKind.Gif => "Sent a GIF",
            _ => body,
        };
    }

    public static float QuoteHeight(in AppletFrame frame) => frame.Units(36f);

    public static float BubbleHeight(in AppletFrame frame, float width, string body, ChatCite cite = default)
    {
        var bodyH = BodyHeight(frame, width, body, cite);
        return Read(Visible(body)).Kind == ChatBitKind.Text ? PadPlain(frame, bodyH) : bodyH;
    }

    /// <summary>
    /// Tell / live-feed rows draw a name band above the bubble and inset the copy.
    /// Those chrome slices are not part of <see cref="BubbleHeight"/>.
    /// </summary>
    public static float NamedRowHeight(in AppletFrame frame, float width, string body, ChatCite cite = default) =>
        frame.Units(32f) + BodyHeight(frame, width, body, cite);

    private static float BodyHeight(in AppletFrame frame, float width, string body, ChatCite cite = default)
    {
        if (TryCite(body, cite, out _, out _, out var rest))
        {
            return QuoteHeight(frame) + BodyHeight(frame, width, rest);
        }

        var bit = Read(body);
        return bit.Kind switch
        {
            ChatBitKind.Pic => StillBox(frame, width * 0.78f, bit.Path).Y,
            ChatBitKind.Gif => GifCache.IsUrl(bit.Body)
                ? StillBox(frame, width * 0.78f, GifCache.PathFor(frame.Paths, bit.Body)).Y
                : frame.Units(118f),
            ChatBitKind.Sticker => frame.Units(88f),
            ChatBitKind.Place => frame.Units(74f),
            _ => EmojiText.MeasureHeight(frame, bit.Body,
                    MathF.Max(frame.Units(40f), width * 0.78f - frame.Units(20f)),
                    EmojiText.Style(bit.Body, Vector4.One)) +
                EmojiText.BubbleExtra(frame, bit.Body),
        };
    }

    private static float PadPlain(in AppletFrame frame, float body) =>
        MathF.Max(frame.Units(38f), body + frame.Units(26f));

    public static Vector2 StillBox(in AppletFrame frame, float maxWidth, string path)
    {
        var side = frame.Units(16f);
        var foot = frame.Units(20f);
        var innerW = MathF.Max(frame.Units(72f), maxWidth - side);
        var maxH = frame.Units(268f);
        var minH = frame.Units(88f);
        if (path.Length > 0 && File.Exists(path))
        {
            var texture = LoadMotion(frame, path);
            if (texture is { IsReady: true } && texture.Size.X > 1f)
            {
                var h = innerW * (texture.Size.Y / texture.Size.X);
                if (h > maxH)
                {
                    h = maxH;
                    innerW = h * (texture.Size.X / texture.Size.Y);
                }

                if (h < minH)
                {
                    h = minH;
                }

                return new Vector2(innerW + side, h + foot);
            }
        }

        return new Vector2(maxWidth, frame.Units(148f));
    }

    public static void Draw(in AppletFrame frame, Rect area, string body, Vector4 ink, Vector4 mute,
        ILifestream? stream = null, IGifDesk? gifs = null, ChatCite cite = default)
    {
        if (TryCite(body, cite, out var who, out var preview, out var rest))
        {
            DrawQuote(frame, area.TopSlice(QuoteHeight(frame)), who, preview, ink, mute);
            Draw(frame, area.Inset(new Edges(0f, QuoteHeight(frame) + frame.Units(2f), 0f, 0f)), rest, ink, mute,
                stream, gifs);
            return;
        }

        var bit = Read(body);
        switch (bit.Kind)
        {
            case ChatBitKind.Place:
                DrawPlace(frame, area, bit, ink, mute, stream);
                return;
            case ChatBitKind.Pic:
                DrawStill(frame, area, bit.Path, "Photo", ink, mute);
                return;
            case ChatBitKind.Gif:
                if (GifCache.IsUrl(bit.Body))
                {
                    gifs?.Ensure(bit.Body);
                    DrawMotion(frame, area, gifs?.PathFor(bit.Body) ?? GifCache.PathFor(frame.Paths, bit.Body),
                        "GIF", ink, mute);
                    return;
                }

                DrawPack(frame, area, ChatPack.Gif(bit.Body), "GIF", ink, mute);
                return;
            case ChatBitKind.Sticker:
                DrawPack(frame, area, ChatPack.Sticker(bit.Body), "Sticker", ink, mute);
                return;
            default:
                EmojiText.Draw(frame, area, bit.Body, ink);
                return;
        }
    }

    public static bool TryPlain(in AppletFrame frame, Rect area, string body, ChatCite cite, out Rect face,
        out string text)
    {
        if (TryCite(body, cite, out _, out _, out var rest))
        {
            return TryPlain(frame, area.Inset(new Edges(0f, QuoteHeight(frame) + frame.Units(2f), 0f, 0f)), rest,
                default, out face, out text);
        }

        var bit = Read(body);
        if (bit.Kind != ChatBitKind.Text || string.IsNullOrEmpty(bit.Body))
        {
            face = default;
            text = string.Empty;
            return false;
        }

        face = area;
        text = bit.Body;
        return true;
    }

    public static bool TryCite(string body, ChatCite cite, out string who, out string preview, out string rest)
    {
        if (!string.IsNullOrEmpty(cite.Who))
        {
            who = ReplyName(cite.Who);
            preview = Snippet(cite.Preview is { Length: > 0 } quoted ? quoted : Visible(body), 80);
            rest = Visible(body);
            return true;
        }

        if (SplitRef(body, out who, out preview, out rest))
        {
            who = ReplyName(who);
            preview = Snippet(preview, 80);
            return true;
        }

        return false;
    }

    public static void DrawQuote(in AppletFrame frame, Rect area, string who, string preview, Vector4 ink,
        Vector4 mute)
    {
        var wash = mute with { W = MathF.Min(0.22f, mute.W * 0.45f + 0.10f) };
        frame.Paint.Fill(area, wash, frame.Units(8f));
        frame.Paint.Fill(area.LeftSlice(frame.Units(3f)), ink with { W = 0.85f }, frame.Units(1.6f));
        var copy = area.Inset(new Edges(frame.Units(10f), frame.Units(3f), frame.Units(6f), frame.Units(3f)));
        frame.Text.DrawEllipsized(copy.TopSlice(frame.Units(14f)), who,
            new TextStyle(FontRole.CaptionStrong, ink));
        frame.Text.DrawEllipsized(copy.BottomSlice(frame.Units(14f)), preview,
            new TextStyle(FontRole.Caption, mute));
    }

    private static void DrawPlace(in AppletFrame frame, Rect area, ChatBit bit, Vector4 ink, Vector4 mute,
        ILifestream? stream)
    {
        TryReadLoc(bit.Path.Length > 0 ? bit.Path : bit.Body, out var loc);
        var place = ZoneHint(loc.Zone.Length > 0 ? loc.Zone : bit.Body);
        var gate = loc.Aetheryte;
        if (gate == 0 && stream is not null && loc.Territory != 0)
        {
            gate = stream.NearestAetheryte(loc.Territory);
        }

        frame.Text.DrawIn(area.TopSlice(frame.Units(14f)), "Location",
            new TextStyle(FontRole.CaptionStrong, mute));
        frame.Text.DrawEllipsized(area.Inset(new Edges(0f, frame.Units(16f), 0f, frame.Units(22f))), bit.Body,
            new TextStyle(FontRole.Body, ink));
        var go = area.BottomSlice(frame.Units(20f)).RightSlice(frame.Units(72f));
        var live = stream is { Ready: true } && (gate != 0 || loc.Territory != 0 || place.Length > 0);
        frame.Paint.Fill(go, live ? new Vector4(0.20f, 0.72f, 0.46f, 0.95f) : mute with { W = 0.22f },
            go.Height * 0.5f);
        frame.Text.DrawIn(go, "Teleport",
            new TextStyle(FontRole.CaptionStrong, live ? Vector4.One : mute, TextAlign.Center));
        if (live && (frame.Input.ConsumeClick(area) || frame.Input.ConsumeClick(go.Expand(frame.Units(6f)))))
        {
            stream!.TryGo(gate, loc.Territory, place);
        }
    }

    public static string ZoneHint(string zone)
    {
        var text = (zone ?? string.Empty).Trim();
        var cut = text.IndexOf('·');
        if (cut >= 0)
        {
            return text[..cut].Trim();
        }

        cut = text.IndexOf(" - ", StringComparison.Ordinal);
        return cut < 0 ? text : text[..cut].Trim();
    }

    public static string PlaceLabel(string body)
    {
        if (!TryReadLoc(body, out var loc))
        {
            return body.StartsWith(PlaceMark, StringComparison.Ordinal) ? body[PlaceMark.Length..] : body;
        }

        var line = loc.Zone;
        if (loc.World.Length > 0)
        {
            line += " · " + loc.World;
        }

        if (loc.X != 0f || loc.Y != 0f)
        {
            line += " · X " + loc.X.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) +
                    "  Y " + loc.Y.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture);
        }

        return line;
    }

    public static bool TryReadLoc(string body, out ChatPlace loc)
    {
        loc = default;
        var raw = body.StartsWith(LocMark, StringComparison.Ordinal) ? body[LocMark.Length..] : string.Empty;
        if (raw.Length == 0)
        {
            return false;
        }

        var parts = raw.Split('|');
        if (parts.Length < 6)
        {
            return false;
        }

        _ = uint.TryParse(parts[2], System.Globalization.NumberStyles.Integer,
            System.Globalization.CultureInfo.InvariantCulture, out var territory);
        _ = float.TryParse(parts[3], System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out var x);
        _ = float.TryParse(parts[4], System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out var y);
        _ = uint.TryParse(parts[5], System.Globalization.NumberStyles.Integer,
            System.Globalization.CultureInfo.InvariantCulture, out var aetheryte);
        loc = new ChatPlace(parts[0], parts[1], territory, x, y, aetheryte);
        return true;
    }

    private static void DrawMotion(in AppletFrame frame, Rect area, string path, string fallback, Vector4 ink,
        Vector4 mute)
    {
        if (path.Length > 0 && File.Exists(path))
        {
            var texture = LoadMotion(frame, path);
            if (texture is { IsReady: true })
            {
                var dest = CoverFit.Contained(texture.Size, area);
                frame.Paint.ImageRounded(texture, dest, Vector2.Zero, Vector2.One, Vector4.One, frame.Units(10f));
                return;
            }
        }

        DrawStill(frame, area, path, fallback, ink, mute);
    }

    private static void DrawStill(in AppletFrame frame, Rect area, string path, string fallback, Vector4 ink,
        Vector4 mute)
    {
        if (path.Length > 0 && File.Exists(path))
        {
            var texture = LoadStill(frame, path);
            if (texture is { IsReady: true })
            {
                var dest = CoverFit.Contained(texture.Size, area);
                frame.Paint.ImageRounded(texture, dest, Vector2.Zero, Vector2.One, Vector4.One, frame.Units(10f));
                return;
            }
        }

        frame.Text.DrawIn(area, fallback, new TextStyle(FontRole.Body, ink));
    }

    public static ITextureHandle? LoadStill(in AppletFrame frame, string path)
    {
        if (path.Length == 0)
        {
            return null;
        }

        var cached = frame.Textures.FromBytes(ReadOnlySpan<byte>.Empty, path);
        if (cached is { IsReady: true })
        {
            return cached;
        }

        if (!File.Exists(path))
        {
            return frame.Textures.FromFile(path);
        }

        try
        {
            var bytes = File.ReadAllBytes(path);
            var handle = frame.Textures.FromBytes(bytes, path);
            if (handle is { IsReady: true })
            {
                return handle;
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }

        return frame.Textures.FromFile(path);
    }

    public static ITextureHandle? LoadMotion(in AppletFrame frame, string path)
    {
        if (path.Length == 0)
        {
            return null;
        }

        var gif = frame.Textures.FromGif(path);
        if (gif is { IsReady: true })
        {
            return gif;
        }

        return LoadStill(frame, path);
    }

    private static void DrawPack(in AppletFrame frame, Rect area, ChatFace face, string kind, Vector4 ink, Vector4 mute)
    {
        if (face.File.Length > 0)
        {
            var path = frame.Paths.Asset(Path.Combine("Icons", "vybe-demo", face.File));
            DrawStill(frame, area, path, face.Label.Length > 0 ? face.Label : kind, ink, mute);
            return;
        }

        frame.Text.DrawIn(area.TopSlice(area.Height - frame.Units(16f)), face.Glyph,
            new TextStyle(FontRole.Display, ink, TextAlign.Center));
        frame.Text.DrawIn(area.BottomSlice(frame.Units(16f)), face.Label.Length > 0 ? face.Label : kind,
            new TextStyle(FontRole.Caption, mute, TextAlign.Center));
    }
}

public readonly record struct ChatPlace(string Zone, string World, uint Territory, float X, float Y, uint Aetheryte);
