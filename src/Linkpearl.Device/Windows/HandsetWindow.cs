using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Windowing;
using Linkpearl.Applets;
using Linkpearl.Canvas.Input;
using Linkpearl.Canvas.Painting;
using Linkpearl.Canvas.Text;
using Linkpearl.Chassis;
using Linkpearl.Device.Chassis;
using Linkpearl.Device.Shell;
using Linkpearl.Geometry;
using Linkpearl.Painting;
using Linkpearl.Preferences;
using Linkpearl.Theming;

namespace Linkpearl.Device.Windows;

// The one Dalamud Window for the whole handset: draws the chassis frame every pixel itself
// rather than letting ImGui chrome show through, so the device reads as an object, not a panel.
public sealed class HandsetWindow : Window
{
    private readonly HandsetShell shell;
    private readonly HandsetFontService fonts;
    private readonly ITheme theme;
    private readonly RouteStack router;
    private readonly HandsetShapePreference shapePreference;
    private readonly ResizeGrip resizeGrip = new();
    private readonly SideButton powerButton = new(SideEdge.Right);

    public HandsetWindow(HandsetShell shell, HandsetFontService fonts, ITheme theme, RouteStack router,
        HandsetShapePreference shapePreference)
        : base("##LinkpearlHandset",
            ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse |
            ImGuiWindowFlags.NoCollapse)
    {
        this.shell = shell;
        this.fonts = fonts;
        this.theme = theme;
        this.router = router;
        this.shapePreference = shapePreference;
        RespectCloseHotkey = false;
    }

    public override void PreDraw()
    {
        var size = HandsetSizeCatalog.SizeFor(shapePreference.Form, shapePreference.ScaleStep) *
            ImGuiHelpers.GlobalScale;
        SizeCondition = ImGuiCond.Always;
        Size = size;
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, Vector2.Zero);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, 0f);
        ImGui.PushStyleColor(ImGuiCol.WindowBg, Vector4.Zero);
    }

    public override void PostDraw()
    {
        ImGui.PopStyleColor();
        ImGui.PopStyleVar(2);
    }

    public override void Draw()
    {
        if (!fonts.Ready)
        {
            return;
        }

        var scale = ImGuiHelpers.GlobalScale * shapePreference.ScaleStep;
        var windowRect = new Rect(ImGui.GetWindowPos(), ImGui.GetWindowPos() + ImGui.GetWindowSize());
        var chassisMetrics = ChassisMetrics.ForOuterWidth(HandsetFinish.Crystal, windowRect.Width);
        var chassis = ChassisGeometry.Outer(windowRect, chassisMetrics.RailWidth, chassisMetrics);

        var drawList = ImGui.GetWindowDrawList();
        var paint = new DalamudPaintSurface(drawList);
        var text = new DalamudTextPainter(fonts, drawList);
        var input = new DalamudInputProbe();

        paint.FillSquircle(chassis.Body, theme.Palette.SurfaceSunken, chassis.BodyRadius);
        paint.FillSquircle(chassis.Glass, new Vector4(0f, 0f, 0f, 1f), chassis.GlassRadius);
        paint.FillSquircle(chassis.Screen, theme.Palette.Surface, chassis.ScreenRadius);

        paint.PushClip(chassis.Screen);
        var frame = new AppletFrame(chassis.Screen, paint, text, input, theme, router, scale, ImGui.GetIO().DeltaTime);
        shell.Draw(frame, chassis.Screen);
        paint.PopClip();

        var frameColor = new Vector4(0f, 0f, 0f, 1f);
        NotchDetail.Draw(paint, chassis.Screen, scale, frameColor, theme.Palette.SurfaceSunken);

        var railDepth = MathF.Max(windowRect.Max.X - chassis.Glass.Max.X, 1f);
        powerButton.Draw(paint, windowRect, railDepth, scale, theme.Palette.InkFaint);
        if (powerButton.Update(windowRect, railDepth, scale, input))
        {
            IsOpen = false;
        }

        var step = shapePreference.ScaleStep;
        var hoveredCorner = resizeGrip.Update(windowRect, input, scale, ref step);
        shapePreference.ScaleStep = step;
        if (hoveredCorner != ResizeCorner.None)
        {
            ImGui.SetMouseCursor(ResizeGrip.IsDiagonalNwse(hoveredCorner)
                ? ImGuiMouseCursor.ResizeNwse
                : ImGuiMouseCursor.ResizeNesw);
            DrawResizeHint(paint, windowRect, hoveredCorner, scale);
        }
    }

    private static void DrawResizeHint(DalamudPaintSurface paint, Rect window, ResizeCorner corner, float scale)
    {
        const float reachUnits = 7f;
        const float insetUnits = 2f;
        var reach = reachUnits * scale;
        var inset = insetUnits * scale;
        var thickness = MathF.Max(1.4f * scale, 1f);
        var ink = new Vector4(1f, 1f, 1f, 0.55f);

        var (origin, signX, signY) = corner switch
        {
            ResizeCorner.TopLeft => (window.Min + new Vector2(inset, inset), 1f, 1f),
            ResizeCorner.TopRight => (new Vector2(window.Max.X - inset, window.Min.Y + inset), -1f, 1f),
            ResizeCorner.BottomLeft => (new Vector2(window.Min.X + inset, window.Max.Y - inset), 1f, -1f),
            _ => (window.Max - new Vector2(inset, inset), -1f, -1f),
        };

        paint.Line(origin, origin + new Vector2(reach * signX, 0f), ink, thickness);
        paint.Line(origin, origin + new Vector2(0f, reach * signY), ink, thickness);
    }
}
