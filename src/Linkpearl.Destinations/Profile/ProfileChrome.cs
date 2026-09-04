using System.Globalization;
using Linkpearl.Applets;
using Linkpearl.Badges;
using Linkpearl.Cards;
using Linkpearl.Destinations.Home;
using Linkpearl.Geometry;
using Linkpearl.Layout;
using Linkpearl.Media;
using Linkpearl.Modules;
using Linkpearl.Net;
using Linkpearl.Painting;
using Linkpearl.Platform;
using Linkpearl.Preferences;

namespace Linkpearl.Destinations.Profile;

public sealed class ProfileChrome
{
    private const float TitleScale = 1f;
    public const float SheetHeightUnits = 172f;

    private enum Sheet : byte
    {
        None = 0,
        Edit = 1,
        PlacePortrait = 2,
        PickBadge = 3,
    }

    private readonly BadgeBook book;
    private readonly HostPaths paths;
    private readonly ITextureSource textures;
    private readonly IFilePicker files;
    private readonly IPearlHub pearl;
    private readonly IGameSession game;
    private readonly DisplayPreferences display;
    private readonly bool development;
    private Sheet sheet;
    private bool returnToEdit;
    private bool draggingPhoto;
    private int pickSlot;
    private float titleClock;
    private ColorWell colorWell;
    private bool nameGlowOpen;
    private bool titleGlowOpen;
    private readonly InkPicker inkPicker = new();

    private enum ColorWell : byte
    {
        None = 0,
        NameInk = 1,
        NameGlow = 2,
        TitleInk = 3,
        TitleGlow = 4,
    }

    public ProfileChrome(BadgeBook book, HostPaths paths, ITextureSource textures, IFilePicker files,
        IPearlHub pearl, IGameSession game, DisplayPreferences display, bool development)
    {
        this.book = book;
        this.paths = paths;
        this.textures = textures;
        this.files = files;
        this.pearl = pearl;
        this.game = game;
        this.display = display;
        this.development = development;
    }

    public bool OverlayOpen => sheet != Sheet.None;

    public void OpenEdit()
    {
        returnToEdit = true;
        sheet = Sheet.Edit;
    }

    public void Close()
    {
        if (sheet == Sheet.PlacePortrait)
        {
            book.CommitPortrait();
        }

        draggingPhoto = false;
        returnToEdit = false;
        sheet = Sheet.None;
    }

    public bool Back()
    {
        if (sheet == Sheet.None)
        {
            return false;
        }

        if (sheet == Sheet.Edit)
        {
            Close();
            return true;
        }

        DismissPicker();
        return true;
    }

    public float DrawCard(in AppletFrame frame, Rect area, string name, string job, string world,
        string place = "", uint jobIconId = 0)
    {
        var gold = frame.Theme.Palette.WarmAccent;
        var pad = frame.Units(4f);
        var inner = area.Inset(new Edges(pad, 0f, pad, pad));
        var trayH = frame.Units(36f);
        var trayGap = frame.Units(5f);
        var face = MathF.Min(frame.Units(88f), MathF.Max(frame.Units(64f), inner.Height - trayH - trayGap));
        var lift = frame.Units(10f);
        var clusterY = inner.Min.Y - lift;
        var bandBottom = inner.Min.Y + face;
        var avatar = Rect.FromSize(new Vector2(inner.Min.X, clusterY), new Vector2(face, face));
        DrawPortrait(frame, avatar);
        if (frame.Input.ConsumeClick(avatar))
        {
            OpenPortrait();
        }

        var well = face * 0.75f;
        var featured = Rect.FromSize(
            new Vector2(inner.Max.X - well, avatar.Min.Y + (avatar.Height - well) * 0.5f),
            new Vector2(well, well));
        DrawMark(frame, featured, book.FeaturedId, gold);
        if (frame.Input.ConsumeClick(featured))
        {
            OpenBadgePick(-1);
        }

        var copyLeft = avatar.Max.X + frame.Units(10f);
        var copyRight = featured.Min.X - frame.Units(8f);
        var head = new Rect(new Vector2(copyLeft, clusterY), new Vector2(copyRight, bandBottom));
        DrawIdentity(frame, head, name, job, world, place, jobIconId, gold);

        var bottom = Rect.FromSize(new Vector2(inner.Min.X, bandBottom + trayGap),
            new Vector2(inner.Width, trayH));
        var tray = bottom;
        if (sheet != Sheet.Edit)
        {
            var edit = bottom.RightSlice(frame.Units(124f));
            DrawEdit(frame, edit, gold);
            if (frame.Input.ConsumeClick(edit))
            {
                OpenEdit();
            }

            tray = new Rect(bottom.Min, new Vector2(edit.Min.X - frame.Units(8f), bottom.Max.Y));
        }

        DrawSlots(frame, tray, gold);
        return area.Height;
    }

    public void DrawStudioEdit(in AppletFrame frame, Rect area)
    {
        var gold = frame.Theme.Palette.WarmAccent;
        CardChrome.DrawGold(frame, area);
        var inset = area.Inset(new Edges(frame.Units(3f), frame.Units(1.5f), frame.Units(3f), frame.Units(1.5f)));
        var scale = 0.64f;
        var iconSide = MathF.Min(frame.Units(7f), inset.Height);
        var gap = frame.Units(2f);
        var label = "EDIT PROFILE";
        var textW = frame.Text.Measure(label, FontRole.CaptionStrong).X * scale;
        var cluster = MathF.Min(inset.Width, iconSide + gap + textW);
        var startX = inset.Center.X - cluster * 0.5f;
        var icon = Rect.FromSize(new Vector2(startX, inset.Center.Y - iconSide * 0.5f),
            new Vector2(iconSide, iconSide));
        DrawPencil(frame, icon, gold);
        var text = new Rect(new Vector2(icon.Max.X + gap, inset.Min.Y),
            new Vector2(MathF.Min(icon.Max.X + gap + textW, inset.Max.X), inset.Max.Y));
        frame.Text.DrawEllipsized(text, label,
            new TextStyle(FontRole.CaptionStrong, gold, TextAlign.Center, 1f, scale));
        if (frame.Input.ConsumeClick(area))
        {
            OpenEdit();
        }
    }

    public void DrawStudioBadges(in AppletFrame frame, Rect area)
    {
        var gold = frame.Theme.Palette.WarmAccent;
        var count = BadgeCatalog.SlotCount;
        var gap = frame.Units(6f);
        var size = MathF.Floor(MathF.Min(area.Height, (area.Width - gap * (count - 1)) / count));
        if (size < 1f)
        {
            return;
        }

        var originX = MathF.Round(area.Min.X);
        var originY = MathF.Round(area.Min.Y + (area.Height - size) * 0.5f);
        for (var index = 0; index < count; index++)
        {
            var cell = Rect.FromSize(new Vector2(originX + index * (size + gap), originY),
                new Vector2(size, size));
            DrawMark(frame, cell, book.Slots[index], gold, true);
            if (frame.Input.ConsumeClick(cell))
            {
                OpenBadgePick(index);
            }
        }
    }

    private void DrawIdentity(in AppletFrame frame, Rect area, string name, string job, string world,
        string place, uint jobIconId, Vector4 gold)
    {
        if (area.Width < 8f || area.Height < 8f)
        {
            return;
        }

        var ink = frame.Theme.Palette.Ink;
        var patron = GlassName.IsPatron(book, pearl.Current, display, development);
        titleClock += frame.DeltaSeconds;
        var title = patron ? ShownName.ClampTitle(display.OwnTitle).Trim() : string.Empty;
        var nameH = frame.Text.LineHeight(FontRole.Display);
        var titleH = title.Length > 0 ? frame.Text.LineHeight(FontRole.CaptionStrong) * TitleScale : 0f;
        var titleGap = titleH > 0f ? frame.Units(2f) : 0f;
        var rowH = MathF.Max(frame.Units(16f), frame.Text.LineHeight(FontRole.CaptionStrong) + frame.Units(4f));
        var metaH = rowH * 3f;
        var metaTop = area.Max.Y - metaH;
        if (titleH > 0f)
        {
            DrawHonorTitle(frame, Rect.FromSize(area.Min, new Vector2(area.Width, titleH)), title);
        }

        var nameRow = Rect.FromSize(new Vector2(area.Min.X, area.Min.Y + titleH + titleGap),
            new Vector2(area.Width, nameH));
        if (patron && (display.NameGlow || display.NameInkCustom))
        {
            DrawHonorMark(frame, nameRow, name, MarkLook.ForName(display, ink), FontRole.Display);
        }
        else
        {
            frame.Text.DrawFitted(nameRow, name,
                GlassName.Title(frame.Theme.Palette, display, patron, FontRole.Display, TextAlign.Left,
                    frame.Units(1f)));
        }

        if (metaH > area.Height - 4f)
        {
            return;
        }

        var icon = MathF.Min(frame.Units(15f), rowH * 0.58f);
        var y = metaTop;

        var jobRow = Rect.FromSize(new Vector2(area.Min.X, y), new Vector2(area.Width, rowH));
        DrawMetaRow(frame, jobRow, icon, ink, job, false);
        var jobMark = MetaMark(jobRow, icon);
        if (!TryDrawGameIcon(frame, jobMark, jobIconId))
        {
            DrawDiamond(frame.Paint, jobMark, gold);
        }

        y += rowH;
        var worldRow = Rect.FromSize(new Vector2(area.Min.X, y), new Vector2(area.Width, rowH));
        DrawMetaRow(frame, worldRow, icon, ink, world, true);
        DrawHouse(frame, MetaMark(worldRow, icon), gold);
        y += rowH;
        var placeRow = Rect.FromSize(new Vector2(area.Min.X, y), new Vector2(area.Width, rowH));
        DrawMetaRow(frame, placeRow, icon, ink, place.Length > 0 ? place : "—", true);
        DrawPin(frame, MetaMark(placeRow, icon), gold);
    }

    private static Rect MetaMark(Rect row, float icon) =>
        Rect.FromSize(new Vector2(row.Min.X, row.Center.Y - icon * 0.5f), new Vector2(icon, icon));

    private static void DrawMetaRow(in AppletFrame frame, Rect row, float icon, Vector4 ink,
        string label, bool hairline)
    {
        if (hairline)
        {
            var lineY = row.Min.Y;
            frame.Paint.Line(new Vector2(row.Min.X, lineY), new Vector2(row.Max.X, lineY),
                ink with { W = 0.16f }, MathF.Max(1f, frame.Units(0.9f)));
        }

        var mark = MetaMark(row, icon);
        var barX = mark.Max.X + frame.Units(6f);
        var barH = icon * 0.92f;
        frame.Paint.Line(new Vector2(barX, row.Center.Y - barH * 0.5f),
            new Vector2(barX, row.Center.Y + barH * 0.5f), ink with { W = 0.38f },
            MathF.Max(1f, frame.Units(1f)));
        var text = new Rect(new Vector2(barX + frame.Units(8f), row.Min.Y), row.Max);
        frame.Text.DrawEllipsized(text, label,
            new TextStyle(FontRole.CaptionStrong, ink));
    }

    public float DrawOverlay(in AppletFrame frame)
    {
        if (sheet == Sheet.None)
        {
            return 0f;
        }

        if (sheet == Sheet.PlacePortrait)
        {
            return DrawPlacePortrait(frame);
        }

        if (sheet == Sheet.PickBadge)
        {
            return DrawBadgePick(frame);
        }

        return DrawEditSheet(frame);
    }

    private float DrawPlacePortrait(in AppletFrame frame)
    {
        var gold = frame.Theme.Palette.WarmAccent;
        frame.Paint.Fill(frame.Content, frame.Theme.Palette.SurfaceSunken with { W = 0.62f });
        var inner = frame.Content.Inset(frame.Units(16f));
        frame.Text.DrawIn(inner.TopSlice(frame.Units(24f)), "Place photo",
            new TextStyle(FontRole.Title, frame.Theme.Palette.Ink));
        frame.Text.DrawWrapped(inner.Inset(new Edges(0f, frame.Units(28f), 0f, 0f)).TopSlice(frame.Units(36f)),
            "Drag to move. Scroll to zoom. The circle is what shows on your profile.",
            new TextStyle(FontRole.Caption, frame.Theme.Palette.InkMuted));

        var previewSize = MathF.Min(inner.Width - frame.Units(24f), inner.Height - frame.Units(120f));
        previewSize = MathF.Max(previewSize, frame.Units(120f));
        var preview = Rect.FromSize(
            new Vector2(inner.Center.X - previewSize * 0.5f, inner.Min.Y + frame.Units(72f)),
            new Vector2(previewSize, previewSize));
        DrawPortrait(frame, preview);
        HandlePortraitGesture(frame, preview);

        var actions = inner.BottomSlice(frame.Units(40f));
        var other = actions.LeftSlice(actions.Width * 0.48f);
        var done = actions.RightSlice(actions.Width * 0.48f);
        frame.Paint.Stroke(other, gold with { W = 0.55f }, frame.Theme.Metrics.Hairline, frame.Units(12f));
        frame.Text.DrawIn(other, "Choose another",
            new TextStyle(FontRole.CaptionStrong, gold, TextAlign.Center));
        frame.Paint.Fill(done, gold with { W = 0.92f }, frame.Units(12f));
        frame.Text.DrawIn(done, "Done",
            new TextStyle(FontRole.CaptionStrong, frame.Theme.Palette.AccentInk, TextAlign.Center));
        if (frame.Input.ConsumeClick(other))
        {
            PickPortrait();
        }

        if (frame.Input.ConsumeClick(done))
        {
            DismissPicker();
        }

        return frame.Content.Height;
    }

    private float DrawEditSheet(in AppletFrame frame)
    {
        var snapshot = pearl.Current;
        var inset = frame.Units(14f);
        var content = frame.Content.Inset(inset);
        var stack = new Stack(content, StackAxis.Vertical, frame.Units(10f));
        DrawCard(frame, stack.Take(frame.Units(SheetHeightUnits)), CardName(snapshot), GlassJob(), GlassWorld(snapshot),
            game.MapPlace, game.JobIconId);
        DrawNameControls(frame, ref stack, snapshot);
        DrawGateRows(frame, ref stack, snapshot);
        return (content.Height - stack.Remaining.Height) + inset * 2f;
    }

    private string CardName(PearlSnapshot snapshot)
    {
        var linked = ShownName.Linked(game.Character.Name, snapshot.MeName);
        var name = GlassName.Resolve(display, linked, GlassName.IsPatron(book, snapshot, display, development));
        return name.Length > 0 ? name : "Not logged in";
    }

    private void DrawNameControls(in AppletFrame frame, ref Stack stack, PearlSnapshot snapshot)
    {
        var patron = GlassName.IsPatron(book, snapshot, display, development);
        if (development)
        {
            DrawChoice(frame, stack.Take(frame.Units(40f)), "Testing account", display.TestingAccount,
                () => display.TestingAccount = !display.TestingAccount);
            DrawNotice(frame, stack.Take(frame.Units(40f)),
                display.TestingAccount
                    ? "Testing account: Patron rows are unlocked on this handset."
                    : "Normal account: Patron rows follow your real Pearlgate pledge.");
        }

        DrawChoice(frame, stack.Take(frame.Units(40f)), "Full name", display.NameStyle == NameStyle.Full,
            () => display.NameStyle = NameStyle.Full);
        DrawChoice(frame, stack.Take(frame.Units(40f)), "First name", display.NameStyle == NameStyle.Given,
            () => display.NameStyle = NameStyle.Given);

        if (!patron)
        {
            DrawNotice(frame, stack.Take(frame.Units(56f)),
                "Custom name, title, and title glow are for Patreon subscribers on this Pearlgate account.");
            if (snapshot.SignedIn)
            {
                DrawAction(frame, stack.Take(frame.Units(44f)), "Connect Patreon", pearl.BeginPatronLink);
                if (snapshot.PatronLinkUrl.Length > 0)
                {
                    DrawAction(frame, stack.Take(frame.Units(40f)), "Open Patreon page", pearl.OpenPatronLink);
                }
            }

            return;
        }

        var nameRow = stack.Take(frame.Units(52f));
        CardChrome.DrawGold(frame, nameRow);
        var namePad = nameRow.Inset(new Edges(frame.Units(14f), frame.Units(6f)));
        frame.Text.DrawIn(namePad.TopSlice(frame.Units(14f)), "Add your own name",
            new TextStyle(FontRole.Caption, frame.Theme.Palette.InkMuted));
        display.OwnName = frame.TextField.Draw("profile-own-name", namePad.Inset(new Edges(0f, frame.Units(16f), 0f, 0f)),
            display.OwnName, "Paste any letters or symbols", ShownName.OwnNameLimit, out _);

        var titleRow = stack.Take(frame.Units(52f));
        CardChrome.DrawGold(frame, titleRow);
        var titlePad = titleRow.Inset(new Edges(frame.Units(14f), frame.Units(6f)));
        frame.Text.DrawIn(titlePad.TopSlice(frame.Units(14f)), "Title",
            new TextStyle(FontRole.Caption, frame.Theme.Palette.InkMuted));
        display.OwnTitle = frame.TextField.Draw("profile-own-title",
            titlePad.Inset(new Edges(0f, frame.Units(16f), 0f, 0f)),
            display.OwnTitle, "Shown above your name", ShownName.OwnTitleLimit, out _);

        DrawColorDoor(frame, ref stack, "Name text", display.NameInkR, display.NameInkG, display.NameInkB,
            ColorWell.NameInk, display.SetNameInkColor);
        DrawColorDoor(frame, ref stack, "Title text", display.TitleInkR, display.TitleInkG, display.TitleInkB,
            ColorWell.TitleInk, display.SetTitleInkColor);

        nameGlowOpen = DrawDrop(frame, ref stack, "Name glow", nameGlowOpen);
        if (nameGlowOpen)
        {
            DrawGlowChoices(frame, ref stack, display.NameGlow, display.NameGlowWeight,
                on => display.NameGlow = on, weight => display.NameGlowWeight = weight);
            DrawMotion(frame, ref stack, display.NameMotion, motion => display.NameMotion = motion);
            if (display.NameGlow)
            {
                DrawColorDoor(frame, ref stack, "Name glow color", display.NameGlowR, display.NameGlowG,
                    display.NameGlowB, ColorWell.NameGlow, display.SetNameGlowColor);
            }
        }

        titleGlowOpen = DrawDrop(frame, ref stack, "Title glow", titleGlowOpen);
        if (titleGlowOpen)
        {
            DrawGlowChoices(frame, ref stack, display.TitleGlow, display.TitleGlowWeight,
                on => display.TitleGlow = on, weight => display.TitleGlowWeight = weight);
            DrawMotion(frame, ref stack, display.TitleMotion, motion => display.TitleMotion = motion);
            if (display.TitleGlow)
            {
                DrawColorDoor(frame, ref stack, "Title glow color", display.TitleGlowR, display.TitleGlowG,
                    display.TitleGlowB, ColorWell.TitleGlow, display.SetTitleGlowColor);
            }
        }
    }

    private void DrawMotion(in AppletFrame frame, ref Stack stack, TitleMotion current, Action<TitleMotion> set)
    {
        DrawChoice(frame, stack.Take(frame.Units(40f)), "Static", current == TitleMotion.Static,
            () => set(TitleMotion.Static));
        DrawChoice(frame, stack.Take(frame.Units(40f)), "Pulse", current == TitleMotion.Pulse,
            () => set(TitleMotion.Pulse));
        DrawChoice(frame, stack.Take(frame.Units(40f)), "Wave", current == TitleMotion.Wave,
            () => set(TitleMotion.Wave));
    }

    private void DrawGlowChoices(in AppletFrame frame, ref Stack stack, bool on, NameGlowWeight current,
        Action<bool> setOn, Action<NameGlowWeight> setWeight)
    {
        DrawChoice(frame, stack.Take(frame.Units(36f)), "Off", !on, () => setOn(false));
        DrawChoice(frame, stack.Take(frame.Units(36f)), "Soft glow", on && current == NameGlowWeight.Soft, () =>
        {
            setOn(true);
            setWeight(NameGlowWeight.Soft);
        });
        DrawChoice(frame, stack.Take(frame.Units(36f)), "Medium glow", on && current == NameGlowWeight.Medium, () =>
        {
            setOn(true);
            setWeight(NameGlowWeight.Medium);
        });
        DrawChoice(frame, stack.Take(frame.Units(36f)), "Strong glow", on && current == NameGlowWeight.Strong, () =>
        {
            setOn(true);
            setWeight(NameGlowWeight.Strong);
        });
    }

    private static bool DrawDrop(in AppletFrame frame, ref Stack stack, string label, bool open)
    {
        var row = stack.Take(frame.Units(40f));
        CardChrome.DrawGold(frame, row);
        var pad = row.Inset(new Edges(frame.Units(14f), 0f, frame.Units(12f), 0f));
        frame.Text.DrawIn(pad.LeftSlice(pad.Width - frame.Units(28f)), label,
            new TextStyle(FontRole.BodyStrong, frame.Theme.Palette.Ink));
        DrawCaret(frame.Paint, pad.RightSlice(frame.Units(16f)), frame.Theme.Palette.WarmAccent, open);
        if (frame.Input.ConsumeClick(row))
        {
            return !open;
        }

        return open;
    }

    private static void DrawCaret(IPaintSurface paint, Rect area, Vector4 gold, bool open)
    {
        var c = area.Center;
        var s = MathF.Min(area.Width, area.Height) * 0.28f;
        var dir = open ? -1f : 1f;
        var stroke = MathF.Max(1.2f, s * 0.42f);
        paint.Line(c + new Vector2(-s, -s * 0.35f * dir), c + new Vector2(0f, s * 0.45f * dir), gold, stroke);
        paint.Line(c + new Vector2(s, -s * 0.35f * dir), c + new Vector2(0f, s * 0.45f * dir), gold, stroke);
    }

    private void DrawColorDoor(in AppletFrame frame, ref Stack stack, string label, float r, float g, float b,
        ColorWell well, Action<Vector4> set)
    {
        var row = stack.Take(frame.Units(40f));
        CardChrome.DrawGold(frame, row);
        var pad = row.Inset(new Edges(frame.Units(14f), frame.Units(8f)));
        var chip = pad.RightSlice(frame.Units(28f));
        frame.Paint.Fill(chip, new Vector4(r, g, b, 1f), frame.Units(6f));
        frame.Paint.Stroke(chip, frame.Theme.Palette.WarmAccent, frame.Units(1.1f), frame.Units(6f));
        frame.Text.DrawIn(new Rect(pad.Min, new Vector2(chip.Min.X - frame.Units(8f), pad.Max.Y)), label,
            new TextStyle(FontRole.BodyStrong, frame.Theme.Palette.Ink));
        if (frame.Input.ConsumeClick(row))
        {
            colorWell = colorWell == well ? ColorWell.None : well;
        }

        if (colorWell == well)
        {
            inkPicker.Draw(frame, stack.Take(inkPicker.Height(frame)), r, g, b, set);
        }
    }

    private static void DrawAction(in AppletFrame frame, Rect row, string label, Action tap)
    {
        CardChrome.DrawGold(frame, row);
        var gold = frame.Theme.Palette.WarmAccent;
        frame.Paint.Fill(row.Inset(new Edges(frame.Units(14f), frame.Units(8f))), gold with { W = 0.92f },
            frame.Units(9f));
        frame.Text.DrawIn(row, label,
            new TextStyle(FontRole.CaptionStrong, frame.Theme.Palette.AccentInk, TextAlign.Center));
        if (frame.Input.ConsumeClick(row))
        {
            tap();
        }
    }

    private void DrawHonorTitle(in AppletFrame frame, Rect area, string title) =>
        DrawHonorMark(frame, area, title, MarkLook.ForTitle(display), FontRole.CaptionStrong, TitleScale);

    private void DrawHonorMark(in AppletFrame frame, Rect area, string text, in MarkLook look, FontRole role,
        float markScale = 1f)
    {
        var unit = frame.Units(1f);
        var width = 0f;
        var count = 0;
        foreach (var rune in text.EnumerateRunes())
        {
            width += frame.Text.Measure(rune.ToString(), role).X * markScale;
            count++;
        }

        if (width < 1f || count == 0)
        {
            return;
        }

        var scale = markScale * MathF.Min(1f, area.Width / width);
        var x = area.Min.X;
        var index = 0;
        foreach (var rune in text.EnumerateRunes())
        {
            var glyph = rune.ToString();
            var size = frame.Text.Measure(glyph, role) * scale;
            var cell = Rect.FromSize(new Vector2(x, area.Min.Y), new Vector2(size.X, area.Height));
            var fill = TitleFx.Fill(look, titleClock, index, count) with { W = 1f };
            var glow = TitleFx.Glow(look, titleClock, index, count);
            frame.Text.DrawFitted(cell, glyph,
                new TextStyle(role, fill, TextAlign.Left, 1f, markScale, glow, TitleFx.Spread(look, unit)));
            x += size.X;
            index++;
        }
    }

    private static void DrawChoice(in AppletFrame frame, Rect row, string label, bool on, Action tap)
    {
        CardChrome.DrawGold(frame, row);
        var pad = row.Inset(new Edges(frame.Units(14f), 0f));
        frame.Text.DrawIn(pad.LeftSlice(pad.Width - frame.Units(72f)), label,
            new TextStyle(FontRole.BodyStrong, frame.Theme.Palette.Ink));
        var mark = pad.RightSlice(frame.Units(22f));
        var gold = frame.Theme.Palette.WarmAccent;
        frame.Paint.StrokeCircle(mark.Center, frame.Units(7f), gold, frame.Units(1.2f));
        if (on)
        {
            frame.Paint.FillCircle(mark.Center, frame.Units(4f), gold);
        }

        if (frame.Input.ConsumeClick(row))
        {
            tap();
        }
    }

    private string GlassJob() =>
        game.JobName.Length > 0 ? TitleCase(game.JobName) : "Warrior of Light";

    private string GlassWorld(PearlSnapshot snapshot)
    {
        var world = snapshot.MeWorld.Length > 0 ? snapshot.MeWorld : game.Character.WorldName;
        return world.Length > 0 ? world : "Eorzea";
    }

    private void DrawGateRows(in AppletFrame frame, ref Stack stack, PearlSnapshot snapshot)
    {
        var inGame = ShownName.Linked(game.Character.Name, snapshot.MeName);
        DrawStat(frame, stack.Take(frame.Units(40f)), "In game", inGame.Length > 0 ? inGame : "—");
        DrawAccount(frame, stack.Take(frame.Units(44f)), snapshot);
        if (snapshot.Busy)
        {
            DrawNotice(frame, stack.Take(frame.Units(36f)), "Working…");
        }

        if (snapshot.Notice.Length > 0)
        {
            DrawNotice(frame, stack.Take(frame.Units(56f)), snapshot.Notice);
        }

        if (snapshot.ChallengeCode.Length > 0)
        {
            DrawChallenge(frame, stack.Take(frame.Units(56f)), snapshot.ChallengeCode);
        }

        DrawStat(frame, stack.Take(frame.Units(40f)), "Followers",
            snapshot.Followers.ToString("N0", CultureInfo.InvariantCulture));
        DrawStat(frame, stack.Take(frame.Units(40f)), "Following",
            snapshot.Following.ToString("N0", CultureInfo.InvariantCulture));
        DrawStat(frame, stack.Take(frame.Units(40f)), "Number",
            snapshot.MyNumber.Length > 0 ? snapshot.MyNumber : "—");
    }

    private void DrawAccount(in AppletFrame frame, Rect row, PearlSnapshot snapshot)
    {
        CardChrome.DrawGold(frame, row);
        var pad = row.Inset(new Edges(frame.Units(14f), 0f, frame.Units(10f), 0f));
        frame.Text.DrawIn(pad.LeftSlice(frame.Units(90f)), "Pearlgate",
            new TextStyle(FontRole.BodyStrong, frame.Theme.Palette.Ink));

        var action = pad.RightSlice(frame.Units(86f)).Inset(new Edges(0f, frame.Units(8f)));
        var gold = frame.Theme.Palette.WarmAccent;
        if (snapshot.Busy)
        {
            frame.Paint.Fill(action, frame.Theme.Palette.SurfaceRaised, frame.Units(9f));
            frame.Text.DrawIn(action, "Working…",
                new TextStyle(FontRole.CaptionStrong, frame.Theme.Palette.InkMuted, TextAlign.Center));
            return;
        }

        frame.Paint.Fill(action, gold with { W = 0.92f }, frame.Units(9f));
        frame.Text.DrawIn(action, snapshot.SignedIn ? "Sign out" : "Sign in",
            new TextStyle(FontRole.CaptionStrong, frame.Theme.Palette.AccentInk, TextAlign.Center));
        if (frame.Input.ConsumeClick(action))
        {
            if (snapshot.SignedIn)
            {
                pearl.SignOut();
            }
            else
            {
                pearl.BeginSignIn();
            }
        }
    }

    private static void DrawStat(in AppletFrame frame, Rect row, string label, string value)
    {
        CardChrome.DrawGold(frame, row);
        var pad = row.Inset(new Edges(frame.Units(14f), 0f));
        frame.Text.DrawIn(pad.LeftSlice(frame.Units(90f)), label,
            new TextStyle(FontRole.BodyStrong, frame.Theme.Palette.Ink));
        frame.Text.DrawEllipsized(pad.RightSlice(pad.Width - frame.Units(90f)), value,
            new TextStyle(FontRole.Body, frame.Theme.Palette.Ink, TextAlign.Right));
    }

    private static void DrawNotice(in AppletFrame frame, Rect row, string text)
    {
        CardChrome.DrawGold(frame, row);
        frame.Text.DrawWrapped(row.Inset(new Edges(frame.Units(14f), frame.Units(8f))), text,
            new TextStyle(FontRole.Caption, frame.Theme.Palette.InkMuted));
    }

    private static void DrawChallenge(in AppletFrame frame, Rect row, string code)
    {
        CardChrome.DrawGold(frame, row);
        var pad = row.Inset(new Edges(frame.Units(14f), frame.Units(8f)));
        var stack = new Stack(pad, StackAxis.Vertical, frame.Units(2f));
        frame.Text.DrawIn(stack.Take(frame.Units(16f)), "Enter this code on Pearlgate",
            new TextStyle(FontRole.Caption, frame.Theme.Palette.InkMuted));
        frame.Text.DrawIn(stack.Take(frame.Units(22f)), code,
            new TextStyle(FontRole.Title, frame.Theme.Palette.Ink, TextAlign.Center));
    }

    private static string TitleCase(string value)
    {
        if (value.Length == 0)
        {
            return value;
        }

        return char.ToUpper(value[0], CultureInfo.InvariantCulture) + value[1..];
    }

    private void DrawSlots(in AppletFrame frame, Rect tray, Vector4 gold)
    {
        var count = BadgeCatalog.SlotCount;
        var gap = frame.Units(6f);
        var size = MathF.Floor(MathF.Min(tray.Height,
            (tray.Width - gap * (count - 1)) / count));
        if (size < 1f)
        {
            return;
        }

        var used = size * count + gap * (count - 1);
        var originX = MathF.Round(tray.Min.X + (tray.Width - used) * 0.5f);
        var originY = MathF.Round(tray.Center.Y - size * 0.5f);
        for (var index = 0; index < count; index++)
        {
            var cell = Rect.FromSize(new Vector2(originX + index * (size + gap), originY),
                new Vector2(size, size));
            DrawMark(frame, cell, book.Slots[index], gold);
            if (frame.Input.ConsumeClick(cell))
            {
                OpenBadgePick(index);
            }
        }
    }

    private void OpenBadgePick(int slot)
    {
        pickSlot = slot;
        returnToEdit = sheet == Sheet.Edit || returnToEdit;
        sheet = Sheet.PickBadge;
    }

    private void DismissPicker()
    {
        if (sheet == Sheet.PlacePortrait)
        {
            book.CommitPortrait();
        }

        draggingPhoto = false;
        sheet = returnToEdit ? Sheet.Edit : Sheet.None;
    }

    private float DrawBadgePick(in AppletFrame frame)
    {
        var gold = frame.Theme.Palette.WarmAccent;
        frame.Paint.Fill(frame.Content, frame.Theme.Palette.SurfaceSunken with { W = 0.55f });
        var panel = frame.Content.Inset(frame.Units(10f));
        CardChrome.DrawGold(frame, panel);
        var inner = panel.Inset(frame.Units(12f));
        frame.Text.DrawIn(inner.TopSlice(frame.Units(22f)), pickSlot < 0 ? "Featured badge" : "Profile badge",
            new TextStyle(FontRole.Title, frame.Theme.Palette.Ink));
        var close = inner.TopSlice(frame.Units(22f)).RightSlice(frame.Units(28f));
        frame.Text.DrawIn(close, "✕",
            new TextStyle(FontRole.BodyStrong, frame.Theme.Palette.InkMuted, TextAlign.Center));
        if (frame.Input.ConsumeClick(close))
        {
            DismissPicker();
            return panel.Height;
        }

        var grid = inner.Inset(new Edges(0f, frame.Units(28f), 0f, frame.Units(40f)));
        var cols = 4;
        var gap = frame.Units(8f);
        var side = (grid.Width - gap * (cols - 1)) / cols;
        var index = 0;
        for (var own = 0; own < book.Owned.Count; own++)
        {
            var spec = BadgeCatalog.Find(book.Owned[own].Id);
            if (spec is null)
            {
                continue;
            }

            var col = index % cols;
            var row = index / cols;
            var cell = Rect.FromSize(new Vector2(grid.Min.X + col * (side + gap), grid.Min.Y + row * (side + gap)),
                new Vector2(side, side));
            if (cell.Min.Y > grid.Max.Y)
            {
                break;
            }

            DrawMark(frame, cell, spec.Value.Id, gold);
            if (frame.Input.ConsumeClick(cell))
            {
                if (pickSlot < 0)
                {
                    book.SetFeatured(spec.Value.Id);
                }
                else
                {
                    book.Equip(pickSlot, spec.Value.Id);
                }

                DismissPicker();
                return panel.Height;
            }

            index++;
        }

        var clear = inner.BottomSlice(frame.Units(32f));
        frame.Text.DrawIn(clear, "Clear this slot",
            new TextStyle(FontRole.Caption, frame.Theme.Palette.InkMuted, TextAlign.Center));
        if (frame.Input.ConsumeClick(clear))
        {
            if (pickSlot < 0)
            {
                book.SetFeatured(string.Empty);
            }
            else
            {
                book.ClearSlot(pickSlot);
            }

            DismissPicker();
        }

        return panel.Height;
    }

    private void DrawMark(in AppletFrame frame, Rect cell, string badgeId, Vector4 gold, bool fill = false)
    {
        var box = CoverFit.Snapped(CoverFit.InscribedSquare(cell));
        var radius = MathF.Min(frame.Units(8f), box.Width * 0.28f);
        if (badgeId.Length > 0 && BadgeCatalog.Find(badgeId) is { } spec)
        {
            if (spec.IconAsset.Length > 0 &&
                (fill
                    ? BadgeArt.TryDrawFill(frame.Paint, textures, paths, box, spec.IconAsset)
                    : BadgeArt.TryDraw(frame.Paint, textures, paths, box, spec.IconAsset)))
            {
                return;
            }

            frame.Paint.Stroke(box, gold with { W = 0.42f }, MathF.Max(1.1f, frame.Units(1.15f)), radius);
            BadgeMarks.Draw(frame.Paint, box, spec.Mark, gold);
            return;
        }

        frame.Paint.Stroke(box, gold with { W = 0.42f }, MathF.Max(1.1f, frame.Units(1.15f)), radius);
        frame.Text.DrawIn(box, "+", new TextStyle(FontRole.BodyStrong, gold, TextAlign.Center));
    }

    private void DrawPortrait(in AppletFrame frame, Rect area)
    {
        var gold = frame.Theme.Palette.WarmAccent;
        var radius = MathF.Min(area.Width, area.Height) * 0.48f;
        var center = area.Center;
        var side = radius * 2f;
        var square = Rect.FromSize(center - new Vector2(side * 0.5f, side * 0.5f), new Vector2(side, side));
        if (book.PortraitFile.Length > 0)
        {
            var texture = textures.FromFile(PortraitFiles.Absolute(paths, book.PortraitFile));
            if (texture is { IsReady: true })
            {
                var crop = CoverFit.Framed(texture.Size, square.Size, book.PortraitZoom, book.PortraitFocus);
                frame.Paint.ImageRounded(texture, square, crop.Min, crop.Max, Vector4.One,
                    MathF.Min(square.Width, square.Height) * 0.5f);
            }
            else
            {
                frame.Paint.FillCircle(center, radius, frame.Theme.Palette.SurfaceRaised with { W = 0.35f });
            }
        }
        else
        {
            frame.Text.DrawIn(square, "+", new TextStyle(FontRole.Display, gold, TextAlign.Center));
        }

        frame.Paint.StrokeCircle(center, radius, gold with { W = 0.72f }, MathF.Max(1.2f, frame.Units(1.4f)));
    }

    private void HandlePortraitGesture(in AppletFrame frame, Rect preview)
    {
        if (book.PortraitFile.Length == 0)
        {
            return;
        }

        var texture = textures.FromFile(PortraitFiles.Absolute(paths, book.PortraitFile));
        if (texture is not { IsReady: true })
        {
            return;
        }

        if (frame.Input.WasPressed(preview))
        {
            draggingPhoto = true;
        }

        if (draggingPhoto && frame.Input.IsHeld())
        {
            var cover = CoverFit.Framed(texture.Size, preview.Size, book.PortraitZoom, book.PortraitFocus);
            var visible = cover.Max - cover.Min;
            var next = book.PortraitFocus - new Vector2(
                preview.Width > 1f ? frame.Input.PointerDelta.X / preview.Width * visible.X : 0f,
                preview.Height > 1f ? frame.Input.PointerDelta.Y / preview.Height * visible.Y : 0f);
            book.AdjustPortrait(book.PortraitZoom, next);
        }

        if (!frame.Input.IsHeld())
        {
            draggingPhoto = false;
        }

        if (frame.Input.IsHovering(preview) && MathF.Abs(frame.Input.ScrollDelta) > 0.01f)
        {
            var zoom = book.PortraitZoom * (1f + frame.Input.ScrollDelta * 0.14f);
            book.AdjustPortrait(zoom, book.PortraitFocus);
        }
    }

    private void OpenPortrait()
    {
        returnToEdit = sheet == Sheet.Edit || returnToEdit;
        if (book.PortraitFile.Length == 0)
        {
            PickPortrait();
            return;
        }

        sheet = Sheet.PlacePortrait;
    }

    private void PickPortrait()
    {
        var picked = files.PickImageFiles();
        if (picked.Count == 0)
        {
            return;
        }

        if (PortraitFiles.TryImport(paths, picked[0], out var name))
        {
            book.SetPortrait(name);
            sheet = Sheet.PlacePortrait;
        }
    }

    private static void DrawEdit(in AppletFrame frame, Rect area, Vector4 gold)
    {
        CardChrome.DrawGold(frame, area);
        var inset = area.Inset(new Edges(frame.Units(8f), frame.Units(6f), frame.Units(8f), frame.Units(6f)));
        var icon = inset.LeftSlice(frame.Units(16f));
        DrawPencil(frame, icon, gold);
        var chevron = inset.RightSlice(frame.Units(10f));
        var stroke = MathF.Max(1.2f, frame.Units(1.4f));
        var c = chevron.Center;
        var s = MathF.Min(chevron.Width, chevron.Height) * 0.32f;
        frame.Paint.Line(c + new Vector2(-s * 0.25f, -s * 0.55f), c + new Vector2(s * 0.35f, 0f), gold with { W = 0.65f },
            stroke);
        frame.Paint.Line(c + new Vector2(-s * 0.25f, s * 0.55f), c + new Vector2(s * 0.35f, 0f), gold with { W = 0.65f },
            stroke);
        frame.Text.DrawEllipsized(inset.Inset(new Edges(frame.Units(20f), 0f, frame.Units(12f), 0f)), "EDIT PROFILE",
            new TextStyle(FontRole.CaptionStrong, gold));
    }

    private static void DrawPencil(in AppletFrame frame, Rect area, Vector4 ink)
    {
        if (HomeMarks.TryDrawAsset(frame.Paint, frame.Textures, frame.Paths, area, AppIconCatalog.HomeEditAsset, ink))
        {
            return;
        }

        var paint = frame.Paint;
        var c = area.Center;
        var s = MathF.Min(area.Width, area.Height) * 0.42f;
        var shaft = MathF.Max(1.3f, s * 0.28f);
        paint.Line(c + new Vector2(-s * 0.72f, s * 0.42f), c + new Vector2(s * 0.38f, -s * 0.68f), ink, shaft);
        paint.Line(c + new Vector2(s * 0.18f, -s * 0.78f), c + new Vector2(s * 0.58f, -s * 0.38f), ink, shaft);
        paint.Line(c + new Vector2(-s * 0.86f, s * 0.22f), c + new Vector2(-s * 0.42f, s * 0.66f), ink, shaft);
    }

    private static bool TryDrawGameIcon(in AppletFrame frame, Rect area, uint iconId)
    {
        if (iconId == 0)
        {
            return false;
        }

        var texture = frame.Textures.GameIcon(iconId);
        if (texture is not { IsReady: true })
        {
            return false;
        }

        frame.Paint.Image(texture, CoverFit.Contained(texture.Size, area), Vector4.One);
        return true;
    }

    private static void DrawDiamond(IPaintSurface paint, Rect area, Vector4 gold)
    {
        var c = area.Center;
        var s = MathF.Min(area.Width, area.Height) * 0.38f;
        Span<Vector2> diamond = stackalloc Vector2[4]
        {
            c + new Vector2(0f, -s),
            c + new Vector2(s, 0f),
            c + new Vector2(0f, s),
            c + new Vector2(-s, 0f),
        };
        paint.Polyline(diamond, gold, MathF.Max(1.2f, s * 0.28f), true);
    }

    private static void DrawPin(in AppletFrame frame, Rect area, Vector4 ink)
    {
        if (HomeMarks.TryDrawAsset(frame.Paint, frame.Textures, frame.Paths, area, AppIconCatalog.HomePinAsset, ink))
        {
            return;
        }

        HomeMarks.Draw(frame.Paint, area, HomeMark.Pin, ink);
    }

    private static void DrawHouse(in AppletFrame frame, Rect area, Vector4 ink)
    {
        if (HomeMarks.TryDrawAsset(frame.Paint, frame.Textures, frame.Paths, area, AppIconCatalog.HomePlaceAsset, ink))
        {
            return;
        }

        var paint = frame.Paint;
        var c = area.Center;
        var s = MathF.Min(area.Width, area.Height) * 0.40f;
        var stroke = MathF.Max(1.2f, s * 0.20f);
        Span<Vector2> roof = stackalloc Vector2[3]
        {
            c + new Vector2(0f, -s),
            c + new Vector2(s * 0.92f, -s * 0.08f),
            c + new Vector2(-s * 0.92f, -s * 0.08f),
        };
        paint.Polyline(roof, ink, stroke, true);
        var body = Rect.FromSize(c + new Vector2(-s * 0.68f, -s * 0.04f), new Vector2(s * 1.36f, s * 0.92f));
        paint.Stroke(body, ink, stroke, s * 0.06f);
        var door = Rect.FromSize(c + new Vector2(-s * 0.16f, s * 0.28f), new Vector2(s * 0.32f, s * 0.60f));
        paint.Stroke(door, ink, stroke, s * 0.04f);
    }
}
