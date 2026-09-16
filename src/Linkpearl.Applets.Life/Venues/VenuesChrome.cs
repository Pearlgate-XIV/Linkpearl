using System.Diagnostics;
using Linkpearl.Geometry;
using Linkpearl.Media;
using Linkpearl.Net;
using Linkpearl.Painting;

namespace Linkpearl.Applets.Life.Venues;

internal static class VenuesChrome
{
    public static readonly Vector4 Open = new(0.38f, 0.92f, 0.62f, 1f);
    public static readonly Vector4 Soon = new(0.98f, 0.78f, 0.32f, 1f);
    public static readonly Vector4 Night = new(0.94f, 0.32f, 0.52f, 1f);

    public static void PaintGround(in AppletFrame frame, Rect area) =>
        AppGround.Paint(frame, area, "venues");

    public static void Card(in AppletFrame frame, Rect area, bool open)
    {
        var radius = frame.Units(20f);
        frame.Paint.Fill(area, new Vector4(0.12f, 0.05f, 0.08f, 0.96f), radius);
        if (open)
        {
            frame.Paint.Stroke(area, Open with { W = 0.50f }, frame.Units(1.2f), radius);
        }
    }

    public static void PosterScrim(in AppletFrame frame, Rect area)
    {
        frame.Paint.FillGradient(area.BottomSlice(frame.Units(78f)), new Vector4(0f, 0f, 0f, 0f),
            new Vector4(0.04f, 0.01f, 0.03f, 0.88f), GradientAxis.Vertical);
    }

    public static void Sheet(in AppletFrame frame, Rect area)
    {
        frame.Paint.Fill(area, new Vector4(0.10f, 0.05f, 0.08f, 0.97f), frame.Units(22f), Corner.Top);
    }

    public static void SearchWell(in AppletFrame frame, Rect area)
    {
        frame.Paint.Fill(area, frame.Theme.Palette.SurfaceRaised, area.Height * 0.5f);
        frame.Paint.Stroke(area, frame.Theme.Palette.Separator with { W = 0.55f }, frame.Theme.Metrics.Hairline,
            area.Height * 0.5f);
    }

    public static int Segmented(in AppletFrame frame, Rect row, IReadOnlyList<string> labels, int selected)
    {
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
                frame.Paint.Fill(cell, Night, cell.Height * 0.5f);
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

    public static bool RoundMark(in AppletFrame frame, Rect area, string mark, bool filled)
    {
        var radius = MathF.Min(area.Width, area.Height) * 0.5f;
        if (filled)
        {
            frame.Paint.FillCircle(area.Center, radius, Night);
            frame.Text.DrawIn(area, mark, new TextStyle(FontRole.CaptionStrong, Vector4.One, TextAlign.Center));
        }
        else
        {
            frame.Paint.FillCircle(area.Center, radius, new Vector4(0.08f, 0.04f, 0.06f, 0.72f));
            frame.Text.DrawIn(area, mark,
                new TextStyle(FontRole.CaptionStrong, Vector4.One, TextAlign.Center));
        }

        return frame.Input.ConsumeClick(area);
    }

    public static bool GoPill(in AppletFrame frame, Rect area, bool ready)
    {
        var radius = area.Height * 0.5f;
        if (ready)
        {
            frame.Paint.Fill(area, Night, radius);
            frame.Text.DrawIn(area, "Go", new TextStyle(FontRole.CaptionStrong, Vector4.One, TextAlign.Center));
        }
        else
        {
            frame.Paint.Fill(area, frame.Theme.Palette.SurfaceOverlay, radius);
            frame.Paint.Stroke(area, Night with { W = 0.40f }, frame.Theme.Metrics.Hairline, radius);
            frame.Text.DrawIn(area, "Go",
                new TextStyle(FontRole.CaptionStrong, frame.Theme.Palette.InkMuted, TextAlign.Center));
        }

        return frame.Input.ConsumeClick(area);
    }

    public static void TagChip(in AppletFrame frame, Rect area, string label)
    {
        var radius = area.Height * 0.5f;
        frame.Paint.Fill(area, Night with { W = 0.16f }, radius);
        frame.Text.DrawIn(area, label,
            new TextStyle(FontRole.CaptionStrong, new Vector4(1f, 0.86f, 0.90f, 1f), TextAlign.Center));
    }

    public static float ChipWrap(in AppletFrame frame, Rect area, IReadOnlyList<string> labels, bool paint)
    {
        var x = 0f;
        var y = 0f;
        var height = frame.Units(26f);
        var gap = frame.Units(6f);
        for (var index = 0; index < labels.Count; index++)
        {
            var label = labels[index];
            if (label.Length == 0)
            {
                continue;
            }

            var width = MathF.Min(area.Width,
                frame.Text.Measure(label, FontRole.CaptionStrong).X + frame.Units(16f));
            if (x > 0f && x + width > area.Width)
            {
                x = 0f;
                y += height + gap;
            }

            if (paint)
            {
                TagChip(frame, Rect.FromSize(new Vector2(area.Min.X + x, area.Min.Y + y), new Vector2(width, height)),
                    label);
            }

            x += width + gap;
        }

        return labels.Count == 0 ? 0f : y + height;
    }

    public static bool Chip(in AppletFrame frame, Rect area, string label, bool on)
    {
        var radius = area.Height * 0.5f;
        var fill = on ? Night : frame.Theme.Palette.SurfaceOverlay;
        var ink = on ? Vector4.One : frame.Theme.Palette.InkMuted;
        frame.Paint.Fill(area, fill, radius);
        if (!on)
        {
            frame.Paint.Stroke(area, Night with { W = 0.28f }, frame.Theme.Metrics.Hairline, radius);
        }

        frame.Text.DrawIn(area, label, new TextStyle(FontRole.CaptionStrong, ink, TextAlign.Center));
        return frame.Input.ConsumeClick(area);
    }

    public static void Badge(in AppletFrame frame, Rect area, string label, Vector4 color)
    {
        frame.Paint.Fill(area, color with { W = 0.18f }, area.Height * 0.5f);
        frame.Paint.Stroke(area, color with { W = 0.70f }, frame.Theme.Metrics.Hairline, area.Height * 0.5f);
        frame.Text.DrawIn(area, label, new TextStyle(FontRole.CaptionStrong, color, TextAlign.Center));
    }

    public static bool Button(in AppletFrame frame, Rect area, string label, bool filled)
    {
        var radius = area.Height * 0.5f;
        if (filled)
        {
            frame.Paint.Fill(area, Night, radius);
            frame.Text.DrawIn(area, label,
                new TextStyle(FontRole.CaptionStrong, Vector4.One, TextAlign.Center));
        }
        else
        {
            frame.Paint.Fill(area, frame.Theme.Palette.SurfaceOverlay, radius);
            frame.Paint.Stroke(area, Night with { W = 0.42f }, frame.Theme.Metrics.Hairline, radius);
            frame.Text.DrawIn(area, label,
                new TextStyle(FontRole.CaptionStrong, new Vector4(1f, 0.86f, 0.90f, 1f), TextAlign.Center));
        }

        return frame.Input.ConsumeClick(area);
    }

    public static bool Banner(in AppletFrame frame, IVenuesDesk desk, Rect area, string url, float radius)
    {
        if (url.Length == 0)
        {
            return false;
        }

        var path = desk.BannerPath(url);
        if (path is not { Length: > 0 })
        {
            desk.PrefetchBanner(url);
            path = desk.BannerPath(url);
        }

        if (path is not { Length: > 0 })
        {
            return false;
        }

        var texture = frame.Textures.FromFile(path);
        if (texture is not { IsReady: true })
        {
            return false;
        }

        var uv = CoverFit.Uv(texture.Size, area.Size);
        frame.Paint.ImageRounded(texture, area, uv.Min, uv.Max, Vector4.One, radius);
        return true;
    }

    public static bool Heart(in AppletFrame frame, Rect area, bool on)
    {
        var color = on ? Night : frame.Theme.Palette.InkMuted;
        frame.Text.DrawIn(area, on ? "♥" : "♡",
            new TextStyle(FontRole.CaptionStrong, color, TextAlign.Center));
        return frame.Input.ConsumeClick(area);
    }

    public static void Footer(in AppletFrame frame, Rect area)
    {
        frame.Text.DrawIn(area, "Data from FFXIV Venues",
            new TextStyle(FontRole.Caption, frame.Theme.Palette.InkMuted, TextAlign.Center));
        if (frame.Input.ConsumeClick(area))
        {
            OpenUrl(VenuesDeskUrl);
        }
    }

    public static void OpenUrl(string url)
    {
        if (url.Length == 0)
        {
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch
        {
            // The OS can refuse a browse; the row still stays tappable.
        }
    }

    private const string VenuesDeskUrl = "https://ffxivvenues.com";
}
