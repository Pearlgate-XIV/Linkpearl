using System.Globalization;
using Linkpearl.Applets;
using Linkpearl.Cards;
using Linkpearl.Geometry;
using Linkpearl.Layout;
using Linkpearl.Painting;
using Linkpearl.Platform;
using Linkpearl.Time;

namespace Linkpearl.Applets.Life.Camera;

public sealed class CameraApplet : IApplet
{
    public static readonly AppletManifest Manifest = new()
    {
        Id = "camera",
        DisplayNameKey = "Camera",
        Family = AppletFamily.Life,
        Glyph = "◎",
        HomeOrder = 28,
        Capabilities = AppletCapabilities.UsesCamera,
    };

    private readonly IGameSession game;
    private readonly IClock clock;
    private string lastStill = string.Empty;

    public CameraApplet(IGameSession game, IClock clock)
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
        frame.Text.DrawIn(stack.Take(frame.Units(28f)), "Camera",
            new TextStyle(FontRole.Title, frame.Theme.Palette.Ink));
        frame.Text.DrawIn(stack.Take(frame.Units(18f)), "Hold the pearl up. The still is a note, not a screenshot.",
            new TextStyle(FontRole.Caption, frame.Theme.Palette.InkMuted));

        var finder = stack.Take(frame.Units(160f));
        CardChrome.DrawGold(frame, finder);
        var inset = finder.Inset(frame.Units(14f));
        var zone = game.IsLoggedIn && game.ZoneName.Length > 0 ? game.ZoneName : "No view";
        var job = game.JobName.Length > 0 ? game.JobName : "—";
        frame.Text.DrawIn(inset.TopSlice(frame.Units(22f)), zone,
            new TextStyle(FontRole.Title, frame.Theme.Palette.Ink, TextAlign.Center));
        frame.Text.DrawIn(inset.Inset(new Edges(0f, frame.Units(28f), 0f, frame.Units(36f))), job,
            new TextStyle(FontRole.Body, frame.Theme.Palette.InkMuted, TextAlign.Center));
        frame.Paint.StrokeCircle(finder.Center + new Vector2(0f, frame.Units(18f)), frame.Units(28f),
            frame.Theme.Palette.WarmAccent with { W = 0.55f }, frame.Units(2f));

        var shutter = stack.Take(frame.Units(48f));
        frame.Paint.Fill(shutter, frame.Theme.Palette.Accent, shutter.Height * 0.5f);
        frame.Text.DrawIn(shutter, "Shutter",
            new TextStyle(FontRole.BodyStrong, frame.Theme.Palette.AccentInk, TextAlign.Center));
        if (frame.Input.ConsumeClick(shutter))
        {
            lastStill = zone + " · " + job + " · " + clock.Now.ToString("HH:mm", CultureInfo.CurrentCulture);
        }

        var still = stack.Take(frame.Units(56f));
        CardChrome.Draw(frame, still);
        frame.Text.DrawWrapped(still.Inset(frame.Units(12f)),
            lastStill.Length > 0 ? lastStill : "No still yet.",
            new TextStyle(FontRole.Caption, frame.Theme.Palette.InkMuted));
    }
}
