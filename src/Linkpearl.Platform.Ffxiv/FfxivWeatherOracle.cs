using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using Linkpearl.Platform;
using Linkpearl.Time;
using WeatherSheet = Lumina.Excel.Sheets.Weather;

namespace Linkpearl.Platform.Ffxiv;

public sealed class FfxivWeatherOracle : IWeatherOracle
{
    private readonly IDataManager data;
    private readonly IClock clock;

    public FfxivWeatherOracle(IDataManager data, IClock clock)
    {
        this.data = data;
        this.clock = clock;
    }

    public WeatherWindow Current(ushort territoryId)
    {
        var hours = Forecast(territoryId, 1);
        return hours.Count > 0 ? hours[0] : default;
    }

    public IReadOnlyList<WeatherWindow> Forecast(ushort territoryId, int windows)
    {
        var count = Math.Clamp(windows, 0, 24);
        if (count == 0 || territoryId == 0)
        {
            return Array.Empty<WeatherWindow>();
        }

        var now = clock.UtcNow;
        var bells = EorzeaTime.FromUnix(now.ToUnixTimeSeconds());
        var intoHour = TimeSpan.FromSeconds(bells.Minute * (3600.0 / EorzeaTime.EarthToEorzea) / 60.0);
        var hourLength = TimeSpan.FromSeconds(3600.0 / EorzeaTime.EarthToEorzea);
        var result = new WeatherWindow[count];
        for (var offset = 0; offset < count; offset++)
        {
            var weatherId = offset == 0 ? FfxivWeatherSense.Live(territoryId) : ReadHour(territoryId, offset);
            var starts = now - intoHour + hourLength * offset;
            result[offset] = new WeatherWindow(Title(weatherId), Icon(weatherId), starts, starts + hourLength);
        }

        return result;
    }

    private static unsafe byte ReadHour(ushort territoryId, int hourOffset)
    {
        var manager = WeatherManager.Instance();
        if (manager is null)
        {
            return 0;
        }

        return manager->GetWeatherForHour(territoryId, hourOffset);
    }

    private string Title(byte weatherId)
    {
        if (weatherId != 0 && data.GetExcelSheet<WeatherSheet>().TryGetRow(weatherId, out var weather))
        {
            return weather.Name.ExtractText() ?? string.Empty;
        }

        return string.Empty;
    }

    private uint Icon(byte weatherId)
    {
        if (weatherId != 0 && data.GetExcelSheet<WeatherSheet>().TryGetRow(weatherId, out var weather))
        {
            return (uint)weather.Icon;
        }

        return 0u;
    }
}
