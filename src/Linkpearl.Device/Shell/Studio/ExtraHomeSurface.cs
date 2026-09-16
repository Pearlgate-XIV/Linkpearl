using System.Globalization;
using Linkpearl.Applets;
using Linkpearl.Geometry;
using Linkpearl.Layout;
using Linkpearl.Painting;

namespace Linkpearl.Device.Shell.Studio;

internal static class ExtraHomeSurface
{
    public static void Draw(in AppletFrame frame, Rect area, int index, int total, Action add, Action remove,
        Action<AppletFrame, Rect> docks)
    {
        if (area.Width < 8f || area.Height < 8f)
        {
            return;
        }

        var inset = area.Inset(new Edges(frame.Units(28f), frame.Units(44f), frame.Units(28f), frame.Units(28f)));
        var inner = inset.Inset(new Edges(frame.Units(14f), frame.Units(16f), frame.Units(14f), frame.Units(16f)));
        var stack = new LayoutFlow(inner, StackAxis.Vertical, frame.Units(10f));
        var gold = frame.Theme.Palette.WarmAccent;
        frame.Text.DrawIn(stack.Take(frame.Units(16f)), "SCREEN " + (index + 2).ToString(CultureInfo.InvariantCulture),
            new TextStyle(FontRole.CaptionStrong, gold));
        var edit = frame.Units(total < AppsDock.ExtraCap ? 90f : 50f);
        var board = stack.Take(MathF.Max(frame.Units(64f), stack.Remaining.Height - edit));
        docks(frame.WithContent(board), board);

        if (total < AppsDock.ExtraCap)
        {
            var addRow = stack.Take(frame.Units(40f));
            StudioChrome.DrawPanel(frame, addRow);
            frame.Text.DrawIn(addRow.Inset(frame.Units(10f)), "Add another screen",
                new TextStyle(FontRole.CaptionStrong, gold));
            if (frame.Input.ConsumeClick(addRow))
            {
                add();
            }
        }

        var drop = stack.Take(frame.Units(40f));
        StudioChrome.DrawPanel(frame, drop);
        frame.Text.DrawIn(drop.Inset(frame.Units(10f)), "Remove this screen",
            new TextStyle(FontRole.CaptionStrong, frame.Theme.Palette.Ink));
        if (frame.Input.ConsumeClick(drop))
        {
            remove();
        }
    }
}
