using Linkpearl.Applets;
using Linkpearl.Cards;
using Linkpearl.Geometry;
using Linkpearl.Painting;
using Linkpearl.Preferences;

namespace Linkpearl.Destinations.Profile;

public sealed class InkPicker
{
    private bool dragPlate;
    private bool dragHue;

    public float Height(in AppletFrame frame) => frame.Units(168f);

    public void Draw(in AppletFrame frame, Rect area, float red, float green, float blue, Action<Vector4> set)
    {
        CardChrome.DrawGold(frame, area);
        var inner = area.Inset(frame.Units(10f));
        RgbToHsv(red, green, blue, out var hue, out var sat, out var val);

        var hueW = frame.Units(18f);
        var gap = frame.Units(8f);
        var preview = inner.RightSlice(frame.Units(36f));
        var body = new Rect(inner.Min, new Vector2(preview.Min.X - gap, inner.Max.Y));
        var bar = body.RightSlice(hueW);
        var plate = new Rect(body.Min, new Vector2(bar.Min.X - gap, body.Max.Y));

        DrawPlate(frame, plate, hue, sat, val);
        DrawHueBar(frame, bar, hue);
        frame.Input.Claim(plate);
        frame.Input.Claim(bar);
        frame.Input.Claim(area);
        frame.Paint.Fill(preview.TopSlice(frame.Units(36f)), new Vector4(red, green, blue, 1f), frame.Units(6f));
        frame.Paint.Stroke(preview.TopSlice(frame.Units(36f)), frame.Theme.Palette.WarmAccent, frame.Units(1.1f),
            frame.Units(6f));
        var hex = $"{(int)(red * 255f):X2}{(int)(green * 255f):X2}{(int)(blue * 255f):X2}";
        frame.Text.DrawIn(preview.Inset(new Edges(0f, frame.Units(40f), 0f, 0f)).TopSlice(frame.Units(16f)), hex,
            new TextStyle(FontRole.Caption, frame.Theme.Palette.InkMuted, TextAlign.Center));

        if (frame.Input.WasPressed(plate))
        {
            dragPlate = true;
        }

        if (dragPlate && frame.Input.IsHeld())
        {
            SamplePlate(plate, frame.Input.Pointer, out sat, out val);
            HsvToRgb(hue, sat, val, out red, out green, out blue);
            set(new Vector4(red, green, blue, 1f));
        }
        else
        {
            dragPlate = false;
        }

        if (frame.Input.WasPressed(bar))
        {
            dragHue = true;
        }

        if (dragHue && frame.Input.IsHeld())
        {
            hue = Math.Clamp((frame.Input.Pointer.Y - bar.Min.Y) / MathF.Max(bar.Height, 1f), 0f, 1f);
            HsvToRgb(hue, sat, val, out red, out green, out blue);
            set(new Vector4(red, green, blue, 1f));
        }
        else
        {
            dragHue = false;
        }
    }

    private static void DrawPlate(in AppletFrame frame, Rect plate, float hue, float sat, float val)
    {
        const int cells = 14;
        var cellW = plate.Width / cells;
        var cellH = plate.Height / cells;
        for (var row = 0; row < cells; row++)
        {
            for (var col = 0; col < cells; col++)
            {
                var s = (col + 0.5f) / cells;
                var v = 1f - (row + 0.5f) / cells;
                HsvToRgb(hue, s, v, out var r, out var g, out var b);
                var cell = Rect.FromSize(new Vector2(plate.Min.X + col * cellW, plate.Min.Y + row * cellH),
                    new Vector2(cellW + 0.6f, cellH + 0.6f));
                frame.Paint.Fill(cell, new Vector4(r, g, b, 1f));
            }
        }

        frame.Paint.Stroke(plate, frame.Theme.Palette.Ink with { W = 0.55f }, frame.Units(1f));
        var cursor = new Vector2(plate.Min.X + sat * plate.Width, plate.Min.Y + (1f - val) * plate.Height);
        frame.Paint.StrokeCircle(cursor, frame.Units(5f), new Vector4(1f, 1f, 1f, 0.95f), frame.Units(1.4f));
        frame.Paint.StrokeCircle(cursor, frame.Units(6.4f), new Vector4(0f, 0f, 0f, 0.75f), frame.Units(1.1f));
    }

    private static void DrawHueBar(in AppletFrame frame, Rect bar, float hue)
    {
        const int steps = 18;
        var slice = bar.Height / steps;
        for (var index = 0; index < steps; index++)
        {
            var t = (index + 0.5f) / steps;
            HsvToRgb(t, 1f, 1f, out var r, out var g, out var b);
            var cell = Rect.FromSize(new Vector2(bar.Min.X, bar.Min.Y + index * slice),
                new Vector2(bar.Width, slice + 0.6f));
            frame.Paint.Fill(cell, new Vector4(r, g, b, 1f));
        }

        frame.Paint.Stroke(bar, frame.Theme.Palette.Ink with { W = 0.55f }, frame.Units(1f));
        var y = bar.Min.Y + hue * bar.Height;
        frame.Paint.Fill(Rect.FromSize(new Vector2(bar.Min.X - frame.Units(2f), y - frame.Units(2f)),
            new Vector2(bar.Width + frame.Units(4f), frame.Units(4f))), new Vector4(1f, 1f, 1f, 0.92f));
    }

    private static void SamplePlate(Rect plate, Vector2 pointer, out float sat, out float val)
    {
        sat = Math.Clamp((pointer.X - plate.Min.X) / MathF.Max(plate.Width, 1f), 0f, 1f);
        val = 1f - Math.Clamp((pointer.Y - plate.Min.Y) / MathF.Max(plate.Height, 1f), 0f, 1f);
    }

    public static void RgbToHsv(float r, float g, float b, out float hue, out float sat, out float val)
    {
        var max = MathF.Max(r, MathF.Max(g, b));
        var min = MathF.Min(r, MathF.Min(g, b));
        val = max;
        var delta = max - min;
        sat = max <= 0.0001f ? 0f : delta / max;
        if (delta <= 0.0001f)
        {
            hue = 0f;
            return;
        }

        if (MathF.Abs(max - r) < 0.0001f)
        {
            hue = ((g - b) / delta + (g < b ? 6f : 0f)) / 6f;
        }
        else if (MathF.Abs(max - g) < 0.0001f)
        {
            hue = ((b - r) / delta + 2f) / 6f;
        }
        else
        {
            hue = ((r - g) / delta + 4f) / 6f;
        }
    }

    public static void HsvToRgb(float hue, float sat, float val, out float r, out float g, out float b)
    {
        hue = ((hue % 1f) + 1f) % 1f;
        sat = Math.Clamp(sat, 0f, 1f);
        val = Math.Clamp(val, 0f, 1f);
        var sector = hue * 6f;
        var i = (int)MathF.Floor(sector);
        var f = sector - i;
        var p = val * (1f - sat);
        var q = val * (1f - sat * f);
        var t = val * (1f - sat * (1f - f));
        switch (i % 6)
        {
            case 0:
                r = val;
                g = t;
                b = p;
                break;
            case 1:
                r = q;
                g = val;
                b = p;
                break;
            case 2:
                r = p;
                g = val;
                b = t;
                break;
            case 3:
                r = p;
                g = q;
                b = val;
                break;
            case 4:
                r = t;
                g = p;
                b = val;
                break;
            default:
                r = val;
                g = p;
                b = q;
                break;
        }
    }
}
