using System.Diagnostics;
using Linkpearl.Applets;
using Linkpearl.Audio;
using Linkpearl.Badges;
using Linkpearl.Cards;
using Linkpearl.Chassis;
using Linkpearl.Destinations.Home;
using Linkpearl.Destinations.Profile;
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
    private readonly IAudioPorts audioPorts;
    private readonly IHandsetAudio audio;
    private readonly ProfileChrome profile;
    private Part part;
    private bool flipped;
    private string bannerError = string.Empty;
    private string hoverHint = string.Empty;
    private delegate void SheetDraw(AppletFrame frame, ref Stack stack);

    private readonly HashSet<string> unfolded = new(StringComparer.Ordinal);
    private string listQuery = string.Empty;
    private string listNeedle = string.Empty;
    private string searchPinned = string.Empty;
    private float pendingFocusY;
    private bool hasFocusY;
    private bool creditsOpen;
    private string creditLook = string.Empty;
    private string sliderDrag = string.Empty;
    private long lastPortScan;

    public SettingsDestination(HandsetShapePreference shape, DisplayPreferences display, HostEnvironment environment,
        IGameSession game, IPearlHub pearl, DestinationHub hub, HostPaths paths, ITextureSource textures,
        IFilePicker files, IAudioPorts audioPorts, IHandsetAudio audio, BadgeBook badges,
        HandsetProfileDesk profiles)
    {
        this.shape = shape;
        this.display = display;
        this.environment = environment;
        this.game = game;
        this.pearl = pearl;
        this.hub = hub;
        this.paths = paths;
        this.textures = textures;
        this.audioPorts = audioPorts;
        this.audio = audio;
        profile = new ProfileChrome(badges, paths, textures, files, pearl, game, display, environment.IsDevelopment,
            profiles);
    }

    public DestinationTab Tab => DestinationTab.Settings;

    public string Glyph => "⚙";

    public string Label => PhoneLanguages.T("nav.settings");

    public int CurrentSection => unfolded.Contains("General") || unfolded.Contains("Notifications")
        ? SettingsPane.Presence
        : SettingsPane.Front;

    public void ShowSection(int section)
    {
        part = Part.None;
        flipped = false;
        if (section == SettingsPane.Presence)
        {
            unfolded.Add("General");
        }
    }

    public void RevealTopic(string title)
    {
        if (title.Length == 0 || string.Equals(title, "Settings", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (string.Equals(title, "Credits", StringComparison.OrdinalIgnoreCase))
        {
            creditsOpen = true;
            creditLook = string.Empty;
            return;
        }

        creditsOpen = false;
        unfolded.Clear();
        var topic = string.Equals(title, "Languages", StringComparison.OrdinalIgnoreCase)
            ? "Language & Time"
            : title;
        unfolded.Add(topic);
        searchPinned = topic;
        hasFocusY = false;
    }

    public bool CanGoBack =>
        profile.OverlayOpen || creditLook.Length > 0 || creditsOpen || part != Part.None || flipped;

    public bool Back()
    {
        if (profile.Back())
        {
            return true;
        }

        if (creditLook.Length > 0)
        {
            creditLook = string.Empty;
            return true;
        }

        if (creditsOpen)
        {
            creditsOpen = false;
            return true;
        }

        if (part != Part.None)
        {
            part = Part.None;
            return true;
        }

        if (flipped)
        {
            flipped = false;
            return true;
        }

        return false;
    }

    public bool TryTakeScrollIntoView(out float y)
    {
        y = pendingFocusY;
        if (!hasFocusY)
        {
            return false;
        }

        hasFocusY = false;
        return true;
    }

    public float Compose(in AppletFrame frame)
    {
        hoverHint = string.Empty;
        if (profile.OverlayOpen)
        {
            return profile.DrawOverlay(frame);
        }

        var inset = frame.Units(14f);
        var content = frame.Content.Inset(inset);
        var stack = new Stack(content, StackAxis.Vertical, frame.Units(10f));
        DrawHead(frame, stack.Take(frame.Units(36f)));
        if (creditsOpen)
        {
            DrawCreditsPanel(frame, ref stack);
        }
        else
        {
            DrawBook(frame, ref stack);
        }

        DrawHoverTip(frame);
        frame.Input.ConsumeClick(frame.Content);
        return (content.Height - stack.Remaining.Height) + inset * 2f;
    }

    private void DrawHead(AppletFrame frame, Rect row)
    {
        var back = creditsOpen;
        var lead = row.LeftSlice(frame.Units(36f));
        if (back)
        {
            frame.Text.DrawIn(lead, "‹",
                new TextStyle(FontRole.Title, frame.Theme.Palette.WarmAccent, TextAlign.Center));
            if (frame.Input.ConsumeClick(lead))
            {
                Back();
            }
        }
        else
        {
            profile.DrawFace(frame, lead);
            if (frame.Input.ConsumeClick(lead))
            {
                profile.OpenEdit();
            }
        }

        frame.Text.DrawIn(row.Inset(new Edges(frame.Units(42f), 0f, 0f, 0f)),
            creditsOpen ? PhoneLanguages.T("set.credits") : PhoneLanguages.T("nav.settings"),
            new TextStyle(FontRole.Title, frame.Theme.Palette.Ink));
    }

    private void DrawBook(AppletFrame frame, ref Stack stack)
    {
        DrawPatreonSupport(frame, stack.Take(frame.Units(52f)));
        DrawSearch(frame, stack.Take(frame.Units(32f)));
        var needle = listQuery.Trim();
        if (needle != listNeedle)
        {
            listNeedle = needle;
            searchPinned = string.Empty;
        }
        DrawPreviewCard(frame, stack.Take(frame.Units(108f)));
        DrawTopic(frame, ref stack, "General", PhoneLanguages.T("set.general.blurb"),
            "lock pin combat portraits cutscenes motion behavior", OptionBand(frame, 6), DrawGeneralPage);
        DrawTopic(frame, ref stack, "Appearance", PhoneLanguages.T("set.appearance.blurb"),
            "theme display wallpaper dim home status accent text default", AppearanceInnerHeight(frame),
            DrawAppearancePage);
        DrawTopic(frame, ref stack, "Sounds", PhoneLanguages.T("set.sounds.blurb"),
            "sound speaker mic volume silent vibration", SoundsHeight(frame), DrawSoundsPage);
        DrawTopic(frame, ref stack, "Notifications", PhoneLanguages.T("set.notifications.blurb"),
            "notify quiet silent duty badge", OptionBand(frame, 3), DrawNotificationsPage);
        DrawTopic(frame, ref stack, "Feed", PhoneLanguages.T("set.feed.blurb"),
            "feed say shout yell party chat live", OptionBand(frame, 4), DrawFeedPage);
        DrawTopic(frame, ref stack, "Phone calls", PhoneLanguages.T("set.calls.blurb"),
            "phone call phonecalls wake portrait cutscene", OptionBand(frame, 3), DrawPhonePage);
        DrawTopic(frame, ref stack, "Language & Time", PhoneLanguages.T("set.language.blurb"),
            "language languages english clock 12 24 eorzea time timezone", LanguagePageHeight(frame),
            DrawLanguagesPage);
        frame.Text.DrawIn(stack.Take(frame.Units(16f)), PhoneLanguages.T("set.version") + " " + environment.Version,
            new TextStyle(FontRole.CaptionStrong, Vector4.One, TextAlign.Center));
        DrawTopic(frame, ref stack, "Terms of service", PhoneLanguages.T("set.tos.blurb"), "tos terms legal",
            frame.Units(220f), DrawTosPage);
        DrawBoxedLink(frame, stack.Take(OptionHeight(frame)), PhoneLanguages.T("set.discord"),
            () => OpenSite("https://discord.gg/KBf4wrzS6F"));
        DrawCreditsLink(frame, stack.Take(OptionHeight(frame)));
    }

    private void DrawAppearancePage(AppletFrame frame, ref Stack stack)
    {
        DrawTopic(frame, ref stack, "Themes", PhoneLanguages.T("set.themes.blurb"),
            "theme color accent text default", InkGroupHeight(frame), DrawInkControls);
        DrawTopic(frame, ref stack, "Display", PhoneLanguages.T("set.display.blurb"),
            "display size miniature minimized case layout brightness crystal etched rim finish", DisplayInnerHeight(frame), DrawDisplayPage);
        DrawTopic(frame, ref stack, "Wallpaper", PhoneLanguages.T("set.wallpaper.blurb"),
            "wallpaper plate photo gallery desktop background", PlateGroupHeight(frame), DrawPlateControls);
        DrawTopic(frame, ref stack, "Dim", PhoneLanguages.T("set.dim.blurb"),
            "dim shade overlay light medium dark", DimGroupHeight(frame), DrawDimControls);
        DrawTopic(frame, ref stack, "Home screen", PhoneLanguages.T("set.homescreen.blurb"),
            "home banner greeting screens extra", BannerGroupHeight(frame), DrawBannerControls);
        DrawTopic(frame, ref stack, "Status bar", PhoneLanguages.T("set.statusbar.blurb"),
            "status world icons", StatusGroupHeight(frame), DrawStripControls);
        DrawTopic(frame, ref stack, "Interactive tuner", PhoneLanguages.T("set.tuner.blurb"),
            "tuner touch preview", TunerInnerHeight(frame), DrawTouch);
    }

    private void DrawGeneralPage(AppletFrame frame, ref Stack stack)
    {
        DrawPinControls(frame, ref stack);
        ToggleRow(frame, stack.Take(OptionHeight(frame)), PhoneLanguages.T("set.motion"), display.ReduceMotion,
            value => display.ReduceMotion = value, PhoneLanguages.T("set.motion.hint"));
        ExclusiveRow(frame, ref stack, PhoneLanguages.T("set.fight.stay"), display.Fight == FightPresence.Stay,
            () => display.Fight = FightPresence.Stay, PhoneLanguages.T("set.fight.stay.hint"));
        ExclusiveRow(frame, ref stack, PhoneLanguages.T("set.fight.pocket"), display.Fight == FightPresence.Pocket,
            () => display.Fight = FightPresence.Pocket, PhoneLanguages.T("set.fight.pocket.hint"));
        ExclusiveRow(frame, ref stack, PhoneLanguages.T("set.fight.hide"), display.Fight == FightPresence.Vanish,
            () => display.Fight = FightPresence.Vanish, PhoneLanguages.T("set.fight.hide.hint"));
    }

    private void DrawDisplayPage(AppletFrame frame, ref Stack stack)
    {
        DrawSliderRow(frame, ref stack, PhoneLanguages.T("set.brightness"), display.Brightness,
            value => display.Brightness = value);
        DrawBodyControls(frame, ref stack);
    }

    private void DrawFeedPage(AppletFrame frame, ref Stack stack)
    {
        ToggleRow(frame, stack.Take(OptionHeight(frame)), PhoneLanguages.T("set.feed.say"), display.FeedShowSay,
            value => display.FeedShowSay = value, PhoneLanguages.T("set.feed.say.hint"));
        ToggleRow(frame, stack.Take(OptionHeight(frame)), PhoneLanguages.T("set.feed.shout"), display.FeedShowShout,
            value => display.FeedShowShout = value, PhoneLanguages.T("set.feed.shout.hint"));
        ToggleRow(frame, stack.Take(OptionHeight(frame)), PhoneLanguages.T("set.feed.yell"), display.FeedShowYell,
            value => display.FeedShowYell = value, PhoneLanguages.T("set.feed.yell.hint"));
        ToggleRow(frame, stack.Take(OptionHeight(frame)), PhoneLanguages.T("set.feed.party"), display.FeedShowParty,
            value => display.FeedShowParty = value, PhoneLanguages.T("set.feed.party.hint"));
    }

    private void DrawNotificationsPage(AppletFrame frame, ref Stack stack)
    {
        ToggleRow(frame, stack.Take(OptionHeight(frame)), PhoneLanguages.T("set.quiet"), display.Quiet,
            value => display.Quiet = value, PhoneLanguages.T("set.quiet.hint"));
        ToggleRow(frame, stack.Take(OptionHeight(frame)), PhoneLanguages.T("set.quiet.duty"), display.QuietWhenBusy,
            value => display.QuietWhenBusy = value, PhoneLanguages.T("set.quiet.duty.hint"));
        ToggleRow(frame, stack.Take(OptionHeight(frame)), PhoneLanguages.T("set.badges"), display.ShowMarks,
            value => display.ShowMarks = value, PhoneLanguages.T("set.badges.hint"));
    }

    private void DrawPhonePage(AppletFrame frame, ref Stack stack)
    {
        ToggleRow(frame, stack.Take(OptionHeight(frame)), PhoneLanguages.T("set.wake.calls"), display.WakeInPocket,
            value => display.WakeInPocket = value, PhoneLanguages.T("set.wake.calls.hint"));
        ToggleRow(frame, stack.Take(OptionHeight(frame)), PhoneLanguages.T("set.portraits"),
            display.StayInPortraits, value => display.StayInPortraits = value,
            PhoneLanguages.T("set.portraits.hint"));
        ToggleRow(frame, stack.Take(OptionHeight(frame)), PhoneLanguages.T("set.cutscenes"), display.TuckForCutscenes,
            value => display.TuckForCutscenes = value, PhoneLanguages.T("set.cutscenes.hint"));
        ToggleRow(frame, stack.Take(OptionHeight(frame)), PhoneLanguages.T("set.chat.e2e"), display.ChatE2E,
            value => display.ChatE2E = value, PhoneLanguages.T("set.chat.e2e.hint"));
    }

    private void DrawLanguagesPage(AppletFrame frame, ref Stack stack)
    {
        frame.Text.DrawIn(stack.Take(frame.Units(16f)), PhoneLanguages.T("set.app.language"),
            new TextStyle(FontRole.CaptionStrong, frame.Theme.Palette.InkMuted));
        var row = stack.Take(OptionHeight(frame));
        var gold = frame.Theme.Palette.WarmAccent;
        frame.Paint.Fill(row, frame.Theme.Palette.SurfaceOverlay, frame.Units(12f));
        frame.Paint.Stroke(row, gold with { W = 0.32f }, frame.Theme.Metrics.Hairline, frame.Units(12f));
        var inner = row.Inset(new Edges(frame.Units(8f), frame.Units(6f), frame.Units(8f), frame.Units(6f)));
        var selected = Math.Max(0, PhoneLanguages.IndexOf(display.LanguageId));
        var picked = frame.TextField.Combo("phone-language", inner, PhoneLanguages.MenuLabels, selected);
        frame.Input.Claim(row);
        if (picked != selected)
        {
            display.LanguageId = PhoneLanguages.All[picked].Id;
        }

        frame.Text.DrawWrapped(stack.Take(frame.Units(32f)), PhoneLanguages.T("set.app.language.hint"),
            new TextStyle(FontRole.Caption, frame.Theme.Palette.InkMuted));
        ExclusiveRow(frame, ref stack, PhoneLanguages.T("set.hour12"), !display.Use24HourClock,
            () => display.Use24HourClock = false, PhoneLanguages.T("set.hour12.hint"));
        ExclusiveRow(frame, ref stack, PhoneLanguages.T("set.hour24"), display.Use24HourClock,
            () => display.Use24HourClock = true, PhoneLanguages.T("set.hour24.hint"));
        ExclusiveRow(frame, ref stack, PhoneLanguages.T("set.clock.local"), display.ClockFace == ClockFace.Local,
            () => display.ClockFace = ClockFace.Local, PhoneLanguages.T("set.clock.local.hint"));
        ExclusiveRow(frame, ref stack, PhoneLanguages.T("set.clock.eorzea"), display.ClockFace == ClockFace.Eorzea,
            () => display.ClockFace = ClockFace.Eorzea, PhoneLanguages.T("set.clock.eorzea.hint"));
        ExclusiveRow(frame, ref stack, PhoneLanguages.T("set.clock.both"), display.ClockFace == ClockFace.Both,
            () => display.ClockFace = ClockFace.Both, PhoneLanguages.T("set.clock.both.hint"));
    }

    private void DrawSoundsPage(AppletFrame frame, ref Stack stack)
    {
        RefreshPorts();
        DrawSliderRow(frame, ref stack, PhoneLanguages.T("set.vol.listen"), display.Volume, value => display.Volume = value);
        DrawSliderRow(frame, ref stack, PhoneLanguages.T("set.vol.mic"), display.MicVolume, value => display.MicVolume = value);
        ToggleRow(frame, stack.Take(OptionHeight(frame)), PhoneLanguages.T("set.sounds.mute"), display.Quiet,
            value => display.Quiet = value, PhoneLanguages.T("set.silent.hint"));
        ActionRow(frame, ref stack, "Rescan audio devices", audioPorts.Status, () =>
        {
            lastPortScan = 0;
            audioPorts.Refresh();
        });
        DrawPortCombo(frame, ref stack, "sounds-speaker", "Listen through", audioPorts.Speakers,
            audioPorts.DefaultSpeakerId, display.SpeakerId, value =>
            {
                if (display.SpeakerId == value)
                {
                    return;
                }

                display.SpeakerId = value;
                audio.Cue();
            });
        DrawPortCombo(frame, ref stack, "sounds-microphone", "Talk through", audioPorts.Microphones,
            audioPorts.DefaultMicrophoneId, display.MicrophoneId, value => display.MicrophoneId = value);
    }

    private void DrawTosPage(AppletFrame frame, ref Stack stack)
    {
        frame.Text.DrawWrapped(stack.Take(frame.Units(220f)),
            "Linkpearl is a communicator for play in Final Fantasy XIV. Use it in good faith. " +
            "Do not use the phone to harass anyone, share account secrets, or break Square Enix or " +
            "Pearlgate rules. Pearlgate may store the name and world you choose to show. You can " +
            "sign out at any time from You. By using this phone you agree to keep other players' " +
            "messages and photos on the glass, not copied out to harm them.",
            new TextStyle(FontRole.Caption, frame.Theme.Palette.Ink));
    }

    private void DrawSliderRow(AppletFrame frame, ref Stack stack, string label, float value, Action<float> set)
    {
        var row = stack.Take(frame.Units(56f));
        var gold = frame.Theme.Palette.WarmAccent;
        var words = row.TopSlice(frame.Units(20f)).Inset(new Edges(frame.Units(8f), 0f, frame.Units(8f), 0f));
        frame.Text.DrawIn(words, label, new TextStyle(FontRole.CaptionStrong, frame.Theme.Palette.Ink));
        var track = row.BottomSlice(frame.Units(18f)).Inset(new Edges(frame.Units(8f), frame.Units(6f),
            frame.Units(8f), frame.Units(6f)));
        frame.Paint.Fill(track, frame.Theme.Palette.SurfaceSunken, track.Height * 0.5f);
        var fill = track.LeftSlice(MathF.Max(track.Height, track.Width * value));
        frame.Paint.Fill(fill, gold with { W = 0.88f }, track.Height * 0.5f);
        var thumb = new Vector2(track.Min.X + track.Width * value, track.Center.Y);
        frame.Paint.FillCircle(thumb, track.Height * 0.85f, gold);
        if (frame.Input.WasPressed(row))
        {
            sliderDrag = label;
        }

        if (sliderDrag == label && frame.Input.IsHeld())
        {
            set((frame.Input.Pointer.X - track.Min.X) / MathF.Max(track.Width, 1f));
        }

        if (!frame.Input.IsHeld())
        {
            sliderDrag = string.Empty;
        }
    }

    private static void OpenSite(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch
        {
            // The OS can refuse a browse; the row still stays tappable.
        }
    }

    private void DrawSearch(in AppletFrame frame, Rect field)
    {
        var gold = frame.Theme.Palette.WarmAccent;
        frame.Paint.Fill(field, frame.Theme.Palette.SurfaceOverlay with { W = 0.72f }, field.Height * 0.5f);
        frame.Paint.Stroke(field, gold with { W = 0.40f }, frame.Theme.Metrics.Hairline, field.Height * 0.5f);
        var type = field.Inset(new Edges(frame.Units(28f), frame.Units(4f), frame.Units(8f), frame.Units(4f)));
        listQuery = frame.TextField.Draw("settings-list-search", type, listQuery, "Search settings");
        SearchMark.Draw(frame.Paint, field.LeftSlice(frame.Units(28f)).Inset(frame.Units(5f)), gold);
    }

    private void DrawPreviewCard(in AppletFrame frame, Rect row)
    {
        CardChrome.DrawGold(frame, row);
        var inner = row.Inset(frame.Units(10f));
        var preview = inner.LeftSlice(inner.Width * 0.36f);
        DrawChassis(frame, ChassisRect(preview, ChassisCatalog.For(shape.Form, shape.Case, shape.Finish), fill: true),
            Part.None);
        var copy = inner.Inset(new Edges(preview.Width + frame.Units(12f), frame.Units(6f), 0f, 0f));
        var lines = new Stack(copy, StackAxis.Vertical, frame.Units(4f));
        frame.Text.DrawIn(lines.Take(frame.Units(16f)), "Live look",
            new TextStyle(FontRole.CaptionStrong, frame.Theme.Palette.WarmAccent));
        frame.Text.DrawEllipsized(lines.Take(frame.Units(20f)), PlateLabel(),
            new TextStyle(FontRole.BodyStrong, frame.Theme.Palette.Ink));
        frame.Text.DrawEllipsized(lines.Take(frame.Units(16f)),
            ColorwayId.Label(display.Colorway) + " theme · " + CoreId.Label(display.Core),
            new TextStyle(FontRole.Caption, frame.Theme.Palette.InkMuted));
        var form = shape.Form == HandsetForm.Tablet ? "Tablet" : "Phone";
        var rim = shape.Finish == HandsetFinish.Etched ? "Etched" : "Crystal";
        var casing = shape.Case == HandsetCase.Android ? "Android" : "Pearl";
        frame.Text.DrawEllipsized(lines.Take(frame.Units(16f)), form + " · " + rim + " · " + casing,
            new TextStyle(FontRole.Caption, frame.Theme.Palette.InkMuted));
    }

    private void DrawTouch(AppletFrame frame, ref Stack stack)
    {
        var flip = stack.Take(frame.Units(32f));
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
            flipped ? "Behavior" : "Touch a part",
            new TextStyle(FontRole.Caption, frame.Theme.Palette.InkMuted));

        if (flipped)
        {
            DrawPresenceFace(frame, stack.Take(PresenceGroupHeight(frame) + frame.Units(108f)));
            return;
        }

        var previewHeight = part == Part.None ? frame.Units(260f) : frame.Units(168f);
        var preview = stack.Take(previewHeight);
        var plate = ChassisCatalog.For(shape.Form, shape.Case, shape.Finish);
        var chassis = ChassisRect(preview, plate, fill: false);
        var glass = plate.GlassOn(chassis);
        var strip = glass.TopSlice(glass.Height * 0.11f);
        var lockTab = new Rect(new Vector2(glass.Max.X - glass.Width * 0.18f, glass.Min.Y),
            new Vector2(glass.Max.X, strip.Max.Y));
        var wash = glass.BottomSlice(glass.Height * 0.12f);
        DrawChassis(frame, chassis, part);
        HitFront(frame, glass, chassis, strip, lockTab, wash);
        DrawTouchSheet(frame, ref stack);
    }

    private string PlateLabel() => display.UsingCustomPlate
        ? "Yours"
        : WallpaperCatalog.Resolve(display.WallpaperId).Label;

    private static Rect ChassisRect(Rect area, HandsetForm form, bool fill) =>
        ChassisRect(area, ChassisCatalog.For(form), fill);

    private static Rect ChassisRect(Rect area, ChassisPlate plate, bool fill)
    {
        var aspect = plate.Aspect;
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

    private void DrawChassis(in AppletFrame frame, Rect chassis, Part lit, ChassisPlate? plate = null)
    {
        var gold = frame.Theme.Palette.WarmAccent;
        var used = plate ?? ChassisCatalog.For(shape.Form, shape.Case, shape.Finish);
        var androidSkin = used.FileName == ChassisCatalog.Android.FileName;
        var glass = used.GlassOn(chassis);
        var hole = used.ScreenOn(chassis);
        var skin = textures.FromFile(paths.Asset(Path.Combine(ChassisCatalog.Folder, used.FileName)));
        var strip = glass.TopSlice(glass.Height * 0.11f);
        var lockTab = new Rect(new Vector2(glass.Max.X - glass.Width * 0.18f, glass.Min.Y),
            new Vector2(glass.Max.X, strip.Max.Y));
        var wash = glass.BottomSlice(glass.Height * 0.12f);
        var body = used.BodyOn(chassis);
        if (skin is not { IsReady: true })
        {
            var rim = shape.Finish == HandsetFinish.Etched ? chassis.Width * 0.045f : chassis.Width * 0.018f;
            CaseWash.Body(frame.Paint, chassis, chassis.Width * 0.08f, frame.Theme.Palette.SurfaceSunken,
                frame.Theme.Palette.WarmAccent);
            frame.Paint.Stroke(chassis, gold with { W = lit == Part.Bezel ? 0.85f : 0.35f },
                MathF.Max(1.2f, rim * 0.4f), chassis.Width * 0.08f);
        }
        var glassRadius = used.GlassRadiusOn(chassis);
        var holeRadius = used.ScreenRadiusOn(chassis);
        var ink = new Vector4(0f, 0f, 0f, 1f);
        if (skin is { IsReady: true })
        {
            if (!androidSkin)
            {
                frame.Paint.FillSquircle(hole, ink, holeRadius);
            }

            CaseWash.StampSkin(frame.Paint, skin, chassis,
                androidSkin ? 0f : used.CornerOn(chassis),
                tint: androidSkin || shape.Finish == HandsetFinish.Etched ? Vector4.One : null);
        }

        frame.Paint.FillSquircle(hole, ink, holeRadius);
        PaintPlate(frame, glass, glassRadius);

        var gasket = used.GasketOn(chassis);
        var band = gasket > 0.5f ? 1.2f : MathF.Max(0.723f, chassis.Width * 0.00434f);
        var gasketRim = new Rect(glass.Min, glass.Max + new Vector2(1f, 1f));
        frame.Paint.Stroke(gasketRim, new Vector4(0f, 0f, 0f, 1f), band, glassRadius);

        if (used.FileName != ChassisCatalog.Android.FileName)
        {
            CaseWash.Sheen(frame.Paint, body.IsEmpty ? chassis : body, glass, used.CornerOn(chassis));
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

    private void PaintPlate(in AppletFrame frame, Rect glass, float radius)
    {
        frame.Paint.PushClip(glass);
        if (display.UsingCustomPlate)
        {
            DrawFile(frame, glass, PlateFiles.Absolute(paths, display.CustomPlateFile), 1f, radius);
        }
        else
        {
            var plate = WallpaperCatalog.Resolve(display.WallpaperId);
            DrawFile(frame, glass, paths.Asset(Path.Combine(WallpaperCatalog.Folder, plate.DayFile)), 1f, radius);
        }

        var scrim = frame.Theme.Palette.SurfaceSunken with { W = 0.22f * display.ShadeMul };
        frame.Paint.Fill(glass, scrim, radius);
        frame.Paint.PopClip();
    }

    private void DrawFile(in AppletFrame frame, Rect area, string path, float alpha, float radius = 0f)
    {
        var texture = textures.FromFile(path);
        if (texture is null || !texture.IsReady)
        {
            return;
        }

        var crop = CoverFit.Uv(texture.Size, area.Size);
        var tint = new Vector4(1f, 1f, 1f, alpha);
        if (radius > 0.5f)
        {
            frame.Paint.ImageRounded(texture, area, crop.Min, crop.Max, tint, radius);
        }
        else
        {
            frame.Paint.Image(texture, area, crop.Min, crop.Max, tint);
        }
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

    private void DrawTouchSheet(AppletFrame frame, ref Stack stack)
    {
        if (part == Part.None)
        {
            frame.Text.DrawWrapped(stack.Take(frame.Units(40f)),
                "Touch the screen for Appearance. The ink wash opens Wallpaper. Rim opens Crystal or Etched, plus size. Status bar and lock open those lists.",
                new TextStyle(FontRole.Caption, frame.Theme.Palette.InkMuted));
            return;
        }

        switch (part)
        {
            case Part.Glass:
                DrawOpenSheet(frame, ref stack, "Appearance",
                    "Default theme, accent color, and text size.",
                    InkGroupHeight(frame), DrawInkControls);
                break;
            case Part.Ink:
                DrawOpenSheet(frame, ref stack, "Wallpaper",
                    "Choose a wallpaper. Photos in Gallery can switch the background.",
                    PlateGroupHeight(frame), DrawPlateControls);
                DrawOpenSheet(frame, ref stack, "Dim",
                    "Light, medium, or dark overlay.",
                    DimGroupHeight(frame), DrawDimControls);
                DrawOpenSheet(frame, ref stack, "Home screen", "Picture behind the Home greeting.",
                    BannerGroupHeight(frame), DrawBannerControls);
                break;
            case Part.Bezel:
                DrawOpenSheet(frame, ref stack, "Display",
                    "Screen size, miniature size, Crystal or Etched rim, and layout.",
                    SizeGroupHeight(frame), DrawBodyControls);
                break;
            case Part.Strip:
                DrawOpenSheet(frame, ref stack, "Status bar",
                    "Clock, world name, and status icons at the top of the screen.",
                    StatusGroupHeight(frame), DrawStripControls);
                break;
            case Part.Lock:
                DrawOpenSheet(frame, ref stack, "Lock",
                    "Keep the phone from moving, and show or hide the lock button.",
                    OptionBand(frame, 2), DrawPinControls);
                break;
        }
    }

    private static float PlateGroupHeight(in AppletFrame frame) =>
        StackRun(frame, OptionBand(frame, WallpaperCatalog.All.Count), frame.Units(16f), frame.Units(32f)) +
        frame.Units(8f);

    private static float DimGroupHeight(in AppletFrame frame) =>
        StackRun(frame, frame.Units(16f), frame.Units(36f)) + frame.Units(8f);

    private static float InkGroupHeight(in AppletFrame frame) =>
        StackRun(frame,
            frame.Units(16f),
            frame.Units(16f),
            frame.Units(36f),
            frame.Units(16f),
            frame.Units(36f),
            frame.Units(40f)) + frame.Units(8f);

    private float BannerGroupHeight(in AppletFrame frame)
    {
        var extra = frame.Units(16f) + frame.Units(8f) + frame.Units(32f) + frame.Units(8f) + OptionHeight(frame) +
            frame.Units(8f) + OptionHeight(frame) + frame.Units(8f) + OptionHeight(frame);
        if (display.UsingBanner)
        {
            extra += frame.Units(8f) + frame.Units(72f) + frame.Units(8f) + OptionHeight(frame) +
                frame.Units(8f) + OptionHeight(frame);
        }

        if (bannerError.Length > 0)
        {
            extra += frame.Units(8f) + frame.Units(16f);
        }

        return extra;
    }

    private static float SizeGroupHeight(in AppletFrame frame) =>
        StackRun(frame,
            frame.Units(16f),
            frame.Units(108f),
            frame.Units(16f),
            frame.Units(36f),
            frame.Units(16f),
            frame.Units(72f),
            frame.Units(16f),
            frame.Units(36f),
            frame.Units(56f),
            frame.Units(36f),
            frame.Units(16f),
            frame.Units(16f),
            frame.Units(36f)) + frame.Units(8f);

    private static float StatusGroupHeight(in AppletFrame frame) => OptionBand(frame, 2);

    private static float DisplayInnerHeight(in AppletFrame frame) =>
        StackRun(frame, frame.Units(56f), SizeGroupHeight(frame));

    private void RefreshPorts()
    {
        var now = Environment.TickCount64;
        if (lastPortScan == 0)
        {
            lastPortScan = now;
            if (audioPorts.Speakers.Count > 0 || audioPorts.Microphones.Count > 0)
            {
                return;
            }
        }
        else if (now - lastPortScan < 15_000)
        {
            return;
        }

        lastPortScan = now;
        audioPorts.Refresh();
    }

    private static float SoundsHeight(in AppletFrame frame)
    {
        var gap = frame.Units(8f);
        return frame.Units(56f) + gap + frame.Units(56f) + gap + OptionHeight(frame) + gap +
            OptionHeight(frame) + gap +
            frame.Units(22f) + gap + OptionHeight(frame) + gap +
            frame.Units(22f) + gap + OptionHeight(frame);
    }

    private float TunerInnerHeight(in AppletFrame frame)
    {
        if (flipped)
        {
            return frame.Units(32f) + PresenceGroupHeight(frame) + frame.Units(108f);
        }

        var preview = part == Part.None ? frame.Units(260f) : frame.Units(168f);
        var sheet = part == Part.None ? frame.Units(48f) : frame.Units(220f);
        return frame.Units(32f) + preview + frame.Units(8f) + sheet;
    }

    private float TopicCardHeight(in AppletFrame frame, string title, float inner)
    {
        var head = OptionHeight(frame);
        var pad = frame.Units(12f);
        return head + (TopicOpen(title) ? pad + inner + pad : 0f);
    }

    private bool TopicOpen(string title)
    {
        if (listQuery.Trim().Length == 0)
        {
            return unfolded.Contains(title);
        }

        return searchPinned.Length == 0 || searchPinned == title;
    }

    private float AppearanceInnerHeight(in AppletFrame frame)
    {
        var gap = frame.Units(8f);
        return TopicCardHeight(frame, "Themes", InkGroupHeight(frame)) + gap +
            TopicCardHeight(frame, "Display", DisplayInnerHeight(frame)) + gap +
            TopicCardHeight(frame, "Wallpaper", PlateGroupHeight(frame)) + gap +
            TopicCardHeight(frame, "Dim", DimGroupHeight(frame)) + gap +
            TopicCardHeight(frame, "Home screen", BannerGroupHeight(frame)) + gap +
            TopicCardHeight(frame, "Status bar", StatusGroupHeight(frame)) + gap +
            TopicCardHeight(frame, "Interactive tuner", TunerInnerHeight(frame));
    }

    private static float PresenceGroupHeight(in AppletFrame frame) => OptionBand(frame, 9);

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
        DrawCreditsLink(frame, stack.Take(frame.Units(36f)));
    }

    private static void DrawPatreonSupport(in AppletFrame frame, Rect row)
    {
        var radius = row.Height * 0.5f;
        var hovered = frame.Input.IsHovering(row);
        var coral = new Vector4(1.00f, 0.42f, 0.40f, 1f);
        var peach = new Vector4(1.00f, 0.72f, 0.38f, 1f);
        frame.Paint.Glow(row, coral with { W = hovered ? 0.48f : 0.30f }, radius, frame.Units(hovered ? 12f : 9f));
        frame.Paint.Fill(row, hovered ? peach : coral, radius);
        frame.Paint.Stroke(row, new Vector4(1f, 0.92f, 0.86f, hovered ? 0.55f : 0.32f), frame.Units(1.2f), radius);

        var label = PhoneLanguages.T("set.patreon");
        var heart = frame.Units(18f);
        var gap = frame.Units(8f);
        var textW = frame.Text.Measure(label, FontRole.BodyStrong).X;
        var pack = heart + gap + textW;
        var start = row.Center.X - pack * 0.5f;
        start = MathF.Max(row.Min.X + frame.Units(10f), start);
        var mark = Rect.FromSize(new Vector2(start, row.Center.Y - heart * 0.5f),
            new Vector2(heart, heart));
        if (!HomeMarks.TryDrawGlyph(frame.Paint, frame.Textures, frame.Paths, mark, "vybe-like.png", Vector4.One))
        {
            frame.Text.DrawIn(mark, "♥", new TextStyle(FontRole.BodyStrong, Vector4.One, TextAlign.Center));
        }

        frame.Text.DrawEllipsized(
            new Rect(new Vector2(start + heart + gap, row.Min.Y),
                new Vector2(row.Max.X - frame.Units(10f), row.Max.Y)),
            label, new TextStyle(FontRole.BodyStrong, Vector4.One));
        if (frame.Input.ConsumeClick(row))
        {
            OpenSite("https://www.patreon.com/c/LinkPearl_XIV/membership");
        }
    }

    private void DrawBoxedLink(in AppletFrame frame, Rect row, string title, Action tap)
    {
        CardChrome.DrawGold(frame, row);
        var inner = row.Inset(new Edges(frame.Units(12f), 0f, frame.Units(12f), 0f));
        frame.Text.DrawIn(inner.LeftSlice(inner.Width * 0.7f), title,
            new TextStyle(FontRole.BodyStrong, frame.Theme.Palette.Ink));
        frame.Text.DrawIn(inner.RightSlice(inner.Width * 0.3f), "Open",
            new TextStyle(FontRole.CaptionStrong, frame.Theme.Palette.WarmAccent, TextAlign.Center));
        if (frame.Input.ConsumeClick(row))
        {
            tap();
        }
    }

    private void DrawCreditsLink(in AppletFrame frame, Rect row)
    {
        DrawBoxedLink(frame, row, PhoneLanguages.T("set.credits"), () => creditsOpen = true);
    }

    private void DrawCreditsPanel(in AppletFrame frame, ref Stack stack)
    {
        var credits = CreditBook.Resolve(pearl.Current, game.Character.Name);
        if (creditLook.Length > 0)
        {
            for (var index = 0; index < credits.Length; index++)
            {
                if (string.Equals(credits[index].Id, creditLook, StringComparison.Ordinal))
                {
                    DrawCreditProfile(frame, ref stack, credits[index]);
                    return;
                }
            }

            creditLook = string.Empty;
        }

        var bar = stack.Take(frame.Units(36f));
        Chip(frame, bar.LeftSlice(frame.Units(72f)), "Back", false, () =>
        {
            creditLook = string.Empty;
            creditsOpen = false;
        });
        frame.Text.DrawIn(bar.Inset(new Edges(frame.Units(80f), 0f, 0f, 0f)), PhoneLanguages.T("set.credits"),
            new TextStyle(FontRole.BodyStrong, frame.Theme.Palette.Ink));
        frame.Text.DrawIn(stack.Take(frame.Units(28f)),
            "Plugins and people behind this phone. Tap a profile to open it.",
            new TextStyle(FontRole.Caption, frame.Theme.Palette.InkMuted));

        frame.Text.DrawIn(stack.Take(frame.Units(16f)), "PLUGINS",
            new TextStyle(FontRole.CaptionStrong, frame.Theme.Palette.WarmAccent));
        for (var index = 0; index < credits.Length; index++)
        {
            if (credits[index].Kind == CreditKind.Plugin)
            {
                DrawCreditRow(frame, stack.Take(frame.Units(80f)), credits[index]);
            }
        }

        frame.Text.DrawIn(stack.Take(frame.Units(16f)), "PEOPLE",
            new TextStyle(FontRole.CaptionStrong, frame.Theme.Palette.WarmAccent));
        for (var index = 0; index < credits.Length; index++)
        {
            if (credits[index].Kind == CreditKind.Person)
            {
                DrawCreditRow(frame, stack.Take(frame.Units(80f)), credits[index]);
            }
        }
    }

    private void DrawCreditProfile(in AppletFrame frame, ref Stack stack, ShownCredit credit)
    {
        var bar = stack.Take(frame.Units(36f));
        Chip(frame, bar.LeftSlice(frame.Units(72f)), "Back", false, () => creditLook = string.Empty);
        frame.Text.DrawIn(bar.Inset(new Edges(frame.Units(80f), 0f, 0f, 0f)), "Profile",
            new TextStyle(FontRole.BodyStrong, frame.Theme.Palette.Ink));

        var hero = stack.Take(frame.Units(132f));
        CardChrome.DrawGold(frame, hero);
        var inset = hero.Inset(frame.Units(14f));
        var face = inset.LeftSlice(frame.Units(88f));
        DrawCreditFace(frame, face, credit);
        var copy = inset.Inset(new Edges(frame.Units(100f), frame.Units(8f), 0f, 0f));
        frame.Text.DrawEllipsized(copy.TopSlice(frame.Units(22f)), credit.Name,
            new TextStyle(FontRole.Title, frame.Theme.Palette.Ink));
        var kind = credit.Kind == CreditKind.Plugin ? "Plugin" : "Person";
        var line = credit.Handle.Length > 0 ? "@" + credit.Handle.TrimStart('@') + "  ·  " + kind : kind;
        frame.Text.DrawEllipsized(copy.Inset(new Edges(0f, frame.Units(24f), 0f, frame.Units(22f))), line,
            new TextStyle(FontRole.Caption, frame.Theme.Palette.WarmAccent));
        frame.Text.DrawWrapped(copy.BottomSlice(frame.Units(36f)), credit.Work,
            new TextStyle(FontRole.Caption, frame.Theme.Palette.InkMuted));

        var about = stack.Take(frame.Units(72f));
        CardChrome.Draw(frame, about);
        var body = about.Inset(frame.Units(12f));
        frame.Text.DrawIn(body.TopSlice(frame.Units(16f)), "Contribution",
            new TextStyle(FontRole.CaptionStrong, frame.Theme.Palette.WarmAccent));
        frame.Text.DrawWrapped(body.Inset(new Edges(0f, frame.Units(20f), 0f, 0f)), credit.Work,
            new TextStyle(FontRole.Caption, frame.Theme.Palette.InkMuted));

        if (credit.Kind == CreditKind.Person && credit.Name.Length > 0)
        {
            pearl.NoteQuery(credit.Name);
        }

        DrawCreditSite(frame, ref stack, "Plugin page", credit.PluginPage);
        DrawCreditSite(frame, ref stack, "GitHub", credit.GitHubPage);

        if (credit.Kind != CreditKind.Person && credit.ProfileId.Length > 0)
        {
            var gate = stack.Take(frame.Units(44f));
            CardChrome.DrawGold(frame, gate);
            frame.Text.DrawIn(gate, "Open Pearlgate profile",
                new TextStyle(FontRole.BodyStrong, frame.Theme.Palette.WarmAccent, TextAlign.Center));
            if (frame.Input.ConsumeClick(gate))
            {
                pearl.WatchProfile(credit.ProfileId);
                hub.OpenProfile(credit.ProfileId);
            }
        }
    }

    private static void DrawCreditSite(in AppletFrame frame, ref Stack stack, string label, string url)
    {
        if (url.Length == 0)
        {
            return;
        }

        var row = stack.Take(frame.Units(44f));
        CardChrome.DrawGold(frame, row);
        frame.Text.DrawIn(row, label,
            new TextStyle(FontRole.BodyStrong, frame.Theme.Palette.WarmAccent, TextAlign.Center));
        if (frame.Input.ConsumeClick(row))
        {
            OpenSite(url);
        }
    }

    private void DrawCreditRow(in AppletFrame frame, Rect row, ShownCredit credit)
    {
        CardChrome.DrawGold(frame, row);
        var inner = row.Inset(frame.Units(10f));
        var face = inner.LeftSlice(frame.Units(48f));
        DrawCreditFace(frame, face, credit);
        var copy = inner.Inset(new Edges(frame.Units(58f), frame.Units(4f), frame.Units(18f), 0f));
        var name = credit.Handle.Length > 0 ? credit.Name + "  @" + credit.Handle.TrimStart('@') : credit.Name;
        frame.Text.DrawEllipsized(copy.TopSlice(frame.Units(20f)), name,
            new TextStyle(FontRole.BodyStrong, frame.Theme.Palette.Ink));
        frame.Text.DrawWrapped(copy.Inset(new Edges(0f, frame.Units(22f), 0f, 0f)), credit.Work,
            new TextStyle(FontRole.Caption, frame.Theme.Palette.InkMuted));
        frame.Text.DrawIn(inner.RightSlice(frame.Units(16f)), "›",
            new TextStyle(FontRole.Title, frame.Theme.Palette.WarmAccent, TextAlign.Center));
        if (frame.Input.ConsumeClick(row))
        {
            creditLook = credit.Id;
            if (credit.Kind == CreditKind.Person && credit.Name.Length > 0)
            {
                pearl.NoteQuery(credit.Name);
            }

            if (credit.ProfileId.Length > 0)
            {
                pearl.WatchProfile(credit.ProfileId);
            }
        }
    }

    private void DrawCreditFace(in AppletFrame frame, Rect area, ShownCredit credit)
    {
        var radius = MathF.Min(area.Width, area.Height) * 0.5f;
        if (TryDrawCreditFace(frame, area, credit.AvatarUrl, radius, true))
        {
            return;
        }

        if (credit.IconAsset.Length > 0 &&
            TryDrawCreditFace(frame, area, frame.Paths.Asset(credit.IconAsset.Replace('/', Path.DirectorySeparatorChar)),
                radius, false))
        {
            return;
        }

        frame.Paint.FillCircle(area.Center, radius, frame.Theme.Palette.SurfaceRaised);
        frame.Paint.StrokeCircle(area.Center, radius, frame.Theme.Palette.WarmAccent, frame.Units(1.2f));
        var glyph = credit.Name.Length > 0 ? credit.Name[0].ToString() : "?";
        frame.Text.DrawIn(area, glyph, new TextStyle(FontRole.BodyStrong, frame.Theme.Palette.Ink, TextAlign.Center));
    }

    private bool TryDrawCreditFace(in AppletFrame frame, Rect area, string source, float radius, bool remote)
    {
        if (source.Length == 0)
        {
            return false;
        }

        var path = source;
        if (remote)
        {
            pearl.PrefetchMedia(source);
            path = pearl.LocalMedia(source) ?? string.Empty;
        }

        if (path.Length == 0)
        {
            return false;
        }

        var texture = frame.Textures.FromFile(path);
        if (texture is not { IsReady: true })
        {
            return false;
        }

        var dest = Rect.FromSize(area.Center - new Vector2(radius, radius),
            new Vector2(radius * 2f, radius * 2f));
        var uv = CoverFit.Uv(texture.Size, dest.Size);
        frame.Paint.ImageRounded(texture, dest, uv.Min, uv.Max, Vector4.One, radius);
        return true;
    }

    private void DrawInkControls(AppletFrame frame, ref Stack stack)
    {
        display.Colorway = ColorwayId.Night;
        frame.Text.DrawIn(stack.Take(frame.Units(16f)), "Default theme",
            new TextStyle(FontRole.CaptionStrong, frame.Theme.Palette.InkMuted));
        frame.Text.DrawIn(stack.Take(frame.Units(16f)), "Accent",
            new TextStyle(FontRole.Caption, frame.Theme.Palette.InkMuted));
        DrawAccentCircles(frame, stack.Take(frame.Units(36f)));
        frame.Text.DrawIn(stack.Take(frame.Units(16f)), "Text size",
            new TextStyle(FontRole.Caption, frame.Theme.Palette.InkMuted));
        DrawLetteringRow(frame, stack.Take(frame.Units(36f)));
        frame.Text.DrawWrapped(stack.Take(frame.Units(40f)),
            "Name, title, photo, banner, time zone, and Dreams typeface live on the profile in the top-left. Sync copies them to Music and VYBE.",
            new TextStyle(FontRole.Caption, frame.Theme.Palette.InkMuted));
    }

    private void DrawAccentCircles(AppletFrame frame, Rect row)
    {
        var gap = frame.Units(6f);
        var count = CoreId.All.Length;
        var cell = (row.Width - gap * (count - 1)) / count;
        var run = new Stack(row, StackAxis.Horizontal, gap);
        for (var index = 0; index < count; index++)
        {
            var id = CoreId.All[index];
            var cellRect = run.Take(cell);
            var radius = MathF.Min(cellRect.Width, cellRect.Height) * 0.38f;
            var selected = string.Equals(display.Core, id, StringComparison.Ordinal);
            var fill = CoreId.Swatch(id);
            frame.Paint.FillCircle(cellRect.Center, radius, fill);
            var rim = selected ? frame.Theme.Palette.Ink : frame.Theme.Palette.InkFaint;
            frame.Paint.StrokeCircle(cellRect.Center, radius, rim, selected ? frame.Units(2.2f) : frame.Units(1.1f));
            if (frame.Input.ConsumeClick(cellRect))
            {
                display.Core = id;
            }
        }
    }

    private void DrawLetteringRow(AppletFrame frame, Rect row)
    {
        DrawChoiceChips(frame, row, new[] { "Small", "Default", "Large" }, (int)display.Lettering,
            index => display.Lettering = (LetteringSize)index);
    }

    private void DrawPhoneSizeRow(AppletFrame frame, Rect row)
    {
        DrawChoiceChips(frame, row, new[] { "Small", "Medium", "Large" },
            HandsetSizeCatalog.StepIndex(shape.ScaleStep),
            index => shape.ScaleStep = HandsetSizeCatalog.ScaleSteps[index]);
    }

    private void DrawPocketSizeRow(AppletFrame frame, Rect row)
    {
        DrawChoiceChips(frame, row, HandsetShapePreference.PocketLabels, shape.PocketIndex(),
            index => shape.PocketScale = HandsetShapePreference.PocketSteps[index]);
    }

    private void DrawLayoutRow(AppletFrame frame, Rect row)
    {
        DrawChoiceChips(frame, row, new[] { "Phone", "Tablet" },
            shape.Form == HandsetForm.Tablet ? 1 : 0,
            index => shape.Form = index == 1 ? HandsetForm.Tablet : HandsetForm.Phone);
    }

    private void DrawFinishRow(AppletFrame frame, Rect row)
    {
        DrawChoiceChips(frame, row, new[] { "Crystal", "Etched" },
            shape.Finish == HandsetFinish.Etched ? 1 : 0,
            index => shape.Finish = index == 1 ? HandsetFinish.Etched : HandsetFinish.Crystal);
    }

    private void DrawBackPreview(in AppletFrame frame, Rect area)
    {
        var plate = ChassisCatalog.For(shape.Form, shape.Case, shape.Finish);
        if (!plate.HasBack)
        {
            frame.Text.DrawIn(area, "Pearl cases show a back plate here.",
                new TextStyle(FontRole.Caption, frame.Theme.Palette.InkMuted, TextAlign.Center));
            return;
        }

        var dest = ChassisRect(area, plate, fill: true);
        var skin = textures.FromFile(paths.Asset(Path.Combine(ChassisCatalog.Folder, plate.BackFileName)));
        if (skin is { IsReady: true })
        {
            CaseWash.StampSkin(frame.Paint, skin, dest, plate.CornerOn(dest), tint: Vector4.One);
            return;
        }

        CaseWash.Body(frame.Paint, dest, plate.CornerOn(dest), frame.Theme.Palette.SurfaceSunken,
            frame.Theme.Palette.WarmAccent);
    }

    private static void DrawChoiceChips(AppletFrame frame, Rect row, string[] labels, int selected, Action<int> pick)
    {
        var gap = frame.Units(6f);
        var count = labels.Length;
        var cell = (row.Width - gap * (count - 1)) / count;
        var run = new Stack(row, StackAxis.Horizontal, gap);
        for (var index = 0; index < count; index++)
        {
            var area = run.Take(cell);
            var on = index == selected;
            var radius = area.Height * 0.5f;
            if (on)
            {
                frame.Paint.Fill(area, frame.Theme.Palette.Accent with { W = 0.92f }, radius);
            }
            else
            {
                frame.Paint.Fill(area, frame.Theme.Palette.SurfaceSunken with { W = 0.82f }, radius);
                frame.Paint.Stroke(area, frame.Theme.Palette.InkFaint, frame.Theme.Metrics.Hairline, radius);
            }

            frame.Text.DrawIn(area, labels[index],
                new TextStyle(FontRole.CaptionStrong, on ? frame.Theme.Palette.AccentInk : frame.Theme.Palette.Ink,
                    TextAlign.Center));
            if (frame.Input.ConsumeClick(area))
            {
                pick(index);
            }
        }
    }

    private void DrawPlateControls(AppletFrame frame, ref Stack stack)
    {
        for (var index = 0; index < WallpaperCatalog.All.Count; index++)
        {
            var plate = WallpaperCatalog.All[index];
            var selected = !display.UsingCustomPlate &&
                           string.Equals(display.WallpaperId, plate.Id, StringComparison.Ordinal);
            ExclusiveRow(frame, ref stack, plate.Label, selected, () =>
            {
                display.CustomPlateFile = string.Empty;
                display.WallpaperId = plate.Id;
            }, "Set as wallpaper.");
        }

        frame.Text.DrawIn(stack.Take(frame.Units(16f)), "Set desktop background",
            new TextStyle(FontRole.CaptionStrong, frame.Theme.Palette.InkMuted));
        frame.Text.DrawWrapped(stack.Take(frame.Units(32f)),
            "Photos in Gallery can switch the background.",
            new TextStyle(FontRole.Caption, frame.Theme.Palette.InkMuted));
    }

    private void DrawDimControls(AppletFrame frame, ref Stack stack)
    {
        frame.Text.DrawIn(stack.Take(frame.Units(16f)), "Dim",
            new TextStyle(FontRole.Caption, frame.Theme.Palette.InkMuted));
        DrawChoiceChips(frame, stack.Take(frame.Units(36f)), new[] { "Light", "Medium", "Dark" },
            (int)display.Shade, index => display.Shade = (ShadeLevel)index);
    }

    private void DrawBannerControls(AppletFrame frame, ref Stack stack)
    {
        frame.Text.DrawIn(stack.Take(frame.Units(16f)), "Behind Home.",
            new TextStyle(FontRole.Caption, frame.Theme.Palette.InkMuted));
        display.BannerDraft = frame.TextField.Draw("tune-banner", stack.Take(frame.Units(32f)), display.BannerDraft,
            "Bring a picture…");
        ActionRow(frame, ref stack, "Set banner", "Use the picture path above.", TryBringBanner);
        if (display.UsingBanner)
        {
            DrawBannerPreview(frame, stack.Take(frame.Units(72f)));
            ActionRow(frame, ref stack, "Place banner", "Drag and zoom what Home shows.", profile.OpenBanner);
            ActionRow(frame, ref stack, "Remove banner", "Clear the Home banner picture.", () =>
            {
                var previous = display.CustomBannerFile;
                if (previous.Length > 0)
                {
                    textures.ForgetFile(BannerFiles.Absolute(paths, previous));
                }

                display.ResetBannerCrop();
                display.CustomBannerFile = string.Empty;
                BannerFiles.Clear(paths);
                bannerError = string.Empty;
                profile.ClearBanner();
            });
        }

        if (bannerError.Length > 0)
        {
            frame.Text.DrawIn(stack.Take(frame.Units(16f)), bannerError,
                new TextStyle(FontRole.Caption, frame.Theme.Palette.Negative));
        }

        var screens = display.ExtraHomeScreens;
        ActionRow(frame, ref stack, screens >= 5 ? "Extra screens full" : "Add a home screen",
            screens == 0 ? "Create another page beside Home." : screens + " extra screen" + (screens == 1 ? "." : "s."),
            () =>
            {
                if (display.ExtraHomeScreens < 5)
                {
                    display.ExtraHomeScreens++;
                }
            });
        if (screens > 0)
        {
            ActionRow(frame, ref stack, "Remove a home screen", "Drop the last extra page.",
                () => display.ExtraHomeScreens--);
        }
    }

    private void DrawBannerPreview(in AppletFrame frame, Rect area)
    {
        var gold = frame.Theme.Palette.WarmAccent;
        CardChrome.DrawGold(frame, area);
        var inner = area.Inset(frame.Units(2f));
        frame.Paint.Fill(inner, frame.Theme.Palette.SurfaceRaised, frame.Units(8f));
        var texture = textures.FromFile(BannerFiles.Absolute(paths, display.CustomBannerFile));
        if (texture is { IsReady: true })
        {
            CoverFit.Placed(texture.Size, inner, display.BannerZoom, display.BannerFocus, out var dest, out var uv);
            frame.Paint.ImageRounded(texture, dest, uv.Min, uv.Max, Vector4.One, frame.Units(8f));
        }

        frame.Paint.Stroke(inner, gold with { W = 0.42f }, frame.Theme.Metrics.Hairline, frame.Units(8f));
        if (frame.Input.ConsumeClick(inner))
        {
            profile.OpenBanner();
        }
    }

    private void TryBringBanner()
    {
        if (BannerFiles.TryImport(paths, display.BannerDraft, out var fileName))
        {
            var previous = display.CustomBannerFile;
            if (previous.Length > 0)
            {
                textures.ForgetFile(BannerFiles.Absolute(paths, previous));
                if (!string.Equals(previous, fileName, StringComparison.OrdinalIgnoreCase))
                {
                    BannerFiles.Delete(paths, previous);
                }
            }

            display.ResetBannerCrop();
            display.CustomBannerFile = fileName;
            textures.ForgetFile(BannerFiles.Absolute(paths, fileName));
            bannerError = string.Empty;
            pearl.SetBanner(BannerFiles.Absolute(paths, fileName));
            profile.OpenBanner();
            return;
        }

        bannerError = "Could not use that picture.";
    }

    private void DrawCasePicker(in AppletFrame frame, ref Stack stack)
    {
        frame.Text.DrawIn(stack.Take(frame.Units(16f)), "Case",
            new TextStyle(FontRole.CaptionStrong, frame.Theme.Palette.WarmAccent));
        var row = stack.Take(frame.Units(108f));
        var gap = frame.Units(8f);
        var width = (row.Width - gap) * 0.5f;
        var pearl = Rect.FromSize(row.Min, new Vector2(width, row.Height));
        var android = Rect.FromSize(new Vector2(row.Min.X + width + gap, row.Min.Y), new Vector2(width, row.Height));
        DrawCaseChoice(frame, pearl, HandsetCase.Pearl, "Pearl");
        DrawCaseChoice(frame, android, HandsetCase.Android, "Android");
    }

    private void DrawCaseChoice(in AppletFrame frame, Rect cell, HandsetCase casing, string label)
    {
        var on = shape.Case == casing;
        var gold = frame.Theme.Palette.WarmAccent;
        CardChrome.DrawGold(frame, cell);
        if (on)
        {
            frame.Paint.Stroke(cell, gold with { W = 0.75f }, frame.Units(1.4f), frame.Units(10f));
        }

        var inner = cell.Inset(frame.Units(6f));
        var caption = inner.BottomSlice(frame.Units(16f));
        var face = new Rect(inner.Min, new Vector2(inner.Max.X, caption.Min.Y - frame.Units(2f)));
        var plate = ChassisCatalog.For(HandsetForm.Phone, casing, shape.Finish);
        DrawChassis(frame, ChassisRect(face, plate, fill: true), on ? Part.Bezel : Part.None, plate);
        frame.Text.DrawIn(caption, label,
            new TextStyle(FontRole.CaptionStrong, on ? gold : frame.Theme.Palette.InkMuted, TextAlign.Center));
        if (frame.Input.ConsumeClick(cell))
        {
            shape.Case = casing;
            if (casing == HandsetCase.Android)
            {
                shape.Form = HandsetForm.Phone;
            }
        }
    }

    private void DrawBodyControls(AppletFrame frame, ref Stack stack)
    {
        DrawCasePicker(frame, ref stack);
        frame.Text.DrawIn(stack.Take(frame.Units(16f)), "Finish",
            new TextStyle(FontRole.CaptionStrong, frame.Theme.Palette.InkMuted));
        DrawFinishRow(frame, stack.Take(frame.Units(36f)));
        frame.Text.DrawIn(stack.Take(frame.Units(16f)), "Back plate",
            new TextStyle(FontRole.CaptionStrong, frame.Theme.Palette.InkMuted));
        DrawBackPreview(frame, stack.Take(frame.Units(72f)));
        frame.Text.DrawIn(stack.Take(frame.Units(16f)), "Layout",
            new TextStyle(FontRole.CaptionStrong, frame.Theme.Palette.InkMuted));
        DrawLayoutRow(frame, stack.Take(frame.Units(36f)));
        var sizeMin = HandsetSizeCatalog.MinScale;
        var sizeMax = Math.Max(HandsetSizeCatalog.MaxScale, shape.ScaleStep);
        var sizeSpan = MathF.Max(sizeMax - sizeMin, 0.01f);
        DrawSliderRow(frame, ref stack, PhoneLanguages.T("set.size"), (shape.ScaleStep - sizeMin) / sizeSpan, value =>
            shape.ScaleStep = HandsetSizeCatalog.ClampFree(sizeMin + Math.Clamp(value, 0f, 1f) * sizeSpan,
                HandsetSizeCatalog.FreeCeiling));
        DrawPhoneSizeRow(frame, stack.Take(frame.Units(36f)));

        var miniTitle = stack.Take(frame.Units(16f));
        frame.Text.DrawIn(miniTitle, "Miniature size",
            new TextStyle(FontRole.CaptionStrong, frame.Theme.Palette.InkMuted));
        var miniLabel = stack.Take(frame.Units(16f));
        frame.Text.DrawIn(miniLabel, "Tiny, Medium, Large - tap to cycle",
            new TextStyle(FontRole.Caption, frame.Theme.Palette.InkMuted));
        if (frame.Input.ConsumeClick(miniTitle) || frame.Input.ConsumeClick(miniLabel))
        {
            shape.CyclePocket();
        }

        DrawPocketSizeRow(frame, stack.Take(frame.Units(36f)));
    }

    private void DrawStripControls(AppletFrame frame, ref Stack stack)
    {
        ToggleRow(frame, stack.Take(OptionHeight(frame)), PhoneLanguages.T("set.world"), display.ShowWorld,
            value => display.ShowWorld = value, PhoneLanguages.T("set.world.hint"));
        ToggleRow(frame, stack.Take(OptionHeight(frame)), PhoneLanguages.T("set.icons"), display.ShowMarks,
            value => display.ShowMarks = value, PhoneLanguages.T("set.icons.hint"));
    }

    private void DrawPinControls(AppletFrame frame, ref Stack stack)
    {
        ToggleRow(frame, stack.Take(OptionHeight(frame)), PhoneLanguages.T("set.lock.pos"), shape.PositionLocked,
            value => shape.PositionLocked = value, PhoneLanguages.T("set.lock.pos.hint"));
        ToggleRow(frame, stack.Take(OptionHeight(frame)), PhoneLanguages.T("set.lock.tab"), shape.ShowLockTab,
            value => shape.ShowLockTab = value, PhoneLanguages.T("set.lock.tab.hint"));
    }

    private void DrawPresenceControls(AppletFrame frame, ref Stack stack)
    {
        ToggleRow(frame, stack.Take(OptionHeight(frame)), PhoneLanguages.T("set.wake.mini"), display.WakeInPocket,
            value => display.WakeInPocket = value, PhoneLanguages.T("set.wake.mini.hint"));
        ToggleRow(frame, stack.Take(OptionHeight(frame)), PhoneLanguages.T("set.portraits.open"), display.StayInPortraits,
            value => display.StayInPortraits = value, PhoneLanguages.T("set.portraits.hint"));
        ToggleRow(frame, stack.Take(OptionHeight(frame)), PhoneLanguages.T("set.cutscenes"), display.TuckForCutscenes,
            value => display.TuckForCutscenes = value, PhoneLanguages.T("set.cutscenes.hint"));
        ToggleRow(frame, stack.Take(OptionHeight(frame)), PhoneLanguages.T("set.chat.e2e"), display.ChatE2E,
            value => display.ChatE2E = value, PhoneLanguages.T("set.chat.e2e.hint"));
        ToggleRow(frame, stack.Take(OptionHeight(frame)), PhoneLanguages.T("set.silent"), display.Quiet,
            value => display.Quiet = value, PhoneLanguages.T("set.silent.hint"));
        ToggleRow(frame, stack.Take(OptionHeight(frame)), PhoneLanguages.T("set.silent.duty"), display.QuietWhenBusy,
            value => display.QuietWhenBusy = value, PhoneLanguages.T("set.quiet.duty.hint"));
        ToggleRow(frame, stack.Take(OptionHeight(frame)), PhoneLanguages.T("set.motion"), display.ReduceMotion,
            value => display.ReduceMotion = value, PhoneLanguages.T("set.motion.hint"));
        ExclusiveRow(frame, ref stack, PhoneLanguages.T("set.fight.stay"), display.Fight == FightPresence.Stay,
            () => display.Fight = FightPresence.Stay, PhoneLanguages.T("set.fight.stay.hint"));
        ExclusiveRow(frame, ref stack, PhoneLanguages.T("set.fight.pocket"), display.Fight == FightPresence.Pocket,
            () => display.Fight = FightPresence.Pocket, PhoneLanguages.T("set.fight.pocket.hint"));
        ExclusiveRow(frame, ref stack, PhoneLanguages.T("set.fight.hide"), display.Fight == FightPresence.Vanish,
            () => display.Fight = FightPresence.Vanish, PhoneLanguages.T("set.fight.hide.hint"));
    }

    private static string TopicLabel(string title) => title switch
    {
        "General" => PhoneLanguages.T("set.general"),
        "Appearance" => PhoneLanguages.T("set.appearance"),
        "Sounds" => PhoneLanguages.T("set.sounds"),
        "Notifications" => PhoneLanguages.T("set.notifications"),
        "Feed" => PhoneLanguages.T("set.feed"),
        "Phone calls" => PhoneLanguages.T("set.calls"),
        "Languages" or "Language & Time" => PhoneLanguages.T("set.language"),
        "Terms of service" => PhoneLanguages.T("set.tos"),
        "Themes" => PhoneLanguages.T("set.themes"),
        "Display" => PhoneLanguages.T("set.display"),
        "Wallpaper" => PhoneLanguages.T("set.wallpaper"),
        "Dim" => PhoneLanguages.T("set.dim"),
        "Home screen" => PhoneLanguages.T("set.homescreen"),
        "Status bar" => PhoneLanguages.T("set.statusbar"),
        "Interactive tuner" => PhoneLanguages.T("set.tuner"),
        _ => title,
    };

    private static float LanguagePageHeight(in AppletFrame frame) =>
        frame.Units(16f) + frame.Units(8f) + OptionHeight(frame) + frame.Units(8f) + frame.Units(32f) +
        frame.Units(8f) + OptionBand(frame, 5);

    private static float OptionHeight(in AppletFrame frame) => frame.Units(48f);

    private static float OptionBand(in AppletFrame frame, int count)
    {
        if (count <= 0)
        {
            return 0f;
        }

        return count * OptionHeight(frame) + (count - 1) * frame.Units(8f);
    }

    private static float StackRun(in AppletFrame frame, params float[] heights) =>
        StackRun(frame, (IReadOnlyList<float>)heights);

    private static float StackRun(in AppletFrame frame, IReadOnlyList<float> heights)
    {
        if (heights.Count == 0)
        {
            return 0f;
        }

        var gap = frame.Units(8f);
        var total = heights[0];
        for (var index = 1; index < heights.Count; index++)
        {
            total += gap + heights[index];
        }

        return total;
    }

    private void DrawTopic(AppletFrame frame, ref Stack stack, string title, string blurb, string haystack,
        float innerHeight, SheetDraw draw)
    {
        if (!Matches(title, blurb, haystack))
        {
            return;
        }

        DrawSquareCategory(frame, ref stack, title, blurb, innerHeight, draw,
            open: TopicOpen(title), canToggle: listQuery.Trim().Length == 0);
    }

    private void DrawOpenSheet(AppletFrame frame, ref Stack stack, string title, string blurb, float innerHeight,
        SheetDraw draw)
    {
        DrawSquareCategory(frame, ref stack, title, blurb, innerHeight, draw, open: true, canToggle: false);
    }

    private void DrawSquareCategory(AppletFrame frame, ref Stack stack, string title, string blurb, float innerHeight,
        SheetDraw draw, bool open, bool canToggle)
    {
        var gold = frame.Theme.Palette.WarmAccent;
        var headH = OptionHeight(frame);
        var pad = frame.Units(12f);
        var height = headH + (open ? pad + innerHeight + pad : 0f);
        var card = stack.Take(height);
        CardChrome.DrawGold(frame, card);
        var head = card.TopSlice(headH);
        var inner = head.Inset(new Edges(frame.Units(12f), 0f, frame.Units(12f), 0f));
        frame.Text.DrawIn(inner.LeftSlice(inner.Width * 0.7f), TopicLabel(title),
            new TextStyle(FontRole.BodyStrong, frame.Theme.Palette.Ink));
        frame.Text.DrawIn(inner.RightSlice(inner.Width * 0.3f),
            open ? PhoneLanguages.T("ui.close") : PhoneLanguages.T("ui.open"),
            new TextStyle(FontRole.CaptionStrong, gold, TextAlign.Center));
        Note(frame, head, blurb);
        if (frame.Input.ConsumeClick(head))
        {
            if (canToggle)
            {
                if (!unfolded.Add(title))
                {
                    unfolded.Remove(title);
                }
            }
            else if (listQuery.Trim().Length > 0)
            {
                searchPinned = title;
                pendingFocusY = card.Min.Y - frame.Content.Min.Y;
                hasFocusY = true;
            }
        }

        if (!open)
        {
            return;
        }

        var rule = new Rect(new Vector2(card.Min.X + frame.Units(12f), head.Max.Y),
            new Vector2(card.Max.X - frame.Units(12f), head.Max.Y + frame.Theme.Metrics.Hairline));
        frame.Paint.Fill(rule, gold with { W = 0.18f });
        var body = card.Inset(new Edges(pad, headH + frame.Units(2f), pad, pad));
        var rows = new Stack(body, StackAxis.Vertical, frame.Units(8f));
        draw(frame, ref rows);
    }

    private static void DrawChevron(in AppletFrame frame, Rect area, bool down, Vector4 gold)
    {
        var c = area.Center;
        var s = frame.Units(5f);
        var stroke = MathF.Max(1.4f, frame.Units(1.4f));
        if (down)
        {
            frame.Paint.Line(c + new Vector2(-s, -s * 0.35f), c + new Vector2(0f, s * 0.45f), gold, stroke);
            frame.Paint.Line(c + new Vector2(s, -s * 0.35f), c + new Vector2(0f, s * 0.45f), gold, stroke);
            return;
        }

        frame.Paint.Line(c + new Vector2(-s * 0.35f, -s), c + new Vector2(s * 0.45f, 0f), gold, stroke);
        frame.Paint.Line(c + new Vector2(-s * 0.35f, s), c + new Vector2(s * 0.45f, 0f), gold, stroke);
    }

    private bool Matches(string title, string blurb, string haystack = "")
    {
        var needle = listQuery.Trim();
        if (needle.Length == 0)
        {
            return true;
        }

        return title.Contains(needle, StringComparison.OrdinalIgnoreCase) ||
               TopicLabel(title).Contains(needle, StringComparison.OrdinalIgnoreCase) ||
               blurb.Contains(needle, StringComparison.OrdinalIgnoreCase) ||
               haystack.Contains(needle, StringComparison.OrdinalIgnoreCase);
    }

    private void HintLabel(in AppletFrame frame, Rect row, string label, string blurb)
    {
        frame.Text.DrawIn(row, label, new TextStyle(FontRole.Caption, frame.Theme.Palette.InkMuted));
        Note(frame, row, blurb);
    }

    private void Note(in AppletFrame frame, Rect area, string blurb)
    {
        if (blurb.Length > 0 && frame.Input.IsHovering(area))
        {
            hoverHint = blurb;
        }
    }

    private void DrawHoverTip(in AppletFrame frame)
    {
        if (hoverHint.Length == 0)
        {
            return;
        }

        var pointer = frame.Input.Pointer;
        var width = MathF.Min(frame.Content.Width * 0.82f, frame.Text.Measure(hoverHint, FontRole.Caption).X +
            frame.Units(16f));
        var height = frame.Units(28f);
        var left = Math.Clamp(pointer.X - width * 0.35f, frame.Content.Min.X, frame.Content.Max.X - width);
        var top = MathF.Min(pointer.Y + frame.Units(16f), frame.Content.Max.Y - height);
        var tip = Rect.FromSize(new Vector2(left, top), new Vector2(width, height));
        frame.Paint.Fill(tip, frame.Theme.Palette.SurfaceRaised, frame.Units(8f));
        frame.Paint.Stroke(tip, frame.Theme.Palette.WarmAccent with { W = 0.45f }, frame.Units(1f), frame.Units(8f));
        frame.Text.DrawEllipsized(tip.Inset(frame.Units(8f)), hoverHint,
            new TextStyle(FontRole.Caption, frame.Theme.Palette.Ink));
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

    private void PairRow(AppletFrame frame, Rect row, string left, bool leftOn, Action leftTap, string right,
        bool rightOn, Action rightTap, string leftHint = "", string rightHint = "")
    {
        Chip(frame, row.LeftSlice(row.Width * 0.48f), left, leftOn, leftTap, leftHint);
        Chip(frame, row.RightSlice(row.Width * 0.48f), right, rightOn, rightTap, rightHint);
    }

    private void DrawPortCombo(AppletFrame frame, ref Stack stack, string id, string title,
        IReadOnlyList<AudioPort> ports, string defaultId, string currentId, Action<string> set)
    {
        frame.Text.DrawIn(stack.Take(frame.Units(22f)), title,
            new TextStyle(FontRole.CaptionStrong, frame.Theme.Palette.InkMuted));
        var row = stack.Take(OptionHeight(frame));
        var gold = frame.Theme.Palette.WarmAccent;
        frame.Paint.Fill(row, frame.Theme.Palette.SurfaceOverlay, frame.Units(12f));
        frame.Paint.Stroke(row, gold with { W = 0.32f }, frame.Theme.Metrics.Hairline, frame.Units(12f));
        var labels = new string[ports.Count + 1];
        labels[0] = "Windows default";
        var selected = 0;
        for (var index = 0; index < ports.Count; index++)
        {
            var port = ports[index];
            labels[index + 1] = port.Id == defaultId ? port.Label + " · default" : port.Label;
            if (currentId.Length > 0 && string.Equals(currentId, port.Id, StringComparison.Ordinal))
            {
                selected = index + 1;
            }
        }

        var inner = row.Inset(new Edges(frame.Units(8f), frame.Units(6f), frame.Units(8f), frame.Units(6f)));
        var picked = frame.TextField.Combo(id, inner, labels, selected);
        frame.Input.Claim(row);
        if (picked == selected)
        {
            return;
        }

        set(picked == 0 ? string.Empty : ports[picked - 1].Id);
    }

    private void ExclusiveRow(AppletFrame frame, ref Stack stack, string label, bool on, Action activate,
        string blurb)
    {
        ToggleRow(frame, stack.Take(OptionHeight(frame)), label, on, value =>
        {
            if (value)
            {
                activate();
            }
        }, blurb);
    }

    private void ActionRow(AppletFrame frame, ref Stack stack, string label, string blurb, Action tap)
    {
        var row = stack.Take(OptionHeight(frame));
        var gold = frame.Theme.Palette.WarmAccent;
        DrawTuneIcon(frame.Paint, row.LeftSlice(frame.Units(28f)), label, frame.Theme.Palette.Ink);
        var words = row.Inset(new Edges(frame.Units(34f), frame.Units(4f), frame.Units(22f), frame.Units(4f)));
        var titleH = frame.Text.LineHeight(FontRole.BodyStrong);
        frame.Text.DrawEllipsized(words.TopSlice(titleH), label,
            new TextStyle(FontRole.BodyStrong, frame.Theme.Palette.Ink));
        if (blurb.Length > 0)
        {
            frame.Text.DrawEllipsized(words.Inset(new Edges(0f, titleH, 0f, 0f)), blurb,
                new TextStyle(FontRole.Caption, frame.Theme.Palette.InkMuted));
        }

        DrawChevron(frame, row.RightSlice(frame.Units(22f)), down: false, gold);
        Note(frame, row, blurb);
        if (frame.Input.ConsumeClick(row))
        {
            tap();
        }
    }

    private void ToggleRow(in AppletFrame frame, Rect row, string label, bool on, Action<bool> set, string blurb = "")
    {
        var gold = frame.Theme.Palette.WarmAccent;
        var icon = row.LeftSlice(frame.Units(28f));
        DrawTuneIcon(frame.Paint, icon, label, frame.Theme.Palette.Ink);
        var toggle = row.RightSlice(frame.Units(58f));
        var words = row.Inset(new Edges(frame.Units(34f), frame.Units(4f), frame.Units(64f), frame.Units(4f)));
        var titleH = frame.Text.LineHeight(FontRole.BodyStrong);
        frame.Text.DrawEllipsized(words.TopSlice(titleH), label,
            new TextStyle(FontRole.BodyStrong, frame.Theme.Palette.Ink));
        if (blurb.Length > 0)
        {
            frame.Text.DrawEllipsized(words.Inset(new Edges(0f, titleH, 0f, 0f)), blurb,
                new TextStyle(FontRole.Caption, frame.Theme.Palette.InkMuted));
        }

        var rule = new Rect(new Vector2(words.Min.X, row.Max.Y - frame.Theme.Metrics.Hairline),
            new Vector2(toggle.Min.X - frame.Units(6f), row.Max.Y));
        frame.Paint.Fill(rule, frame.Theme.Palette.Separator with { W = 0.55f });
        DrawSwitch(frame, toggle.Inset(new Edges(0f, frame.Units(12f))), on);
        Note(frame, row, blurb);
        if (frame.Input.ConsumeClick(row))
        {
            set(!on);
        }
    }

    private static void DrawSwitch(in AppletFrame frame, Rect area, bool on)
    {
        var gold = frame.Theme.Palette.WarmAccent;
        var height = MathF.Min(area.Height, frame.Units(22f));
        var width = MathF.Min(area.Width, frame.Units(52f));
        var track = Rect.FromSize(new Vector2(area.Max.X - width, area.Center.Y - height * 0.5f),
            new Vector2(width, height));
        var radius = height * 0.5f;
        if (on)
        {
            frame.Paint.Fill(track, gold with { W = 0.92f }, radius);
        }
        else
        {
            frame.Paint.Fill(track, frame.Theme.Palette.SurfaceSunken with { W = 0.82f }, radius);
            frame.Paint.Stroke(track, gold with { W = 0.28f }, frame.Theme.Metrics.Hairline, radius);
        }

        var knob = height * 0.38f;
        var knobX = on ? track.Max.X - knob - frame.Units(4f) : track.Min.X + knob + frame.Units(4f);
        var word = on ? "ON" : "OFF";
        var wordArea = on
            ? track.Inset(new Edges(frame.Units(7f), 0f, height, 0f))
            : track.Inset(new Edges(height, 0f, frame.Units(6f), 0f));
        frame.Text.DrawIn(wordArea, word,
            new TextStyle(FontRole.CaptionStrong, on ? frame.Theme.Palette.AccentInk : frame.Theme.Palette.InkMuted,
                TextAlign.Center));
        frame.Paint.FillCircle(new Vector2(knobX, track.Center.Y), knob,
            on ? new Vector4(0.96f, 0.90f, 0.78f, 1f) : frame.Theme.Palette.InkMuted);
    }

    private static void DrawTuneIcon(IPaintSurface paint, Rect area, string label, Vector4 gold)
    {
        var c = area.Center;
        var s = MathF.Min(area.Width, area.Height) * 0.32f;
        var stroke = MathF.Max(1.2f, s * 0.22f);
        switch (label)
        {
            case "Show world name":
                paint.StrokeCircle(c, s, gold, stroke);
                paint.Line(c + new Vector2(0f, -s), c + new Vector2(0f, s), gold, stroke);
                paint.Line(c + new Vector2(-s, 0f), c + new Vector2(s, 0f), gold, stroke);
                break;
            case "Show status icons":
                paint.Line(c + new Vector2(-s * 0.7f, s * 0.55f), c + new Vector2(-s * 0.7f, -s * 0.1f), gold, stroke);
                paint.Line(c + new Vector2(0f, s * 0.55f), c + new Vector2(0f, -s * 0.35f), gold, stroke);
                paint.Line(c + new Vector2(s * 0.7f, s * 0.55f), c + new Vector2(s * 0.7f, -s * 0.62f), gold, stroke);
                break;
            case "Lock position":
                paint.Line(c + new Vector2(0f, -s), c + new Vector2(0f, s * 0.55f), gold, stroke);
                paint.StrokeCircle(c + new Vector2(0f, -s * 0.35f), s * 0.42f, gold, stroke);
                break;
            case "Show lock button":
                paint.Stroke(Rect.FromSize(c + new Vector2(-s * 0.55f, -s * 0.7f), new Vector2(s * 1.1f, s * 1.4f)),
                    gold, stroke, s * 0.2f);
                paint.Line(c + new Vector2(s * 0.15f, -s * 0.15f), c + new Vector2(s * 0.15f, s * 0.35f), gold, stroke);
                break;
            case "Silent":
            case "Silent in duties":
                paint.StrokeCircle(c, s * 0.72f, gold, stroke);
                paint.StrokeCircle(c + new Vector2(s * 0.12f, 0f), s * 0.38f, gold, stroke);
                break;
            case "Reduce motion":
                paint.StrokeCircle(c, s, gold, stroke);
                paint.Line(c + new Vector2(s * 0.72f, -s * 0.2f), c + new Vector2(s * 1.05f, s * 0.1f), gold, stroke);
                break;
            case "Wake when minimized":
                paint.Stroke(Rect.FromSize(c + new Vector2(-s * 0.5f, -s * 0.75f), new Vector2(s, s * 1.5f)), gold,
                    stroke, s * 0.22f);
                paint.FillCircle(c + new Vector2(0f, s * 0.55f), s * 0.12f, gold);
                break;
            case "Keep open in portraits":
                paint.StrokeCircle(c + new Vector2(0f, -s * 0.28f), s * 0.38f, gold, stroke);
                paint.Stroke(Rect.FromSize(c + new Vector2(-s * 0.62f, s * 0.12f), new Vector2(s * 1.24f, s * 0.7f)),
                    gold, stroke, s * 0.28f);
                break;
            case "Hide during cutscenes":
                paint.Stroke(Rect.FromSize(c + new Vector2(-s * 0.75f, -s * 0.5f), new Vector2(s * 1.5f, s)), gold,
                    stroke, s * 0.16f);
                paint.Line(c + new Vector2(-s * 0.2f, -s * 0.15f), c + new Vector2(s * 0.28f, 0f), gold, stroke);
                paint.Line(c + new Vector2(-s * 0.2f, s * 0.15f), c + new Vector2(s * 0.28f, 0f), gold, stroke);
                break;
            default:
                paint.StrokeCircle(c, s * 0.7f, gold, stroke);
                break;
        }
    }

    private void Chip(AppletFrame frame, Rect area, string label, bool active, Action tap, string blurb = "")
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
        Note(frame, area, blurb);
        if (frame.Input.ConsumeClick(area))
        {
            tap();
        }
    }
}
