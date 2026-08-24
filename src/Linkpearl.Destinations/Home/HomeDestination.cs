using System.Globalization;
using Linkpearl.Applets;
using Linkpearl.Cards;
using Linkpearl.Geometry;
using Linkpearl.Layout;
using Linkpearl.Painting;
using Linkpearl.Time;

namespace Linkpearl.Destinations.Home;

// "What matters to me right now" — a modular dashboard, not a launcher and not a feed. Card
// sizes vary with how time-sensitive the content is: Up Next gets a full-width hero slot,
// everything else pairs up two-to-a-row. Rows are skipped entirely when there's nothing to
// show, rather than reserving empty space for a widget that has nothing to say.
public sealed class HomeDestination : IDestinationScreen
{
    private readonly IClock clock;

    public HomeDestination(IClock clock)
    {
        this.clock = clock;
    }

    public DestinationTab Tab => DestinationTab.Home;

    public string Glyph => "⌂";

    public string Label => "Home";

    public void Compose(in AppletFrame frame)
    {
        var content = frame.Content.Inset(frame.Units(14f));
        var stack = new Stack(content, StackAxis.Vertical, frame.Units(8f));

        // Row heights below are sized from RowAt's fixed 20-unit line pitch: an N-line card
        // needs (N - 1) * 20 + 19 units of interior plus the card's own inset on both sides, not
        // an eyeballed constant — a mismatch here doesn't error, it just quietly bleeds a card's
        // last line past its own background and into whatever comes next.
        DrawHeader(frame, stack.Take(frame.Units(80f)), clock);
        DrawUpNext(frame, stack.Take(frame.Units(120f)));

        var messageRow = stack.Take(frame.Units(88f));
        CardChrome.Draw(frame, messageRow);
        DrawMessage(frame, messageRow.Inset(frame.Units(14f)));

        var partyRow = stack.Take(frame.Units(88f));
        CardChrome.Draw(frame, partyRow);
        DrawParty(frame, partyRow.Inset(frame.Units(14f)));

        var retainerFriendsRow = stack.Take(frame.Units(88f));
        var (retainerCell, friendsCell) = SplitPair(retainerFriendsRow, frame);
        CardChrome.Draw(frame, retainerCell);
        DrawRetainer(frame, retainerCell.Inset(frame.Units(12f)));
        CardChrome.Draw(frame, friendsCell);
        DrawFriends(frame, friendsCell.Inset(frame.Units(12f)));

        var marketEventRow = stack.Take(frame.Units(88f));
        var (marketCell, eventCell) = SplitPair(marketEventRow, frame);
        CardChrome.Draw(frame, marketCell);
        DrawMarket(frame, marketCell.Inset(frame.Units(12f)));
        CardChrome.Draw(frame, eventCell);
        DrawEvent(frame, eventCell.Inset(frame.Units(12f)));
    }

    private static (Rect Left, Rect Right) SplitPair(Rect row, in AppletFrame frame)
    {
        var halves = new Stack(row, StackAxis.Horizontal, frame.Units(10f));
        var left = halves.Take(row.Width * 0.5f - frame.Units(5f));
        var right = halves.TakeRemaining();
        return (left, right);
    }

    private static void DrawHeader(in AppletFrame frame, Rect row, IClock clock)
    {
        var hour = clock.Now.Hour;
        var greeting = hour < 12 ? "Good morning," : hour < 18 ? "Good afternoon," : "Good evening,";

        frame.Text.DrawIn(row.TopSlice(frame.Units(22f)), greeting,
            new TextStyle(FontRole.Body, frame.Theme.Palette.InkMuted));

        var nameRow = row.Inset(new Edges(0f, frame.Units(24f), 0f, frame.Units(34f)));
        frame.Text.DrawIn(nameRow, DemoData.CharacterName, new TextStyle(FontRole.Display, frame.Theme.Palette.Ink));

        frame.Text.DrawIn(row.BottomSlice(frame.Units(28f)), $"⛨ {DemoData.CharacterTitle}",
            new TextStyle(FontRole.Caption, frame.Theme.Palette.InkMuted));
    }

    private static void DrawUpNext(in AppletFrame frame, Rect row)
    {
        CardChrome.Draw(frame, row, emphasis: 2f);
        var inset = row.Inset(frame.Units(14f));
        var stack = new Stack(inset, StackAxis.Vertical, frame.Units(2f));

        CardChrome.DrawKicker(frame, stack.Take(frame.Units(16f)), "UP NEXT", frame.Theme.Palette.Accent);

        var upNext = DemoData.UpNext;
        var titleRow = stack.Take(frame.Units(24f));
        frame.Text.DrawIn(titleRow.LeftSlice(titleRow.Width - frame.Units(70f)), upNext.Title,
            new TextStyle(FontRole.BodyStrong, frame.Theme.Palette.Ink));
        frame.Text.DrawIn(titleRow.RightSlice(frame.Units(70f)), upNext.StartsIn,
            new TextStyle(FontRole.Caption, frame.Theme.Palette.WarmAccent, TextAlign.Right));

        frame.Text.DrawIn(stack.Take(frame.Units(20f)), $"{upNext.Venue} · {upNext.World}  🕒 {upNext.Time}",
            new TextStyle(FontRole.Caption, frame.Theme.Palette.InkMuted));

        stack.Take(frame.Units(4f));
        frame.Text.DrawIn(stack.Take(frame.Units(18f)),
            string.Format(CultureInfo.InvariantCulture, "{0} friends interested", upNext.InterestedFriends),
            new TextStyle(FontRole.Caption, frame.Theme.Palette.InkFaint));
    }

    private static void DrawMessage(in AppletFrame frame, Rect inset)
    {
        var message = DemoData.LatestMessage;
        CardChrome.DrawKicker(frame, RowAt(frame, inset, 0), "MESSAGES");
        DrawTitleValueRow(frame, RowAt(frame, inset, 1), message.Sender, message.When);
        frame.Text.DrawIn(RowAt(frame, inset, 2), message.Body,
            new TextStyle(FontRole.Caption, frame.Theme.Palette.InkMuted));
    }

    private static void DrawParty(in AppletFrame frame, Rect inset)
    {
        var party = DemoData.Party;
        CardChrome.DrawKicker(frame, RowAt(frame, inset, 0), "PARTY");
        DrawTitleValueRow(frame, RowAt(frame, inset, 1), party.Title,
            string.Format(CultureInfo.InvariantCulture, "{0}/{1}", party.Filled, party.Capacity));
        frame.Text.DrawIn(RowAt(frame, inset, 2), party.Need,
            new TextStyle(FontRole.Caption, frame.Theme.Palette.WarmAccent));
    }

    private static void DrawRetainer(in AppletFrame frame, Rect inset)
    {
        var retainer = DemoData.Retainer;
        CardChrome.DrawKicker(frame, RowAt(frame, inset, 0), "RETAINER");
        frame.Text.DrawIn(RowAt(frame, inset, 1), retainer.Task,
            new TextStyle(FontRole.BodyStrong, frame.Theme.Palette.Positive));
        frame.Text.DrawIn(RowAt(frame, inset, 2), retainer.Detail,
            new TextStyle(FontRole.Caption, frame.Theme.Palette.InkMuted));
    }

    private static void DrawFriends(in AppletFrame frame, Rect inset)
    {
        CardChrome.DrawKicker(frame, RowAt(frame, inset, 0), "FRIENDS ONLINE");
        frame.Text.DrawIn(RowAt(frame, inset, 1), DemoData.FriendsOnline.ToString(CultureInfo.InvariantCulture),
            new TextStyle(FontRole.BodyStrong, frame.Theme.Palette.Ink));
    }

    private static void DrawMarket(in AppletFrame frame, Rect inset)
    {
        var market = DemoData.Market;
        CardChrome.DrawKicker(frame, RowAt(frame, inset, 0), "MARKET WATCH");
        frame.Text.DrawIn(RowAt(frame, inset, 1), market.Item,
            new TextStyle(FontRole.BodyStrong, frame.Theme.Palette.Ink));
        var changeColor = market.PercentChange >= 0f ? frame.Theme.Palette.Positive : frame.Theme.Palette.Negative;
        var sign = market.PercentChange >= 0f ? "▲" : "▼";
        var percentText = (market.PercentChange * 100f).ToString("0", CultureInfo.InvariantCulture);
        var priceText = market.Price.ToString("N0", CultureInfo.InvariantCulture);
        frame.Text.DrawIn(RowAt(frame, inset, 2), $"{sign} {percentText}%   {priceText}p",
            new TextStyle(FontRole.Caption, changeColor));
    }

    private static void DrawEvent(in AppletFrame frame, Rect inset)
    {
        var upcoming = DemoData.Event;
        CardChrome.DrawKicker(frame, RowAt(frame, inset, 0), "EVENT");
        frame.Text.DrawIn(RowAt(frame, inset, 1), upcoming.Title,
            new TextStyle(FontRole.BodyStrong, frame.Theme.Palette.Ink));
        frame.Text.DrawIn(RowAt(frame, inset, 2), upcoming.StartsIn,
            new TextStyle(FontRole.Caption, frame.Theme.Palette.Positive));
    }

    private static void DrawTitleValueRow(in AppletFrame frame, Rect row, string title, string value)
    {
        frame.Text.DrawIn(row.LeftSlice(row.Width - frame.Units(60f)), title,
            new TextStyle(FontRole.BodyStrong, frame.Theme.Palette.Ink));
        frame.Text.DrawIn(row.RightSlice(frame.Units(60f)), value,
            new TextStyle(FontRole.Caption, frame.Theme.Palette.InkMuted, TextAlign.Right));
    }

    private static Rect RowAt(in AppletFrame frame, Rect inset, int index)
    {
        var top = inset.Min.Y + index * frame.Units(20f);
        return new Rect(new Vector2(inset.Min.X, top), new Vector2(inset.Max.X, top + frame.Units(19f)));
    }
}
