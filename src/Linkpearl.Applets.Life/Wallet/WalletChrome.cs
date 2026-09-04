using System.Globalization;
using Linkpearl.Applets;
using Linkpearl.Badges;
using Linkpearl.Cards;
using Linkpearl.Geometry;
using Linkpearl.Media;
using Linkpearl.Modules;
using Linkpearl.Painting;

namespace Linkpearl.Applets.Life.Wallet;

internal static class WalletChrome
{
    public static readonly string[] Nav = ["Home", "Earn", "Shop", "Rewards", "Play"];

    public static void Pearl(in AppletFrame frame, Rect area)
    {
        var texture = frame.Textures.FromFile(AppIconCatalog.Absolute(frame.Paths, "pearl.jpg")) ??
            frame.Textures.FromFile(AppIconCatalog.Absolute(frame.Paths, "pearl.png"));
        if (texture is { IsReady: true })
        {
            var dest = CoverFit.Contained(texture.Size, area);
            frame.Paint.Image(texture, dest, Vector4.One);
            return;
        }

        var gold = frame.Theme.Palette.WarmAccent;
        frame.Paint.FillCircle(area.Center, MathF.Min(area.Width, area.Height) * 0.38f,
            new Vector4(0.92f, 0.93f, 0.96f, 0.92f));
        frame.Paint.StrokeCircle(area.Center, MathF.Min(area.Width, area.Height) * 0.46f, gold,
            MathF.Max(1.2f, frame.Units(1.4f)));
    }

    public static void Amount(in AppletFrame frame, Rect area, int value, TextAlign align = TextAlign.Left)
    {
        frame.Text.DrawEllipsized(area, value.ToString("N0", CultureInfo.CurrentCulture),
            new TextStyle(FontRole.Title, frame.Theme.Palette.Ink, align));
    }

    public static bool Chip(in AppletFrame frame, Rect area, string label, bool on)
    {
        var gold = frame.Theme.Palette.WarmAccent;
        if (on)
        {
            frame.Paint.Fill(area, gold with { W = 0.92f }, frame.Units(12f));
            frame.Text.DrawIn(area, label,
                new TextStyle(FontRole.CaptionStrong, frame.Theme.Palette.AccentInk, TextAlign.Center));
        }
        else
        {
            frame.Paint.Stroke(area, gold with { W = 0.40f }, frame.Theme.Metrics.Hairline, frame.Units(12f));
            frame.Text.DrawIn(area, label,
                new TextStyle(FontRole.Caption, frame.Theme.Palette.InkMuted, TextAlign.Center));
        }

        return frame.Input.ConsumeClick(area);
    }

    public static bool GoldButton(in AppletFrame frame, Rect area, string label)
    {
        var gold = frame.Theme.Palette.WarmAccent;
        frame.Paint.Fill(area, gold with { W = 0.92f }, frame.Units(12f));
        frame.Text.DrawIn(area, label,
            new TextStyle(FontRole.CaptionStrong, frame.Theme.Palette.AccentInk, TextAlign.Center));
        return frame.Input.ConsumeClick(area);
    }

    public static bool GhostButton(in AppletFrame frame, Rect area, string label)
    {
        var gold = frame.Theme.Palette.WarmAccent;
        frame.Paint.Stroke(area, gold with { W = 0.55f }, frame.Theme.Metrics.Hairline, frame.Units(12f));
        frame.Text.DrawIn(area, label, new TextStyle(FontRole.CaptionStrong, gold, TextAlign.Center));
        return frame.Input.ConsumeClick(area);
    }

    public static void BadgeFace(in AppletFrame frame, Rect area, BadgeSpec spec, HostPaths paths)
    {
        if (spec.IconAsset.Length > 0 &&
            BadgeArt.TryDraw(frame.Paint, frame.Textures, paths, area, spec.IconAsset))
        {
            return;
        }

        var gold = frame.Theme.Palette.WarmAccent;
        frame.Paint.StrokeCircle(area.Center, MathF.Min(area.Width, area.Height) * 0.42f, gold,
            MathF.Max(1.1f, frame.Units(1.2f)));
    }

    public static void Signed(in AppletFrame frame, Rect area, int amount)
    {
        var gold = frame.Theme.Palette.WarmAccent;
        var ink = amount < 0 ? frame.Theme.Palette.Negative : gold;
        var prefix = amount > 0 ? "+" : string.Empty;
        frame.Text.DrawIn(area, prefix + amount.ToString("N0", CultureInfo.CurrentCulture),
            new TextStyle(FontRole.CaptionStrong, ink, TextAlign.Right));
    }

    public static string When(long unix)
    {
        var stamp = DateTimeOffset.FromUnixTimeSeconds(unix).ToLocalTime();
        var today = DateTimeOffset.Now.Date;
        if (stamp.Date == today)
        {
            return "Today · " + stamp.ToString("h:mm tt", CultureInfo.CurrentCulture);
        }

        if (stamp.Date == today.AddDays(-1))
        {
            return "Yesterday · " + stamp.ToString("h:mm tt", CultureInfo.CurrentCulture);
        }

        return stamp.ToString("MMM d · h:mm tt", CultureInfo.CurrentCulture);
    }
}
