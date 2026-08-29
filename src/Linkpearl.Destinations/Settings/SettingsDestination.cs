using Linkpearl.Applets;
using Linkpearl.Cards;
using Linkpearl.Chassis;
using Linkpearl.Geometry;
using Linkpearl.Layout;
using Linkpearl.Media;
using Linkpearl.Modules;
using Linkpearl.Net;
using Linkpearl.Painting;
using Linkpearl.Platform;
using Linkpearl.Preferences;

namespace Linkpearl.Destinations.Settings;

public sealed class SettingsDestination : IDestinationScreen, ISectionedDestination
{
    private enum Part : byte
    {
        None = 0,
        Glass = 1,
        Bezel = 2,
        Strip = 3,
        Lock = 4,
        Ink = 5,
    }

    private readonly HandsetShapePreference shape;
    private readonly DisplayPreferences display;
    private readonly HostEnvironment environment;
    private readonly IGameSession game;
    private readonly IPearlHub pearl;
    private readonly DestinationHub hub;
    private readonly HostPaths paths;
    private readonly ITextureSource textures;
    private Part part;
    private bool flipped;
    private string pictureError = string.Empty;
    private string bannerError = string.Empty;

    public SettingsDestination(HandsetShapePreference shape, DisplayPreferences display, HostEnvironment environment,
        IGameSession game, IPearlHub pearl, DestinationHub hub, HostPaths paths, ITextureSource textures)
    {
        this.shape = shape;
        this.display = display;
        this.environment = environment;
        this.game = game;
        this.pearl = pearl;
        this.hub = hub;
        this.paths = paths;
        this.textures = textures;
    }

    public DestinationTab Tab => DestinationTab.Settings;

    public string Glyph => "⚙";

    public string Label => "Settings";

    public int CurrentSection => display.UsingList ? 0 : (flipped ? SettingsPane.Presence : SettingsPane.Front);

    public void ShowSection(int section)
    {
        if (display.UsingList)
        {
            return;
        }

        flipped = section == SettingsPane.Presence;
        part = Part.None;
    }

    public float Compose(in AppletFrame frame)
    {
        var inset = frame.Units(14f);
        var content = frame.Content.Inset(inset);
        var stack = new Stack(content, StackAxis.Vertical, frame.Units(10f));
        DrawHead(frame, stack.Take(frame.Units(36f)));
        if (display.UsingList)
        {
            DrawList(frame, ref stack);
        }
        else
        {
            DrawTouch(frame, stack.TakeRemaining());
        }

        return (content.Height - stack.Remaining.Height) + inset * 2f;
    }

    private void DrawHead(AppletFrame frame, Rect row)
    {
        var you = row.LeftSlice(frame.Units(36f));
        DrawYouMark(frame, you);
        var modes = row.RightSlice(frame.Units(108f));
        PairRow(frame, modes, "Touch", !display.UsingList, () => display.Layout = TuneLayout.Touch,
            "List", display.UsingList, () => display.Layout = TuneLayout.List);
        frame.Text.DrawIn(row.Inset(new Edges(frame.Units(42f), 0f, frame.Units(116f), 0f)), "Tune",
            new TextStyle(FontRole.Title, frame.Theme.Palette.Ink));
        if (frame.Input.ConsumeClick(you))
        {
            hub.Open(DestinationTab.You);
        }
    }

    private void DrawYouMark(in AppletFrame frame, Rect area)
    {
        var name = pearl.Current.MeName.Length > 0 ? pearl.Current.MeName : game.Character.Name;
        var glyph = name.Length > 0 ? name[0].ToString() : "?";
        frame.Paint.FillCircle(area.Center, frame.Units(14f), frame.Theme.Palette.SurfaceRaised);
        frame.Paint.StrokeCircle(area.Center, frame.Units(14f), frame.Theme.Palette.WarmAccent, frame.Units(1.2f));
        frame.Text.DrawIn(area, glyph, new TextStyle(FontRole.BodyStrong, frame.Theme.Palette.Ink, TextAlign.Center));
    }

    private void DrawList(AppletFrame frame, ref Stack stack)
    {
        DrawPreviewCard(frame, stack.Take(frame.Units(132f)));
        DrawInkGroup(frame, ref stack);
        DrawPlateGroup(frame, ref stack);
        DrawBannerGroup(frame, ref stack);
        DrawBodyGroup(frame, ref stack);
        DrawStripGroup(frame, ref stack);
        DrawPinGroup(frame, ref stack);
        DrawPresenceGroup(frame, ref stack);
        frame.Text.DrawIn(stack.Take(frame.Units(16f)), environment.Version,
            new TextStyle(FontRole.Caption, frame.Theme.Palette.InkFaint, TextAlign.Center));
    }

    private void DrawPreviewCard(in AppletFrame frame, Rect row)
    {
        CardChrome.DrawGold(frame, row);
        var inner = row.Inset(frame.Units(10f));
        var preview = inner.LeftSlice(inner.Width * 0.36f);
        DrawChassis(frame, ChassisRect(preview, shape.Form, fill: true), Part.None);
        var copy = inner.Inset(new Edges(preview.Width + frame.Units(12f), frame.Units(6f), 0f, 0f));
        var lines = new Stack(copy, StackAxis.Vertical, frame.Units(4f));
        frame.Text.DrawIn(lines.Take(frame.Units(16f)), "Live look",
            new TextStyle(FontRole.CaptionStrong, frame.Theme.Palette.WarmAccent));
        frame.Text.DrawEllipsized(lines.Take(frame.Units(20f)), PlateLabel(),
            new TextStyle(FontRole.BodyStrong, frame.Theme.Palette.Ink));
        frame.Text.DrawEllipsized(lines.Take(frame.Units(16f)),
            ColorwayId.Label(display.Colorway) + " · " + CoreId.Label(display.Core),
            new TextStyle(FontRole.Caption, frame.Theme.Palette.InkMuted));
        var form = shape.Form == HandsetForm.Tablet ? "Tablet" : "Phone";
        var rim = shape.Finish == HandsetFinish.Etched ? "wide rim" : "slim rim";
        frame.Text.DrawEllipsized(lines.Take(frame.Units(16f)), form + " · " + rim,
            new TextStyle(FontRole.Caption, frame.Theme.Palette.InkMuted));
    }

    private void DrawTouch(AppletFrame frame, Rect body)
    {
        var flip = body.TopSlice(frame.Units(32f));
        PairRow(frame, flip.RightSlice(frame.Units(108f)), "Front", !flipped, () =>
        {
            flipped = false;
            part = Part.None;
        }, "Flip", flipped, () =>
        {
            flipped = true;
            part = Part.None;
        });
        frame.Text.DrawIn(flip.Inset(new Edges(0f, 0f, frame.Units(116f), 0f)),
            flipped ? "Presence" : "Touch a part",
            new TextStyle(FontRole.Caption, frame.Theme.Palette.InkMuted));

        var rest = body.Inset(new Edges(0f, frame.Units(40f), 0f, 0f));
        if (flipped)
        {
            DrawPresenceFace(frame, rest);
            return;
        }

        var previewHeight = MathF.Min(rest.Height * 0.56f, frame.Units(300f));
        var preview = rest.TopSlice(previewHeight);
        var chassis = ChassisRect(preview, shape.Form, fill: false);
        var glass = ChassisCatalog.For(shape.Form).ScreenOn(chassis);
        var strip = glass.TopSlice(glass.Height * 0.11f);
        var lockTab = new Rect(new Vector2(glass.Max.X - glass.Width * 0.18f, glass.Min.Y),
            new Vector2(glass.Max.X, strip.Max.Y));
        var wash = glass.BottomSlice(glass.Height * 0.12f);
        DrawChassis(frame, chassis, part);
        HitFront(frame, glass, chassis, strip, lockTab, wash);
        DrawTouchTray(frame, new Rect(new Vector2(rest.Min.X, preview.Max.Y + frame.Units(10f)), rest.Max));
    }

    private string PlateLabel() => display.UsingCustomPlate
        ? "Yours"
        : WallpaperCatalog.Resolve(display.WallpaperId).Label;

    private static Rect ChassisRect(Rect area, HandsetForm form, bool fill)
    {
        var aspect = ChassisCatalog.For(form).Aspect;
        var height = area.Height;
        var width = height * aspect;
        var maxWidth = fill ? area.Width : area.Width * 0.72f;
        if (width > maxWidth)
        {
            width = maxWidth;
            height = width / aspect;
        }

        var origin = new Vector2(area.Center.X - width * 0.5f, area.Center.Y - height * 0.5f);
        return Rect.FromSize(origin, new Vector2(width, height));
    }

    private void DrawChassis(in AppletFrame frame, Rect chassis, Part lit)
    {
        var gold = frame.Theme.Palette.WarmAccent;
        var plate = ChassisCatalog.For(shape.Form);
        var glass = plate.ScreenOn(chassis);
        var skin = textures.FromFile(paths.Asset(Path.Combine(ChassisCatalog.Folder, plate.FileName)));
        var strip = glass.TopSlice(glass.Height * 0.11f);
        var lockTab = new Rect(new Vector2(glass.Max.X - glass.Width * 0.18f, glass.Min.Y),
            new Vector2(glass.Max.X, strip.Max.Y));
        var wash = glass.BottomSlice(glass.Height * 0.12f);
        if (skin is not { IsReady: true })
        {
            var rim = shape.Finish == HandsetFinish.Etched ? chassis.Width * 0.045f : chassis.Width * 0.018f;
            frame.Paint.Fill(chassis, frame.Theme.Palette.SurfaceSunken, chassis.Width * 0.08f);
            frame.Paint.Stroke(chassis, gold with { W = lit == Part.Bezel ? 0.85f : 0.35f },
                MathF.Max(1.2f, rim * 0.4f), chassis.Width * 0.08f);
        }
        else
        {
            frame.Paint.FillSquircle(plate.BodyOn(chassis), new Vector4(0.08f, 0.08f, 0.08f, 1f),
                plate.CornerOn(chassis));
        }

        frame.Paint.Fill(glass, new Vector4(0f, 0f, 0f, 1f));
        PaintPlate(frame, glass);
        if (skin is { IsReady: true })
        {
            frame.Paint.Image(skin, chassis, Vector4.One);
        }

        if (lit == Part.Glass)
        {
            frame.Paint.Stroke(glass, gold with { W = 0.7f }, frame.Units(1.4f), glass.Width * 0.02f);
        }

        frame.Paint.Fill(strip, frame.Theme.Palette.SurfaceSunken with { W = 0.45f });
        if (lit == Part.Strip)
        {
            frame.Paint.Stroke(strip, gold with { W = 0.7f }, frame.Units(1.2f));
        }

        if (shape.ShowLockTab)
        {
            frame.Paint.Fill(lockTab, lit == Part.Lock
                ? gold with { W = 0.4f }
                : frame.Theme.Palette.SurfaceOverlay with { W = 0.7f }, lockTab.Height * 0.4f, Corner.Left);
        }

        frame.Paint.FillGradient(wash, gold with { W = 0.05f }, gold with { W = lit == Part.Ink ? 0.45f : 0.22f },
            GradientAxis.Horizontal);
        if (lit == Part.Bezel && skin is { IsReady: true })
        {
            frame.Paint.Stroke(chassis, gold with { W = 0.7f }, frame.Units(1.4f));
        }
    }

    private void PaintPlate(in AppletFrame frame, Rect glass)
    {
        frame.Paint.PushClip(glass);
        if (display.UsingCustomPlate)
        {
            DrawFile(frame, glass, PlateFiles.Absolute(paths, display.CustomPlateFile), 1f);
        }
        else
        {
            var plate = WallpaperCatalog.Resolve(display.WallpaperId);
            DrawFile(frame, glass, paths.Asset(Path.Combine(WallpaperCatalog.Folder, plate.DayFile)), 1f);
        }

        var scrim = frame.Theme.Palette.SurfaceSunken with { W = 0.22f * display.ShadeMul };
        frame.Paint.Fill(glass, scrim);
        frame.Paint.PopClip();
    }

    private void DrawFile(in AppletFrame frame, Rect area, string path, float alpha)
    {
        var texture = textures.FromFile(path);
        if (texture is null || !texture.IsReady)
        {
            return;
        }

        var crop = CoverFit.Uv(texture.Size, area.Size);
        frame.Paint.Image(texture, area, crop.Min, crop.Max, new Vector4(1f, 1f, 1f, alpha));
    }

    private void HitFront(in AppletFrame frame, Rect glass, Rect bezel, Rect strip, Rect lockTab, Rect wash)
    {
        if (shape.ShowLockTab && frame.Input.ConsumeClick(lockTab))
        {
            part = Part.Lock;
            return;
        }

        if (frame.Input.ConsumeClick(strip))
        {
            part = Part.Strip;
            return;
        }

        if (frame.Input.ConsumeClick(wash))
        {
            part = Part.Ink;
            return;
        }

        if (frame.Input.ConsumeClick(glass))
        {
            part = Part.Glass;
            return;
        }

        if (frame.Input.ConsumeClick(bezel))
        {
            part = Part.Bezel;
        }
    }

    private void DrawTouchTray(AppletFrame frame, Rect tray)
    {
        if (part == Part.None)
        {
            frame.Text.DrawWrapped(tray.TopSlice(frame.Units(40f)),
                "Touch the glass, rim, strip, pin, or the ink wash.",
                new TextStyle(FontRole.Caption, frame.Theme.Palette.InkMuted));
            return;
        }

        var stack = new Stack(tray, StackAxis.Vertical, frame.Units(8f));
        var title = part switch
        {
            Part.Glass => "Plate",
            Part.Bezel => "Body",
            Part.Strip => "Strip",
            Part.Lock => "Pin",
            Part.Ink => "Ink",
            _ => string.Empty,
        };
        frame.Text.DrawIn(stack.Take(frame.Units(18f)), title,
            new TextStyle(FontRole.CaptionStrong, frame.Theme.Palette.WarmAccent));
        switch (part)
        {
            case Part.Glass:
                DrawPlateControls(frame, ref stack);
                frame.Text.DrawIn(stack.Take(frame.Units(16f)), "Banner",
                    new TextStyle(FontRole.CaptionStrong, frame.Theme.Palette.WarmAccent));
                DrawBannerControls(frame, ref stack);
                break;
            case Part.Bezel:
                DrawBodyControls(frame, ref stack);
                break;
            case Part.Strip:
                DrawStripControls(frame, ref stack);
                break;
            case Part.Lock:
                DrawPinControls(frame, ref stack);
                break;
            case Part.Ink:
                DrawInkControls(frame, ref stack);
                break;
        }
    }

    private void DrawInkGroup(AppletFrame frame, ref Stack stack)
    {
        OpenGroup(frame, ref stack, "Ink", Stacked(frame, 36f, 36f, 36f, 36f), out var inner);
        var rows = new Stack(inner, StackAxis.Vertical, frame.Units(8f));
        DrawInkControls(frame, ref rows);
    }

    private void DrawPlateGroup(AppletFrame frame, ref Stack stack)
    {
        var innerHeight = Stacked(frame, 36f, 36f, 32f, 32f);
        if (pictureError.Length > 0)
        {
            innerHeight += frame.Units(8f) + frame.Units(16f);
        }

        OpenGroup(frame, ref stack, "Plate", innerHeight, out var inner);
        var rows = new Stack(inner, StackAxis.Vertical, frame.Units(8f));
        DrawPlateControls(frame, ref rows);
    }

    private void DrawBannerGroup(AppletFrame frame, ref Stack stack)
    {
        var innerHeight = Stacked(frame, 16f, 32f, 32f);
        if (bannerError.Length > 0)
        {
            innerHeight += frame.Units(8f) + frame.Units(16f);
        }

        OpenGroup(frame, ref stack, "Banner", innerHeight, out var inner);
        var rows = new Stack(inner, StackAxis.Vertical, frame.Units(8f));
        DrawBannerControls(frame, ref rows);
    }

    private void DrawBodyGroup(AppletFrame frame, ref Stack stack)
    {
        OpenGroup(frame, ref stack, "Body", Stacked(frame, 36f, 36f, 36f, 36f), out var inner);
        var rows = new Stack(inner, StackAxis.Vertical, frame.Units(8f));
        DrawBodyControls(frame, ref rows);
    }

    private void DrawStripGroup(AppletFrame frame, ref Stack stack)
    {
        OpenGroup(frame, ref stack, "Strip", Stacked(frame, 36f, 36f, 40f, 40f), out var inner);
        var rows = new Stack(inner, StackAxis.Vertical, frame.Units(8f));
        DrawStripControls(frame, ref rows);
    }

    private void DrawPinGroup(AppletFrame frame, ref Stack stack)
    {
        OpenGroup(frame, ref stack, "Pin", Stacked(frame, 40f, 40f), out var inner);
        var rows = new Stack(inner, StackAxis.Vertical, frame.Units(8f));
        DrawPinControls(frame, ref rows);
    }

    private void DrawPresenceGroup(AppletFrame frame, ref Stack stack)
    {
        OpenGroup(frame, ref stack, "Presence", Stacked(frame, 40f, 40f, 40f, 40f, 40f, 40f, 16f, 36f),
            out var inner);
        var rows = new Stack(inner, StackAxis.Vertical, frame.Units(8f));
        DrawPresenceControls(frame, ref rows);
    }

    private void DrawPresenceFace(AppletFrame frame, Rect body)
    {
        CardChrome.DrawGold(frame, body);
        var inner = body.Inset(frame.Units(14f));
        var stack = new Stack(inner, StackAxis.Vertical, frame.Units(8f));
        frame.Text.DrawWrapped(stack.Take(frame.Units(28f)), "The back of the pearl. How it sits in the world.",
            new TextStyle(FontRole.Caption, frame.Theme.Palette.InkMuted));
        DrawPresenceControls(frame, ref stack);
        frame.Text.DrawIn(stack.Take(frame.Units(16f)), environment.Version,
            new TextStyle(FontRole.Caption, frame.Theme.Palette.InkFaint));
    }

    private void DrawInkControls(AppletFrame frame, ref Stack stack)
    {
        ChoiceRow(frame, stack.Take(frame.Units(36f)), ColorwayId.All.Length, (cell, index) =>
        {
            var id = ColorwayId.All[index];
            Chip(frame, cell, ColorwayId.Label(id), display.Colorway == id, () => display.Colorway = id);
        });
        ChoiceRow(frame, stack.Take(frame.Units(36f)), CoreId.All.Length, (cell, index) =>
        {
            var id = CoreId.All[index];
            Chip(frame, cell, CoreId.Label(id), display.Core == id, () => display.Core = id);
        });
        ChoiceRow(frame, stack.Take(frame.Units(36f)), 3, (cell, index) =>
        {
            var size = (LetteringSize)index;
            var label = size switch
            {
                LetteringSize.Small => "S",
                LetteringSize.Large => "L",
                _ => "M",
            };
            Chip(frame, cell, "Lettering " + label, display.Lettering == size, () => display.Lettering = size);
        });
        ChoiceRow(frame, stack.Take(frame.Units(36f)), 3, (cell, index) =>
        {
            var mode = (AppearanceMode)index;
            var label = mode switch
            {
                AppearanceMode.Day => "Day",
                AppearanceMode.Night => "Night",
                _ => "Auto",
            };
            Chip(frame, cell, label, display.Appearance == mode, () => display.Appearance = mode);
        });
    }

    private void DrawPlateControls(AppletFrame frame, ref Stack stack)
    {
        ChoiceRow(frame, stack.Take(frame.Units(36f)), WallpaperCatalog.All.Count + 1, (cell, index) =>
        {
            if (index == WallpaperCatalog.All.Count)
            {
                var on = display.UsingCustomPlate;
                Chip(frame, cell, "Yours", on, () =>
                {
                    if (!on)
                    {
                        TryBringPicture();
                    }
                });
                return;
            }

            var plate = WallpaperCatalog.All[index];
            var selected = !display.UsingCustomPlate &&
                           string.Equals(display.WallpaperId, plate.Id, StringComparison.Ordinal);
            Chip(frame, cell, plate.Label, selected, () =>
            {
                display.CustomPlateFile = string.Empty;
                display.WallpaperId = plate.Id;
            });
        });
        ChoiceRow(frame, stack.Take(frame.Units(36f)), 3, (cell, index) =>
        {
            var level = (ShadeLevel)index;
            var label = level switch
            {
                ShadeLevel.Light => "Light",
                ShadeLevel.Deep => "Deep",
                _ => "Even",
            };
            Chip(frame, cell, label, display.Shade == level, () => display.Shade = level);
        });
        display.PictureDraft = frame.TextField.Draw("tune-plate", stack.Take(frame.Units(32f)), display.PictureDraft,
            "Bring a picture…");
        var apply = stack.Take(frame.Units(32f));
        Chip(frame, apply.LeftSlice(apply.Width * 0.48f), "Use path", false, TryBringPicture);
        if (display.UsingCustomPlate)
        {
            Chip(frame, apply.RightSlice(apply.Width * 0.48f), "Clear", false, () =>
            {
                display.CustomPlateFile = string.Empty;
                PlateFiles.Clear(paths);
                pictureError = string.Empty;
            });
        }

        if (pictureError.Length > 0)
        {
            frame.Text.DrawIn(stack.Take(frame.Units(16f)), pictureError,
                new TextStyle(FontRole.Caption, frame.Theme.Palette.Negative));
        }
    }

    private void TryBringPicture()
    {
        if (PlateFiles.TryImport(paths, display.PictureDraft, out var fileName))
        {
            display.CustomPlateFile = fileName;
            pictureError = string.Empty;
            return;
        }

        pictureError = "Could not use that picture.";
    }

    private void DrawBannerControls(AppletFrame frame, ref Stack stack)
    {
        frame.Text.DrawIn(stack.Take(frame.Units(16f)), "Behind Home.",
            new TextStyle(FontRole.Caption, frame.Theme.Palette.InkMuted));
        display.BannerDraft = frame.TextField.Draw("tune-banner", stack.Take(frame.Units(32f)), display.BannerDraft,
            "Bring a picture…");
        var apply = stack.Take(frame.Units(32f));
        Chip(frame, apply.LeftSlice(apply.Width * 0.48f), "Use path", false, TryBringBanner);
        if (display.UsingBanner)
        {
            Chip(frame, apply.RightSlice(apply.Width * 0.48f), "Clear", false, () =>
            {
                display.CustomBannerFile = string.Empty;
                BannerFiles.Clear(paths);
                bannerError = string.Empty;
            });
        }

        if (bannerError.Length > 0)
        {
            frame.Text.DrawIn(stack.Take(frame.Units(16f)), bannerError,
                new TextStyle(FontRole.Caption, frame.Theme.Palette.Negative));
        }
    }

    private void TryBringBanner()
    {
        if (BannerFiles.TryImport(paths, display.BannerDraft, out var fileName))
        {
            display.CustomBannerFile = fileName;
            bannerError = string.Empty;
            return;
        }

        bannerError = "Could not use that picture.";
    }

    private void DrawBodyControls(AppletFrame frame, ref Stack stack)
    {
        ChoiceRow(frame, stack.Take(frame.Units(36f)), HandsetSizeCatalog.StepLabels.Count, (cell, index) =>
        {
            var on = HandsetSizeCatalog.StepIndex(shape.ScaleStep) == index;
            Chip(frame, cell, HandsetSizeCatalog.StepLabels[index], on,
                () => shape.ScaleStep = HandsetSizeCatalog.ScaleSteps[index]);
        });
        ChoiceRow(frame, stack.Take(frame.Units(36f)), HandsetShapePreference.PocketLabels.Length, (cell, index) =>
        {
            var on = shape.PocketIndex() == index;
            Chip(frame, cell, "Pocket " + HandsetShapePreference.PocketLabels[index], on,
                () => shape.PocketScale = HandsetShapePreference.PocketSteps[index]);
        });
        PairRow(frame, stack.Take(frame.Units(36f)), "Phone", shape.Form == HandsetForm.Phone,
            () => shape.Form = HandsetForm.Phone, "Tablet", shape.Form == HandsetForm.Tablet,
            () => shape.Form = HandsetForm.Tablet);
        PairRow(frame, stack.Take(frame.Units(36f)), "Slim rim", shape.Finish == HandsetFinish.Crystal,
            () => shape.Finish = HandsetFinish.Crystal, "Wide rim", shape.Finish == HandsetFinish.Etched,
            () => shape.Finish = HandsetFinish.Etched);
    }

    private void DrawStripControls(AppletFrame frame, ref Stack stack)
    {
        PairRow(frame, stack.Take(frame.Units(36f)), "12-hour", !display.Use24HourClock,
            () => display.Use24HourClock = false, "24-hour", display.Use24HourClock,
            () => display.Use24HourClock = true);
        ChoiceRow(frame, stack.Take(frame.Units(36f)), 3, (cell, index) =>
        {
            var value = (ClockFace)index;
            var label = value switch
            {
                ClockFace.Eorzea => "Eorzea",
                ClockFace.Both => "Both",
                _ => "Local",
            };
            Chip(frame, cell, label, display.ClockFace == value, () => display.ClockFace = value);
        });
        ToggleRow(frame, stack.Take(frame.Units(40f)), "World", display.ShowWorld, value => display.ShowWorld = value);
        ToggleRow(frame, stack.Take(frame.Units(40f)), "Marks", display.ShowMarks, value => display.ShowMarks = value);
    }

    private void DrawPinControls(AppletFrame frame, ref Stack stack)
    {
        ToggleRow(frame, stack.Take(frame.Units(40f)), "Pinned", shape.PositionLocked,
            value => shape.PositionLocked = value);
        ToggleRow(frame, stack.Take(frame.Units(40f)), "Show pin", shape.ShowLockTab,
            value => shape.ShowLockTab = value);
    }

    private void DrawPresenceControls(AppletFrame frame, ref Stack stack)
    {
        ToggleRow(frame, stack.Take(frame.Units(40f)), "Wake in pocket", display.WakeInPocket,
            value => display.WakeInPocket = value);
        ToggleRow(frame, stack.Take(frame.Units(40f)), "Stay in portraits", display.StayInPortraits,
            value => display.StayInPortraits = value);
        ToggleRow(frame, stack.Take(frame.Units(40f)), "Tuck for cutscenes", display.TuckForCutscenes,
            value => display.TuckForCutscenes = value);
        ToggleRow(frame, stack.Take(frame.Units(40f)), "Quiet", display.Quiet, value => display.Quiet = value);
        ToggleRow(frame, stack.Take(frame.Units(40f)), "Hush in duty", display.QuietWhenBusy,
            value => display.QuietWhenBusy = value);
        ToggleRow(frame, stack.Take(frame.Units(40f)), "Still motion", display.ReduceMotion,
            value => display.ReduceMotion = value);
        frame.Text.DrawIn(stack.Take(frame.Units(16f)), "When fighting",
            new TextStyle(FontRole.CaptionStrong, frame.Theme.Palette.InkMuted));
        ChoiceRow(frame, stack.Take(frame.Units(36f)), 3, (cell, index) =>
        {
            var value = (FightPresence)index;
            var label = value switch
            {
                FightPresence.Pocket => "Pocket",
                FightPresence.Vanish => "Vanish",
                _ => "Stay",
            };
            Chip(frame, cell, label, display.Fight == value, () => display.Fight = value);
        });
    }

    private static float Stacked(in AppletFrame frame, params float[] rows)
    {
        var gap = frame.Units(8f);
        var total = 0f;
        for (var index = 0; index < rows.Length; index++)
        {
            if (index > 0)
            {
                total += gap;
            }

            total += frame.Units(rows[index]);
        }

        return total;
    }

    private static void OpenGroup(in AppletFrame frame, ref Stack stack, string title, float innerHeight, out Rect inner)
    {
        var pad = frame.Units(12f);
        var titleBand = frame.Units(22f);
        var group = stack.Take(innerHeight + pad * 2f + titleBand);
        CardChrome.DrawGold(frame, group);
        var body = group.Inset(pad);
        frame.Text.DrawIn(body.TopSlice(frame.Units(18f)), title,
            new TextStyle(FontRole.CaptionStrong, frame.Theme.Palette.WarmAccent));
        inner = body.Inset(new Edges(0f, titleBand, 0f, 0f));
    }

    private static void ChoiceRow(AppletFrame frame, Rect row, int count, Action<Rect, int> draw)
    {
        var gap = frame.Units(6f);
        var width = (row.Width - gap * (count - 1)) / Math.Max(count, 1);
        for (var index = 0; index < count; index++)
        {
            var cell = Rect.FromSize(new Vector2(row.Min.X + index * (width + gap), row.Min.Y),
                new Vector2(width, row.Height));
            draw(cell, index);
        }
    }

    private static void PairRow(AppletFrame frame, Rect row, string left, bool leftOn, Action leftTap, string right,
        bool rightOn, Action rightTap)
    {
        Chip(frame, row.LeftSlice(row.Width * 0.48f), left, leftOn, leftTap);
        Chip(frame, row.RightSlice(row.Width * 0.48f), right, rightOn, rightTap);
    }

    private static void ToggleRow(in AppletFrame frame, Rect row, string label, bool on, Action<bool> set)
    {
        frame.Paint.Fill(row, frame.Theme.Palette.SurfaceSunken with { W = 0.35f }, row.Height * 0.35f);
        frame.Text.DrawIn(row.Inset(new Edges(frame.Units(12f), 0f, frame.Units(64f), 0f)), label,
            new TextStyle(FontRole.Body, frame.Theme.Palette.Ink));
        Chip(frame, row.RightSlice(frame.Units(56f)).Inset(frame.Units(8f)), on ? "On" : "Off", on, () => set(!on));
    }

    private static void Chip(AppletFrame frame, Rect area, string label, bool active, Action tap)
    {
        if (area.Width <= 1f || area.Height <= 1f)
        {
            return;
        }

        var gold = frame.Theme.Palette.WarmAccent;
        frame.Paint.Fill(area, active ? gold with { W = 0.28f } : frame.Theme.Palette.SurfaceOverlay,
            area.Height * 0.4f);
        frame.Paint.Stroke(area, gold with { W = active ? 0.7f : 0.22f }, frame.Units(1f), area.Height * 0.4f);
        frame.Text.DrawEllipsized(area.Inset(frame.Units(4f)), label,
            new TextStyle(FontRole.Caption, active ? gold : frame.Theme.Palette.Ink, TextAlign.Center));
        if (frame.Input.ConsumeClick(area))
        {
            tap();
        }
    }
}
