using Linkpearl.Applets;
using Linkpearl.Geometry;
using Linkpearl.Layout;
using Linkpearl.Painting;

namespace Linkpearl.Applets.Life.Calculator;

public sealed class CalculatorApplet : IApplet
{
    private static readonly string[] ButtonGrid =
    {
        "C", "±", "%", "÷",
        "7", "8", "9", "×",
        "4", "5", "6", "−",
        "1", "2", "3", "+",
        "0", "0", ".", "=",
    };

    public static readonly AppletManifest Manifest = new()
    {
        Id = "calculator",
        DisplayNameKey = "Calculator",
        Family = AppletFamily.Life,
        Glyph = "🧮",
        HomeOrder = 10,
    };

    private readonly CalculatorState state = new();

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
        var displayArea = content.TopSlice(content.Height * 0.22f);
        frame.Paint.Fill(displayArea, frame.Theme.Palette.SurfaceSunken, frame.Units(12f));
        frame.Text.DrawIn(displayArea.Inset(frame.Units(12f)), state.Display,
            new TextStyle(FontRole.Display, frame.Theme.Palette.Ink, TextAlign.Right));

        var padArea = new Rect(new Vector2(content.Min.X, displayArea.Max.Y + frame.Units(16f)), content.Max);
        var grid = new TileGrid(padArea, 4, 5, frame.Units(10f));
        for (var index = 0; index < ButtonGrid.Length; index++)
        {
            if (index == 17)
            {
                continue;
            }

            var cell = index == 16 ? MergeWithNext(grid, index) : grid.CellAt(index);
            DrawButton(frame, cell, ButtonGrid[index]);
        }
    }

    private static Rect MergeWithNext(TileGrid grid, int index)
    {
        var first = grid.CellAt(index);
        var second = grid.CellAt(index + 1);
        return new Rect(first.Min, second.Max);
    }

    private void DrawButton(in AppletFrame frame, Rect cell, string label)
    {
        var isOperator = label is "÷" or "×" or "−" or "+" or "=";
        var isFunction = label is "C" or "±" or "%";
        var background = isOperator
            ? frame.Theme.Palette.Accent
            : isFunction
                ? frame.Theme.Palette.SurfaceOverlay
                : frame.Theme.Palette.SurfaceRaised;
        var ink = isOperator ? frame.Theme.Palette.AccentInk : frame.Theme.Palette.Ink;

        frame.Paint.Fill(cell, background, frame.Units(999f));
        frame.Text.DrawIn(cell, label, new TextStyle(FontRole.Title, ink, TextAlign.Center));

        if (frame.Input.ConsumeClick(cell))
        {
            Press(label);
        }
    }

    private void Press(string label)
    {
        switch (label)
        {
            case "C":
                state.Clear();
                break;
            case "±":
                state.PressToggleSign();
                break;
            case "%":
                state.PressPercent();
                break;
            case ".":
                state.PressDecimal();
                break;
            case "=":
                state.PressEquals();
                break;
            case "÷" or "×" or "−" or "+":
                state.PressOperator(label[0]);
                break;
            default:
                state.PressDigit(label[0]);
                break;
        }
    }
}
