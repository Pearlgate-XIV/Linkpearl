using Linkpearl.Applets;
using Linkpearl.Geometry;
using Linkpearl.Painting;
using Linkpearl.Persistence;
using Linkpearl.Platform;

namespace Linkpearl.Device.Shell;

internal static class CharacterClaimSheet
{
    public static bool Draw(in AppletFrame frame, Rect glass, IGameSession game, CharacterMigration migration)
    {
        var id = game.Character.ContentId;
        if (!migration.NeedsPrompt(id))
        {
            return false;
        }

        frame.Paint.Fill(glass, new Vector4(0.04f, 0.05f, 0.07f, 0.94f));
        var card = glass.Inset(new Edges(frame.Units(16f), frame.Units(48f), frame.Units(16f), frame.Units(24f)));
        frame.Paint.Fill(card, new Vector4(0.10f, 0.11f, 0.14f, 0.98f), frame.Units(18f));
        var inner = card.Inset(new Edges(frame.Units(16f), frame.Units(18f), frame.Units(16f), frame.Units(16f)));
        frame.Text.DrawIn(inner.TopSlice(frame.Units(18f)), "Character files",
            new TextStyle(FontRole.CaptionStrong, new Vector4(0.62f, 0.78f, 1f, 1f)));
        var head = Rect.FromSize(new Vector2(inner.Min.X, inner.Min.Y + frame.Units(26f)),
            new Vector2(inner.Width, frame.Units(52f)));
        frame.Text.DrawWrapped(head, "Keep calendar, pearls, and phone notes on this character?",
            new TextStyle(FontRole.Title, Vector4.One));
        var buttons = inner.BottomSlice(frame.Units(168f));
        var gap = frame.Units(8f);
        var row = (buttons.Height - gap * 2f) / 3f;
        var claim = new Rect(buttons.Min, new Vector2(buttons.Max.X, buttons.Min.Y + row));
        var later = new Rect(new Vector2(buttons.Min.X, claim.Max.Y + gap),
            new Vector2(buttons.Max.X, claim.Max.Y + gap + row));
        var never = new Rect(new Vector2(buttons.Min.X, later.Max.Y + gap), buttons.Max);
        DrawChoice(frame, claim, "This character", new Vector4(0.28f, 0.52f, 0.92f, 1f));
        DrawChoice(frame, later, "Later", new Vector4(0.22f, 0.24f, 0.28f, 1f));
        DrawChoice(frame, never, "Never for this install", new Vector4(0.22f, 0.24f, 0.28f, 1f));
        if (frame.Input.ConsumeClick(claim) || frame.Input.PressedInside(claim))
        {
            migration.Claim(id);
        }
        else if (frame.Input.ConsumeClick(later) || frame.Input.PressedInside(later))
        {
            migration.Later(id);
        }
        else if (frame.Input.ConsumeClick(never) || frame.Input.PressedInside(never))
        {
            migration.Never();
        }

        frame.Input.Claim(glass);
        return true;
    }

    private static void DrawChoice(in AppletFrame frame, Rect row, string label, Vector4 fill)
    {
        frame.Paint.Fill(row, fill, row.Height * 0.35f);
        frame.Text.DrawIn(row, label, new TextStyle(FontRole.BodyStrong, Vector4.One, TextAlign.Center));
    }
}
