using Dalamud.Bindings.ImGui;
using Linkpearl.Geometry;
using Linkpearl.Painting;

namespace Linkpearl.Canvas.Text;

public sealed class DalamudTextPainter : ITextPainter
{
    private readonly HandsetFontService fonts;
    private readonly ImDrawListPtr drawList;

    public DalamudTextPainter(HandsetFontService fonts, ImDrawListPtr drawList)
    {
        this.fonts = fonts;
        this.drawList = drawList;
    }

    public Vector2 Measure(ReadOnlySpan<char> text, FontRole role)
    {
        using var pushed = fonts.Handle(role).Push();
        return ImGui.CalcTextSize(text);
    }

    public Vector2 MeasureWrapped(ReadOnlySpan<char> text, FontRole role, float wrapWidth)
    {
        using var pushed = fonts.Handle(role).Push();
        return ImGui.CalcTextSize(text, false, wrapWidth);
    }

    public float LineHeight(FontRole role)
    {
        using var pushed = fonts.Handle(role).Push();
        return ImGui.GetTextLineHeightWithSpacing();
    }

    public void Draw(Vector2 origin, ReadOnlySpan<char> text, in TextStyle style)
    {
        using var pushed = fonts.Handle(style.Role).Push();
        var size = MeasureScaled(text, style.Scale);
        var aligned = AlignedOrigin(origin, size, style.Align, origin.X);
        DrawGlyphs(aligned, text, style);
    }

    public void DrawIn(Rect area, ReadOnlySpan<char> text, in TextStyle style)
    {
        if (area.Width < 1f || area.Height < 1f)
        {
            return;
        }

        using var pushed = fonts.Handle(style.Role).Push();
        var size = MeasureScaled(text, style.Scale);
        var origin = FittedOrigin(area, size, style.Align);
        ClipTo(area);
        DrawGlyphs(origin, text, style);
        drawList.PopClipRect();
    }

    public void DrawWrapped(Rect area, ReadOnlySpan<char> text, in TextStyle style)
    {
        if (area.Width < 1f || area.Height < 1f)
        {
            return;
        }

        using var pushed = fonts.Handle(style.Role).Push();
        ClipTo(area);
        if (style.Scale == 1f)
        {
            drawList.AddText(ImGui.GetFont(), ImGui.GetFontSize(), area.Min, ImGui.GetColorU32(style.Color), text,
                area.Width);
        }
        else
        {
            drawList.AddText(ImGui.GetFont(), ImGui.GetFontSize() * style.Scale, area.Min,
                ImGui.GetColorU32(style.Color), text, area.Width);
        }

        drawList.PopClipRect();
    }

    public void DrawEllipsized(Rect area, ReadOnlySpan<char> text, in TextStyle style)
    {
        if (area.Width < 1f || area.Height < 1f)
        {
            return;
        }

        using var pushed = fonts.Handle(style.Role).Push();
        ClipTo(area);
        try
        {
            var size = MeasureScaled(text, style.Scale);
            if (size.X <= area.Width)
            {
                DrawGlyphs(FittedOrigin(area, size, style.Align), text, style);
                return;
            }

            const string ellipsis = "...";
            var low = 0;
            var high = text.Length;
            while (low < high)
            {
                var mid = FitChars(text, (low + high + 1) / 2);
                if (mid <= low)
                {
                    mid = NextChars(text, low);
                    if (mid <= low)
                    {
                        break;
                    }
                }

                var candidateSize = MeasureScaled(string.Concat(Prefix(text, mid), ellipsis), style.Scale);
                if (candidateSize.X <= area.Width)
                {
                    low = mid;
                }
                else
                {
                    high = mid > 0 ? FitChars(text, mid - 1) : 0;
                }
            }

            var truncated = string.Concat(Prefix(text, low), ellipsis);
            var truncatedSize = MeasureScaled(truncated, style.Scale);
            DrawGlyphs(FittedOrigin(area, truncatedSize, style.Align), truncated, style);
        }
        catch (ArgumentException)
        {
            DrawGlyphs(area.Min, text, style);
        }
        finally
        {
            drawList.PopClipRect();
        }
    }

    public void DrawFitted(Rect area, ReadOnlySpan<char> text, in TextStyle style)
    {
        if (area.Width < 1f || area.Height < 1f || text.Length == 0)
        {
            return;
        }

        using var pushed = fonts.Handle(style.Role).Push();
        var measured = ImGui.CalcTextSize(text);
        if (measured.X < 1f || measured.Y < 1f)
        {
            return;
        }

        var fit = MathF.Min(area.Width / measured.X, area.Height / measured.Y);
        var scale = MathF.Min(1f, fit) * (style.Scale > 0f ? style.Scale : 1f);

        var size = measured * scale;
        var origin = FittedOrigin(area, size, style.Align);
        var fitted = new TextStyle(style.Role, style.Color, style.Align, style.LineSpacing, scale, style.Glow,
            style.GlowSpread);
        DrawGlyphs(origin, text, fitted);
    }

    private void DrawGlyphs(Vector2 origin, ReadOnlySpan<char> text, in TextStyle style)
    {
        DrawHalo(origin, text, style);
        var color = ImGui.GetColorU32(style.Color);
        if (style.Scale == 1f)
        {
            drawList.AddText(origin, color, text);
            return;
        }

        drawList.AddText(ImGui.GetFont(), ImGui.GetFontSize() * style.Scale, origin, color, text);
    }

    private void DrawHalo(Vector2 origin, ReadOnlySpan<char> text, in TextStyle style)
    {
        if (style.GlowSpread <= 0.15f || style.Glow.W <= 0.02f || text.Length == 0)
        {
            return;
        }

        var font = ImGui.GetFont();
        var size = ImGui.GetFontSize() * (style.Scale > 0f ? style.Scale : 1f);
        var spread = MathF.Max(style.GlowSpread, 1.6f);
        var strength = Math.Clamp(style.Glow.W, 0f, 1f);
        var measured = ImGui.CalcTextSize(text);
        var bloomSize = size + spread * 1.35f;
        var grow = bloomSize / MathF.Max(size, 1f) - 1f;
        var bloomOrigin = origin - measured * (style.Scale > 0f ? style.Scale : 1f) * grow * 0.5f;
        drawList.AddText(font, bloomSize, bloomOrigin, ImGui.GetColorU32(style.Glow with { W = strength * 0.28f }),
            text);

        const int spokes = 16;
        var rim = ImGui.GetColorU32(style.Glow with { W = strength * 0.42f });
        for (var spoke = 0; spoke < spokes; spoke++)
        {
            var angle = spoke * (MathF.PI * 2f / spokes);
            var stamp = origin + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * spread;
            drawList.AddText(font, size, stamp, rim, text);
        }
    }

    private void ClipTo(Rect area)
    {
        var pad = ImGui.GetFontSize() * 0.2f;
        drawList.PushClipRect(new Vector2(area.Min.X, area.Min.Y - pad),
            new Vector2(area.Max.X, area.Max.Y + pad), true);
    }

    private static Vector2 FittedOrigin(Rect area, Vector2 size, TextAlign align)
    {
        var y = area.Center.Y - size.Y * 0.5f;
        var floor = area.Max.Y - size.Y;
        y = floor < area.Min.Y ? area.Min.Y : Math.Clamp(y, area.Min.Y, floor);
        return new Vector2(AlignedX(area, size.X, align), y);
    }

    private static int FitChars(ReadOnlySpan<char> text, int count)
    {
        count = Math.Clamp(count, 0, text.Length);
        if (count > 0 && count < text.Length && char.IsLowSurrogate(text[count]))
        {
            count--;
        }

        if (count > 0 && char.IsHighSurrogate(text[count - 1]))
        {
            count--;
        }

        return count;
    }

    private static int NextChars(ReadOnlySpan<char> text, int from)
    {
        if (from >= text.Length)
        {
            return from;
        }

        var next = from + 1;
        if (from < text.Length && char.IsHighSurrogate(text[from]) && next < text.Length &&
            char.IsLowSurrogate(text[next]))
        {
            next++;
        }

        return next;
    }

    private static string Prefix(ReadOnlySpan<char> text, int count)
    {
        count = FitChars(text, count);
        return count <= 0 ? string.Empty : text[..count].ToString();
    }

    private static Vector2 MeasureScaled(ReadOnlySpan<char> text, float scale)
    {
        return ImGui.CalcTextSize(text) * (scale > 0f ? scale : 1f);
    }

    private static Vector2 AlignedOrigin(Vector2 anchor, Vector2 size, TextAlign align, float left) => align switch
    {
        // Center is the only alignment anything currently draws at a bare point rather than
        // into a Rect (badges, glyph labels) — for that usage "centered at this point" means
        // both axes, not just horizontal.
        TextAlign.Center => anchor - size * 0.5f,
        TextAlign.Right => new Vector2(anchor.X - size.X, anchor.Y),
        _ => anchor,
    };

    private static float AlignedX(Rect area, float width, TextAlign align) => align switch
    {
        TextAlign.Center => area.Center.X - width * 0.5f,
        TextAlign.Right => area.Max.X - width,
        _ => area.Min.X,
    };
}
