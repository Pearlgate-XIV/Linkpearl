using Linkpearl.Applets;
using Linkpearl.Cards;
using Linkpearl.Geometry;
using Linkpearl.Layout;
using Linkpearl.Painting;

namespace Linkpearl.Destinations.Explore;

// "What can I do?" — one feed instead of separate Venue/Activity/Event apps. The kind label on
// each card (VENUE/ACTIVITY/EVENT) is what tells the player what they're looking at, not which
// app they're in. Section tabs genuinely switch; only "For You" has real content behind it, the
// rest show an honest "not built yet" placeholder rather than a tab that looks clickable and does
// nothing (see the design brief's own "impossible widgets" warning).
public sealed class ExploreDestination : IDestinationScreen
{
    private static readonly string[] SectionTabs = { "For You", "Places", "Activities", "Events", "Groups" };

    private int selectedSection;

    public DestinationTab Tab => DestinationTab.Explore;

    public string Glyph => "◈";

    public string Label => "Explore";

    public void Compose(in AppletFrame frame)
    {
        var content = frame.Content.Inset(frame.Units(14f));
        var stack = new Stack(content, StackAxis.Vertical, frame.Units(10f));

        frame.Text.DrawIn(stack.Take(frame.Units(30f)), "Explore", new TextStyle(FontRole.Title, frame.Theme.Palette.Ink));
        DrawSectionTabs(frame, stack.Take(frame.Units(28f)));

        if (selectedSection != 0)
        {
            DrawUnbuiltSection(frame, stack.TakeRemaining(), SectionTabs[selectedSection]);
            return;
        }

        var feed = DemoData.ExploreFeed;
        for (var index = 0; index < feed.Count; index++)
        {
            var cardRow = stack.Take(frame.Units(88f));
            CardChrome.Draw(frame, cardRow);
            DrawCard(frame, cardRow.Inset(frame.Units(12f)), feed[index]);
        }
    }

    private void DrawSectionTabs(in AppletFrame frame, Rect row)
    {
        var cellWidth = row.Width / SectionTabs.Length;
        for (var index = 0; index < SectionTabs.Length; index++)
        {
            var cell = row.Translate(new Vector2(index * cellWidth, 0f)).WithWidth(cellWidth);
            var isActive = index == selectedSection;
            frame.Text.DrawEllipsized(cell, SectionTabs[index],
                new TextStyle(FontRole.Caption, isActive ? frame.Theme.Palette.Accent : frame.Theme.Palette.InkFaint,
                    TextAlign.Center));

            if (frame.Input.ConsumeClick(cell))
            {
                selectedSection = index;
            }
        }
    }

    private static void DrawUnbuiltSection(in AppletFrame frame, Rect area, string sectionName)
    {
        frame.Text.DrawIn(area.TopSlice(frame.Units(60f)), $"{sectionName} isn't built yet",
            new TextStyle(FontRole.Body, frame.Theme.Palette.InkMuted, TextAlign.Center));
    }

    private static void DrawCard(in AppletFrame frame, Rect inset, DemoData.ExploreCard card)
    {
        var stack = new Stack(inset, StackAxis.Vertical, frame.Units(3f));
        CardChrome.DrawKicker(frame, stack.Take(frame.Units(15f)), card.Kind, frame.Theme.Palette.WarmAccent);

        var titleRow = stack.Take(frame.Units(22f));
        var hasMeta = card.Meta.Length > 0;
        frame.Text.DrawIn(hasMeta ? titleRow.LeftSlice(titleRow.Width - frame.Units(100f)) : titleRow, card.Title,
            new TextStyle(FontRole.BodyStrong, frame.Theme.Palette.Ink));
        if (hasMeta)
        {
            frame.Text.DrawIn(titleRow.RightSlice(frame.Units(100f)), card.Meta,
                new TextStyle(FontRole.Caption, frame.Theme.Palette.Positive, TextAlign.Right));
        }

        frame.Text.DrawIn(stack.Take(frame.Units(18f)), card.Detail,
            new TextStyle(FontRole.Caption, frame.Theme.Palette.InkMuted));
    }
}
