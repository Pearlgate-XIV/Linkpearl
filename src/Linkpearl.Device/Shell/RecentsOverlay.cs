using Linkpearl.Applets;
using Linkpearl.Geometry;
using Linkpearl.Input;
using Linkpearl.Painting;
using Linkpearl.Shell;
using Linkpearl.Theming;

namespace Linkpearl.Device.Shell;

public sealed class RecentsOverlay
{
    private bool open;

    public bool IsOpen => open;

    public void Open() => open = true;

    public void Close() => open = false;

    public void Draw(IPaintSurface paint, ITextPainter text, IInputProbe input, ITheme theme, Rect screen, float scale,
        IRouter router, IReadOnlyList<IApplet> apps, IReadOnlyList<string> recentIds)
    {
        if (!open)
        {
            return;
        }

        paint.Fill(screen, theme.Palette.SurfaceSunken with { W = 0.72f });
        var panel = screen.Inset(scale * 16f);
        paint.Fill(panel, theme.Palette.SurfaceRaised, scale * 16f);
        paint.Stroke(panel, theme.Palette.WarmAccent with { W = 0.32f }, MathF.Max(1f, scale), scale * 16f);

        var inner = panel.Inset(scale * 14f);
        text.DrawIn(inner.TopSlice(scale * 24f), "Recents",
            new TextStyle(FontRole.Title, theme.Palette.Ink));
        var close = inner.TopSlice(scale * 24f).RightSlice(scale * 28f);
        text.DrawIn(close, "✕", new TextStyle(FontRole.BodyStrong, theme.Palette.InkMuted, TextAlign.Center));
        if (input.ConsumeClick(close) || (input.WasClicked(screen) && !panel.Contains(input.Pointer)))
        {
            open = false;
            return;
        }

        if (recentIds.Count == 0)
        {
            text.DrawWrapped(inner.Inset(new Edges(0f, scale * 40f, 0f, 0f)).TopSlice(scale * 48f),
                "Open an app from the right-edge drawer to see it here.",
                new TextStyle(FontRole.Caption, theme.Palette.InkMuted));
            return;
        }

        var list = inner.Inset(new Edges(0f, scale * 36f, 0f, 0f));
        var rowHeight = scale * 48f;
        for (var index = 0; index < recentIds.Count; index++)
        {
            var applet = Find(apps, recentIds[index]);
            if (applet is null)
            {
                continue;
            }

            var row = Rect.FromSize(new Vector2(list.Min.X, list.Min.Y + index * (rowHeight + scale * 8f)),
                new Vector2(list.Width, rowHeight));
            paint.Fill(row, theme.Palette.SurfaceOverlay, scale * 12f);
            paint.Stroke(row, theme.Palette.WarmAccent with { W = 0.28f }, MathF.Max(1f, scale), scale * 12f);
            text.DrawIn(row.LeftSlice(scale * 40f), applet.Manifest.Glyph,
                new TextStyle(FontRole.Title, theme.Palette.WarmAccent, TextAlign.Center));
            text.DrawIn(row.Inset(new Edges(scale * 44f, 0f, 0f, 0f)), applet.Manifest.DisplayNameKey,
                new TextStyle(FontRole.BodyStrong, theme.Palette.Ink));
            if (input.ConsumeClick(row))
            {
                open = false;
                router.Open(applet.Manifest.Id);
                return;
            }
        }
    }

    private static IApplet? Find(IReadOnlyList<IApplet> apps, string id)
    {
        for (var index = 0; index < apps.Count; index++)
        {
            if (string.Equals(apps[index].Manifest.Id, id, StringComparison.Ordinal))
            {
                return apps[index];
            }
        }

        return null;
    }
}
