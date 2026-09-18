using Linkpearl.Applets;
using Linkpearl.Audio;
using Linkpearl.Badges;
using Linkpearl.Destinations;
using Linkpearl.Destinations.Explore;
using Linkpearl.Destinations.Home;
using Linkpearl.Destinations.Profile;
using Linkpearl.Destinations.Settings;
using Linkpearl.Destinations.Social;
using Linkpearl.Device.Shell.Studio;
using Linkpearl.Device.Time;
using Linkpearl.Geometry;
using Linkpearl.Input;
using Linkpearl.Net;
using Linkpearl.Platform;
using Linkpearl.Painting;
using Linkpearl.Persistence;
using Linkpearl.Preferences;
using Linkpearl.Talk;
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
    private readonly RouteTrail router;
    private bool pocketRequest;
    private bool powerOffRequest;
    private string pendingPocketNoticeId = string.Empty;
    private readonly List<IApplet> apps;
    private readonly GlassEdit glass = new();
    private readonly AppsDrawer appsDrawer;
    private readonly QuickAppsTray quickApps = new();
    private readonly UniversalSearchOverlay search;
    private readonly ControlCenter control;
    private readonly NoticeLedger notices;
    private readonly NoticeBanner banner = new();
    private readonly RecentsOverlay recents = new();
    private readonly DestinationDock dock = new();
    private readonly AppsDock appsDock = new();
    private readonly StudioSurface studio;
    private readonly ScrollState studioScroll = new();
    private readonly List<ShellSeat> trail = [];
    private ShellSeat? launchSeat;
    private DestinationTab currentTab = DestinationTab.Home;

    public HandsetShell(IReadOnlyList<IDestinationScreen> destinations, IClock clock, IGameSession game,
        DisplayPreferences preferences, ITextField textField, IPearlHub pearl, DestinationHub hub, RouteTrail router,
        ITalk talk, IReadOnlyList<IApplet> applets, IWifeSync wife, NoticeLedger notices, IWeatherOracle weather,
        ISkyDesk sky, bool development, BadgeBook badges, IHandsetAudio audio, IPublicRadio radio, IStationMarks marks,
        ISettings<SearchScratch> searchDraft)
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
        appsDrawer = new AppsDrawer(apps, hub, preferences, glass, talk, notices, RememberLaunchSeat);
        search = new UniversalSearchOverlay(pearl, talk, hub, searchDraft);
        this.notices = notices;
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

        studio = new StudioSurface(clock, game, pearl, talk, hub, weather, sky, preferences, development, badges, notices,
            profile, LaunchStudioApplet, (origin, hint) => LaunchStudioApplet("music", origin, hint), audio, radio, marks,
            glass, searchDraft);
    }

    public bool ConsumePocket()
    {
        var ready = pocketRequest || game.TakePocketCue();
        pocketRequest = false;
        return ready;
    }

    public bool ConsumePowerOff()
    {
        var ready = powerOffRequest;
        powerOffRequest = false;
        return ready;
    }

    public bool HoldsWindow => recents.IsOpen || recents.HoldsPointer;

    public void AlignAfterWake() => appsDock.SyncToPage();

    public int PocketNoticeCount() => notices.Count(pearl.Current, talk, clock);

    public bool DrawMinimized(in AppletFrame frame, Rect screen, PocketUnlock unlock, bool allowSlide, PocketFace face)
    {
        var tray = notices.Visible(pearl.Current, talk, clock);
        var mark = tray.Count > 0 ? NoticeMarks.For(tray[0].Kind) : "pearlchat";
        return MinimizedFace.Draw(frame, screen, unlock, allowSlide, HandsetClockText.Format(clock, preferences),
            HandsetClockText.FormatDate(clock), tray.Count, mark, face);
    }

    public void OfferPocketNoticeTap()
    {
        if (pendingPocketNoticeId.Length > 0)
        {
            return;
        }

        var tray = notices.Visible(pearl.Current, talk, clock);
        if (tray.Count == 0)
        {
            return;
        }

        pendingPocketNoticeId = tray[0].Id;
    }

    public void CancelPocketNotice() => pendingPocketNoticeId = string.Empty;

    public void CommitPocketNoticeAfterWake()
    {
        var id = pendingPocketNoticeId;
        pendingPocketNoticeId = string.Empty;
        if (id.Length == 0 || !notices.TryGet(id, out var item))
        {
            return;
        }

        NoticeLaunch.ToHub(hub, item.Kind, item.Tab, item.Section, item.TargetId);
        notices.Dismiss(id);
    }

    public void Draw(in AppletFrame outerFrame, Rect screen)
    {
        router.RevokeDisallowed();
        if (StaffNoticeSheet.Draw(outerFrame, screen, pearl.Current, pearl, notices))
        {
            return;
        }

        var scale = outerFrame.Scale;
        var hush = preferences.Hushed(game.IsInDuty || game.IsInCutscene);
        banner.Observe(pearl.Current, talk, clock, notices, hush);
        banner.Capture(outerFrame, screen, hub, notices);
        var statusHeight = StatusStrip.Height(scale);
        StatusStrip.Draw(outerFrame, screen, HandsetClockText.Format(clock, preferences),
            game.Character.WorldName, preferences.ShowWorld, preferences.ShowMarks);

        var canGoBack = CanGoBack();
        var softKey = SoftKeyBar.Consume(outerFrame.Input, screen, scale, canGoBack);
        var stripHandle = ControlCenter.HandleOn(screen, scale, control.IsOpen);
        if (softKey == SoftKey.None && !search.IsOpen && !recents.IsOpen && !control.IsPulling &&
            outerFrame.Input.ConsumeClick(stripHandle))
        {
            quickApps.Close();
            if (control.IsOpen)
            {
                control.Close();
            }
            else
            {
                control.Open();
            }
        }

        var appletOpen = CurrentApplet(router.CurrentAppletId) is not null;
        var dockHeight = appletOpen ? SoftKeyBar.AppLift(scale) : SoftKeyBar.Height(scale);
        var content = screen.Inset(new Edges(0f, statusHeight, 0f, dockHeight));
        var swipe = content.Inset(new Edges(EdgeHandles.WidthUnits * scale, 0f));
        var awayFromHomeDash = destinationsByTab.TryGetValue(DestinationTab.Home, out var homeScreen) &&
                               homeScreen is ISectionedDestination homePanes &&
                               homePanes.CurrentSection != HomePane.Dashboard;
        var destLane = currentTab != DestinationTab.Home || awayFromHomeDash;
        appsDock.SetExtraScreens(preferences.ExtraHomeScreens);
        appsDock.SetAppScreens(preferences.AppScreenCount);
        appsDock.SetDestLane(destLane);
        var showPager = !appletOpen && !destLane && !search.IsOpen && !dock.IsOpen && !control.IsOpen &&
                        !recents.IsOpen && !quickApps.IsOpen;
        var canSwipe = showPager && !awayFromHomeDash &&
                       !studio.BlocksPager(outerFrame.Input.Cursor, outerFrame.Input.IsHeld()) &&
                       !appsDrawer.BlocksPager(outerFrame.Input.Cursor, outerFrame.Input.IsHeld()) &&
                       !banner.BlocksPager;
        var swiped = appsDock.CaptureSwipe(outerFrame.Input, swipe, scale, canSwipe);
        var handleTapped = showPager && appsDock.ConsumeHandle(outerFrame.Input, screen, scale);
        appsDock.Advance(outerFrame.DeltaSeconds, preferences.ReduceMotion);

        var studioArea = appsDock.PageArea(content, appsDock.StudioPage);
        var destArea = destLane ? appsDock.PageArea(content, 0) : content;
        var onStudio = studioArea.Intersect(content).Width > 8f;
        var onApps = false;
        for (var appScreen = 0; appScreen < appsDock.AppScreenCount; appScreen++)
        {
            var area = appsDock.PageArea(content, appsDock.FirstAppsPage + appScreen);
            if (area.Intersect(content).Width > 8f)
            {
                onApps = true;
                break;
            }
        }
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

                var extraMute = overlayOpen && studio.FlyingId is null;
                var extraFrame = extraMute
                    ? outerFrame.WithContent(extraArea).WithInput(SilentInput.Instance)
                    : outerFrame.WithContent(extraArea);
                ExtraHomeSurface.Draw(extraFrame, extraArea, extra, preferences.ExtraHomeScreens,
                    () => AddHomeScreen(extra),
                    () => RemoveHomeScreen(extra),
                    (page, _) => studio.ComposeExtra(page, extra));
                outerFrame.Paint.PopClip();
            }
        }

        if (onDest && (currentTab != DestinationTab.Home || awayFromHomeDash) &&
            BeginPageClip(outerFrame, content, destBody))
        {
            try
            {
                DrawDestination(outerFrame, destBody, content, overlayOpen, scale);
            }
            finally
            {
                outerFrame.Paint.PopClip();
            }
        }

        if (homeDock > 0f)
        {
            var camera = CurrentApplet("camera");
            var dockFrame = overlayOpen ? outerFrame.WithInput(SilentInput.Instance) : outerFrame;
            HomeDock.Draw(dockFrame, destArea.BottomSlice(homeDock), camera, hush,
                () => LaunchQuickApp("phone"),
                () =>
                {
                    RememberLaunchSeat();
                    appsDock.Open();
                    if (camera is not null)
                    {
                        router.Open(camera.Manifest.Id);
                    }
                });
        }

        if (onApps)
        {
            for (var appScreen = 0; appScreen < appsDock.AppScreenCount; appScreen++)
            {
                var appsArea = appsDock.PageArea(content, appsDock.FirstAppsPage + appScreen);
                if (!BeginPageClip(outerFrame, content, appsArea))
                {
                    continue;
                }

                var mute = appScreen != appsDock.AppScreenIndex ||
                           (overlayOpen && appsDrawer.FlyingId is null);
                DrawAppsPage(outerFrame, appsArea, mute, hush, appScreen);
                outerFrame.Paint.PopClip();
            }
        }
        }
        finally
        {
            if (clipped)
            {
                outerFrame.Paint.PopClip();
            }
        }

        ApplyStudioNudge();
        ApplyAppPageNudge();

        search.Draw(outerFrame.Paint, outerFrame.Text, textField, outerFrame.Input, outerFrame.Theme, screen, scale);
        recents.Draw(outerFrame, screen, router, apps, preferences, clock, ResumeRecent);
        var controlResult = control.Draw(outerFrame, screen, preferences, pearl.Current, pearl, talk, clock, wife, game,
            allowStrip: !search.IsOpen && !recents.IsOpen);
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

        if (controlResult.PowerOff)
        {
            powerOffRequest = true;
        }

        if (controlResult.Tab is { } openedTab)
        {
            quickApps.Close();
            if (controlResult.NoticeId.Length > 0 &&
                openedTab == DestinationTab.Settings &&
                !controlResult.NoticeId.StartsWith("staff:", StringComparison.Ordinal))
            {
                pearl.MarkStaffNotice(controlResult.NoticeId);
            }

            OpenDestination(openedTab, controlResult.Section, controlResult.TalkId, controlResult.ProfileId,
                controlResult.NoticeId);
        }

        if (!string.IsNullOrEmpty(controlResult.AppletId))
        {
            quickApps.Close();
            LaunchQuickApp(controlResult.AppletId, controlResult.RouteHint);
        }

        var dockResult = dock.Draw(outerFrame.Paint, outerFrame.Text, outerFrame.Input, outerFrame.Theme, screen,
            scale, outerFrame.DeltaSeconds, currentTab, preferences.ReduceMotion);
        if (showPager)
        {
            AppsDock.DrawHandle(outerFrame.Paint, outerFrame.Input, outerFrame.Theme, screen, scale);
        }
        SoftKeyBar.Paint(outerFrame.Paint, outerFrame.Theme, outerFrame.Input, screen, scale, canGoBack);
        banner.Draw(outerFrame, screen, hub, notices);
        quickApps.Draw(outerFrame, screen, preferences, preferences.ReduceMotion, LaunchQuickApp, OpenQuickCustomize,
            notices, talk, preferences.Hushed(game.IsInDuty || game.IsInCutscene));

        if (handleTapped || swiped)
        {
            dock.Close();
            search.Close();
            control.Close();
            recents.Close();
            quickApps.Close();
            if (!appsDock.OnApps)
            {
                if (appsDrawer.FlyingId is { Length: > 0 } flying)
                {
                    studio.AdoptDrag(flying);
                    appsDrawer.ReleaseDrag();
                }
                else
                {
                    appsDrawer.CloseInner();
                }

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
            if (appletOpen)
            {
                ReturnFromApp(outerFrame);
            }
            else if (!OnHomeDashboard())
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

        if (hub.TryTakeApplet(out var appletId, out var appletHint))
        {
            dock.Close();
            appsDock.Close();
            appsDrawer.CloseInner();
            search.Close();
            control.Close();
            recents.Close();
            quickApps.Close();
            LaunchQuickApp(appletId, appletHint);
        }

        if (hub.TryTake(out var opened, out var section, out var talkId, out var profileId, out var noticeId))
        {
            dock.Close();
            appsDock.Close();
            appsDrawer.CloseInner();
            search.Close();
            control.Close();
            recents.Close();
            quickApps.Close();
            outerFrame.Router.Home();
            OpenDestination(opened, section, talkId, profileId, noticeId, hub.TakeLabel());
            if (destinationsByTab.TryGetValue(DestinationTab.Explore, out var exploreScreen) &&
                exploreScreen is ExploreDestination explore)
            {
                var storyAuthor = hub.TakeStory();
                if (storyAuthor.Length > 0)
                {
                    explore.OpenStory(storyAuthor);
                }
            }
        }

        if (hub.TryTakeSearch())
        {
            // Universal search has no product chrome entry. StudioHunt is the search surface.
            _ = search;
        }

        if (hub.TryTakeMenu())
        {
            // DestinationDock has no product chrome entry. Destinations open from Studio and Apps.
            _ = dock;
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

        float contentHeight;
        try
        {
            contentHeight = current.Compose(composeFrame);
        }
        catch (Exception ex)
        {
            outerFrame.Text.DrawIn(destArea.Inset(outerFrame.Units(16f)),
                "This screen hit an error.\n" + ex.GetType().Name + ": " + ex.Message,
                new TextStyle(FontRole.Caption, outerFrame.Theme.Palette.InkMuted));
            return;
        }
        if (current is SettingsDestination settings && settings.TryTakeScrollIntoView(out var focusY))
        {
            scroll.Jump(focusY);
        }

        _ = clip;
        _ = scale;
        scroll.Apply(outerFrame, destArea, contentHeight, live: !overlayOpen);
    }

    private void DrawStudio(in AppletFrame outerFrame, Rect studioArea, bool overlayOpen)
    {
        var sheet = studio.OverlayOpen;
        if (!sheet)
        {
            studioScroll.Reset();
        }

        var page = sheet ? studioArea.Translate(new Vector2(0f, -studioScroll.Offset)) : studioArea;
        var mute = overlayOpen && studio.FlyingId is null;
        var frame = mute
            ? outerFrame.WithContent(page).WithInput(SilentInput.Instance)
            : outerFrame.WithContent(page);
        var height = studio.Compose(frame, CurrentApplet("camera"));
        if (!sheet)
        {
            return;
        }

        studioScroll.Apply(outerFrame, studioArea, height, live: !overlayOpen);
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

    private void DrawAppsPage(in AppletFrame outerFrame, Rect appsArea, bool overlayOpen, bool hush, int screenIndex)
    {
        var applet = CurrentApplet(outerFrame.Router.CurrentAppletId);
        var frame = overlayOpen
            ? outerFrame.WithContent(appsArea).WithInput(SilentInput.Instance)
            : outerFrame.WithContent(appsArea);

        if (applet is not null)
        {
            if (screenIndex != appsDock.AppScreenIndex)
            {
                return;
            }

            AppGround.Paint(outerFrame, appsArea, applet.Manifest.Id);

            try
            {
                applet.Compose(overlayOpen ? frame.WithContent(appsArea) : outerFrame.WithContent(appsArea));
            }
            catch (Exception ex)
            {
                AppGround.Paint(outerFrame, appsArea, applet.Manifest.Id);
                outerFrame.Text.DrawIn(appsArea.Inset(outerFrame.Units(16f)),
                    "This app hit an error.\n" + ex.GetType().Name + ": " + ex.Message,
                    new TextStyle(FontRole.Caption, outerFrame.Theme.Palette.InkMuted));
            }

            return;
        }

        appsDrawer.Draw(frame, appsArea, hush, screenIndex, interact: !overlayOpen);
    }

    private void ApplyStudioNudge()
    {
        var nudge = studio.PageNudge;
        if (nudge == 0)
        {
            return;
        }

        studio.ClearNudge();
        if (studio.FlyingId is { Length: > 2 } flying &&
            flying.StartsWith("w:", StringComparison.Ordinal) &&
            ContainsDefaultDock(flying[2..]))
        {
            ParkFlyingDock(nudge, flying[2..]);
            return;
        }

        if (nudge > 0)
        {
            appsDock.ShowAppScreen(0);
            if (studio.FlyingId is { Length: > 0 } app && ShelfIdOf(app) is { } shelf)
            {
                appsDrawer.AdoptDrag(shelf);
                studio.ReleaseDrag();
            }

            return;
        }

        if (appsDock.ExtraCount > 0)
        {
            appsDock.Step(-1);
        }
    }

    private void ParkFlyingDock(int nudge, string widget)
    {
        if (nudge < 0)
        {
            if (appsDock.ExtraCount <= 0)
            {
                return;
            }

            appsDock.Step(-1);
        }
        else
        {
            if (appsDock.OnStudio)
            {
                return;
            }

            appsDock.Step(1);
        }

        preferences.SetStudioWidgetSeat(widget, appsDock.OnExtra ? appsDock.ExtraIndex : -1);
    }

    private static string? ShelfIdOf(string dragId)
    {
        var id = dragId.StartsWith("w:", StringComparison.Ordinal) ||
                 dragId.StartsWith("a:", StringComparison.Ordinal)
            ? dragId[2..]
            : dragId;
        if (id.Length == 0 || ContainsDefaultDock(id))
        {
            return null;
        }

        return id;
    }

    private static bool ContainsDefaultDock(string id)
    {
        for (var index = 0; index < DisplayPreferences.DefaultStudioWidgets.Length; index++)
        {
            if (string.Equals(DisplayPreferences.DefaultStudioWidgets[index], id, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private void ApplyAppPageNudge()
    {
        var nudge = appsDrawer.PageNudge;
        if (nudge == 0)
        {
            return;
        }

        appsDrawer.ClearNudge();
        var from = appsDock.AppScreenIndex;
        var flying = appsDrawer.FlyingId;
        if (nudge == 2)
        {
            if (!preferences.TryAddAppScreen())
            {
                return;
            }

            appsDock.SetAppScreens(preferences.AppScreenCount);
            appsDock.ShowAppScreen(preferences.AppScreenCount - 1);
            return;
        }

        if (nudge == 3)
        {
            if (from <= 0 || !preferences.RemoveAppScreen(from))
            {
                return;
            }

            appsDock.SetAppScreens(preferences.AppScreenCount);
            appsDock.ShowAppScreen(Math.Max(0, from - 1));
            return;
        }

        if (nudge > 0)
        {
            if (from >= preferences.AppScreenCount - 1 && !preferences.TryAddAppScreen())
            {
                return;
            }

            appsDock.SetAppScreens(preferences.AppScreenCount);
            appsDock.Step(1);
        }
        else if (from > 0)
        {
            appsDock.Step(-1);
        }
        else
        {
            appsDock.ShowStudio();
            if (flying is { Length: > 0 })
            {
                studio.AdoptDrag(flying);
                appsDrawer.ReleaseDrag();
            }
        }

        if (appsDrawer.FlyingId is null && studio.FlyingId is null)
        {
            preferences.PruneEmptyAppScreens(appsDock.AppScreenIndex);
            appsDock.SetAppScreens(preferences.AppScreenCount);
        }
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

    private static List<IApplet> LifeApps(IReadOnlyList<IApplet> applets)
    {
        var list = new List<IApplet>();
        for (var index = 0; index < applets.Count; index++)
        {
            if (applets[index].Manifest.Family == AppletFamily.System ||
                AppShelf.IsHidden(applets[index].Manifest.Id))
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
        if (studio.OverlayOpen || studio.Editing)
        {
            return true;
        }

        if (quickApps.IsOpen || recents.IsOpen || search.IsOpen || control.IsOpen || dock.IsOpen ||
            appsDock.OnApps || appsDock.OnExtra || trail.Count > 0)
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
        trail.Clear();
        launchSeat = null;
        search.Close();
        dock.Close();
        control.Close();
        recents.Close();
        quickApps.Close();
        appsDock.Close();
        appsDrawer.CloseInner();
        outerFrame.Router.Home();
        OpenDestination(DestinationTab.Home, 0, remember: false);
    }

    private void ReturnFromApp(in AppletFrame outerFrame)
    {
        RememberOpenSurface();
        search.Close();
        dock.Close();
        control.Close();
        recents.Close();
        quickApps.Close();
        appsDrawer.CloseInner();
        outerFrame.Router.Home();
        var seat = launchSeat;
        launchSeat = null;
        trail.Clear();
        ApplySeat(seat ?? new ShellSeat(ShellKind.Studio, 0, DestinationTab.Home));
    }

    private void GoBack(in AppletFrame outerFrame)
    {
        if (studio.HuntOpen)
        {
            studio.DismissHunt();
            textField.Release();
            return;
        }

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
            textField.Release();
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
            RestoreSeat();
            return;
        }

        if (appsDock.OnApps)
        {
            if (appsDrawer.Back())
            {
                return;
            }

            if (appsDock.AppScreenIndex > 0)
            {
                appsDock.Step(-1);
                preferences.PruneEmptyAppScreens(appsDock.AppScreenIndex);
                appsDock.SetAppScreens(preferences.AppScreenCount);
                return;
            }

            appsDrawer.CloseInner();
            appsDock.Close();
            return;
        }

        if (appsDock.OnExtra)
        {
            appsDock.ShowStudio();
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

        if (trail.Count > 0)
        {
            RestoreSeat();
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
        if (AppShelf.IsHidden(id))
        {
            return;
        }

        if (TryOpenShortcut(id))
        {
            return;
        }

        if (!router.CanOpen(id))
        {
            return;
        }

        RememberLaunchSeat();
        appsDock.CoverWithApp();
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
        if (AppShelf.IsHidden(id))
        {
            return;
        }

        if (TryOpenShortcut(id))
        {
            return;
        }

        if (router.CanOpen(id))
        {
            RememberLaunchSeat();
            appsDock.CoverWithApp();
            router.Open(id);
            return;
        }

        if (string.Equals(id, "you", StringComparison.Ordinal))
        {
            OpenDestination(DestinationTab.You, 0);
        }
    }

    private bool TryOpenShortcut(string id)
    {
        if (AppShelf.Find(id) is not { Kind: AppKind.Shortcut, Hidden: false } spec)
        {
            return false;
        }

        hub.Open(spec.Tab, spec.Pane);
        return true;
    }

    private void LaunchQuickApp(string id) => LaunchQuickApp(id, "");

    private void LaunchQuickApp(string id, string routeHint)
    {
        if (AppShelf.Find(id) is not AppSpec spec || spec.Hidden)
        {
            return;
        }

        if (spec.Kind == AppKind.Shortcut)
        {
            hub.Open(spec.Tab, spec.Pane);
            return;
        }

        if (!router.CanOpen(id))
        {
            return;
        }

        RememberLaunchSeat();
        appsDock.CoverWithApp();
        if (routeHint.Length > 0)
        {
            router.Open(id, routeHint);
        }
        else
        {
            router.Open(id);
        }
    }

    private void OpenQuickCustomize()
    {
        RememberLaunchSeat();
        appsDock.CoverWithApp();
        router.Open("appstore");
    }

    private void OpenDestination(DestinationTab tab, int section, string talkId = "", string profileId = "",
        string noticeId = "", string label = "", bool remember = true)
    {
        if (remember)
        {
            RememberSeat();
        }

        if (tab == DestinationTab.Social)
        {
            if (section == SocialPane.Phone)
            {
                LaunchQuickApp("phone");
                return;
            }

            LaunchQuickApp(section == SocialPane.People ? "friends" : "pearlchat");
            if (destinationsByTab.TryGetValue(DestinationTab.Social, out var social) &&
                social is ISectionedDestination socialPanes)
            {
                socialPanes.ShowSection(section);
            }

            if (talkId.Length > 0)
            {
                OpenTalk(talkId);
            }

            if (profileId.Length > 0)
            {
                OpenProfile(profileId);
            }

            return;
        }

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

        if (tab == DestinationTab.Settings && label.Length > 0 &&
            destinationsByTab.TryGetValue(DestinationTab.Settings, out var settings) &&
            settings is SettingsDestination book)
        {
            book.RevealTopic(label);
        }

        if (noticeId.Length > 0 && destinationsByTab.TryGetValue(DestinationTab.Home, out var home) &&
            home is HomeDestination dashboard)
        {
            dashboard.OpenAnnouncement(noticeId);
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
        DestinationTab.Explore => section == ExplorePane.ForYou
            ? "stories"
            : section == ExplorePane.Events
                ? "events"
                : "market",
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
            SocialPane.People => "friends",
            SocialPane.Feed => "feed",
            SocialPane.Communities => "communities",
            SocialPane.Linkshells => "linkshells",
            _ => "direct",
        },
        DestinationTab.Explore => section == ExplorePane.ForYou
            ? "stories"
            : section == ExplorePane.Events
                ? "events"
                : "explore",
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

        LaunchQuickApp("pearlchat");
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

        LaunchQuickApp("friends");
        if (destinationsByTab.TryGetValue(DestinationTab.Social, out var destination) &&
            destination is SocialDestination social)
        {
            social.OpenProfile(peerId);
        }
    }

    private void RememberLaunchSeat()
    {
        launchSeat = CaptureSeat();
        RememberSeat();
    }

    private void RememberSeat()
    {
        var seat = CaptureSeat();
        if (trail.Count > 0 && trail[^1].Equals(seat))
        {
            return;
        }

        trail.Add(seat);
        if (trail.Count > 16)
        {
            trail.RemoveAt(0);
        }
    }

    private void RestoreSeat()
    {
        if (trail.Count == 0)
        {
            appsDock.Retreat();
            return;
        }

        var seat = trail[^1];
        trail.RemoveAt(trail.Count - 1);
        ApplySeat(seat);
    }

    private void ApplySeat(ShellSeat seat)
    {
        switch (seat.Kind)
        {
            case ShellKind.Extra:
                currentTab = DestinationTab.Home;
                ShowHomeDashboard();
                appsDock.SetDestLane(false);
                if (seat.Index >= 0 && seat.Index < preferences.ExtraHomeScreens)
                {
                    appsDock.ShowExtra(seat.Index);
                    return;
                }

                appsDock.ShowStudio();
                return;
            case ShellKind.Apps:
                currentTab = DestinationTab.Home;
                ShowHomeDashboard();
                appsDock.SetDestLane(false);
                appsDock.ShowAppScreen(seat.Index);
                return;
            case ShellKind.Dest:
                OpenDestination(seat.Tab, seat.Index, remember: false);
                return;
            default:
                currentTab = DestinationTab.Home;
                ShowHomeDashboard();
                appsDock.SetDestLane(false);
                appsDock.ShowStudio();
                return;
        }
    }

    private void ShowHomeDashboard()
    {
        if (destinationsByTab.TryGetValue(DestinationTab.Home, out var home) &&
            home is ISectionedDestination panes)
        {
            panes.ShowSection(HomePane.Dashboard);
        }
    }

    private ShellSeat CaptureSeat()
    {
        if (appsDock.OnApps)
        {
            return new ShellSeat(ShellKind.Apps, appsDock.AppScreenIndex, currentTab);
        }

        if (appsDock.OnExtra)
        {
            return new ShellSeat(ShellKind.Extra, appsDock.ExtraIndex, DestinationTab.Home);
        }

        var section = CurrentDestination() is ISectionedDestination panes ? panes.CurrentSection : 0;
        if (currentTab != DestinationTab.Home || section != HomePane.Dashboard)
        {
            return new ShellSeat(ShellKind.Dest, section, currentTab);
        }

        return new ShellSeat(ShellKind.Studio, 0, DestinationTab.Home);
    }

    private enum ShellKind : byte
    {
        Studio = 0,
        Extra = 1,
        Apps = 2,
        Dest = 3,
    }

    private readonly record struct ShellSeat(ShellKind Kind, int Index, DestinationTab Tab);
}
