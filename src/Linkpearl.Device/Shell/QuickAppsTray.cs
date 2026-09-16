using Linkpearl.Applets;
using Linkpearl.Destinations;
using Linkpearl.Geometry;
using Linkpearl.Painting;
using Linkpearl.Preferences;
using Linkpearl.Talk;

namespace Linkpearl.Device.Shell;

public sealed class QuickAppsTray
{
    private bool open;
    private float reveal;

    public bool IsOpen => open || reveal > 0.02f;

    public void Open() => open = true;

    public void Close() => open = false;

    public void Toggle() => open = !open;

    public void Draw(in AppletFrame frame, Rect screen, DisplayPreferences display, bool reduceMotion,
        Action<string> launch, Action customize, NoticeLedger notices, ITalk talk, bool hush)
    {
        var speed = reduceMotion ? 14f : 9f;
        var target = open ? 1f : 0f;
        reveal += (target - reveal) * (1f - MathF.Exp(-speed * MathF.Max(frame.DeltaSeconds, 0f)));
        if (!open && reveal < 0.02f)
        {
            reveal = 0f;
            return;
        }

        var t = reveal * reveal * (3f - 2f * reveal);
        var gold = frame.Theme.Palette.WarmAccent;
        var home = SoftKeyBar.HomeCell(screen, frame.Scale);
        var gap = frame.Units(8f);
        var pad = frame.Units(10f);
        var slot = frame.Units(52f);
        var label = frame.Units(14f);
        var innerW = slot * DisplayPreferences.QuickAppSlots + gap * (DisplayPreferences.QuickAppSlots - 1);
        var width = MathF.Min(screen.Width - frame.Units(24f), innerW + pad * 2f);
        var height = pad + frame.Units(16f) + slot + label + pad;
        var left = screen.Center.X - width * 0.5f;
        var top = home.Min.Y - gap - height + (1f - t) * frame.Units(18f);
        var panel = Rect.FromSize(new Vector2(left, top), new Vector2(width, height));

        frame.Paint.Fill(screen, frame.Theme.Palette.SurfaceSunken with { W = 0.28f * t });
        frame.Paint.Fill(panel, frame.Theme.Palette.SurfaceRaised with { W = 0.94f * t }, frame.Units(14f));
        frame.Paint.Stroke(panel, gold with { W = 0.40f * t }, frame.Theme.Metrics.Hairline, frame.Units(14f));

        if (open && !panel.Contains(frame.Input.Cursor) && !home.Contains(frame.Input.Cursor) &&
            frame.Input.ConsumeClick(screen))
        {
            Close();
            return;
        }

        if (t < 0.12f)
        {
            return;
        }

        var ink = frame.Theme.Palette.Ink with { W = t };
        var muted = frame.Theme.Palette.InkMuted with { W = t };
        var inner = panel.Inset(new Edges(pad, frame.Units(8f), pad, frame.Units(8f)));
        frame.Text.DrawIn(inner.TopSlice(frame.Units(16f)), PhoneLanguages.T("shell.quickapps"),
            new TextStyle(FontRole.CaptionStrong, gold with { W = t }));

        var row = inner.Inset(new Edges(0f, frame.Units(18f), 0f, 0f));
        var cellW = (row.Width - gap * (DisplayPreferences.QuickAppSlots - 1)) / DisplayPreferences.QuickAppSlots;
        for (var index = 0; index < DisplayPreferences.QuickAppSlots; index++)
        {
            var cell = Rect.FromSize(new Vector2(row.Min.X + index * (cellW + gap), row.Min.Y),
                new Vector2(cellW, row.Height));
            var square = Rect.FromSize(
                new Vector2(cell.Center.X - slot * 0.5f, cell.Min.Y),
                new Vector2(slot, slot));
            var id = index < display.QuickApps.Count ? display.QuickApps[index] : string.Empty;
            frame.Paint.Fill(square, frame.Theme.Palette.SurfaceOverlay with { W = 0.72f * t }, frame.Units(10f));
            frame.Paint.Stroke(square, gold with { W = (id.Length > 0 ? 0.38f : 0.22f) * t },
                frame.Theme.Metrics.Hairline, frame.Units(10f));
            if (id.Length > 0)
            {
                AppMarks.DrawFace(frame, square.Inset(frame.Units(6f)), id, frame.Input.IsHovering(square));
                if (!hush)
                {
                    AppMarks.DrawCount(frame, square, notices.AppBadge(id, talk));
                }
                var spec = AppShelf.Find(id);
                frame.Text.DrawEllipsized(cell.BottomSlice(label), spec?.Name ?? id,
                    new TextStyle(FontRole.Caption, ink, TextAlign.Center));
                if (open && frame.Input.ConsumeClick(square))
                {
                    Close();
                    launch(id);
                    return;
                }
            }
            else
            {
                frame.Text.DrawIn(square, "+", new TextStyle(FontRole.Title, muted, TextAlign.Center));
                if (open && frame.Input.ConsumeClick(square))
                {
                    Close();
                    customize();
                    return;
                }
            }
        }
    }
}
