using System.Globalization;
using System.Text;
using Linkpearl.Applets;
using Linkpearl.Calendar;
using Linkpearl.Device.Shell;
using Linkpearl.Audio;
using Linkpearl.Badges;
using Linkpearl.Destinations;
using Linkpearl.Destinations.Home;
using Linkpearl.Destinations.Profile;
using Linkpearl.Geometry;
using Linkpearl.Layout;
using Linkpearl.Media;
using Linkpearl.Net;
using Linkpearl.Notices;
using Linkpearl.Painting;
using Linkpearl.Platform;
using Linkpearl.Preferences;
using Linkpearl.Talk;
using Linkpearl.Time;

namespace Linkpearl.Device.Shell.Studio;

internal sealed class StudioSurface
{
    private readonly IClock clock;
    private readonly IGameSession game;
    private readonly IPearlHub pearl;
    private readonly ITalk talk;
    private readonly DestinationHub hub;
    private readonly DisplayPreferences display;
    private readonly bool development;
    private readonly BadgeBook badges;
    private readonly NoticeLedger notices;
    private readonly ProfileChrome profile;
    private readonly Action<string, Rect> openApplet;
    private readonly StudioWeather sky;
    private readonly StudioHunt hunt = new();
    private readonly StudioMusicDock musicDock;
    private readonly List<(string Id, Rect Area)> widgetHits = [];
    private readonly List<(string Id, Rect Area)> appHits = [];
    private readonly GlassEdit glass;
    private float nameClock;
    private float jiggle;
    private string? pressId;
    private long pressAt;
    private Vector2 pressPoint;
    private string? dragId;
    private bool skipOpen;
    private int pageNudge;
    private long edgeAt;
    private int pickSlot = -1;
    private bool appsEdit;
    private int seat = -1;
    private Rect appsDockArea;

    public StudioSurface(IClock clock, IGameSession game, IPearlHub pearl, ITalk talk, DestinationHub hub,
        IWeatherOracle weather, DisplayPreferences display, bool development, BadgeBook badges, NoticeLedger notices,
        ProfileChrome profile, Action<string, Rect> openApplet, Action<Rect, string> openRadioStations, IHandsetAudio audio,
        IPublicRadio radio, IStationMarks marks, GlassEdit glass)
    {
        this.clock = clock;
        this.game = game;
        this.pearl = pearl;
        this.talk = talk;
        this.hub = hub;
        this.display = display;
        this.glass = glass;
        this.development = development;
        this.badges = badges;
        this.notices = notices;
        this.profile = profile;
        this.openApplet = openApplet;
        sky = new StudioWeather(game, clock, weather);
        musicDock = new StudioMusicDock(audio, radio, pearl, marks, openRadioStations);
    }

    public bool OverlayOpen => profile.OverlayOpen;

    public bool HuntOpen => hunt.IsOpen;

    public void DismissHunt() => hunt.Dismiss();

    public bool Editing => glass.Active || appsEdit;

    public string? FlyingId => dragId;

    public int PageNudge => pageNudge;

    public void ClearNudge() => pageNudge = 0;

    public void AdoptDrag(string id)
    {
        if (id.Length == 0)
        {
            return;
        }

        glass.Active = true;
        skipOpen = true;
        dragId = ContainsId(DisplayPreferences.DefaultStudioWidgets, id) ? "w:" + id : "a:" + id;
        pressId = dragId;
        pressAt = Environment.TickCount64;
        pressPoint = new Vector2(float.MinValue, 0f);
    }

    public void ReleaseDrag()
    {
        pressId = null;
        dragId = null;
    }

    public bool BlocksPager(Vector2 at, bool held)
    {
        if (hunt.IsOpen || musicDock.BlocksPager || pressId is not null || dragId is not null || pickSlot >= 0 ||
            appsEdit)
        {
            return true;
        }

        return held && HitsMoveable(at);
    }

    public bool Back()
    {
        if (hunt.IsOpen)
        {
            hunt.Dismiss();
            return true;
        }

        if (pickSlot >= 0)
        {
            pickSlot = -1;
            return true;
        }

        if (appsEdit)
        {
            appsEdit = false;
            return true;
        }

        if (glass.Active)
        {
            StopEdit();
            return true;
        }

        return profile.Back();
    }

    public float ComposeExtra(in AppletFrame frame, int extraIndex) =>
        Compose(frame, camera: null, extraIndex);

    public float Compose(in AppletFrame frame, IApplet? camera, int extraIndex = -1)
    {
        seat = extraIndex;
        display.RestoreStudioDocks();
        var content = frame.Content;
        nameClock += frame.DeltaSeconds;
        jiggle += frame.DeltaSeconds;
        var snapshot = pearl.Current;
        badges.Sync(snapshot.SignedIn && snapshot.FounderSeat > 0 && snapshot.FounderSeat <= FounderFaces.SeatLimit,
            game.JobName, development, GlassName.IsPatron(badges, snapshot, display, development));
        if (profile.OverlayOpen)
        {
            return profile.DrawOverlay(frame);
        }

        var hunting = hunt.IsOpen;
        if (!hunting)
        {
            TickDrag(frame, content);
        }

        widgetHits.Clear();
        appHits.Clear();

        var dockH = extraIndex < 0 ? HomeDock.Height(frame) : 0f;
        var dockLift = extraIndex < 0 ? frame.Units(3f) : 0f;
        var inset = content.Inset(new Edges(frame.Units(28f), frame.Units(44f), frame.Units(28f),
            dockH + dockLift + frame.Units(8f)));
        var gap = frame.Units(10f);
        var stack = new Stack(inset, StackAxis.Vertical, gap);
        if (extraIndex >= 0)
        {
            LayoutWidgets(frame, stack, gap);
            DrawFlying(frame);
            return content.Height;
        }

        // Results paint after widgets. Mute everything under the sheet so a result tap
        // cannot also open weather (or any other tile) on the same press.
        var behind = hunting ? frame.WithInput(SilentInput.Instance) : frame;
        HomeHeaderTools.Draw(behind, frame.Content, notices.Count(snapshot, talk, clock),
            display.Hushed(game.IsInDuty || game.IsInCutscene), out var notice, out var settings);
        if (!hunting && frame.Input.ConsumeClick(settings))
        {
            hub.Open(DestinationTab.Settings);
        }
        else if (!hunting && frame.Input.ConsumeClick(notice))
        {
            hub.Open(DestinationTab.Home, HomePane.Announcements);
        }

        var huntH = StudioHunt.BarHeight(frame);
        var huntBar = stack.Take(huntH);
        LayoutWidgets(behind, stack, gap);
        hunt.Draw(frame, huntBar, inset, pearl, talk, hub, openApplet);
        DrawFlying(behind);
        if (pickSlot >= 0 && !hunting)
        {
            DrawAppPicker(frame, inset);
        }
        var dock = content.BottomSlice(dockH + dockLift).Inset(new Edges(0f, 0f, 0f, dockLift));
        HomeDock.Draw(behind, dock, camera,
            display.Hushed(game.IsInDuty || game.IsInCutscene),
            () => openApplet("phone", content.BottomSlice(dockH + dockLift)),
            () => openApplet("camera", content.BottomSlice(dockH + dockLift)));
        var at = frame.Input.Pointer;
        var onChrome = huntBar.Contains(at) || notice.Contains(at) || settings.Contains(at) || dock.Contains(at);
        if (!hunting && (appsEdit || pickSlot >= 0) && !skipOpen && !appsDockArea.Contains(at) &&
            frame.Input.ConsumeClick(content))
        {
            appsEdit = false;
            pickSlot = -1;
        }
        else if (!hunting && glass.Active && !appsEdit && dragId is null && !skipOpen && !onChrome &&
                 frame.Input.ConsumeClick(content))
        {
            StopEdit();
        }

        if (!frame.Input.IsHeld())
        {
            skipOpen = false;
        }

        return content.Height;
    }

    private void TickDrag(in AppletFrame frame, Rect body)
    {
        var held = frame.Input.IsHeld();
        if (!held)
        {
            if (dragId is not null)
            {
                ResolveDrop(frame.Input.Pointer);
            }

            pressId = null;
            dragId = null;
            edgeAt = 0;
            return;
        }

        if (held && dragId is not null)
        {
            WatchEdge(frame, body);
        }

        if (pressId is not { } holding)
        {
            return;
        }

        if (string.Equals(holding, "w:music", StringComparison.Ordinal) && musicDock.BlocksPager)
        {
            return;
        }

        var travel = (frame.Input.Pointer - pressPoint).Length();
        var armed = Environment.TickCount64 - pressAt >= 480;
        var appsHold = string.Equals(holding, "w:apps", StringComparison.Ordinal);
        if (appsHold && appsEdit)
        {
            return;
        }

        if (travel > frame.Units(6f))
        {
            if (glass.Active || armed)
            {
                glass.Active = true;
                appsEdit = false;
                dragId = holding;
                pickSlot = -1;
                skipOpen = true;
            }

            return;
        }

        if (!armed)
        {
            return;
        }

        skipOpen = true;
        if (appsHold)
        {
            appsEdit = true;
            return;
        }

        glass.Active = true;
    }

    private void ResolveDrop(Vector2 pointer)
    {
        if (dragId is not { Length: > 0 } moving)
        {
            return;
        }

        if (moving.StartsWith("w:", StringComparison.Ordinal))
        {
            var widget = moving[2..];
            display.PlaceStudioWidget(widget, seat);
            var inside = Hit(widgetHits, pointer, moving, pad: false);
            var rowHit = HitRow(widgetHits, pointer, moving);
            var tail = TailBelow(widgetHits, pointer, moving);
            var target = inside ?? rowHit ?? tail ?? Nearest(widgetHits, pointer, moving);
            if (target is not { Length: > 2 })
            {
                return;
            }

            var neighbor = target[2..];
            WidgetDrop side;
            if (tail is not null && inside is null && rowHit is null)
            {
                side = WidgetDrop.Below;
            }
            else if (inside is null && rowHit is not null)
            {
                side = RowDrop(widgetHits, target, pointer);
            }
            else
            {
                side = ReadDrop(widgetHits, target, pointer);
            }

            if (string.Equals(neighbor, "apps", StringComparison.Ordinal) &&
                side is WidgetDrop.Left or WidgetDrop.Right)
            {
                side = AppsDropSide(widgetHits, target, pointer, side);
            }

            if (side is WidgetDrop.Left or WidgetDrop.Right)
            {
                display.SeatStudioWidget(widget, neighbor, side == WidgetDrop.Left);
                return;
            }

            display.StackStudioWidget(widget, neighbor, side == WidgetDrop.Above);
            return;
        }

        if (moving.StartsWith("a:", StringComparison.Ordinal))
        {
            var app = moving[2..];
            var target = Hit(appHits, pointer, moving);
            if (target is not { Length: > 2 })
            {
                return;
            }

            display.ReplaceStudioApp(app, IndexOnHome(target[2..]));
        }
    }

    private static string? Hit(List<(string Id, Rect Area)> cells, Vector2 pointer, string skip, bool pad = false)
    {
        string? found = null;
        var best = float.MaxValue;
        for (var index = 0; index < cells.Count; index++)
        {
            var cell = cells[index];
            var area = pad ? cell.Area.Expand(cell.Area.Height * 0.18f) : cell.Area;
            if (string.Equals(cell.Id, skip, StringComparison.Ordinal) || !area.Contains(pointer))
            {
                continue;
            }

            var distance = (cell.Area.Center - pointer).LengthSquared();
            if (distance >= best)
            {
                continue;
            }

            best = distance;
            found = cell.Id;
        }

        return found;
    }

    private static string? Nearest(List<(string Id, Rect Area)> cells, Vector2 pointer, string skip)
    {
        string? found = null;
        var best = float.MaxValue;
        for (var index = 0; index < cells.Count; index++)
        {
            var cell = cells[index];
            if (string.Equals(cell.Id, skip, StringComparison.Ordinal))
            {
                continue;
            }

            var distance = (cell.Area.Center - pointer).LengthSquared();
            if (distance >= best)
            {
                continue;
            }

            best = distance;
            found = cell.Id;
        }

        return found;
    }

    private static WidgetDrop DropSideOf(Rect area, Vector2 pointer)
    {
        var across = area.Width > 1f ? (pointer.X - area.Min.X) / area.Width : 0.5f;
        var down = area.Height > 1f ? (pointer.Y - area.Min.Y) / area.Height : 0.5f;
        if (down <= 0.30f)
        {
            return WidgetDrop.Above;
        }

        if (down >= 0.70f)
        {
            return WidgetDrop.Below;
        }

        if (across <= 0.42f)
        {
            return WidgetDrop.Left;
        }

        if (across >= 0.58f)
        {
            return WidgetDrop.Right;
        }

        return down < 0.5f ? WidgetDrop.Above : WidgetDrop.Below;
    }

    private static WidgetDrop RowDrop(List<(string Id, Rect Area)> cells, string target, Vector2 pointer)
    {
        for (var index = 0; index < cells.Count; index++)
        {
            if (!string.Equals(cells[index].Id, target, StringComparison.Ordinal))
            {
                continue;
            }

            var area = cells[index].Area;
            if (pointer.Y < area.Min.Y)
            {
                return WidgetDrop.Above;
            }

            if (pointer.Y > area.Max.Y)
            {
                return WidgetDrop.Below;
            }

            return pointer.X < area.Center.X ? WidgetDrop.Left : WidgetDrop.Right;
        }

        return WidgetDrop.Below;
    }

    private static string? TailBelow(List<(string Id, Rect Area)> cells, Vector2 pointer, string skip)
    {
        string? last = null;
        var bottom = float.MinValue;
        for (var index = 0; index < cells.Count; index++)
        {
            var cell = cells[index];
            if (string.Equals(cell.Id, skip, StringComparison.Ordinal))
            {
                continue;
            }

            if (cell.Area.Max.Y > bottom)
            {
                bottom = cell.Area.Max.Y;
                last = cell.Id;
            }
        }

        return last is not null && pointer.Y > bottom ? last : null;
    }

    private static string? HitRow(List<(string Id, Rect Area)> cells, Vector2 pointer, string skip)
    {
        string? found = null;
        var best = float.MaxValue;
        for (var index = 0; index < cells.Count; index++)
        {
            var cell = cells[index];
            if (string.Equals(cell.Id, skip, StringComparison.Ordinal))
            {
                continue;
            }

            var slop = cell.Area.Height * 0.28f;
            if (pointer.Y < cell.Area.Min.Y - slop || pointer.Y > cell.Area.Max.Y + slop)
            {
                continue;
            }

            var distance = MathF.Abs(cell.Area.Center.X - pointer.X);
            if (distance >= best)
            {
                continue;
            }

            best = distance;
            found = cell.Id;
        }

        return found;
    }

    private static WidgetDrop SideByX(List<(string Id, Rect Area)> cells, string target, Vector2 pointer)
    {
        for (var index = 0; index < cells.Count; index++)
        {
            if (string.Equals(cells[index].Id, target, StringComparison.Ordinal))
            {
                return pointer.X < cells[index].Area.Center.X ? WidgetDrop.Left : WidgetDrop.Right;
            }
        }

        return WidgetDrop.Right;
    }

    private static WidgetDrop AppsDropSide(List<(string Id, Rect Area)> cells, string target, Vector2 pointer,
        WidgetDrop fallback)
    {
        for (var index = 0; index < cells.Count; index++)
        {
            if (!string.Equals(cells[index].Id, target, StringComparison.Ordinal))
            {
                continue;
            }

            var area = cells[index].Area;
            var span = area.Width;
            var along = span > 1f ? (pointer.X - area.Min.X) / span : 0.5f;
            if (along <= 0.48f)
            {
                return WidgetDrop.Left;
            }

            if (along >= 0.52f)
            {
                return WidgetDrop.Right;
            }

            return fallback;
        }

        return fallback;
    }

    private static WidgetDrop ReadDrop(List<(string Id, Rect Area)> cells, string target, Vector2 pointer)
    {
        for (var index = 0; index < cells.Count; index++)
        {
            if (string.Equals(cells[index].Id, target, StringComparison.Ordinal))
            {
                return DropSideOf(cells[index].Area, pointer);
            }
        }

        return WidgetDrop.Below;
    }

    private void LayoutWidgets(in AppletFrame frame, Stack stack, float gap)
    {
        var order = new List<string>(display.StudioWidgets.Count);
        for (var index = 0; index < display.StudioWidgets.Count; index++)
        {
            var id = display.StudioWidgets[index];
            if (display.StudioWidgetSeat(id) == seat)
            {
                order.Add(id);
            }
        }

        var rows = new List<string[]>(order.Count);
        for (var index = 0; index < order.Count; index++)
        {
            var id = order[index];
            if (index + 1 < order.Count && display.StudioWidgetHalf(id) &&
                display.StudioWidgetHalf(order[index + 1]))
            {
                rows.Add([id, order[index + 1]]);
                index++;
                continue;
            }

            rows.Add([id]);
        }

        var leftover = stack.Remaining.Height;
        var musicH = StudioMusicDock.Height(frame);
        var musicRows = 0;
        for (var index = 0; index < rows.Count; index++)
        {
            if (rows[index].Length == 1 && string.Equals(rows[index][0], "music", StringComparison.Ordinal))
            {
                musicRows++;
            }
        }

        var flex = rows.Count - musicRows;
        var body = leftover - musicH * musicRows - gap * Math.Max(0, rows.Count - 1);
        var rowH = flex > 0 ? MathF.Max(frame.Units(64f), body / flex) : musicH;
        for (var index = 0; index < rows.Count; index++)
        {
            var row = rows[index];
            var music = row.Length == 1 && string.Equals(row[0], "music", StringComparison.Ordinal);
            var area = stack.Take(music ? musicH : rowH);
            if (row.Length == 2)
            {
                var half = MathF.Max(0f, (area.Width - gap) * 0.5f);
                PlaceWidget(frame, row[0], area.LeftSlice(half));
                PlaceWidget(frame, row[1], area.RightSlice(half));
                continue;
            }

            PlaceWidget(frame, row[0], area);
        }
    }

    private void PlaceWidget(in AppletFrame frame, string id, Rect area)
    {
        var key = "w:" + id;
        widgetHits.Add((key, area));
        if (string.Equals(dragId, key, StringComparison.Ordinal))
        {
            return;
        }

        if (!string.Equals(id, "music", StringComparison.Ordinal) || glass.Active || appsEdit)
        {
            if (!(string.Equals(id, "music", StringComparison.Ordinal) && musicDock.BlocksPager))
            {
                WatchPress(frame, area, key);
            }
        }
        var open = !glass.Active && !appsEdit && !skipOpen;
        var drawn = glass.Active ? area.Translate(EditSway(jiggle, area.Min.X, frame.Units(0.4f))) : area;
        var hover = open && dragId is null && LiftsOnHover(id) && frame.Input.IsHovering(drawn);
        if (hover)
        {
            drawn = Lift(drawn, 1.06f);
        }

        DrawWidget(frame, id, drawn, open);
        if (hover)
        {
            var radius = MathF.Max(frame.Units(18f), MathF.Min(drawn.Width, drawn.Height) * 0.12f);
            frame.Paint.Fill(drawn, new Vector4(1f, 1f, 1f, 0.08f), radius);
            frame.Paint.Stroke(drawn, new Vector4(1f, 1f, 1f, 0.28f), frame.Theme.Metrics.Hairline, radius);
        }
        if (appsEdit && string.Equals(id, "apps", StringComparison.Ordinal))
        {
            frame.Paint.Stroke(drawn, frame.Theme.Palette.WarmAccent with { W = 0.35f },
                frame.Theme.Metrics.Hairline, frame.Units(10f));
        }
        else if (glass.Active && dragId is null)
        {
            frame.Paint.Stroke(drawn, frame.Theme.Palette.WarmAccent with { W = 0.22f },
                frame.Theme.Metrics.Hairline, frame.Units(10f));
        }
        else if (glass.Active && dragId is { Length: > 2 } flying &&
                 flying.StartsWith("w:", StringComparison.Ordinal) &&
                 !string.Equals(flying, key, StringComparison.Ordinal) &&
                 area.Contains(frame.Input.Pointer))
        {
            var gold = frame.Theme.Palette.WarmAccent with { W = 0.20f };
            var side = string.Equals(id, "apps", StringComparison.Ordinal)
                ? AppsDropSide(widgetHits, key, frame.Input.Pointer, DropSideOf(area, frame.Input.Pointer))
                : DropSideOf(area, frame.Input.Pointer);
            if (side == WidgetDrop.Left)
            {
                frame.Paint.Fill(area.LeftSlice(area.Width * 0.5f), gold, frame.Units(10f));
            }
            else if (side == WidgetDrop.Right)
            {
                frame.Paint.Fill(area.RightSlice(area.Width * 0.5f), gold, frame.Units(10f));
            }
            else if (side == WidgetDrop.Above)
            {
                frame.Paint.Fill(area.TopSlice(area.Height * 0.5f), gold, frame.Units(10f));
            }
            else
            {
                frame.Paint.Fill(area.BottomSlice(area.Height * 0.5f), gold, frame.Units(10f));
            }
        }
    }

    private void DrawWidget(in AppletFrame frame, string id, Rect area, bool open)
    {
        switch (id)
        {
            case "calendar":
                DrawCalendar(frame, area, open);
                return;
            case "announcements":
                DrawAnnouncements(frame, area, open);
                return;
            case "apps":
                DrawApps(frame, area, open);
                return;
            case "weather":
                sky.Draw(open ? frame : frame.WithInput(SilentInput.Instance), area,
                    rect => openApplet("weather", rect));
                return;
            case "music":
                try
                {
                    musicDock.Draw(open ? frame : frame.WithInput(SilentInput.Instance), area);
                }
                catch
                {
                }

                return;
        }
    }

    private void DrawFlying(in AppletFrame frame)
    {
        if (dragId is not { Length: > 2 } flying)
        {
            return;
        }

        if (flying.StartsWith("w:", StringComparison.Ordinal))
        {
            var size = new Vector2(frame.Units(168f), frame.Units(110f));
            for (var index = 0; index < widgetHits.Count; index++)
            {
                if (string.Equals(widgetHits[index].Id, flying, StringComparison.Ordinal))
                {
                    size = widgetHits[index].Area.Size;
                    break;
                }
            }

            DrawWidget(frame, flying[2..], Rect.FromSize(frame.Input.Pointer - size * 0.5f, size), false);
            return;
        }

        if (!flying.StartsWith("a:", StringComparison.Ordinal))
        {
            return;
        }

        var tile = new Vector2(frame.Units(64f), frame.Units(72f));
        for (var index = 0; index < appHits.Count; index++)
        {
            if (string.Equals(appHits[index].Id, flying, StringComparison.Ordinal))
            {
                tile = appHits[index].Area.Size;
                break;
            }
        }

        DrawApp(frame, Rect.FromSize(frame.Input.Pointer - tile * 0.5f, tile), flying[2..], TitleOf(flying[2..]),
            0, static () => { }, false);
    }

    private void WatchPress(in AppletFrame frame, Rect area, string id)
    {
        if (frame.Input.WasPressed(area))
        {
            pressId = id;
            pressAt = Environment.TickCount64;
            pressPoint = frame.Input.Pointer;
        }
    }

    private bool HitsMoveable(Vector2 at)
    {
        for (var index = 0; index < widgetHits.Count; index++)
        {
            if (widgetHits[index].Area.Contains(at))
            {
                return true;
            }
        }

        for (var index = 0; index < appHits.Count; index++)
        {
            if (appHits[index].Area.Contains(at))
            {
                return true;
            }
        }

        return false;
    }

    private void WatchEdge(in AppletFrame frame, Rect body)
    {
        var edge = frame.Units(28f);
        var at = frame.Input.Pointer;
        var side = 0;
        if (at.X >= body.Max.X - edge)
        {
            side = 1;
        }
        else if (at.X <= body.Min.X + edge)
        {
            side = -1;
        }

        if (side == 0)
        {
            edgeAt = 0;
            return;
        }

        if (edgeAt == 0)
        {
            edgeAt = Environment.TickCount64;
        }

        if (Environment.TickCount64 - edgeAt < 280)
        {
            return;
        }

        pageNudge = side;
        edgeAt = Environment.TickCount64;
    }

    private static bool ContainsId(IReadOnlyList<string> ids, string id)
    {
        for (var index = 0; index < ids.Count; index++)
        {
            if (string.Equals(ids[index], id, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private void StopEdit()
    {
        glass.Active = false;
        appsEdit = false;
        pressId = null;
        dragId = null;
        skipOpen = false;
        pickSlot = -1;
    }

    // Identity stay on ProfileChrome for social surfaces. This page no longer hosts the card.
    private void DrawGreeting(in AppletFrame frame, Rect row, float calendarRight)
    {
        var snapshot = pearl.Current;
        var linked = ShownName.Linked(game.Character.Name, snapshot.MeName);
        var patron = GlassName.IsPatron(badges, snapshot, display, development);
        var name = GlassName.ProfileName(display, linked, patron);
        if (name.Length == 0)
        {
            name = "Linkpearl";
        }

        var title = GlassName.Honorific(display);
        var ink = frame.Theme.Palette.Ink;
        var muted = frame.Theme.Palette.InkMuted;
        var gold = frame.Theme.Palette.WarmAccent;
        var hour = clock.Now.Hour;
        var hello = hour < 12 ? "Good morning," : hour < 17 ? "Good afternoon," : "Good evening,";
        HomeHeaderTools.Draw(frame, frame.Content, notices.Count(snapshot, talk, clock),
            display.Hushed(game.IsInDuty || game.IsInCutscene), out var notice, out var settings);

        if (frame.Input.ConsumeClick(settings))
        {
            hub.Open(DestinationTab.Settings);
        }
        else if (frame.Input.ConsumeClick(notice))
        {
            hub.Open(DestinationTab.Home, HomePane.Announcements);
        }

        var helloRow = new Rect(
            new Vector2(frame.Content.Min.X + frame.Units(18f) + frame.Units(2f), row.Min.Y),
            new Vector2(notice.Min.X - frame.Units(8f), row.Min.Y + frame.Units(22f)));
        frame.Text.DrawEllipsized(helloRow, hello,
            new TextStyle(FontRole.Caption, muted, TextAlign.Left, 1f, 1.13f));

        var identity = new Rect(new Vector2(row.Min.X, helloRow.Max.Y + frame.Units(2f)),
            new Vector2(notice.Min.X - frame.Units(8f), row.Max.Y));
        var nameScale = 0.78f;
        var nameH = frame.Text.Measure("Ag", FontRole.Display).Y * nameScale + frame.Units(1f);
        var titleScale = 1.25f;
        var titleH = title.Length > 0 ? frame.Text.Measure("Ag", FontRole.CaptionStrong).Y * titleScale : 0f;
        var titleGap = title.Length > 0 ? frame.Units(2f) : 0f;
        var badgeH = frame.Units(22f);
        var badgeGap = frame.Units(3f);
        var copyLeft = identity.Min.X + MathF.Min(identity.Height, frame.Units(79.2f)) + frame.Units(8f);
        var nameRight = MathF.Max(copyLeft, notice.Min.X - frame.Units(8f));
        var nameWidth = MathF.Max(0f, nameRight - copyLeft);
        SplitName(frame, name, nameWidth, FontRole.Display, nameScale, out var nameOne, out var nameTwo);
        var nameLines = nameTwo.Length > 0 ? 2 : 1;
        var nameBlockH = nameH * nameLines;
        var stackH = nameBlockH + titleH + titleGap + badgeGap + badgeH;
        var face = MathF.Min(identity.Height, frame.Units(79.2f));
        var clusterH = MathF.Min(identity.Height, MathF.Max(face, stackH));
        var cluster = Rect.FromSize(identity.Min, new Vector2(identity.Width, clusterH));
        var portrait = Rect.FromSize(cluster.Min, new Vector2(face, face));
        DrawPortrait(frame, portrait);
        copyLeft = portrait.Max.X + frame.Units(8f);
        var copyWidth = MathF.Max(0f, calendarRight - copyLeft);
        nameRight = MathF.Max(copyLeft, notice.Min.X - frame.Units(8f));
        nameWidth = MathF.Max(0f, nameRight - copyLeft);
        var copyTop = portrait.Min.Y;
        var nameRow = Rect.FromSize(new Vector2(copyLeft, copyTop), new Vector2(nameWidth, nameBlockH));
        var look = MarkLook.ForName(display, ink);
        var honor = patron && (display.NameGlow || display.NameInkCustom);
        DrawNameLine(frame, nameRow.TopSlice(nameH), nameOne, look, honor, ink, nameScale);
        if (nameTwo.Length > 0)
        {
            DrawNameLine(frame, nameRow.BottomSlice(nameH), nameTwo, look, honor, ink, nameScale);
        }

        var cursor = nameRow.Max.Y;
        if (title.Length > 0)
        {
            var titleRow = Rect.FromSize(new Vector2(copyLeft, cursor + titleGap), new Vector2(nameWidth, titleH));
            DrawHonorName(frame, titleRow, title, MarkLook.ForTitle(display), FontRole.CaptionStrong, titleScale);
            cursor = titleRow.Max.Y;
        }

        var calendarGap = frame.Units(8f);
        var slot = MathF.Floor(badgeH);
        var rowY = MathF.Round(cursor + badgeGap);
        var editRight = calendarRight - calendarGap;
        var edit = new Rect(
            new Vector2(copyLeft, rowY),
            new Vector2(MathF.Min(copyLeft + frame.Units(124f), editRight), rowY + slot));
        profile.DrawStudioEdit(frame, edit);
    }

    private void DrawPortrait(in AppletFrame frame, Rect area)
    {
        var gold = frame.Theme.Palette.WarmAccent;
        var side = MathF.Min(area.Width, area.Height);
        var square = Rect.FromSize(area.Center - new Vector2(side * 0.5f), new Vector2(side));
        var radius = side * 0.5f;
        if (badges.PortraitFile.Length > 0)
        {
            var texture = frame.Textures.FromFile(PortraitFiles.Absolute(frame.Paths, badges.PortraitFile));
            if (texture is { IsReady: true })
            {
                var crop = CoverFit.Framed(texture.Size, square.Size, badges.PortraitZoom, badges.PortraitFocus);
                frame.Paint.ImageRounded(texture, square, crop.Min, crop.Max, Vector4.One, radius);
                frame.Paint.StrokeCircle(square.Center, radius, gold with { W = 0.70f },
                    MathF.Max(1.1f, frame.Units(1.3f)));
                return;
            }
        }

        if (game.JobIconId != 0)
        {
            var job = frame.Textures.GameIcon(game.JobIconId);
            if (job is { IsReady: true })
            {
                frame.Paint.FillCircle(square.Center, radius, frame.Theme.Palette.SurfaceRaised with { W = 0.40f });
                frame.Paint.Image(job, CoverFit.Contained(job.Size, square.Inset(frame.Units(6f))), Vector4.One);
                frame.Paint.StrokeCircle(square.Center, radius, gold with { W = 0.70f },
                    MathF.Max(1.1f, frame.Units(1.3f)));
                return;
            }
        }

        frame.Paint.FillCircle(square.Center, radius, frame.Theme.Palette.SurfaceRaised with { W = 0.40f });
        StudioMarks.Draw(frame.Paint, square.Inset(side * 0.22f), StudioMark.Person, gold);
        frame.Paint.StrokeCircle(square.Center, radius, gold with { W = 0.70f },
            MathF.Max(1.1f, frame.Units(1.3f)));
    }

    private void DrawNameLine(in AppletFrame frame, Rect area, string text, in MarkLook look, bool honor,
        Vector4 ink, float scale)
    {
        if (honor)
        {
            DrawHonorName(frame, area, text, look, FontRole.Display, scale);
            return;
        }

        frame.Text.DrawEllipsized(area, text,
            new TextStyle(FontRole.Display, ink, TextAlign.Left, 1f, scale));
    }

    private static void SplitName(in AppletFrame frame, string name, float width, FontRole role, float scale,
        out string first, out string second)
    {
        first = name;
        second = string.Empty;
        if (width < 1f || MeasureName(frame, name, role, scale) <= width)
        {
            return;
        }

        var split = -1;
        for (var index = name.Length - 1; index >= 0; index--)
        {
            if (name[index] != ' ')
            {
                continue;
            }

            if (MeasureName(frame, name[..index], role, scale) <= width)
            {
                split = index;
                break;
            }
        }

        if (split < 0)
        {
            var taken = 0f;
            var end = 0;
            foreach (var rune in name.EnumerateRunes())
            {
                var next = taken + frame.Text.Measure(rune.ToString(), role).X * scale;
                if (end > 0 && next > width)
                {
                    break;
                }

                taken = next;
                end += rune.Utf16SequenceLength;
            }

            if (end <= 0 || end >= name.Length)
            {
                return;
            }

            first = name[..end];
            second = name[end..];
            return;
        }

        first = name[..split];
        second = name[(split + 1)..];
    }

    private static float MeasureName(in AppletFrame frame, string text, FontRole role, float scale)
    {
        var width = 0f;
        foreach (var rune in text.EnumerateRunes())
        {
            width += frame.Text.Measure(rune.ToString(), role).X * scale;
        }

        return width;
    }

    private void DrawHonorName(in AppletFrame frame, Rect area, string text, in MarkLook look, FontRole role,
        float markScale)
    {
        if (area.Width < 1f || text.Length == 0)
        {
            return;
        }

        var unit = frame.Units(1f);
        var count = 0;
        foreach (var _ in text.EnumerateRunes())
        {
            count++;
        }

        if (count == 0)
        {
            return;
        }

        frame.Paint.PushClip(area);
        var x = area.Min.X;
        var index = 0;
        foreach (var rune in text.EnumerateRunes())
        {
            var glyph = rune.ToString();
            var size = frame.Text.Measure(glyph, role) * markScale;
            if (x + size.X > area.Max.X && index > 0)
            {
                break;
            }

            var cell = Rect.FromSize(new Vector2(x, area.Min.Y), new Vector2(size.X, area.Height));
            var fill = TitleFx.Fill(look, nameClock, index, count) with { W = 1f };
            var glow = TitleFx.Glow(look, nameClock, index, count);
            frame.Text.DrawFitted(cell, glyph,
                new TextStyle(role, fill, TextAlign.Left, 1f, markScale, glow, TitleFx.Spread(look, unit)));
            x += size.X;
            index++;
        }

        frame.Paint.PopClip();
    }

    private void DrawCalendar(in AppletFrame frame, Rect row, bool open)
    {
        var bells = EorzeaTime.FromUnix(clock.UtcNow.ToUnixTimeSeconds());
        CalendarChrome.Dock(open ? frame : frame.WithInput(SilentInput.Instance), row, clock.Now, bells);
        if (!display.Hushed(game.IsInDuty || game.IsInCutscene))
        {
            AppMarks.DrawCount(frame, row, notices.AppBadge("calendar", talk));
        }

        if (open && frame.Input.ConsumeClick(row))
        {
            openApplet("calendar", row);
        }
    }

    private void DrawAnnouncements(in AppletFrame frame, Rect area, bool open)
    {
        var snapshot = pearl.Current;
        var notices = snapshot.Announcements ?? [];
        string title;
        string body;
        var when = string.Empty;
        if (notices.Length > 0)
        {
            title = notices[0].Title;
            body = notices[0].Body;
            when = AnnouncementChrome.Ago(notices[0].CreatedAtUnix, clock.Now);
        }
        else if (!snapshot.SignedIn)
        {
            title = "Link to Pearlgate";
            body = "Sign in to read the latest notes from Linkpearl.";
        }
        else
        {
            title = "No announcements";
            body = "When Pearlgate posts, the latest note will sit here.";
        }

        AnnouncementChrome.Dock(open ? frame : frame.WithInput(SilentInput.Instance), area, title, body, when,
            Math.Max(0, notices.Length - 1), clock.Now);
        if (!display.Hushed(game.IsInDuty || game.IsInCutscene))
        {
            AppMarks.DrawCount(frame, area, this.notices.AppBadge("announcements", talk));
        }

        if (open && frame.Input.ConsumeClick(area))
        {
            hub.Open(DestinationTab.Home, HomePane.Announcements);
        }
    }

    private void DrawApps(in AppletFrame frame, Rect area, bool open)
    {
        appsDockArea = area;
        var grid = new TileGrid(area, 3, 2, frame.Units(4f));
        var ids = display.StudioApps;
        for (var index = 0; index < 6; index++)
        {
            var cell = grid.Cell(index % 3, index / 3);
            var id = index < ids.Count ? ids[index] : string.Empty;
            DrawHomeApp(frame, cell, id, index, open);
        }
    }

    private void DrawHomeApp(in AppletFrame frame, Rect cell, string appletId, int slot, bool open)
    {
        if (appletId.Length > 0 && !display.CanPlaceHomeApp(appletId))
        {
            appletId = string.Empty;
        }

        if (appletId.Length == 0)
        {
            if (appsEdit)
            {
                frame.Paint.Stroke(cell.Inset(frame.Units(2f)),
                    pickSlot == slot
                        ? frame.Theme.Palette.WarmAccent
                        : frame.Theme.Palette.WarmAccent with { W = 0.28f },
                    frame.Units(1.4f), frame.Units(10f));
                if (pickSlot < 0 && !skipOpen && frame.Input.ConsumeClick(cell))
                {
                    pickSlot = slot;
                    skipOpen = true;
                }
            }

            return;
        }

        var key = "a:" + appletId;
        appHits.Add((key, cell));
        if (string.Equals(dragId, key, StringComparison.Ordinal))
        {
            return;
        }

        var drawn = appsEdit ? cell.Translate(EditSway(jiggle, cell.Min.X + cell.Min.Y, frame.Units(0.35f))) : cell;
        var dropLit = dragId is { Length: > 2 } && dragId.StartsWith("a:", StringComparison.Ordinal) &&
                      cell.Contains(frame.Input.Pointer);
        DrawApp(frame, drawn, appletId, TitleOf(appletId),
            display.Hushed(game.IsInDuty || game.IsInCutscene) ? 0 : notices.AppBadge(appletId, talk),
            () => OpenHomeApp(appletId, cell), open);
        if (appsEdit)
        {
            frame.Paint.Stroke(drawn.Inset(frame.Units(2f)),
                pickSlot == slot
                    ? frame.Theme.Palette.WarmAccent
                    : frame.Theme.Palette.WarmAccent with { W = 0.28f },
                frame.Units(1.4f), frame.Units(10f));
        }

        if (dropLit)
        {
            frame.Paint.Stroke(drawn.Inset(frame.Units(2f)), frame.Theme.Palette.WarmAccent,
                frame.Units(2f), frame.Units(10f));
        }

        if (appsEdit && pickSlot < 0 && !skipOpen && frame.Input.ConsumeClick(cell))
        {
            pickSlot = slot;
            skipOpen = true;
        }
    }

    private void DrawAppPicker(in AppletFrame frame, Rect area)
    {
        var sheet = area.Inset(new Edges(0f, frame.Units(36f), 0f, 0f));
        frame.Paint.Fill(sheet, frame.Theme.Palette.SurfaceRaised with { W = 0.97f }, frame.Units(14f));
        frame.Paint.Stroke(sheet, frame.Theme.Palette.WarmAccent with { W = 0.4f }, frame.Theme.Metrics.Hairline,
            frame.Units(14f));
        var inner = sheet.Inset(frame.Units(12f));
        var title = inner.TopSlice(frame.Units(20f));
        frame.Text.DrawIn(title, "Replace with",
            new TextStyle(FontRole.CaptionStrong, frame.Theme.Palette.Ink));
        var gridArea = inner.Inset(new Edges(0f, frame.Units(26f), 0f, 0f));
        var choices = HomeAppChoices();
        var columns = 4;
        var cell = frame.Units(64f);
        var gap = frame.Units(8f);
        for (var index = 0; index < choices.Count; index++)
        {
            var col = index % columns;
            var row = index / columns;
            var tile = Rect.FromSize(
                new Vector2(gridArea.Min.X + col * (gridArea.Width / columns), gridArea.Min.Y + row * (cell + gap)),
                new Vector2(gridArea.Width / columns, cell));
            if (tile.Min.Y > gridArea.Max.Y)
            {
                break;
            }

            var id = choices[index];
            DrawApp(frame, tile, id, TitleOf(id), 0, () => { }, false);
            if (frame.Input.ConsumeClick(tile))
            {
                display.ReplaceStudioApp(id, pickSlot);
                pickSlot = -1;
                skipOpen = true;
            }
        }

        if (frame.Input.ConsumeClick(sheet))
        {
            skipOpen = true;
        }
    }

    private List<string> HomeAppChoices()
    {
        var choices = new List<string>();
        AddHomeChoice(choices, "party");
        var owned = display.OwnedApps;
        for (var index = 0; index < owned.Count; index++)
        {
            AddHomeChoice(choices, owned[index]);
        }

        return choices;
    }

    private void AddHomeChoice(List<string> choices, string id)
    {
        if (id.Length == 0 || id.StartsWith("folder:", StringComparison.Ordinal) || choices.Contains(id))
        {
            return;
        }

        if (display.CanPlaceHomeApp(id))
        {
            choices.Add(id);
        }
    }

    private int IndexOnHome(string id)
    {
        var ids = display.StudioApps;
        for (var index = 0; index < ids.Count; index++)
        {
            if (string.Equals(ids[index], id, StringComparison.Ordinal))
            {
                return index;
            }
        }

        return 0;
    }

    private void OpenHomeApp(string appletId, Rect cell)
    {
        if (AppShelf.Find(appletId) is AppSpec spec)
        {
            if (spec.Kind == AppKind.Shortcut)
            {
                hub.Open(spec.Tab, spec.Pane);
                return;
            }

            openApplet(appletId, cell);
            return;
        }

        if (string.Equals(appletId, "party", StringComparison.Ordinal))
        {
            hub.Open(DestinationTab.Social, SocialPane.Linkshells);
        }
    }

    private static string TitleOf(string appletId)
    {
        if (AppShelf.Find(appletId) is AppSpec spec)
        {
            return spec.Name;
        }

        return appletId switch
        {
            "pearlchat" => "Messages",
            "party" => "Party",
            _ => appletId,
        };
    }

    private static void DrawApp(in AppletFrame frame, Rect cell, string appletId, string label, int badge,
        Action pressed, bool open)
    {
        if (cell.Width < 4f || cell.Height < 4f)
        {
            return;
        }

        var side = MathF.Min(frame.Units(52f), cell.Width);
        var labelGap = frame.Units(4f);
        var labelHeight = frame.Text.LineHeight(FontRole.Caption);
        var hover = frame.Input.IsHovering(cell);
        var drawn = hover ? side * 1.08f : side;
        var icon = Rect.FromSize(new Vector2(cell.Center.X - side * 0.5f, cell.Min.Y), new Vector2(side, side));
        var bubble = Rect.FromSize(icon.Center - new Vector2(drawn * 0.5f, drawn * 0.5f), new Vector2(drawn, drawn));
        AppMarks.DrawFace(frame, bubble, appletId, hover);
        var caption = new Rect(
            new Vector2(cell.Min.X, icon.Max.Y + labelGap),
            new Vector2(cell.Max.X, icon.Max.Y + labelGap + labelHeight));
        var style = new TextStyle(FontRole.Caption, frame.Theme.Palette.Ink, TextAlign.Center);
        if (frame.Text.Measure(label, FontRole.Caption).X <= caption.Width)
        {
            frame.Text.DrawIn(caption, label, style);
        }
        else
        {
            frame.Text.DrawWrapped(caption, label, style);
        }

        AppMarks.DrawCount(frame, bubble, badge);

        if (open && frame.Input.ConsumeClick(cell))
        {
            pressed();
        }
    }

    private static bool LiftsOnHover(string id) =>
        id is "weather" or "announcements" or "calendar";

    private static Rect Lift(Rect area, float scale)
    {
        var size = area.Size * scale;
        return Rect.FromSize(area.Center - size * 0.5f, size);
    }

    private static Vector2 EditSway(float time, float seed, float amplitude)
    {
        var phase = time * 5.1f + seed * 0.041f;
        return new Vector2(MathF.Sin(phase) * amplitude, MathF.Cos(phase * 0.82f) * amplitude * 0.42f);
    }

    private enum WidgetDrop : byte
    {
        Left = 0,
        Right = 1,
        Above = 2,
        Below = 3,
    }
}
