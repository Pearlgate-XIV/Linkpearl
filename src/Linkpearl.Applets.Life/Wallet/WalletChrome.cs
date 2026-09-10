using System.Globalization;
using Linkpearl.Applets;
using Linkpearl.Badges;
using Linkpearl.Geometry;
using Linkpearl.Media;
using Linkpearl.Modules;
using Linkpearl.Painting;

namespace Linkpearl.Applets.Life.Wallet;

internal static class WalletChrome
{
    public static readonly string[] Nav = ["Wallet", "Shop", "Items", "History"];

    public static void PaintGround(in AppletFrame frame, Rect area) =>
        AppGround.Paint(frame, area, "wallet");

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
        var radius = MathF.Min(area.Width, area.Height) * 0.38f;
        frame.Paint.FillCircle(area.Center, radius, new Vector4(0.94f, 0.88f, 0.62f, 0.96f));
        frame.Paint.StrokeCircle(area.Center, MathF.Min(area.Width, area.Height) * 0.46f, gold,
            MathF.Max(1.2f, frame.Units(1.4f)));
        frame.Paint.FillCircle(area.Center, radius * 0.18f, gold);
    }

    public static void Hero(in AppletFrame frame, Rect area, int balance, int earned, int spent)
    {
        var gold = frame.Theme.Palette.WarmAccent;
        var radius = frame.Units(18f);
        frame.Paint.Fill(area, new Vector4(0.14f, 0.11f, 0.05f, 0.96f), radius);
        frame.Paint.Glow(area, gold with { W = 0.18f }, radius, frame.Units(16f));
        frame.Paint.Stroke(area, gold with { W = 0.42f }, frame.Units(1.2f), radius);
        var mark = area.RightSlice(MathF.Min(area.Height, frame.Units(92f))).Inset(frame.Units(10f));
        Pearl(frame, mark);
        var inset = area.Inset(new Edges(frame.Units(16f), frame.Units(12f), mark.Width + frame.Units(6f),
            frame.Units(12f)));
        frame.Text.DrawIn(inset.TopSlice(frame.Units(16f)), "YOUR PEARLS",
            new TextStyle(FontRole.CaptionStrong, gold));
        Amount(frame, inset.Inset(new Edges(0f, frame.Units(18f), 0f, frame.Units(20f))), balance);
        frame.Text.DrawEllipsized(inset.BottomSlice(frame.Units(16f)),
            earned.ToString("N0", CultureInfo.CurrentCulture) + " earned  ·  " +
            spent.ToString("N0", CultureInfo.CurrentCulture) + " spent",
            new TextStyle(FontRole.Caption, gold with { W = 0.82f }));
    }

    public static void Amount(in AppletFrame frame, Rect area, int value, TextAlign align = TextAlign.Left)
    {
        frame.Text.DrawEllipsized(area, value.ToString("N0", CultureInfo.CurrentCulture),
            new TextStyle(FontRole.Display, frame.Theme.Palette.Ink, align));
    }

    public static void Title(in AppletFrame frame, Rect area, string label)
    {
        frame.Text.DrawEllipsized(area, label, new TextStyle(FontRole.Title, frame.Theme.Palette.Ink));
    }

    public static void Kicker(in AppletFrame frame, Rect area, string label)
    {
        frame.Text.DrawEllipsized(area, label,
            new TextStyle(FontRole.CaptionStrong, frame.Theme.Palette.WarmAccent));
    }

    public static bool Chip(in AppletFrame frame, Rect area, string label, bool on)
    {
        var gold = frame.Theme.Palette.WarmAccent;
        var radius = frame.Units(12f);
        if (on)
        {
            frame.Paint.Fill(area, gold with { W = 0.94f }, radius);
            frame.Text.DrawIn(area, label,
                new TextStyle(FontRole.CaptionStrong, frame.Theme.Palette.AccentInk, TextAlign.Center));
        }
        else
        {
            frame.Paint.Fill(area, frame.Theme.Palette.SurfaceOverlay, radius);
            frame.Paint.Stroke(area, gold with { W = 0.32f }, frame.Theme.Metrics.Hairline, radius);
            frame.Text.DrawIn(area, label,
                new TextStyle(FontRole.Caption, frame.Theme.Palette.InkMuted, TextAlign.Center));
        }

        return frame.Input.ConsumeClick(area);
    }

    public static bool GoldButton(in AppletFrame frame, Rect area, string label)
    {
        var gold = frame.Theme.Palette.WarmAccent;
        frame.Paint.Fill(area, gold with { W = 0.94f }, frame.Units(14f));
        frame.Text.DrawIn(area, label,
            new TextStyle(FontRole.BodyStrong, frame.Theme.Palette.AccentInk, TextAlign.Center));
        return frame.Input.ConsumeClick(area);
    }

    public static bool GhostButton(in AppletFrame frame, Rect area, string label)
    {
        var gold = frame.Theme.Palette.WarmAccent;
        frame.Paint.Fill(area, frame.Theme.Palette.SurfaceOverlay, frame.Units(14f));
        frame.Paint.Stroke(area, gold with { W = 0.50f }, frame.Units(1.2f), frame.Units(14f));
        frame.Text.DrawIn(area, label, new TextStyle(FontRole.BodyStrong, gold, TextAlign.Center));
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
            return stamp.ToString("h:mm tt", CultureInfo.CurrentCulture);
        }

        if (stamp.Date == today.AddDays(-1))
        {
            return "Yesterday · " + stamp.ToString("h:mm tt", CultureInfo.CurrentCulture);
        }

        return stamp.ToString("MMM d · h:mm tt", CultureInfo.CurrentCulture);
    }

    public static string DayLabel(long unix)
    {
        var stamp = DateTimeOffset.FromUnixTimeSeconds(unix).ToLocalTime().Date;
        var today = DateTimeOffset.Now.Date;
        if (stamp == today)
        {
            return "TODAY";
        }

        if (stamp == today.AddDays(-1))
        {
            return "YESTERDAY";
        }

        return stamp.ToString("MMMM d", CultureInfo.CurrentCulture).ToUpperInvariant();
    }
}
