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
        var card = glass.Inset(new Edges(frame.Units(16f), frame.Units(36f), frame.Units(16f), frame.Units(20f)));
        frame.Paint.Fill(card, new Vector4(0.10f, 0.11f, 0.14f, 0.98f), frame.Units(18f));
        var inner = card.Inset(new Edges(frame.Units(16f), frame.Units(16f), frame.Units(16f), frame.Units(14f)));
        frame.Text.DrawIn(inner.TopSlice(frame.Units(18f)), "Main character",
            new TextStyle(FontRole.CaptionStrong, new Vector4(0.62f, 0.78f, 1f, 1f)));
        var body = new Rect(
            new Vector2(inner.Min.X, inner.Min.Y + frame.Units(24f)),
            new Vector2(inner.Max.X, inner.Max.Y - frame.Units(108f)));
        frame.Text.DrawWrapped(body,
            "Calendar, Pearls, and Phone notes belong to one main character. You can choose that character now (the one you just logged in as). You can change it later in Tune \u2192 General.",
            new TextStyle(FontRole.Body, Vector4.One));
        var buttons = inner.BottomSlice(frame.Units(100f));
        var gap = frame.Units(8f);
        var row = (buttons.Height - gap) / 2f;
        var claim = new Rect(buttons.Min, new Vector2(buttons.Max.X, buttons.Min.Y + row));
        var later = new Rect(new Vector2(buttons.Min.X, claim.Max.Y + gap), buttons.Max);
        DrawChoice(frame, claim, "Use this character as main", new Vector4(0.28f, 0.52f, 0.92f, 1f));
        DrawChoice(frame, later, "Decide later", new Vector4(0.22f, 0.24f, 0.28f, 1f));
        if (frame.Input.ConsumeClick(claim) || frame.Input.PressedInside(claim))
        {
            migration.Claim(id);
        }
        else if (frame.Input.ConsumeClick(later) || frame.Input.PressedInside(later))
        {
            migration.Later(id);
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
