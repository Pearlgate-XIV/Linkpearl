using Linkpearl.Geometry;
using Linkpearl.Input;
using Linkpearl.Painting;
using Linkpearl.Preferences;
using Linkpearl.Theming;

namespace Linkpearl.Device.Shell;

// Horizontal glass pager. Extra home pages sit to the left of Studio. Apps sit to the right.
// Destinations take the middle lane only while a tab is open.
public sealed class AppsDock
{
    public const int ExtraCap = 5;

    private const float SlideSeconds = 0.28f;
    private const float SwipeCommit = 0.28f;

    private int extras;
    private int appScreens = 1;
    private int page = -1;
    private int rest = -1;
    private float slide = -1f;
    private bool destLane;
    private bool tracking;
    private bool dragging;
    private Vector2 grabOrigin;
    private float grabSlide;

    public bool OnApps => page >= FirstAppsPage;

    public bool OnStudio => page == StudioPage;

    public bool OnExtra => !destLane && page >= 0 && page < extras;

    public int ExtraIndex => OnExtra ? page : -1;

    public int ExtraCount => extras;

    public int StudioPage => destLane ? -1 : extras;

    public int FirstAppsPage => destLane ? 1 : extras + 1;

    public int AppsPage => FirstAppsPage;

    public int AppScreenIndex => OnApps ? page - FirstAppsPage : 0;

    public int AppScreenCount => destLane ? 1 : Math.Max(1, appScreens);

    public bool DestLane => destLane;

    public bool IsOpen => OnApps || MathF.Abs(slide - page) > 0.004f;

    public bool IsDragging => dragging;

    public bool IsPaging => dragging || MathF.Abs(slide - page) > 0.02f;

    public float Slide => destLane ? EaseSigned(slide) : slide;

    public float Shift => OnApps || slide > (destLane ? 0.5f : extras + 0.5f) ? 1f : 0f;

    public void ShowStudio()
    {
        rest = StudioPage;
        page = StudioPage;
        slide = page;
    }

    public void SnapStudio()
    {
        rest = StudioPage;
        page = StudioPage;
        slide = page;
    }

    public void SetExtraScreens(int count)
    {
        extras = Math.Clamp(count, 0, ExtraCap);
        if (!destLane)
        {
            var next = Math.Clamp(page, MinPage, MaxPage);
            if (next != page)
            {
                page = next;
                slide = page;
            }

            slide = Scalar.Clamp(slide, MinPage, MaxPage);
        }
    }

    public void SetAppScreens(int count)
    {
        appScreens = Math.Clamp(count, 1, DisplayPreferences.AppScreenCap);
        if (!destLane)
        {
            var next = Math.Clamp(page, MinPage, MaxPage);
            if (next != page)
            {
                page = next;
                slide = page;
            }

            slide = Scalar.Clamp(slide, MinPage, MaxPage);
        }
    }

    public void SetDestLane(bool open)
    {
        if (destLane == open)
        {
            return;
        }

        var wantApps = OnApps;
        destLane = open;
        if (wantApps)
        {
            page = AppsPage;
            rest = page;
            slide = page;
            return;
        }

        page = StudioPage;
        rest = page;
        slide = page;
    }

    public void ShowDestinations()
    {
        destLane = true;
        rest = 0;
        page = 0;
        slide = 0f;
    }

    public void ShowExtra(int index)
    {
        if (destLane || extras == 0)
        {
            return;
        }

        page = Math.Clamp(index, 0, extras - 1);
        rest = page;
        slide = page;
    }

    public void SyncToPage()
    {
        tracking = false;
        dragging = false;
        slide = page;
        rest = page;
    }

    public void Close() => ShowStudio();

    public void Open()
    {
        if (!OnApps)
        {
            rest = page;
        }

        page = AppsPage;
        slide = page;
    }

    public void CoverWithApp()
    {
        rest = page;
        if (!OnApps)
        {
            page = AppsPage;
        }
    }

    public void Retreat()
    {
        page = rest;
    }

    public void ShowAppScreen(int index)
    {
        page = FirstAppsPage + Math.Clamp(index, 0, Math.Max(0, AppScreenCount - 1));
    }

    public bool Step(int delta)
    {
        if (delta == 0)
        {
            return false;
        }

        var next = page + delta;
        if (destLane)
        {
            next = Math.Clamp(next, -1, 1);
        }
        else
        {
            next = Math.Clamp(next, MinPage, MaxPage);
        }

        if (next == page)
        {
            return false;
        }

        rest = page;
        page = next;
        return true;
    }

    public bool CaptureSwipe(IInputProbe input, Rect glass, float scale, bool allow)
    {
        if (!allow)
        {
            if (dragging)
            {
                slide = page;
            }

            tracking = false;
            dragging = false;
            return false;
        }

        var pageWidth = MathF.Max(glass.Width, 1f);
        var threshold = MathF.Max(10f, 14f * scale);
        var span = destLane ? 1f : MathF.Max(1f, extras + 1);

        if (!tracking && !input.PointerClaimed() && input.WasPressed(glass))
        {
            tracking = true;
            dragging = false;
            grabOrigin = input.Pointer;
            grabSlide = slide;
        }

        if (tracking && input.IsHeld())
        {
            var delta = input.Pointer - grabOrigin;
            if (!dragging && MathF.Abs(delta.X) >= threshold && MathF.Abs(delta.X) > MathF.Abs(delta.Y) * 1.15f)
            {
                dragging = true;
                input.Claim(glass);
            }

            if (dragging)
            {
                slide = Scalar.Clamp(grabSlide - delta.X / pageWidth * span, MinPage, MaxPage);
                input.Claim(glass);
            }

            return false;
        }

        if (!tracking)
        {
            return false;
        }

        var committed = dragging;
        if (dragging)
        {
            page = CommitPage(grabSlide, slide);
            input.Claim(glass);
        }

        tracking = false;
        dragging = false;
        return committed;
    }

    public bool ConsumeHandle(IInputProbe input, Rect screen, float scale)
    {
        if (input.ConsumeClick(EdgeHandles.Left(screen, scale)))
        {
            return Step(-1);
        }

        if (input.ConsumeClick(EdgeHandles.Right(screen, scale)))
        {
            return Step(1);
        }

        return false;
    }

    public void Advance(float deltaSeconds, bool reduceMotion)
    {
        if (dragging || reduceMotion)
        {
            if (!dragging && reduceMotion)
            {
                slide = page;
            }

            return;
        }

        StepSlide(deltaSeconds);
    }

    public void DrawHandle(IPaintSurface paint, IInputProbe input, ITheme theme, Rect screen, float scale)
    {
        DrawSide(paint, input, theme, EdgeHandles.Left(screen, scale), Corner.Right, pointLeft: true);
        DrawSide(paint, input, theme, EdgeHandles.Right(screen, scale), Corner.Left, pointLeft: false);
    }

    private static void DrawSide(IPaintSurface paint, IInputProbe input, ITheme theme, Rect trigger, Corner corner,
        bool pointLeft)
    {
        var hot = input.IsHovering(trigger);
        var handleFill = theme.Palette.SurfaceOverlay with { W = hot ? 0.78f : 0.22f };
        var handleInk = theme.Palette.Ink with { W = hot ? 1f : 0.28f };
        paint.Fill(trigger, handleFill, trigger.Height * 0.5f, corner);
        DrawChevron(paint, trigger, handleInk, pointLeft);
    }

    public Rect PageArea(Rect content, int packedPage)
    {
        var shown = destLane ? EaseSigned(slide) : slide;
        return content.Translate(new Vector2((packedPage - shown) * content.Width, 0f));
    }

    private int MinPage => destLane ? -1 : 0;

    private int MaxPage => destLane ? 1 : extras + AppScreenCount;

    private void StepSlide(float deltaSeconds)
    {
        var target = (float)page;
        var span = destLane ? 1f : MathF.Max(1f, extras + 1);
        var delta = MathF.Max(deltaSeconds, 0f) / SlideSeconds * span;
        if (slide < target)
        {
            slide = MathF.Min(target, slide + delta);
        }
        else if (slide > target)
        {
            slide = MathF.Max(target, slide - delta);
        }
    }

    private int CommitPage(float from, float to)
    {
        var commit = destLane ? SwipeCommit : SwipeCommit;
        int next;
        if (to - from >= commit)
        {
            next = (int)MathF.Round(from) + 1;
        }
        else if (from - to >= commit)
        {
            next = (int)MathF.Round(from) - 1;
        }
        else
        {
            next = (int)MathF.Round(to);
        }

        return Math.Clamp(next, MinPage, MaxPage);
    }

    private static float Ease(float value) => value * value * (3f - 2f * value);

    private static float EaseSigned(float value)
    {
        var sign = MathF.Sign(value);
        return sign * Ease(MathF.Abs(value));
    }

    private static void DrawChevron(IPaintSurface paint, Rect trigger, Vector4 color, bool pointLeft)
    {
        var center = trigger.Center;
        var wing = trigger.Width * 0.16f;
        var rise = trigger.Height * 0.14f;
        var thickness = MathF.Max(1.4f, trigger.Height * 0.05f);
        var dir = pointLeft ? -1f : 1f;
        var tip = new Vector2(center.X + wing * dir, center.Y);
        var top = new Vector2(center.X - wing * dir, center.Y - rise);
        var bottom = new Vector2(center.X - wing * dir, center.Y + rise);
        paint.Line(top, tip, color, thickness);
        paint.Line(bottom, tip, color, thickness);
    }
}
