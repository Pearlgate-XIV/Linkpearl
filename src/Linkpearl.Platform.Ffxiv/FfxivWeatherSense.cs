using FFXIVClientStructs.FFXIV.Client.Game;

namespace Linkpearl.Platform.Ffxiv;

internal static class FfxivWeatherSense
{
    public static unsafe byte Live(ushort territoryId)
    {
        var manager = WeatherManager.Instance();
        if (manager is null)
        {
            return 0;
        }

        var seen = manager->GetCurrentWeather();
        if (seen != 0)
        {
            return seen;
        }

        var weathers = manager->Weathers;
        if (weathers.Length > 0)
        {
            var current = weathers[0].GetCurrentWeatherId();
            if (current == 0)
            {
                current = weathers[0].CurrentWeatherId;
            }

            if (current != 0)
            {
                return current;
            }
        }

        if (manager->WeatherId != 0)
        {
            return manager->WeatherId;
        }

        if (manager->WeatherOverride != 0)
        {
            return manager->WeatherOverride;
        }

        if (manager->IndividualWeatherId != 0)
        {
            return manager->IndividualWeatherId;
        }

        return Natural(territoryId);
    }

    public static unsafe byte Natural(ushort territoryId)
    {
        var manager = WeatherManager.Instance();
        if (manager is null || territoryId == 0)
        {
            return 0;
        }

        return manager->GetWeatherForHour(territoryId, 0);
    }
}
