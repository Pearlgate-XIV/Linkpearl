using Linkpearl.Applets;
using Linkpearl.Geometry;
using Linkpearl.Platform;
using Linkpearl.Weather;

namespace Linkpearl.Device.Shell.Studio;

internal sealed class StudioWeather
{
    private readonly IGameSession game;
    private readonly IWeatherOracle weather;
    private readonly ISkyDesk sky;

    public StudioWeather(IGameSession game, IWeatherOracle weather, ISkyDesk sky)
    {
        this.game = game;
        this.weather = weather;
        this.sky = sky;
    }

    public void Draw(in AppletFrame frame, Rect row, Action<Rect> open)
    {
        var territory = (ushort)game.TerritoryId;
        var look = sky.Look(territory);
        var hours = game.IsLoggedIn && territory != 0
            ? WeatherChrome.AlignNow(weather.Forecast(territory, WeatherChrome.ForecastHours), look)
            : [];
        var condition = First(look.Name, game.IsLoggedIn ? game.WeatherName : "", "Unknown skies");
        var place = First(game.ZoneName, game.Character.WorldName, "Not logged in");
        WeatherChrome.Dock(frame, row, place, condition, look.IconId, look.Bells, hours);
        if (frame.Input.ConsumeClick(row))
        {
            open(row);
        }
    }

    private static string First(params string[] values)
    {
        for (var index = 0; index < values.Length; index++)
        {
            if (!string.IsNullOrWhiteSpace(values[index]))
            {
                return values[index];
            }
        }

        return string.Empty;
    }
}
