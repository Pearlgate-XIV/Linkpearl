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
        var size = ImGui.CalcTextSize(text);
        var aligned = AlignedOrigin(origin, size, style.Align, origin.X);
        drawList.AddText(aligned, ImGui.GetColorU32(style.Color), text);
    }

    public void DrawIn(Rect area, ReadOnlySpan<char> text, in TextStyle style)
    {
        using var pushed = fonts.Handle(style.Role).Push();
        var size = ImGui.CalcTextSize(text);
        var origin = new Vector2(AlignedX(area, size.X, style.Align), area.Center.Y - size.Y * 0.5f);
        drawList.AddText(origin, ImGui.GetColorU32(style.Color), text);
    }

    public void DrawWrapped(Rect area, ReadOnlySpan<char> text, in TextStyle style)
    {
        using var pushed = fonts.Handle(style.Role).Push();
        drawList.AddText(ImGui.GetFont(), ImGui.GetFontSize(), area.Min, ImGui.GetColorU32(style.Color), text,
            area.Width);
    }

    public void DrawEllipsized(Rect area, ReadOnlySpan<char> text, in TextStyle style)
    {
        using var pushed = fonts.Handle(style.Role).Push();
        var size = ImGui.CalcTextSize(text);
        if (size.X <= area.Width)
        {
            DrawIn(area, text, style);
            return;
        }

        const string ellipsis = "...";
        var low = 0;
        var high = text.Length;
        while (low < high)
        {
            var mid = (low + high + 1) / 2;
            var candidateSize = ImGui.CalcTextSize(string.Concat(text[..mid], ellipsis));
            if (candidateSize.X <= area.Width)
            {
                low = mid;
            }
            else
            {
                high = mid - 1;
            }
        }

        var truncated = string.Concat(text[..low], ellipsis);
        var truncatedSize = ImGui.CalcTextSize(truncated);
        var origin = new Vector2(AlignedX(area, truncatedSize.X, style.Align), area.Center.Y - truncatedSize.Y * 0.5f);
        drawList.AddText(origin, ImGui.GetColorU32(style.Color), truncated);
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
