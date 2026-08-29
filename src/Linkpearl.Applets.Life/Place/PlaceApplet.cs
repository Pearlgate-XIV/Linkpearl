using Linkpearl.Applets;
using Linkpearl.Cards;
using Linkpearl.Geometry;
using Linkpearl.Layout;
using Linkpearl.Painting;
using Linkpearl.Platform;

namespace Linkpearl.Applets.Life.Place;

public sealed class PlaceApplet : IApplet
{
    public static readonly AppletManifest Manifest = new()
    {
        Id = "place",
        DisplayNameKey = "Place",
        Family = AppletFamily.Life,
        Glyph = "◉",
        HomeOrder = 25,
    };

    private readonly IGameSession game;

    public PlaceApplet(IGameSession game)
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
        frame.Text.DrawIn(stack.Take(frame.Units(28f)), "Place",
            new TextStyle(FontRole.Title, frame.Theme.Palette.Ink));
        frame.Text.DrawIn(stack.Take(frame.Units(18f)), "Where you are right now.",
            new TextStyle(FontRole.Caption, frame.Theme.Palette.InkMuted));

        if (!game.IsLoggedIn)
        {
            var empty = stack.Take(frame.Units(72f));
            CardChrome.Draw(frame, empty);
            frame.Text.DrawIn(empty.Inset(frame.Units(12f)), "Log in to see your zone and job.",
                new TextStyle(FontRole.Body, frame.Theme.Palette.InkMuted, TextAlign.Center));
            return;
        }

        var identity = game.Character;
        DrawRow(frame, stack.Take(frame.Units(64f)), "You",
            identity.Name.Length > 0 ? identity.Name : "Unknown",
            identity.WorldName);
        DrawRow(frame, stack.Take(frame.Units(64f)), "Job",
            game.JobName.Length > 0 ? game.JobName : "Unknown", string.Empty);
        DrawRow(frame, stack.Take(frame.Units(64f)), "Zone",
            game.ZoneName.Length > 0 ? game.ZoneName : "Unknown", string.Empty);

        var chips = stack.Take(frame.Units(40f));
        var cursor = chips.Min.X;
        cursor = DrawChip(frame, chips, cursor, game.IsInParty, "Party");
        cursor = DrawChip(frame, chips, cursor, game.IsInDuty, "Duty");
        DrawChip(frame, chips, cursor, game.IsInCombat, "Combat");
    }

    private static void DrawRow(in AppletFrame frame, Rect row, string kicker, string title, string detail)
    {
        CardChrome.Draw(frame, row);
        var inset = row.Inset(frame.Units(12f));
        CardChrome.DrawKicker(frame, inset.TopSlice(frame.Units(16f)), kicker, frame.Theme.Palette.WarmAccent);
        frame.Text.DrawEllipsized(inset.Inset(new Edges(0f, frame.Units(18f), 0f, detail.Length > 0 ? frame.Units(18f) : 0f)),
            title, new TextStyle(FontRole.BodyStrong, frame.Theme.Palette.Ink));
        if (detail.Length > 0)
        {
            frame.Text.DrawEllipsized(inset.BottomSlice(frame.Units(18f)), detail,
                new TextStyle(FontRole.Caption, frame.Theme.Palette.InkMuted));
        }
    }

    private static float DrawChip(in AppletFrame frame, Rect row, float cursor, bool on, string label)
    {
        if (!on)
        {
            return cursor;
        }

        var width = frame.Text.Measure(label, FontRole.Caption).X + frame.Units(20f);
        var chip = new Rect(new Vector2(cursor, row.Min.Y), new Vector2(cursor + width, row.Max.Y));
        frame.Paint.Fill(chip, frame.Theme.Palette.Accent with { W = 0.85f }, chip.Height * 0.5f);
        frame.Text.DrawIn(chip, label,
            new TextStyle(FontRole.CaptionStrong, frame.Theme.Palette.AccentInk, TextAlign.Center));
        return cursor + width + frame.Units(6f);
    }
}