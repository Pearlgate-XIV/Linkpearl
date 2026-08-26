using Linkpearl.Applets;
using Linkpearl.Cards;
using Linkpearl.Chassis;
using Linkpearl.Geometry;
using Linkpearl.Layout;
using Linkpearl.Painting;
using Linkpearl.Preferences;

namespace Linkpearl.Destinations.You;

// The player's personal space: profile identity, a couple of stat rows, and phone display
// settings — the one piece of "phone customization" the design brief lists for You that has
// anywhere real to live yet. Everything else the spec lists (glamours, collections, favorites)
// belongs here eventually, as entries in this list rather than as separate destinations — only a
// small placeholder slice is built in this pass.
public sealed class YouDestination : IDestinationScreen
{
    private readonly HandsetShapePreference shapePreference;

    public YouDestination(HandsetShapePreference shapePreference)
    {
        this.shapePreference = shapePreference;
    }

    public DestinationTab Tab => DestinationTab.You;

    public string Glyph => "🧑";

    public string Label => "You";

    public float Compose(in AppletFrame frame)
    {
        var inset = frame.Units(14f);
        var content = frame.Content.Inset(inset);
        var stack = new Stack(content, StackAxis.Vertical, frame.Units(10f));

        var profileRow = stack.Take(frame.Units(72f));
        CardChrome.Draw(frame, profileRow);
        DrawProfile(frame, profileRow.Inset(frame.Units(12f)));

        var stats = DemoData.ProfileStats;
        for (var index = 0; index < stats.Count; index++)
        {
            var statRow = stack.Take(frame.Units(44f));
            CardChrome.Draw(frame, statRow);
            DrawStat(frame, statRow.Inset(new Edges(frame.Units(12f), 0f)), stats[index]);
        }

        stack.Take(frame.Units(10f));
        CardChrome.DrawKicker(frame, stack.Take(frame.Units(16f)), "PHONE");

        var sizeRow = stack.Take(frame.Units(44f));
        CardChrome.Draw(frame, sizeRow);
        DrawSizeStepper(frame, sizeRow.Inset(new Edges(frame.Units(12f), 0f)));

        var formRow = stack.Take(frame.Units(44f));
        CardChrome.Draw(frame, formRow);
        DrawFormToggle(frame, formRow.Inset(new Edges(frame.Units(12f), 0f)));

        return (content.Height - stack.Remaining.Height) + inset * 2f;
    }

    private static void DrawProfile(in AppletFrame frame, Rect inset)
    {
        var stack = new Stack(inset, StackAxis.Vertical, frame.Units(2f));
        frame.Text.DrawIn(stack.Take(frame.Units(22f)), $"{DemoData.CharacterName} Morningstar",
            new TextStyle(FontRole.BodyStrong, frame.Theme.Palette.Ink));
        frame.Text.DrawIn(stack.Take(frame.Units(18f)), $"⛨ {DemoData.CharacterTitle} · {DemoData.World}",
            new TextStyle(FontRole.Caption, frame.Theme.Palette.InkMuted));
    }

    private static void DrawStat(in AppletFrame frame, Rect row, DemoData.ProfileStat stat)
    {
        frame.Text.DrawIn(row.LeftSlice(row.Width - frame.Units(140f)), stat.Label,
            new TextStyle(FontRole.Body, frame.Theme.Palette.Ink));
        frame.Text.DrawIn(row.RightSlice(frame.Units(140f)), stat.Value,
            new TextStyle(FontRole.Caption, frame.Theme.Palette.InkMuted, TextAlign.Right));
    }

    private void DrawSizeStepper(in AppletFrame frame, Rect row)
    {
        var index = HandsetSizeCatalog.StepIndex(shapePreference.ScaleStep);
        frame.Text.DrawIn(row.LeftSlice(row.Width - frame.Units(110f)), "Phone size",
            new TextStyle(FontRole.Body, frame.Theme.Palette.Ink));

        var controls = row.RightSlice(frame.Units(110f));
        var minus = controls.LeftSlice(frame.Units(28f));
        var label = new Rect(new Vector2(minus.Max.X, controls.Min.Y),
            new Vector2(controls.Max.X - frame.Units(28f), controls.Max.Y));
        var plus = controls.RightSlice(frame.Units(28f));

        DrawStepButton(frame, minus, "−", index - 1);
        frame.Text.DrawIn(label, HandsetSizeCatalog.StepLabels[index],
            new TextStyle(FontRole.BodyStrong, frame.Theme.Palette.Ink, TextAlign.Center));
        DrawStepButton(frame, plus, "+", index + 1);
    }

    private void DrawStepButton(in AppletFrame frame, Rect area, string glyph, int targetIndex)
    {
        var enabled = targetIndex >= 0 && targetIndex < HandsetSizeCatalog.ScaleSteps.Count;
        var ink = enabled ? frame.Theme.Palette.Ink : frame.Theme.Palette.InkFaint;
        frame.Text.DrawIn(area, glyph, new TextStyle(FontRole.BodyStrong, ink, TextAlign.Center));
        if (enabled && frame.Input.ConsumeClick(area))
        {
            shapePreference.ScaleStep = HandsetSizeCatalog.ScaleSteps[targetIndex];
        }
    }

    private void DrawFormToggle(in AppletFrame frame, Rect row)
    {
        frame.Text.DrawIn(row.LeftSlice(row.Width - frame.Units(140f)), "Form",
            new TextStyle(FontRole.Body, frame.Theme.Palette.Ink));

        var controls = row.RightSlice(frame.Units(140f));
        var half = controls.Width * 0.5f;
        var phoneArea = controls.LeftSlice(half);
        var tabletArea = controls.RightSlice(controls.Width - half);

        DrawFormOption(frame, phoneArea, "Phone", HandsetForm.Phone);
        DrawFormOption(frame, tabletArea, "Tablet", HandsetForm.Tablet);
    }

    private void DrawFormOption(in AppletFrame frame, Rect area, string label, HandsetForm form)
    {
        var isActive = shapePreference.Form == form;
        if (isActive)
        {
            frame.Paint.Fill(area.Inset(frame.Units(2f)), frame.Theme.Palette.Accent, frame.Units(999f));
        }

        var ink = isActive ? frame.Theme.Palette.AccentInk : frame.Theme.Palette.InkMuted;
        frame.Text.DrawIn(area, label, new TextStyle(FontRole.Caption, ink, TextAlign.Center));

        if (!isActive && frame.Input.ConsumeClick(area))
        {
            shapePreference.Form = form;
        }
    }
}
