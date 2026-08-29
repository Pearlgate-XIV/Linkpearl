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
using Linkpearl.Input;
using Linkpearl.Modules;
using Linkpearl.Painting;
using Linkpearl.Platform;
using Linkpearl.Preferences;
using Linkpearl.Theming;

namespace Linkpearl.Device.Windows;

// The one Dalamud Window for the whole handset: draws the chassis frame every pixel itself
// rather than letting ImGui chrome show through, so the device reads as an object, not a panel.
public sealed class HandsetWindow : Window
{
    private const ImGuiWindowFlags ChromeFlags =
        ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse |
        ImGuiWindowFlags.NoCollapse;

    private readonly HandsetShell shell;
    private readonly HandsetFontService fonts;
    private readonly ITheme theme;
    private readonly RouteStack router;
    private readonly HandsetShapePreference shapePreference;
    private readonly ScreenField screenField;
    private readonly ITextField textField;
    private readonly DisplayPreferences display;
    private readonly IGameSession game;
    private readonly Action persistShape;
    private readonly Action<bool> persistOpen;
    private readonly Action<bool> persistMinimized;
    private readonly ITextureSource textures;
    private readonly HostPaths paths;
    private readonly ResizeGrip resizeGrip = new();
    private readonly SideButton powerButton = new(SideEdge.Right);
    private bool wantFold;
    private float fold;
    private bool presencePocket;
    private bool presenceVanish;

    public HandsetWindow(HandsetShell shell, HandsetFontService fonts, ITheme theme, RouteStack router,
        HandsetShapePreference shapePreference, ScreenField screenField, ITextField textField,
        DisplayPreferences display, IGameSession game, ITextureSource textures, HostPaths paths, Action persistShape,
        Action<bool> persistOpen, Action<bool> persistMinimized)
        : base("##LinkpearlHandset", ChromeFlags)
    {
        this.shell = shell;
        this.fonts = fonts;
        this.theme = theme;
        this.router = router;
        this.shapePreference = shapePreference;
        this.screenField = screenField;
        this.textField = textField;
        this.display = display;
        this.game = game;
        this.textures = textures;
        this.paths = paths;
        this.persistShape = persistShape;
        this.persistOpen = persistOpen;
        this.persistMinimized = persistMinimized;
        RespectCloseHotkey = false;
    }

    public bool IsResizing => resizeGrip.IsDragging;

    public bool IsMinimized => wantFold || fold > 0.04f;

    public override void OnOpen()
    {
        ApplyWindowSize();
        persistOpen(true);
    }

    public override void OnClose()
    {
        shapePreference.ScaleStep = HandsetSizeCatalog.SnapToStep(shapePreference.ScaleStep);
        persistShape();
        persistOpen(false);
        persistMinimized(wantFold);
    }

    public void Minimize()
    {
        if (wantFold)
        {
            return;
        }

        wantFold = true;
        textField.Release();
        persistMinimized(true);
    }

    public void Restore()
    {
        if (!wantFold && fold <= 0f)
        {
            return;
        }

        wantFold = false;
        persistMinimized(false);
    }

    public void SnapMinimized()
    {
        wantFold = true;
        fold = 1f;
        textField.Release();
    }

    public override void PreDraw()
    {
        ApplyPresence();
        StepFold(ImGui.GetIO().DeltaTime);
        ApplyWindowSize();
        Flags = ChromeFlags | (shapePreference.PositionLocked ? ImGuiWindowFlags.NoMove : 0);
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

        if (textField is not DalamudTextField fields)
        {
            DrawHandset();
            return;
        }

        fields.BeginFrame();
        try
        {
            DrawHandset();
        }
        finally
        {
            fields.EndFrame();
        }
    }

    private void DrawHandset()
    {
        var deltaSeconds = ImGui.GetIO().DeltaTime;
        var asleep = fold > 0.02f;
        var scale = asleep
            ? ImGuiHelpers.GlobalScale
            : ImGuiHelpers.GlobalScale * shapePreference.ScaleStep * display.LetteringScale;
        var windowRect = new Rect(ImGui.GetWindowPos(), ImGui.GetWindowPos() + ImGui.GetWindowSize());
        var drawList = ImGui.GetWindowDrawList();
        var paint = new DalamudPaintSurface(drawList);
        var text = new DalamudTextPainter(fonts, drawList);
        var input = new DalamudInputProbe();

        var plate = ChassisCatalog.For(shapePreference.Form);
        var skin = asleep
            ? null
            : textures.FromFile(paths.Asset(Path.Combine(ChassisCatalog.Folder, plate.FileName)));
        var usingSkin = skin is { IsReady: true };
        Rect screen;
        float railDepth;
        if (usingSkin)
        {
            screen = plate.ScreenOn(windowRect);
            railDepth = MathF.Max(windowRect.Max.X - screen.Max.X, 1f);
            paint.FillSquircle(plate.BodyOn(windowRect), new Vector4(0.08f, 0.08f, 0.08f, 1f),
                plate.CornerOn(windowRect));
        }
        else
        {
            var chassisMetrics = ChassisMetrics.ForOuterWidth(shapePreference.Finish, windowRect.Width);
            var chassis = ChassisGeometry.Outer(windowRect, chassisMetrics.RailWidth, chassisMetrics);
            screen = chassis.Screen;
            railDepth = MathF.Max(windowRect.Max.X - chassis.Glass.Max.X, 1f);
            paint.FillSquircle(chassis.Body, theme.Palette.SurfaceSunken, chassis.BodyRadius);
            paint.FillSquircle(chassis.Glass, new Vector4(0f, 0f, 0f, 1f), chassis.GlassRadius);
        }

        paint.Fill(screen, new Vector4(0f, 0f, 0f, 1f));
        if (presenceVanish)
        {
            return;
        }

        paint.PushClip(screen);
        screenField.Paint(paint, theme, screen, deltaSeconds);
        var frame = new AppletFrame(screen, paint, text, input, theme, router, textField, scale, deltaSeconds);
        var powerArea = usingSkin ? plate.PowerOn(windowRect) : powerButton.Area(windowRect, railDepth, scale);
        if (asleep)
        {
            shell.DrawMinimized(paint, text, theme, screen, scale);
            paint.PopClip();
            if (!usingSkin)
            {
                powerButton.Draw(paint, windowRect, railDepth, scale, theme.Palette.InkFaint);
            }

            if (WakeFace(screen, !shapePreference.PositionLocked) ||
                HitChassis(powerArea, "##linkpearl-power"))
            {
                Restore();
            }

            return;
        }

        var locked = shapePreference.PositionLocked;
        var showLock = shapePreference.ShowLockTab;
        var lockArea = LockButton.Area(windowRect, screen, scale);
        if (showLock)
        {
            input.Claim(lockArea);
        }

        input.Claim(powerArea);

        shell.Draw(frame, screen);
        paint.PopClip();
        if (skin is { IsReady: true })
        {
            paint.Image(skin, windowRect, Vector4.One);
        }

        if (showLock)
        {
            LockButton.Draw(paint, theme, lockArea, locked);
        }

        if (!usingSkin)
        {
            powerButton.Draw(paint, windowRect, railDepth, scale, theme.Palette.InkFaint);
        }

        var lockClicked = showLock && HitChassis(lockArea, "##linkpearl-lock");
        if (lockClicked)
        {
            locked = !locked;
            shapePreference.PositionLocked = locked;
        }

        if (HitChassis(powerArea, "##linkpearl-power"))
        {
            Minimize();
        }

        if (locked || lockClicked || (showLock && (input.IsHovering(lockArea) || input.IsHovering(powerArea))) ||
            input.IsHovering(powerArea))
        {
            return;
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

    private void StepFold(float deltaSeconds)
    {
        var target = wantFold ? 1f : 0f;
        var delta = MathF.Max(deltaSeconds, 0f) / 0.28f;
        if (fold < target)
        {
            fold = MathF.Min(target, fold + delta);
        }
        else if (fold > target)
        {
            fold = MathF.Max(target, fold - delta);
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

    private void ApplyWindowSize()
    {
        SizeCondition = ImGuiCond.Always;
        var full = HandsetSizeCatalog.SizeFor(shapePreference.Form, shapePreference.ScaleStep) *
            ImGuiHelpers.GlobalScale;
        var face = MinimizedFace.WindowSize * shapePreference.PocketScale * ImGuiHelpers.GlobalScale;
        Size = presenceVanish ? new Vector2(8f, 8f) : Vector2.Lerp(full, face, Ease(fold));
    }

    private void ApplyPresence()
    {
        var cutscene = game.IsInCutscene && !game.IsInGpose;
        var fighting = game.IsInCombat;

        if (cutscene && display.TuckForCutscenes)
        {
            if (!wantFold)
            {
                presencePocket = true;
                Minimize();
            }
        }
        else if (fighting && display.Fight == FightPresence.Pocket)
        {
            if (!wantFold)
            {
                presencePocket = true;
                Minimize();
            }
        }
        else if (presencePocket && !cutscene && !(fighting && display.Fight == FightPresence.Pocket))
        {
            presencePocket = false;
            Restore();
        }

        presenceVanish = fighting && display.Fight == FightPresence.Vanish;
    }

    private static float Ease(float value) => value * value * (3f - 2f * value);

    private static bool WakeFace(Rect screen, bool canDrag)
    {
        if (screen.Width < 1f || screen.Height < 1f)
        {
            return false;
        }

        ImGui.SetCursorScreenPos(screen.Min);
        ImGui.InvisibleButton("##linkpearl-wake", screen.Size);
        if (canDrag && ImGui.IsItemActive() && ImGui.IsMouseDragging(ImGuiMouseButton.Left, 6f))
        {
            ImGui.SetWindowPos(ImGui.GetWindowPos() + ImGui.GetIO().MouseDelta);
            return false;
        }

        return ImGui.IsItemClicked();
    }

    private static bool HitChassis(Rect area, string id)
    {
        if (area.Width < 1f || area.Height < 1f)
        {
            return false;
        }

        ImGui.SetCursorScreenPos(area.Min);
        ImGui.InvisibleButton(id, area.Size);
        return ImGui.IsItemClicked();
    }
}
