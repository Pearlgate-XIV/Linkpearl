using System.Globalization;
using Linkpearl.Geometry;
using Linkpearl.Layout;
using Linkpearl.Platform;
using Linkpearl.Preferences;
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
    private readonly ISkyDesk sky;
    private float scroll;
    private int pane;

    public WeatherApplet(IGameSession game, IClock clock, IWeatherOracle oracle, ISkyDesk sky)
    {
        this.game = game;
        this.clock = clock;
        this.oracle = oracle;
        this.sky = sky;
    }

    AppletManifest IApplet.Manifest => Manifest;

    public AppletBadge Badge => AppletBadge.None;

    public void Enter(AppletEntry entry) => scroll = 0f;

    public void Leave()
    {
    }

    public void Compose(in AppletFrame frame)
    {
        var territory = (ushort)game.TerritoryId;
        var look = sky.Look(territory);
        var bells = look.Bells;
        var night = SkyChrome.IsNight(bells);
        var hours = game.IsLoggedIn && territory != 0
            ? WeatherChrome.AlignNow(oracle.Forecast(territory, WeatherChrome.ForecastHours), look)
            : [];
        var current = hours.Count > 0 ? hours[0] : default;
        var condition = First(look.Name, game.IsLoggedIn ? game.WeatherName : "", current.Name,
            PhoneLanguages.T("weather.unknown"));
        var place = First(game.ZoneName, game.MapPlace, game.Character.WorldName, PhoneLanguages.T("weather.offline"));
        var runs = Runs(hours);

        SkyChrome.Paint(frame, frame.Content, condition, night);
        var dock = frame.Content.BottomSlice(frame.Units(68f));
        var inner = new Rect(frame.Content.Min + new Vector2(frame.Units(16f), frame.Units(8f)),
            new Vector2(frame.Content.Max.X - frame.Units(16f), dock.Min.Y - frame.Units(6f)));
        var page = inner.Translate(new Vector2(0f, -scroll));
        frame.Paint.PushClip(inner);
        var content = pane == 0
            ? WeatherChrome.Forecast(frame, page, place, condition, current.IconId, bells, hours, runs,
                NextSky(hours))
            : WeatherChrome.Control(frame, page, bells, sky.ZoneChoices((ushort)game.TerritoryId), sky,
                sky.CompanionLoaded, night);
        frame.Paint.PopClip();
        ScrollSlider.Apply(frame, inner, ref scroll, content);
        pane = WeatherChrome.Tabs(frame, dock, pane, night);
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
                : name + " in " + ((int)MathF.Round((float)minutes)).ToString(CultureInfo.InvariantCulture) + "m";
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
