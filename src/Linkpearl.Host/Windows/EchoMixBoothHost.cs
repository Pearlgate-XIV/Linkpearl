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
    private readonly EchoMixPlugin echo;
    private readonly IEchoMixStation station;
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
        this.station = station;
        echo = new EchoMixPlugin(pluginInterface, pluginLog, gameConfig, framework, objectTable, textureProvider,
            clientState);
    }

    public bool IsOpen => echo.IsDeckOpen;

    public bool Mixing => echo.Mixing;

    public void Open() => echo.OpenDeck();

    public void Close() => echo.CloseDeck();

    public void Draw() => echo.Draw();

    public void SyncStation()
    {
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

    public void Dispose() => echo.Dispose();
}
