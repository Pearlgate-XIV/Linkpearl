using Linkpearl.Applets;
using Linkpearl.Geometry;
using Linkpearl.Input;
using Linkpearl.Painting;
using Linkpearl.Preferences;
using Linkpearl.Time;

namespace Linkpearl.Device.Shell;

public sealed class RecentsOverlay
{
    private static readonly Vector4 CloseFill = new(0.16f, 0.16f, 0.18f, 0.78f);
    private static readonly Vector4 CloseInk = new(0.96f, 0.96f, 0.97f, 1f);
    private static readonly Vector4 Badge = new(0.12f, 0.12f, 0.14f, 0.92f);

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

    public bool HoldsPointer => open && (tracking || dragging);

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

    public void Draw(in AppletFrame frame, Rect screen, RouteTrail router, IReadOnlyList<IApplet> apps,
        DisplayPreferences display, IClock clock, Action<string> resume)
    {
        if (!open)
        {
            return;
        }

        var paint = frame.Paint;
        var text = frame.Text;
        var input = frame.Input;
        var scale = frame.Scale;
        PlateFrost.Draw(frame, screen, display, clock);

        var nav = SoftKeyBar.Height(scale);
        var appsIcon = scale * 36f;
        var appsHead = scale * 16f;
        var appsH = appsHead + scale * 8f + appsIcon + scale * 8f;
        var icon = scale * 30f;
        var cardW = screen.Width * 0.50f;
        var cardH = MathF.Min(screen.Height * 0.45f, cardW * 2.15f);
        var closeH = scale * 32f;
        var closeW = MathF.Min(screen.Width * 0.42f, scale * 128f);
        var closeGap = scale * 22f;
        var appsGap = scale * 16f;
        var field = new Rect(new Vector2(screen.Min.X, screen.Min.Y + scale * 8f),
            new Vector2(screen.Max.X, screen.Max.Y - nav));
        var stackH = icon + scale * 8f + cardH + closeGap + closeH + appsGap + appsH;
        var stackTop = field.Center.Y - stackH * 0.5f;
        var stage = new Rect(
            new Vector2(screen.Min.X, stackTop + icon + scale * 8f),
            new Vector2(screen.Max.X, stackTop + icon + scale * 8f + cardH));
        var close = Rect.FromSize(
            new Vector2(screen.Center.X - closeW * 0.5f, stage.Max.Y + closeGap),
            new Vector2(closeW, closeH));
        var appsBand = Rect.FromSize(
            new Vector2(screen.Min.X, close.Max.Y + appsGap),
            new Vector2(screen.Width, appsH));
        var tasks = router.Tasks;

        if (tasks.Count == 0)
        {
            text.DrawWrapped(screen.Inset(new Edges(scale * 28f, screen.Height * 0.38f, scale * 28f, nav)),
                "Apps you open land here. Swipe a card up to close it, or use Close all.",
                new TextStyle(FontRole.Caption, CloseInk with { W = 0.82f }));
            if (input.ConsumeClick(screen))
            {
                open = false;
            }

            input.Claim(screen);
            return;
        }

        var pitch = cardW * 0.36f;
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

        var lane = new Rect(screen.Min, new Vector2(screen.Max.X, close.Min.Y - scale * 4f));
        DriveCarousel(input, tasks, lane, stage, cardW, cardH, pitch, maxSlide, scale);

        var focus = slide / MathF.Max(pitch, 1f);
        var live = frame;
        WalkDeck(tasks.Count, focus, backFirst: true, index =>
        {
            var task = tasks[index];
            var card = CardAt(stage, cardW, cardH, pitch, index);
            if (tossing && string.Equals(task.Id, tossingId, StringComparison.Ordinal))
            {
                card = card.Translate(new Vector2(0f, -toss * cardH * 0.85f));
            }

            if (card.Max.X < screen.Min.X - 8f || card.Min.X > screen.Max.X + 8f)
            {
                return;
            }

            DrawCard(live, card, Find(apps, task.Id), task.Id, scale, MathF.Abs(index - focus) < 0.45f);
        });
        var opened = string.Empty;
        if (!tossing && !swept)
        {
            WalkDeck(tasks.Count, focus, backFirst: false, index =>
            {
                if (opened.Length > 0)
                {
                    return;
                }

                var card = CardAt(stage, cardW, cardH, pitch, index);
                if (input.ConsumeClick(card))
                {
                    opened = tasks[index].Id;
                }
            });
        }

        paint.Fill(close, CloseFill, close.Height * 0.5f);
        text.DrawIn(close, "Close all",
            new TextStyle(FontRole.CaptionStrong, CloseInk, TextAlign.Center));
        opened = TakeRecentApp(live, appsBand, tasks, scale, opened.Length == 0 && !tossing, opened);

        if (opened.Length > 0)
        {
            open = false;
            resume(opened);
            input.Claim(screen);
            return;
        }

        if (!tossing && !dragging && input.ConsumeClick(close))
        {
            router.DismissAll();
            open = false;
            input.Claim(screen);
            return;
        }

        if (!tossing && !swept && !appsBand.Contains(input.Cursor) && input.ConsumeClick(screen))
        {
            open = false;
        }

        input.Claim(screen);
    }

    private Rect CardAt(Rect stage, float cardWidth, float cardHeight, float pitch, int index)
    {
        var x = stage.Center.X - cardWidth * 0.5f - index * pitch + slide;
        var y = stage.Center.Y - cardHeight * 0.5f;
        var card = Rect.FromSize(new Vector2(x, y), new Vector2(cardWidth, cardHeight));
        var dist = MathF.Abs(index - slide / MathF.Max(pitch, 1f));
        var zoom = Math.Clamp(1f - dist * 0.07f, 0.90f, 1f);
        if (zoom >= 0.999f)
        {
            return card;
        }

        var size = card.Size * zoom;
        return Rect.FromSize(card.Center - size * 0.5f, size);
    }

    private static void WalkDeck(int count, float focus, bool backFirst, Action<int> visit)
    {
        var mid = Math.Clamp((int)MathF.Round(focus), 0, Math.Max(0, count - 1));
        if (backFirst)
        {
            for (var index = 0; index < mid; index++)
            {
                visit(index);
            }

            for (var index = count - 1; index > mid; index--)
            {
                visit(index);
            }

            visit(mid);
            return;
        }

        visit(mid);
        for (var index = mid + 1; index < count; index++)
        {
            visit(index);
        }

        for (var index = mid - 1; index >= 0; index--)
        {
            visit(index);
        }
    }

    private void DriveCarousel(IInputProbe input, IReadOnlyList<RecentTask> tasks, Rect lane, Rect stage,
        float cardWidth, float cardHeight, float pitch, float maxSlide, float scale)
    {
        if (tossing)
        {
            return;
        }

        if (!input.IsHeld() && !tracking)
        {
            swept = false;
        }

        var threshold = MathF.Max(3f, 3.5f * scale);
        const float gain = 2.6f;
        if (!tracking && input.WasPressed(lane))
        {
            tracking = true;
            dragging = false;
            swept = false;
            grab = input.Cursor;
            grabSlide = slide;
        }

        if (tracking && input.IsHeld())
        {
            var delta = input.Cursor - grab;
            if (!dragging && MathF.Abs(delta.X) >= threshold)
            {
                dragging = true;
                swept = true;
                input.Claim(lane);
            }

            if (dragging)
            {
                if (MathF.Abs(delta.Y) > MathF.Abs(delta.X) * 2.1f && delta.Y < -threshold * 2f)
                {
                    var id = PickCard(tasks, stage, cardWidth, cardHeight, pitch, grab);
                    tossingId = id.Length > 0 ? id : tasks[Math.Clamp((int)MathF.Round(slide / MathF.Max(pitch, 1f)),
                        0, tasks.Count - 1)].Id;
                    tossing = tossingId.Length > 0;
                    toss = 0f;
                    tracking = false;
                    dragging = false;
                    return;
                }

                slide = Math.Clamp(grabSlide + delta.X * gain, 0f, maxSlide);
                input.Claim(lane);
            }

            return;
        }

        if (!tracking)
        {
            var wheel = input.IsHovering(lane) ? input.ScrollDelta : 0f;
            if (MathF.Abs(wheel) > 0.01f)
            {
                slide = Math.Clamp(slide + wheel * pitch * 0.85f, 0f, maxSlide);
                swept = true;
            }

            return;
        }

        if (dragging)
        {
            var coast = input.PointerDelta.X * gain * 4f;
            slide = Math.Clamp(slide + coast, 0f, maxSlide);
            slide = MathF.Round(slide / MathF.Max(pitch, 1f)) * pitch;
            slide = Math.Clamp(slide, 0f, maxSlide);
            input.Claim(lane);
        }

        tracking = false;
        dragging = false;
    }

    private string PickCard(IReadOnlyList<RecentTask> tasks, Rect stage, float cardWidth, float cardHeight,
        float pitch, Vector2 point)
    {
        var focus = slide / MathF.Max(pitch, 1f);
        var id = string.Empty;
        WalkDeck(tasks.Count, focus, backFirst: false, index =>
        {
            if (id.Length == 0 && CardAt(stage, cardWidth, cardHeight, pitch, index).Contains(point))
            {
                id = tasks[index].Id;
            }
        });
        return id;
    }

    private static void DrawBackdrop(IPaintSurface paint, Rect card, float radius, float scale)
    {
        var drop = scale * 2.5f;
        paint.Fill(card.Translate(new Vector2(0f, drop)).Expand(scale * 1.6f),
            new Vector4(0f, 0f, 0f, 0.14f), radius + scale);
        paint.Glow(card, new Vector4(0f, 0f, 0f, 0.20f), radius, scale * 9f);
    }

    private static void DrawCard(in AppletFrame frame, Rect card, IApplet? applet, string id, float scale, bool showIcon)
    {
        var radius = MathF.Min(card.Width, card.Height) * 0.14f;
        DrawBackdrop(frame.Paint, card, radius, scale);
        AppGround.Paint(frame, card, applet?.Manifest.Id ?? id, radius);
        if (applet is not null)
        {
            try
            {
                frame.Paint.PushClip(card);
                applet.Compose(frame.WithContent(card).WithInput(SilentInput.Instance));
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

        if (!showIcon)
        {
            return;
        }

        var icon = scale * 30f;
        var badge = new Vector2(card.Center.X, card.Min.Y - scale * 8f - icon * 0.5f);
        frame.Paint.FillCircle(badge, icon * 0.5f, Badge);
        var mark = Rect.FromSize(badge - new Vector2(icon * 0.34f, icon * 0.34f),
            new Vector2(icon * 0.68f, icon * 0.68f));
        AppMarks.DrawFace(frame.Paint, frame.Textures, frame.Paths, mark, applet?.Manifest.Id ?? id, false);
    }

    private static string TakeRecentApp(in AppletFrame frame, Rect band, IReadOnlyList<RecentTask> tasks, float scale,
        bool allowTap, string opened)
    {
        var head = band.TopSlice(scale * 16f).Inset(new Edges(scale * 16f, 0f));
        frame.Text.DrawIn(head, "Most recent apps",
            new TextStyle(FontRole.Caption, CloseInk with { W = 0.78f }));
        var row = band.Inset(new Edges(scale * 16f, scale * 22f, scale * 16f, scale * 8f));
        var shown = Math.Min(tasks.Count, 5);
        if (shown == 0)
        {
            return opened;
        }

        var cell = MathF.Min(scale * 48f, row.Width / shown);
        var used = cell * shown;
        var left = row.Center.X - used * 0.5f;
        for (var index = 0; index < shown; index++)
        {
            var hit = Rect.FromSize(new Vector2(left + index * cell, row.Min.Y),
                new Vector2(cell, row.Height));
            var hover = frame.Input.IsHovering(hit);
            var side = scale * (hover ? 38f : 36f);
            var mark = Rect.FromSize(
                new Vector2(hit.Center.X - side * 0.5f, hit.Center.Y - side * 0.5f),
                new Vector2(side, side));
            AppMarks.DrawFace(frame.Paint, frame.Textures, frame.Paths, mark, tasks[index].Id, hover);
            if (allowTap && opened.Length == 0 && frame.Input.ConsumeClick(hit))
            {
                opened = tasks[index].Id;
            }
        }

        return opened;
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
