using Linkpearl.Applets;
using Linkpearl.Destinations;
using Linkpearl.Destinations.Social;
using Linkpearl.Device.Time;
using Linkpearl.Geometry;
using Linkpearl.Input;
using Linkpearl.Net;
using Linkpearl.Platform;
using Linkpearl.Painting;
using Linkpearl.Preferences;
using Linkpearl.Talk;
using Linkpearl.Theming;
using Linkpearl.Time;

namespace Linkpearl.Device.Shell;

public sealed class HandsetShell
{
    private readonly Dictionary<DestinationTab, IDestinationScreen> destinationsByTab;
    private readonly Dictionary<DestinationTab, ScrollState> scrollByTab;
    private readonly IClock clock;
    private readonly IGameSession game;
    private readonly DisplayPreferences preferences;
    private readonly ITextField textField;
    private readonly IPearlHub pearl;
    private readonly DestinationHub hub;
    private readonly RouteStack router;
    private readonly IReadOnlyList<IApplet> apps;
    private readonly HomeSurface appsSurface;
    private readonly UniversalSearchOverlay search;
    private readonly ControlCenter control = new();
    private readonly RecentsOverlay recents = new();
    private readonly DestinationDock dock = new();
    private readonly AppsDock appsDock = new();
    private DestinationTab currentTab = DestinationTab.Home;

    public HandsetShell(IReadOnlyList<IDestinationScreen> destinations, IClock clock, IGameSession game,
        DisplayPreferences preferences, ITextField textField, IPearlHub pearl, DestinationHub hub, RouteStack router,
        ITalk talk, IReadOnlyList<IApplet> applets)
    {
        this.clock = clock;
        this.game = game;
        this.preferences = preferences;
        this.textField = textField;
        this.pearl = pearl;
        this.hub = hub;
        this.router = router;
        apps = LifeApps(applets);
        appsSurface = new HomeSurface(apps);
        search = new UniversalSearchOverlay(pearl, talk, hub);

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

    public void DrawMinimized(IPaintSurface paint, ITextPainter text, ITheme theme, Rect screen, float scale)
    {
        MinimizedFace.Draw(paint, text, theme, screen, scale, HandsetClockText.Format(clock, preferences));
    }

    public void Draw(in AppletFrame outerFrame, Rect screen)
    {
        var scale = outerFrame.Scale;
        var hush = preferences.Hushed(game.IsInDuty || game.IsInCutscene);
        var statusHeight = StatusStrip.Height(scale);
        StatusStrip.Draw(outerFrame, screen, HandsetClockText.Format(clock, preferences),
            game.Character.WorldName, preferences.ShowWorld, preferences.ShowMarks);

        var strip = StatusStrip.StripArea(screen, scale);
        var canGoBack = CanGoBack();
        var softKey = SoftKeyBar.Consume(outerFrame.Input, screen, scale, canGoBack);
        if (softKey == SoftKey.None && !search.IsOpen && !control.IsOpen && !recents.IsOpen &&
            outerFrame.Input.ConsumeClick(strip))
        {
            control.Open();
        }

        var shift = appsDock.Shift;
        var onApps = shift > 0.08f;
        var onDest = shift < 0.92f;
        var paging = shift is > 0.02f and < 0.98f;
        var overlayOpen = search.IsOpen || dock.IsOpen || paging || control.IsOpen || recents.IsOpen;

        var dockHeight = SoftKeyBar.Height(scale);
        var content = screen.Inset(new Edges(0f, statusHeight, 0f, dockHeight));
        var slide = new Vector2(content.Width * shift, 0f);
        var destArea = content.Translate(new Vector2(-slide.X, 0f));
        var appsArea = content.Translate(new Vector2(content.Width - slide.X, 0f));

        outerFrame.Paint.PushClip(content);
        if (onDest)
        {
            DrawDestination(outerFrame, destArea, content, overlayOpen, scale);
        }

        if (onApps)
        {
            DrawAppsPage(outerFrame, appsArea, overlayOpen, hush);
        }

        outerFrame.Paint.PopClip();

        search.Draw(outerFrame.Paint, outerFrame.Text, textField, outerFrame.Input, outerFrame.Theme, screen, scale);
        recents.Draw(outerFrame.Paint, outerFrame.Text, outerFrame.Input, outerFrame.Theme, screen, scale,
            outerFrame.Router, apps, router.RecentIds);
        var controlResult = control.Draw(outerFrame.Paint, outerFrame.Text, outerFrame.Input, outerFrame.Theme, screen,
            scale, preferences);
        if (controlResult.Recents || router.TakeRecents())
        {
            recents.Open();
        }

        var dockResult = dock.Draw(outerFrame.Paint, outerFrame.Text, outerFrame.Input, outerFrame.Theme, screen,
            scale, outerFrame.DeltaSeconds, currentTab, preferences.ReduceMotion);
        var handleTapped = appsDock.DrawHandle(outerFrame.Paint, outerFrame.Input, outerFrame.Theme, screen, scale,
            outerFrame.DeltaSeconds, preferences.ReduceMotion);
        SoftKeyBar.Paint(outerFrame.Paint, outerFrame.Theme, screen, scale, canGoBack);

        if (handleTapped)
        {
            dock.Close();
            search.Close();
            control.Close();
            recents.Close();
            if (!appsDock.OnApps)
            {
                outerFrame.Router.Home();
            }
        }

        if (appsDock.OnApps)
        {
            dock.Close();
        }

        if (dockResult.Selected is { } selected)
        {
            appsDock.Close();
            outerFrame.Router.Home();
            OpenDestination(selected, dockResult.Section, dockResult.TalkId);
        }

        if (softKey == SoftKey.Home)
        {
            GoHome(outerFrame);
        }

        if (softKey == SoftKey.Recents)
        {
            if (recents.IsOpen)
            {
                recents.Close();
            }
            else
            {
                search.Close();
                dock.Close();
                control.Close();
                recents.Open();
            }
        }

        if (softKey == SoftKey.Back)
        {
            GoBack(outerFrame);
        }

        if (hub.TryTake(out var opened, out var section, out var talkId, out var profileId))
        {
            dock.Close();
            appsDock.Close();
            search.Close();
            control.Close();
            recents.Close();
            outerFrame.Router.Home();
            OpenDestination(opened, section, talkId, profileId);
        }

        if (hub.TryTakeSearch())
        {
            dock.Close();
            appsDock.Close();
            control.Close();
            recents.Close();
            search.Open();
        }

        if (hub.TryTakeMenu())
        {
            search.Close();
            appsDock.Close();
            control.Close();
            recents.Close();
            dock.Open();
        }
    }

    private void DrawDestination(in AppletFrame outerFrame, Rect destArea, Rect clip, bool overlayOpen, float scale)
    {
        if (!destinationsByTab.TryGetValue(currentTab, out var current) ||
            !scrollByTab.TryGetValue(currentTab, out var scroll))
        {
            return;
        }

        var scrolled = destArea.Translate(new Vector2(0f, -scroll.Offset));
        var composeFrame = overlayOpen
            ? outerFrame.WithContent(scrolled).WithInput(SilentInput.Instance)
            : outerFrame.WithContent(scrolled);

        var contentHeight = current.Compose(composeFrame);
        ScrollState.DrawIndicator(outerFrame.Paint, outerFrame.Theme, destArea, contentHeight, scroll.Offset, scale);

        var wheelDelta = !overlayOpen && outerFrame.Input.IsHovering(clip) ? outerFrame.Input.ScrollDelta : 0f;
        scroll.Update(contentHeight, destArea.Height, wheelDelta, scale);
    }

    private void DrawAppsPage(in AppletFrame outerFrame, Rect appsArea, bool overlayOpen, bool hush)
    {
        var applet = CurrentApplet(outerFrame.Router.CurrentAppletId);
        var frame = overlayOpen
            ? outerFrame.WithContent(appsArea).WithInput(SilentInput.Instance)
            : outerFrame.WithContent(appsArea);

        if (applet is not null)
        {
            var back = appsArea.TopSlice(outerFrame.Units(32f));
            outerFrame.Text.DrawIn(back.LeftSlice(outerFrame.Units(28f)), "‹",
                new TextStyle(FontRole.Title, outerFrame.Theme.Palette.Accent, TextAlign.Center));
            outerFrame.Text.DrawIn(back.Inset(new Edges(outerFrame.Units(28f), 0f, 0f, 0f)), "Apps",
                new TextStyle(FontRole.BodyStrong, outerFrame.Theme.Palette.Ink));
            if (!overlayOpen && outerFrame.Input.ConsumeClick(back))
            {
                outerFrame.Router.Home();
                return;
            }

            var rest = appsArea.Inset(new Edges(0f, outerFrame.Units(32f), 0f, 0f));
            applet.Compose(overlayOpen ? frame.WithContent(rest) : outerFrame.WithContent(rest));
            return;
        }

        outerFrame.Text.DrawIn(appsArea.TopSlice(outerFrame.Units(30f)), "Apps",
            new TextStyle(FontRole.Title, outerFrame.Theme.Palette.Ink));
        appsSurface.Draw(frame, appsArea.Inset(new Edges(0f, outerFrame.Units(36f), 0f, 0f)), hush);
    }

    private IApplet? CurrentApplet(string? id)
    {
        if (id is null)
        {
            return null;
        }

        for (var index = 0; index < apps.Count; index++)
        {
            if (string.Equals(apps[index].Manifest.Id, id, StringComparison.Ordinal))
            {
                return apps[index];
            }
        }

        return null;
    }

    private static IReadOnlyList<IApplet> LifeApps(IReadOnlyList<IApplet> applets)
    {
        var list = new List<IApplet>();
        for (var index = 0; index < applets.Count; index++)
        {
            if (applets[index].Manifest.Family == AppletFamily.System)
            {
                continue;
            }

            list.Add(applets[index]);
        }

        list.Sort(static (left, right) => left.Manifest.HomeOrder.CompareTo(right.Manifest.HomeOrder));
        return list;
    }

    private bool CanGoBack()
    {
        if (recents.IsOpen || search.IsOpen || control.IsOpen || dock.IsOpen || appsDock.OnApps)
        {
            return true;
        }

        if (router.CurrentAppletId is not null)
        {
            return true;
        }

        if (currentTab != DestinationTab.Home)
        {
            return true;
        }

        return destinationsByTab.TryGetValue(DestinationTab.Home, out var home) &&
               home is ISectionedDestination panes && panes.CurrentSection != HomePane.Dashboard;
    }

    private void GoHome(in AppletFrame outerFrame)
    {
        search.Close();
        dock.Close();
        control.Close();
        recents.Close();
        appsDock.Close();
        outerFrame.Router.Home();
        OpenDestination(DestinationTab.Home, 0);
    }

    private void GoBack(in AppletFrame outerFrame)
    {
        if (recents.IsOpen)
        {
            recents.Close();
            return;
        }

        if (search.IsOpen)
        {
            search.Close();
            return;
        }

        if (control.IsOpen)
        {
            control.Close();
            return;
        }

        if (dock.IsOpen)
        {
            dock.Close();
            return;
        }

        if (router.CurrentAppletId is not null)
        {
            outerFrame.Router.Back();
            return;
        }

        if (appsDock.OnApps)
        {
            appsDock.Close();
            return;
        }

        OpenDestination(DestinationTab.Home, 0);
    }

    private void OpenDestination(DestinationTab tab, int section, string talkId = "", string profileId = "")
    {
        currentTab = tab;
        if (scrollByTab.TryGetValue(tab, out var jumped))
        {
            jumped.Reset();
        }

        if (destinationsByTab.TryGetValue(tab, out var destination) &&
            destination is ISectionedDestination panes)
        {
            panes.ShowSection(section);
        }

        if (talkId.Length > 0)
        {
            OpenTalk(talkId);
        }

        if (profileId.Length > 0)
        {
            OpenProfile(profileId);
        }
    }

    private void OpenTalk(string threadId)
    {
        if (threadId.Length == 0)
        {
            return;
        }

        currentTab = DestinationTab.Social;
        if (scrollByTab.TryGetValue(currentTab, out var jumped))
        {
            jumped.Reset();
        }

        if (destinationsByTab.TryGetValue(DestinationTab.Social, out var destination) &&
            destination is SocialDestination social)
        {
            social.OpenThread(threadId);
        }
    }

    private void OpenProfile(string peerId)
    {
        if (peerId.Length == 0)
        {
            return;
        }

        currentTab = DestinationTab.Social;
        if (scrollByTab.TryGetValue(currentTab, out var jumped))
        {
            jumped.Reset();
        }

        if (destinationsByTab.TryGetValue(DestinationTab.Social, out var destination) &&
            destination is SocialDestination social)
        {
            social.OpenProfile(peerId);
        }
    }
}
