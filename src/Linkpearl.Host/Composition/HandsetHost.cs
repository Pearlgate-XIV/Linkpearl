using System.Linq;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Linkpearl.Applets;
using Linkpearl.Canvas.Input;
using Linkpearl.Canvas.Text;
using Linkpearl.Canvas.Theming;
using Linkpearl.Chassis;
using Linkpearl.Destinations;
using Linkpearl.Destinations.Explore;
using Linkpearl.Destinations.Home;
using Linkpearl.Destinations.Social;
using Linkpearl.Destinations.You;
using Linkpearl.Device.Shell;
using Linkpearl.Device.Windows;
using Linkpearl.Diagnostics;
using Linkpearl.Host.Time;
using Linkpearl.Modules;
using Linkpearl.Platform;
using Linkpearl.Platform.Ffxiv;
using Linkpearl.Preferences;
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

    public HandsetHost(IDalamudPluginInterface pluginInterface, IFramework framework, IClientState clientState,
        IObjectTable objectTable, ICondition condition, IDutyState dutyState, IPluginLog pluginLog)
    {
        this.pluginInterface = pluginInterface;
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

        session = new FfxivGameSession(clientState, objectTable, condition, dutyState);
        services.AddSingleton<IGameSession>(session);

        var preferences = new DisplayPreferences();
        services.AddSingleton(preferences);

        foreach (var module in ModuleDiscovery.Discover())
        {
            module.Configure(services, context);
        }

        provider = services.BuildServiceProvider();

        fonts = new HandsetFontService(pluginInterface);
        var theme = new HandsetTheme(1f);

        // Clock/Calculator/Settings still exist as IApplet modules, but nothing routes to them
        // through the new destination-based UI yet (see docs/STATUS.md) — Settings-equivalent
        // functionality belongs inside "You" per the design spec, not as a home-screen icon.
        // RouteStack stays wired so that plumbing is exercised and ready for a destination's own
        // future drill-down navigation, not because anything opens these apps today.
        var apps = provider.GetServices<IApplet>().ToList();
        var appletById = apps.ToDictionary(applet => applet.Manifest.Id, applet => applet, StringComparer.Ordinal);
        var router = new RouteStack(appletById);

        var shapePreference = new HandsetShapePreference(HandsetSizeCatalog.DefaultStep, HandsetForm.Phone);
        IReadOnlyList<IDestinationScreen> destinations = new IDestinationScreen[]
        {
            new HomeDestination(clock), new SocialDestination(), new ExploreDestination(),
            new YouDestination(shapePreference),
        };
        var textField = new DalamudTextField();
        var shell = new HandsetShell(destinations, clock, preferences, textField);

        window = new HandsetWindow(shell, fonts, theme, router, shapePreference);
        windowSystem.AddWindow(window);

        pluginInterface.UiBuilder.Draw += windowSystem.Draw;
        pluginInterface.UiBuilder.OpenMainUi += ToggleHandset;
    }

    public void ToggleHandset() => window.IsOpen = !window.IsOpen;

    public void OpenHandset() => window.IsOpen = true;

    public void Dispose()
    {
        pluginInterface.UiBuilder.Draw -= windowSystem.Draw;
        pluginInterface.UiBuilder.OpenMainUi -= ToggleHandset;
        windowSystem.RemoveAllWindows();
        fonts.Dispose();
        session.Dispose();
        clock.Dispose();
        provider.Dispose();
    }
}
