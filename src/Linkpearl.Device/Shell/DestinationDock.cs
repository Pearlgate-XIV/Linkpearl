using Linkpearl.Destinations;
using Linkpearl.Geometry;
using Linkpearl.Input;
using Linkpearl.Painting;
using Linkpearl.Theming;

namespace Linkpearl.Device.Shell;

public readonly record struct DestinationDockResult(
    DestinationTab? Selected, int Section, string TalkId = "");

public sealed class DestinationDock
{
    private const float TriggerWidthUnits = 18f;
    private const float TriggerHeightUnits = 72f;
    private const float DrawerWidthFraction = 0.40f;
    private const float RowUnits = 36f;
    private const float PadUnits = 14f;
    private const float SlideSeconds = 0.22f;

    private static readonly MenuItem[] Items =
    {
        new("Messages", DestinationTab.Social, SocialPane.Messages),
        new("You", DestinationTab.You, 0),
        new("Explore", DestinationTab.Explore, ExplorePane.ForYou),
        new("Settings", DestinationTab.Settings, 0),
    };

    private bool expanded;
    private float slide;

    public bool IsOpen => expanded || slide > 0.004f;

    public static float Height(float scale) => SoftKeyBar.Height(scale);

    public void Close() => expanded = false;

    public void Open() => expanded = true;

    public DestinationDockResult Draw(IPaintSurface paint, ITextPainter text, IInputProbe input, ITheme theme,
        Rect screen, float scale, float deltaSeconds, DestinationTab current, bool reduceMotion)
    {
        if (reduceMotion)
        {
            slide = expanded ? 1f : 0f;
        }
        else
        {
            StepSlide(deltaSeconds);
        }

        var rest = DrawerArea(screen, scale);
        var shown = Ease(slide);
        var drawer = rest.Translate(new Vector2(-(1f - shown) * rest.Width, 0f));
        var trigger = TriggerOnDrawer(drawer, scale);
        var interactive = shown > 0.92f;
        var radius = scale * 14f;

        if (slide > 0.004f)
        {
            paint.Fill(screen, theme.Palette.SurfaceSunken with { W = 0.28f * shown });
            paint.PushClip(screen);
            paint.Fill(drawer, theme.Palette.SurfaceRaised with { W = 1f }, radius, Corner.Right);
            paint.Stroke(drawer, theme.Palette.WarmAccent with { W = 0.32f }, theme.Metrics.Hairline, radius,
                Corner.Right);
            var pressed = DrawMenu(paint, text, input, theme, drawer, scale, current, interactive);
            paint.PopClip();
            if (pressed is { } item)
            {
                expanded = false;
                return new DestinationDockResult(item.Tab, item.Section, item.TalkId);
            }
        }

        var handleFill = theme.Palette.SurfaceOverlay with { W = expanded ? 0.86f : 0.50f };
        var handleInk = (expanded ? theme.Palette.Accent : theme.Palette.Ink) with { W = expanded ? 1f : 0.50f };
        paint.Fill(trigger, handleFill, trigger.Height * 0.5f, Corner.Right);
        DrawDrawerGlyph(paint, trigger, handleInk, expanded);

        if (input.ConsumeClick(trigger))
        {
            expanded = !expanded;
            return new DestinationDockResult(null, 0);
        }

        if (interactive && input.WasClicked(screen) && !drawer.Contains(input.Pointer) &&
            !trigger.Contains(input.Pointer))
        {
            expanded = false;
        }

        return new DestinationDockResult(null, 0);
    }

    private void StepSlide(float deltaSeconds)
    {
        var target = expanded ? 1f : 0f;
        var delta = MathF.Max(deltaSeconds, 0f) / SlideSeconds;
        if (slide < target)
        {
            slide = MathF.Min(target, slide + delta);
        }
        else if (slide > target)
        {
            slide = MathF.Max(target, slide - delta);
        }
    }

    private static float Ease(float value) => value * value * (3f - 2f * value);

    private static Rect TriggerOnDrawer(Rect drawer, float scale)
    {
        var width = TriggerWidthUnits * scale;
        var height = TriggerHeightUnits * scale;
        var origin = new Vector2(drawer.Max.X, drawer.Center.Y - height * 0.5f);
        return Rect.FromSize(origin, new Vector2(width, height));
    }

    private static Rect DrawerArea(Rect screen, float scale)
    {
        var width = screen.Width * DrawerWidthFraction;
        var topLimit = screen.Min.Y + scale * 10f;
        var bottomLimit = screen.Max.Y - Height(scale);
        var available = MathF.Max(bottomLimit - topLimit, 0f);
        var compact = (PadUnits * 2f + RowUnits * Items.Length) * scale;
        var height = MathF.Min(compact, available);
        var slack = MathF.Max(available - height, 0f);
        return Rect.FromSize(new Vector2(screen.Min.X, topLimit + slack * 0.5f), new Vector2(width, height));
    }

    private static MenuItem? DrawMenu(IPaintSurface paint, ITextPainter text, IInputProbe input, ITheme theme,
        Rect drawer, float scale, DestinationTab current, bool interactive)
    {
        var inner = drawer.Inset(new Edges(scale * 14f, PadUnits * scale));
        var rowHeight = inner.Height / Items.Length;
        MenuItem? pressed = null;
        for (var index = 0; index < Items.Length; index++)
        {
            var item = Items[index];
            var top = inner.Min.Y + index * rowHeight;
            var cell = new Rect(new Vector2(inner.Min.X, top), new Vector2(inner.Max.X, top + rowHeight));
            var isActive = item.Tab == current;
            if (isActive)
            {
                paint.Fill(cell.Inset(new Edges(0f, scale * 2f)), theme.Palette.WarmAccent with { W = 0.16f },
                    cell.Height * 0.35f);
            }

            var glyphInk = isActive ? theme.Palette.WarmAccent : theme.Palette.InkFaint;
            var labelInk = isActive ? theme.Palette.Ink : theme.Palette.InkMuted;
            DrawItemMark(paint, cell.LeftSlice(scale * 32f), item.Tab, glyphInk);
            text.DrawIn(cell.Inset(new Edges(scale * 38f, 0f, 0f, 0f)), item.Label,
                new TextStyle(FontRole.CaptionStrong, labelInk));
            if (interactive && input.ConsumeClick(cell))
            {
                pressed = item;
            }
        }

        return pressed;
    }

    private static void DrawItemMark(IPaintSurface paint, Rect area, DestinationTab tab, Vector4 color)
    {
        var size = MathF.Min(area.Width, area.Height) * 0.32f;
        var center = area.Center;
        var stroke = MathF.Max(1.2f, size * 0.18f);
        switch (tab)
        {
            case DestinationTab.Social:
                DrawBubble(paint, center + new Vector2(-size * 0.18f, -size * 0.12f), size * 0.78f, color, stroke);
                DrawBubble(paint, center + new Vector2(size * 0.22f, size * 0.18f), size * 0.62f, color, stroke);
                break;
            case DestinationTab.You:
                paint.StrokeCircle(center + new Vector2(0f, -size * 0.28f), size * 0.32f, color, stroke);
                paint.Stroke(Rect.FromSize(center + new Vector2(-size * 0.58f, size * 0.12f),
                    new Vector2(size * 1.16f, size * 0.62f)), color, stroke, size * 0.58f);
                break;
            case DestinationTab.Explore:
                Span<Vector2> star = stackalloc Vector2[8]
                {
                    center + new Vector2(0f, -size * 0.92f),
                    center + new Vector2(size * 0.22f, -size * 0.22f),
                    center + new Vector2(size * 0.92f, 0f),
                    center + new Vector2(size * 0.22f, size * 0.22f),
                    center + new Vector2(0f, size * 0.92f),
                    center + new Vector2(-size * 0.22f, size * 0.22f),
                    center + new Vector2(-size * 0.92f, 0f),
                    center + new Vector2(-size * 0.22f, -size * 0.22f),
                };
                paint.Polyline(star, color, stroke, closed: true);
                break;
            case DestinationTab.Settings:
                paint.StrokeCircle(center, size * 0.42f, color, stroke);
                paint.StrokeCircle(center, size * 0.16f, color, stroke);
                for (var tooth = 0; tooth < 6; tooth++)
                {
                    var angle = tooth * MathF.PI / 3f;
                    var dir = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
                    paint.Line(center + dir * size * 0.58f, center + dir * size * 0.88f, color, stroke);
                }

                break;
        }
    }

    private static void DrawBubble(IPaintSurface paint, Vector2 center, float size, Vector4 color, float stroke)
    {
        paint.Stroke(Rect.FromSize(center - new Vector2(size, size * 0.7f), new Vector2(size * 2f, size * 1.4f)),
            color, stroke, size * 0.55f);
    }

    private static void DrawDrawerGlyph(IPaintSurface paint, Rect trigger, Vector4 color, bool expanded)
    {
        var center = trigger.Center;
        var wing = trigger.Width * 0.16f;
        var rise = trigger.Height * 0.14f;
        var thickness = MathF.Max(1.4f, trigger.Height * 0.05f);
        var dir = expanded ? -1f : 1f;
        var tip = new Vector2(center.X + wing * dir, center.Y);
        var top = new Vector2(center.X - wing * dir, center.Y - rise);
        var bottom = new Vector2(center.X - wing * dir, center.Y + rise);
        paint.Line(top, tip, color, thickness);
        paint.Line(bottom, tip, color, thickness);
    }

    private readonly record struct MenuItem(string Label, DestinationTab Tab, int Section, string TalkId = "");
}
