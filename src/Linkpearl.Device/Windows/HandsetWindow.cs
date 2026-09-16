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
        ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoResize;

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
    private readonly Action persistPlacement;
    private readonly Action powerOffPlugin;
    private readonly HandsetPlacement placement;
    private readonly ITextureSource textures;
    private readonly HostPaths paths;
    private readonly ResizeGrip resizeGrip = new();
    private readonly SideButton powerButton = new(SideEdge.Right);
    private readonly SideButton volumeButton = new(SideEdge.Right, 0.16f, 42f);
    private readonly PocketUnlock pocketUnlock = new();
    private readonly HandsetBoot boot = new();
    private bool wantFold;
    private float fold;
    private bool presencePocket;
    private bool presenceVanish;
    private bool pocketMoving;
    private Vector2 pocketGrab;
    private bool placedOnce;
    private bool placeOnce;
    private bool savePlacement;
    private Rect lastOuter;
    private Rect lastShell;
    private Rect lastScreen;
    private float lastOuterRadius;
    private float lastGripScale = 1f;
    private bool holdSide;
    private float volumeCue;

    public HandsetWindow(HandsetShell shell, HandsetFontService fonts, ITheme theme, RouteStack router,
        HandsetShapePreference shapePreference, ScreenField screenField, ITextField textField,
        DisplayPreferences display, IGameSession game, ITextureSource textures, HostPaths paths,         Action persistShape,
        Action<bool> persistOpen, Action<bool> persistMinimized, HandsetPlacement placement, Action persistPlacement,
        Action powerOffPlugin)
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
        this.placement = placement;
        this.persistPlacement = persistPlacement;
        this.powerOffPlugin = powerOffPlugin;
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
        persistShape();
        persistOpen(false);
        persistMinimized(wantFold);
        try
        {
            persistPlacement();
        }
        catch
        {
            // Closing must not take the process down.
        }
    }

    public void Minimize()
    {
        if (wantFold)
        {
            return;
        }

        EnsurePocketSeed();
        wantFold = true;
        pocketUnlock.Reset();
        pocketMoving = false;
        textField.Release();
        boot.Cancel();
        persistMinimized(true);
        savePlacement = true;
    }

    public void PlayBoot() => boot.Play(display.ReduceMotion);

    public void Restore()
    {
        if (!wantFold && fold <= 0f)
        {
            return;
        }

        wantFold = false;
        pocketUnlock.Reset();
        pocketMoving = false;
        persistMinimized(false);
        savePlacement = true;
    }

    public void SnapMinimized()
    {
        wantFold = true;
        fold = 1f;
        pocketUnlock.Reset();
        pocketMoving = false;
        textField.Release();
        boot.Cancel();
    }

    public override void PreDraw()
    {
        if (savePlacement)
        {
            try
            {
                persistPlacement();
            }
            catch
            {
                // Config write must not take the game down.
            }

            savePlacement = false;
        }

        ApplyPresence();
        var priorFold = fold;
        StepFold(ImGui.GetIO().DeltaTime);
        if (priorFold < 0.98f && fold >= 0.98f || priorFold > 0.02f && fold <= 0.02f)
        {
            placeOnce = true;
        }

        ApplyWindowSize();
        if (resizeGrip.IsDragging)
        {
            ImGui.SetNextWindowPos(
                ResizeGrip.PosFromAnchor(resizeGrip.ActiveCorner, resizeGrip.Anchor, Size ?? FullSize()),
                ImGuiCond.Always);
        }
        else if (pocketMoving)
        {
            ImGui.SetNextWindowPos(
                HandsetPlacement.Clamp(ImGui.GetMousePos() - pocketGrab, Size ?? FaceSize()),
                ImGuiCond.Always);
        }
        else if (resizeGrip.Holding && lastOuter.Width > 16f)
        {
            ImGui.SetNextWindowPos(lastOuter.Min, ImGuiCond.Always);
        }
        else
        {
            ApplyWindowPos();
        }
        var pointer = ImGui.GetMousePos();
        var overScreen = lastScreen.Width > 16f && lastScreen.Contains(pointer);
        var roundCorners = shapePreference.Case == HandsetCase.Android;
        var board = lastShell.IsEmpty ? lastOuter : lastShell;
        var overCorner = board.Width > 16f &&
            ResizeGrip.Hits(board, lastScreen, lastGripScale, lastOuterRadius, roundCorners, pointer);
        Flags = ChromeFlags | ImGuiWindowFlags.NoBackground |
            (shapePreference.PositionLocked || wantFold || fold > 0.02f || resizeGrip.Holding || overCorner ||
                shell.HoldsWindow || holdSide || overScreen
                ? ImGuiWindowFlags.NoMove
                : 0);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, Vector2.Zero);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, 0f);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, CaseRounding());
        ImGui.PushStyleColor(ImGuiCol.WindowBg, Vector4.Zero);
    }

    public override void PostDraw()
    {
        ImGui.PopStyleColor();
        ImGui.PopStyleVar(3);
    }

    private float CaseRounding()
    {
        var plate = ChassisCatalog.For(shapePreference.Form, shapePreference.Case);
        var size = fold > 0.5f ? FaceSize() : FullSize();
        var radius = size.X * plate.Corner;
        if (shapePreference.Case == HandsetCase.Android)
        {
            radius += size.X * plate.BodyRight;
        }

        return radius;
    }

    public override void Draw()
    {
        try
        {
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
        catch
        {
            // A bad frame must not take the process down.
        }
    }

    private void DrawHandset()
    {
        RememberLivePlacement();
        var deltaSeconds = ImGui.GetIO().DeltaTime;
        var asleep = fold > 0.02f;
        var gripScale = MathF.Max(0.75f, ImGuiHelpers.GlobalScale);
        var windowRect = new Rect(ImGui.GetWindowPos(), ImGui.GetWindowPos() + ImGui.GetWindowSize());
        if (windowRect.Width < 16f || windowRect.Height < 16f)
        {
            return;
        }

        var drawList = ImGui.GetWindowDrawList();
        var paint = new DalamudPaintSurface(drawList);
        var text = new DalamudTextPainter(fonts, drawList);
        var blocked = DalamudInputProbe.OtherWindowAbove();
        var input = new DalamudInputProbe { Live = !blocked };

        var plate = ChassisCatalog.For(shapePreference.Form, shapePreference.Case);
        lastOuter = windowRect;
        lastShell = plate.BodyOn(windowRect);
        if (lastShell.IsEmpty)
        {
            lastShell = windowRect;
        }

        lastOuterRadius = plate.CornerOn(windowRect);
        var skin = textures.FromFile(paths.Asset(Path.Combine(ChassisCatalog.Folder, plate.FileName)));
        var usingSkin = skin is { IsReady: true } && !(display.Landscape && !asleep);
        Rect screen;
        Rect body;
        float screenRadius;
        float railDepth;
        if (usingSkin)
        {
            var android = shapePreference.Case == HandsetCase.Android;
            body = plate.BodyOn(windowRect);
            var overlap = android ? 0f : MathF.Max(1.6f, windowRect.Width * 0.010f);
            screen = plate.GlassOn(windowRect);
            screenRadius = plate.GlassRadiusOn(windowRect);
            var under = plate.ScreenOn(windowRect).Expand(android ? 0f : overlap);
            var hole = plate.ScreenOn(windowRect).Expand(overlap);
            var underRadius = plate.ScreenRadiusOn(windowRect) + (android ? 0f : overlap);
            var holeRadius = android ? plate.ScreenRadiusOn(windowRect) + overlap : 0f;
            if (!android)
            {
                screenRadius = 0f;
                underRadius = 0f;
            }

            railDepth = MathF.Max(windowRect.Max.X - screen.Max.X, 1f);
            var ink = new Vector4(0f, 0f, 0f, 1f);
            paint.FillSquircle(under, ink, underRadius);
            if (skin is { IsReady: true } && skin.Handle != 0)
            {
                CaseWash.StampSkin(paint, skin, windowRect, android ? 0f : plate.CornerOn(windowRect),
                    tint: android ? Vector4.One : asleep ? new Vector4(1.42f, 1.42f, 1.46f, 1f) : null);
            }

            paint.FillSquircle(hole, ink, holeRadius);
        }
        else
        {
            var chassisMetrics = ChassisMetrics.ForOuterWidth(shapePreference.Finish, windowRect.Width);
            var chassis = ChassisGeometry.Outer(windowRect, chassisMetrics.RailWidth, chassisMetrics);
            body = chassis.Body;
            screen = chassis.Screen;
            screenRadius = chassis.ScreenRadius;
            railDepth = MathF.Max(windowRect.Max.X - chassis.Glass.Max.X, 1f);
            if (shapePreference.Case == HandsetCase.Android)
            {
                CaseWash.Android(paint, body, screen, plate.VolumeOn(windowRect), plate.PowerOn(windowRect),
                    chassis.BodyRadius, screenRadius);
            }
            else
            {
                CaseWash.Body(paint, body, chassis.BodyRadius, theme.Palette.SurfaceSunken, theme.Palette.WarmAccent);
            }

            paint.FillSquircle(chassis.Glass, new Vector4(0f, 0f, 0f, 1f),
                shapePreference.Case == HandsetCase.Android ? chassis.GlassRadius : 0f);
        }

        if (shapePreference.Case != HandsetCase.Android)
        {
            screenRadius = 0f;
        }

        lastShell = body.IsEmpty ? windowRect : body;
        lastScreen = screen.IsEmpty ? lastShell : screen;
        lastOuterRadius = plate.CornerOn(windowRect);
        lastGripScale = gripScale;
        var scale = asleep
            ? gripScale
            : MathF.Max(0.25f, screen.Height / 800f) * display.LetteringScale;
        paint.FillSquircle(screen, new Vector4(0f, 0f, 0f, 1f), screenRadius);
        if (presenceVanish || screen.IsEmpty)
        {
            DrawCase(paint, skin, windowRect, body, screen, screenRadius, plate.CornerOn(windowRect),
                plate.GasketOn(windowRect), asleep);
            return;
        }

        var clipped = false;
        var woke = false;
        var pocketNow = false;
        var asleepNow = asleep;
        try
        {
            paint.PushClip(screen);
            clipped = true;
            if (boot.Covering)
            {
                boot.PaintVeil(paint, screen);
            }
            else
            {
                screenField.Paint(paint, theme, screen, deltaSeconds, screenRadius);
            }

            var powerArea = usingSkin ? plate.PowerOn(windowRect) : powerButton.Area(windowRect, railDepth, scale);
            var powerHit = CasePowerHit(powerArea, screen, scale);
            var volumeArea = usingSkin ? plate.VolumeOn(windowRect) : volumeButton.Area(windowRect, railDepth, scale);
            var volumeHit = CaseSideHit(volumeArea, screen, windowRect, scale);
            var volumeUp = volumeHit.TopSlice(volumeHit.Height * 0.5f);
            var volumeDown = volumeHit.BottomSlice(volumeHit.Height * 0.5f);
            var showLock = !asleep && shapePreference.ShowLockTab;
            var lockArea = LockButton.Area(windowRect, screen, scale);
            var lockHit = showLock ? LockButton.HitArea(windowRect, screen, scale) : Rect.Empty;
            var pointer = input.Pointer;
            holdSide = volumeHit.Contains(pointer) || powerHit.Contains(pointer);
            var overChrome = powerHit.Contains(pointer) || volumeHit.Contains(pointer) || lockHit.Contains(pointer);
            IInputProbe frameInput = overChrome || boot.Covering ? SilentInput.Instance : input;
            var frame = new AppletFrame(screen, paint, text, frameInput, theme, router, textField, textures, paths,
                scale, deltaSeconds);
            textField.Dress(paint, text, textures, paths);
            if (asleep)
            {
                var dip = ImGuiHelpers.GlobalScale;
                var hasNotice = shell.PocketNoticeCount() > 0;
                var sliderHit = PocketUnlock.HitOn(screen, dip, hasNotice);
                var noticeHit = MinimizedFace.NoticeOn(screen, dip, hasNotice);
                if (!blocked && !pocketMoving && !pocketUnlock.IsDragging &&
                    ImGui.IsMouseClicked(ImGuiMouseButton.Left))
                {
                    if (!sliderHit.Contains(pointer) && !noticeHit.Contains(pointer) &&
                        !powerHit.Contains(pointer) && !volumeHit.Contains(pointer) &&
                        windowRect.Contains(pointer))
                    {
                        pocketGrab = pointer - windowRect.Min;
                        pocketMoving = true;
                    }
                }

                if (fonts.Ready)
                {
                    woke = shell.DrawMinimized(frame, screen, pocketUnlock,
                        allowSlide: !pocketMoving && !overChrome);
                }
            }
            else
            {
                if (fonts.Ready && !boot.Covering)
                {
                    try
                    {
                        shell.Draw(frame, screen);
                    }
                    catch
                    {
                        // Keep the chassis, gasket, and soft keys if a page throws.
                    }
                }

                if (boot.Active)
                {
                    boot.Draw(frame, screen);
                }

                pocketNow = shell.ConsumePocket();
                if (shell.ConsumePowerOff())
                {
                    persistOpen(true);
                    persistMinimized(false);
                    powerOffPlugin();
                    return;
                }
                if (display.Brightness < 0.995f && !boot.Active)
                {
                    paint.Fill(screen, new Vector4(0f, 0f, 0f, 1f - display.Brightness));
                }
            }

            paint.FillOutsideRound(screen, screenRadius, new Vector4(0f, 0f, 0f, 1f));
            paint.PopClip();
            clipped = false;
            DrawCase(paint, skin, windowRect, body, screen, screenRadius, plate.CornerOn(windowRect),
                plate.GasketOn(windowRect), asleep);
            if (!usingSkin)
            {
                volumeButton.Draw(paint, windowRect, railDepth, scale, theme.Palette.InkFaint);
                powerButton.Draw(paint, windowRect, railDepth, scale, theme.Palette.InkFaint);
            }

            DrawVolumeCue(paint, screen, scale);
            if (asleep)
            {
                if (woke)
                {
                    Restore();
                    return;
                }

                if (!ImGui.IsMouseDown(ImGuiMouseButton.Left))
                {
                    if (pocketMoving)
                    {
                        CapturePocketFromWindow();
                        savePlacement = true;
                    }

                    pocketMoving = false;
                }

                if (ChromeClicked(powerHit))
                {
                    Restore();
                }

                NudgeVolume(volumeUp, volumeDown);
                return;
            }

            var locked = shapePreference.PositionLocked;
            if (showLock)
            {
                LockButton.Draw(paint, theme, lockArea, locked);
            }

            var lockClicked = showLock && ChromeClicked(lockHit);
            if (lockClicked)
            {
                locked = !locked;
                shapePreference.PositionLocked = locked;
            }

            if (NudgeVolume(volumeUp, volumeDown))
            {
                return;
            }

            if (ChromeClicked(powerHit) || pocketNow)
            {
                Minimize();
                return;
            }

            if (!locked && !lockClicked && !resizeGrip.Holding && fold <= 0.02f)
            {
                CaptureOpenFromWindow();
                if (!ImGui.IsMouseDown(ImGuiMouseButton.Left) && placement.Dirty)
                {
                    savePlacement = true;
                }
            }

            if (lockClicked || overChrome)
            {
                return;
            }
        }
        finally
        {
            if (clipped)
            {
                paint.PopClip();
            }

            TickResize(paint, windowRect, input, gripScale, asleepNow);
        }
    }

    private void TickResize(DalamudPaintSurface paint, Rect windowRect, IInputProbe input, float gripScale,
        bool asleep)
    {
        if (asleep || fold > 0.02f)
        {
            resizeGrip.Cancel();
            return;
        }

        var caseRadius = lastOuterRadius;
        var roundCorners = shapePreference.Case == HandsetCase.Android;
        var step = shapePreference.ScaleStep;
        var hovered = resizeGrip.Update(windowRect, lastShell, lastScreen, input, gripScale, caseRadius, roundCorners,
            HandsetSizeCatalog.FloorScale, HandsetSizeCatalog.FreeCeiling, ref step, true);
        if (resizeGrip.IsDragging)
        {
            step = HandsetSizeCatalog.ClampFree(step, MaxScaleOnScreen(resizeGrip.ActiveCorner));
            shapePreference.ScaleStep = step;
        }

        if (resizeGrip.JustReleased)
        {
            persistShape();
            CaptureOpenFromWindow();
            savePlacement = true;
        }

        DrawResizeSliders(paint, lastShell, hovered, gripScale, caseRadius, roundCorners);
        if (hovered != ResizeCorner.None)
        {
            ImGui.SetMouseCursor(ResizeGrip.IsDiagonalNwse(hovered)
                ? ImGuiMouseCursor.ResizeNwse
                : ImGuiMouseCursor.ResizeNesw);
        }
    }

    private static void DrawCase(IPaintSurface paint, ITextureHandle? skin, Rect window, Rect body, Rect screen,
        float screenRadius, float caseRadius, float gasket, bool pocket)
    {
        if (skin is not { IsReady: true })
        {
            CaseWash.Sheen(paint, body, screen, caseRadius);
        }

        DrawScreenGasket(paint, window, screen, screenRadius, gasket, pocket);
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

    private static void DrawScreenGasket(IPaintSurface paint, Rect window, Rect screen, float radius, float gasket,
        bool pocket)
    {
        if (screen.IsEmpty)
        {
            return;
        }

        // ImGui insets an AA stroke 0.5px from Max, which leaves a hairline on the right and
        // bottom. Grow those two edges so the rim meets the case.
        var rim = new Rect(screen.Min, screen.Max + new Vector2(1f, 1f));
        if (gasket > 0.5f)
        {
            // A 4px black rim eats the whole pocket bezel. Keep the skin visible.
            paint.Stroke(rim, new Vector4(0f, 0f, 0f, 1f), pocket ? 1.4f : 4f, radius);
            return;
        }

        var gap = MathF.Min(screen.Min.X - window.Min.X, screen.Min.Y - window.Min.Y);
        var thickness = !float.IsFinite(gap) || gap <= 0f
            ? 1.084f
            : Math.Clamp(gap * 0.152f, 0.8f, 1.987f);
        paint.Stroke(rim, new Vector4(0f, 0f, 0f, 1f), thickness, 0f);
    }

    private static void DrawResizeSliders(DalamudPaintSurface paint, Rect window, ResizeCorner hovered, float scale,
        float caseRadius, bool roundCorners)
    {
        for (var corner = ResizeCorner.TopLeft; corner <= ResizeCorner.BottomRight; corner++)
        {
            var hot = corner == hovered;
            if (roundCorners)
            {
                DrawRoundResizeSlider(paint, window, corner, scale, caseRadius, hot);
            }
            else
            {
                DrawSquareResizeSlider(paint, window, corner, scale, hot);
            }
        }
    }

    private static void DrawSquareResizeSlider(DalamudPaintSurface paint, Rect window, ResizeCorner corner, float scale,
        bool hot)
    {
        var reach = 8f * scale;
        var inset = MathF.Max(2.2f * scale, 2f);
        var thickness = MathF.Max((hot ? 2.2f : 1.6f) * scale, 1.2f);
        var ink = new Vector4(1f, 1f, 1f, hot ? 0.82f : 0.42f);
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

    private static void DrawRoundResizeSlider(DalamudPaintSurface paint, Rect window, ResizeCorner corner, float scale,
        float caseRadius, bool hot)
    {
        var radius = MathF.Min(caseRadius, MathF.Min(window.Width, window.Height) * 0.5f);
        if (radius < 4f)
        {
            radius = 10f * scale;
        }

        var inset = MathF.Max(2.6f * scale, 2.2f);
        var rail = MathF.Max(radius - inset, 4f);
        var tick = 9f * scale;
        var thickness = MathF.Max((hot ? 2.2f : 1.6f) * scale, 1.2f);
        var ink = new Vector4(1f, 1f, 1f, hot ? 0.82f : 0.42f);
        var (center, start) = corner switch
        {
            ResizeCorner.TopLeft => (window.Min + new Vector2(radius, radius), MathF.PI),
            ResizeCorner.TopRight => (new Vector2(window.Max.X - radius, window.Min.Y + radius), -MathF.PI * 0.5f),
            ResizeCorner.BottomRight => (window.Max - new Vector2(radius, radius), 0f),
            _ => (new Vector2(window.Min.X + radius, window.Max.Y - radius), MathF.PI * 0.5f),
        };

        paint.StrokeArc(center, rail, start, MathF.PI * 0.5f, ink, thickness);
        var from = center + new Vector2(MathF.Cos(start), MathF.Sin(start)) * rail;
        var until = start + MathF.PI * 0.5f;
        var to = center + new Vector2(MathF.Cos(until), MathF.Sin(until)) * rail;
        var inward = window.Center - center;
        if (inward.LengthSquared() > 1f)
        {
            inward = Vector2.Normalize(inward);
        }

        var alongFrom = new Vector2(-MathF.Sin(start), MathF.Cos(start));
        if (Vector2.Dot(alongFrom, inward) < 0f)
        {
            alongFrom = -alongFrom;
        }

        var alongTo = new Vector2(-MathF.Sin(until), MathF.Cos(until));
        if (Vector2.Dot(alongTo, inward) < 0f)
        {
            alongTo = -alongTo;
        }

        paint.Line(from, from + alongFrom * tick, ink, thickness);
        paint.Line(to, to + alongTo * tick, ink, thickness);
    }

    private float MaxScaleOnScreen(ResizeCorner corner)
    {
        var viewport = ImGui.GetMainViewport();
        var workMin = viewport.WorkPos;
        var workMax = viewport.WorkPos + viewport.WorkSize;
        Vector2 room;
        if (resizeGrip.IsDragging)
        {
            room = ResizeGrip.RoomFromAnchor(resizeGrip.ActiveCorner, resizeGrip.Anchor, workMin, workMax);
        }
        else
        {
            var pos = ImGui.GetWindowPos();
            var size = ImGui.GetWindowSize();
            room = corner switch
            {
                ResizeCorner.TopLeft => pos + size - workMin,
                ResizeCorner.TopRight => new Vector2(pos.X + size.X - workMin.X, workMax.Y - pos.Y),
                ResizeCorner.BottomLeft => new Vector2(workMax.X - pos.X, pos.Y + size.Y - workMin.Y),
                _ => workMax - pos,
            };
        }

        room = new Vector2(MathF.Max(1f, MathF.Min(room.X, viewport.WorkSize.X)),
            MathF.Max(1f, MathF.Min(room.Y, viewport.WorkSize.Y)));
        return HandsetSizeCatalog.FitScale(shapePreference.Form, shapePreference.Case, room,
            ImGuiHelpers.GlobalScale, display.Landscape);
    }

    private Vector2 lastWorkSize;

    private void ApplyWindowSize()
    {
        SizeCondition = ImGuiCond.Always;
        if (!resizeGrip.IsDragging && fold <= 0.02f)
        {
            var viewport = ImGui.GetMainViewport();
            var work = viewport.WorkSize;
            var jumped = lastWorkSize == Vector2.Zero
                || MathF.Abs(work.X - lastWorkSize.X) > 12f
                || MathF.Abs(work.Y - lastWorkSize.Y) > 12f;
            if (jumped)
            {
                lastWorkSize = work;
                var fit = HandsetSizeCatalog.FitScale(shapePreference.Form, shapePreference.Case, work,
                    ImGuiHelpers.GlobalScale, display.Landscape);
                if (shapePreference.ScaleStep > fit + 0.002f)
                {
                    shapePreference.ScaleStep = fit;
                }
            }
        }

        var full = FullSize();
        var face = FaceSize();
        Size = presenceVanish ? new Vector2(8f, 8f) : Vector2.Lerp(full, face, Ease(fold));
    }

    private Vector2 FaceSize()
    {
        var pocket = game.IsInGpose
            ? HandsetShapePreference.PocketSteps[0]
            : shapePreference.PocketScale;
        return HandsetPlacement.FaceSize(shapePreference.Form, shapePreference.Case, pocket,
            ImGuiHelpers.GlobalScale);
    }

    private Vector2 FullSize()
    {
        var full = HandsetSizeCatalog.SizeFor(shapePreference.Form, shapePreference.Case, shapePreference.ScaleStep) *
            ImGuiHelpers.GlobalScale;
        return display.Landscape ? new Vector2(full.Y, full.X) : full;
    }

    private void ApplyWindowPos()
    {
        if (pocketMoving)
        {
            return;
        }

        var full = FullSize();
        var face = FaceSize();
        var folding = fold > 0.02f && fold < 0.98f;

        if (!placedOnce)
        {
            PlaceOnce(wantFold && placement.HasPocket ? placement.Pocket : placement.HasOpen ? placement.Open : null,
                wantFold ? face : full);
            placedOnce = true;
            placeOnce = false;
            return;
        }

        if (folding && placement.HasOpen && placement.HasPocket)
        {
            var t = Ease(fold);
            var size = Vector2.Lerp(full, face, t);
            ImGui.SetNextWindowPos(HandsetPlacement.Clamp(Vector2.Lerp(placement.Open, placement.Pocket, t), size),
                ImGuiCond.Always);
            return;
        }

        if (!placeOnce)
        {
            return;
        }

        PlaceOnce(wantFold && placement.HasPocket ? placement.Pocket : placement.HasOpen ? placement.Open : null,
            wantFold ? face : full);
        placeOnce = false;
    }

    private static void PlaceOnce(Vector2? pos, Vector2 size)
    {
        if (pos is { } value)
        {
            ImGui.SetNextWindowPos(HandsetPlacement.Clamp(value, size), ImGuiCond.Always);
        }
    }

    private void RememberLivePlacement()
    {
        if (pocketMoving || fold > 0.02f && fold < 0.98f)
        {
            return;
        }

        if (!wantFold && fold <= 0.02f)
        {
            placement.RememberOpen(ImGui.GetWindowPos());
        }
        else if (wantFold && fold >= 0.98f)
        {
            placement.RememberPocket(ImGui.GetWindowPos());
        }

        if (!placement.HasPocket)
        {
            EnsurePocketSeed();
        }
    }

    private void CaptureOpenFromWindow()
    {
        if (fold > 0.08f)
        {
            return;
        }

        placement.RememberOpen(ImGui.GetWindowPos());
    }

    public void FlushPlacement()
    {
        try
        {
            persistPlacement();
        }
        catch
        {
            // Unload must not take the process down.
        }
    }

    private void CapturePocketFromWindow()
    {
        if (fold < 0.92f && !wantFold)
        {
            return;
        }

        placement.RememberPocket(ImGui.GetWindowPos());
    }

    private void CapturePlacement()
    {
        if (wantFold || fold > 0.5f)
        {
            CapturePocketFromWindow();
        }
        else
        {
            CaptureOpenFromWindow();
        }
    }

    private void EnsurePocketSeed()
    {
        placement.SeedPocketFromOpen(FullSize(), FaceSize());
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

    private static Rect CasePowerHit(Rect power, Rect screen, float scale)
    {
        var pad = MathF.Max(4f, 6f * scale);
        var left = MathF.Max(power.Min.X, screen.Max.X);
        if (left >= power.Max.X)
        {
            left = power.Min.X;
        }

        return new Rect(
            new Vector2(left, power.Min.Y - pad * 0.35f),
            new Vector2(power.Max.X + pad, power.Max.Y + pad * 0.35f));
    }

    private static Rect CaseSideHit(Rect nub, Rect screen, Rect window, float scale)
    {
        if (nub.IsEmpty)
        {
            return Rect.Empty;
        }

        var pad = MathF.Max(12f, 16f * scale);
        var left = MathF.Min(nub.Min.X, screen.Max.X) - pad;
        var right = MathF.Max(nub.Max.X, window.Max.X);
        return new Rect(
            new Vector2(left, nub.Min.Y - pad * 0.2f),
            new Vector2(right, nub.Max.Y + pad * 0.2f));
    }

    private bool NudgeVolume(Rect up, Rect down)
    {
        const float step = 0.08f;
        var raised = SideClick(up, "##lp-vol-up");
        var lowered = !raised && SideClick(down, "##lp-vol-down");
        if (!raised && !lowered)
        {
            return false;
        }

        display.Volume = Math.Clamp(display.Volume + (raised ? step : -step), 0f, 1f);
        volumeCue = 1.35f;
        return true;
    }

    private static bool SideClick(Rect area, string id)
    {
        if (area.IsEmpty || area.Width < 2f || area.Height < 2f)
        {
            return false;
        }

        ImGui.SetCursorScreenPos(area.Min);
        ImGui.InvisibleButton(id, new Vector2(MathF.Max(area.Width, 2f), MathF.Max(area.Height, 2f)));
        return ImGui.IsItemClicked() || ChromeClicked(area);
    }

    private void DrawVolumeCue(IPaintSurface paint, Rect screen, float scale)
    {
        if (volumeCue <= 0f)
        {
            return;
        }

        volumeCue = MathF.Max(0f, volumeCue - ImGui.GetIO().DeltaTime);
        var fade = MathF.Min(1f, volumeCue);
        var width = MathF.Min(screen.Width * 0.64f, scale * 220f);
        var height = scale * 34f;
        var box = Rect.FromSize(
            new Vector2(screen.Center.X - width * 0.5f, screen.Min.Y + scale * 52f),
            new Vector2(width, height));
        paint.Fill(box, new Vector4(0.07f, 0.07f, 0.09f, 0.84f * fade), height * 0.5f);
        var inner = box.Inset(new Edges(scale * 10f, scale * 11f));
        paint.Fill(inner, new Vector4(1f, 1f, 1f, 0.14f * fade), inner.Height * 0.5f);
        var fill = inner.LeftSlice(inner.Width * display.Volume);
        if (fill.Width > 1.5f)
        {
            paint.Fill(fill, new Vector4(1f, 1f, 1f, 0.92f * fade), inner.Height * 0.5f);
        }
    }

    private static bool ChromeClicked(Rect area) =>
        !DalamudInputProbe.OtherWindowAbove() && !area.IsEmpty && area.Contains(ImGui.GetMousePos()) &&
        ImGui.IsMouseClicked(ImGuiMouseButton.Left);
}
