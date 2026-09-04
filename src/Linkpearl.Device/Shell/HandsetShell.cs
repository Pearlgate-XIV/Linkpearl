using Linkpearl.Applets;
using Linkpearl.Audio;
using Linkpearl.Badges;
using Linkpearl.Destinations;
using Linkpearl.Destinations.Home;
using Linkpearl.Destinations.Profile;
using Linkpearl.Destinations.Social;
using Linkpearl.Device.Shell.Studio;
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
    private readonly ITalk talk;
    private readonly IWifeSync wife;
    private readonly DestinationHub hub;
    private readonly RouteStack router;
    private bool pocketRequest;
    private readonly IReadOnlyList<IApplet> apps;
    private readonly AppsDrawer appsDrawer;
    private readonly QuickAppsTray quickApps = new();
    private readonly UniversalSearchOverlay search;
    private readonly ControlCenter control;
    private readonly RecentsOverlay recents = new();
    private readonly DestinationDock dock = new();
    private readonly AppsDock appsDock = new();
    private readonly StudioSurface studio;
    private readonly ScrollState studioScroll = new();
    private DestinationTab currentTab = DestinationTab.Home;

    public HandsetShell(IReadOnlyList<IDestinationScreen> destinations, IClock clock, IGameSession game,
        DisplayPreferences preferences, ITextField textField, IPearlHub pearl, DestinationHub hub, RouteStack router,
        ITalk talk, IReadOnlyList<IApplet> applets, IWifeSync wife, NoticeLedger notices, IWeatherOracle weather,
        bool development, BadgeBook badges, IHandsetAudio audio, IPublicRadio radio)
    {
        this.clock = clock;
        this.game = game;
        this.preferences = preferences;
        this.textField = textField;
        this.pearl = pearl;
        this.talk = talk;
        this.wife = wife;
        this.hub = hub;
        this.router = router;
        apps = LifeApps(applets);
        appsDrawer = new AppsDrawer(apps, hub, preferences);
        search = new UniversalSearchOverlay(pearl, talk, hub);
        control = new ControlCenter(notices);

        var byTab = new Dictionary<DestinationTab, IDestinationScreen>();
        var scrollState = new Dictionary<DestinationTab, ScrollState>();
        ProfileChrome? profile = null;
        foreach (var destination in destinations)
        {
            byTab[destination.Tab] = destination;
            scrollState[destination.Tab] = new ScrollState();
            if (destination is HomeDestination home)
            {
                profile = home.Profile;
            }
        }

        destinationsByTab = byTab;
        scrollByTab = scrollState;
        if (profile is null)
        {
            throw new InvalidOperationException("Studio home needs the Home destination profile.");
        }

        studio = new StudioSurface(clock, game, pearl, talk, hub, weather, preferences, development, badges, notices,
            profile, LaunchStudioApplet, origin => LaunchStudioApplet("music", origin, "radio"), audio, radio);
    }

    public bool ConsumePocket()
    {
        var ready = pocketRequest;
        pocketRequest = false;
        return ready;
    }

    public bool DrawMinimized(IPaintSurface paint, ITextPainter text, ITheme theme, IInputProbe input, Rect screen,
        float scale, PocketUnlock unlock, float deltaSeconds, bool allowSlide)
    {
        return MinimizedFace.Draw(paint, text, theme, input, screen, scale,
            HandsetClockText.Format(clock, preferences), unlock, deltaSeconds, allowSlide);
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
            quickApps.Close();
            control.Open();
        }

        var dockHeight = SoftKeyBar.Height(scale);
        var content = screen.Inset(new Edges(0f, statusHeight, 0f, dockHeight));
        var appletOpen = CurrentApplet(router.CurrentAppletId) is not null;
        var swipe = content.Inset(new Edges(EdgeHandles.WidthUnits * scale, 0f));
        var awayFromHomeDash = destinationsByTab.TryGetValue(DestinationTab.Home, out var homeScreen) &&
                               homeScreen is ISectionedDestination homePanes &&
                               homePanes.CurrentSection != HomePane.Dashboard;
        var destLane = currentTab != DestinationTab.Home || awayFromHomeDash;
        appsDock.SetExtraScreens(preferences.ExtraHomeScreens);
        appsDock.SetDestLane(destLane);
        var showPager = !appletOpen && !destLane && !search.IsOpen && !dock.IsOpen && !control.IsOpen &&
                        !recents.IsOpen && !quickApps.IsOpen;
        var canSwipe = showPager && !awayFromHomeDash && !studio.BlocksPager;
        var swiped = appsDock.CaptureSwipe(outerFrame.Input, swipe, scale, canSwipe);
        var handleTapped = showPager && appsDock.ConsumeHandle(outerFrame.Input, screen, scale);
        appsDock.Advance(outerFrame.DeltaSeconds, preferences.ReduceMotion);

        var studioArea = appsDock.PageArea(content, appsDock.StudioPage);
        var appsArea = appsDock.PageArea(content, appsDock.AppsPage);
        var destArea = destLane ? appsDock.PageArea(content, 0) : content;
        var onStudio = studioArea.Intersect(content).Width > 8f;
        var onApps = appsArea.Intersect(content).Width > 8f;
        var onDest = destLane && destArea.Intersect(content).Width > 8f;
        var overlayOpen = search.IsOpen || dock.IsOpen || appsDock.IsPaging || control.IsOpen || recents.IsOpen ||
            appsDock.IsDragging || quickApps.IsOpen;

        var homeDock = 0f;
        var destBody = homeDock > 0f ? destArea.Inset(new Edges(0f, 0f, 0f, homeDock)) : destArea;

        var clipped = false;
        try
        {
        outerFrame.Paint.PushClip(content);
        clipped = true;
        if (onStudio && BeginPageClip(outerFrame, content, studioArea))
        {
            try
            {
                DrawStudio(outerFrame, studioArea, overlayOpen);
            }
            catch
            {
                // Studio must not skip soft keys or the pager.
            }

            outerFrame.Paint.PopClip();
        }

        if (!destLane)
        {
            for (var extra = 0; extra < preferences.ExtraHomeScreens; extra++)
            {
                var extraArea = appsDock.PageArea(content, extra);
                if (!BeginPageClip(outerFrame, content, extraArea))
                {
                    continue;
                }

                var extraFrame = overlayOpen
                    ? outerFrame.WithContent(extraArea).WithInput(SilentInput.Instance)
                    : outerFrame.WithContent(extraArea);
                ExtraHomeSurface.Draw(extraFrame, extraArea, extra, preferences.ExtraHomeScreens,
                    () => AddHomeScreen(extra),
                    () => RemoveHomeScreen(extra));
                outerFrame.Paint.PopClip();
            }
        }

        if (onDest && (currentTab != DestinationTab.Home || awayFromHomeDash) &&
            BeginPageClip(outerFrame, content, destBody))
        {
            DrawDestination(outerFrame, destBody, content, overlayOpen, scale);
            outerFrame.Paint.PopClip();
        }

        if (homeDock > 0f)
        {
            var camera = CurrentApplet("camera");
            var dockFrame = overlayOpen ? outerFrame.WithInput(SilentInput.Instance) : outerFrame;
            HomeDock.Draw(dockFrame, destArea.BottomSlice(homeDock), camera, hush,
                () => hub.Open(DestinationTab.Social, SocialPane.Phone),
                () =>
                {
                    appsDock.Open();
                    if (camera is not null)
                    {
                        router.Open(camera.Manifest.Id);
                    }
                });
        }

        if (onApps && BeginPageClip(outerFrame, content, appsArea))
        {
            DrawAppsPage(outerFrame, appsArea, overlayOpen, hush);
            outerFrame.Paint.PopClip();
        }
        }
        finally
        {
            if (clipped)
            {
                outerFrame.Paint.PopClip();
            }
        }

        search.Draw(outerFrame.Paint, outerFrame.Text, textField, outerFrame.Input, outerFrame.Theme, screen, scale);
        recents.Draw(outerFrame, screen, router, apps, ResumeRecent);
        var controlResult = control.Draw(outerFrame, screen, preferences, pearl.Current, talk, clock, wife);
        if (controlResult.Recents || router.TakeRecents())
        {
            quickApps.Close();
            RememberOpenSurface();
            recents.Open();
        }

        if (controlResult.Pocket)
        {
            pocketRequest = true;
        }

        if (controlResult.Tab is { } openedTab)
        {
            quickApps.Close();
            OpenDestination(openedTab, controlResult.Section);
        }

        if (!string.IsNullOrEmpty(controlResult.AppletId))
        {
            quickApps.Close();
            LaunchQuickApp(controlResult.AppletId);
        }

        var dockResult = dock.Draw(outerFrame.Paint, outerFrame.Text, outerFrame.Input, outerFrame.Theme, screen,
            scale, outerFrame.DeltaSeconds, currentTab, preferences.ReduceMotion);
        if (showPager)
        {
            appsDock.DrawHandle(outerFrame.Paint, outerFrame.Input, outerFrame.Theme, screen, scale);
        }
        SoftKeyBar.Paint(outerFrame.Paint, outerFrame.Theme, outerFrame.Input, screen, scale, canGoBack);
        quickApps.Draw(outerFrame, screen, preferences, preferences.ReduceMotion, LaunchQuickApp, OpenQuickCustomize);

        if (handleTapped || swiped)
        {
            dock.Close();
            search.Close();
            control.Close();
            recents.Close();
            quickApps.Close();
            if (!appsDock.OnApps)
            {
                appsDrawer.CloseInner();
                outerFrame.Router.Home();
            }
        }

        if (dockResult.Selected is { } selected)
        {
            appsDock.Close();
            appsDrawer.CloseInner();
            quickApps.Close();
            outerFrame.Router.Home();
            OpenDestination(selected, dockResult.Section, dockResult.TalkId);
        }

        if (softKey == SoftKey.Home)
        {
            if (OnHomeDashboard())
            {
                search.Close();
                dock.Close();
                control.Close();
                recents.Close();
                quickApps.Toggle();
            }
            else
            {
                quickApps.Close();
                GoHome(outerFrame);
            }
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
                recents.Close();
                quickApps.Close();
                RememberOpenSurface();
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
            appsDrawer.CloseInner();
            search.Close();
            control.Close();
            recents.Close();
            quickApps.Close();
            outerFrame.Router.Home();
            OpenDestination(opened, section, talkId, profileId);
        }

        if (hub.TryTakeSearch())
        {
            dock.Close();
            appsDock.Close();
            control.Close();
            recents.Close();
            quickApps.Close();
            search.Open();
        }

        if (hub.TryTakeMenu())
        {
            search.Close();
            appsDock.Close();
            control.Close();
            recents.Close();
            quickApps.Close();
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

    private void DrawStudio(in AppletFrame outerFrame, Rect studioArea, bool overlayOpen)
    {
        var sheet = studio.OverlayOpen;
        if (!sheet)
        {
            studioScroll.Reset();
        }

        var page = sheet ? studioArea.Translate(new Vector2(0f, -studioScroll.Offset)) : studioArea;
        var frame = overlayOpen
            ? outerFrame.WithContent(page).WithInput(SilentInput.Instance)
            : outerFrame.WithContent(page);
        var height = studio.Compose(frame, CurrentApplet("camera"));
        if (!sheet)
        {
            return;
        }

        ScrollState.DrawIndicator(outerFrame.Paint, outerFrame.Theme, studioArea, height, studioScroll.Offset,
            outerFrame.Scale);
        var wheel = !overlayOpen && outerFrame.Input.IsHovering(studioArea) ? outerFrame.Input.ScrollDelta : 0f;
        studioScroll.Update(height, studioArea.Height, wheel, outerFrame.Scale);
    }

    private static bool BeginPageClip(in AppletFrame frame, Rect glass, Rect page)
    {
        var clip = page.Intersect(glass);
        if (clip.IsEmpty)
        {
            return false;
        }

        frame.Paint.PushClip(clip);
        return true;
    }

    private void AddHomeScreen(int fromIndex)
    {
        if (preferences.ExtraHomeScreens >= AppsDock.ExtraCap)
        {
            return;
        }

        preferences.ExtraHomeScreens++;
        appsDock.SetExtraScreens(preferences.ExtraHomeScreens);
        appsDock.ShowExtra(Math.Min(fromIndex + 1, preferences.ExtraHomeScreens - 1));
    }

    private void RemoveHomeScreen(int index)
    {
        if (preferences.ExtraHomeScreens <= 0)
        {
            return;
        }

        preferences.ExtraHomeScreens--;
        appsDock.SetExtraScreens(preferences.ExtraHomeScreens);
        if (preferences.ExtraHomeScreens == 0)
        {
            appsDock.ShowStudio();
            return;
        }

        appsDock.ShowExtra(Math.Clamp(index, 0, preferences.ExtraHomeScreens - 1));
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
            try
            {
                applet.Compose(overlayOpen ? frame.WithContent(rest) : outerFrame.WithContent(rest));
            }
            catch
            {
                // A broken applet must not skip the case, gasket, or soft keys.
            }

            return;
        }

        appsDrawer.Draw(frame, appsArea, hush);
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
        if (studio.OverlayOpen)
        {
            return true;
        }

        if (quickApps.IsOpen || recents.IsOpen || search.IsOpen || control.IsOpen || dock.IsOpen || appsDock.OnApps)
        {
            return true;
        }

        var applet = CurrentApplet(router.CurrentAppletId);
        if (applet is not null)
        {
            return true;
        }

        if (CurrentDestination() is { } dest && dest.CanGoBack)
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
        RememberOpenSurface();
        search.Close();
        dock.Close();
        control.Close();
        recents.Close();
        quickApps.Close();
        appsDock.Close();
        appsDrawer.CloseInner();
        outerFrame.Router.Home();
        OpenDestination(DestinationTab.Home, 0);
    }

    private void GoBack(in AppletFrame outerFrame)
    {
        if (studio.Back())
        {
            return;
        }

        if (quickApps.IsOpen)
        {
            quickApps.Close();
            return;
        }

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

        var applet = CurrentApplet(router.CurrentAppletId);
        if (applet is not null)
        {
            if (applet.Back())
            {
                return;
            }

            outerFrame.Router.Back();
            return;
        }

        if (appsDock.OnApps)
        {
            if (appsDrawer.Back())
            {
                return;
            }

            appsDrawer.CloseInner();
            appsDock.Close();
            return;
        }

        if (appsDock.OnStudio)
        {
            return;
        }

        if (CurrentDestination() is { } dest && dest.Back())
        {
            return;
        }

        if (currentTab != DestinationTab.Home)
        {
            GoHome(outerFrame);
        }
    }

    private IDestinationScreen? CurrentDestination() =>
        destinationsByTab.TryGetValue(currentTab, out var dest) ? dest : null;

    private bool OnHomeDashboard()
    {
        if (appsDock.OnApps || !appsDock.OnStudio || !router.AtHome)
        {
            return false;
        }

        if (search.IsOpen || control.IsOpen || recents.IsOpen || dock.IsOpen)
        {
            return false;
        }

        return !destinationsByTab.TryGetValue(DestinationTab.Home, out var home) ||
               home is not ISectionedDestination panes || panes.CurrentSection == HomePane.Dashboard;
    }

    private void LaunchStudioApplet(string id, Rect origin)
    {
        LaunchStudioApplet(id, origin, null);
    }

    private void LaunchStudioApplet(string id, Rect origin, string? place)
    {
        if (!router.CanOpen(id))
        {
            return;
        }

        appsDock.Open();
        if (place is { Length: > 0 })
        {
            router.OpenFrom(id, origin, place);
            return;
        }

        router.OpenFrom(id, origin);
    }

    private void ResumeRecent(string id)
    {
        recents.Close();
        if (router.CanOpen(id))
        {
            appsDock.Open();
            router.Open(id);
            return;
        }

        if (AppShelf.Find(id) is { Kind: AppKind.Shortcut } spec)
        {
            hub.Open(spec.Tab, spec.Pane);
            return;
        }

        if (string.Equals(id, "you", StringComparison.Ordinal))
        {
            OpenDestination(DestinationTab.You, 0);
        }
    }

    private void LaunchQuickApp(string id)
    {
        if (AppShelf.Find(id) is not AppSpec spec)
        {
            return;
        }

        if (spec.Kind == AppKind.Shortcut)
        {
            hub.Open(spec.Tab, spec.Pane);
            return;
        }

        appsDock.Open();
        router.Open(id);
    }

    private void OpenQuickCustomize()
    {
        appsDock.Open();
        appsDrawer.ShowManage();
    }

    private void OpenDestination(DestinationTab tab, int section, string talkId = "", string profileId = "")
    {
        if (tab == DestinationTab.Home && section == HomePane.Dashboard)
        {
            appsDock.ShowStudio();
        }
        else
        {
            appsDock.ShowDestinations();
        }

        currentTab = tab;
        RememberDestination(tab, section);
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

    private void RememberOpenSurface()
    {
        router.CapturePlaces();
        if (router.CurrentAppletId is not null)
        {
            return;
        }

        RememberDestination(currentTab, CurrentDestination() is ISectionedDestination panes
            ? panes.CurrentSection
            : 0);
    }

    private void RememberDestination(DestinationTab tab, int section)
    {
        if (tab == DestinationTab.Home && section == HomePane.Dashboard)
        {
            return;
        }

        var id = RecentIdFor(tab, section);
        if (id.Length == 0)
        {
            return;
        }

        router.RememberVisit(id, RecentPlaceFor(tab, section));
    }

    private static string RecentIdFor(DestinationTab tab, int section) => tab switch
    {
        DestinationTab.Settings => "settings",
        DestinationTab.You => "you",
        DestinationTab.Explore => section == ExplorePane.Events ? "events" : "market",
        DestinationTab.Social => section switch
        {
            SocialPane.Phone => "phone",
            SocialPane.People => "friends",
            _ => "pearlchat",
        },
        DestinationTab.Home => section == HomePane.Announcements ? "announcements" : string.Empty,
        _ => string.Empty,
    };

    private static string RecentPlaceFor(DestinationTab tab, int section) => tab switch
    {
        DestinationTab.Settings => section == SettingsPane.Presence ? "presence" : "display",
        DestinationTab.Social => section switch
        {
            SocialPane.Phone => "phone",
            SocialPane.People => "people",
            SocialPane.Feed => "feed",
            SocialPane.Communities => "communities",
            SocialPane.Linkshells => "linkshells",
            _ => "messages",
        },
        DestinationTab.Explore => section == ExplorePane.Events ? "events" : "explore",
        DestinationTab.You => "you",
        DestinationTab.Home => "announcements",
        _ => string.Empty,
    };

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
