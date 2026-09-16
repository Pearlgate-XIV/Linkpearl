using Linkpearl.Destinations;
using Linkpearl.Geometry;
using Linkpearl.Input;
using Linkpearl.Painting;
using Linkpearl.Preferences;
using Linkpearl.Theming;

namespace Linkpearl.Device.Shell;

public readonly record struct DestinationDockResult(
    DestinationTab? Selected, int Section, string TalkId = "");

public sealed class DestinationDock
{
    private const float DrawerWidthFraction = 0.36f;
    private const float RowUnits = 46f;
    private const float RowGapUnits = 10f;
    private const float PadXUnits = 20f;
    private const float PadYUnits = 18f;
    private const float SlideSeconds = 0.22f;

    private static readonly MenuItem[] Items =
    {
        new("nav.messages", DestinationTab.Social, SocialPane.Messages),
        new("nav.you", DestinationTab.You, 0),
        new("nav.explore", DestinationTab.Explore, ExplorePane.ForYou),
        new("nav.settings", DestinationTab.Settings, 0),
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
        var interactive = shown > 0.92f;
        var gold = theme.Palette.WarmAccent;

        if (slide > 0.004f)
        {
            paint.Fill(screen, theme.Palette.SurfaceSunken with { W = 0.28f * shown });
            paint.PushClip(screen);
            paint.Fill(drawer, theme.Palette.SurfaceOverlay with { W = 0.88f * shown });
            paint.Stroke(drawer, gold with { W = 0.32f * shown }, theme.Metrics.Hairline);
            var pressed = DrawMenu(paint, text, input, theme, drawer, scale, current, interactive);
            paint.PopClip();
            if (pressed is { } item)
            {
                expanded = false;
                return new DestinationDockResult(item.Tab, item.Section, item.TalkId);
            }
        }

        if (interactive && input.WasClicked(screen) && !drawer.Contains(input.Cursor))
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

    private static Rect DrawerArea(Rect screen, float scale)
    {
        var width = screen.Width * DrawerWidthFraction;
        var topLimit = screen.Min.Y + scale * 10f;
        var bottomLimit = screen.Max.Y - Height(scale);
        var available = MathF.Max(bottomLimit - topLimit, 0f);
        var compact = (PadYUnits * 2f + RowUnits * Items.Length + RowGapUnits * (Items.Length - 1)) * scale;
        var height = MathF.Min(compact, available);
        var slack = MathF.Max(available - height, 0f);
        return Rect.FromSize(new Vector2(screen.Min.X, topLimit + slack * 0.5f), new Vector2(width, height));
    }

    private static MenuItem? DrawMenu(IPaintSurface paint, ITextPainter text, IInputProbe input, ITheme theme,
        Rect drawer, float scale, DestinationTab current, bool interactive)
    {
        var gold = theme.Palette.WarmAccent;
        var inner = drawer.Inset(new Edges(PadXUnits * scale, PadYUnits * scale));
        var rowHeight = RowUnits * scale;
        var gap = RowGapUnits * scale;
        var markWidth = scale * 32f;
        MenuItem? pressed = null;
        for (var index = 0; index < Items.Length; index++)
        {
            var item = Items[index];
            var top = inner.Min.Y + index * (rowHeight + gap);
            var cell = Rect.FromSize(new Vector2(inner.Min.X, top), new Vector2(inner.Width, rowHeight));
            var isActive = item.Tab == current;
            var hovering = input.IsHovering(cell);
            var ink = theme.Palette.Ink;
            var glyphInk = hovering
                ? ink
                : isActive ? gold : theme.Palette.InkFaint with { W = 0.38f };
            var labelInk = hovering || isActive ? ink : theme.Palette.InkFaint with { W = 0.40f };
            var mark = cell.LeftSlice(markWidth);
            if (isActive && !hovering)
            {
                paint.Glow(mark.Inset(scale * 4f), gold with { W = 0.42f }, scale * 10f, scale * 12f);
                paint.Glow(cell.Inset(new Edges(markWidth, scale * 8f, scale * 12f, scale * 8f)),
                    gold with { W = 0.18f }, scale * 6f, scale * 8f);
            }

            DrawItemMark(paint, mark, item.Tab, glyphInk);
            text.DrawIn(cell.Inset(new Edges(markWidth + scale * 10f, 0f, 0f, 0f)), PhoneLanguages.T(item.Label),
                new TextStyle(FontRole.BodyStrong, labelInk));
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
                DrawBubble(paint, center + new Vector2(-size * 0.18f, -size * 0.12f), size * 0.94f, color, stroke);
                DrawBubble(paint, center + new Vector2(size * 0.22f, size * 0.18f), size * 0.74f, color, stroke);
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

    private readonly record struct MenuItem(string Label, DestinationTab Tab, int Section, string TalkId = "");
}
