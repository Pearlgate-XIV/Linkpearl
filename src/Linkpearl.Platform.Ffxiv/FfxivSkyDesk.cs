using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.System.Framework;
using Linkpearl.Platform;
using Linkpearl.Time;
using TerritorySheet = Lumina.Excel.Sheets.TerritoryType;
using WeatherSheet = Lumina.Excel.Sheets.Weather;

namespace Linkpearl.Platform.Ffxiv;

public sealed class FfxivSkyDesk : ISkyDesk
{
    private readonly IDalamudPluginInterface plugins;
    private readonly IDataManager data;
    private readonly IGameSession game;
    private readonly IClock clock;
    private bool timeLocked;
    private bool weatherLocked;
    private int lockedMinute;
    private byte lockedWeather;

    public FfxivSkyDesk(IDalamudPluginInterface plugins, IDataManager data, IGameSession game, IClock clock,
        IFrameLoop loop)
    {
        this.plugins = plugins;
        this.data = data;
        this.game = game;
        this.clock = clock;
        loop.Tick += _ => Pulse();
    }

    public bool Ready => game.IsLoggedIn && game.TerritoryId != 0;

    public bool TimeLocked => timeLocked;

    public bool WeatherLocked => weatherLocked;

    public int LockedMinute => lockedMinute;

    public byte LockedWeather => lockedWeather;

    public bool CompanionLoaded
    {
        get
        {
            foreach (var plugin in plugins.InstalledPlugins)
            {
                if (string.Equals(plugin.InternalName, "Weatherman", StringComparison.OrdinalIgnoreCase) &&
                    plugin.IsLoaded)
                {
                    return true;
                }
            }

            return false;
        }
    }

    public SkyLook Look(ushort territoryId)
    {
        var bells = LiveBells();
        var weatherId = weatherLocked && lockedWeather != 0
            ? lockedWeather
            : FfxivWeatherSense.Live(territoryId);
        Title(weatherId, out var name, out var icon);
        return new SkyLook(bells, name, icon, weatherId);
    }

    public IReadOnlyList<SkyChoice> ZoneChoices(ushort territoryId)
    {
        var found = new List<SkyChoice>();
        var seen = new HashSet<byte>();
        if (territoryId == 0)
        {
            return found;
        }

        RememberRate(found, seen, territoryId);
        Remember(found, seen, FfxivWeatherSense.Live(territoryId));
        for (var hour = 0; hour < 24; hour++)
        {
            Remember(found, seen, ReadHour(territoryId, hour));
        }

        for (var day = 0; day < 4; day++)
        {
            Remember(found, seen, ReadDay(territoryId, day));
        }

        found.Sort(static (a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
        return found;
    }

    public bool TryLockTime(int minuteOfDay)
    {
        if (!Ready)
        {
            return false;
        }

        lockedMinute = ((minuteOfDay % 1440) + 1440) % 1440;
        timeLocked = true;
        return WriteTime(lockedMinute, overrideOn: true);
    }

    public bool TryLockWeather(byte weatherId)
    {
        if (!Ready || weatherId == 0)
        {
            return false;
        }

        lockedWeather = weatherId;
        weatherLocked = true;
        return WriteWeather((ushort)game.TerritoryId, weatherId);
    }

    public void UnlockWeather()
    {
        weatherLocked = false;
        lockedWeather = 0;
        WriteWeather((ushort)game.TerritoryId, 0);
    }

    public void UnlockTime()
    {
        timeLocked = false;
        WriteTime(0, overrideOn: false);
    }

    public void Reset()
    {
        UnlockTime();
        UnlockWeather();
    }

    private void Pulse()
    {
        if (!Ready)
        {
            return;
        }

        if (weatherLocked && lockedWeather != 0)
        {
            WriteWeather((ushort)game.TerritoryId, lockedWeather);
        }

        if (timeLocked)
        {
            WriteTime(lockedMinute, overrideOn: true);
        }
    }

    private void RememberRate(List<SkyChoice> found, HashSet<byte> seen, ushort territoryId)
    {
        if (!data.GetExcelSheet<TerritorySheet>().TryGetRow(territoryId, out var territory))
        {
            return;
        }

        var rate = territory.WeatherRate.ValueNullable;
        if (rate is not { } table)
        {
            return;
        }

        foreach (var weather in table.Weather)
        {
            Remember(found, seen, (byte)weather.RowId);
        }
    }

    private void Remember(List<SkyChoice> found, HashSet<byte> seen, byte weatherId)
    {
        if (weatherId == 0 || !seen.Add(weatherId))
        {
            return;
        }

        if (!data.GetExcelSheet<WeatherSheet>().TryGetRow(weatherId, out var weather))
        {
            return;
        }

        var name = weather.Name.ExtractText() ?? string.Empty;
        if (name.Length == 0)
        {
            return;
        }

        found.Add(new SkyChoice(weatherId, name, (uint)weather.Icon));
    }

    private unsafe EorzeaTime LiveBells()
    {
        if (timeLocked)
        {
            return new EorzeaTime(lockedMinute / 60, lockedMinute % 60);
        }

        var framework = Framework.Instance();
        if (framework is not null)
        {
            ref var client = ref framework->ClientTime;
            var et = client.IsEorzeaTimeOverridden && client.EorzeaTimeOverride > 0
                ? client.EorzeaTimeOverride
                : client.EorzeaTime;
            if (et > 0)
            {
                return EorzeaTime.FromEorzeaSeconds(et);
            }
        }

        return EorzeaTime.FromUnix(clock.UtcNow.ToUnixTimeSeconds());
    }

    private void Title(byte weatherId, out string name, out uint icon)
    {
        name = string.Empty;
        icon = 0;
        if (weatherId != 0 && data.GetExcelSheet<WeatherSheet>().TryGetRow(weatherId, out var weather))
        {
            name = weather.Name.ExtractText() ?? string.Empty;
            icon = (uint)weather.Icon;
        }
    }

    private static unsafe byte ReadHour(ushort territoryId, int hour) =>
        WeatherManager.Instance() is var manager && manager is not null
            ? manager->GetWeatherForHour(territoryId, hour)
            : (byte)0;

    private static unsafe byte ReadDay(ushort territoryId, int daytime) =>
        WeatherManager.Instance() is var manager && manager is not null
            ? manager->GetWeatherForDaytime(territoryId, daytime)
            : (byte)0;

    private static unsafe bool WriteWeather(ushort territoryId, byte weatherId)
    {
        var manager = WeatherManager.Instance();
        if (manager is null)
        {
            return false;
        }

        if (weatherId == 0)
        {
            manager->IndividualWeatherId = 0;
            manager->WeatherOverride = 0;
            var natural = FfxivWeatherSense.Natural(territoryId);
            var restore = manager->Weathers;
            for (var index = 0; index < restore.Length; index++)
            {
                restore[index].IsCurrentWeatherForced = false;
                restore[index].IsNextWeatherForced = false;
                restore[index].Update();
                if (natural != 0)
                {
                    restore[index].SetNextWeather(natural, 0f, true);
                    restore[index].Update();
                }
            }

            var live = manager->GetCurrentWeather();
            manager->WeatherId = live != 0 ? live : natural;
            return true;
        }

        manager->IndividualWeatherId = weatherId;
        manager->WeatherId = weatherId;
        manager->WeatherOverride = weatherId;

        var weathers = manager->Weathers;
        for (var index = 0; index < weathers.Length; index++)
        {
            weathers[index].CurrentWeatherId = weatherId;
            weathers[index].NextWeatherId = weatherId;
            weathers[index].IsCurrentWeatherForced = true;
            weathers[index].IsNextWeatherForced = true;
            weathers[index].SetNextWeather(weatherId, 0f, false);
        }

        return true;
    }

    private unsafe bool WriteTime(int minuteOfDay, bool overrideOn)
    {
        var framework = Framework.Instance();
        if (framework is null)
        {
            return false;
        }

        ref var client = ref framework->ClientTime;
        if (!overrideOn)
        {
            var real = (long)(clock.UtcNow.ToUnixTimeSeconds() * EorzeaTime.EarthToEorzea);
            client.EorzeaTime = real;
            client.EorzeaTimeOverride = 0;
            client.IsEorzeaTimeOverridden = false;
            return true;
        }

        var current = client.EorzeaTime;
        if (current <= 0)
        {
            current = (long)(clock.UtcNow.ToUnixTimeSeconds() * EorzeaTime.EarthToEorzea);
        }

        var day = current / 86400L * 86400L;
        var next = day + minuteOfDay * 60L;
        client.EorzeaTime = next;
        client.EorzeaTimeOverride = next;
        client.IsEorzeaTimeOverridden = true;
        return true;
    }
}
