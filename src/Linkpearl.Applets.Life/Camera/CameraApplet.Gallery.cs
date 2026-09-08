using System.Globalization;
using Linkpearl.Applets;
using Linkpearl.Geometry;
using Linkpearl.Input;
using Linkpearl.Layout;
using Linkpearl.Media;
using Linkpearl.Painting;

namespace Linkpearl.Applets.Life.Camera;

public sealed partial class CameraApplet
{
    private void DrawGallery(in AppletFrame frame)
    {
        if (mode == Mode.MakeFolder)
        {
            DrawMakeFolder(frame);
            return;
        }

        if (confirmRemove)
        {
            DrawRemoveConfirm(frame);
            return;
        }

        if (confirmBulkRemove)
        {
            DrawBulkRemoveConfirm(frame);
            return;
        }

        var tools = frame.Content.TopSlice(frame.Units(36f));
        if (picking)
        {
            DrawPickBar(frame, tools);
        }
        else
        {
            var upload = tools.RightSlice(frame.Units(72f)).Inset(new Edges(0f, frame.Units(4f), frame.Units(8f),
                frame.Units(4f)));
            var album = tools.RightSlice(frame.Units(168f)).LeftSlice(frame.Units(88f)).Inset(new Edges(0f,
                frame.Units(4f), frame.Units(8f), frame.Units(4f)));
            var select = tools.RightSlice(frame.Units(236f)).LeftSlice(frame.Units(60f)).Inset(new Edges(0f,
                frame.Units(4f), frame.Units(8f), frame.Units(4f)));
            var head = tools.Inset(new Edges(frame.Units(12f), frame.Units(6f), frame.Units(244f), 0f));
            frame.Text.DrawEllipsized(head, GalleryTitle(), new TextStyle(FontRole.Title, PhotosChrome.Ink));
            if (library.Shots.Count > 0)
            {
                ActionChip(frame, select, "Select");
                if (frame.Input.ConsumeClick(select))
                {
                    BeginPick(string.Empty);
                    return;
                }
            }

            ActionChip(frame, album, place.StartsWith("f:", StringComparison.Ordinal) ? "Rename" : "Album");
            ActionChip(frame, upload, "Upload");
            if (frame.Input.ConsumeClick(album))
            {
                if (place.StartsWith("f:", StringComparison.Ordinal))
                {
                    folderDraft = CurrentFolderTitle();
                    mode = Mode.MakeFolder;
                    return;
                }

                var made = library.CreateFolder(string.Empty);
                if (made is not null)
                {
                    place = "f:" + made.Id;
                    scroll = 0f;
                    RebuildAlbums();
                }

                return;
            }

            if (frame.Input.ConsumeClick(upload))
            {
                BeginUpload();
                return;
            }
        }

        var body = frame.Content.Inset(new Edges(frame.Units(8f), frame.Units(40f), frame.Units(8f), frame.Units(8f)));
        if (confirmDropFolder)
        {
            DrawConfirm(frame, body, "Remove this album? Photos stay in Gallery by date.", () =>
            {
                library.DropFolder(place[2..]);
                place = string.Empty;
                confirmDropFolder = false;
                RebuildAlbums();
            }, () => confirmDropFolder = false);
            return;
        }

        var content = place.StartsWith("f:", StringComparison.Ordinal) ? DrawFolder(frame, body, place[2..])
            : place.StartsWith("d:", StringComparison.Ordinal) ? DrawShotGrid(frame, body, library.InAlbum(place[2..]))
            : DrawRoot(frame, body);
        PhotosChrome.Wheel(frame, body, ref scroll, content);
        DrawShotMenu(frame, frame.Content);
    }

    private string GalleryTitle()
    {
        if (place.StartsWith("f:", StringComparison.Ordinal))
        {
            return library.FolderOf(place[2..])?.Title ?? "Album";
        }

        if (place.StartsWith("d:", StringComparison.Ordinal))
        {
            return AlbumTitle(place[2..]);
        }

        return "Gallery";
    }

    private string CurrentFolderTitle() =>
        place.StartsWith("f:", StringComparison.Ordinal) ? library.FolderOf(place[2..])?.Title ?? string.Empty
            : string.Empty;

    private void BeginUpload()
    {
        if (library.GposeFolderReady())
        {
            files.BeginImagePickFrom(library.GposeFolder);
        }
        else
        {
            files.BeginImagePick();
        }

        uploadWait = true;
    }

    private void BeginGposeLink()
    {
        var start = library.GposeFolderReady()
            ? library.GposeFolder
            : PhotoLibrary.SuggestedGposeFolder();
        if (start.Length > 0)
        {
            files.BeginFolderPickFrom(start);
        }
        else
        {
            files.BeginFolderPick();
        }

        gposeWait = true;
    }

    private void FinishGposeLink()
    {
        if (!gposeWait || !files.TryTakeFolder(out var folder))
        {
            return;
        }

        gposeWait = false;
        if (folder.Length == 0 || !Directory.Exists(folder))
        {
            return;
        }

        library.SetGposeFolder(folder);
    }

    private void FinishUpload()
    {
        if (!uploadWait || !files.TryTakeImages(out var picked))
        {
            return;
        }

        uploadWait = false;
        if (picked.Count == 0)
        {
            return;
        }

        var folder = place.StartsWith("f:", StringComparison.Ordinal) ? place[2..] : string.Empty;
        library.Import(picked, clock.Now, folder);
        RebuildAlbums();
        scroll = 0f;
        pane = Pane.Gallery;
        mode = Mode.Page;
    }

    private float DrawRoot(in AppletFrame frame, Rect viewport)
    {
        frame.Paint.PushClip(viewport);
        var cursor = viewport.Min.Y - scroll;
        var total = 0f;
        var add = Rect.FromSize(new Vector2(viewport.Min.X, cursor),
            new Vector2(viewport.Width, frame.Units(36f)));
        if (add.Overlaps(viewport))
        {
            ActionChip(frame, add, "+ Album");
            if (frame.Input.ConsumeClick(add))
            {
                var made = library.CreateFolder(string.Empty);
                if (made is not null)
                {
                    place = "f:" + made.Id;
                    scroll = 0f;
                    RebuildAlbums();
                }
            }
        }

        cursor += frame.Units(44f);
        total += frame.Units(44f);
        var gpose = Rect.FromSize(new Vector2(viewport.Min.X, cursor),
            new Vector2(viewport.Width, frame.Units(52f)));
        if (gpose.Overlaps(viewport))
        {
            DrawGposeRow(frame, gpose);
        }

        cursor += frame.Units(60f);
        total += frame.Units(60f);
        var books = library.Folders;
        if (books.Count > 0)
        {
            var label = Rect.FromSize(new Vector2(viewport.Min.X, cursor),
                new Vector2(viewport.Width, frame.Units(22f)));
            if (label.Overlaps(viewport))
            {
                frame.Text.DrawIn(label, "Albums",
                    new TextStyle(FontRole.CaptionStrong, PhotosChrome.Mute));
            }

            cursor += frame.Units(24f);
            total += frame.Units(24f);
            for (var index = 0; index < books.Count; index++)
            {
                var row = Rect.FromSize(new Vector2(viewport.Min.X, cursor),
                    new Vector2(viewport.Width, frame.Units(64f)));
                if (row.Overlaps(viewport))
                {
                    DrawFolderRow(frame, row, books[index]);
                }

                cursor += frame.Units(70f);
                total += frame.Units(70f);
            }
        }

        if (albums.Count == 0 && books.Count == 0)
        {
            frame.Text.DrawIn(viewport.TopSlice(frame.Units(28f)), "No photos yet",
                new TextStyle(FontRole.BodyStrong, PhotosChrome.Ink, TextAlign.Center));
            frame.Text.DrawWrapped(viewport.Inset(new Edges(frame.Units(16f), frame.Units(40f), frame.Units(16f), 0f)),
                "Tap Upload to pick pictures, then crop before they land here. Link a GPose folder so Upload opens there.",
                new TextStyle(FontRole.Caption, PhotosChrome.Mute, TextAlign.Center));
            frame.Paint.PopClip();
            return viewport.Height;
        }

        for (var album = 0; album < albums.Count; album++)
        {
            var group = albums[album];
            var header = Rect.FromSize(new Vector2(viewport.Min.X, cursor),
                new Vector2(viewport.Width, frame.Units(28f)));
            if (header.Overlaps(viewport))
            {
                frame.Text.DrawIn(header, group.Title,
                    new TextStyle(FontRole.BodyStrong, PhotosChrome.Ink));
                frame.Text.DrawIn(header.RightSlice(frame.Units(48f)),
                    group.Items.Count.ToString(CultureInfo.CurrentCulture),
                    new TextStyle(FontRole.Caption, PhotosChrome.Mute, TextAlign.Right));
                if (frame.Input.ConsumeClick(header))
                {
                    place = "d:" + group.Key;
                    scroll = 0f;
                }
            }

            cursor += frame.Units(30f);
            total += frame.Units(30f);
            var used = DrawThumbs(frame, viewport, ref cursor, group.Items);
            total += used;
        }

        frame.Paint.PopClip();
        return MathF.Max(total, viewport.Height);
    }

    private float DrawFolder(in AppletFrame frame, Rect viewport, string id)
    {
        var drop = viewport.TopSlice(frame.Units(28f)).RightSlice(frame.Units(72f));
        frame.Text.DrawIn(drop, "Remove",
            new TextStyle(FontRole.CaptionStrong, frame.Theme.Palette.Negative, TextAlign.Right));
        if (frame.Input.ConsumeClick(drop))
        {
            confirmDropFolder = true;
        }

        var grid = viewport.Inset(new Edges(0f, frame.Units(32f), 0f, 0f));
        return DrawShotGrid(frame, grid, library.InFolder(id)) + frame.Units(32f);
    }

    private float DrawShotGrid(in AppletFrame frame, Rect viewport, List<PhotoShot> items)
    {
        frame.Paint.PushClip(viewport);
        var cursor = viewport.Min.Y - scroll;
        var total = DrawThumbs(frame, viewport, ref cursor, items);
        if (items.Count == 0)
        {
            frame.Text.DrawIn(viewport.TopSlice(frame.Units(28f)), "Empty album",
                new TextStyle(FontRole.Caption, PhotosChrome.Mute, TextAlign.Center));
        }

        frame.Paint.PopClip();
        return MathF.Max(total, viewport.Height);
    }

    private float DrawThumbs(in AppletFrame frame, Rect viewport, ref float cursor, List<PhotoShot> items)
    {
        var columns = 3;
        var gap = frame.Units(3f);
        var cell = (viewport.Width - gap * (columns - 1)) / columns;
        var rowsNeeded = Math.Max(1, (items.Count + columns - 1) / columns);
        var gridArea = Rect.FromSize(new Vector2(viewport.Min.X, cursor),
            new Vector2(viewport.Width, rowsNeeded * (cell + gap) - gap));
        var grid = new TileGrid(gridArea, columns, rowsNeeded, gap);
        for (var item = 0; item < items.Count; item++)
        {
            var tile = grid.CellAt(item);
            if (tile.Overlaps(viewport))
            {
                DrawThumb(frame, tile, items[item]);
            }
        }

        var block = rowsNeeded * (cell + gap) - gap + frame.Units(12f);
        cursor += block;
        return block;
    }

    private void DrawGposeRow(in AppletFrame frame, Rect row)
    {
        frame.Paint.Fill(row, PhotosChrome.Tile, frame.Units(12f));
        var text = row.Inset(new Edges(frame.Units(12f), frame.Units(8f), frame.Units(12f), frame.Units(8f)));
        var linked = library.GposeFolderReady();
        frame.Text.DrawIn(text.TopSlice(frame.Units(18f)), linked ? "GPose folder" : "Link GPose folder",
            new TextStyle(FontRole.BodyStrong, PhotosChrome.Ink));
        var detail = linked
            ? library.GposeFolder
            : "ReShade or FFXIV screenshots. Upload opens this folder.";
        frame.Text.DrawEllipsized(text.BottomSlice(frame.Units(16f)), detail,
            new TextStyle(FontRole.Caption, PhotosChrome.Mute));
        if (frame.Input.ConsumeClick(row))
        {
            BeginGposeLink();
        }
    }

    private void DrawFolderRow(in AppletFrame frame, Rect row, PhotoFolder folder)
    {
        frame.Paint.Fill(row, PhotosChrome.Tile, frame.Units(12f));
        var cover = row.LeftSlice(row.Height).Inset(frame.Units(8f));
        var first = library.InFolder(folder.Id);
        if (first.Count > 0)
        {
            var texture = textures.FromFile(library.Absolute(first[0]));
            if (texture is { IsReady: true })
            {
                frame.Paint.PushClip(cover);
                PhotosChrome.Cover(frame, cover, texture);
                frame.Paint.PopClip();
            }
            else
            {
                PhotosChrome.Placeholder(frame, cover);
            }
        }
        else
        {
            PhotosChrome.Placeholder(frame, cover);
        }

        var text = row.Inset(new Edges(row.Height, frame.Units(12f), frame.Units(12f), frame.Units(12f)));
        frame.Text.DrawEllipsized(text.TopSlice(frame.Units(20f)), folder.Title,
            new TextStyle(FontRole.BodyStrong, PhotosChrome.Ink));
        frame.Text.DrawIn(text.BottomSlice(frame.Units(16f)),
            first.Count.ToString(CultureInfo.CurrentCulture) + " photos",
            new TextStyle(FontRole.Caption, PhotosChrome.Mute));
        if (frame.Input.ConsumeClick(row))
        {
            place = "f:" + folder.Id;
            scroll = 0f;
        }
    }

    private void DrawThumb(in AppletFrame frame, Rect tile, PhotoShot shot)
    {
        var texture = textures.FromFile(library.Absolute(shot));
        if (texture is { IsReady: true })
        {
            frame.Paint.PushClip(tile);
            PhotosChrome.Cover(frame, tile, texture);
            frame.Paint.PopClip();
        }
        else
        {
            PhotosChrome.Placeholder(frame, tile);
        }

        if (picking)
        {
            var on = picked.Contains(shot.Id);
            PhotosChrome.PickMark(frame, tile, on);
            if (frame.Input.ConsumeClick(tile) || frame.Input.ConsumeClick(tile, PointerButton.Secondary))
            {
                if (on)
                {
                    picked.Remove(shot.Id);
                }
                else
                {
                    picked.Add(shot.Id);
                }
            }

            return;
        }

        if (frame.Input.ConsumeClick(tile, PointerButton.Secondary))
        {
            menuShotId = shot.Id;
            menuAt = frame.Input.Pointer;
            return;
        }

        if (menuShotId.Length > 0)
        {
            return;
        }

        if (frame.Input.ConsumeClick(tile))
        {
            viewingId = shot.Id;
            mode = Mode.Viewer;
            confirmRemove = false;
            menuShotId = string.Empty;
        }
    }

    private void DrawPickBar(in AppletFrame frame, Rect tools)
    {
        var remove = tools.RightSlice(frame.Units(80f)).Inset(new Edges(0f, frame.Units(4f), frame.Units(8f),
            frame.Units(4f)));
        var cancel = tools.RightSlice(frame.Units(156f)).LeftSlice(frame.Units(68f)).Inset(new Edges(0f,
            frame.Units(4f), frame.Units(8f), frame.Units(4f)));
        var head = tools.Inset(new Edges(frame.Units(12f), frame.Units(6f), frame.Units(164f), 0f));
        var count = picked.Count;
        var title = count == 0 ? "Select photos" : count.ToString(CultureInfo.CurrentCulture) + " selected";
        frame.Text.DrawEllipsized(head, title, new TextStyle(FontRole.Title, PhotosChrome.Ink));
        ActionChip(frame, cancel, "Cancel");
        if (count > 0)
        {
            frame.Paint.Fill(remove, frame.Theme.Palette.Negative with { W = 0.88f }, frame.Units(12f));
            frame.Text.DrawIn(remove, "Delete",
                new TextStyle(FontRole.CaptionStrong, PhotosChrome.AccentInk, TextAlign.Center));
        }
        else
        {
            frame.Paint.Fill(remove, PhotosChrome.Tile, frame.Units(12f));
            frame.Text.DrawIn(remove, "Delete",
                new TextStyle(FontRole.CaptionStrong, PhotosChrome.Mute, TextAlign.Center));
        }

        if (frame.Input.ConsumeClick(cancel))
        {
            EndPick();
            return;
        }

        if (count > 0 && frame.Input.ConsumeClick(remove))
        {
            confirmBulkRemove = true;
        }
    }

    private void DrawMakeFolder(in AppletFrame frame)
    {
        var stack = new Stack(frame.Content.Inset(frame.Units(14f)), StackAxis.Vertical, frame.Units(10f));
        var rename = place.StartsWith("f:", StringComparison.Ordinal);
        frame.Text.DrawIn(stack.Take(frame.Units(24f)), rename ? "Rename album" : "New album",
            new TextStyle(FontRole.Title, PhotosChrome.Ink));
        folderDraft = frame.TextField.Draw("photo-folder", stack.Take(frame.Units(36f)), folderDraft, "Album name",
            32, out var submitted);
        var row = stack.Take(frame.Units(36f));
        ActionChip(frame, row.LeftSlice(row.Width * 0.48f), "Cancel");
        ActionChip(frame, row.RightSlice(row.Width * 0.48f), rename ? "Save" : "Create");
        if (frame.Input.ConsumeClick(row.LeftSlice(row.Width * 0.48f)))
        {
            mode = Mode.Page;
            folderDraft = string.Empty;
            return;
        }

        if (submitted || frame.Input.ConsumeClick(row.RightSlice(row.Width * 0.48f)))
        {
            if (rename)
            {
                library.RenameFolder(place[2..], folderDraft.Trim().Length > 0 ? folderDraft : CurrentFolderTitle());
                mode = Mode.Page;
            }
            else
            {
                var made = library.CreateFolder(folderDraft);
                if (made is not null && viewingId.Length > 0)
                {
                    library.MoveToFolder(viewingId, made.Id);
                    mode = Mode.Viewer;
                }
                else if (made is not null)
                {
                    place = "f:" + made.Id;
                    mode = Mode.Page;
                }
            }

            folderDraft = string.Empty;
            scroll = 0f;
            RebuildAlbums();
        }
    }

    private void DrawViewer(in AppletFrame frame)
    {
        var shot = library.Find(viewingId);
        if (shot is null)
        {
            mode = Mode.Page;
            viewingId = string.Empty;
            return;
        }

        if (confirmRemove)
        {
            DrawRemoveConfirm(frame);
            return;
        }

        var bar = frame.Content.TopSlice(frame.Units(40f));
        frame.Text.DrawIn(bar.LeftSlice(frame.Units(28f)), "‹",
            new TextStyle(FontRole.Title, PhotosChrome.Accent, TextAlign.Center));
        frame.Text.DrawEllipsized(bar.Inset(new Edges(frame.Units(32f), frame.Units(8f), frame.Units(8f), 0f)),
            ViewerTitle(shot),
            new TextStyle(FontRole.CaptionStrong, PhotosChrome.Ink));
        if (frame.Input.ConsumeClick(bar.LeftSlice(frame.Units(40f))))
        {
            mode = Mode.Page;
            pane = Pane.Gallery;
            viewingId = string.Empty;
            return;
        }

        var tools = frame.Content.BottomSlice(frame.Units(44f)).Inset(new Edges(frame.Units(8f), frame.Units(4f),
            frame.Units(8f), frame.Units(8f)));
        var apply = new Rect(new Vector2(frame.Content.Min.X + frame.Units(8f), tools.Min.Y - frame.Units(40f)),
            new Vector2(frame.Content.Max.X - frame.Units(8f), tools.Min.Y - frame.Units(6f)));
        var stage = new Rect(frame.Content.Min + new Vector2(frame.Units(8f), frame.Units(44f)),
            new Vector2(frame.Content.Max.X - frame.Units(8f), apply.Min.Y - frame.Units(6f)));
        var texture = textures.FromFile(library.Absolute(shot));
        if (texture is { IsReady: true })
        {
            PhotosChrome.Contain(frame, stage, texture);
        }
        else
        {
            PhotosChrome.Placeholder(frame, stage);
        }

        if (frame.Input.ConsumeClick(stage, PointerButton.Secondary))
        {
            menuShotId = shot.Id;
            menuAt = frame.Input.Pointer;
        }

        LandscapeHold.Draw(frame, display);
        ActionChip(frame, apply, "Set as background");
        if (menuShotId.Length == 0 && frame.Input.ConsumeClick(apply))
        {
            ApplyAsBackground(shot);
        }

        var width = tools.Width / 3f;
        Tool(frame, tools.Translate(new Vector2(0f, 0f)).WithWidth(width), "Edit", () => BeginCropShot(shot));
        Tool(frame, tools.Translate(new Vector2(width, 0f)).WithWidth(width), "Album", () => mode = Mode.Move);
        Tool(frame, tools.Translate(new Vector2(width * 2f, 0f)).WithWidth(width), "Remove",
            () => confirmRemove = true);
        DrawShotMenu(frame, frame.Content);
    }

    private string ViewerTitle(PhotoShot shot)
    {
        if (shot.Folder.Length > 0)
        {
            return (library.FolderOf(shot.Folder)?.Title ?? "Album") + "  ·  " + shot.Title;
        }

        return AlbumTitle(shot.Album) + "  ·  " + shot.Title;
    }

    private void BeginCropShot(PhotoShot shot)
    {
        cropExisting = shot.Id;
        cropSource = library.Absolute(shot);
        ResetCrop();
        mode = Mode.Crop;
    }

    private void CancelCrop()
    {
        mode = Mode.Viewer;
        cropSource = string.Empty;
        cropExisting = string.Empty;
    }

    private void ResetCrop()
    {
        cropX = 0f;
        cropY = 0f;
        cropW = 1f;
        cropH = 1f;
        cropTurns = 0;
        cropDrag = CropDrag.None;
        RefreshCropPreview();
    }

    private void RefreshCropPreview()
    {
        cropPreview = PhotoEdit.Preview(cropSource, cropTurns);
        cropKey = "crop:" + cropSource + ":" + cropTurns.ToString(CultureInfo.InvariantCulture);
    }

    private void DrawCrop(in AppletFrame frame)
    {
        var bar = frame.Content.TopSlice(frame.Units(36f));
        frame.Text.DrawIn(bar.Inset(frame.Units(12f)), "Edit",
            new TextStyle(FontRole.Title, PhotosChrome.Ink));
        var tools = frame.Content.BottomSlice(frame.Units(44f)).Inset(new Edges(frame.Units(8f), frame.Units(4f),
            frame.Units(8f), frame.Units(8f)));
        var stage = new Rect(frame.Content.Min + new Vector2(frame.Units(8f), frame.Units(40f)),
            new Vector2(frame.Content.Max.X - frame.Units(8f), tools.Min.Y - frame.Units(6f)));
        var texture = cropPreview is { Length: > 0 } ? textures.FromBytes(cropPreview, cropKey) : null;
        var dest = stage;
        if (texture is { IsReady: true })
        {
            dest = Contained(stage, texture.Size);
            frame.Paint.Image(texture, dest, Vector4.One);
        }
        else
        {
            PhotosChrome.Placeholder(frame, stage);
        }

        var box = CropBox(dest);
        frame.Paint.Stroke(box, PhotosChrome.Accent, frame.Units(2f), 0f);
        DrawHandle(frame, box.Min, dest);
        DrawHandle(frame, new Vector2(box.Max.X, box.Min.Y), dest);
        DrawHandle(frame, new Vector2(box.Min.X, box.Max.Y), dest);
        DrawHandle(frame, box.Max, dest);
        TickCrop(frame, dest, box);

        var width = tools.Width / 3f;
        Tool(frame, tools.LeftSlice(width).Inset(new Edges(0f, 0f, frame.Units(4f), 0f)), "Cancel", CancelCrop);
        Tool(frame, tools.Translate(new Vector2(width, 0f)).WithWidth(width).Inset(new Edges(frame.Units(4f), 0f,
            frame.Units(4f), 0f)), "Rotate", () =>
        {
            cropTurns++;
            RefreshCropPreview();
        });
        Tool(frame, tools.RightSlice(width).Inset(new Edges(frame.Units(4f), 0f, 0f, 0f)), "Save", CommitCrop);
    }

    private void CommitCrop()
    {
        var shot = library.Find(cropExisting);
        if (shot is not null)
        {
            var copy = library.CopyEdited(shot, clock.Now, cropX, cropY, cropW, cropH, cropTurns);
            if (copy is not null)
            {
                viewingId = copy.Id;
                RebuildAlbums();
            }
        }

        cropExisting = string.Empty;
        cropSource = string.Empty;
        mode = Mode.Viewer;
    }

    private void TickCrop(in AppletFrame frame, Rect dest, Rect box)
    {
        var at = frame.Input.Pointer;
        var held = frame.Input.IsHeld();
        if (!held)
        {
            cropDrag = CropDrag.None;
        }

        var pad = frame.Units(14f);
        if (frame.Input.WasPressed(HandleAt(box.Min, pad)))
        {
            ArmCrop(CropDrag.NorthWest, at);
        }
        else if (frame.Input.WasPressed(HandleAt(new Vector2(box.Max.X, box.Min.Y), pad)))
        {
            ArmCrop(CropDrag.NorthEast, at);
        }
        else if (frame.Input.WasPressed(HandleAt(new Vector2(box.Min.X, box.Max.Y), pad)))
        {
            ArmCrop(CropDrag.SouthWest, at);
        }
        else if (frame.Input.WasPressed(HandleAt(box.Max, pad)))
        {
            ArmCrop(CropDrag.SouthEast, at);
        }
        else if (frame.Input.WasPressed(box))
        {
            ArmCrop(CropDrag.Move, at);
        }

        if (!held || cropDrag == CropDrag.None || dest.Width < 1f || dest.Height < 1f)
        {
            return;
        }

        var dx = (at.X - cropGrab.X) / dest.Width;
        var dy = (at.Y - cropGrab.Y) / dest.Height;
        var min = 0.08f;
        if (cropDrag == CropDrag.Move)
        {
            cropX = Math.Clamp(grabX + dx, 0f, 1f - grabW);
            cropY = Math.Clamp(grabY + dy, 0f, 1f - grabH);
            return;
        }

        var left = grabX;
        var top = grabY;
        var right = grabX + grabW;
        var bottom = grabY + grabH;
        if (cropDrag is CropDrag.NorthWest or CropDrag.SouthWest)
        {
            left = Math.Clamp(grabX + dx, 0f, right - min);
        }

        if (cropDrag is CropDrag.NorthEast or CropDrag.SouthEast)
        {
            right = Math.Clamp(grabX + grabW + dx, left + min, 1f);
        }

        if (cropDrag is CropDrag.NorthWest or CropDrag.NorthEast)
        {
            top = Math.Clamp(grabY + dy, 0f, bottom - min);
        }

        if (cropDrag is CropDrag.SouthWest or CropDrag.SouthEast)
        {
            bottom = Math.Clamp(grabY + grabH + dy, top + min, 1f);
        }

        cropX = left;
        cropY = top;
        cropW = right - left;
        cropH = bottom - top;
    }

    private void ArmCrop(CropDrag drag, Vector2 at)
    {
        cropDrag = drag;
        cropGrab = at;
        grabX = cropX;
        grabY = cropY;
        grabW = cropW;
        grabH = cropH;
    }

    private Rect CropBox(Rect dest) =>
        Rect.FromSize(dest.Min + new Vector2(cropX * dest.Width, cropY * dest.Height),
            new Vector2(cropW * dest.Width, cropH * dest.Height));

    private static Rect HandleAt(Vector2 point, float pad) =>
        Rect.FromSize(point - new Vector2(pad, pad), new Vector2(pad * 2f, pad * 2f));

    private static void DrawHandle(in AppletFrame frame, Vector2 point, Rect dest)
    {
        var size = frame.Units(10f);
        var handle = Rect.FromSize(point - new Vector2(size * 0.5f, size * 0.5f), new Vector2(size, size));
        if (!dest.Expand(frame.Units(4f)).Overlaps(handle))
        {
            return;
        }

        frame.Paint.Fill(handle, PhotosChrome.Accent, 2f);
    }

    private static Rect Contained(Rect area, Vector2 size)
    {
        if (size.X <= 0f || size.Y <= 0f)
        {
            return area;
        }

        var image = size.X / size.Y;
        var box = area.Width / MathF.Max(area.Height, 1f);
        if (image > box)
        {
            var height = area.Width / image;
            return Rect.FromSize(new Vector2(area.Min.X, area.Min.Y + (area.Height - height) * 0.5f),
                new Vector2(area.Width, height));
        }

        var width = area.Height * image;
        return Rect.FromSize(new Vector2(area.Min.X + (area.Width - width) * 0.5f, area.Min.Y),
            new Vector2(width, area.Height));
    }

    private void DrawMove(in AppletFrame frame)
    {
        var shot = library.Find(viewingId);
        if (shot is null)
        {
            mode = Mode.Page;
            return;
        }

        var stack = new Stack(frame.Content.Inset(frame.Units(12f)), StackAxis.Vertical, frame.Units(8f));
        frame.Text.DrawIn(stack.Take(frame.Units(24f)), "Move to album",
            new TextStyle(FontRole.Title, PhotosChrome.Ink));
        var dates = stack.Take(frame.Units(40f));
        frame.Paint.Fill(dates, PhotosChrome.Tile, frame.Units(12f));
        frame.Text.DrawIn(dates.Inset(frame.Units(10f)), "Keep with dates",
            new TextStyle(FontRole.BodyStrong, PhotosChrome.Ink));
        if (frame.Input.ConsumeClick(dates))
        {
            library.MoveToFolder(shot.Id, string.Empty);
            mode = Mode.Viewer;
            RebuildAlbums();
            return;
        }

        var books = library.Folders;
        for (var index = 0; index < books.Count; index++)
        {
            var row = stack.Take(frame.Units(40f));
            frame.Paint.Fill(row, PhotosChrome.Tile, frame.Units(12f));
            frame.Text.DrawEllipsized(row.Inset(frame.Units(10f)), books[index].Title,
                new TextStyle(FontRole.BodyStrong, PhotosChrome.Ink));
            if (frame.Input.ConsumeClick(row))
            {
                library.MoveToFolder(shot.Id, books[index].Id);
                mode = Mode.Viewer;
                RebuildAlbums();
                return;
            }
        }

        var make = stack.Take(frame.Units(36f));
        ActionChip(frame, make, "New album");
        if (frame.Input.ConsumeClick(make))
        {
            folderDraft = string.Empty;
            mode = Mode.MakeFolder;
        }
    }

    private static void DrawConfirm(in AppletFrame frame, Rect area, string copy, Action yes, Action no)
    {
        var stack = new Stack(area.Inset(frame.Units(12f)), StackAxis.Vertical, frame.Units(10f));
        frame.Text.DrawWrapped(stack.Take(frame.Units(56f)), copy,
            new TextStyle(FontRole.Body, PhotosChrome.Ink, TextAlign.Center));
        var row = stack.Take(frame.Units(36f));
        ActionChip(frame, row.LeftSlice(row.Width * 0.48f), "Keep");
        frame.Paint.Fill(row.RightSlice(row.Width * 0.48f), frame.Theme.Palette.Negative with { W = 0.88f },
            frame.Units(12f));
        frame.Text.DrawIn(row.RightSlice(row.Width * 0.48f), "Remove",
            new TextStyle(FontRole.CaptionStrong, PhotosChrome.AccentInk, TextAlign.Center));
        if (frame.Input.ConsumeClick(row.LeftSlice(row.Width * 0.48f)))
        {
            no();
        }

        if (frame.Input.ConsumeClick(row.RightSlice(row.Width * 0.48f)))
        {
            yes();
        }
    }

    private static void ActionChip(in AppletFrame frame, Rect area, string label)
    {
        frame.Paint.Fill(area, PhotosChrome.Accent, frame.Units(12f));
        frame.Text.DrawIn(area, label,
            new TextStyle(FontRole.CaptionStrong, PhotosChrome.AccentInk, TextAlign.Center));
    }

    private void DrawShotMenu(in AppletFrame frame, Rect bounds)
    {
        if (menuShotId.Length == 0)
        {
            return;
        }

        var shot = library.Find(menuShotId);
        if (shot is null)
        {
            menuShotId = string.Empty;
            return;
        }

        var labels = new[] { "Select", "Set as background", "Remove" };
        var width = frame.Units(176f);
        var rowH = frame.Units(34f);
        var height = rowH * labels.Length + frame.Units(8f);
        var left = Math.Clamp(menuAt.X, bounds.Min.X, bounds.Max.X - width);
        var top = Math.Clamp(menuAt.Y, bounds.Min.Y, bounds.Max.Y - height);
        var box = Rect.FromSize(new Vector2(left, top), new Vector2(width, height));
        var gold = frame.Theme.Palette.WarmAccent;
        var radius = frame.Units(10f);
        frame.Paint.Fill(box, frame.Theme.Palette.SurfaceRaised with { W = 0.98f }, radius);
        frame.Paint.Stroke(box, gold with { W = 0.55f }, frame.Theme.Metrics.Hairline, radius);
        for (var index = 0; index < labels.Length; index++)
        {
            var row = Rect.FromSize(box.Min + new Vector2(0f, frame.Units(4f) + index * rowH),
                new Vector2(width, rowH)).Inset(new Edges(frame.Units(4f), 0f));
            var hover = frame.Input.IsHovering(row);
            if (hover)
            {
                frame.Paint.Fill(row, gold with { W = 0.20f }, frame.Units(8f));
            }

            var ink = labels[index] == "Remove"
                ? frame.Theme.Palette.Negative
                : hover
                    ? gold
                    : frame.Theme.Palette.Ink;
            frame.Text.DrawIn(row.Inset(new Edges(frame.Units(10f), 0f)), labels[index],
                new TextStyle(FontRole.CaptionStrong, ink));
            if (frame.Input.ConsumeClick(row))
            {
                menuShotId = string.Empty;
                if (labels[index] == "Select")
                {
                    BeginPick(shot.Id);
                    return;
                }

                if (labels[index] == "Remove")
                {
                    viewingId = shot.Id;
                    confirmRemove = true;
                    return;
                }

                ApplyAsBackground(shot);
                return;
            }
        }

        frame.Input.ConsumeClick(box);
        frame.Input.ConsumeClick(box, PointerButton.Secondary);
        frame.Input.Claim(box);
        if (frame.Input.ConsumeClick(bounds) || frame.Input.ConsumeClick(bounds, PointerButton.Secondary))
        {
            menuShotId = string.Empty;
        }
    }

    private void DrawRemoveConfirm(in AppletFrame frame)
    {
        var shot = library.Find(viewingId);
        if (shot is null)
        {
            confirmRemove = false;
            viewingId = string.Empty;
            return;
        }

        DrawConfirm(frame, frame.Content.Inset(frame.Units(16f)), "Remove this photo from Gallery?", () =>
        {
            if (library.Remove(shot.Id, out var path))
            {
                textures.ForgetFile(path);
            }

            confirmRemove = false;
            viewingId = string.Empty;
            mode = Mode.Page;
            pane = Pane.Gallery;
            RebuildAlbums();
        }, () => confirmRemove = false);
    }

    private void DrawBulkRemoveConfirm(in AppletFrame frame)
    {
        var count = picked.Count;
        if (count == 0)
        {
            confirmBulkRemove = false;
            return;
        }

        var copy = count == 1
            ? "Remove this photo from Gallery?"
            : "Remove " + count.ToString(CultureInfo.CurrentCulture) + " photos from Gallery?";
        DrawConfirm(frame, frame.Content.Inset(frame.Units(16f)), copy, () =>
        {
            var forgotten = new List<string>();
            var ids = new string[picked.Count];
            picked.CopyTo(ids);
            library.RemoveMany(ids, forgotten);
            for (var index = 0; index < forgotten.Count; index++)
            {
                textures.ForgetFile(forgotten[index]);
            }

            EndPick();
            RebuildAlbums();
        }, () => confirmBulkRemove = false);
    }

    private void ApplyAsBackground(PhotoShot shot)
    {
        if (!PlateFiles.TryImport(paths, library.Absolute(shot), out var fileName))
        {
            return;
        }

        if (display.UsingCustomPlate)
        {
            var previous = display.CustomPlateFile;
            if (string.Equals(previous, fileName, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            if (HasPlate(previous))
            {
                display.SwapCustomPlate(previous, fileName);
                PlateFiles.Delete(paths, previous);
            }
            else
            {
                PlacePlate(fileName);
            }

            display.CustomPlateFile = fileName;
            return;
        }

        PlacePlate(fileName);
    }

    private void PlacePlate(string fileName)
    {
        if (display.CustomPlateFiles.Count < PlateFiles.MaxSlots)
        {
            display.AddCustomPlate(fileName);
            return;
        }

        var previous = display.CustomPlateFiles[^1];
        display.SwapCustomPlate(previous, fileName);
        PlateFiles.Delete(paths, previous);
        display.CustomPlateFile = fileName;
    }

    private bool HasPlate(string fileName)
    {
        var files = display.CustomPlateFiles;
        for (var index = 0; index < files.Count; index++)
        {
            if (string.Equals(files[index], fileName, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static void Tool(in AppletFrame frame, Rect area, string label, Action press)
    {
        frame.Paint.Fill(area.Inset(frame.Units(3f)), PhotosChrome.Tile, frame.Units(10f));
        frame.Text.DrawIn(area, label,
            new TextStyle(FontRole.CaptionStrong, PhotosChrome.Ink, TextAlign.Center));
        if (frame.Input.ConsumeClick(area))
        {
            press();
        }
    }

    private void RebuildAlbums()
    {
        albums.Clear();
        Dictionary<string, List<PhotoShot>> map = new(StringComparer.Ordinal);
        var shots = library.Shots;
        for (var index = 0; index < shots.Count; index++)
        {
            var shot = shots[index];
            if (shot.Folder.Length > 0)
            {
                continue;
            }

            if (!map.TryGetValue(shot.Album, out var list))
            {
                list = new List<PhotoShot>();
                map[shot.Album] = list;
            }

            list.Add(shot);
        }

        var keys = new List<string>(map.Keys);
        keys.Sort(static (left, right) => string.CompareOrdinal(right, left));
        for (var index = 0; index < keys.Count; index++)
        {
            var key = keys[index];
            albums.Add((key, AlbumTitle(key), map[key]));
        }
    }

    private string AlbumTitle(string key)
    {
        if (!DateTime.TryParseExact(key, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None,
                out var day))
        {
            return key;
        }

        var today = DateOnly.FromDateTime(clock.Now.LocalDateTime);
        var album = DateOnly.FromDateTime(day);
        if (album == today)
        {
            return "Today";
        }

        if (album == today.AddDays(-1))
        {
            return "Yesterday";
        }

        return day.ToString("MMMM d, yyyy", CultureInfo.CurrentCulture);
    }
}
