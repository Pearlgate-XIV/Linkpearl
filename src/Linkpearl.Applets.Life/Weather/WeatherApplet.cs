using System.Globalization;
using Linkpearl.Applets;
using Linkpearl.Geometry;
using Linkpearl.Layout;
using Linkpearl.Painting;
using Linkpearl.Platform;
using Linkpearl.Time;
using Linkpearl.Weather;

namespace Linkpearl.Applets.Life.Weather;

public sealed class WeatherApplet : IApplet
{
    public static readonly AppletManifest Manifest = new()
    {
        Id = "weather",
        DisplayNameKey = "Weather",
        Family = AppletFamily.Life,
        Glyph = "☁",
        HomeOrder = 22,
    };

    private readonly IGameSession game;
    private readonly IClock clock;
    private readonly IWeatherOracle oracle;
    private float scroll;

    public WeatherApplet(IGameSession game, IClock clock, IWeatherOracle oracle)
    {
        this.game = game;
        this.clock = clock;
        this.oracle = oracle;
    }

    AppletManifest IApplet.Manifest => Manifest;

    public AppletBadge Badge => AppletBadge.None;

    public void Enter(AppletEntry entry) => scroll = 0f;

    public void Leave()
    {
    }

    public void Compose(in AppletFrame frame)
    {
        var bells = EorzeaTime.FromUnix(clock.UtcNow.ToUnixTimeSeconds());
        var night = SkyChrome.IsNight(bells);
        var hours = game.IsLoggedIn && game.TerritoryId != 0
            ? oracle.Forecast((ushort)game.TerritoryId, WeatherChrome.ForecastHours)
            : [];
        var current = hours.Count > 0 ? hours[0] : default;
        var condition = First(game.IsLoggedIn ? game.WeatherName : "", current.Name, "Unknown skies");
        var place = First(game.ZoneName, game.MapPlace, game.Character.WorldName, "Not logged in");
        var runs = Runs(hours);

        SkyChrome.Paint(frame, frame.Content, condition, night);
        var inner = frame.Content.Inset(new Edges(frame.Units(16f), frame.Units(8f), frame.Units(16f),
            frame.Units(10f)));
        var page = inner.Translate(new Vector2(0f, -scroll));
        frame.Paint.PushClip(inner);
        var content = WeatherChrome.App(frame, page, place, condition, current.IconId, bells, hours, runs,
            NextSky(hours));
        frame.Paint.PopClip();
        ScrollSlider.Apply(frame, inner, ref scroll, content);
    }

    private string NextSky(IReadOnlyList<WeatherWindow> hours)
    {
        if (hours.Count < 2)
        {
            return string.Empty;
        }

        var opening = hours[0].Name ?? string.Empty;
        for (var index = 1; index < hours.Count; index++)
        {
            var name = hours[index].Name ?? string.Empty;
            if (name.Length == 0 || string.Equals(name, opening, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var minutes = (hours[index].Starts - clock.UtcNow).TotalMinutes;
            return minutes < 1.0
                ? name
                : name + " in " + ((int)MathF.Round((float)minutes)).ToString(CultureInfo.InvariantCulture) + " min";
        }

        return string.Empty;
    }

    private static List<WeatherWindow> Runs(IReadOnlyList<WeatherWindow> hours)
    {
        var runs = new List<WeatherWindow>();
        for (var index = 0; index < hours.Count; index++)
        {
            var row = hours[index];
            if (string.IsNullOrEmpty(row.Name))
            {
                continue;
            }

            if (runs.Count > 0 && string.Equals(runs[^1].Name, row.Name, StringComparison.OrdinalIgnoreCase))
            {
                var last = runs[^1];
                runs[^1] = new WeatherWindow(last.Name, last.IconId, last.Starts, row.Ends);
                continue;
            }

            runs.Add(row);
        }

        return runs;
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
