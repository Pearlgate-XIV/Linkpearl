using Linkpearl.Destinations;
using Linkpearl.Geometry;
using Linkpearl.Input;
using Linkpearl.Painting;
using Linkpearl.Theming;

namespace Linkpearl.Device.Shell;

// The signature Linkpearl/crystal interaction: tap the crystal, search across the whole phone.
// Results here come from DemoData.Search — a small in-memory placeholder index, not a live
// backend (none exists yet). The visual interaction is real; the data behind it is labelled
// demo content, matching the reference's own instruction not to fake backend functionality.
public sealed class UniversalSearchOverlay
{
    private string query = string.Empty;

    public bool IsOpen { get; private set; }

    public void Open() => IsOpen = true;

    public void Close()
    {
        IsOpen = false;
        query = string.Empty;
    }

    public void Draw(IPaintSurface paint, ITextPainter text, ITextField textField, IInputProbe input, ITheme theme,
        Rect screen, float scale)
    {
        if (!IsOpen)
        {
            return;
        }

        paint.Fill(screen, theme.Palette.SurfaceSunken with { W = 0.92f });

        var panel = screen.Inset(scale * 14f).TopSlice(screen.Height - scale * 28f);
        if (input.WasClicked(screen) && !panel.Contains(input.Pointer))
        {
            Close();
            return;
        }

        paint.Fill(panel, theme.Palette.SurfaceOverlay, scale * 16f);
        paint.Stroke(panel, theme.Palette.Separator, theme.Metrics.Hairline, scale * 16f);

        var inset = panel.Inset(scale * 14f);
        var closeButton = inset.TopSlice(scale * 22f).RightSlice(scale * 22f);
        text.DrawIn(closeButton, "✕", new TextStyle(FontRole.BodyStrong, theme.Palette.InkMuted, TextAlign.Center));
        if (input.ConsumeClick(closeButton))
        {
            Close();
            return;
        }

        var fieldArea = new Rect(inset.Min, new Vector2(closeButton.Min.X - scale * 8f, inset.Min.Y + scale * 28f));
        query = textField.Draw("universal-search", fieldArea, query, "Search Eorzea...");

        var listArea = new Rect(new Vector2(inset.Min.X, fieldArea.Max.Y + scale * 12f), inset.Max);
        var results = query.Length > 0 ? UniversalSearch.Search(query) : UniversalSearch.RecentSearches;
        var kicker = query.Length > 0 ? "RESULTS" : "RECENT SEARCHES";
        DrawResults(text, listArea, scale, theme, kicker, results);
    }

    private static void DrawResults(ITextPainter text, Rect area, float scale, ITheme theme, string kicker,
        IReadOnlyList<SearchResult> results)
    {
        text.DrawIn(area.TopSlice(scale * 16f), kicker,
            new TextStyle(FontRole.CaptionStrong, theme.Palette.InkMuted));

        var rowHeight = scale * 32f;
        for (var index = 0; index < results.Count; index++)
        {
            var row = new Rect(new Vector2(area.Min.X, area.Min.Y + scale * 20f + index * rowHeight),
                new Vector2(area.Max.X, area.Min.Y + scale * 20f + (index + 1) * rowHeight - scale * 4f));

            var entry = results[index];
            text.DrawIn(row.TopSlice(scale * 18f), entry.Title, new TextStyle(FontRole.Body, theme.Palette.Ink));
            if (entry.Subtitle.Length > 0)
            {
                text.DrawIn(row.BottomSlice(scale * 14f), entry.Subtitle,
                    new TextStyle(FontRole.Caption, theme.Palette.InkFaint));
            }
        }
    }
}
