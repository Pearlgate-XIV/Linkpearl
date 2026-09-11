using System.Globalization;
using System.IO;
using Dalamud.Bindings.ImGui;
using Linkpearl.Applets;
using Linkpearl.Destinations;
using Linkpearl.Device.Chassis;
using Linkpearl.Geometry;
using Linkpearl.Modules;
using Linkpearl.Input;
using Linkpearl.Layout;
using Linkpearl.Net;
using Linkpearl.Painting;
using Linkpearl.Platform;
using Linkpearl.Preferences;
using Linkpearl.Talk;
using Linkpearl.Theming;
using Linkpearl.Time;

namespace Linkpearl.Device.Shell;

public readonly record struct ControlCenterResult(
    bool Recents,
    bool Closed,
    bool Pocket,
    bool PowerOff,
    DestinationTab? Tab,
    int Section,
    string AppletId,
    string TalkId = "",
    string ProfileId = "",
    string NoticeId = "",
    string RouteHint = "");

public sealed class ControlCenter
{
    private static readonly Vector4 Glass = new(0.16f, 0.16f, 0.18f, 0.82f);
    private static readonly Vector4 Active = new(0.93f, 0.93f, 0.95f, 0.96f);
    private static readonly Vector4 CircleOff = new(0.22f, 0.22f, 0.24f, 0.92f);
    private static readonly Vector4 InkOn = new(0.12f, 0.12f, 0.14f, 1f);
    private static readonly Vector4 InkOff = new(0.96f, 0.96f, 0.97f, 1f);
    private static readonly Vector4 Muted = new(0.72f, 0.72f, 0.74f, 1f);

    private readonly NoticeLedger ledger;
    private bool open;
    private bool draggingLight;
    private bool draggingVolume;
    private bool sheetTrack;
    private bool sheetDrag;
    private float grabY;
    private float grabReveal;
    private float reveal;
    private string noticeHoldId = string.Empty;
    private float noticeGrabY;
    private float noticeLift;
    private bool noticeDrag;

    public ControlCenter(NoticeLedger ledger)
    {
        this.ledger = ledger;
    }

    public bool IsOpen => open || sheetDrag || reveal > 0.02f;

    public bool IsPulling => sheetDrag;

    public void Open() => open = true;

    // Left side of the status strip. When the sheet is open, the gear / power / lock stay out
    // of this handle so those taps do not also dismiss.
    public static Rect HandleOn(Rect screen, float scale, bool excludeTools)
    {
        var strip = StatusStrip.StripArea(screen, scale);
        var dead = LockButton.ReservedRight(scale) + (excludeTools ? scale * 72f : 0f);
        return dead <= 0f ? strip : strip.Inset(new Edges(0f, 0f, dead, 0f));
    }

    public void Close()
    {
        open = false;
        draggingLight = false;
        draggingVolume = false;
        sheetTrack = false;
        sheetDrag = false;
        DropNoticeHold();
    }

    public ControlCenterResult Draw(in AppletFrame frame, Rect screen, DisplayPreferences display,
        PearlSnapshot snapshot, IPearlHub pearl, ITalk talk, IClock clock, IWifeSync wife, IGameSession game,
        bool allowStrip = true)
    {
        var scale = frame.Scale;
        var input = frame.Input;
        PullClosed(input, screen, scale, allowStrip);
        if (!sheetDrag)
        {
            var speed = display.ReduceMotion ? 8f : 5.5f;
            var target = open ? 1f : 0f;
            reveal += (target - reveal) * (1f - MathF.Exp(-speed * MathF.Max(frame.DeltaSeconds, 0f)));
        }

        if (!open && !sheetDrag && reveal < 0.02f)
        {
            reveal = 0f;
            return default;
        }

        var paint = frame.Paint;
        var text = frame.Text;
        var theme = frame.Theme;

        PlateFrost.Draw(frame, screen, display, clock, reveal);
        var topPad = scale * 6f;
        var fullHeight = MathF.Max(scale * 80f,
            screen.Height - topPad - SoftKeyBar.Height(scale) - scale * 6f);
        var sheet = Rect.FromSize(
            new Vector2(screen.Min.X, screen.Min.Y + topPad - fullHeight * (1f - reveal)),
            new Vector2(screen.Width, fullHeight));

        paint.PushClip(screen);
        try
        {
            // The tap that opens the sheet lands on the status strip, which is outside the
            // panel. Wait until the slide-in finishes, then consume leftover clicks so that
            // same press cannot also dismiss.
            if (open && reveal > 0.88f && !draggingLight && !draggingVolume && !sheetDrag &&
                !sheet.Contains(input.Pointer) && input.ConsumeClick(screen))
            {
                Close();
                return new ControlCenterResult(false, true, false, false, null, 0, string.Empty);
            }

            var live = open && reveal > 0.88f && !sheetDrag ? input : SilentInput.Instance;
            var inner = sheet.Inset(new Edges(scale * 14f, scale * 4f, scale * 14f, scale * 10f));
            var stack = new Stack(inner, StackAxis.Vertical, scale * 10f);
            var pending = new Pending();

            DrawClockRow(frame, live, stack.Take(scale * 36f), scale, clock, pending);
            DrawHeroPair(paint, text, live, stack.Take(scale * 56f), scale, snapshot, pearl, wife, pending,
                ShadeGlyph(frame, "wifi.png"));
            DrawRoundPanel(frame, live, stack.Take(scale * 152f), scale, display, game, pending);
            ShadeSlider.Draw(paint, live, theme, stack.Take(scale * 46f), display.Brightness, ref draggingLight,
                value => display.Brightness = value, sun: true, ShadeGlyph(frame, "bright-low.png"),
                ShadeGlyph(frame, "bright-high.png"));
            ShadeSlider.Draw(paint, live, theme, stack.Take(scale * 46f), display.Volume, ref draggingVolume,
                value => display.Volume = value, sun: false, ShadeGlyph(frame, "volume-low.png"),
                ShadeGlyph(frame, "volume-high.png"));
            DrawNotices(frame, live, stack.Remaining, scale, snapshot, talk, clock, pending);
            DragSheet(input, sheet, fullHeight, scale);

            var result = pending.Result;
            if (result.Closed || result.Pocket || result.PowerOff || result.Recents || result.Tab is not null ||
                !string.IsNullOrEmpty(result.AppletId))
            {
                Close();
            }

            return result;
        }
        finally
        {
            paint.PopClip();
        }
    }

    private static void DrawClockRow(in AppletFrame frame, IInputProbe input, Rect area, float scale,
        IClock clock, Pending pending)
    {
        var now = clock.Now.ToLocalTime();
        var time = now.ToString("h:mm", CultureInfo.InvariantCulture);
        var date = now.ToString("ddd, MMM d", CultureInfo.InvariantCulture);
        var trail = LockButton.ReservedRight(scale);
        var tools = area.Inset(new Edges(0f, 0f, trail, 0f)).RightSlice(scale * 72f);
        var power = tools.LeftSlice(scale * 34f);
        var gear = tools.RightSlice(scale * 34f);
        var copy = area.Inset(new Edges(0f, 0f, trail + tools.Width + scale * 8f, 0f));
        var timeWidth = frame.Text.Measure(time, FontRole.Display).X + scale * 8f;
        frame.Text.DrawIn(copy.LeftSlice(timeWidth), time, new TextStyle(FontRole.Display, InkOff));
        frame.Text.DrawEllipsized(copy.Inset(new Edges(timeWidth, copy.Height * 0.22f, 0f, 0f)), date,
            new TextStyle(FontRole.Caption, Muted));
        DrawPowerMark(frame, power);
        DrawNavGear(frame, gear);
        if (input.ConsumeClick(gear))
        {
            pending.Result = pending.Result with { Tab = DestinationTab.Settings };
        }

        if (input.ConsumeClick(power))
        {
            pending.Result = pending.Result with { PowerOff = true };
        }
    }

    private static void DrawHeroPair(IPaintSurface paint, ITextPainter text, IInputProbe input, Rect area, float scale,
        PearlSnapshot snapshot, IPearlHub pearl, IWifeSync wife, Pending pending, ITextureHandle? wifiIcon)
    {
        var gap = scale * 8f;
        var half = (area.Width - gap) * 0.5f;
        var left = Rect.FromSize(area.Min, new Vector2(half, area.Height));
        var right = Rect.FromSize(new Vector2(left.Max.X + gap, area.Min.Y), new Vector2(half, area.Height));
        DrawHero(paint, text, input, left, scale, Glyph.Burst, "Pearlgate",
            GateLine(snapshot), snapshot.SignedIn, () => ToggleGate(snapshot, pearl));
        var wifeOn = wife.IsPresent && wife.IsOn;
        DrawHero(paint, text, input, right, scale, Glyph.Wifi, "WIFI",
            !wife.IsPresent
                ? PhoneLanguages.T("shell.noplugin")
                : wifeOn
                    ? PhoneLanguages.T("shell.connected")
                    : PhoneLanguages.T("shell.off"), wifeOn, () =>
            {
                if (wife.IsPresent)
                {
                    wife.SetOn(!wife.IsOn);
                }
            }, wifiIcon);
    }

    private static void DrawRoundPanel(in AppletFrame frame, IInputProbe input, Rect area,
        float scale, DisplayPreferences display, IGameSession game, Pending pending)
    {
        frame.Paint.Fill(area, Glass, scale * 28f);
        var inner = area.Inset(new Edges(scale * 8f, scale * 10f, scale * 8f, scale * 16f));
        var cellW = inner.Width / 4f;
        var cellH = inner.Height / 2f;
        DrawRound(frame, input, CellBox(inner, cellW, cellH, 0, 0), scale, Glyph.Moon, PhoneLanguages.T("shell.quiet"),
            display.Quiet, () => display.Quiet = !display.Quiet);
        DrawRound(frame, input, CellBox(inner, cellW, cellH, 1, 0), scale, Glyph.Bell, PhoneLanguages.T("shell.duties"),
            display.QuietWhenBusy, () => display.QuietWhenBusy = !display.QuietWhenBusy);
        DrawRound(frame, input, CellBox(inner, cellW, cellH, 2, 0), scale, Glyph.Mini, PhoneLanguages.T("shell.mini"),
            false, () => pending.Result = pending.Result with { Pocket = true });
        DrawRound(frame, input, CellBox(inner, cellW, cellH, 3, 0), scale, Glyph.Camera, PhoneLanguages.T("shell.gpose"),
            game.IsInGpose, () =>
            {
                if (!game.IsInGpose)
                {
                    game.OpenGroupPose();
                }

                game.CuePocket();
                pending.Result = pending.Result with { Pocket = true };
            });
        DrawRound(frame, input, CellBox(inner, cellW, cellH, 0, 1), scale, Glyph.Glow, PhoneLanguages.T("shell.icons"),
            display.ShowMarks, () => display.ShowMarks = !display.ShowMarks);
        DrawRound(frame, input, CellBox(inner, cellW, cellH, 1, 1), scale, Glyph.Globe, PhoneLanguages.T("shell.world"),
            display.ShowWorld, () => display.ShowWorld = !display.ShowWorld);
        DrawRound(frame, input, CellBox(inner, cellW, cellH, 2, 1), scale, Glyph.Friend, PhoneLanguages.T("shell.portraits"),
            display.StayInPortraits, () => display.StayInPortraits = !display.StayInPortraits);
        DrawRound(frame, input, CellBox(inner, cellW, cellH, 3, 1), scale, Glyph.Film, PhoneLanguages.T("shell.scenes"),
            display.TuckForCutscenes, () => display.TuckForCutscenes = !display.TuckForCutscenes);
        DrawGrip(frame.Paint, area.BottomSlice(scale * 12f), Muted);
    }

    private static void DrawHero(IPaintSurface paint, ITextPainter text, IInputProbe input, Rect area, float scale,
        Glyph glyph, string title, string detail, bool on, Action tap, ITextureHandle? icon = null)
    {
        var capsule = area.Height * 0.5f;
        paint.Fill(area, Glass, capsule);
        var pad = scale * 7f;
        var circleSide = MathF.Min(area.Height - pad * 2f, area.Width * 0.34f);
        var circle = Rect.FromSize(
            new Vector2(area.Min.X + pad, area.Center.Y - circleSide * 0.5f),
            new Vector2(circleSide, circleSide));
        paint.FillCircle(circle.Center, circleSide * 0.5f, on ? Active : CircleOff);
        var mark = circle.Inset(circleSide * 0.22f);
        var ink = on ? InkOn : InkOff;
        if (icon is { IsReady: true })
        {
            paint.Image(icon, mark, ink);
        }
        else
        {
            DrawGlyph(paint, mark, glyph, ink);
        }
        var copy = Rect.FromSize(
            new Vector2(circle.Max.X + scale * 8f, area.Min.Y + scale * 8f),
            new Vector2(MathF.Max(0f, area.Max.X - circle.Max.X - scale * 14f), area.Height - scale * 16f));
        text.DrawEllipsized(copy.TopSlice(scale * 16f), title,
            new TextStyle(FontRole.CaptionStrong, InkOff));
        text.DrawEllipsized(copy.BottomSlice(scale * 14f), detail,
            new TextStyle(FontRole.Caption, Muted));
        ImGui.PushID(title);
        ImGui.SetCursorScreenPos(area.Min);
        ImGui.InvisibleButton("##hero-card", area.Size);
        if (ImGui.IsItemHovered())
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Arrow);
        }

        ImGui.PopID();
        if (input.ConsumeClick(circle))
        {
            tap();
        }
    }

    private void DrawNotices(in AppletFrame frame, IInputProbe input, Rect area,
        float scale, PearlSnapshot snapshot, ITalk talk, IClock clock, Pending pending)
    {
        if (area.Height < scale * 36f)
        {
            return;
        }

        var head = area.TopSlice(scale * 18f);
        frame.Text.DrawIn(head, PhoneLanguages.T("shell.notifications"), new TextStyle(FontRole.CaptionStrong, InkOff));
        var clear = head.RightSlice(scale * 64f);
        frame.Text.DrawIn(clear, PhoneLanguages.T("shell.clear"), new TextStyle(FontRole.Caption, Muted, TextAlign.Right));
        if (input.ConsumeClick(clear))
        {
            ledger.Clear(snapshot, talk);
        }

        var list = area.Inset(new Edges(0f, scale * 22f, 0f, 0f));
        var rowH = scale * 52f;
        var gap = scale * 6f;
        var items = ledger.Visible(snapshot, talk, clock);
        var drawn = 0;
        for (var index = 0; index < items.Count; index++)
        {
            var item = items[index];
            var row = RowAt(list, rowH, gap, drawn);
            if (row.Max.Y > list.Max.Y + 0.5f)
            {
                break;
            }

            var lift = string.Equals(noticeHoldId, item.Id, StringComparison.Ordinal) ? noticeLift : 0f;
            var card = row.Translate(new Vector2(0f, -lift));
            DrawNotice(frame, card, scale, NoticeMarks.For(item.Kind), item.Title, item.Detail, item.When);
            SteerNotice(input, row, item, scale, pending);
            drawn++;
        }

        if (drawn == 0)
        {
            frame.Text.DrawIn(list.TopSlice(scale * 20f), PhoneLanguages.T("shell.empty"),
                new TextStyle(FontRole.Caption, Muted));
        }
    }

    private void SteerNotice(IInputProbe input, Rect row, in GlassNotice item, float scale, Pending pending)
    {
        if (row.Contains(input.Pointer))
        {
            input.Claim(row);
        }

        if (noticeHoldId.Length == 0 && input.WasPressed(row))
        {
            noticeHoldId = item.Id;
            noticeGrabY = input.Pointer.Y;
            noticeLift = 0f;
            noticeDrag = false;
            input.Claim(row);
            return;
        }

        if (!string.Equals(noticeHoldId, item.Id, StringComparison.Ordinal))
        {
            return;
        }

        if (input.IsHeld())
        {
            noticeLift = MathF.Max(0f, noticeGrabY - input.Pointer.Y);
            noticeDrag = noticeDrag || noticeLift > MathF.Max(10f, 12f * scale);
            input.Claim(row.Translate(new Vector2(0f, -noticeLift)).Expand(scale * 8f));
            return;
        }

        var toss = noticeDrag && noticeLift >= MathF.Max(18f, 22f * scale);
        var tap = !noticeDrag;
        DropNoticeHold();
        if (toss)
        {
            ledger.Dismiss(item.Id);
            return;
        }

        if (tap)
        {
            ledger.Dismiss(item.Id);
            pending.Result = NoticeLaunch.ToResult(item);
        }
    }

    private void DropNoticeHold()
    {
        noticeHoldId = string.Empty;
        noticeGrabY = 0f;
        noticeLift = 0f;
        noticeDrag = false;
    }

    private static void DrawNotice(in AppletFrame frame, Rect row, float scale,
        string appletId, string title, string detail, string when)
    {
        frame.Paint.Fill(row, Glass, scale * 18f);
        var inset = row.Inset(new Edges(scale * 10f, scale * 6f, scale * 10f, scale * 6f));
        var side = MathF.Min(inset.Height, scale * 36f);
        var mark = Rect.FromSize(new Vector2(inset.Min.X, inset.Center.Y - side * 0.5f),
            new Vector2(side, side));
        AppMarks.DrawFace(frame, mark, appletId, false);
        var copy = new Rect(new Vector2(mark.Max.X + scale * 8f, inset.Min.Y), inset.Max);
        frame.Text.DrawEllipsized(copy.TopSlice(scale * 16f), title,
            new TextStyle(FontRole.CaptionStrong, InkOff));
        frame.Text.DrawEllipsized(copy.Inset(new Edges(0f, scale * 16f, scale * 52f, 0f)), detail,
            new TextStyle(FontRole.Caption, Muted));
        frame.Text.DrawIn(copy.RightSlice(scale * 48f).BottomSlice(scale * 14f), when,
            new TextStyle(FontRole.Caption, Muted, TextAlign.Right));
    }

    private void PullClosed(IInputProbe input, Rect screen, float scale, bool allow)
    {
        var pullingOpen = sheetDrag && grabReveal <= 0.001f;
        if (open && !pullingOpen)
        {
            // Leave DragSheet's close swipe alone. Only drop a leftover press from
            // the closed-state pull-down.
            if (sheetTrack && !sheetDrag && grabReveal <= 0.001f)
            {
                sheetTrack = false;
            }

            return;
        }

        if (!allow && !pullingOpen)
        {
            if (!open)
            {
                sheetTrack = false;
                sheetDrag = false;
            }

            return;
        }

        var handle = HandleOn(screen, scale, excludeTools: false);
        var topPad = scale * 6f;
        var fullHeight = MathF.Max(scale * 80f,
            screen.Height - topPad - SoftKeyBar.Height(scale) - scale * 6f);
        if (!sheetTrack && !open && input.WasPressed(handle))
        {
            sheetTrack = true;
            sheetDrag = false;
            grabY = input.Pointer.Y;
            grabReveal = 0f;
        }

        if (!sheetTrack)
        {
            return;
        }

        if (input.IsHeld())
        {
            var dy = input.Pointer.Y - grabY;
            if (!sheetDrag && dy > MathF.Max(10f, 12f * scale))
            {
                sheetDrag = true;
                open = true;
            }

            if (sheetDrag)
            {
                reveal = Math.Clamp(dy / MathF.Max(fullHeight, 1f), 0f, 1f);
                input.Claim(handle);
            }

            return;
        }

        if (!sheetDrag)
        {
            sheetTrack = false;
            return;
        }

        if (reveal < 0.35f)
        {
            Close();
            return;
        }

        open = true;
        sheetTrack = false;
        sheetDrag = false;
    }

    private void DragSheet(IInputProbe input, Rect sheet, float fullHeight, float scale)
    {
        if (draggingLight || draggingVolume || noticeHoldId.Length > 0)
        {
            sheetTrack = false;
            sheetDrag = false;
            return;
        }

        // Press in the lower part of the sheet, then drag up. No extra chrome.
        var grab = sheet.BottomSlice(MathF.Max(sheet.Height * 0.38f, scale * 120f));
        if (!sheetTrack && open && reveal > 0.35f && !input.PointerClaimed() && input.WasPressed(grab))
        {
            sheetTrack = true;
            sheetDrag = false;
            grabY = input.Pointer.Y;
            grabReveal = reveal;
        }

        if (sheetTrack && input.IsHeld())
        {
            var dy = input.Pointer.Y - grabY;
            if (!sheetDrag && dy < -MathF.Max(10f, 12f * scale))
            {
                sheetDrag = true;
            }

            if (sheetDrag)
            {
                reveal = Math.Clamp(grabReveal + dy / MathF.Max(fullHeight, 1f), 0f, 1f);
                input.Claim(sheet);
            }

            return;
        }

        if (!sheetTrack)
        {
            return;
        }

        if (sheetDrag && reveal < 0.62f)
        {
            Close();
            return;
        }

        sheetTrack = false;
        sheetDrag = false;
    }

    private static void DrawGrip(IPaintSurface paint, Rect row, Vector4 ink)
    {
        var bar = Rect.FromSize(new Vector2(row.Center.X - row.Width * 0.07f, row.Center.Y - row.Height * 0.14f),
            new Vector2(row.Width * 0.14f, MathF.Max(3f, row.Height * 0.28f)));
        paint.Fill(bar, ink with { W = 0.55f }, bar.Height * 0.5f);
    }

    private static void DrawRound(in AppletFrame frame, IInputProbe input, Rect area, float scale,
        Glyph glyph, string title, bool on, Action tap)
    {
        var side = MathF.Min(area.Width, area.Height - scale * 14f) * 0.72f;
        var circle = Rect.FromSize(new Vector2(area.Center.X - side * 0.5f, area.Min.Y + scale * 2f),
            new Vector2(side, side));
        frame.Paint.FillCircle(circle.Center, side * 0.5f, on ? Active : CircleOff);
        DrawGlyph(frame.Paint, circle.Inset(side * 0.22f), glyph, on ? InkOn : InkOff);
        frame.Text.DrawEllipsized(area.BottomSlice(scale * 14f), title,
            new TextStyle(FontRole.Caption, on ? InkOff : Muted, TextAlign.Center));
        if (input.ConsumeClick(area))
        {
            tap();
        }
    }

    private static Rect CellBox(Rect grid, float width, float height, int column, int row) =>
        Rect.FromSize(new Vector2(grid.Min.X + column * width, grid.Min.Y + row * height),
            new Vector2(width, height));

    private static Rect RowAt(Rect list, float height, float gap, int index) =>
        Rect.FromSize(new Vector2(list.Min.X, list.Min.Y + index * (height + gap)),
            new Vector2(list.Width, height));

    private static string NonEmpty(string value, string fallback) => value.Length > 0 ? value : fallback;

    private static string GateLine(PearlSnapshot snapshot)
    {
        if (snapshot.Busy)
        {
            return PhoneLanguages.T("shell.signing");
        }

        if (snapshot.SignedIn)
        {
            return NonEmpty(snapshot.MeWorld, PhoneLanguages.T("shell.signed"));
        }

        return snapshot.ChallengeCode.Length > 0
            ? PhoneLanguages.T("shell.entercode")
            : PhoneLanguages.T("shell.offline");
    }

    private static void ToggleGate(PearlSnapshot snapshot, IPearlHub pearl)
    {
        if (snapshot.Busy)
        {
            return;
        }

        if (snapshot.SignedIn)
        {
            pearl.SignOut();
            return;
        }

        pearl.BeginSignIn();
    }

    private sealed class Pending
    {
        public ControlCenterResult Result = new(false, false, false, false, null, 0, string.Empty);
    }

    private enum Glyph : byte
    {
        Wifi,
        Chat,
        Bell,
        Moon,
        Mini,
        Camera,
        Music,
        After,
        Glow,
        Rotate,
        Wife,
        Burst,
        Friend,
        Globe,
        Film,
    }

    private static void DrawGlyph(IPaintSurface paint, Rect area, Glyph glyph, Vector4 ink)
    {
        switch (glyph)
        {
            case Glyph.Wifi:
                DrawWifi(paint, area, ink);
                break;
            case Glyph.Chat:
                DrawChat(paint, area, ink);
                break;
            case Glyph.Bell:
                DrawBell(paint, area, ink);
                break;
            case Glyph.Moon:
                DrawMoon(paint, area, ink);
                break;
            case Glyph.Mini:
                DrawMini(paint, area, ink);
                break;
            case Glyph.Camera:
                DrawCamera(paint, area, ink);
                break;
            case Glyph.Music:
                DrawNote(paint, area, ink);
                break;
            case Glyph.After:
                DrawMoon(paint, area, ink);
                break;
            case Glyph.Glow:
                DrawBurst(paint, area, ink);
                break;
            case Glyph.Rotate:
                DrawRotate(paint, area, ink);
                break;
            case Glyph.Wife:
                DrawHeart(paint, area, ink);
                break;
            case Glyph.Burst:
                DrawBurst(paint, area, ink);
                break;
            case Glyph.Globe:
                DrawGlobe(paint, area, ink);
                break;
            case Glyph.Film:
                DrawFilm(paint, area, ink);
                break;
            default:
                DrawFriend(paint, area, ink);
                break;
        }
    }

    private static void DrawGlobe(IPaintSurface paint, Rect area, Vector4 ink)
    {
        var c = area.Center;
        var s = MathF.Min(area.Width, area.Height) * 0.36f;
        var stroke = MathF.Max(1.2f, s * 0.16f);
        paint.StrokeCircle(c, s, ink, stroke);
        paint.Line(c + new Vector2(0f, -s), c + new Vector2(0f, s), ink, stroke);
        paint.Line(c + new Vector2(-s * 0.72f, -s * 0.38f), c + new Vector2(s * 0.72f, -s * 0.38f), ink, stroke);
        paint.Line(c + new Vector2(-s * 0.72f, s * 0.38f), c + new Vector2(s * 0.72f, s * 0.38f), ink, stroke);
    }

    private static void DrawFilm(IPaintSurface paint, Rect area, Vector4 ink)
    {
        var body = area.Inset(new Edges(area.Width * 0.18f, area.Height * 0.22f));
        var stroke = MathF.Max(1.2f, area.Width * 0.06f);
        paint.Stroke(body, ink, stroke, body.Height * 0.12f);
        var hole = MathF.Max(1.1f, body.Width * 0.07f);
        paint.FillCircle(new Vector2(body.Min.X + body.Width * 0.18f, body.Min.Y + body.Height * 0.22f), hole, ink);
        paint.FillCircle(new Vector2(body.Min.X + body.Width * 0.18f, body.Max.Y - body.Height * 0.22f), hole, ink);
        paint.FillCircle(new Vector2(body.Max.X - body.Width * 0.18f, body.Min.Y + body.Height * 0.22f), hole, ink);
        paint.FillCircle(new Vector2(body.Max.X - body.Width * 0.18f, body.Max.Y - body.Height * 0.22f), hole, ink);
        var play = new[]
        {
            body.Center + new Vector2(-body.Width * 0.08f, -body.Height * 0.18f),
            body.Center + new Vector2(body.Width * 0.18f, 0f),
            body.Center + new Vector2(-body.Width * 0.08f, body.Height * 0.18f),
        };
        paint.Polyline(play, ink, stroke, true);
    }

    private static void DrawWifi(IPaintSurface paint, Rect area, Vector4 ink)
    {
        var c = area.Center;
        var s = MathF.Min(area.Width, area.Height) * 0.38f;
        var stroke = MathF.Max(1.2f, s * 0.18f);
        paint.StrokeCircle(c, s, ink, stroke);
        paint.StrokeCircle(c, s * 0.62f, ink with { W = ink.W * 0.75f }, stroke);
        paint.FillCircle(c, s * 0.18f, ink);
    }

    private static void DrawChat(IPaintSurface paint, Rect area, Vector4 ink)
    {
        var body = area.Inset(area.Width * 0.18f);
        paint.Stroke(body, ink, MathF.Max(1.2f, area.Width * 0.06f), body.Height * 0.28f);
        paint.FillCircle(body.Center + new Vector2(-body.Width * 0.18f, 0f), body.Width * 0.07f, ink);
        paint.FillCircle(body.Center, body.Width * 0.07f, ink);
        paint.FillCircle(body.Center + new Vector2(body.Width * 0.18f, 0f), body.Width * 0.07f, ink);
    }

    private static void DrawBell(IPaintSurface paint, Rect area, Vector4 ink)
    {
        var c = area.Center;
        var s = MathF.Min(area.Width, area.Height) * 0.32f;
        var stroke = MathF.Max(1.2f, s * 0.18f);
        paint.StrokeCircle(c + new Vector2(0f, -s * 0.08f), s * 0.72f, ink, stroke);
        paint.Line(c + new Vector2(-s * 0.72f, s * 0.42f), c + new Vector2(s * 0.72f, s * 0.42f), ink, stroke);
        paint.FillCircle(c + new Vector2(0f, s * 0.62f), s * 0.12f, ink);
    }

    private static void DrawMoon(IPaintSurface paint, Rect area, Vector4 ink)
    {
        var c = area.Center;
        var s = MathF.Min(area.Width, area.Height) * 0.34f;
        paint.FillCircle(c, s, ink);
        paint.FillCircle(c + new Vector2(s * 0.38f, -s * 0.12f), s * 0.72f, ink with { W = 0.12f });
    }

    private static void DrawMini(IPaintSurface paint, Rect area, Vector4 ink)
    {
        var outer = area.Inset(area.Width * 0.16f);
        paint.Stroke(outer, ink, MathF.Max(1.1f, area.Width * 0.05f), outer.Height * 0.16f);
        var inner = Rect.FromSize(outer.Min + new Vector2(outer.Width * 0.12f, outer.Height * 0.38f),
            new Vector2(outer.Width * 0.46f, outer.Height * 0.42f));
        paint.Stroke(inner, ink, MathF.Max(1.1f, area.Width * 0.05f), inner.Height * 0.18f);
    }

    private static void DrawCamera(IPaintSurface paint, Rect area, Vector4 ink)
    {
        var body = area.Inset(new Edges(area.Width * 0.16f, area.Height * 0.24f));
        paint.Stroke(body, ink, MathF.Max(1.2f, area.Width * 0.055f), body.Height * 0.22f);
        paint.StrokeCircle(body.Center, body.Height * 0.22f, ink, MathF.Max(1.1f, area.Width * 0.05f));
        paint.Fill(Rect.FromSize(new Vector2(body.Min.X + body.Width * 0.18f, body.Min.Y - body.Height * 0.16f),
            new Vector2(body.Width * 0.22f, body.Height * 0.16f)), ink, body.Height * 0.08f);
    }

    private static void DrawNote(IPaintSurface paint, Rect area, Vector4 ink)
    {
        var c = area.Center;
        var s = MathF.Min(area.Width, area.Height) * 0.32f;
        var stroke = MathF.Max(1.3f, s * 0.22f);
        paint.FillCircle(c + new Vector2(-s * 0.28f, s * 0.42f), s * 0.22f, ink);
        paint.Line(c + new Vector2(-s * 0.08f, s * 0.42f), c + new Vector2(-s * 0.08f, -s * 0.62f), ink, stroke);
        paint.Line(c + new Vector2(-s * 0.08f, -s * 0.62f), c + new Vector2(s * 0.55f, -s * 0.38f), ink, stroke);
    }

    private static void DrawBurst(IPaintSurface paint, Rect area, Vector4 ink)
    {
        var c = area.Center;
        var s = MathF.Min(area.Width, area.Height) * 0.36f;
        var stroke = MathF.Max(1.2f, s * 0.16f);
        paint.Line(c + new Vector2(0f, -s), c + new Vector2(0f, s), ink, stroke);
        paint.Line(c + new Vector2(-s, 0f), c + new Vector2(s, 0f), ink, stroke);
        paint.Line(c + new Vector2(-s * 0.68f, -s * 0.68f), c + new Vector2(s * 0.68f, s * 0.68f), ink, stroke);
        paint.Line(c + new Vector2(s * 0.68f, -s * 0.68f), c + new Vector2(-s * 0.68f, s * 0.68f), ink, stroke);
        paint.FillCircle(c, s * 0.18f, ink);
    }

    private static void DrawRotate(IPaintSurface paint, Rect area, Vector4 ink)
    {
        var c = area.Center;
        var s = MathF.Min(area.Width, area.Height) * 0.32f;
        var stroke = MathF.Max(1.2f, s * 0.18f);
        paint.StrokeCircle(c, s, ink, stroke);
        paint.Line(c + new Vector2(s * 0.72f, -s * 0.18f), c + new Vector2(s * 1.05f, s * 0.08f), ink, stroke);
        paint.Line(c + new Vector2(s * 0.72f, -s * 0.18f), c + new Vector2(s * 0.42f, s * 0.12f), ink, stroke);
    }

    private static void DrawHeart(IPaintSurface paint, Rect area, Vector4 ink)
    {
        var c = area.Center;
        var s = MathF.Min(area.Width, area.Height) * 0.30f;
        paint.FillCircle(c + new Vector2(-s * 0.38f, -s * 0.18f), s * 0.38f, ink);
        paint.FillCircle(c + new Vector2(s * 0.38f, -s * 0.18f), s * 0.38f, ink);
        var stroke = MathF.Max(1.2f, s * 0.22f);
        paint.Line(c + new Vector2(-s * 0.72f, 0f), c + new Vector2(0f, s * 0.82f), ink, stroke);
        paint.Line(c + new Vector2(s * 0.72f, 0f), c + new Vector2(0f, s * 0.82f), ink, stroke);
    }

    private static void DrawFriend(IPaintSurface paint, Rect area, Vector4 ink)
    {
        var c = area.Center;
        var s = MathF.Min(area.Width, area.Height) * 0.28f;
        paint.FillCircle(c + new Vector2(0f, -s * 0.45f), s * 0.42f, ink);
        paint.Stroke(Rect.FromSize(c + new Vector2(-s * 0.72f, s * 0.05f), new Vector2(s * 1.44f, s * 0.85f)), ink,
            MathF.Max(1.2f, s * 0.16f), s * 0.42f);
    }

    private static void DrawNavGear(in AppletFrame frame, Rect area)
    {
        var texture = frame.Textures.FromFile(AppIconCatalog.Glyph(frame.Paths, "settings.png"));
        if (texture is not { IsReady: true })
        {
            texture = frame.Textures.FromFile(AppIconCatalog.Absolute(frame.Paths, "settings.png"));
        }

        if (texture is { IsReady: true })
        {
            var side = MathF.Min(area.Width, area.Height) * 0.576f;
            var dest = Rect.FromSize(area.Center - new Vector2(side * 0.5f, side * 0.5f), new Vector2(side, side));
            frame.Paint.Image(texture, dest, InkOff);
            return;
        }

        var c = area.Center;
        var s = MathF.Min(area.Width, area.Height) * 0.176f;
        var stroke = MathF.Max(1.2f, frame.Units(1.4f));
        frame.Paint.StrokeCircle(c, s, InkOff, stroke);
        frame.Paint.StrokeCircle(c, s * 0.42f, InkOff, stroke);
        for (var tooth = 0; tooth < 8; tooth++)
        {
            var angle = tooth * (MathF.PI * 2f / 8f);
            frame.Paint.FillCircle(c + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * (s * 1.22f),
                stroke * 0.9f, InkOff);
        }
    }

    private static ITextureHandle? ShadeGlyph(in AppletFrame frame, string file) =>
        frame.Textures.FromFile(AppIconCatalog.Glyph(frame.Paths, file));

    private static void DrawPowerMark(in AppletFrame frame, Rect area)
    {
        var texture = ShadeGlyph(frame, "power.png");
        if (texture is { IsReady: true })
        {
            var side = MathF.Min(area.Width, area.Height) * 0.576f;
            var dest = Rect.FromSize(area.Center - new Vector2(side * 0.5f, side * 0.5f), new Vector2(side, side));
            frame.Paint.Image(texture, dest, InkOff);
            return;
        }

        var c = area.Center;
        var s = MathF.Min(area.Width, area.Height) * 0.224f;
        var stroke = MathF.Max(1.6f, s * 0.28f);
        frame.Paint.StrokeCircle(c, s, InkOff, stroke);
        frame.Paint.Line(c + new Vector2(0f, -s * 1.05f), c + new Vector2(0f, -s * 0.12f), InkOff, stroke);
    }
}
