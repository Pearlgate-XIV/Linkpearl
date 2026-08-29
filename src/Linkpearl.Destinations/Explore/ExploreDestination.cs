using Linkpearl.Applets;
using Linkpearl.Cards;
using Linkpearl.Geometry;
using Linkpearl.Layout;
using Linkpearl.Net;
using Linkpearl.Painting;

namespace Linkpearl.Destinations.Explore;

public sealed class ExploreDestination : IDestinationScreen, ISectionedDestination
{
    private static readonly string[] SectionTabs = { "For You", "Places", "Activities", "Events", "Groups" };

    private readonly IPearlHub pearl;
    private int selectedSection;

    public ExploreDestination(IPearlHub pearl)
    {
        this.pearl = pearl;
    }

    public DestinationTab Tab => DestinationTab.Explore;

    public string Glyph => "◈";

    public string Label => "Explore";

    public int CurrentSection => selectedSection;

    public void ShowSection(int section) =>
        selectedSection = Math.Clamp(section, 0, SectionTabs.Length - 1);

    public float Compose(in AppletFrame frame)
    {
        var inset = frame.Units(14f);
        var content = frame.Content.Inset(inset);
        var stack = new Stack(content, StackAxis.Vertical, frame.Units(10f));
        var snapshot = pearl.Current;

        frame.Text.DrawIn(stack.Take(frame.Units(30f)), "Explore",
            new TextStyle(FontRole.Title, frame.Theme.Palette.Ink));
        DrawSectionTabs(frame, stack.Take(frame.Units(28f)));

        if (selectedSection != 0)
        {
            DrawEmpty(frame, stack.TakeRemaining(),
                SectionTabs[selectedSection] + " needs Yellow Pages and Muster, which Pearlgate has turned off.");
            return content.Height + inset * 2f;
        }

        if (!snapshot.SignedIn)
        {
            DrawEmpty(frame, stack.TakeRemaining(), "Sign in from You to load people and stories.");
            return content.Height + inset * 2f;
        }

        var drew = false;
        for (var index = 0; index < snapshot.Stories.Length; index++)
        {
            var row = stack.Take(frame.Units(72f));
            CardChrome.Draw(frame, row);
            DrawStory(frame, row.Inset(frame.Units(12f)), snapshot.Stories[index]);
            drew = true;
        }

        for (var index = 0; index < snapshot.People.Length; index++)
        {
            var row = stack.Take(frame.Units(72f));
            CardChrome.Draw(frame, row);
            DrawPerson(frame, row.Inset(frame.Units(12f)), snapshot.People[index]);
            drew = true;
        }

        if (!drew)
        {
            DrawEmpty(frame, stack.Take(frame.Units(72f)), "Nothing to explore yet. Add people from Social.");
        }

        return (content.Height - stack.Remaining.Height) + inset * 2f;
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

    private static void DrawStory(in AppletFrame frame, Rect inset, PearlStory story)
    {
        var stack = new Stack(inset, StackAxis.Vertical, frame.Units(3f));
        CardChrome.DrawKicker(frame, stack.Take(frame.Units(15f)), "Story", frame.Theme.Palette.WarmAccent);
        frame.Text.DrawIn(stack.Take(frame.Units(22f)), story.AuthorName,
            new TextStyle(FontRole.BodyStrong, frame.Theme.Palette.Ink));
        frame.Text.DrawIn(stack.Take(frame.Units(18f)), story.HasUnseen ? "Unseen" : "Seen",
            new TextStyle(FontRole.Caption, frame.Theme.Palette.InkMuted));
    }

    private static void DrawPerson(in AppletFrame frame, Rect inset, PearlPerson person)
    {
        var stack = new Stack(inset, StackAxis.Vertical, frame.Units(3f));
        CardChrome.DrawKicker(frame, stack.Take(frame.Units(15f)), "Player", frame.Theme.Palette.WarmAccent);
        frame.Text.DrawIn(stack.Take(frame.Units(22f)), person.DisplayName,
            new TextStyle(FontRole.BodyStrong, frame.Theme.Palette.Ink));
        frame.Text.DrawIn(stack.Take(frame.Units(18f)), person.Handle.Length > 0 ? "@" + person.Handle : "Pearlgate",
            new TextStyle(FontRole.Caption, frame.Theme.Palette.InkMuted));
    }

    private static void DrawEmpty(in AppletFrame frame, Rect area, string text)
    {
        frame.Text.DrawWrapped(area.TopSlice(frame.Units(80f)), text,
            new TextStyle(FontRole.Body, frame.Theme.Palette.InkMuted, TextAlign.Center));
    }
}
