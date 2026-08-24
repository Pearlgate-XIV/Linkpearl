using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Windowing;
using Linkpearl.Applets;
using Linkpearl.Canvas.Input;
using Linkpearl.Canvas.Painting;
using Linkpearl.Canvas.Text;
using Linkpearl.Device.Chassis;
using Linkpearl.Device.Shell;
using Linkpearl.Geometry;
using Linkpearl.Painting;
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
    private HandsetForm form = HandsetForm.Pocket;
    private float scaleStep = HandsetSizeCatalog.DefaultStep;

    public HandsetWindow(HandsetShell shell, HandsetFontService fonts, ITheme theme, RouteStack router)
        : base("##LinkpearlHandset",
            ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse |
            ImGuiWindowFlags.NoCollapse)
    {
        this.shell = shell;
        this.fonts = fonts;
        this.theme = theme;
        this.router = router;
        RespectCloseHotkey = false;
    }

    public void SetForm(HandsetForm nextForm) => form = nextForm;

    public void SetScaleStep(float nextStep) => scaleStep = HandsetSizeCatalog.SnapToStep(nextStep);

    public override void PreDraw()
    {
        var size = HandsetSizeCatalog.SizeFor(form, scaleStep) * ImGuiHelpers.GlobalScale;
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

        var scale = ImGuiHelpers.GlobalScale * scaleStep;
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
    }
}
