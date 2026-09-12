using Linkpearl.Applets;
using Linkpearl.Geometry;
using Linkpearl.Painting;

namespace Linkpearl.Preferences;

public static class NameMark
{
    public static void DrawName(in AppletFrame frame, Rect area, string name, DisplayPreferences display,
        Vector4 fallbackInk, bool fancy, float clock)
    {
        if (area.Width < 1f || name.Length == 0)
        {
            return;
        }

        if (!fancy || !(display.NameGlow || display.NameInkCustom))
        {
            frame.Text.DrawEllipsized(area, name, new TextStyle(FontRole.Display, fallbackInk));
            return;
        }

        Draw(frame, area, name, MarkLook.ForName(display, fallbackInk), FontRole.Display, clock);
    }

    public static void DrawTitle(in AppletFrame frame, Rect area, string title, DisplayPreferences display,
        Vector4 fallbackInk, bool fancy, float clock)
    {
        if (area.Width < 1f || title.Length == 0)
        {
            return;
        }

        _ = fallbackInk;
        if (!fancy)
        {
            frame.Text.DrawEllipsized(area, title, new TextStyle(FontRole.CaptionStrong, Vector4.One));
            return;
        }

        Draw(frame, area, title, MarkLook.ForTitle(display), FontRole.CaptionStrong, clock);
    }

    public static void Draw(in AppletFrame frame, Rect area, string text, in MarkLook look, FontRole role,
        float clock, float markScale = 1f)
    {
        if (area.Width < 1f || text.Length == 0)
        {
            return;
        }

        var unit = frame.Units(1f);
        var width = 0f;
        var count = 0;
        foreach (var rune in text.EnumerateRunes())
        {
            width += frame.Text.Measure(rune.ToString(), role).X * markScale;
            count++;
        }

        if (width < 1f || count == 0)
        {
            return;
        }

        var scale = markScale * MathF.Min(1f, area.Width / width);
        var halo = look.Glow ? TitleFx.Spread(look, unit) * 2.6f : 0f;
        frame.Paint.PushClip(area.Expand(halo));
        var x = area.Min.X;
        var index = 0;
        foreach (var rune in text.EnumerateRunes())
        {
            var glyph = rune.ToString();
            var size = frame.Text.Measure(glyph, role) * scale;
            var cell = Rect.FromSize(new Vector2(x, area.Min.Y), new Vector2(size.X, area.Height));
            var fill = TitleFx.Fill(look, clock, index, count) with { W = 1f };
            var glow = TitleFx.Glow(look, clock, index, count);
            frame.Text.DrawFitted(cell, glyph,
                new TextStyle(role, fill, TextAlign.Left, 1f, scale, glow, TitleFx.Spread(look, unit)));
            x += size.X;
            index++;
        }

        frame.Paint.PopClip();
    }
}
