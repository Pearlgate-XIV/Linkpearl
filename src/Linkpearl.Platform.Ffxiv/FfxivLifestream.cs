using System.Globalization;
using Dalamud.Plugin;
using HousingAddress =
    (string Name, int World, int City, int Ward, int PropertyType, int Plot, int Apartment, bool ApartmentSubdivision,
    bool AliasEnabled, string Alias);
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
    private readonly IAetheryteList aetherytes;
    private readonly ICallGateSubscriber<uint, byte, bool>? teleport;
    private readonly ICallGateSubscriber<string, string, string, string, bool, bool, HousingAddress>? buildHome;
    private readonly ICallGateSubscriber<HousingAddress, object>? goHome;
    private readonly ICallGateSubscriber<string, object>? execute;

    public FfxivLifestream(IDalamudPluginInterface plugins, IDataManager data, IAetheryteList aetherytes)
    {
        this.plugins = plugins;
        this.data = data;
        this.aetherytes = aetherytes;
        teleport = Subscribe(() => plugins.GetIpcSubscriber<uint, byte, bool>("Lifestream.Teleport"));
        buildHome = Subscribe(() => plugins.GetIpcSubscriber<string, string, string, string, bool, bool, HousingAddress>(
            "Lifestream.BuildAddressBookEntry"));
        goHome = Subscribe(() => plugins.GetIpcSubscriber<HousingAddress, object>("Lifestream.GoToHousingAddress"));
        execute = Subscribe(() => plugins.GetIpcSubscriber<string, object>("Lifestream.ExecuteCommand"));
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

        foreach (var entry in aetherytes)
        {
            if (entry.TerritoryId == territoryId)
            {
                return entry.AetheryteId;
            }
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

    public bool TryTeleport(uint aetheryteId) => TryTeleport(aetheryteId, SubIndexOf(aetheryteId));

    public bool TryGo(uint aetheryteId, uint territory, string place)
    {
        if (!Ready)
        {
            return false;
        }

        if (aetheryteId != 0 && TryTeleport(aetheryteId, SubIndexOf(aetheryteId)))
        {
            return true;
        }

        if (territory != 0)
        {
            foreach (var entry in aetherytes)
            {
                if (entry.TerritoryId == territory && TryTeleport(entry.AetheryteId, entry.SubIndex))
                {
                    return true;
                }
            }
        }

        return TryGoPlace(place);
    }

    public bool TryGoPlace(string place)
    {
        var name = place.Trim();
        if (name.Length == 0 || !Ready)
        {
            return false;
        }

        foreach (var entry in aetherytes)
        {
            if (NamesAetheryte(entry.AetheryteId, name) && TryTeleport(entry.AetheryteId, entry.SubIndex))
            {
                return true;
            }
        }

        var gate = AetheryteNamed(name);
        if (gate != 0 && TryTeleport(gate, SubIndexOf(gate)))
        {
            return true;
        }

        return RunCommand(name);
    }

    private bool TryTeleport(uint aetheryteId, byte subIndex)
    {
        if (aetheryteId == 0 || teleport is null)
        {
            return false;
        }

        try
        {
            return teleport.InvokeFunc(aetheryteId, subIndex);
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

    private byte SubIndexOf(uint aetheryteId)
    {
        foreach (var entry in aetherytes)
        {
            if (entry.AetheryteId == aetheryteId)
            {
                return entry.SubIndex;
            }
        }

        return 0;
    }

    private bool NamesAetheryte(uint aetheryteId, string needle)
    {
        var sheet = data.GetExcelSheet<AetheryteSheet>();
        if (!sheet.TryGetRow(aetheryteId, out var row))
        {
            return false;
        }

        var title = TitleOf(row);
        return title.Length > 0 &&
               (title.Equals(needle, StringComparison.OrdinalIgnoreCase) ||
                title.StartsWith(needle, StringComparison.OrdinalIgnoreCase) ||
                needle.StartsWith(title, StringComparison.OrdinalIgnoreCase));
    }

    private uint AetheryteNamed(string place)
    {
        var needle = place.Trim();
        if (needle.Length == 0)
        {
            return 0;
        }

        var sheet = data.GetExcelSheet<AetheryteSheet>();
        uint loose = 0;
        foreach (var row in sheet)
        {
            if (!row.IsAetheryte)
            {
                continue;
            }

            var title = TitleOf(row);
            if (title.Length == 0)
            {
                continue;
            }

            if (title.Equals(needle, StringComparison.OrdinalIgnoreCase))
            {
                return row.RowId;
            }

            if (loose == 0 && title.StartsWith(needle, StringComparison.OrdinalIgnoreCase))
            {
                loose = row.RowId;
            }
        }

        return loose;
    }

    private static string TitleOf(AetheryteSheet row)
    {
        var title = row.PlaceName.ValueNullable?.Name.ExtractText() ?? string.Empty;
        if (title.Length > 0)
        {
            return title;
        }

        return row.Territory.ValueNullable?.PlaceName.ValueNullable?.Name.ExtractText() ?? string.Empty;
    }

    private bool RunCommand(string place)
    {
        if (execute is null)
        {
            return false;
        }

        try
        {
            execute.InvokeAction(place);
            return true;
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

    public bool TryGoHome(string world, string district, int ward, int plot, int apartment, bool subdivision)
    {
        if (!Ready || world.Length == 0 || district.Length == 0 || ward <= 0)
        {
            return false;
        }

        var apartmentHome = apartment > 0 && plot <= 0;
        var number = apartmentHome ? apartment : plot;
        if (number <= 0)
        {
            return false;
        }

        var wardText = ward.ToString(CultureInfo.InvariantCulture);
        var numberText = number.ToString(CultureInfo.InvariantCulture);
        try
        {
            if (buildHome is not null && goHome is not null)
            {
                var entry = buildHome.InvokeFunc(world, district, wardText, numberText, apartmentHome, subdivision);
                if (entry.World != 0)
                {
                    goHome.InvokeAction(entry);
                    return true;
                }
            }

            if (execute is null)
            {
                return false;
            }

            var command = apartmentHome
                ? world + " " + district + " W" + wardText + (subdivision ? " sub" : string.Empty) + " A" +
                  numberText
                : world + " " + district + " W" + wardText + " P" + numberText;
            execute.InvokeAction(command);
            return true;
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

    private static T? Subscribe<T>(Func<T> make) where T : class
    {
        try
        {
            return make();
        }
        catch (IpcError)
        {
            return null;
        }
    }
}
