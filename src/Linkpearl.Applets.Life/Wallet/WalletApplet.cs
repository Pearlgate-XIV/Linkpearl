using System.Globalization;
using Linkpearl.Applets;
using Linkpearl.Cards;
using Linkpearl.Geometry;
using Linkpearl.Layout;
using Linkpearl.Painting;
using Linkpearl.Platform;

namespace Linkpearl.Applets.Life.Wallet;

public sealed class WalletApplet : IApplet
{
    public static readonly AppletManifest Manifest = new()
    {
        Id = "wallet",
        DisplayNameKey = "Wallet",
        Family = AppletFamily.Life,
        Glyph = "⬡",
        HomeOrder = 32,
    };

    private readonly IGameSession game;

    public WalletApplet(IGameSession game)
    {
        this.game = game;
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
        frame.Text.DrawIn(stack.Take(frame.Units(28f)), "Wallet",
            new TextStyle(FontRole.Title, frame.Theme.Palette.Ink));
        frame.Text.DrawIn(stack.Take(frame.Units(18f)), "Gil on this character.",
            new TextStyle(FontRole.Caption, frame.Theme.Palette.InkMuted));

        var face = stack.Take(frame.Units(110f));
        CardChrome.DrawGold(frame, face);
        var inset = face.Inset(frame.Units(14f));
        CardChrome.DrawKicker(frame, inset.TopSlice(frame.Units(16f)), "Gil", frame.Theme.Palette.WarmAccent);
        var amount = game.IsLoggedIn ? game.Gil.ToString("N0", CultureInfo.CurrentCulture) : "—";
        frame.Text.DrawIn(inset.Inset(new Edges(0f, frame.Units(22f), 0f, frame.Units(22f))), amount,
            new TextStyle(FontRole.Display, frame.Theme.Palette.Ink, TextAlign.Center));
        var who = game.Character.Name.Length > 0 ? game.Character.Name : "Log in to count gil.";
        frame.Text.DrawIn(inset.BottomSlice(frame.Units(20f)), who,
            new TextStyle(FontRole.Caption, frame.Theme.Palette.InkMuted, TextAlign.Center));
    }
}
