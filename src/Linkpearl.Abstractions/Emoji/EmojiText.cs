using System.Globalization;
using Linkpearl.Applets;
using Linkpearl.Geometry;
using Linkpearl.Modules;
using Linkpearl.Painting;
using Linkpearl.Platform;

namespace Linkpearl.Emoji;

public static class EmojiText
{
    public static TextStyle Style(string body, Vector4 ink)
    {
        var count = EmojiBits.OnlyCount(body);
        if (count is >= 1 and <= 3)
        {
            return new TextStyle(FontRole.Title, ink, TextAlign.Left, 1.05f, 1.28f);
        }

        if (count >= 4)
        {
            return new TextStyle(FontRole.Body, ink, TextAlign.Left, 1.05f, 1.12f);
        }

        return new TextStyle(FontRole.Body, ink);
    }

    public static float BubbleExtra(in AppletFrame frame, string body)
    {
        var count = EmojiBits.OnlyCount(body);
        return count is >= 1 and <= 3 ? frame.Units(10f) : 0f;
    }

    public static float MeasureHeight(in AppletFrame frame, string body, float width, TextStyle style)
    {
        var height = 0f;
        var used = 0f;
        Walk(frame.Paint, frame.Text, frame.Textures, frame.Paths, body, width, style, false, ref height,
            ref used, default, false);
        return MathF.Max(height, frame.Text.LineHeight(style.Role) * style.Scale);
    }

    public static void Draw(in AppletFrame frame, Rect area, string body, Vector4 ink)
    {
        DrawStyled(frame, area, body, Style(body, ink), false);
    }

    public static void DrawEllipsized(in AppletFrame frame, Rect area, string body, Vector4 ink)
    {
        DrawStyled(frame, area, body, new TextStyle(FontRole.Caption, ink), true);
    }

    public static void DrawField(IPaintSurface paint, ITextPainter text, ITextureSource textures, HostPaths paths,
        Rect area, string body, string placeholder, Vector4 ink, float padX, bool focused, bool caret)
    {
        if (area.Width < 1f || area.Height < 1f)
        {
            return;
        }

        var empty = body.Length == 0;
        var hint = empty && !focused;
        var shown = hint ? placeholder : body;
        var color = hint ? ink with { W = ink.W * 0.42f } : ink;
        var style = new TextStyle(FontRole.Body, color);
        var inner = MathF.Max(area.Width - padX * 2f, 1f);
        var wide = MeasureWidth(paint, text, textures, paths, shown, style);
        var scroll = !empty && wide > inner ? wide - inner : 0f;
        var face = MathF.Max(text.Measure("Ag", style.Role).Y, 1f);
        var textY = area.Min.Y + MathF.Max(0f, (area.Height - face) * 0.5f);
        var origin = new Vector2(area.Min.X + padX - scroll, textY);
        var height = 0f;
        var used = 0f;
        paint.PushClip(area);
        Walk(paint, text, textures, paths, shown, MathF.Max(wide, inner), style, true, ref height, ref used, origin,
            true);
        if (caret)
        {
            var caretH = MathF.Max(10f, face * 0.82f);
            var x = Math.Clamp(empty ? origin.X : origin.X + wide + 1f, area.Min.X + 1f, area.Max.X - 2f);
            var top = textY + MathF.Max(0f, (face - caretH) * 0.5f);
            paint.Line(new Vector2(x, top), new Vector2(x, top + caretH), ink, 1.2f);
        }

        paint.PopClip();
    }

    public static float MeasureWidth(IPaintSurface paint, ITextPainter text, ITextureSource textures, HostPaths paths,
        string body, TextStyle style)
    {
        var height = 0f;
        var used = 0f;
        Walk(paint, text, textures, paths, body, 10_000f, style, false, ref height, ref used, default, true);
        return used;
    }

    private static void DrawStyled(in AppletFrame frame, Rect area, string body, TextStyle style, bool oneLine)
    {
        if (area.Width < 1f || area.Height < 1f)
        {
            return;
        }

        var clipped = false;
        try
        {
            var height = 0f;
            var used = 0f;
            frame.Paint.PushClip(area);
            clipped = true;
            Walk(frame.Paint, frame.Text, frame.Textures, frame.Paths, body, area.Width, style, true, ref height,
                ref used, area.Min, oneLine);
        }
        catch (Exception)
        {
            frame.Text.DrawWrapped(area, body ?? string.Empty, new TextStyle(style.Role, style.Color));
        }
        finally
        {
            if (clipped)
            {
                frame.Paint.PopClip();
            }
        }
    }

    private static void Walk(IPaintSurface paint, ITextPainter text, ITextureSource textures, HostPaths paths,
        string body, float width, TextStyle style, bool draw, ref float height, ref float used, Vector2 origin,
        bool oneLine)
    {
        var face = MathF.Max(14f, text.LineHeight(style.Role) * style.Scale);
        var x = 0f;
        var y = 0f;
        var line = face;
        var dots = false;
        var walk = StringInfo.GetTextElementEnumerator(body ?? string.Empty);
        var pending = string.Empty;
        while (walk.MoveNext())
        {
            var part = walk.GetTextElement();
            if (EmojiBits.LooksEmoji(part))
            {
                PlaceText(paint, text, textures, paths, pending, width, style, draw, origin, face, oneLine,
                    ref x, ref y, ref line, ref dots);
                pending = string.Empty;
                PlaceFace(paint, text, textures, paths, part, width, style, draw, origin, face, oneLine,
                    ref x, ref y, ref line, ref dots);
                continue;
            }

            pending += part;
        }

        PlaceText(paint, text, textures, paths, pending, width, style, draw, origin, face, oneLine,
            ref x, ref y, ref line, ref dots);
        height = y + line;
        used = x;
    }

    private static void PlaceFace(IPaintSurface paint, ITextPainter text, ITextureSource textures, HostPaths paths,
        string glyph, float width, TextStyle style, bool draw, Vector2 origin, float face, bool oneLine,
        ref float x, ref float y, ref float line, ref bool dots)
    {
        if (oneLine && dots)
        {
            return;
        }

        if (x > 0f && x + face > width)
        {
            if (oneLine)
            {
                PlaceDots(text, style, draw, origin, x, y, ref dots);
                return;
            }

            x = 0f;
            y += line;
            line = face;
        }

        if (draw)
        {
            var dest = Rect.FromSize(origin + new Vector2(x, y), new Vector2(face, face));
            if (!EmojiArt.TryDraw(paint, textures, paths, dest, glyph))
            {
                text.DrawIn(dest, glyph, new TextStyle(style.Role, style.Color, TextAlign.Center));
            }
        }

        x += face;
    }

    private static void PlaceText(IPaintSurface paint, ITextPainter text, ITextureSource textures, HostPaths paths,
        string value, float width, TextStyle style, bool draw, Vector2 origin, float face, bool oneLine,
        ref float x, ref float y, ref float line, ref bool dots)
    {
        _ = paint;
        _ = textures;
        _ = paths;
        if (value.Length == 0 || (oneLine && dots))
        {
            return;
        }

        var start = 0;
        while (start < value.Length)
        {
            if (value[start] == '\r')
            {
                start++;
                continue;
            }

            if (value[start] == '\n')
            {
                if (oneLine)
                {
                    PlaceDots(text, style, draw, origin, x, y, ref dots);
                    return;
                }

                start++;
                x = 0f;
                y += line;
                line = face;
                continue;
            }

            if (x == 0f)
            {
                while (start < value.Length && value[start] == ' ')
                {
                    start++;
                }

                if (start >= value.Length)
                {
                    break;
                }

                if (value[start] is '\n' or '\r')
                {
                    continue;
                }
            }

            var take = value.Length - start;
            var slice = value.AsSpan(start, take);
            var size = text.Measure(slice, style.Role) * style.Scale;
            if (x + size.X > width && take > 0)
            {
                var fit = FitLine(text, value, start, width - x, x <= 0f || oneLine, style);
                if (fit == 0)
                {
                    if (oneLine)
                    {
                        PlaceDots(text, style, draw, origin, x, y, ref dots);
                        return;
                    }

                    x = 0f;
                    y += line;
                    line = face;
                    continue;
                }

                take = fit;
                slice = value.AsSpan(start, take);
                size = text.Measure(slice, style.Role) * style.Scale;
            }

            line = MathF.Max(line, size.Y);
            if (draw)
            {
                text.Draw(origin + new Vector2(x, y + MathF.Max(0f, (line - size.Y) * 0.5f)), slice,
                    new TextStyle(style.Role, style.Color, TextAlign.Left, style.LineSpacing, style.Scale));
            }

            x += size.X;
            start += take;
            if (start < value.Length)
            {
                if (oneLine)
                {
                    PlaceDots(text, style, draw, origin, x, y, ref dots);
                    return;
                }

                x = 0f;
                y += line;
                line = face;
            }
        }
    }

    private static void PlaceDots(ITextPainter text, TextStyle style, bool draw, Vector2 origin, float x, float y,
        ref bool dots)
    {
        if (dots)
        {
            return;
        }

        dots = true;
        if (draw)
        {
            text.Draw(origin + new Vector2(x, y), "...",
                new TextStyle(style.Role, style.Color, TextAlign.Left, style.Scale));
        }
    }

    private static int FitLine(ITextPainter text, string value, int start, float room, bool allowHardWrap,
        TextStyle style)
    {
        if (room <= 0f || start >= value.Length)
        {
            return 0;
        }

        if (value[start] is '\n' or '\r')
        {
            return 0;
        }

        var fit = Fit(text, value, start, room, style);
        if (fit == 0)
        {
            return 0;
        }

        for (var index = start; index < start + fit; index++)
        {
            if (value[index] is '\n' or '\r')
            {
                return index - start;
            }
        }

        if (start + fit >= value.Length)
        {
            return fit;
        }

        var next = start + fit;
        if (IsWrapSpace(value[next]) || IsWrapSpace(value[next - 1]))
        {
            return fit;
        }

        var last = LastWrap(value, start, next);
        if (last > start)
        {
            return last - start;
        }

        return allowHardWrap ? Math.Max(1, fit) : 0;
    }

    private static bool IsWrapSpace(char c) => char.IsWhiteSpace(c);

    private static int LastWrap(string value, int start, int end)
    {
        for (var index = end - 1; index >= start; index--)
        {
            if (IsWrapSpace(value[index]))
            {
                return index + 1;
            }
        }

        return start;
    }

    private static int Fit(ITextPainter text, string value, int start, float room, TextStyle style)
    {
        if (room <= 0f)
        {
            return 0;
        }

        var low = 0;
        var high = value.Length - start;
        while (low < high)
        {
            var mid = (low + high + 1) / 2;
            var size = text.Measure(value.AsSpan(start, mid), style.Role) * style.Scale;
            if (size.X <= room)
            {
                low = mid;
            }
            else
            {
                high = mid - 1;
            }
        }

        return low;
    }
}
