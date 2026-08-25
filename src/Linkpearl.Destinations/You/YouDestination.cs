using Linkpearl.Applets;
using Linkpearl.Cards;
using Linkpearl.Geometry;
using Linkpearl.Layout;
using Linkpearl.Painting;

namespace Linkpearl.Destinations.You;

// The player's personal space: profile identity plus a couple of stat rows. Everything the
// spec lists (glamours, collections, favorites, phone settings) belongs here eventually, as
// entries in this list rather than as separate destinations — only a small placeholder slice
// is built in this pass.
public sealed class YouDestination : IDestinationScreen
{
    public DestinationTab Tab => DestinationTab.You;

    public string Glyph => "🧑";

    public string Label => "You";

    public float Compose(in AppletFrame frame)
    {
        var inset = frame.Units(14f);
        var content = frame.Content.Inset(inset);
        var stack = new Stack(content, StackAxis.Vertical, frame.Units(10f));

        var profileRow = stack.Take(frame.Units(72f));
        CardChrome.Draw(frame, profileRow);
        DrawProfile(frame, profileRow.Inset(frame.Units(12f)));

        var stats = DemoData.ProfileStats;
        for (var index = 0; index < stats.Count; index++)
        {
            var statRow = stack.Take(frame.Units(44f));
            CardChrome.Draw(frame, statRow);
            DrawStat(frame, statRow.Inset(new Edges(frame.Units(12f), 0f)), stats[index]);
        }

        return (content.Height - stack.Remaining.Height) + inset * 2f;
    }

    private static void DrawProfile(in AppletFrame frame, Rect inset)
    {
        var stack = new Stack(inset, StackAxis.Vertical, frame.Units(2f));
        frame.Text.DrawIn(stack.Take(frame.Units(22f)), $"{DemoData.CharacterName} Morningstar",
            new TextStyle(FontRole.BodyStrong, frame.Theme.Palette.Ink));
        frame.Text.DrawIn(stack.Take(frame.Units(18f)), $"⛨ {DemoData.CharacterTitle} · {DemoData.World}",
            new TextStyle(FontRole.Caption, frame.Theme.Palette.InkMuted));
    }

    private static void DrawStat(in AppletFrame frame, Rect row, DemoData.ProfileStat stat)
    {
        frame.Text.DrawIn(row.LeftSlice(row.Width - frame.Units(140f)), stat.Label,
            new TextStyle(FontRole.Body, frame.Theme.Palette.Ink));
        frame.Text.DrawIn(row.RightSlice(frame.Units(140f)), stat.Value,
            new TextStyle(FontRole.Caption, frame.Theme.Palette.InkMuted, TextAlign.Right));
    }
}
