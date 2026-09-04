using Linkpearl.Applets;
using Linkpearl.Destinations;
using Linkpearl.Geometry;
using Linkpearl.Layout;
using Linkpearl.Painting;
using Linkpearl.Preferences;
using Linkpearl.Shell;

namespace Linkpearl.Device.Shell;

public sealed class AppsDrawer
{
    private enum Page : byte
    {
        Shelf = 0,
        Manage = 1,
        Library = 2,
    }

    private readonly IReadOnlyList<IApplet> applets;
    private readonly DestinationHub hub;
    private readonly DisplayPreferences display;
    private readonly ScrollState scroll = new();
    private readonly HashSet<string> selected = new(StringComparer.Ordinal);
    private readonly HashSet<AppGroup> openGroups = [];
    private readonly List<string> visible = new();
    private Page page;
    private AppChip chip;
    private string query = string.Empty;
    private bool reorder;
    private string? reorderHold;
    private string? openFolder;

    public AppsDrawer(IReadOnlyList<IApplet> applets, DestinationHub hub, DisplayPreferences display)
    {
        this.applets = applets;
        this.hub = hub;
        this.display = display;
    }

    public bool OnInnerPage => page != Page.Shelf || openFolder is not null;

    public bool Back()
    {
        if (openFolder is not null)
        {
            openFolder = null;
            return true;
        }

        if (page == Page.Library)
        {
            page = Page.Manage;
            query = string.Empty;
            return true;
        }

        if (page == Page.Manage)
        {
            page = Page.Shelf;
            reorder = false;
            selected.Clear();
            query = string.Empty;
            return true;
        }

        return false;
    }

    public void CloseInner()
    {
        page = Page.Shelf;
        openFolder = null;
        reorder = false;
        selected.Clear();
        query = string.Empty;
        scroll.Reset();
    }

    public void ShowManage()
    {
        page = Page.Manage;
        openFolder = null;
        reorder = false;
        selected.Clear();
        query = string.Empty;
        scroll.Reset();
    }

    public void Draw(in AppletFrame frame, Rect area, bool hush)
    {
        var gold = frame.Theme.Palette.WarmAccent;
        var pad = frame.Units(14f);
        var inner = area.Inset(new Edges(pad, frame.Units(8f), pad, frame.Units(6f)));
        var cursor = inner.Min.Y;

        if (page == Page.Manage || page == Page.Library)
        {
            cursor = DrawManageHeader(frame, inner, gold, cursor);
        }
        else
        {
            cursor = DrawShelfHeader(frame, inner, gold, cursor);
        }

        cursor = DrawSearch(frame, inner, gold, cursor);
        cursor = DrawChips(frame, inner, gold, cursor);

        if (page == Page.Manage)
        {
            cursor = DrawQuickStrip(frame, inner, gold, cursor);
        }

        if (openFolder is not null)
        {
            cursor = DrawFolderBanner(frame, inner, gold, cursor);
        }

        var footer = page == Page.Manage ? frame.Units(44f) : 0f;
        var body = new Rect(new Vector2(inner.Min.X, cursor), new Vector2(inner.Max.X, inner.Max.Y - footer));
        var scrolled = body.Translate(new Vector2(0f, -scroll.Offset));
        var list = body.Contains(frame.Input.Pointer) ? frame : frame.WithInput(SilentInput.Instance);
        frame.Paint.PushClip(body);
        var height = page switch
        {
            Page.Library => DrawLibrary(list, scrolled, hush),
            Page.Manage => DrawManageGrid(list, scrolled, hush),
            _ => DrawShelf(list, scrolled, hush),
        };
        frame.Paint.PopClip();

        var wheel = frame.Input.IsHovering(body) ? frame.Input.ScrollDelta : 0f;
        scroll.Update(height, body.Height, wheel, frame.Scale);
        ScrollState.DrawIndicator(frame.Paint, frame.Theme, body, height, scroll.Offset, frame.Scale);

        if (page == Page.Manage)
        {
            DrawManageFooter(frame, inner.BottomSlice(footer), gold);
        }
    }

    private float DrawShelfHeader(in AppletFrame frame, Rect inner, Vector4 gold, float top)
    {
        var row = Rect.FromSize(new Vector2(inner.Min.X, top), new Vector2(inner.Width, frame.Units(34f)));
        frame.Text.DrawIn(row, "Apps", new TextStyle(FontRole.Display, gold));
        var edit = row.RightSlice(frame.Units(64f));
        frame.Text.DrawIn(edit, "Manage", new TextStyle(FontRole.CaptionStrong, gold, TextAlign.Right));
        if (frame.Input.ConsumeClick(edit))
        {
            page = Page.Manage;
            scroll.Reset();
        }

        var rule = Rect.FromSize(new Vector2(inner.Min.X, row.Max.Y + frame.Units(2f)),
            new Vector2(inner.Width, frame.Units(10f)));
        DrawOrnament(frame.Paint, rule, gold);
        return rule.Max.Y + frame.Units(8f);
    }

    private float DrawManageHeader(in AppletFrame frame, Rect inner, Vector4 gold, float top)
    {
        var row = Rect.FromSize(new Vector2(inner.Min.X, top), new Vector2(inner.Width, frame.Units(28f)));
        frame.Text.DrawIn(row, "Apps", new TextStyle(FontRole.Display, frame.Theme.Palette.Ink));
        var done = row.RightSlice(frame.Units(72f));
        frame.Paint.Stroke(done.Inset(new Edges(0f, frame.Units(2f))), gold, frame.Theme.Metrics.Hairline,
            frame.Units(10f));
        frame.Text.DrawIn(done, "Done", new TextStyle(FontRole.CaptionStrong, frame.Theme.Palette.Ink, TextAlign.Center));
        if (frame.Input.ConsumeClick(done))
        {
            if (page == Page.Library)
            {
                page = Page.Manage;
            }
            else
            {
                CloseInner();
            }

            scroll.Reset();
        }

        var sub = Rect.FromSize(new Vector2(inner.Min.X, row.Max.Y), new Vector2(inner.Width, frame.Units(18f)));
        frame.Text.DrawIn(sub, "Tap an app, then a Quick Apps slot to place it.",
            new TextStyle(FontRole.Caption, frame.Theme.Palette.InkMuted));
        var mode = sub.RightSlice(frame.Units(108f));
        var modeInk = reorder ? gold : frame.Theme.Palette.InkMuted;
        frame.Text.DrawIn(mode, reorder ? "Reordering" : "Reorder Mode",
            new TextStyle(FontRole.Caption, modeInk, TextAlign.Right));
        if (page == Page.Manage && frame.Input.ConsumeClick(mode))
        {
            reorder = !reorder;
            selected.Clear();
            reorderHold = null;
        }

        return sub.Max.Y + frame.Units(8f);
    }

    private float DrawSearch(in AppletFrame frame, Rect inner, Vector4 gold, float top)
    {
        var field = Rect.FromSize(new Vector2(inner.Min.X, top), new Vector2(inner.Width, frame.Units(32f)));
        frame.Paint.Fill(field, frame.Theme.Palette.SurfaceOverlay with { W = 0.72f }, field.Height * 0.5f);
        frame.Paint.Stroke(field, gold with { W = 0.35f }, frame.Theme.Metrics.Hairline, field.Height * 0.5f);
        var hint = page == Page.Library ? "Find an app to add..." : "Search apps";
        var type = field.Inset(new Edges(frame.Units(28f), 0f, frame.Units(8f), 0f));
        query = frame.TextField.Draw("apps-shelf-search", type, query, hint);
        SearchMark.Draw(frame.Paint, field.LeftSlice(frame.Units(28f)).Inset(frame.Units(5f)),
            frame.Theme.Palette.InkMuted);
        return field.Max.Y + frame.Units(10f);
    }

    private float DrawQuickStrip(in AppletFrame frame, Rect inner, Vector4 gold, float top)
    {
        var row = Rect.FromSize(new Vector2(inner.Min.X, top), new Vector2(inner.Width, frame.Units(78f)));
        frame.Text.DrawIn(row.TopSlice(frame.Units(16f)), "Quick Apps",
            new TextStyle(FontRole.CaptionStrong, gold));
        var slots = row.Inset(new Edges(0f, frame.Units(18f), 0f, 0f));
        var gap = frame.Units(8f);
        var cellW = (slots.Width - gap * (DisplayPreferences.QuickAppSlots - 1)) / DisplayPreferences.QuickAppSlots;
        var picked = selected.Count == 1 ? FirstSelected() : string.Empty;
        for (var index = 0; index < DisplayPreferences.QuickAppSlots; index++)
        {
            var cell = Rect.FromSize(new Vector2(slots.Min.X + index * (cellW + gap), slots.Min.Y),
                new Vector2(cellW, slots.Height));
            var square = cell.Inset(new Edges(MathF.Max(0f, (cell.Width - cell.Height) * 0.5f), 0f));
            var id = index < display.QuickApps.Count ? display.QuickApps[index] : string.Empty;
            frame.Paint.Fill(square, frame.Theme.Palette.SurfaceOverlay with { W = 0.62f }, frame.Units(10f));
            frame.Paint.Stroke(square, gold with { W = id.Length > 0 ? 0.45f : 0.22f }, frame.Theme.Metrics.Hairline,
                frame.Units(10f));
            if (id.Length > 0)
            {
                AppMarks.DrawFace(frame, square.Inset(frame.Units(6f)), id, false);
            }
            else
            {
                frame.Text.DrawIn(square, "+",
                    new TextStyle(FontRole.Title, frame.Theme.Palette.InkMuted, TextAlign.Center));
            }

            if (!frame.Input.ConsumeClick(square))
            {
                continue;
            }

            if (picked.Length > 0)
            {
                display.PlaceQuickApp(index, picked);
                selected.Clear();
            }
            else if (id.Length > 0)
            {
                display.ClearQuickApp(index);
            }
            else
            {
                page = Page.Library;
                query = string.Empty;
                scroll.Reset();
            }
        }

        return row.Max.Y + frame.Units(8f);
    }

    private string FirstSelected()
    {
        foreach (var id in selected)
        {
            return id;
        }

        return string.Empty;
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
        }

        return row.Max.Y + frame.Units(8f);
    }

    private float DrawShelf(in AppletFrame frame, Rect area, bool hush)
    {
        FillVisible(forLibrary: false);
        GroupVisible();
        var columns = 4;
        var cell = frame.Units(86f);
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
                DrawLauncherTile(frame, tile, visible[first + slot], hush);
            }

            y += ((count + columns - 1) / columns) * (cell + gap) + frame.Units(4f);
        }

        return y - startY + frame.Units(12f);
    }

    private float DrawManageGrid(in AppletFrame frame, Rect area, bool hush)
    {
        FillVisible(forLibrary: false);
        var columns = 3;
        var gap = frame.Units(8f);
        var cellH = frame.Units(118f);
        var cellW = (area.Width - gap * (columns - 1)) / columns;
        var count = visible.Count + 1;
        for (var index = 0; index < count; index++)
        {
            var col = index % columns;
            var row = index / columns;
            var tile = Rect.FromSize(
                new Vector2(area.Min.X + col * (cellW + gap), area.Min.Y + row * (cellH + gap)),
                new Vector2(cellW, cellH));
            if (index == visible.Count)
            {
                DrawAddTile(frame, tile);
                continue;
            }

            DrawManageTile(frame, tile, visible[index], hush);
        }

        var rows = (count + columns - 1) / columns;
        return rows * (cellH + gap);
    }

    private float DrawLibrary(in AppletFrame frame, Rect area, bool hush)
    {
        FillVisible(forLibrary: true);
        if (visible.Count == 0)
        {
            frame.Text.DrawWrapped(area.TopSlice(frame.Units(48f)), "Every remaining app is already on the shelf.",
                new TextStyle(FontRole.Caption, frame.Theme.Palette.InkMuted));
            return frame.Units(56f);
        }

        var rowH = frame.Units(56f);
        var gap = frame.Units(8f);
        for (var index = 0; index < visible.Count; index++)
        {
            var row = Rect.FromSize(new Vector2(area.Min.X, area.Min.Y + index * (rowH + gap)),
                new Vector2(area.Width, rowH));
            DrawLibraryRow(frame, row, visible[index], hush);
        }

        return visible.Count * (rowH + gap);
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
        if (!frame.Input.ConsumeClick(cell))
        {
            return;
        }

        Open(frame.Router, id);
    }

    private void DrawManageTile(in AppletFrame frame, Rect tile, string id, bool hush)
    {
        var gold = frame.Theme.Palette.WarmAccent;
        var picked = selected.Contains(id);
        var hold = string.Equals(reorderHold, id, StringComparison.Ordinal);
        frame.Paint.Fill(tile, frame.Theme.Palette.SurfaceOverlay with { W = 0.55f }, frame.Units(14f));
        frame.Paint.Stroke(tile, (picked || hold) ? gold : gold with { W = 0.22f }, frame.Theme.Metrics.Hairline,
            frame.Units(14f));
        var handle = tile.Inset(frame.Units(8f)).TopSlice(frame.Units(12f)).RightSlice(frame.Units(16f));
        DrawGrip(frame.Paint, handle, frame.Theme.Palette.InkFaint);
        var icon = frame.Units(40f);
        var iconArea = Rect.FromSize(new Vector2(tile.Center.X - icon * 0.5f, tile.Min.Y + frame.Units(22f)),
            new Vector2(icon, icon));
        AppMarks.DrawFace(frame, iconArea, id, picked);
        var spec = AppShelf.Find(id);
        var name = TitleOf(id);
        var caption = spec?.Caption ?? (id.StartsWith("folder:", StringComparison.Ordinal) ? "Folder" : "App");
        var nameRow = Rect.FromSize(new Vector2(tile.Min.X + frame.Units(6f), iconArea.Max.Y + frame.Units(6f)),
            new Vector2(tile.Width - frame.Units(12f), frame.Units(16f)));
        frame.Text.DrawEllipsized(nameRow, name,
            new TextStyle(FontRole.CaptionStrong, frame.Theme.Palette.Ink, TextAlign.Center));
        var capRow = nameRow.Translate(new Vector2(0f, frame.Units(14f)));
        frame.Text.DrawEllipsized(capRow, caption,
            new TextStyle(FontRole.Caption, frame.Theme.Palette.InkMuted, TextAlign.Center));
        DrawBadge(frame, iconArea, id, hush);
        if (display.IsQuickApp(id))
        {
            frame.Paint.StrokeCircle(new Vector2(iconArea.Max.X - frame.Units(4f), iconArea.Min.Y + frame.Units(4f)),
                frame.Units(4f), gold, frame.Units(1.4f));
        }

        if (!reorder)
        {
            var minus = Rect.FromSize(new Vector2(tile.Min.X + frame.Units(8f), tile.Min.Y + frame.Units(8f)),
                new Vector2(frame.Units(18f), frame.Units(18f)));
            frame.Paint.FillCircle(minus.Center, minus.Width * 0.5f, frame.Theme.Palette.Negative with { W = 0.92f });
            frame.Text.DrawIn(minus, "−",
                new TextStyle(FontRole.CaptionStrong, frame.Theme.Palette.AccentInk, TextAlign.Center));
            if (frame.Input.ConsumeClick(minus))
            {
                display.RemoveApp(id);
                selected.Remove(id);
                return;
            }
        }

        if (!frame.Input.ConsumeClick(tile))
        {
            return;
        }

        if (reorder)
        {
            if (reorderHold is null)
            {
                reorderHold = id;
                return;
            }

            if (!string.Equals(reorderHold, id, StringComparison.Ordinal))
            {
                var delta = IndexOf(id) - IndexOf(reorderHold);
                display.MoveApp(reorderHold, delta);
            }

            reorderHold = null;
            return;
        }

        if (!selected.Add(id))
        {
            selected.Remove(id);
        }
    }

    private void DrawAddTile(in AppletFrame frame, Rect tile)
    {
        var gold = frame.Theme.Palette.WarmAccent;
        StrokeDashed(frame.Paint, tile, gold with { W = 0.72f }, MathF.Max(1.2f, frame.Units(1.2f)));
        frame.Text.DrawIn(tile.Inset(new Edges(0f, frame.Units(28f), 0f, 0f)).TopSlice(frame.Units(36f)), "+",
            new TextStyle(FontRole.Display, gold, TextAlign.Center));
        frame.Text.DrawIn(tile.BottomSlice(frame.Units(36f)), "Add App",
            new TextStyle(FontRole.CaptionStrong, gold, TextAlign.Center));
        if (frame.Input.ConsumeClick(tile))
        {
            page = Page.Library;
            selected.Clear();
            reorder = false;
            query = string.Empty;
            scroll.Reset();
        }
    }

    private void DrawLibraryRow(in AppletFrame frame, Rect row, string id, bool hush)
    {
        var gold = frame.Theme.Palette.WarmAccent;
        frame.Paint.Fill(row, frame.Theme.Palette.SurfaceOverlay with { W = 0.5f }, frame.Units(12f));
        var icon = frame.Units(36f);
        var iconArea = Rect.FromSize(new Vector2(row.Min.X + frame.Units(10f), row.Center.Y - icon * 0.5f),
            new Vector2(icon, icon));
        AppMarks.DrawFace(frame, iconArea, id, false);
        var spec = AppShelf.Find(id);
        var copy = row.Inset(new Edges(frame.Units(54f), 0f, frame.Units(72f), 0f));
        frame.Text.DrawIn(copy.TopSlice(copy.Height * 0.55f), spec?.Name ?? id,
            new TextStyle(FontRole.BodyStrong, frame.Theme.Palette.Ink));
        frame.Text.DrawIn(copy.BottomSlice(copy.Height * 0.45f), spec?.Caption ?? string.Empty,
            new TextStyle(FontRole.Caption, frame.Theme.Palette.InkMuted));
        var add = row.RightSlice(frame.Units(64f)).Inset(frame.Units(8f));
        frame.Paint.Stroke(add, gold, frame.Theme.Metrics.Hairline, frame.Units(8f));
        frame.Text.DrawIn(add, "Add", new TextStyle(FontRole.CaptionStrong, gold, TextAlign.Center));
        DrawBadge(frame, iconArea, id, hush);
        if (frame.Input.ConsumeClick(row) || frame.Input.ConsumeClick(add))
        {
            display.InstallApp(id);
        }
    }

    private void DrawManageFooter(in AppletFrame frame, Rect bar, Vector4 gold)
    {
        frame.Paint.Fill(bar, frame.Theme.Palette.SurfaceRaised with { W = 0.72f }, frame.Units(10f));
        var left = bar.LeftSlice(bar.Width * 0.5f);
        var right = bar.RightSlice(bar.Width * 0.5f);
        frame.Text.DrawIn(left, "Create Folder", new TextStyle(FontRole.CaptionStrong, gold, TextAlign.Center));
        frame.Text.DrawIn(right, "Add to Quick Apps", new TextStyle(FontRole.CaptionStrong, gold, TextAlign.Center));
        frame.Paint.Line(new Vector2(bar.Center.X, bar.Min.Y + frame.Units(8f)),
            new Vector2(bar.Center.X, bar.Max.Y - frame.Units(8f)), gold with { W = 0.28f }, frame.Theme.Metrics.Hairline);
        if (frame.Input.ConsumeClick(left) && selected.Count > 0)
        {
            display.CreateAppFolder(new List<string>(selected));
            selected.Clear();
        }

        if (frame.Input.ConsumeClick(right) && selected.Count > 0)
        {
            foreach (var id in selected)
            {
                display.TryAddQuickApp(id);
            }

            selected.Clear();
        }
    }

    private void Open(IRouter router, string id)
    {
        if (display.TryFolder(id, out _, out var children))
        {
            openFolder = id;
            _ = children;
            scroll.Reset();
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

        router.Open(id);
    }

    private void FillVisible(bool forLibrary)
    {
        visible.Clear();
        IEnumerable<string> source;
        if (openFolder is not null && !forLibrary && page == Page.Shelf)
        {
            display.TryFolder(openFolder, out _, out var children);
            source = children;
        }
        else if (forLibrary)
        {
            source = LibraryIds();
        }
        else
        {
            source = display.InstalledApps;
        }

        var needle = query.Trim();
        foreach (var id in source)
        {
            if (!Matches(id, needle))
            {
                continue;
            }

            visible.Add(id);
        }
    }

    private IEnumerable<string> LibraryIds()
    {
        var nested = new HashSet<string>(StringComparer.Ordinal);
        foreach (var packed in display.AppFolders)
        {
            var parts = packed.Split('\u001f');
            if (parts.Length < 3 || parts[2].Length == 0)
            {
                continue;
            }

            foreach (var child in parts[2].Split(','))
            {
                nested.Add(child);
            }
        }

        for (var index = 0; index < AppShelf.Catalog.Length; index++)
        {
            var id = AppShelf.Catalog[index].Id;
            if (display.IsOnShelf(id) || nested.Contains(id))
            {
                continue;
            }

            yield return id;
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

    private static void DrawGrip(IPaintSurface paint, Rect area, Vector4 ink)
    {
        for (var row = 0; row < 2; row++)
        {
            for (var col = 0; col < 3; col++)
            {
                var point = new Vector2(area.Min.X + (col + 0.5f) * (area.Width / 3f),
                    area.Min.Y + (row + 0.5f) * (area.Height / 2f));
                paint.FillCircle(point, 1.4f, ink);
            }
        }
    }

    private static void StrokeDashed(IPaintSurface paint, Rect box, Vector4 color, float thickness)
    {
        var dash = 7f;
        var gap = 5f;
        Trace(box.Min, new Vector2(box.Max.X, box.Min.Y));
        Trace(new Vector2(box.Max.X, box.Min.Y), box.Max);
        Trace(box.Max, new Vector2(box.Min.X, box.Max.Y));
        Trace(new Vector2(box.Min.X, box.Max.Y), box.Min);
        return;

        void Trace(Vector2 from, Vector2 to)
        {
            var delta = to - from;
            var length = delta.Length();
            if (length < 1f)
            {
                return;
            }

            var dir = delta / length;
            var walked = 0f;
            while (walked < length)
            {
                var start = from + dir * walked;
                var end = from + dir * MathF.Min(walked + dash, length);
                paint.Line(start, end, color, thickness);
                walked += dash + gap;
            }
        }
    }
}
