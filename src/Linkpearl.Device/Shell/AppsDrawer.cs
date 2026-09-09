using Linkpearl.Applets;
using Linkpearl.Destinations;
using Linkpearl.Geometry;
using Linkpearl.Input;
using Linkpearl.Layout;
using Linkpearl.Painting;
using Linkpearl.Preferences;
using Linkpearl.Shell;
using Linkpearl.Talk;
using Linkpearl.Theming;

namespace Linkpearl.Device.Shell;

public sealed class AppsDrawer
{
    private enum Page : byte
    {
        Shelf = 0,
        Manage = 1,
    }

    private readonly IReadOnlyList<IApplet> applets;
    private readonly DestinationHub hub;
    private readonly DisplayPreferences display;
    private readonly GlassEdit glass;
    private readonly ITalk talk;
    private readonly NoticeLedger notices;
    private readonly Action rememberReturn;
    private readonly ScrollState scroll = new();
    private readonly HashSet<AppGroup> openGroups = [];
    private readonly List<string> visible = new();
    private readonly List<Rect> cells = new();
    private readonly List<Rect> icons = new();
    private Page page;
    private AppChip chip;
    private string query = string.Empty;
    private string? openFolder;
    private string? pressId;
    private long pressAt;
    private Vector2 pressPoint;
    private string? dragId;
    private bool skipOpen;
    private float jiggle;
    private int screen;
    private int pageNudge;
    private long edgeAt;
    private bool menuOpen;
    private Vector2 menuAt;
    private string? menuFolder;

    public AppsDrawer(IReadOnlyList<IApplet> applets, DestinationHub hub, DisplayPreferences display, GlassEdit glass,
        ITalk talk, NoticeLedger notices, Action rememberReturn)
    {
        this.applets = applets;
        this.hub = hub;
        this.display = display;
        this.glass = glass;
        this.talk = talk;
        this.notices = notices;
        this.rememberReturn = rememberReturn;
    }

    public bool Editing => glass.Active;

    public bool OnInnerPage => page != Page.Shelf || openFolder is not null || glass.Active;

    public bool BlocksPager(Vector2 at, bool held)
    {
        if (page != Page.Shelf)
        {
            return false;
        }

        if (pressId is not null || dragId is not null || menuOpen)
        {
            return true;
        }

        return held && HitsIcon(at);
    }

    public int PageNudge => pageNudge;

    public string? FlyingId => dragId;

    public void ClearNudge() => pageNudge = 0;

    public void EnterEdit() => glass.Active = true;

    public void AdoptDrag(string id)
    {
        if (id.Length == 0)
        {
            return;
        }

        glass.Active = true;
        dragId = id;
        skipOpen = true;
        pressId = id;
        pressAt = Environment.TickCount64;
        pressPoint = new Vector2(float.MinValue, 0f);
    }

    public void ReleaseDrag()
    {
        pressId = null;
        dragId = null;
    }

    public bool Back()
    {
        if (menuOpen)
        {
            menuOpen = false;
            return true;
        }

        if (glass.Active)
        {
            StopEdit();
            return true;
        }

        if (openFolder is not null)
        {
            openFolder = null;
            return true;
        }

        if (page == Page.Manage)
        {
            page = Page.Shelf;
            query = string.Empty;
            return true;
        }

        return false;
    }

    public void CloseInner()
    {
        page = Page.Shelf;
        openFolder = null;
        query = string.Empty;
        StopEdit();
        menuOpen = false;
        menuFolder = null;
        scroll.Reset();
    }

    public void ShowManage()
    {
        page = Page.Manage;
        openFolder = null;
        query = string.Empty;
        StopEdit();
        menuOpen = false;
        scroll.Reset();
    }

    public void Draw(in AppletFrame frame, Rect area, bool hush, int screenIndex = 0, bool interact = true)
    {
        screen = screenIndex;
        if (page == Page.Manage)
        {
            frame.Paint.Fill(area, Palette.AppGround);
        }

        var gold = frame.Theme.Palette.WarmAccent;
        var pad = frame.Units(14f);
        var inner = area.Inset(new Edges(pad, frame.Units(8f), pad, frame.Units(6f)));
        var cursor = inner.Min.Y;

        if (page == Page.Manage)
        {
            cursor = DrawManageHeader(frame, inner, gold, cursor);
            cursor = DrawSearch(frame, inner, gold, cursor);
            cursor = DrawChips(frame, inner, gold, cursor);
        }
        else
        {
            cursor = DrawShelfHeader(frame, inner, gold, cursor);
            if (glass.Active)
            {
                var hint = Rect.FromSize(new Vector2(inner.Min.X, cursor), new Vector2(inner.Width, frame.Units(16f)));
                frame.Text.DrawIn(hint, "Drag to move or onto an app for a folder. − removes.",
                    new TextStyle(FontRole.Caption, frame.Theme.Palette.InkMuted));
                cursor = hint.Max.Y + frame.Units(6f);
            }
        }

        if (page == Page.Shelf && openFolder is not null)
        {
            cursor = DrawFolderBanner(frame, inner, gold, cursor);
        }

        var body = new Rect(new Vector2(inner.Min.X, cursor), inner.Max);
        var scrolled = body.Translate(new Vector2(0f, -scroll.Offset));
        var dragging = dragId is not null;
        var live = !menuOpen && (body.Contains(frame.Input.Pointer) || dragging);
        var list = live ? frame : frame.WithInput(SilentInput.Instance);
        frame.Paint.PushClip(body);
        var height = page == Page.Manage
            ? DrawGroupedShelf(list, scrolled, hush)
            : DrawHomeGrid(list, scrolled, hush, body, interact);
        frame.Paint.PopClip();

        scroll.Apply(frame, body, height, live: !menuOpen && !dragging);

        if (page == Page.Shelf && !menuOpen &&
            frame.Input.ConsumeClick(inner, PointerButton.Secondary))
        {
            menuOpen = true;
            menuAt = frame.Input.Pointer;
            var hit = HitId(menuAt);
            menuFolder = hit is not null && display.TryFolder(hit, out _, out _) ? hit : null;
        }

        if (menuOpen)
        {
            DrawContextMenu(frame, inner, gold);
        }
    }

    private float DrawShelfHeader(in AppletFrame frame, Rect inner, Vector4 gold, float top)
    {
        var row = Rect.FromSize(new Vector2(inner.Min.X, top), new Vector2(inner.Width, frame.Units(28f)));
        var action = row.RightSlice(frame.Units(72f));
        var label = glass.Active ? "Done" : "Manage";
        frame.Text.DrawIn(action, label, new TextStyle(FontRole.CaptionStrong, gold, TextAlign.Right));
        if (frame.Input.ConsumeClick(action))
        {
            if (glass.Active)
            {
                StopEdit();
            }
            else
            {
                page = Page.Manage;
                scroll.Reset();
            }
        }

        return row.Max.Y + frame.Units(6f);
    }

    private float DrawManageHeader(in AppletFrame frame, Rect inner, Vector4 gold, float top)
    {
        var row = Rect.FromSize(new Vector2(inner.Min.X, top), new Vector2(inner.Width, frame.Units(28f)));
        frame.Text.DrawIn(row, "Manage Apps", new TextStyle(FontRole.Display, frame.Theme.Palette.Ink));
        var done = row.RightSlice(frame.Units(72f));
        frame.Paint.Stroke(done.Inset(new Edges(0f, frame.Units(2f))), gold, frame.Theme.Metrics.Hairline,
            frame.Units(10f));
        frame.Text.DrawIn(done, "Done", new TextStyle(FontRole.CaptionStrong, frame.Theme.Palette.Ink, TextAlign.Center));
        if (frame.Input.ConsumeClick(done))
        {
            CloseInner();
        }

        var sub = Rect.FromSize(new Vector2(inner.Min.X, row.Max.Y), new Vector2(inner.Width, frame.Units(18f)));
        frame.Text.DrawIn(sub, "Choose which owned apps sit on your screens.",
            new TextStyle(FontRole.Caption, frame.Theme.Palette.InkMuted));
        return sub.Max.Y + frame.Units(8f);
    }

    private float DrawSearch(in AppletFrame frame, Rect inner, Vector4 gold, float top)
    {
        var field = Rect.FromSize(new Vector2(inner.Min.X, top), new Vector2(inner.Width, frame.Units(32f)));
        frame.Paint.Fill(field, frame.Theme.Palette.SurfaceOverlay with { W = 0.72f }, field.Height * 0.5f);
        frame.Paint.Stroke(field, gold with { W = 0.35f }, frame.Theme.Metrics.Hairline, field.Height * 0.5f);
        var type = field.Inset(new Edges(frame.Units(28f), frame.Units(4f), frame.Units(8f), frame.Units(4f)));
        query = frame.TextField.Draw("apps-manage-search", type, query, "Search apps");
        SearchMark.Draw(frame.Paint, field.LeftSlice(frame.Units(28f)).Inset(frame.Units(5f)),
            frame.Theme.Palette.InkMuted);
        return field.Max.Y + frame.Units(10f);
    }

    private float DrawChips(in AppletFrame frame, Rect inner, Vector4 gold, float top)
    {
        var row = Rect.FromSize(new Vector2(inner.Min.X, top), new Vector2(inner.Width, frame.Units(26f)));
        var gap = frame.Units(6f);
        var x = row.Min.X;
        for (var index = 0; index < AppShelf.ChipLabels.Length; index++)
        {
            var label = AppShelf.ChipLabels[index];
            var width = MathF.Max(frame.Units(36f), frame.Text.Measure(label, FontRole.Caption).X + frame.Units(16f));
            if (x + width > row.Max.X)
            {
                break;
            }

            var chipRect = Rect.FromSize(new Vector2(x, row.Min.Y), new Vector2(width, row.Height));
            var on = chip == (AppChip)index;
            frame.Paint.Fill(chipRect, frame.Theme.Palette.SurfaceOverlay with { W = on ? 0.55f : 0.28f },
                chipRect.Height * 0.5f);
            if (on)
            {
                frame.Paint.Stroke(chipRect, gold, frame.Theme.Metrics.Hairline, chipRect.Height * 0.5f);
            }

            var ink = on ? gold : frame.Theme.Palette.InkMuted;
            frame.Text.DrawIn(chipRect, label, new TextStyle(FontRole.Caption, ink, TextAlign.Center));
            if (frame.Input.ConsumeClick(chipRect))
            {
                chip = (AppChip)index;
                scroll.Reset();
            }

            x = chipRect.Max.X + gap;
        }

        return row.Max.Y + frame.Units(12f);
    }

    private float DrawFolderBanner(in AppletFrame frame, Rect inner, Vector4 gold, float top)
    {
        display.TryFolder(openFolder ?? string.Empty, out var name, out _);
        var row = Rect.FromSize(new Vector2(inner.Min.X, top), new Vector2(inner.Width, frame.Units(22f)));
        frame.Text.DrawIn(row, name.Length > 0 ? name : "Folder", new TextStyle(FontRole.CaptionStrong, gold));
        var close = row.RightSlice(frame.Units(48f));
        frame.Text.DrawIn(close, "Close", new TextStyle(FontRole.Caption, frame.Theme.Palette.InkMuted, TextAlign.Right));
        if (frame.Input.ConsumeClick(close))
        {
            openFolder = null;
            StopEdit();
        }

        return row.Max.Y + frame.Units(8f);
    }

    private float DrawGroupedShelf(in AppletFrame frame, Rect area, bool hush)
    {
        FillVisible();
        GroupVisible();
        var columns = 4;
        var cell = frame.Units(104f);
        var gap = frame.Units(8f);
        var barH = frame.Units(32f);
        var searching = query.Trim().Length > 0;
        var y = area.Min.Y;
        var startY = y;
        var index = 0;
        while (index < visible.Count)
        {
            var group = GroupOf(visible[index]);
            var first = index;
            while (index < visible.Count && GroupOf(visible[index]) == group)
            {
                index++;
            }

            var count = index - first;
            var header = Rect.FromSize(new Vector2(area.Min.X, y), new Vector2(area.Width, barH));
            var open = searching || openGroups.Contains(group);
            DrawGroupDrop(frame, header, AppShelf.GroupLabels[(int)group], open);
            if (frame.Input.ConsumeClick(header) && !searching)
            {
                if (!openGroups.Add(group))
                {
                    openGroups.Remove(group);
                }
            }

            y = header.Max.Y + frame.Units(8f);
            if (!open)
            {
                y += frame.Units(4f);
                continue;
            }

            for (var slot = 0; slot < count; slot++)
            {
                var col = slot % columns;
                var row = slot / columns;
                var tile = Rect.FromSize(
                    new Vector2(area.Min.X + col * (area.Width / columns), y + row * (cell + gap)),
                    new Vector2(area.Width / columns, cell));
                DrawManageItem(frame, tile, visible[first + slot], hush);
            }

            y += ((count + columns - 1) / columns) * (cell + gap) + frame.Units(4f);
        }

        return y - startY + frame.Units(12f);
    }

    private float DrawHomeGrid(in AppletFrame frame, Rect area, bool hush, Rect body, bool interact)
    {
        var held = interact && frame.Input.IsHeld();
        if (interact && !held)
        {
            if (dragId is not null)
            {
                ResolveDrop(frame.Input.Pointer);
            }

            pressId = null;
            dragId = null;
            edgeAt = 0;
        }

        else if (interact && pressId is { } holding)
        {
            var elapsed = Environment.TickCount64 - pressAt;
            var travel = (frame.Input.Pointer - pressPoint).Length();
            if (glass.Active && travel > frame.Units(6f))
            {
                dragId = holding;
                skipOpen = true;
            }
            else if (elapsed >= 480)
            {
                glass.Active = true;
                skipOpen = true;
                if (travel > frame.Units(8f))
                {
                    dragId = holding;
                }
            }
        }

        FillHome();
        cells.Clear();
        icons.Clear();
        var columns = 4;
        var cell = frame.Units(86f);
        var gap = frame.Units(8f);
        jiggle += frame.DeltaSeconds;
        if (openFolder is null)
        {
            var rowsFit = Math.Max(4, (int)(area.Height / MathF.Max(1f, cell + gap)));
            var need = columns * rowsFit;
            while (visible.Count < need)
            {
                visible.Add(string.Empty);
            }
        }

        if (held && dragId is not null)
        {
            WatchEdge(frame, body);
        }

        var hoverDrop = dragId is not null ? HitFolderTarget(frame.Input.Pointer) : null;
        for (var index = 0; index < visible.Count; index++)
        {
            var col = index % columns;
            var row = index / columns;
            var tile = Rect.FromSize(
                new Vector2(area.Min.X + col * (area.Width / columns), area.Min.Y + row * (cell + gap)),
                new Vector2(area.Width / columns, cell));
            cells.Add(tile);
            var icon = MathF.Min(frame.Units(52f), tile.Width * 0.62f);
            icons.Add(Rect.FromSize(new Vector2(tile.Center.X - icon * 0.5f, tile.Min.Y + frame.Units(4f)),
                new Vector2(icon, icon)));
            var id = visible[index];
            if (string.Equals(dragId, id, StringComparison.Ordinal))
            {
                continue;
            }

            var drawn = glass.Active && id.Length > 0
                ? tile.Translate(EditSway(jiggle, index, frame.Units(0.45f)))
                : tile;
            var lit = hoverDrop is not null && string.Equals(hoverDrop, id, StringComparison.Ordinal);
            DrawHomeTile(frame, drawn, id, hush, lit);
        }

        if (interact && dragId is { } flying)
        {
            var size = new Vector2(area.Width / columns, cell);
            var floatTile = Rect.FromSize(frame.Input.Pointer - size * 0.5f, size);
            DrawHomeTile(frame, floatTile, flying, hush, false);
        }

        if (interact && glass.Active && dragId is null && !skipOpen && frame.Input.ConsumeClick(body))
        {
            StopEdit();
        }

        if (interact && !held)
        {
            skipOpen = false;
        }

        var rows = (visible.Count + columns - 1) / columns;
        return MathF.Max(cell, rows * (cell + gap));
    }

    private void DrawHomeTile(in AppletFrame frame, Rect cell, string id, bool hush, bool dropLit)
    {
        if (id.Length == 0)
        {
            if (glass.Active)
            {
                frame.Paint.Stroke(cell.Inset(frame.Units(10f)),
                    frame.Theme.Palette.WarmAccent with { W = 0.12f },
                    frame.Theme.Metrics.Hairline, frame.Units(16f));
            }

            return;
        }

        var hover = frame.Input.IsHovering(cell) || dropLit;
        var icon = MathF.Min(frame.Units(52f), cell.Width * 0.62f);
        var iconArea = Rect.FromSize(new Vector2(cell.Center.X - icon * 0.5f, cell.Min.Y + frame.Units(4f)),
            new Vector2(icon, icon));
        var drawn = hover ? icon * 1.08f : icon;
        var drawArea = Rect.FromSize(iconArea.Center - new Vector2(drawn * 0.5f, drawn * 0.5f),
            new Vector2(drawn, drawn));
        if (display.TryFolder(id, out _, out var kids))
        {
            AppMarks.DrawFolderFace(frame, drawArea, kids, hover || dropLit);
        }
        else
        {
            AppMarks.DrawFace(frame, drawArea, id, hover);
        }

        if (dropLit)
        {
            frame.Paint.StrokeCircle(drawArea.Center, drawArea.Width * 0.52f, frame.Theme.Palette.WarmAccent,
                frame.Units(2f));
        }

        var name = TitleOf(id);
        var label = new Rect(new Vector2(cell.Min.X, iconArea.Max.Y + frame.Units(4f)),
            new Vector2(cell.Max.X, cell.Max.Y));
        frame.Text.DrawEllipsized(label, name, new TextStyle(FontRole.Caption, frame.Theme.Palette.Ink, TextAlign.Center));
        DrawBadge(frame, iconArea, id, hush);

        if (glass.Active)
        {
            var minus = Rect.FromSize(new Vector2(iconArea.Min.X - frame.Units(2f), iconArea.Min.Y - frame.Units(2f)),
                new Vector2(frame.Units(18f), frame.Units(18f)));
            frame.Paint.FillCircle(minus.Center, minus.Width * 0.5f, frame.Theme.Palette.Negative with { W = 0.92f });
            frame.Text.DrawIn(minus, "−",
                new TextStyle(FontRole.CaptionStrong, frame.Theme.Palette.AccentInk, TextAlign.Center));
            if (frame.Input.ConsumeClick(minus))
            {
                RemoveHome(id);
                return;
            }
        }

        if (frame.Input.WasPressed(cell))
        {
            pressId = id;
            pressAt = Environment.TickCount64;
            pressPoint = frame.Input.Pointer;
        }

        if (!frame.Input.ConsumeClick(cell))
        {
            return;
        }

        if (skipOpen || glass.Active)
        {
            return;
        }

        Open(frame.Router, id);
    }

    private void DrawManageItem(in AppletFrame frame, Rect cell, string id, bool hush)
    {
        var hover = frame.Input.IsHovering(cell);
        var gold = frame.Theme.Palette.WarmAccent;
        var icon = MathF.Min(frame.Units(44f), cell.Width * 0.48f);
        var iconArea = Rect.FromSize(new Vector2(cell.Center.X - icon * 0.5f, cell.Min.Y + frame.Units(2f)),
            new Vector2(icon, icon));
        if (display.TryFolder(id, out _, out var kids))
        {
            AppMarks.DrawFolderFace(frame, iconArea, kids, hover);
        }
        else
        {
            AppMarks.DrawFace(frame, iconArea, id, hover);
        }

        DrawBadge(frame, iconArea, id, hush);
        var name = TitleOf(id);
        var label = Rect.FromSize(new Vector2(cell.Min.X, iconArea.Max.Y + frame.Units(2f)),
            new Vector2(cell.Width, frame.Units(16f)));
        frame.Text.DrawEllipsized(label, name,
            new TextStyle(FontRole.Caption, frame.Theme.Palette.Ink, TextAlign.Center));
        var placed = display.IsPlaced(id);
        var action = Rect.FromSize(new Vector2(cell.Min.X + frame.Units(6f), cell.Max.Y - frame.Units(22f)),
            new Vector2(cell.Width - frame.Units(12f), frame.Units(18f)));
        var verb = placed ? "Remove" : "Add";
        var ink = placed ? frame.Theme.Palette.Negative : gold;
        frame.Paint.Stroke(action, ink with { W = 0.70f }, frame.Theme.Metrics.Hairline, frame.Units(8f));
        frame.Text.DrawIn(action, verb, new TextStyle(FontRole.CaptionStrong, ink, TextAlign.Center));
        if (!frame.Input.ConsumeClick(action) && !frame.Input.ConsumeClick(cell))
        {
            return;
        }

        if (placed)
        {
            if (openFolder is { } folder && !display.TryFolder(id, out _, out _))
            {
                display.DropFromFolder(folder, id);
                return;
            }

            display.RemoveApp(id);
            return;
        }

        display.InstallApp(id);
    }

    private void DrawLauncherTile(in AppletFrame frame, Rect cell, string id, bool hush)
    {
        var hover = frame.Input.IsHovering(cell);
        var icon = MathF.Min(frame.Units(52f), cell.Width * 0.62f);
        var iconArea = Rect.FromSize(new Vector2(cell.Center.X - icon * 0.5f, cell.Min.Y + frame.Units(4f)),
            new Vector2(icon, icon));
        var drawn = hover ? icon * 1.08f : icon;
        var drawArea = Rect.FromSize(iconArea.Center - new Vector2(drawn * 0.5f, drawn * 0.5f),
            new Vector2(drawn, drawn));
        AppMarks.DrawFace(frame, drawArea, id, hover);
        var name = TitleOf(id);
        var label = new Rect(new Vector2(cell.Min.X, iconArea.Max.Y + frame.Units(4f)),
            new Vector2(cell.Max.X, cell.Max.Y));
        frame.Text.DrawEllipsized(label, name, new TextStyle(FontRole.Caption, frame.Theme.Palette.Ink, TextAlign.Center));
        DrawBadge(frame, iconArea, id, hush);
        if (frame.Input.ConsumeClick(cell))
        {
            Open(frame.Router, id);
        }
    }

    private void ResolveDrop(Vector2 pointer)
    {
        if (!glass.Active || dragId is not { Length: > 0 } moving)
        {
            return;
        }

        var target = HitFolderTarget(pointer);
        if (target is { Length: > 0 } && !string.Equals(target, moving, StringComparison.Ordinal))
        {
            if (display.TryFolder(target, out _, out _))
            {
                display.NestInFolder(target, moving);
                return;
            }

            if (!display.TryFolder(moving, out _, out _))
            {
                display.CreateAppFolder([moving, target]);
                return;
            }
        }

        var slot = HitIndex(pointer);
        if (slot < 0)
        {
            slot = FirstOpenSlot();
        }

        if (slot < 0)
        {
            return;
        }

        if (openFolder is { } folder)
        {
            display.MoveFolderChild(folder, moving, slot);
            return;
        }

        display.PlaceAppAt(moving, screen, slot);
        display.RemoveStudioWidget(moving);
        display.RemoveStudioApp(moving);
    }

    private int FirstOpenSlot()
    {
        for (var index = 0; index < visible.Count; index++)
        {
            if (visible[index].Length == 0)
            {
                return index;
            }
        }

        return visible.Count;
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

    private void DrawContextMenu(in AppletFrame frame, Rect inner, Vector4 gold)
    {
        var labels = new List<string>();
        if (menuFolder is not null)
        {
            labels.Add("Delete Folder");
        }
        else
        {
            labels.Add("Create Folder");
            if (display.AppScreenCount < DisplayPreferences.AppScreenCap)
            {
                labels.Add("Create New Screen");
            }

            if (screen > 0)
            {
                labels.Add("Delete Screen");
            }
        }

        var width = frame.Units(176f);
        var rowH = frame.Units(34f);
        var height = rowH * labels.Count + frame.Units(8f);
        var left = Math.Clamp(menuAt.X, inner.Min.X, inner.Max.X - width);
        var top = Math.Clamp(menuAt.Y, inner.Min.Y, inner.Max.Y - height);
        var box = Rect.FromSize(new Vector2(left, top), new Vector2(width, height));
        var radius = frame.Units(10f);
        frame.Paint.Fill(box, frame.Theme.Palette.SurfaceRaised with { W = 0.98f }, radius);
        frame.Paint.Stroke(box, gold with { W = 0.55f }, frame.Theme.Metrics.Hairline, radius);
        for (var index = 0; index < labels.Count; index++)
        {
            var row = Rect.FromSize(box.Min + new Vector2(0f, frame.Units(4f) + index * rowH),
                new Vector2(width, rowH)).Inset(new Edges(frame.Units(4f), 0f));
            var danger = labels[index].StartsWith("Delete", StringComparison.Ordinal);
            var hover = frame.Input.IsHovering(row);
            if (hover)
            {
                var wash = danger ? frame.Theme.Palette.Negative with { W = 0.22f } : gold with { W = 0.20f };
                frame.Paint.Fill(row, wash, frame.Units(8f));
            }

            var ink = danger
                ? frame.Theme.Palette.Negative
                : hover
                    ? gold
                    : frame.Theme.Palette.Ink;
            frame.Text.DrawIn(row.Inset(new Edges(frame.Units(10f), 0f)), labels[index],
                new TextStyle(FontRole.CaptionStrong, ink));
            if (frame.Input.ConsumeClick(row))
            {
                RunMenu(labels[index]);
                return;
            }
        }

        frame.Input.ConsumeClick(box);
        frame.Input.ConsumeClick(box, PointerButton.Secondary);
        frame.Input.Claim(box);
        if (box.Contains(frame.Input.Pointer))
        {
            return;
        }

        if (frame.Input.ConsumeClick(inner, PointerButton.Secondary))
        {
            menuAt = frame.Input.Pointer;
            var hit = HitId(menuAt);
            menuFolder = hit is not null && display.TryFolder(hit, out _, out _) ? hit : null;
            return;
        }

        if (frame.Input.ConsumeClick(inner))
        {
            menuOpen = false;
            menuFolder = null;
        }
    }

    private void RunMenu(string label)
    {
        menuOpen = false;
        if (string.Equals(label, "Delete Folder", StringComparison.Ordinal) && menuFolder is { } folder)
        {
            display.RemoveApp(folder);
            if (string.Equals(openFolder, folder, StringComparison.Ordinal))
            {
                openFolder = null;
            }

            menuFolder = null;
            return;
        }

        menuFolder = null;
        if (string.Equals(label, "Create Folder", StringComparison.Ordinal))
        {
            display.CreateEmptyFolder(screen);
            return;
        }

        if (string.Equals(label, "Create New Screen", StringComparison.Ordinal))
        {
            pageNudge = 2;
            return;
        }

        if (string.Equals(label, "Delete Screen", StringComparison.Ordinal) && screen > 0)
        {
            pageNudge = 3;
        }
    }

    private bool HitsIcon(Vector2 pointer)
    {
        var index = HitIndex(pointer);
        return index >= 0 && index < visible.Count && visible[index].Length > 0;
    }

    private string? HitFolderTarget(Vector2 pointer)
    {
        string? found = null;
        var best = float.MaxValue;
        var count = Math.Min(cells.Count, visible.Count);
        for (var index = 0; index < count; index++)
        {
            var id = visible[index];
            if (id.Length == 0 || string.Equals(id, dragId, StringComparison.Ordinal))
            {
                continue;
            }

            var cell = cells[index];
            if (!cell.Contains(pointer))
            {
                continue;
            }

            var distance = (cell.Center - pointer).LengthSquared();
            if (distance >= best)
            {
                continue;
            }

            best = distance;
            found = id;
        }

        return found;
    }

    private string? HitId(Vector2 pointer)
    {
        var index = HitIndex(pointer);
        return index >= 0 && index < visible.Count ? visible[index] : null;
    }

    private int HitIndex(Vector2 pointer)
    {
        for (var index = 0; index < cells.Count; index++)
        {
            if (cells[index].Contains(pointer))
            {
                return index;
            }
        }

        return -1;
    }

    private void RemoveHome(string id)
    {
        if (openFolder is { } folder && !display.TryFolder(id, out _, out _))
        {
            display.DropFromFolder(folder, id);
            return;
        }

        display.RemoveApp(id);
        if (string.Equals(openFolder, id, StringComparison.Ordinal))
        {
            openFolder = null;
        }
    }

    private void StopEdit()
    {
        glass.Active = false;
        pressId = null;
        dragId = null;
        skipOpen = false;
    }

    private void Open(IRouter router, string id)
    {
        if (display.TryFolder(id, out _, out var children))
        {
            openFolder = id;
            _ = children;
            scroll.Reset();
            StopEdit();
            return;
        }

        if (AppShelf.Find(id) is not AppSpec spec)
        {
            return;
        }

        if (spec.Kind == AppKind.Shortcut)
        {
            hub.Open(spec.Tab, spec.Pane);
            return;
        }

        rememberReturn();
        router.Open(id);
    }

    private void FillHome()
    {
        visible.Clear();
        if (openFolder is not null)
        {
            display.TryFolder(openFolder, out _, out var children);
            visible.AddRange(children);
            return;
        }

        var shelf = display.AppsOnScreen(screen);
        for (var index = 0; index < shelf.Count; index++)
        {
            visible.Add(shelf[index]);
        }
    }

    private void FillVisible()
    {
        visible.Clear();
        var needle = query.Trim();
        foreach (var id in display.OwnedApps)
        {
            if (id.StartsWith("folder:", StringComparison.Ordinal) || !Matches(id, needle) ||
                visible.Contains(id))
            {
                continue;
            }

            visible.Add(id);
        }
    }

    private bool Matches(string id, string needle)
    {
        var spec = AppShelf.Find(id);
        var name = spec?.Name ?? TitleOf(id);
        var caption = spec?.Caption ?? string.Empty;
        if (needle.Length > 0 &&
            name.IndexOf(needle, StringComparison.OrdinalIgnoreCase) < 0 &&
            caption.IndexOf(needle, StringComparison.OrdinalIgnoreCase) < 0)
        {
            return false;
        }

        if (chip == AppChip.All)
        {
            return true;
        }

        if (chip == AppChip.Favorites)
        {
            return display.IsFavorite(id);
        }

        return spec is AppSpec found && found.Chip == chip;
    }

    private void GroupVisible()
    {
        visible.Sort((left, right) =>
        {
            var group = GroupOf(left).CompareTo(GroupOf(right));
            return group != 0 ? group : IndexOf(left).CompareTo(IndexOf(right));
        });
    }

    private static AppGroup GroupOf(string id)
    {
        var spec = AppShelf.Find(id);
        return spec?.Group ?? AppGroup.Tools;
    }

    private string TitleOf(string id)
    {
        if (display.TryFolder(id, out var name, out _))
        {
            return name;
        }

        if (AppShelf.Find(id) is AppSpec spec)
        {
            return spec.Name;
        }

        for (var index = 0; index < applets.Count; index++)
        {
            if (string.Equals(applets[index].Manifest.Id, id, StringComparison.Ordinal))
            {
                return applets[index].Manifest.DisplayNameKey;
            }
        }

        return id;
    }

    private int IndexOf(string id)
    {
        var shelf = display.InstalledApps;
        for (var index = 0; index < shelf.Count; index++)
        {
            if (string.Equals(shelf[index], id, StringComparison.Ordinal))
            {
                return index;
            }
        }

        return 0;
    }

    private void DrawBadge(in AppletFrame frame, Rect icon, string id, bool hush)
    {
        if (hush)
        {
            return;
        }

        var missed = notices.AppBadge(id, talk);
        if (missed <= 0 && display.TryFolder(id, out _, out var kids))
        {
            for (var index = 0; index < kids.Length; index++)
            {
                missed += notices.AppBadge(kids[index], talk);
            }
        }

        if (missed > 0)
        {
            AppMarks.DrawCount(frame, icon, missed);
            return;
        }

        for (var index = 0; index < applets.Count; index++)
        {
            if (!string.Equals(applets[index].Manifest.Id, id, StringComparison.Ordinal) ||
                !applets[index].Badge.IsVisible)
            {
                continue;
            }

            var radius = frame.Units(6f);
            frame.Paint.FillCircle(new Vector2(icon.Max.X - radius * 0.2f, icon.Min.Y + radius * 0.2f), radius,
                frame.Theme.Palette.Negative);
            return;
        }
    }

    private static void DrawGroupDrop(in AppletFrame frame, Rect row, string label, bool open)
    {
        var gold = frame.Theme.Palette.WarmAccent;
        frame.Paint.Fill(row, frame.Theme.Palette.SurfaceOverlay with { W = 0.72f }, row.Height * 0.5f);
        frame.Paint.Stroke(row, gold with { W = 0.35f }, frame.Theme.Metrics.Hairline, row.Height * 0.5f);
        var inner = row.Inset(new Edges(frame.Units(12f), 0f, frame.Units(10f), 0f));
        var caret = inner.RightSlice(frame.Units(16f));
        var size = frame.Text.Measure(label, FontRole.CaptionStrong);
        var text = inner.LeftSlice(size.X + frame.Units(8f));
        frame.Text.DrawIn(text, label, new TextStyle(FontRole.CaptionStrong, gold));
        var rule = new Rect(new Vector2(text.Max.X + frame.Units(6f), inner.Center.Y),
            new Vector2(caret.Min.X - frame.Units(6f), inner.Center.Y + frame.Theme.Metrics.Hairline));
        if (rule.Width > frame.Units(8f))
        {
            frame.Paint.Fill(rule, gold with { W = 0.55f });
        }

        DrawCaret(frame.Paint, caret, gold, open);
    }

    private static void DrawCaret(IPaintSurface paint, Rect area, Vector4 gold, bool open)
    {
        var center = area.Center;
        var span = MathF.Min(area.Width, area.Height) * 0.28f;
        var stroke = MathF.Max(1.2f, span * 0.38f);
        if (open)
        {
            paint.Line(center + new Vector2(-span, -span * 0.35f), center + new Vector2(0f, span * 0.45f), gold, stroke);
            paint.Line(center + new Vector2(span, -span * 0.35f), center + new Vector2(0f, span * 0.45f), gold, stroke);
            return;
        }

        paint.Line(center + new Vector2(-span * 0.35f, -span), center + new Vector2(span * 0.45f, 0f), gold, stroke);
        paint.Line(center + new Vector2(-span * 0.35f, span), center + new Vector2(span * 0.45f, 0f), gold, stroke);
    }

    private static void DrawOrnament(IPaintSurface paint, Rect row, Vector4 gold)
    {
        var mid = row.Center;
        paint.Fill(new Rect(new Vector2(row.Min.X, mid.Y - 0.6f), new Vector2(mid.X - 8f, mid.Y + 0.6f)),
            gold with { W = 0.55f });
        paint.Fill(new Rect(new Vector2(mid.X + 8f, mid.Y - 0.6f), new Vector2(row.Max.X, mid.Y + 0.6f)),
            gold with { W = 0.55f });
        paint.FillCircle(mid, 3.2f, gold);
    }

    private static Vector2 EditSway(float time, int index, float amplitude)
    {
        var phase = time * 5.1f + index * 0.73f;
        return new Vector2(MathF.Sin(phase) * amplitude, MathF.Cos(phase * 0.82f) * amplitude * 0.42f);
    }
}
