using System.Globalization;
using Linkpearl.Applets;
using Linkpearl.Geometry;
using Linkpearl.Modules;
using Linkpearl.Painting;
using Linkpearl.Platform;

namespace Linkpearl.Emoji;

public static class EmojiArt
{
    public const string Smile = "😀";
    public const string Folder = "Icons/emoji/noto";

    private static readonly Dictionary<string, string?> Files = new(StringComparer.Ordinal);

    public static IEnumerable<string> Needed()
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        void Take(string glyph)
        {
            if (glyph.Length > 0)
            {
                seen.Add(glyph);
            }
        }

        Take(Smile);
        for (var index = 0; index < EmojiShelf.Groups.Length; index++)
        {
            Take(EmojiShelf.Groups[index].Mark);
        }

        for (var index = 0; index < EmojiShelf.All.Count; index++)
        {
            var mark = EmojiShelf.All[index];
            Take(mark.Value);
            if (!mark.Tones)
            {
                continue;
            }

            for (var tone = 1; tone < EmojiBits.Tones.Length; tone++)
            {
                Take(EmojiBits.WithTone(mark.Value, EmojiBits.Tones[tone]));
            }
        }

        return seen;
    }

    public static IEnumerable<string> Stems(string glyph)
    {
        var skipVs = Stem(glyph, false);
        if (skipVs.Length > 0)
        {
            yield return skipVs;
        }

        var keepVs = Stem(glyph, true);
        if (keepVs.Length > 0 && !string.Equals(keepVs, skipVs, StringComparison.Ordinal))
        {
            yield return keepVs;
        }
    }

    public static string? FileOf(HostPaths paths, string glyph)
    {
        if (glyph.Length == 0)
        {
            return null;
        }

        lock (Files)
        {
            if (Files.TryGetValue(glyph, out var hit))
            {
                return hit;
            }

            foreach (var stem in Stems(glyph))
            {
                var path = paths.Asset(Path.Combine("Icons", "emoji", "noto", stem + ".png"));
                if (File.Exists(path))
                {
                    Files[glyph] = path;
                    return path;
                }
            }

            Files[glyph] = null;
            return null;
        }
    }

    public static bool TryDraw(in AppletFrame frame, Rect area, string glyph) =>
        TryDraw(frame.Paint, frame.Textures, frame.Paths, area, glyph);

    public static bool TryDraw(IPaintSurface paint, ITextureSource textures, HostPaths paths, Rect area, string glyph)
    {
        var path = FileOf(paths, glyph);
        if (path is null)
        {
            return false;
        }

        var texture = textures.FromFile(path, area.Size);
        if (texture is not { IsReady: true })
        {
            return false;
        }

        var side = MathF.Min(area.Width, area.Height);
        if (side <= 0f)
        {
            return true;
        }

        var dest = Rect.FromSize(area.Center - new Vector2(side * 0.5f, side * 0.5f), new Vector2(side, side));
        paint.Image(texture, dest, Vector4.One);
        return true;
    }

    public static void DrawOrMark(in AppletFrame frame, Rect area, string glyph, Vector4 ink)
    {
        if (TryDraw(frame, area, glyph))
        {
            return;
        }

        frame.Text.DrawIn(area, glyph, new TextStyle(FontRole.Title, ink, TextAlign.Center, 1f, 1.12f));
    }

    public static void DrawSmile(in AppletFrame frame, Rect area, Vector4 ink)
    {
        if (TryDraw(frame, area, Smile))
        {
            return;
        }

        var radius = MathF.Min(area.Width, area.Height) * 0.38f;
        var mid = area.Center;
        var stroke = MathF.Max(1.2f, frame.Units(1.5f));
        frame.Paint.StrokeCircle(mid, radius, ink, stroke);
        var eye = radius * 0.12f;
        frame.Paint.FillCircle(mid + new Vector2(-radius * 0.32f, -radius * 0.18f), eye, ink);
        frame.Paint.FillCircle(mid + new Vector2(radius * 0.32f, -radius * 0.18f), eye, ink);
        frame.Paint.Line(mid + new Vector2(-radius * 0.28f, radius * 0.22f),
            mid + new Vector2(0f, radius * 0.38f), ink, stroke);
        frame.Paint.Line(mid + new Vector2(0f, radius * 0.38f),
            mid + new Vector2(radius * 0.28f, radius * 0.22f), ink, stroke);
    }

    private static string Stem(string glyph, bool keepVs)
    {
        var parts = new List<string>(8);
        foreach (var rune in glyph.EnumerateRunes())
        {
            if (!keepVs && rune.Value == 0xFE0F)
            {
                continue;
            }

            parts.Add(rune.Value.ToString("x", CultureInfo.InvariantCulture));
        }

        return parts.Count == 0 ? string.Empty : "emoji_u" + string.Join('_', parts);
    }
}
