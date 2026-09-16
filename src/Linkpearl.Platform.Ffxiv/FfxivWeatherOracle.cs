using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using Linkpearl.Time;
using WeatherSheet = Lumina.Excel.Sheets.Weather;

namespace Linkpearl.Platform.Ffxiv;

public sealed class FfxivWeatherOracle : IWeatherOracle
{
    private readonly IDataManager data;
    private readonly IClock clock;
    private readonly string[] titles = new string[256];
    private readonly uint[] icons = new uint[256];
    private readonly bool[] named = new bool[256];
    private ushort forecastTerritory;
    private int forecastCount;
    private long forecastAt;
    private WeatherWindow[] forecast = [];

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
        var stamp = now.ToUnixTimeSeconds();
        if (forecastTerritory == territoryId && forecastCount == count && forecast.Length == count &&
            stamp - forecastAt < 2)
        {
            return forecast;
        }

        var bells = EorzeaTime.FromUnix(stamp);
        var intoHour = TimeSpan.FromSeconds(bells.Minute * (3600.0 / EorzeaTime.EarthToEorzea) / 60.0);
        var hourLength = TimeSpan.FromSeconds(3600.0 / EorzeaTime.EarthToEorzea);
        var result = new WeatherWindow[count];
        for (var offset = 0; offset < count; offset++)
        {
            var weatherId = offset == 0 ? FfxivWeatherSense.Live(territoryId) : ReadHour(territoryId, offset);
            var starts = now - intoHour + hourLength * offset;
            result[offset] = new WeatherWindow(Title(weatherId), Icon(weatherId), starts, starts + hourLength);
        }

        forecastTerritory = territoryId;
        forecastCount = count;
        forecastAt = stamp;
        forecast = result;
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
        Remember(weatherId);
        return titles[weatherId] ?? string.Empty;
    }

    private uint Icon(byte weatherId)
    {
        Remember(weatherId);
        return icons[weatherId];
    }

    private void Remember(byte weatherId)
    {
        if (named[weatherId])
        {
            return;
        }

        named[weatherId] = true;
        if (weatherId != 0 && data.GetExcelSheet<WeatherSheet>().TryGetRow(weatherId, out var weather))
        {
            titles[weatherId] = weather.Name.ExtractText() ?? string.Empty;
            icons[weatherId] = (uint)weather.Icon;
        }
    }
}
