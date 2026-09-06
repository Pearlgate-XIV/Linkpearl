using Linkpearl.Applets;
using Linkpearl.Geometry;
using Linkpearl.Platform;
using Linkpearl.Time;
using Linkpearl.Weather;

namespace Linkpearl.Device.Shell.Studio;

internal sealed class StudioWeather
{
    private readonly IGameSession game;
    private readonly IClock clock;
    private readonly IWeatherOracle weather;

    public StudioWeather(IGameSession game, IClock clock, IWeatherOracle weather)
    {
        this.game = game;
        this.clock = clock;
        this.weather = weather;
    }

    public void Draw(in AppletFrame frame, Rect row, Action<Rect> open)
    {
        var bells = EorzeaTime.FromUnix(clock.UtcNow.ToUnixTimeSeconds());
        var hours = game.IsLoggedIn && game.TerritoryId != 0
            ? weather.Forecast((ushort)game.TerritoryId, WeatherChrome.ForecastHours)
            : [];
        var current = hours.Count > 0 ? hours[0] : default;
        var condition = First(game.IsLoggedIn ? game.WeatherName : "", current.Name, "Unknown skies");
        var place = First(game.ZoneName, game.Character.WorldName, "Not logged in");
        WeatherChrome.Dock(frame, row, place, condition, current.IconId, bells, hours);
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
