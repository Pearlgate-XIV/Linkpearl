using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Linkpearl.Audio;
using EchoMixPlugin = EchoMix.Plugin.Plugin;

namespace Linkpearl.Host.Windows;

// Hosts the real EchoMix deck (jfraygit, AGPL-3.0) inside Linkpearl's UI loop.
// Station listing and Icecast ingest stay on Pearlgate. Outside-gear DJs keep
// using Music's capture path when this booth is not mixing.
public sealed class EchoMixBoothHost : IEchoMixBooth, IDisposable
{
    private readonly IDalamudPluginInterface pluginInterface;
    private readonly IPluginLog pluginLog;
    private readonly IGameConfig gameConfig;
    private readonly IFramework framework;
    private readonly IObjectTable objectTable;
    private readonly ITextureProvider textureProvider;
    private readonly IClientState clientState;
    private readonly IEchoMixStation station;
    private EchoMixPlugin? echo;
    private bool lastLive;
    private bool stationFromEcho;

    public EchoMixBoothHost(
        IDalamudPluginInterface pluginInterface,
        IPluginLog pluginLog,
        IGameConfig gameConfig,
        IFramework framework,
        IObjectTable objectTable,
        ITextureProvider textureProvider,
        IClientState clientState,
        IEchoMixStation station)
    {
        this.pluginInterface = pluginInterface;
        this.pluginLog = pluginLog;
        this.gameConfig = gameConfig;
        this.framework = framework;
        this.objectTable = objectTable;
        this.textureProvider = textureProvider;
        this.clientState = clientState;
        this.station = station;
    }

    public bool IsOpen => echo?.IsDeckOpen ?? false;

    public bool Mixing => echo?.Mixing ?? false;

    public void Open()
    {
        echo ??= new EchoMixPlugin(pluginInterface, pluginLog, gameConfig, framework, objectTable,
            textureProvider, clientState);
        echo.OpenDeck();
    }

    public void Close() => echo?.CloseDeck();

    public void Draw() => echo?.Draw();

    public void SyncStation()
    {
        if (echo is null)
        {
            return;
        }

        var live = echo.IsLive;
        if (live && !lastLive)
        {
            station.GoLive();
            stationFromEcho = true;
        }
        else if (!live && lastLive && stationFromEcho)
        {
            station.EndLive();
            stationFromEcho = false;
        }

        lastLive = live;
    }

    public void Dispose() => echo?.Dispose();
}
