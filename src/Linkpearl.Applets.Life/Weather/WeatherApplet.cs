using Linkpearl.Applets;
using Linkpearl.Cards;
using Linkpearl.Geometry;
using Linkpearl.Layout;
using Linkpearl.Painting;
using Linkpearl.Platform;
using Linkpearl.Time;

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

    public WeatherApplet(IGameSession game, IClock clock)
    {
        this.game = game;
        this.clock = clock;
    }

    AppletManifest IApplet.Manifest => Manifest;

    public AppletBadge Badge => AppletBadge.None;

    public void Enter(AppletEntry entry)
    {
    }

    public void Leave()
    {
    }

    public void Compose(in AppletFrame frame)
    {
        var content = frame.Content.Inset(frame.Units(16f));
        var stack = new Stack(content, StackAxis.Vertical, frame.Units(10f));
        frame.Text.DrawIn(stack.Take(frame.Units(28f)), "Weather",
            new TextStyle(FontRole.Title, frame.Theme.Palette.Ink));
        frame.Text.DrawIn(stack.Take(frame.Units(18f)), "Sky over the zone you stand in.",
            new TextStyle(FontRole.Caption, frame.Theme.Palette.InkMuted));

        var face = stack.Take(frame.Units(120f));
        CardChrome.DrawGold(frame, face);
        var inset = face.Inset(frame.Units(14f));
        var weather = game.IsLoggedIn && game.WeatherName.Length > 0 ? game.WeatherName : "Unknown skies";
        frame.Text.DrawIn(inset.TopSlice(frame.Units(28f)), weather,
            new TextStyle(FontRole.Title, frame.Theme.Palette.Ink, TextAlign.Center));
        var zone = game.ZoneName.Length > 0 ? game.ZoneName : "Not logged in";
        frame.Text.DrawIn(inset.Inset(new Edges(0f, frame.Units(32f), 0f, frame.Units(28f))), zone,
            new TextStyle(FontRole.Body, frame.Theme.Palette.InkMuted, TextAlign.Center));
        var bells = EorzeaTime.FromUnix(clock.UtcNow.ToUnixTimeSeconds());
        var sky = bells.Hour is >= 6 and < 18 ? "Eorzea day" : "Eorzea night";
        frame.Text.DrawIn(inset.BottomSlice(frame.Units(22f)), bells.Format() + " · " + sky,
            new TextStyle(FontRole.CaptionStrong, frame.Theme.Palette.WarmAccent, TextAlign.Center));
    }
}
