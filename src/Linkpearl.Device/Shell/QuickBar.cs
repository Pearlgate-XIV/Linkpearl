using Linkpearl.Geometry;
using Linkpearl.Painting;
using Linkpearl.Theming;

namespace Linkpearl.Device.Shell;

// For things that need attention right now (duty ready, an incoming tell, a party invite) —
// not a permanent dock. When there is nothing urgent this occupies zero height, so Home's
// dashboard content simply grows to fill the space rather than leaving a bar of empty chrome.
public static class QuickBar
{
    private const float HeightUnits = 40f;

    public static float Height(float scale, int itemCount) => itemCount > 0 ? HeightUnits * scale : 0f;

    public static void Draw(IPaintSurface paint, ITextPainter text, ITheme theme, Rect area, float scale,
        IReadOnlyList<QuickBarItem> items)
    {
        if (items.Count == 0)
        {
            return;
        }

        var gap = 8f * scale;
        var chipHeight = area.Height;
        var cursorX = area.Min.X;
        for (var index = 0; index < items.Count; index++)
        {
            var item = items[index];
            var label = $"{item.Glyph} {item.Label}";
            var textWidth = text.Measure(label, FontRole.Caption).X;
            var chipWidth = textWidth + 20f * scale;
            var chip = new Rect(new Vector2(cursorX, area.Min.Y), new Vector2(cursorX + chipWidth, area.Max.Y));

            paint.Fill(chip, theme.Palette.SurfaceOverlay, chipHeight * 0.5f);
            paint.Stroke(chip, theme.Palette.Separator, theme.Metrics.Hairline, chipHeight * 0.5f);
            text.DrawIn(chip, label, new TextStyle(FontRole.Caption, theme.Palette.Ink, TextAlign.Center));

            cursorX += chipWidth + gap;
        }
    }
}
