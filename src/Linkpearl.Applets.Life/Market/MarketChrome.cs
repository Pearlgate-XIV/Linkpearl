using System.Globalization;
using Linkpearl.Cards;
using Linkpearl.Geometry;
using Linkpearl.Painting;

namespace Linkpearl.Applets.Life.Market;

internal static class MarketChrome
{
    public static Vector4 Brand() => AppGround.Brand("market");

    public static void Ground(in AppletFrame frame) =>
        AppGround.Paint(frame, frame.Content, "market");

    public static void Title(in AppletFrame frame, Rect area, string label)
    {
        frame.Text.DrawEllipsized(area, label,
            new TextStyle(FontRole.Title, frame.Theme.Palette.Ink));
    }

    public static void Kicker(in AppletFrame frame, Rect area, string label)
    {
        frame.Text.DrawEllipsized(area, label,
            new TextStyle(FontRole.CaptionStrong, Brand()));
    }

    public static void Mute(in AppletFrame frame, Rect area, string label)
    {
        frame.Text.DrawWrapped(area, label,
            new TextStyle(FontRole.Caption, frame.Theme.Palette.InkMuted));
    }

    public static void Plate(in AppletFrame frame, Rect area, bool lift = false)
    {
        if (lift)
        {
            CardChrome.DrawGold(frame, area);
            return;
        }

        CardChrome.Draw(frame, area);
    }

    public static void SearchWell(in AppletFrame frame, Rect area)
    {
        var radius = area.Height * 0.5f;
        frame.Paint.Fill(area, frame.Theme.Palette.SurfaceRaised, radius);
        frame.Paint.Stroke(area, Brand() with { W = 0.28f }, frame.Units(1f), radius);
    }

    public static int Segmented(in AppletFrame frame, Rect row, IReadOnlyList<string> labels, int selected)
    {
        var brand = Brand();
        var radius = row.Height * 0.5f;
        frame.Paint.Fill(row, frame.Theme.Palette.SurfaceOverlay, radius);
        var width = row.Width / Math.Max(1, labels.Count);
        var picked = selected;
        for (var index = 0; index < labels.Count; index++)
        {
            var cell = Rect.FromSize(new Vector2(row.Min.X + width * index, row.Min.Y),
                new Vector2(width, row.Height)).Inset(frame.Units(2f));
            var on = index == selected;
            if (on)
            {
                frame.Paint.Fill(cell, brand, cell.Height * 0.5f);
            }

            frame.Text.DrawIn(cell, labels[index],
                new TextStyle(FontRole.CaptionStrong, on ? Vector4.One : frame.Theme.Palette.InkMuted,
                    TextAlign.Center));
            if (frame.Input.ConsumeClick(cell))
            {
                picked = index;
            }
        }

        return picked;
    }

    public static void Hero(in AppletFrame frame, Rect area, string kicker, string value, string note)
    {
        var brand = Brand();
        var gold = frame.Theme.Palette.WarmAccent;
        var radius = frame.Units(16f);
        frame.Paint.Fill(area, new Vector4(0.04f, 0.16f, 0.14f, 0.96f), radius);
        frame.Paint.Glow(area, brand with { W = 0.18f }, radius, frame.Units(12f));
        frame.Paint.Stroke(area, brand with { W = 0.48f }, frame.Units(1.2f), radius);
        var inset = area.Inset(new Edges(frame.Units(14f), frame.Units(10f)));
        frame.Text.DrawEllipsized(inset.TopSlice(frame.Units(14f)), kicker,
            new TextStyle(FontRole.CaptionStrong, brand));
        frame.Text.DrawEllipsized(inset.Inset(new Edges(0f, frame.Units(16f), 0f, frame.Units(18f))), value,
            new TextStyle(FontRole.Title, gold));
        frame.Text.DrawEllipsized(inset.BottomSlice(frame.Units(16f)), note,
            new TextStyle(FontRole.Caption, frame.Theme.Palette.InkMuted));
    }

    public static void Stat(in AppletFrame frame, Rect area, string label, string value)
    {
        Plate(frame, area);
        var inset = area.Inset(new Edges(frame.Units(8f), frame.Units(6f)));
        frame.Text.DrawEllipsized(inset.TopSlice(frame.Units(14f)), label,
            new TextStyle(FontRole.Caption, frame.Theme.Palette.InkMuted));
        frame.Text.DrawEllipsized(inset.BottomSlice(frame.Units(18f)), value,
            new TextStyle(FontRole.CaptionStrong, frame.Theme.Palette.Ink));
    }

    public static bool Star(in AppletFrame frame, Rect area, bool on)
    {
        var brand = Brand();
        frame.Text.DrawIn(area, on ? "★" : "☆",
            new TextStyle(FontRole.Title, on ? brand : frame.Theme.Palette.InkMuted, TextAlign.Center));
        return frame.Input.ConsumeClick(area);
    }

    public static string Gil(int value) =>
        value <= 0 ? "—" : value.ToString("N0", CultureInfo.InvariantCulture) + " gil";

    public static string Ago(long unix, bool milliseconds)
    {
        if (unix <= 0)
        {
            return string.Empty;
        }

        var seconds = milliseconds ? unix / 1000L : unix;
        DateTimeOffset then;
        try
        {
            then = DateTimeOffset.FromUnixTimeSeconds(seconds);
        }
        catch (ArgumentOutOfRangeException)
        {
            return string.Empty;
        }

        var delta = DateTimeOffset.UtcNow - then;
        if (delta.TotalSeconds < 45)
        {
            return "just now";
        }

        if (delta.TotalMinutes < 60)
        {
            return ((int)delta.TotalMinutes).ToString(CultureInfo.InvariantCulture) + "m ago";
        }

        if (delta.TotalHours < 24)
        {
            return ((int)delta.TotalHours).ToString(CultureInfo.InvariantCulture) + "h ago";
        }

        return ((int)delta.TotalDays).ToString(CultureInfo.InvariantCulture) + "d ago";
    }
}
