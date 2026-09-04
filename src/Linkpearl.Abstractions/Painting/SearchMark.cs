using Linkpearl.Geometry;

namespace Linkpearl.Painting;

// Magnifying glass for search fields. Drawn, not a font glyph — Inter has no ⌕ and
// substitutes a question mark.
public static class SearchMark
{
    public static void Draw(IPaintSurface paint, Rect area, Vector4 color)
    {
        var span = MathF.Min(area.Width, area.Height);
        if (span < 4f)
        {
            return;
        }

        var stroke = MathF.Max(span * 0.13f, 1.5f);
        var lens = span * 0.22f;
        var center = area.Center + new Vector2(-span * 0.08f, -span * 0.08f);
        paint.StrokeCircle(center, lens, color, stroke);
        var along = Vector2.Normalize(new Vector2(1f, 1f));
        var from = center + along * (lens + stroke * 0.12f);
        var to = from + along * (span * 0.26f);
        paint.Line(from, to, color, stroke);
        paint.FillCircle(to, stroke * 0.5f, color);
    }
}
