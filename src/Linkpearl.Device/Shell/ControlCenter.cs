using System.Globalization;
using Linkpearl.Applets;
using Linkpearl.Destinations;
using Linkpearl.Geometry;
using Linkpearl.Input;
using Linkpearl.Layout;
using Linkpearl.Media;
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
    DestinationTab? Tab,
    int Section,
    string AppletId);

public sealed class ControlCenter
{
    private readonly NoticeLedger ledger;
    private bool open;
    private bool draggingLight;
    private bool draggingVolume;
    private bool sheetTrack;
    private bool sheetDrag;
    private float grabY;
    private float grabReveal;
    private float reveal;

    public ControlCenter(NoticeLedger ledger)
    {
        this.ledger = ledger;
    }

    public bool IsOpen => open || sheetDrag || reveal > 0.02f;

    public void Open() => open = true;

    public void Close()
    {
        open = false;
        draggingLight = false;
        draggingVolume = false;
        sheetTrack = false;
        sheetDrag = false;
    }

    public ControlCenterResult Draw(in AppletFrame frame, Rect screen, DisplayPreferences display,
        PearlSnapshot snapshot, ITalk talk, IClock clock, IWifeSync wife)
    {
        var scale = frame.Scale;
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
        var input = frame.Input;
        var theme = frame.Theme;
        var gold = theme.Palette.WarmAccent;

        paint.Fill(screen, theme.Palette.SurfaceSunken with { W = 0.52f * reveal });
        var topPad = StatusStrip.Height(scale) + scale * 4f;
        var fullHeight = MathF.Max(scale * 80f,
            screen.Height - topPad - SoftKeyBar.Height(scale) - scale * 8f);
        var sheet = Rect.FromSize(
            new Vector2(screen.Min.X + scale * 8f, screen.Min.Y + topPad - fullHeight * (1f - reveal)),
            new Vector2(screen.Width - scale * 16f, fullHeight));

        paint.PushClip(screen);
        try
        {
            paint.Fill(sheet, theme.Palette.SurfaceRaised with { W = 0.94f }, scale * 20f);
            paint.Stroke(sheet, gold with { W = 0.28f }, MathF.Max(1f, scale), scale * 20f);
            paint.Glow(sheet, gold with { W = 0.10f * reveal }, scale * 20f, scale * 10f);
            DrawGrip(paint, sheet.BottomSlice(scale * 14f), gold);

            // The tap that opens the sheet lands on the status strip, which is outside the
            // panel. Wait until the slide-in finishes, then consume leftover clicks so that
            // same press cannot also dismiss.
            if (open && reveal > 0.88f && !draggingLight && !draggingVolume && !sheetDrag &&
                !sheet.Contains(input.Pointer) && input.ConsumeClick(screen))
            {
                Close();
                return new ControlCenterResult(false, true, false, null, 0, string.Empty);
            }

            var live = open && reveal > 0.88f && !sheetDrag ? input : SilentInput.Instance;
            var inner = sheet.Inset(new Edges(scale * 12f, scale * 12f, scale * 12f, scale * 14f));
            var stack = new Stack(inner, StackAxis.Vertical, scale * 10f);
            var pending = new Pending();

            var gap = scale * 8f;
            var cols = 4;
            var tile = (inner.Width - gap * (cols - 1)) / cols;
            tile = MathF.Min(tile, scale * 78f);
            DrawGrid(paint, text, live, theme, stack.Take(tile * 2 + gap), tile, gap, display, snapshot, talk, pending);
            DrawLowerRow(paint, text, live, theme, stack.Take(tile), tile, gap, display, wife, pending);

            ShadeSlider.Draw(paint, live, theme, stack.Take(scale * 28f), display.Brightness, ref draggingLight,
                value => display.Brightness = value, sun: true);
            ShadeSlider.Draw(paint, live, theme, stack.Take(scale * 28f), display.Volume, ref draggingVolume,
                value => display.Volume = value, sun: false);

            DrawNotices(paint, text, live, theme, stack.Remaining, scale, snapshot, talk, clock, gold, pending);
            DragSheet(input, sheet, fullHeight, scale);

            var result = pending.Result;
            if (result.Closed || result.Pocket || result.Recents || result.Tab is not null ||
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

    private static void DrawGrid(IPaintSurface paint, ITextPainter text, IInputProbe input, ITheme theme, Rect area,
        float tile, float gap, DisplayPreferences display, PearlSnapshot snapshot, ITalk talk, Pending pending)
    {
        var signed = snapshot.SignedIn;
        var unread = talk.UnreadTotal + snapshot.UnreadTotal;
        DrawTile(paint, text, input, theme, Cell(area, tile, gap, 0, 0), Glyph.Wifi, "Pearlgate",
            signed ? NonEmpty(snapshot.MeWorld, "Signed in") : "Offline", signed,
            () => pending.Result = pending.Result with { Tab = DestinationTab.You });
        DrawTile(paint, text, input, theme, Cell(area, tile, gap, 1, 0), Glyph.Chat, "PearlChat",
            unread > 0 ? unread.ToString(CultureInfo.InvariantCulture) + " waiting" :
            signed ? "Connected" : "Away", signed,
            () => pending.Result = pending.Result with { Tab = DestinationTab.Social, Section = SocialPane.Messages });
        DrawTile(paint, text, input, theme, Cell(area, tile, gap, 2, 0), Glyph.Bell, "Notices",
            display.ShowMarks ? "On" : "Off", display.ShowMarks, () => display.ShowMarks = !display.ShowMarks);
        DrawTile(paint, text, input, theme, Cell(area, tile, gap, 3, 0), Glyph.Moon, "Quiet",
            display.Quiet ? "On" : "Off", display.Quiet, () => display.Quiet = !display.Quiet);

        DrawTile(paint, text, input, theme, Cell(area, tile, gap, 0, 1), Glyph.Mini, "Mini mode",
            "Pocket", false, () => pending.Result = pending.Result with { Pocket = true });
        DrawTile(paint, text, input, theme, Cell(area, tile, gap, 1, 1), Glyph.Camera, "Camera",
            "Ready", false, () => pending.Result = pending.Result with { AppletId = "camera" });
        DrawTile(paint, text, input, theme, Cell(area, tile, gap, 2, 1), Glyph.Music, "Music",
            "Broadcast", false, () => pending.Result = pending.Result with { AppletId = "music" });
        DrawTile(paint, text, input, theme, Cell(area, tile, gap, 3, 1), Glyph.After, "Afterdark",
            AppearanceLabel(display.Appearance), display.Appearance == AppearanceMode.Night, () =>
                display.Appearance = display.Appearance switch
                {
                    AppearanceMode.Day => AppearanceMode.Night,
                    AppearanceMode.Night => AppearanceMode.FollowClock,
                    _ => AppearanceMode.Day,
                });
    }

    private static void DrawLowerRow(IPaintSurface paint, ITextPainter text, IInputProbe input, ITheme theme,
        Rect area, float tile, float gap, DisplayPreferences display, IWifeSync wife, Pending pending)
    {
        DrawTile(paint, text, input, theme, Cell(area, tile, gap, 0, 0), Glyph.Glow, "Honorific",
            display.ShowWorld ? "On" : "Off", display.ShowWorld, () => display.ShowWorld = !display.ShowWorld);
        DrawTile(paint, text, input, theme, Cell(area, tile, gap, 1, 0), Glyph.Rotate, "Auto-rotate",
            display.AutoRotate ? "On" : "Off", display.AutoRotate, () =>
            {
                display.AutoRotate = !display.AutoRotate;
                if (!display.AutoRotate)
                {
                    display.Landscape = false;
                }
            });
        var wifeHint = !wife.IsPresent ? "No plugin" : wife.IsOn ? "Connected" : "Off";
        DrawTile(paint, text, input, theme, Cell(area, tile, gap, 2, 0), Glyph.Wife, "WIFI",
            wifeHint, wife.IsOn, () =>
            {
                if (wife.IsPresent)
                {
                    wife.SetOn(!wife.IsOn);
                }
            });

        var edit = Rect.FromSize(new Vector2(area.Min.X + (tile + gap) * 3f, area.Min.Y + (tile - tile * 0.42f) * 0.5f),
            new Vector2(tile * 0.42f, tile * 0.42f));
        var gold = theme.Palette.WarmAccent;
        paint.Stroke(edit, gold with { W = 0.45f }, MathF.Max(1f, tile * 0.03f), edit.Height * 0.28f);
        DrawPencil(paint, edit.Inset(edit.Width * 0.22f), gold);
        if (input.ConsumeClick(edit))
        {
            pending.Result = pending.Result with { Tab = DestinationTab.Settings };
        }
    }

    private void DrawNotices(IPaintSurface paint, ITextPainter text, IInputProbe input, ITheme theme, Rect area,
        float scale, PearlSnapshot snapshot, ITalk talk, IClock clock, Vector4 gold, Pending pending)
    {
        if (area.Height < scale * 36f)
        {
            return;
        }

        var head = area.TopSlice(scale * 18f);
        text.DrawIn(head, "Notifications", new TextStyle(FontRole.CaptionStrong, theme.Palette.Ink));
        var clear = head.RightSlice(scale * 64f);
        text.DrawIn(clear, "Clear all", new TextStyle(FontRole.Caption, gold, TextAlign.Right));
        if (input.ConsumeClick(clear))
        {
            ledger.Clear(snapshot, talk);
        }

        var list = area.Inset(new Edges(0f, scale * 22f, 0f, 0f));
        var rowH = scale * 52f;
        var gap = scale * 6f;
        var items = ledger.Visible(snapshot, talk, clock);
        var drawn = 0;
        for (var index = 0; index < items.Count && drawn < 3; index++)
        {
            var item = items[index];
            var row = RowAt(list, rowH, gap, drawn);
            if (row.Max.Y > list.Max.Y)
            {
                break;
            }

            DrawNotice(paint, text, theme, row, scale, GlyphFor(item.Kind), item.Title, item.Detail, item.When, gold);
            if (input.ConsumeClick(row))
            {
                pending.Result = pending.Result with { Tab = item.Tab, Section = item.Section };
            }

            drawn++;
        }

        if (drawn == 0)
        {
            text.DrawIn(list.TopSlice(scale * 20f), "Nothing waiting.",
                new TextStyle(FontRole.Caption, theme.Palette.InkMuted));
        }
    }

    private static Glyph GlyphFor(NoticeKind kind) => kind switch
    {
        NoticeKind.Chat => Glyph.Chat,
        NoticeKind.People => Glyph.Friend,
        _ => Glyph.Burst,
    };

    private static void DrawNotice(IPaintSurface paint, ITextPainter text, ITheme theme, Rect row, float scale,
        Glyph glyph, string title, string detail, string when, Vector4 gold)
    {
        paint.Fill(row, theme.Palette.SurfaceOverlay, scale * 12f);
        paint.Stroke(row, gold with { W = 0.16f }, MathF.Max(1f, scale * 0.8f), scale * 12f);
        var inset = row.Inset(new Edges(scale * 8f, scale * 6f, scale * 8f, scale * 6f));
        var mark = inset.LeftSlice(scale * 28f);
        DrawGlyph(paint, mark, glyph, gold);
        var copy = inset.Inset(new Edges(scale * 34f, 0f, 0f, 0f));
        text.DrawEllipsized(copy.TopSlice(scale * 16f), title,
            new TextStyle(FontRole.CaptionStrong, theme.Palette.Ink));
        text.DrawEllipsized(copy.Inset(new Edges(0f, scale * 16f, scale * 52f, 0f)), detail,
            new TextStyle(FontRole.Caption, theme.Palette.InkMuted));
        text.DrawIn(copy.RightSlice(scale * 48f).BottomSlice(scale * 14f), when,
            new TextStyle(FontRole.Caption, gold with { W = 0.8f }, TextAlign.Right));
    }

    private void DragSheet(IInputProbe input, Rect sheet, float fullHeight, float scale)
    {
        if (draggingLight || draggingVolume)
        {
            sheetTrack = false;
            sheetDrag = false;
            return;
        }

        if (!sheetTrack && open && reveal > 0.35f && input.WasPressed(sheet))
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

    private static void DrawGrip(IPaintSurface paint, Rect row, Vector4 gold)
    {
        var bar = Rect.FromSize(new Vector2(row.Center.X - row.Width * 0.08f, row.Center.Y - row.Height * 0.12f),
            new Vector2(row.Width * 0.16f, MathF.Max(3f, row.Height * 0.22f)));
        paint.Fill(bar, gold with { W = 0.42f }, bar.Height * 0.5f);
    }

    private static void DrawTile(IPaintSurface paint, ITextPainter text, IInputProbe input, ITheme theme, Rect area,
        Glyph glyph, string title, string detail, bool on, Action tap)
    {
        var gold = theme.Palette.WarmAccent;
        var radius = area.Width * 0.22f;
        paint.Fill(area, on ? gold with { W = 0.16f } : theme.Palette.SurfaceOverlay with { W = 0.72f }, radius);
        paint.Stroke(area, gold with { W = on ? 0.78f : 0.22f }, MathF.Max(1f, area.Width * 0.025f), radius);
        if (on)
        {
            paint.Glow(area, gold with { W = 0.22f }, radius, area.Width * 0.10f);
        }

        var inset = area.Inset(area.Width * 0.10f);
        var icon = inset.TopSlice(inset.Height * 0.46f);
        DrawGlyph(paint, icon, glyph, on ? gold : theme.Palette.Ink);
        text.DrawEllipsized(inset.Inset(new Edges(0f, inset.Height * 0.48f, 0f, inset.Height * 0.22f)), title,
            new TextStyle(FontRole.CaptionStrong, on ? gold : theme.Palette.Ink, TextAlign.Center));
        text.DrawEllipsized(inset.BottomSlice(inset.Height * 0.22f), detail,
            new TextStyle(FontRole.Caption, on ? gold with { W = 0.85f } : theme.Palette.InkMuted, TextAlign.Center));
        if (input.ConsumeClick(area))
        {
            tap();
        }
    }

    private static Rect Cell(Rect grid, float tile, float gap, int column, int row)
    {
        var origin = new Vector2(grid.Min.X + column * (tile + gap), grid.Min.Y + row * (tile + gap));
        return Rect.FromSize(origin, new Vector2(tile, tile));
    }

    private static Rect RowAt(Rect list, float height, float gap, int index) =>
        Rect.FromSize(new Vector2(list.Min.X, list.Min.Y + index * (height + gap)),
            new Vector2(list.Width, height));

    private static string AppearanceLabel(AppearanceMode mode) => mode switch
    {
        AppearanceMode.Day => "Daylight",
        AppearanceMode.Night => "Afterdark",
        _ => "Auto",
    };

    private static string NonEmpty(string value, string fallback) => value.Length > 0 ? value : fallback;

    private sealed class Pending
    {
        public ControlCenterResult Result = new(false, false, false, null, 0, string.Empty);
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
            default:
                DrawFriend(paint, area, ink);
                break;
        }
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

    private static void DrawPencil(IPaintSurface paint, Rect area, Vector4 ink)
    {
        var c = area.Center;
        var s = MathF.Min(area.Width, area.Height) * 0.42f;
        var shaft = MathF.Max(1.2f, s * 0.28f);
        paint.Line(c + new Vector2(-s * 0.72f, s * 0.42f), c + new Vector2(s * 0.38f, -s * 0.68f), ink, shaft);
        paint.Line(c + new Vector2(s * 0.18f, -s * 0.78f), c + new Vector2(s * 0.58f, -s * 0.38f), ink, shaft);
    }
}
