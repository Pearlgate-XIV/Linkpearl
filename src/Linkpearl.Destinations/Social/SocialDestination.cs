using Linkpearl.Applets;
using Linkpearl.Cards;
using Linkpearl.Geometry;
using Linkpearl.Layout;
using Linkpearl.Painting;

namespace Linkpearl.Destinations.Social;

// Placeholder shell only: a feed of demo posts under a section tab row. Messages/People/
// Communities tabs exist as labels the layout reserves space for, not as built screens yet — the
// consolidation this destination exists for (tells, friends, FC, linkshells, communities) is
// deliberately not implemented in this pass.
public sealed class SocialDestination : IDestinationScreen
{
    private static readonly string[] SectionTabs = { "Feed", "Messages", "People", "Communities" };

    public DestinationTab Tab => DestinationTab.Social;

    public string Glyph => "👥";

    public string Label => "Social";

    public void Compose(in AppletFrame frame)
    {
        var content = frame.Content.Inset(frame.Units(14f));
        var stack = new Stack(content, StackAxis.Vertical, frame.Units(10f));

        DrawSectionHeader(frame, stack.Take(frame.Units(30f)), "Social");
        DrawSectionTabs(frame, stack.Take(frame.Units(28f)));

        var feed = DemoData.Feed;
        for (var index = 0; index < feed.Count; index++)
        {
            var postRow = stack.Take(frame.Units(104f));
            CardChrome.Draw(frame, postRow, feed[index].IsEventShare ? 2f : 1f);
            DrawPost(frame, postRow.Inset(frame.Units(12f)), feed[index]);
        }
    }

    private static void DrawSectionHeader(in AppletFrame frame, Rect row, string title) =>
        frame.Text.DrawIn(row, title, new TextStyle(FontRole.Title, frame.Theme.Palette.Ink));

    private static void DrawSectionTabs(in AppletFrame frame, Rect row)
    {
        var cellWidth = row.Width / SectionTabs.Length;
        for (var index = 0; index < SectionTabs.Length; index++)
        {
            var cell = row.Translate(new Vector2(index * cellWidth, 0f)).WithWidth(cellWidth);
            var isActive = index == 0;
            frame.Text.DrawIn(cell, SectionTabs[index],
                new TextStyle(FontRole.CaptionStrong, isActive ? frame.Theme.Palette.Accent : frame.Theme.Palette.InkFaint,
                    TextAlign.Center));
        }
    }

    private static void DrawPost(in AppletFrame frame, Rect inset, DemoData.SocialPost post)
    {
        var stack = new Stack(inset, StackAxis.Vertical, frame.Units(3f));
        var header = stack.Take(frame.Units(18f));
        frame.Text.DrawIn(header.LeftSlice(header.Width - frame.Units(50f)), post.Author,
            new TextStyle(FontRole.BodyStrong, frame.Theme.Palette.Ink));
        frame.Text.DrawIn(header.RightSlice(frame.Units(50f)), post.When,
            new TextStyle(FontRole.Caption, frame.Theme.Palette.InkFaint, TextAlign.Right));

        frame.Text.DrawWrapped(stack.Take(frame.Units(36f)), post.Body,
            new TextStyle(FontRole.Body, frame.Theme.Palette.InkMuted));

        var footer = stack.Take(frame.Units(18f));
        frame.Text.DrawIn(footer, $"♥ {post.Likes}    💬 {post.Comments}",
            new TextStyle(FontRole.Caption, frame.Theme.Palette.InkFaint));
    }
}
