using System.Linq;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Linkpearl.Applets;
using Linkpearl.Canvas.Input;
using Linkpearl.Canvas.Painting;
using Linkpearl.Canvas.Text;
using Linkpearl.Canvas.Theming;
using Linkpearl.Chassis;
using Linkpearl.Destinations;
using Linkpearl.Destinations.Explore;
using Linkpearl.Destinations.Home;
using Linkpearl.Destinations.Settings;
using Linkpearl.Destinations.Social;
using Linkpearl.Destinations.You;
using Linkpearl.Device.Shell;
using Linkpearl.Device.Windows;
using Linkpearl.Diagnostics;
using Linkpearl.Host.Time;
using Linkpearl.Media;
using Linkpearl.Modules;
using Linkpearl.Net;
using Linkpearl.Platform;
using Linkpearl.Platform.Ffxiv;
using Linkpearl.Preferences;
using Linkpearl.Talk;
using Linkpearl.Theming;
using Linkpearl.Time;
using Microsoft.Extensions.DependencyInjection;

namespace Linkpearl.Host.Composition;

// Composition root: the only place in the plugin that knows every concrete type. Applets never
// see this class; they are handed contracts through the container ILinkpearlModule.Configure
// builds for them.
public sealed class HandsetHost : IDisposable
{
    private readonly IDalamudPluginInterface pluginInterface;
    private readonly ServiceProvider provider;
    private readonly WindowSystem windowSystem = new("Linkpearl");
    private readonly HandsetWindow window;
    private readonly HandsetFontService fonts;
    private readonly FrameworkClock clock;
    private readonly FfxivGameSession session;
    private readonly FfxivChatBridge chat;
    private readonly TalkInbox talk;
    private readonly PearlHub pearl;
    private readonly HandsetConfig config;
    private readonly HandsetShapePreference shapePreference;
    private readonly DisplayPreferences display;
    private readonly IFramework framework;
    private readonly IKeyState keys;
    private readonly DalamudTextField textField;
    private int lastUnread;

    public HandsetHost(IDalamudPluginInterface pluginInterface, IFramework framework, IClientState clientState,
        IObjectTable objectTable, ICondition condition, IDutyState dutyState, IPluginLog pluginLog,
        ITextureProvider textureProvider, IDataManager dataManager, IChatGui chatGui, IPartyList partyList,
        IKeyState keys)
    {
        this.pluginInterface = pluginInterface;
        this.framework = framework;
        this.keys = keys;
        var services = new ServiceCollection();

        var log = new HandsetLog(pluginLog);
        var paths = new HostPaths(pluginInterface.AssemblyLocation.DirectoryName ?? string.Empty,
            pluginInterface.ConfigDirectory.FullName);
        var environment = new HostEnvironment(pluginInterface.Manifest.AssemblyVersion.ToString(),
            pluginInterface.IsDev, System.Environment.OSVersion.Platform == PlatformID.Unix);
        var context = new ModuleContext(paths, log, environment);

        services.AddSingleton<ILinkpearlLog>(log);
        services.AddSingleton(paths);
        services.AddSingleton(environment);

        clock = new FrameworkClock(framework);
        services.AddSingleton<IClock>(clock);
        services.AddSingleton<IFrameLoop>(clock);

        session = new FfxivGameSession(clientState, objectTable, condition, dutyState, partyList, framework, dataManager);
        services.AddSingleton<IGameSession>(session);

        config = pluginInterface.GetPluginConfig() as HandsetConfig ?? new HandsetConfig();
        config.Sanitize();

        pearl = new PearlHub(string.Empty, config.SessionToken, session, clock, log,
            token => clock.Post(() => RememberToken(token)));
        services.AddSingleton<IPearlHub>(pearl);

        chat = new FfxivChatBridge(chatGui, clientState, partyList, objectTable, dataManager, session, framework,
            pluginLog);
        services.AddSingleton<IChatBridge>(chat);
        talk = new TalkInbox(chat, pearl, clock, session, paths.State("talk"));
        services.AddSingleton<ITalk>(talk);

        var preferences = new DisplayPreferences();
        LoadDisplay(preferences);
        preferences.Changed += RememberDisplay;
        display = preferences;
        services.AddSingleton(preferences);

        var chime = new DalamudChime(chatGui, preferences, session);
        services.AddSingleton<IChime>(chime);

        foreach (var module in ModuleDiscovery.Discover())
        {
            module.Configure(services, context);
        }

        provider = services.BuildServiceProvider();

        fonts = new HandsetFontService(pluginInterface);
        var theme = new HandsetTheme(1f, preferences);

        // Clock and Calculator are reached from the apps drawer (left-edge grid handle). Settings
        // stays a destination. RouteStack is the back-stack for those applets.
        var apps = provider.GetServices<IApplet>().ToList();
        var appletById = apps.ToDictionary(applet => applet.Manifest.Id, applet => applet, StringComparer.Ordinal);
        var router = new RouteStack(appletById);

        shapePreference = new HandsetShapePreference(config.ScaleStep, config.Form, config.PositionLocked,
            config.PocketScale, config.Finish, config.ShowLockTab);
        var hub = new DestinationHub();
        var textures = new DalamudTextureSource(textureProvider);
        IReadOnlyList<IDestinationScreen> destinations = new IDestinationScreen[]
        {
            new HomeDestination(clock, session, pearl, talk, hub, preferences, paths, textures),
            new SocialDestination(pearl, clock, talk, session, preferences),
            new ExploreDestination(pearl), new YouDestination(session, pearl),
            new SettingsDestination(shapePreference, preferences, environment, session, pearl, hub, paths, textures),
        };
        var textField = new DalamudTextField(fonts);
        this.textField = textField;
        var shell = new HandsetShell(destinations, clock, session, preferences, textField, pearl, hub, router, talk,
            apps);
        var screenField = new ScreenField(textures, paths, preferences, clock);

        window = new HandsetWindow(shell, fonts, theme, router, shapePreference, screenField, textField, preferences,
            session, textures, paths, RememberShape, RememberOpen, RememberMinimized);
        shapePreference.Changed += OnShapeChanged;
        windowSystem.AddWindow(window);

        pluginInterface.UiBuilder.DisableGposeUiHide = preferences.StayInPortraits;
        preferences.Changed += () =>
            pluginInterface.UiBuilder.DisableGposeUiHide = preferences.StayInPortraits;

        pluginInterface.UiBuilder.Draw += windowSystem.Draw;
        pluginInterface.UiBuilder.OpenMainUi += ToggleHandset;
        framework.Update += OnFrameworkUpdate;

        if (pluginInterface.Reason is PluginLoadReason.Reload or PluginLoadReason.Update || config.HandsetOpen)
        {
            window.IsOpen = true;
            if (config.HandsetMinimized)
            {
                window.SnapMinimized();
            }
        }
    }

    public void ToggleHandset()
    {
        if (!window.IsOpen)
        {
            OpenHandset();
            return;
        }

        if (window.IsMinimized)
        {
            window.Restore();
            return;
        }

        window.Minimize();
    }

    private void OnFrameworkUpdate(IFramework _)
    {
        var unread = talk.UnreadTotal;
        if (window.IsOpen && window.IsMinimized && display.WakeInPocket &&
            !display.Hushed(session.IsInDuty || session.IsInCutscene) && unread > lastUnread)
        {
            window.Restore();
        }

        lastUnread = unread;

        if (!window.IsOpen || window.IsMinimized)
        {
            return;
        }

        if (!textField.Capturing)
        {
            return;
        }

        textField.Harvest(keys);
        keys.ClearAll();
    }

    public void OpenHandset()
    {
        window.IsOpen = true;
        window.Restore();
    }

    public void Dispose()
    {
        shapePreference.Changed -= OnShapeChanged;
        display.Changed -= RememberDisplay;
        RememberShape();
        RememberDisplay();
        RememberOpen(window.IsOpen);
        RememberMinimized(window.IsMinimized);
        framework.Update -= OnFrameworkUpdate;
        pluginInterface.UiBuilder.Draw -= windowSystem.Draw;
        pluginInterface.UiBuilder.OpenMainUi -= ToggleHandset;
        windowSystem.RemoveAllWindows();
        fonts.Dispose();
        talk.Dispose();
        chat.Dispose();
        pearl.Dispose();
        session.Dispose();
        clock.Dispose();
        provider.Dispose();
    }

    private void OnShapeChanged()
    {
        if (window.IsResizing)
        {
            return;
        }

        RememberShape();
    }

    private void RememberShape()
    {
        config.ScaleStep = HandsetSizeCatalog.SnapToStep(shapePreference.ScaleStep);
        config.Form = shapePreference.Form;
        config.PositionLocked = shapePreference.PositionLocked;
        config.PocketScale = shapePreference.PocketScale;
        config.Finish = shapePreference.Finish;
        config.ShowLockTab = shapePreference.ShowLockTab;
        pluginInterface.SavePluginConfig(config);
    }

    private void LoadDisplay(DisplayPreferences preferences)
    {
        preferences.Use24HourClock = config.Use24HourClock;
        preferences.Appearance = (AppearanceMode)config.Appearance;
        preferences.WallpaperId = config.WallpaperId;
        preferences.CustomPlateFile = config.CustomPlateFile;
        preferences.CustomBannerFile = config.CustomBannerFile;
        preferences.Colorway = config.Colorway;
        preferences.Core = config.Core;
        preferences.Shade = (ShadeLevel)config.Shade;
        preferences.ClockFace = (ClockFace)config.ClockFace;
        preferences.Lettering = (LetteringSize)config.Lettering;
        preferences.ShowWorld = config.ShowWorld;
        preferences.ShowMarks = config.ShowMarks;
        preferences.ReduceMotion = config.ReduceMotion;
        preferences.Quiet = config.Quiet;
        preferences.QuietWhenBusy = config.QuietWhenBusy;
        preferences.WakeInPocket = config.WakeInPocket;
        preferences.StayInPortraits = config.StayInPortraits;
        preferences.TuckForCutscenes = config.TuckForCutscenes;
        preferences.Fight = (FightPresence)config.Fight;
        preferences.Layout = (TuneLayout)config.TuneLayout;
        if (config.Replies.Length > 0)
        {
            preferences.SetReplies(config.Replies);
        }
    }

    private void RememberDisplay()
    {
        config.Use24HourClock = display.Use24HourClock;
        config.Appearance = (int)display.Appearance;
        config.WallpaperId = display.WallpaperId;
        config.CustomPlateFile = display.CustomPlateFile;
        config.CustomBannerFile = display.CustomBannerFile;
        config.Colorway = display.Colorway;
        config.Core = display.Core;
        config.Shade = (int)display.Shade;
        config.ClockFace = (int)display.ClockFace;
        config.Lettering = (int)display.Lettering;
        config.ShowWorld = display.ShowWorld;
        config.ShowMarks = display.ShowMarks;
        config.ReduceMotion = display.ReduceMotion;
        config.Quiet = display.Quiet;
        config.QuietWhenBusy = display.QuietWhenBusy;
        config.WakeInPocket = display.WakeInPocket;
        config.StayInPortraits = display.StayInPortraits;
        config.TuckForCutscenes = display.TuckForCutscenes;
        config.Fight = (int)display.Fight;
        config.TuneLayout = (int)display.Layout;
        config.Replies = display.Replies.ToArray();
        pluginInterface.SavePluginConfig(config);
    }

    private void RememberToken(string? token)
    {
        config.SessionToken = token ?? string.Empty;
        pluginInterface.SavePluginConfig(config);
    }

    private void RememberOpen(bool open)
    {
        config.HandsetOpen = open;
        pluginInterface.SavePluginConfig(config);
    }

    private void RememberMinimized(bool minimized)
    {
        config.HandsetMinimized = minimized;
        pluginInterface.SavePluginConfig(config);
    }
}
