using Linkpearl.Applets;
using Linkpearl.Destinations;
using Linkpearl.Device.Time;
using Linkpearl.Geometry;
using Linkpearl.Input;
using Linkpearl.Preferences;
using Linkpearl.Time;

namespace Linkpearl.Device.Shell;

// Composes the pieces that make a Linkpearl handset recognisable: status strip, an optional
// quick bar, the current destination, and the destination bar with its central crystal.
// Chassis geometry is drawn by the window host; this type only fills the screen rect it is
// handed. RouteStack/HomeSurface/SoftKeyBar from the earlier icon-launcher model are not wired
// in here any more (see docs/STATUS.md) but remain available for a destination's own future
// drill-down navigation (e.g. Explore opening a venue detail).
public sealed class HandsetShell
{
    // Kept short deliberately: the design brief is explicit that this bar is contextual, not a
    // permanent dock of every notification category, so a couple of items is more representative
    // demo content than lining up all four the reference happens to show at once.
    private static readonly QuickBarItem[] DemoQuickItems =
    {
        new("⚔", "Duty Ready"), new("🗡", "Party Invite", Badge: 1),
    };

    private readonly Dictionary<DestinationTab, IDestinationScreen> destinationsByTab;
    private readonly Dictionary<DestinationTab, ScrollState> scrollByTab;
    private readonly IReadOnlyList<IDestinationScreen> destinationsInOrder;
    private readonly IClock clock;
    private readonly DisplayPreferences preferences;
    private readonly ITextField textField;
    private readonly UniversalSearchOverlay search = new();
    private DestinationTab currentTab = DestinationTab.Home;

    public HandsetShell(IReadOnlyList<IDestinationScreen> destinations, IClock clock, DisplayPreferences preferences,
        ITextField textField)
    {
        destinationsInOrder = destinations;
        this.clock = clock;
        this.preferences = preferences;
        this.textField = textField;

        var byTab = new Dictionary<DestinationTab, IDestinationScreen>();
        var scrollState = new Dictionary<DestinationTab, ScrollState>();
        foreach (var destination in destinations)
        {
            byTab[destination.Tab] = destination;
            scrollState[destination.Tab] = new ScrollState();
        }

        destinationsByTab = byTab;
        scrollByTab = scrollState;
    }

    public void Draw(in AppletFrame outerFrame, Rect screen)
    {
        var scale = outerFrame.Scale;
        var statusHeight = StatusStrip.Height(scale);
        StatusStrip.Draw(outerFrame, screen, HandsetClockText.Format(clock, preferences.Use24HourClock));

        var quickBarHeight = QuickBar.Height(scale, DemoQuickItems.Length);
        if (quickBarHeight > 0f)
        {
            var quickBarArea = new Rect(new Vector2(screen.Min.X, screen.Min.Y + statusHeight),
                new Vector2(screen.Max.X, screen.Min.Y + statusHeight + quickBarHeight))
                .Inset(new Edges(outerFrame.Units(12f), outerFrame.Units(4f)));
            QuickBar.Draw(outerFrame.Paint, outerFrame.Text, outerFrame.Theme, quickBarArea, scale, DemoQuickItems);
        }

        var content = screen.Inset(new Edges(0f, statusHeight + quickBarHeight, 0f, DestinationBar.Height(scale)));
        if (destinationsByTab.TryGetValue(currentTab, out var current) &&
            scrollByTab.TryGetValue(currentTab, out var scroll))
        {
            var scrolledContent = content.Translate(new Vector2(0f, -scroll.Offset));
            var scrolledFrame = outerFrame.WithContent(scrolledContent);

            outerFrame.Paint.PushClip(content);
            var contentHeight = current.Compose(scrolledFrame);
            outerFrame.Paint.PopClip();

            ScrollState.DrawIndicator(outerFrame.Paint, outerFrame.Theme, content, contentHeight, scroll.Offset, scale);

            var wheelDelta = !search.IsOpen && outerFrame.Input.IsHovering(content) ? outerFrame.Input.ScrollDelta : 0f;
            scroll.Update(contentHeight, content.Height, wheelDelta, scale);
        }

        var barResult = DestinationBar.Draw(outerFrame.Paint, outerFrame.Text, outerFrame.Input, outerFrame.Theme,
            screen, scale, destinationsInOrder, currentTab);

        // The overlay's scrim only covers the bar visually; its clicks still land underneath
        // unless explicitly ignored here, so a tap that closes the overlay can't also switch
        // tabs or reopen it in the same frame.
        if (!search.IsOpen)
        {
            if (barResult.Selected is { } selected)
            {
                currentTab = selected;
            }

            if (barResult.CrystalTapped)
            {
                search.Open();
            }
        }

        search.Draw(outerFrame.Paint, outerFrame.Text, textField, outerFrame.Input, outerFrame.Theme, screen, scale);
    }
}
