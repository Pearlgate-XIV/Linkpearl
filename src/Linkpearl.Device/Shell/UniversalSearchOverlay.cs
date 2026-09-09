using Linkpearl.Destinations;
using Linkpearl.Geometry;
using Linkpearl.Input;
using Linkpearl.Net;
using Linkpearl.Painting;
using Linkpearl.Talk;
using Linkpearl.Theming;

namespace Linkpearl.Device.Shell;

public sealed class UniversalSearchOverlay
{
    private readonly IPearlHub pearl;
    private readonly ITalk talk;
    private readonly DestinationHub hub;
    private readonly SearchResult[] resultsScratch = new SearchResult[24];
    private string query = string.Empty;

    public UniversalSearchOverlay(IPearlHub pearl, ITalk talk, DestinationHub hub)
    {
        this.pearl = pearl;
        this.talk = talk;
        this.hub = hub;
    }

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

        if (input.EscapePressed() ||
            (input.PointerReleased() && !screen.Contains(input.Pointer)))
        {
            Close();
            textField.Release();
            return;
        }

        paint.Fill(screen, theme.Palette.SurfaceSunken with { W = 0.92f });

        var panel = screen.Inset(scale * 14f).TopSlice(screen.Height - scale * 28f);
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
        pearl.NoteQuery(query);

        var listArea = new Rect(new Vector2(inset.Min.X, fieldArea.Max.Y + scale * 12f), inset.Max);
        var snapshot = pearl.Current;
        var resultCount = DirectorySearch.Fill(snapshot, query, resultsScratch, talk);
        var kicker = query.Length > 0 ? "Results" : "Recent";
        if (DrawResults(paint, text, input, listArea, scale, theme, kicker, resultsScratch, resultCount,
                EmptySearchCopy(snapshot, query), out var picked))
        {
            if (picked.TalkId.Length > 0)
            {
                hub.OpenTalk(picked.TalkId);
            }
            else if (picked.ProfileId.Length > 0)
            {
                hub.OpenProfile(picked.ProfileId);
            }

            Close();
        }

        // Rows already claimed their taps. Eat leftover overlay clicks so the same press
        // cannot land on home tiles, the dock, or Control Center under the dimmer.
        if (input.ConsumeClick(screen) && !panel.Contains(input.Pointer))
        {
            Close();
        }
    }

    private static bool DrawResults(IPaintSurface paint, ITextPainter text, IInputProbe input, Rect area,
        float scale, ITheme theme, string kicker, SearchResult[] results, int count, string emptyCopy,
        out SearchResult picked)
    {
        picked = default;
        text.DrawIn(area.TopSlice(scale * 16f), kicker,
            new TextStyle(FontRole.CaptionStrong, theme.Palette.InkMuted));

        if (count == 0)
        {
            text.DrawWrapped(area.Inset(new Edges(0f, scale * 24f, 0f, 0f)).TopSlice(scale * 48f), emptyCopy,
                new TextStyle(FontRole.Caption, theme.Palette.InkMuted));
            return false;
        }

        var rowHeight = scale * 32f;
        for (var index = 0; index < count; index++)
        {
            var row = new Rect(new Vector2(area.Min.X, area.Min.Y + scale * 20f + index * rowHeight),
                new Vector2(area.Max.X, area.Min.Y + scale * 20f + (index + 1) * rowHeight - scale * 4f));

            if (input.IsHovering(row))
            {
                paint.Fill(row, theme.Palette.SurfaceRaised, scale * 8f);
            }

            var entry = results[index];
            text.DrawIn(row.TopSlice(scale * 18f), entry.Title, new TextStyle(FontRole.Body, theme.Palette.Ink));
            if (entry.Subtitle.Length > 0)
            {
                text.DrawIn(row.BottomSlice(scale * 14f), entry.Subtitle,
                    new TextStyle(FontRole.Caption, theme.Palette.InkFaint));
            }

            if (input.ConsumeClick(row))
            {
                picked = entry;
                return true;
            }
        }

        return false;
    }

    private static string EmptySearchCopy(PearlSnapshot snapshot, string query)
    {
        if (!snapshot.SignedIn)
        {
            return query.Trim().Length >= 2 ? "No talks match." : "Sign in from You to search Pearlgate.";
        }

        return query.Trim().Length >= 2 ? "No people match." : "No recent chats or people.";
    }
}
