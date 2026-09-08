using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;
using Dalamud.Plugin.Ipc.Exceptions;
using Dalamud.Plugin.Services;
using Linkpearl.Platform;
using AetheryteSheet = Lumina.Excel.Sheets.Aetheryte;

namespace Linkpearl.Platform.Ffxiv;

public sealed class FfxivLifestream : ILifestream
{
    private readonly IDalamudPluginInterface plugins;
    private readonly IDataManager data;
    private readonly ICallGateSubscriber<uint, byte, bool>? teleport;

    public FfxivLifestream(IDalamudPluginInterface plugins, IDataManager data)
    {
        this.plugins = plugins;
        this.data = data;
        try
        {
            teleport = plugins.GetIpcSubscriber<uint, byte, bool>("Lifestream.Teleport");
        }
        catch (IpcError)
        {
            teleport = null;
        }
    }

    public bool Ready
    {
        get
        {
            if (teleport is null)
            {
                return false;
            }

            foreach (var plugin in plugins.InstalledPlugins)
            {
                if (string.Equals(plugin.InternalName, "Lifestream", StringComparison.OrdinalIgnoreCase) &&
                    plugin.IsLoaded)
                {
                    return true;
                }
            }

            return false;
        }
    }

    public uint NearestAetheryte(uint territoryId)
    {
        if (territoryId == 0)
        {
            return 0;
        }

        var sheet = data.GetExcelSheet<AetheryteSheet>();
        foreach (var row in sheet)
        {
            if (row.IsAetheryte && row.Territory.RowId == territoryId)
            {
                return row.RowId;
            }
        }

        return 0;
    }

    public bool TryTeleport(uint aetheryteId)
    {
        if (aetheryteId == 0 || teleport is null)
        {
            return false;
        }

        try
        {
            return teleport.InvokeFunc(aetheryteId, 0);
        }
        catch (IpcNotReadyError)
        {
            return false;
        }
        catch (IpcError)
        {
            return false;
        }
    }
}
