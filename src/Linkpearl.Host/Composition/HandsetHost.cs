using System.Linq;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Linkpearl.Applets;
using Linkpearl.Audio;
using Linkpearl.Badges;
using Linkpearl.Canvas.Input;
using Linkpearl.Canvas.Painting;
using Linkpearl.Canvas.Text;
using Linkpearl.Canvas.Theming;
using Linkpearl.Chassis;
using Linkpearl.Chat;
using Linkpearl.Destinations;
using Linkpearl.Destinations.Explore;
using Linkpearl.Destinations.Home;
using Linkpearl.Destinations.Settings;
using Linkpearl.Destinations.Social;
using Linkpearl.Destinations.You;
using Linkpearl.Device.Shell;
using Linkpearl.Device.Windows;
using Linkpearl.Diagnostics;
using Linkpearl.Feedback;
using Linkpearl.Host.Feedback;
using Linkpearl.Host.Platform;
using Linkpearl.Host.Time;
using Linkpearl.Host.Windows;
using Linkpearl.Media;
using Linkpearl.Modules;
using Linkpearl.Net;
using Linkpearl.Notices;
using Linkpearl.Net.Market;
using Linkpearl.Net.Radio;
using Linkpearl.Pearls;
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
    private readonly HandsetPlacement placement;
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
    private readonly TalkPopoutBoard popouts;
    private readonly RouteStack router;
    private readonly IHandsetAudio audio;
    private readonly UsGenreRadio publicRadio;
    private readonly PearlCommunityRadio communityRadio;
    private readonly WasapiBroadcastSense broadcastSense;
    private readonly IcecastBroadcastPush broadcastPush;
    private readonly bool isDevelopment;
    private int lastUnread;
    private bool poweringOff;

    public HandsetHost(IDalamudPluginInterface pluginInterface, IFramework framework, IClientState clientState,
        IObjectTable objectTable, ICondition condition, IDutyState dutyState, IPluginLog pluginLog,
        ITextureProvider textureProvider, IDataManager dataManager, IChatGui chatGui, IPartyList partyList,
        IKeyState keys, ICommandManager commands, ITargetManager targets)
    {
        this.pluginInterface = pluginInterface;
        this.framework = framework;
        this.keys = keys;
        isDevelopment = pluginInterface.IsDev;
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

        var jobs = new FfxivJobCatalog(dataManager);
        services.AddSingleton<IJobCatalog>(jobs);
        services.AddSingleton<IGameItems>(new FfxivGameItems(dataManager));
        session = new FfxivGameSession(clientState, objectTable, condition, dutyState, partyList, framework, dataManager,
            jobs);
        services.AddSingleton<IGameSession>(session);
        services.AddSingleton<ILifestream>(new FfxivLifestream(pluginInterface, dataManager));
        services.AddSingleton<IWeatherOracle>(new FfxivWeatherOracle(dataManager, clock));

        config = pluginInterface.GetPluginConfig() as HandsetConfig ?? new HandsetConfig();
        config.Sanitize();

        pearl = new PearlHub(string.Empty, config.SessionToken, session, clock, log,
            token => clock.Post(() => RememberToken(token)), paths.State("media-cache"));
        services.AddSingleton<IPearlHub>(pearl);

        chat = new FfxivChatBridge(chatGui, clientState, partyList, objectTable, dataManager, session, framework,
            pluginLog, targets);
        services.AddSingleton<IChatBridge>(chat);
        talk = new TalkInbox(chat, pearl, clock, session, paths.State("talk"));
        services.AddSingleton<ITalk>(talk);

        var preferences = new DisplayPreferences();
        LoadDisplay(preferences);
        preferences.Changed += RememberDisplay;
        display = preferences;
        services.AddSingleton(preferences);
        services.AddSingleton<HandsetProfileDesk>();
        services.AddSingleton(_ => BadgeBook.Load(paths, clock));
        services.AddSingleton(_ =>
        {
            var book = PearlLedger.Load(paths, clock);
            if (isDevelopment)
            {
                book.GrantDevTestPurse();
            }

            return book;
        });

        var chime = new DalamudChime(chatGui, preferences, session);
        services.AddSingleton<IChime>(chime);
        var ports = new WindowsAudioPorts();
        services.AddSingleton<IAudioPorts>(ports);
        audio = new WasapiStreamPlayer(preferences.Volume, value =>
        {
            config.MusicVolume = value;
            display.Volume = value;
            pluginInterface.SavePluginConfig(config);
        });
        publicRadio = new UsGenreRadio();
        communityRadio = new PearlCommunityRadio(() => config.SessionToken, () => pearl.Current.SignedIn, paths,
            iceListenBase: config.IcecastListenBase, iceUser: config.IcecastSourceUser,
            icePassword: config.IcecastSourcePassword);
        broadcastSense = new WasapiBroadcastSense();
        broadcastPush = new IcecastBroadcastPush(paths.AssemblyDirectory);
        services.AddSingleton<IHandsetAudio>(audio);
        ApplyAudioRoute();
        preferences.Changed += ApplyAudioRoute;
        services.AddSingleton<IPublicRadio>(publicRadio);
        services.AddSingleton<ICommunityRadio>(communityRadio);
        services.AddSingleton<IUniversalisMarket>(new UniversalisMarket());
        services.AddSingleton<IBroadcastSense>(broadcastSense);
        services.AddSingleton<IBroadcastPush>(broadcastPush);

        var textures = new DalamudTextureSource(textureProvider);
        services.AddSingleton<ITextureSource>(textures);
        var files = new WindowsImagePicker();
        var fileGlass = new FilePickWindow(files.AcceptGlass);
        files.Bind(fileGlass);
        services.AddSingleton<IFilePicker>(files);
        services.AddSingleton<IGifDesk>(new GiphyGifDesk(paths, ReadGiphyKey(paths, config)));

        var hub = new DestinationHub();
        services.AddSingleton(hub);
        services.AddSingleton<NoticeLedger>();
        services.AddSingleton<INoticeTray>(static provider => provider.GetRequiredService<NoticeLedger>());

        services.AddSingleton<IFeedbackDesk>(new DiscordFeedbackDesk(environment, clock, clock, log));

        foreach (var module in ModuleDiscovery.Discover())
        {
            module.Configure(services, context);
        }

        provider = services.BuildServiceProvider();

        fonts = new HandsetFontService(pluginInterface);
        fonts.SetDisplayFace(FounderFaces.Active(preferences.DisplayFace,
            GlassName.Unlocked(pearl.Current, preferences, isDevelopment)));
        var theme = new HandsetTheme(1f, preferences);
        popouts = new TalkPopoutBoard(windowSystem, talk, theme, RememberPopouts);
        popouts.Restore(config.PopoutTalkIds, config.PopoutTalkPlaces);

        // Clock and Calculator are reached from the apps drawer (left-edge grid handle). Settings
        // stays a destination. RouteStack is the back-stack for those applets.
        var social = new SocialDestination(pearl, clock, talk, session, preferences, popouts, chat, paths, files,
            provider.GetRequiredService<IGifDesk>());
        var apps = provider.GetServices<IApplet>().ToList();
        apps.Add(new SocialAppApplet(social, talk, "pearlchat", "PearlChat", "💬", 2, SocialPane.Messages, true));
        apps.Add(new SocialAppApplet(social, talk, "friends", "Friends", "👥", 3, SocialPane.People, false));
        var appletById = apps.ToDictionary(applet => applet.Manifest.Id, applet => applet, StringComparer.Ordinal);
        router = new RouteStack(appletById);
        router.Restore(config.RecentAppIds, config.RecentAppPlaces);
        router.RecentsChanged += RememberRecents;

        shapePreference = new HandsetShapePreference(config.ScaleStep, config.Form, config.PositionLocked,
            config.PocketScale, config.Finish, config.ShowLockTab, config.Case);
        var notices = provider.GetRequiredService<NoticeLedger>();
        var badges = provider.GetRequiredService<BadgeBook>();
        var weather = provider.GetRequiredService<IWeatherOracle>();
        var profiles = provider.GetRequiredService<HandsetProfileDesk>();
        IReadOnlyList<IDestinationScreen> destinations = new IDestinationScreen[]
        {
            new HomeDestination(clock, session, pearl, talk, hub, preferences, paths, textures, badges, files,
                weather, notices, isDevelopment, profiles),
            social,
            new ExploreDestination(pearl, session),
            new YouDestination(session, pearl, badges, paths, textures, files, preferences, isDevelopment, profiles),
            new SettingsDestination(shapePreference, preferences, environment, session, pearl, hub, paths, textures,
                files, ports, audio, badges, profiles),
        };
        var textField = new DalamudTextField(fonts);
        this.textField = textField;
        var wife = new WifeSyncBridge(pluginInterface, commands);
        var shell = new HandsetShell(destinations, clock, session, preferences, textField, pearl, hub, router, talk,
            apps, wife, notices, weather, isDevelopment, badges, audio, publicRadio,
            provider.GetRequiredService<IStationMarks>());
        var screenField = new ScreenField(textures, paths, preferences, clock);

        placement = new HandsetPlacement();
        placement.Load(config.HasOpenPos, config.OpenX, config.OpenY, config.HasPocketPos, config.PocketX,
            config.PocketY);
        window = new HandsetWindow(shell, fonts, theme, router, shapePreference, screenField, textField, preferences,
            session, textures, paths, RememberShape, RememberOpen, RememberMinimized, placement, RememberPlacement,
            RequestPowerOff);
        shapePreference.Changed += OnShapeChanged;
        windowSystem.AddWindow(window);
        windowSystem.AddWindow(fileGlass);

        ApplyGposeUi();
        preferences.Changed += ApplyGposeUi;

        pluginInterface.UiBuilder.Draw += OnUiDraw;
        pluginInterface.UiBuilder.OpenMainUi += ToggleHandset;
        framework.Update += OnFrameworkUpdate;

        window.IsOpen = true;
        if (config.HandsetMinimized)
        {
            window.SnapMinimized();
        }
        else
        {
            window.PlayBoot();
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

    private void ApplyGposeUi() =>
        pluginInterface.UiBuilder.DisableGposeUiHide = display.StayInPortraits || session.IsInGpose;

    private void OnFrameworkUpdate(IFramework _)
    {
        ApplyGposeUi();
        var unread = talk.UnreadTotal;
        ApplyDisplayFace();
        if (window.IsOpen && window.IsMinimized && display.WakeInPocket &&
            !display.Hushed(session.IsInDuty || session.IsInCutscene) && unread > lastUnread)
        {
            window.Restore();
        }

        lastUnread = unread;
        popouts.Pulse();
    }

    private void OnUiDraw()
    {
        windowSystem.Draw();
        if (window.IsOpen && !window.IsMinimized && textField.Capturing)
        {
            keys.ClearAll();
        }
    }

    public void OpenHandset()
    {
        window.IsOpen = true;
        window.Restore();
        window.PlayBoot();
    }

    private void RequestPowerOff()
    {
        if (poweringOff)
        {
            return;
        }

        poweringOff = true;
        RememberOpen(true);
        RememberMinimized(false);
        _ = Task.Run(() => PluginSwitch.TurnOff(pluginInterface));
    }

    public void Dispose()
    {
        shapePreference.Changed -= OnShapeChanged;
        display.Changed -= RememberDisplay;
        display.Changed -= ApplyAudioRoute;
        router.RecentsChanged -= RememberRecents;
        RememberShape();
        RememberDisplay();
        RememberOpen(window.IsOpen);
        RememberMinimized(window.IsMinimized);
        window.FlushPlacement();
        framework.Update -= OnFrameworkUpdate;
        pluginInterface.UiBuilder.Draw -= OnUiDraw;
        pluginInterface.UiBuilder.OpenMainUi -= ToggleHandset;
        popouts.Dispose();
        audio.Dispose();
        broadcastPush.Dispose();
        broadcastSense.Dispose();
        publicRadio.Dispose();
        communityRadio.Dispose();
        windowSystem.RemoveAllWindows();
        fonts.Dispose();
        talk.Dispose();
        chat.Dispose();
        pearl.Dispose();
        session.Dispose();
        clock.Dispose();
        provider.Dispose();
    }

    private void ApplyDisplayFace()
    {
        var snapshot = pearl.Current;
        var unlocked = GlassName.Unlocked(snapshot, display, isDevelopment);
        if (!unlocked)
        {
            GlassName.Relinquish(display);
        }

        fonts.SetDisplayFace(FounderFaces.Active(display.DisplayFace, unlocked));
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
        config.ScaleStep = HandsetSizeCatalog.ClampFree(shapePreference.ScaleStep, HandsetSizeCatalog.FreeCeiling);
        config.Form = shapePreference.Form;
        config.PositionLocked = shapePreference.PositionLocked;
        config.PocketScale = shapePreference.PocketScale;
        config.Finish = shapePreference.Finish;
        config.ShowLockTab = shapePreference.ShowLockTab;
        config.Case = shapePreference.Case;
        pluginInterface.SavePluginConfig(config);
    }

    private void LoadDisplay(DisplayPreferences preferences)
    {
        preferences.Use24HourClock = config.Use24HourClock;
        preferences.Appearance = (AppearanceMode)config.Appearance;
        preferences.WallpaperId = config.WallpaperId;
        preferences.ReplaceCustomPlates(config.CustomPlateFiles);
        if (config.CustomPlateFile.Length > 0)
        {
            preferences.AddCustomPlate(config.CustomPlateFile);
        }

        preferences.CustomBannerFile = config.CustomBannerFile;
        preferences.BannerZoom = config.BannerZoom > 0f ? config.BannerZoom : 1f;
        preferences.BannerFocusX = config.BannerFocusX == 0f && config.BannerFocusY == 0f
            ? 0.5f
            : config.BannerFocusX;
        preferences.BannerFocusY = config.BannerFocusX == 0f && config.BannerFocusY == 0f
            ? 0.5f
            : config.BannerFocusY;
        preferences.Colorway = config.Colorway;
        preferences.Core = config.Core;
        preferences.Shade = (ShadeLevel)config.Shade;
        preferences.ClockFace = (ClockFace)config.ClockFace;
        preferences.Lettering = (LetteringSize)config.Lettering;
        preferences.NameStyle = (NameStyle)config.NameStyle;
        preferences.TestingAccount = config.TestingAccount;
        preferences.OwnName = config.OwnName;
        preferences.OwnTitle = config.OwnTitle;
        preferences.OwnTimeZoneId = config.OwnTimeZoneId;
        preferences.TitleMotion = (TitleMotion)config.TitleMotion;
        preferences.TitleGlow = config.TitleGlow;
        preferences.TitleGlowWeight = (NameGlowWeight)config.TitleGlowWeight;
        preferences.TitleInkR = config.TitleInkR;
        preferences.TitleInkG = config.TitleInkG;
        preferences.TitleInkB = config.TitleInkB;
        preferences.TitleGlowR = config.TitleGlowR;
        preferences.TitleGlowG = config.TitleGlowG;
        preferences.TitleGlowB = config.TitleGlowB;
        preferences.NameMotion = (TitleMotion)config.NameMotion;
        preferences.NameGlow = config.NameGlow;
        preferences.NameGlowR = config.NameGlowR;
        preferences.NameGlowG = config.NameGlowG;
        preferences.NameGlowB = config.NameGlowB;
        preferences.NameGlowWeight = (NameGlowWeight)config.NameGlowWeight;
        preferences.NameInkCustom = config.NameInkCustom;
        preferences.NameInkR = config.NameInkR;
        preferences.NameInkG = config.NameInkG;
        preferences.NameInkB = config.NameInkB;
        preferences.DisplayFace = config.DisplayFace;
        preferences.FounderFacesGranted = config.FounderFacesGranted;
        preferences.ShowWorld = config.ShowWorld;
        preferences.ShowMarks = config.ShowMarks;
        preferences.FeedShowSay = config.FeedShowSay;
        preferences.FeedShowShout = config.FeedShowShout;
        preferences.FeedShowYell = config.FeedShowYell;
        preferences.FeedShowParty = config.FeedShowParty;
        preferences.ExtraHomeScreens = config.ExtraHomeScreens;
        preferences.ReduceMotion = config.ReduceMotion;
        preferences.Quiet = config.Quiet;
        preferences.QuietWhenBusy = config.QuietWhenBusy;
        preferences.WakeInPocket = config.WakeInPocket;
        preferences.StayInPortraits = config.StayInPortraits;
        preferences.TuckForCutscenes = config.TuckForCutscenes;
        preferences.Fight = (FightPresence)config.Fight;
        preferences.Layout = (TuneLayout)config.TuneLayout;
        preferences.Brightness = config.Brightness;
        preferences.Volume = config.MusicVolume;
        preferences.MicVolume = config.MicVolume;
        preferences.SpeakerId = config.SpeakerDeviceId;
        preferences.MicrophoneId = config.MicrophoneDeviceId;
        preferences.CallSpeakerId = config.CallSpeakerDeviceId;
        preferences.CallMicrophoneId = config.CallMicrophoneDeviceId;
        preferences.AutoRotate = config.AutoRotate;
        if (config.Replies.Length > 0)
        {
            preferences.SetReplies(config.Replies);
        }

        preferences.LoadAppShelf(config.InstalledApps, config.FavoriteApps, config.AppFolders, config.QuickApps,
            config.SeenShelfApps, config.AppScreens, config.OwnedApps);
        preferences.LoadStudioLayout(config.StudioWidgets, config.StudioApps);
    }

    private void RememberDisplay()
    {
        config.Use24HourClock = display.Use24HourClock;
        config.Appearance = (int)display.Appearance;
        config.WallpaperId = display.WallpaperId;
        config.CustomPlateFile = display.CustomPlateFile;
        config.CustomPlateFiles = display.CustomPlateFiles.ToArray();
        config.CustomBannerFile = display.CustomBannerFile;
        config.BannerZoom = display.BannerZoom;
        config.BannerFocusX = display.BannerFocus.X;
        config.BannerFocusY = display.BannerFocus.Y;
        config.Colorway = display.Colorway;
        config.Core = display.Core;
        config.Shade = (int)display.Shade;
        config.ClockFace = (int)display.ClockFace;
        config.Lettering = (int)display.Lettering;
        config.NameStyle = (int)display.NameStyle;
        config.TestingAccount = display.TestingAccount;
        config.OwnName = display.OwnName;
        config.OwnTitle = display.OwnTitle;
        config.OwnTimeZoneId = display.OwnTimeZoneId;
        config.TitleMotion = (int)display.TitleMotion;
        config.TitleGlow = display.TitleGlow;
        config.TitleGlowWeight = (int)display.TitleGlowWeight;
        config.TitleInkR = display.TitleInkR;
        config.TitleInkG = display.TitleInkG;
        config.TitleInkB = display.TitleInkB;
        config.TitleGlowR = display.TitleGlowR;
        config.TitleGlowG = display.TitleGlowG;
        config.TitleGlowB = display.TitleGlowB;
        config.NameMotion = (int)display.NameMotion;
        config.NameGlow = display.NameGlow;
        config.NameGlowR = display.NameGlowR;
        config.NameGlowG = display.NameGlowG;
        config.NameGlowB = display.NameGlowB;
        config.NameGlowWeight = (int)display.NameGlowWeight;
        config.NameInkCustom = display.NameInkCustom;
        config.NameInkR = display.NameInkR;
        config.NameInkG = display.NameInkG;
        config.NameInkB = display.NameInkB;
        config.DisplayFace = display.DisplayFace;
        config.FounderFacesGranted = display.FounderFacesGranted;
        config.ShowWorld = display.ShowWorld;
        config.ShowMarks = display.ShowMarks;
        config.FeedShowSay = display.FeedShowSay;
        config.FeedShowShout = display.FeedShowShout;
        config.FeedShowYell = display.FeedShowYell;
        config.FeedShowParty = display.FeedShowParty;
        config.ExtraHomeScreens = display.ExtraHomeScreens;
        config.ReduceMotion = display.ReduceMotion;
        config.Quiet = display.Quiet;
        config.QuietWhenBusy = display.QuietWhenBusy;
        config.WakeInPocket = display.WakeInPocket;
        config.StayInPortraits = display.StayInPortraits;
        config.TuckForCutscenes = display.TuckForCutscenes;
        config.Fight = (int)display.Fight;
        config.TuneLayout = (int)display.Layout;
        config.Brightness = display.Brightness;
        config.Volume = display.Volume;
        config.MusicVolume = display.Volume;
        config.MicVolume = display.MicVolume;
        config.SpeakerDeviceId = display.SpeakerId;
        config.MicrophoneDeviceId = display.MicrophoneId;
        config.CallSpeakerDeviceId = display.CallSpeakerId;
        config.CallMicrophoneDeviceId = display.CallMicrophoneId;
        config.AutoRotate = display.AutoRotate;
        config.Replies = display.Replies.ToArray();
        config.InstalledApps = display.InstalledApps.ToArray();
        config.AppScreens = display.PackedAppScreens();
        config.FavoriteApps = display.FavoriteApps.ToArray();
        config.AppFolders = display.AppFolders.ToArray();
        config.QuickApps = display.QuickApps.ToArray();
        config.RecentAppIds = router.RecentIds.ToArray();
        config.RecentAppPlaces = router.RecentPlaces.ToArray();
        config.SeenShelfApps = display.SeenShelfApps.ToArray();
        config.OwnedApps = display.OwnedApps.ToArray();
        config.StudioWidgets = display.PackedStudioWidgets;
        config.StudioApps = display.PackedStudioApps;
        pluginInterface.SavePluginConfig(config);
    }

    private void ApplyAudioRoute()
    {
        audio.UseSpeaker(display.SpeakerId);
        if (Math.Abs(audio.Volume - display.Volume) > 0.0005f)
        {
            audio.Volume = display.Volume;
        }

        broadcastSense.MicGain = display.MicVolume;
        broadcastSense.RoutePhone(display.ActiveCallSpeaker, display.ActiveCallMicrophone);
    }

    private void RememberRecents()
    {
        config.RecentAppIds = router.RecentIds.ToArray();
        config.RecentAppPlaces = router.RecentPlaces.ToArray();
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

    private void RememberPopouts()
    {
        config.PopoutTalkIds = popouts.ArmedIds.ToArray();
        config.PopoutTalkPlaces = popouts.PlaceBlobs.ToArray();
        pluginInterface.SavePluginConfig(config);
    }

    private void RememberPlacement()
    {
        config.HasOpenPos = placement.HasOpen;
        config.OpenX = placement.Open.X;
        config.OpenY = placement.Open.Y;
        config.HasPocketPos = placement.HasPocket;
        config.PocketX = placement.Pocket.X;
        config.PocketY = placement.Pocket.Y;
        placement.ClearDirty();
        pluginInterface.SavePluginConfig(config);
    }

    private static string ReadGiphyKey(HostPaths paths, HandsetConfig cfg)
    {
        var env = Environment.GetEnvironmentVariable("LINKPEARL_GIPHY_KEY");
        if (!string.IsNullOrWhiteSpace(env))
        {
            return env.Trim();
        }

        if (cfg.GiphyApiKey.Length > 0)
        {
            return cfg.GiphyApiKey;
        }

        var file = paths.State("giphy.key");
        if (File.Exists(file))
        {
            var fromFile = File.ReadAllText(file).Trim();
            if (fromFile.Length > 0)
            {
                return fromFile;
            }
        }

        return string.Empty;
    }
}
