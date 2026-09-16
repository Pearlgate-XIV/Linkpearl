using System.Globalization;
using Linkpearl.Applets;
using Linkpearl.Cards;
using Linkpearl.Destinations.Stories;
using Linkpearl.Geometry;
using Linkpearl.Layout;
using Linkpearl.Net;
using Linkpearl.Painting;
using Linkpearl.Platform;
using Linkpearl.Preferences;
using Linkpearl.Time;

namespace Linkpearl.Destinations.Explore;

public sealed class ExploreDestination : IDestinationScreen, ISectionedDestination
{
    private static readonly string[] SectionTabs = { "For You", "Places", "Activities", "Events", "Groups" };

    private readonly IPearlHub pearl;
    private readonly IGameSession game;
    private readonly StoriesSurface stories;
    private int selectedSection;
    private string itemDraft = string.Empty;

    public ExploreDestination(IPearlHub pearl, IGameSession game, IFilePicker files, IClock clock)
    {
        this.pearl = pearl;
        this.game = game;
        stories = new StoriesSurface(pearl, files, clock);
    }

    public DestinationTab Tab => DestinationTab.Explore;

    public string Glyph => "◈";

    public string Label => PhoneLanguages.T("nav.explore");

    public int CurrentSection => selectedSection;

    public void ShowSection(int section)
    {
        selectedSection = Math.Clamp(section, 0, SectionTabs.Length - 1);
        if (selectedSection != 0)
        {
            stories.Close();
        }
    }

    public bool CanGoBack => stories.OverlayOpen || selectedSection != 0;

    public bool Back()
    {
        if (stories.Back())
        {
            return true;
        }

        if (selectedSection == 0)
        {
            return false;
        }

        selectedSection = 0;
        return true;
    }

    public void OpenStory(string authorId)
    {
        selectedSection = 0;
        if (authorId.Length > 0)
        {
            stories.Open(authorId);
            return;
        }

        stories.Close();
    }

    public void OpenCompose()
    {
        selectedSection = 0;
        stories.OpenCompose();
    }

    public float Compose(in AppletFrame frame)
    {
        var inset = frame.Units(14f);
        var content = frame.Content.Inset(inset);
        var stack = new LayoutFlow(content, StackAxis.Vertical, frame.Units(10f));
        var snapshot = pearl.Current;

        if (stories.OverlayOpen && selectedSection == 0)
        {
            return stories.ComposeOverlay(frame);
        }

        frame.Text.DrawIn(stack.Take(frame.Units(30f)), "Explore",
            new TextStyle(FontRole.Title, frame.Theme.Palette.Ink));
        DrawSectionTabs(frame, stack.Take(frame.Units(28f)));

        if (selectedSection == 1)
        {
            DrawPlaces(frame, ref stack, snapshot);
            return (content.Height - stack.Remaining.Height) + inset * 2f;
        }

        if (selectedSection != 0)
        {
            DrawEmpty(frame, stack.TakeRemaining(),
                SectionTabs[selectedSection] + " needs Yellow Pages and Muster, which Pearlgate has turned off.");
            return content.Height + inset * 2f;
        }

        stories.DrawList(frame, ref stack, snapshot);

        var drewPeople = false;
        for (var index = 0; index < snapshot.People.Length; index++)
        {
            var row = stack.Take(frame.Units(72f));
            CardChrome.Draw(frame, row);
            DrawPerson(frame, row.Inset(frame.Units(12f)), snapshot.People[index]);
            drewPeople = true;
        }

        if (!drewPeople && snapshot.SignedIn && snapshot.StoriesLive && snapshot.Stories.Length == 0)
        {
            DrawEmpty(frame, stack.Take(frame.Units(56f)), "Add people from Social to fill Explore.");
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

    private void DrawPlaces(in AppletFrame frame, ref LayoutFlow stack, PearlSnapshot snapshot)
    {
        if (!snapshot.SignedIn)
        {
            DrawEmpty(frame, stack.TakeRemaining(), "Sign in from You to watch market prices.");
            return;
        }

        itemDraft = frame.TextField.Draw("market-item", stack.Take(frame.Units(36f)), itemDraft, "Item id");
        var parsed = uint.TryParse(itemDraft.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var itemId)
            && itemId > 0;
        var named = parsed ? game.ItemName(itemId) : string.Empty;
        var addRow = stack.Take(frame.Units(40f));
        var hint = named.Length > 0 ? named : parsed ? "Item " + itemId.ToString(CultureInfo.InvariantCulture) : "Type an item id";
        frame.Text.DrawEllipsized(addRow.LeftSlice(addRow.Width - frame.Units(88f)), hint,
            new TextStyle(FontRole.Caption, frame.Theme.Palette.InkMuted));
        if (Chip(frame, addRow.RightSlice(frame.Units(80f)), "Watch") && parsed)
        {
            pearl.WatchMarket(itemId, named);
            itemDraft = string.Empty;
        }

        if (snapshot.MarketWatches.Length == 0)
        {
            DrawEmpty(frame, stack.Take(frame.Units(72f)), "No watches yet.");
            return;
        }

        for (var index = 0; index < snapshot.MarketWatches.Length; index++)
        {
            var row = stack.Take(frame.Units(72f));
            CardChrome.Draw(frame, row);
            DrawWatch(frame, row.Inset(frame.Units(12f)), snapshot.MarketWatches[index]);
        }
    }

    private void DrawWatch(in AppletFrame frame, Rect inset, PearlMarketWatch watch)
    {
        var stack = new LayoutFlow(inset, StackAxis.Vertical, frame.Units(3f));
        var header = stack.Take(frame.Units(22f));
        var title = MarketTitle(watch);
        frame.Text.DrawEllipsized(header.LeftSlice(header.Width - frame.Units(64f)), title,
            new TextStyle(FontRole.BodyStrong, frame.Theme.Palette.Ink));
        if (Chip(frame, header.RightSlice(frame.Units(60f)), "Remove"))
        {
            pearl.UnwatchMarket((uint)watch.ItemId);
        }

        CardChrome.DrawKicker(frame, stack.Take(frame.Units(15f)),
            watch.World.Length > 0 ? watch.World : "Market", frame.Theme.Palette.WarmAccent);
        frame.Text.DrawEllipsized(stack.Take(frame.Units(18f)), MarketDetail(watch),
            new TextStyle(FontRole.Caption, frame.Theme.Palette.InkMuted));
    }

    private string MarketTitle(PearlMarketWatch watch)
    {
        var named = game.ItemName((uint)watch.ItemId);
        if (named.Length > 0)
        {
            return named;
        }

        return watch.Label.Length > 0 ? watch.Label : "Item " + watch.ItemId.ToString(CultureInfo.InvariantCulture);
    }

    private static string MarketDetail(PearlMarketWatch watch)
    {
        var parts = new List<string>(3);
        if (watch.NqGil > 0)
        {
            parts.Add("NQ " + watch.NqGil.ToString("N0", CultureInfo.InvariantCulture));
        }

        if (watch.HqGil > 0)
        {
            parts.Add("HQ " + watch.HqGil.ToString("N0", CultureInfo.InvariantCulture));
        }

        if (watch.Listed > 0)
        {
            parts.Add(watch.Listed.ToString(CultureInfo.InvariantCulture) + " listed");
        }

        return parts.Count == 0 ? "No quote yet" : string.Join(" · ", parts);
    }

    private static bool Chip(in AppletFrame frame, Rect area, string label)
    {
        frame.Paint.Fill(area.Inset(frame.Units(2f)), frame.Theme.Palette.Accent, frame.Units(999f));
        frame.Text.DrawIn(area, label, new TextStyle(FontRole.Caption, frame.Theme.Palette.AccentInk, TextAlign.Center));
        return frame.Input.ConsumeClick(area);
    }

    private static void DrawPerson(in AppletFrame frame, Rect inset, PearlPerson person)
    {
        var stack = new LayoutFlow(inset, StackAxis.Vertical, frame.Units(3f));
        CardChrome.DrawKicker(frame, stack.Take(frame.Units(15f)), "Player", frame.Theme.Palette.WarmAccent);
        frame.Text.DrawIn(stack.Take(frame.Units(22f)), person.DisplayName,
            new TextStyle(FontRole.BodyStrong, frame.Theme.Palette.Ink));
        var detail = person.Handle.Length > 0 ? "@" + person.Handle : "Pearlgate";
        var zone = WorldZones.ForPerson(person.TimeZoneId, person.Id);
        if (zone.Length > 0)
        {
            detail += " · " + ZoneClock.Line(zone, false);
        }

        frame.Text.DrawIn(stack.Take(frame.Units(18f)), detail,
            new TextStyle(FontRole.Caption, frame.Theme.Palette.InkMuted));
    }

    private static void DrawEmpty(in AppletFrame frame, Rect area, string text)
    {
        frame.Text.DrawWrapped(area.TopSlice(frame.Units(80f)), text,
            new TextStyle(FontRole.Body, frame.Theme.Palette.InkMuted, TextAlign.Center));
    }
}
