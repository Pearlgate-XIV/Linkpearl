using Linkpearl.Applets;
using Linkpearl.Geometry;
using Linkpearl.Input;
using Linkpearl.Painting;
using Linkpearl.Theming;

namespace Linkpearl.Device.Shell;

public sealed class RecentsOverlay
{
    private bool open;
    private float slide;
    private bool tracking;
    private bool dragging;
    private bool tossing;
    private bool swept;
    private Vector2 grab;
    private float grabSlide;
    private float toss;
    private string tossingId = string.Empty;

    public bool IsOpen => open;

    public void Open()
    {
        open = true;
        slide = 0f;
        tracking = false;
        dragging = false;
        tossing = false;
        swept = false;
        toss = 0f;
        tossingId = string.Empty;
    }

    public void Close()
    {
        open = false;
        tracking = false;
        dragging = false;
        tossing = false;
    }

    public void Draw(in AppletFrame frame, Rect screen, RouteStack router, IReadOnlyList<IApplet> apps,
        Action<string> resume)
    {
        if (!open)
        {
            return;
        }

        var paint = frame.Paint;
        var text = frame.Text;
        var input = frame.Input;
        var theme = frame.Theme;
        var scale = frame.Scale;
        paint.Fill(screen, theme.Palette.SurfaceSunken with { W = 0.42f });

        var panel = screen.Inset(new Edges(screen.Width * 0.08f, screen.Height * 0.16f, screen.Width * 0.08f,
            screen.Height * 0.18f));
        paint.Fill(panel, theme.Palette.SurfaceRaised with { W = 0.94f }, scale * 18f);
        paint.Stroke(panel, theme.Palette.WarmAccent with { W = 0.22f }, MathF.Max(1f, scale), scale * 18f);

        var tasks = router.Tasks;
        var header = panel.TopSlice(scale * 28f).Inset(new Edges(scale * 12f, scale * 8f, scale * 12f, 0f));
        text.DrawIn(header, "Recents", new TextStyle(FontRole.BodyStrong, theme.Palette.Ink));

        if (tasks.Count == 0)
        {
            text.DrawWrapped(panel.Inset(scale * 16f),
                "Apps you open land here. Swipe a card up to close it, or use Close all.",
                new TextStyle(FontRole.Caption, theme.Palette.InkMuted));
            if (input.ConsumeClick(screen) && !panel.Contains(input.Pointer))
            {
                open = false;
            }

            input.Claim(screen);
            return;
        }

        var footer = panel.BottomSlice(scale * 40f);
        var closeAll = footer.Inset(new Edges(panel.Width * 0.18f, scale * 8f, panel.Width * 0.18f, scale * 8f));
        paint.Fill(closeAll, theme.Palette.SurfaceOverlay, closeAll.Height * 0.5f);
        paint.Stroke(closeAll, theme.Palette.WarmAccent with { W = 0.35f }, MathF.Max(1f, scale),
            closeAll.Height * 0.5f);
        text.DrawIn(closeAll, "Close all",
            new TextStyle(FontRole.CaptionStrong, theme.Palette.Ink, TextAlign.Center));

        var stage = panel.Inset(new Edges(scale * 8f, scale * 32f, scale * 8f, scale * 44f));
        var cardWidth = stage.Width * 0.62f;
        var gap = stage.Width * 0.06f;
        var pitch = cardWidth + gap;
        var maxSlide = MathF.Max(0f, (tasks.Count - 1) * pitch);
        slide = Math.Clamp(slide, 0f, maxSlide);

        if (tossing)
        {
            toss = Math.Clamp(toss + frame.DeltaSeconds / 0.16f, 0f, 1f);
            if (toss >= 1f)
            {
                router.Dismiss(tossingId);
                tossing = false;
                toss = 0f;
                tossingId = string.Empty;
                tasks = router.Tasks;
                if (tasks.Count == 0)
                {
                    open = false;
                    return;
                }

                maxSlide = MathF.Max(0f, (tasks.Count - 1) * pitch);
                slide = Math.Clamp(slide, 0f, maxSlide);
            }
        }

        DriveCarousel(input, tasks, stage, cardWidth, pitch, maxSlide, scale);

        var opened = string.Empty;
        for (var index = tasks.Count - 1; index >= 0; index--)
        {
            var task = tasks[index];
            var x = stage.Min.X + (stage.Width - cardWidth) * 0.5f + index * pitch - slide;
            var card = new Rect(new Vector2(x, stage.Min.Y + scale * 4f),
                new Vector2(x + cardWidth, stage.Max.Y - scale * 2f));
            if (tossing && string.Equals(task.Id, tossingId, StringComparison.Ordinal))
            {
                var lift = toss * stage.Height * 0.7f;
                card = card.Translate(new Vector2(0f, -lift));
            }

            if (card.Max.X < screen.Min.X - 8f || card.Min.X > screen.Max.X + 8f)
            {
                continue;
            }

            DrawCard(frame, card, Find(apps, task.Id), task.Id, router.PlaceOf(task.Id), theme, scale);
            if (!tossing && !swept && opened.Length == 0 && input.ConsumeClick(card))
            {
                opened = task.Id;
            }
        }

        if (opened.Length > 0)
        {
            open = false;
            resume(opened);
            input.Claim(screen);
            return;
        }

        if (!tossing && !dragging && input.ConsumeClick(closeAll))
        {
            router.DismissAll();
            open = false;
            input.Claim(screen);
            return;
        }

        if (!tossing && !swept && input.ConsumeClick(screen) && !panel.Contains(input.Pointer))
        {
            open = false;
        }

        input.Claim(screen);
    }

    private void DriveCarousel(IInputProbe input, IReadOnlyList<RecentTask> tasks, Rect stage, float cardWidth,
        float pitch, float maxSlide, float scale)
    {
        if (tossing)
        {
            return;
        }

        var threshold = MathF.Max(8f, 10f * scale);
        if (!tracking && input.WasPressed(stage))
        {
            tracking = true;
            dragging = false;
            swept = false;
            grab = input.Pointer;
            grabSlide = slide;
        }

        if (tracking && input.IsHeld())
        {
            var delta = input.Pointer - grab;
            if (!dragging && delta.Length() >= threshold)
            {
                dragging = true;
                swept = true;
                input.Claim(stage);
            }

            if (dragging)
            {
                if (MathF.Abs(delta.Y) > MathF.Abs(delta.X) * 1.05f && delta.Y < -threshold)
                {
                    var id = PickCard(tasks, stage, cardWidth, pitch, grab);
                    tossingId = id;
                    tossing = id.Length > 0;
                    toss = 0f;
                    tracking = false;
                    dragging = false;
                    return;
                }

                slide = Math.Clamp(grabSlide - delta.X, 0f, maxSlide);
                input.Claim(stage);
            }

            return;
        }

        if (!tracking)
        {
            var wheel = input.IsHovering(stage) ? input.ScrollDelta : 0f;
            if (MathF.Abs(wheel) > 0.01f)
            {
                slide = Math.Clamp(slide - wheel * pitch * 0.35f, 0f, maxSlide);
            }

            return;
        }

        if (dragging)
        {
            var delta = input.Pointer - grab;
            var fling = MathF.Abs(delta.X) > pitch * 0.18f;
            if (fling)
            {
                slide = Math.Clamp(grabSlide - MathF.Sign(delta.X) * pitch, 0f, maxSlide);
            }

            slide = MathF.Round(slide / MathF.Max(pitch, 1f)) * pitch;
            slide = Math.Clamp(slide, 0f, maxSlide);
            input.Claim(stage);
        }

        tracking = false;
        dragging = false;
    }

    private string PickCard(IReadOnlyList<RecentTask> tasks, Rect stage, float cardWidth, float pitch, Vector2 point)
    {
        for (var index = 0; index < tasks.Count; index++)
        {
            var x = stage.Min.X + (stage.Width - cardWidth) * 0.5f + index * pitch - slide;
            var card = new Rect(new Vector2(x, stage.Min.Y), new Vector2(x + cardWidth, stage.Max.Y));
            if (card.Contains(point))
            {
                return tasks[index].Id;
            }
        }

        return string.Empty;
    }

    private static void DrawCard(in AppletFrame frame, Rect card, IApplet? applet, string id, string place,
        ITheme theme, float scale)
    {
        var radius = scale * 14f;
        frame.Paint.Fill(card, theme.Palette.SurfaceRaised, radius);
        frame.Paint.Stroke(card, theme.Palette.WarmAccent with { W = 0.28f }, MathF.Max(1f, scale), radius);

        var spec = AppShelf.Find(id);
        var name = applet?.Manifest.DisplayNameKey ?? spec?.Name ?? TitleCase(id);
        var markId = applet?.Manifest.Id ?? id;
        var head = card.TopSlice(scale * 30f).Inset(new Edges(scale * 8f, scale * 5f, scale * 8f, scale * 3f));
        var mark = head.LeftSlice(scale * 20f);
        AppMarks.DrawFace(frame.Paint, frame.Textures, frame.Paths, mark, markId, false);
        var title = head.Inset(new Edges(scale * 26f, 0f, 0f, 0f));
        frame.Text.DrawEllipsized(title.TopSlice(scale * 14f), name,
            new TextStyle(FontRole.CaptionStrong, theme.Palette.Ink));
        if (place.Length > 0)
        {
            frame.Text.DrawEllipsized(title.BottomSlice(scale * 11f), TitleCase(place),
                new TextStyle(FontRole.Caption, theme.Palette.InkMuted));
        }

        var body = card.Inset(new Edges(scale * 6f, scale * 32f, scale * 6f, scale * 6f));
        frame.Paint.Fill(body, theme.Palette.SurfaceSunken with { W = 0.92f }, scale * 10f);
        if (applet is null)
        {
            return;
        }

        try
        {
            frame.Paint.PushClip(body);
            applet.Compose(frame.WithContent(body).WithInput(SilentInput.Instance));
        }
        catch
        {
            // A broken preview must not take the carousel down.
        }
        finally
        {
            frame.Paint.PopClip();
        }
    }

    private static string TitleCase(string place)
    {
        if (place.Length == 0)
        {
            return place;
        }

        return char.ToUpperInvariant(place[0]) + place[1..];
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
