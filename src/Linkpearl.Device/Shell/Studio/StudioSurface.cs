using System.Globalization;
using System.Text;
using Linkpearl.Applets;
using Linkpearl.Audio;
using Linkpearl.Badges;
using Linkpearl.Destinations;
using Linkpearl.Destinations.Home;
using Linkpearl.Destinations.Profile;
using Linkpearl.Geometry;
using Linkpearl.Layout;
using Linkpearl.Media;
using Linkpearl.Net;
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
    private float nameClock;

    public StudioSurface(IClock clock, IGameSession game, IPearlHub pearl, ITalk talk, DestinationHub hub,
        IWeatherOracle weather, DisplayPreferences display, bool development, BadgeBook badges, NoticeLedger notices,
        ProfileChrome profile, Action<string, Rect> openApplet, Action<Rect> openRadioStations, IHandsetAudio audio,
        IPublicRadio radio)
    {
        this.clock = clock;
        this.game = game;
        this.pearl = pearl;
        this.talk = talk;
        this.hub = hub;
        this.display = display;
        this.development = development;
        this.badges = badges;
        this.notices = notices;
        this.profile = profile;
        this.openApplet = openApplet;
        sky = new StudioWeather(game, clock, weather);
        musicDock = new StudioMusicDock(audio, radio, pearl, openRadioStations);
    }

    public bool OverlayOpen => profile.OverlayOpen;

    public bool BlocksPager => musicDock.BlocksPager;

    public bool Back() => profile.Back();

    public float Compose(in AppletFrame frame, IApplet? camera)
    {
        var content = frame.Content;
        nameClock += frame.DeltaSeconds;
        var snapshot = pearl.Current;
        badges.Sync(snapshot.SignedIn && snapshot.FounderSeat > 0 && snapshot.FounderSeat <= FounderFaces.SeatLimit,
            game.JobName, development, GlassName.IsPatron(badges, snapshot, display, development));
        if (profile.OverlayOpen)
        {
            return profile.DrawOverlay(frame);
        }

        var dockH = HomeDock.Height(frame);
        var dockLift = frame.Units(14f);
        var inset = content.Inset(new Edges(frame.Units(28f), frame.Units(44f), frame.Units(28f),
            dockH + dockLift + frame.Units(8f)));
        var gap = frame.Units(10f);
        var stack = new Stack(inset, StackAxis.Vertical, gap);
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

        var musicH = StudioMusicDock.Height(frame);
        var huntH = StudioHunt.BarHeight(frame);
        var leftover = MathF.Max(0f, stack.Remaining.Height - musicH - gap - huntH - gap);
        var band = MathF.Max(0f, (leftover - gap * 2f) / 3f);
        var huntBar = stack.Take(huntH);
        var calendar = stack.Take(band);
        var middle = stack.Take(band);
        var weather = stack.Take(band);
        var music = stack.TakeRemaining();
        var half = MathF.Max(0f, (middle.Width - gap) * 0.5f);

        DrawCalendar(frame, calendar);
        DrawAnnouncements(frame, middle.LeftSlice(half));
        DrawApps(frame, middle.RightSlice(half));
        sky.Draw(frame, weather, area => openApplet("weather", area));
        try
        {
            musicDock.Draw(frame, music);
        }
        catch
        {
            // A bad radio frame must not take down search, docks, or soft keys.
        }

        hunt.Draw(frame, huntBar, inset, pearl, talk, hub, openApplet);
        var dock = content.BottomSlice(dockH + dockLift).Inset(new Edges(0f, 0f, 0f, dockLift));
        HomeDock.Draw(frame, dock, camera,
            display.Hushed(game.IsInDuty || game.IsInCutscene),
            () => hub.Open(DestinationTab.Social, SocialPane.Phone),
            () => openApplet("camera", content.BottomSlice(dockH + dockLift)));
        return content.Height;
    }

    // Identity stay on ProfileChrome for social surfaces. This page no longer hosts the card.
    private void DrawGreeting(in AppletFrame frame, Rect row, float calendarRight)
    {
        var snapshot = pearl.Current;
        var linked = ShownName.Linked(game.Character.Name, snapshot.MeName);
        var patron = GlassName.IsPatron(badges, snapshot, display, development);
        var name = GlassName.Resolve(display, linked, patron);
        if (name.Length == 0)
        {
            name = "Linkpearl";
        }

        var title = patron ? ShownName.ClampTitle(display.OwnTitle).Trim() : string.Empty;
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

        var slotGap = frame.Units(6f);
        var buttonGap = frame.Units(12f);
        var calendarGap = frame.Units(8f);
        var slot = MathF.Floor(badgeH);
        var rowY = MathF.Round(cursor + badgeGap);
        var editRight = calendarRight - calendarGap;
        var badgesW = MathF.Min(
            slot * BadgeCatalog.SlotCount + slotGap * (BadgeCatalog.SlotCount - 1),
            MathF.Max(0f, editRight - copyLeft - frame.Units(54f) - buttonGap));
        var badgesRow = Rect.FromSize(new Vector2(copyLeft, rowY), new Vector2(badgesW, slot));
        var edit = new Rect(
            new Vector2(badgesRow.Max.X + buttonGap, rowY),
            new Vector2(editRight, rowY + slot));

        profile.DrawStudioBadges(frame, badgesRow);
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

    private void DrawCalendar(in AppletFrame frame, Rect row)
    {
        StudioChrome.DrawPanel(frame, row);
        var gold = frame.Theme.Palette.WarmAccent;
        var ink = frame.Theme.Palette.Ink;
        var muted = frame.Theme.Palette.InkMuted;
        var now = clock.Now;
        var inset = row.Inset(new Edges(frame.Units(12f), frame.Units(8f), frame.Units(12f), frame.Units(8f)));
        StudioChrome.DrawHeader(frame, inset.TopSlice(frame.Units(14f)), "CALENDAR", true, out var more);
        if (frame.Input.ConsumeClick(more))
        {
            openApplet("calendar", row);
        }

        var body = inset.Inset(new Edges(0f, frame.Units(18f), 0f, 0f));
        var split = body.Width * 0.48f;
        var left = body.LeftSlice(split);
        var right = body.RightSlice(body.Width - split - frame.Units(8f));
        var weekday = now.ToString("dddd", CultureInfo.CurrentCulture).ToUpperInvariant();
        var date = weekday + " " + now.Day.ToString(CultureInfo.InvariantCulture);
        frame.Text.DrawFitted(left.TopSlice(left.Height * 0.58f), date, new TextStyle(FontRole.Title, ink));
        frame.Text.DrawEllipsized(left.BottomSlice(frame.Units(16f)), "No events today.",
            new TextStyle(FontRole.Caption, muted));

        DrawMonth(frame, right, now, gold, ink, muted);
        if (frame.Input.ConsumeClick(row))
        {
            openApplet("calendar", row);
        }
    }

    private static void DrawMonth(in AppletFrame frame, Rect area, DateTimeOffset now, Vector4 gold, Vector4 ink,
        Vector4 muted)
    {
        frame.Text.DrawEllipsized(area.TopSlice(frame.Units(12f)),
            now.ToString("MMMM", CultureInfo.CurrentCulture),
            new TextStyle(FontRole.CaptionStrong, gold, TextAlign.Center, 1f, 0.90f));
        var grid = area.Inset(new Edges(0f, frame.Units(14f), 0f, 0f));
        var cellW = grid.Width / 7f;
        var cellH = grid.Height / 7f;
        var start = now.Date.AddDays(-(int)now.DayOfWeek);
        for (var column = 0; column < 7; column++)
        {
            var head = Rect.FromSize(new Vector2(grid.Min.X + column * cellW, grid.Min.Y), new Vector2(cellW, cellH));
            var label = start.AddDays(column).ToString("ddd", CultureInfo.CurrentCulture);
            frame.Text.DrawIn(head, label.Length > 0 ? label[..1] : "?",
                new TextStyle(FontRole.Caption, muted, TextAlign.Center, 1f, 0.80f));
        }

        var first = new DateTime(now.Year, now.Month, 1);
        var lead = (int)first.DayOfWeek;
        var days = DateTime.DaysInMonth(now.Year, now.Month);
        for (var day = 1; day <= days; day++)
        {
            var index = lead + day - 1;
            var cell = Rect.FromSize(
                new Vector2(grid.Min.X + index % 7 * cellW, grid.Min.Y + (index / 7 + 1) * cellH),
                new Vector2(cellW, cellH));
            var today = day == now.Day;
            if (today)
            {
                frame.Paint.StrokeCircle(cell.Center, MathF.Min(cell.Width, cell.Height) * 0.36f, gold,
                    MathF.Max(1f, frame.Units(1.1f)));
            }

            frame.Text.DrawIn(cell, day.ToString(CultureInfo.InvariantCulture),
                new TextStyle(FontRole.Caption, today ? gold : ink, TextAlign.Center, 1f, 0.86f));
        }
    }

    private void DrawAnnouncements(in AppletFrame frame, Rect area)
    {
        StudioChrome.DrawPanel(frame, area);
        var gold = frame.Theme.Palette.WarmAccent;
        var ink = frame.Theme.Palette.Ink;
        var muted = frame.Theme.Palette.InkMuted;
        var inset = area.Inset(new Edges(frame.Units(12f), frame.Units(8f), frame.Units(12f), frame.Units(8f)));
        StudioChrome.DrawHeader(frame, inset.TopSlice(frame.Units(14f)), "LINKPEARL ANNOUNCEMENTS", false,
            out _);

        var snapshot = pearl.Current;
        var notices = snapshot.Announcements ?? [];
        string title;
        string body;
        if (notices.Length > 0)
        {
            title = notices[0].Title;
            body = notices[0].Body;
        }
        else if (!snapshot.SignedIn)
        {
            title = "Link to Pearlgate";
            body = "Sign in to read the latest notes from Linkpearl.";
        }
        else
        {
            title = "No announcements.";
            body = "When Pearlgate posts, the latest note will sit here.";
        }

        var copy = inset.Inset(new Edges(0f, frame.Units(18f), 0f, 0f));
        var mark = copy.LeftSlice(frame.Units(22f));
        StudioMarks.Draw(frame.Paint, mark.TopSlice(frame.Units(22f)), StudioMark.Mail, gold);
        var text = copy.Inset(new Edges(frame.Units(26f), 0f, 0f, 0f));
        frame.Text.DrawEllipsized(text.TopSlice(frame.Units(16f)), title,
            new TextStyle(FontRole.CaptionStrong, ink));
        if (text.Height > frame.Units(28f))
        {
            frame.Text.DrawWrapped(text.Inset(new Edges(0f, frame.Units(16f), 0f, 0f)), body,
                new TextStyle(FontRole.Caption, muted));
        }

        if (frame.Input.ConsumeClick(area))
        {
            hub.Open(DestinationTab.Home, HomePane.Announcements);
        }
    }

    private void DrawApps(in AppletFrame frame, Rect area)
    {
        var grid = new TileGrid(area, 3, 2, frame.Units(2f));
        DrawApp(frame, grid.Cell(0, 0), "pearlchat", AppIconCatalog.HomeMessagesAsset, "Messages",
            talk.UnreadTotal, () => hub.Open(DestinationTab.Social, SocialPane.Messages));
        DrawApp(frame, grid.Cell(1, 0), "party", AppIconCatalog.HomePartyAsset, "Party", 0,
            () => hub.Open(DestinationTab.Social, SocialPane.Linkshells));
        DrawApp(frame, grid.Cell(2, 0), "friends", string.Empty, "Friends", 0,
            () => hub.Open(DestinationTab.Social, SocialPane.People));
        DrawApp(frame, grid.Cell(0, 1), "retainer", string.Empty, "Retainer", 0,
            () => hub.Open(DestinationTab.You));
        DrawApp(frame, grid.Cell(1, 1), "market", string.Empty, "Market", 0,
            () => hub.Open(DestinationTab.Explore, ExplorePane.Places));
        DrawApp(frame, grid.Cell(2, 1), "events", string.Empty, "Events", 0,
            () => hub.Open(DestinationTab.Explore, ExplorePane.Events));
    }

    private static void DrawApp(in AppletFrame frame, Rect cell, string appletId, string fallbackAsset, string label,
        int badge, Action pressed)
    {
        var gold = frame.Theme.Palette.WarmAccent;
        var caption = cell.BottomSlice(frame.Units(11f));
        var art = new Rect(cell.Min, new Vector2(cell.Max.X, caption.Min.Y - frame.Units(1f)));
        var side = MathF.Min(art.Width, art.Height) * 0.85f;
        var hover = frame.Input.IsHovering(cell);
        var drawn = hover ? side * 1.08f : side;
        var disc = Rect.FromSize(new Vector2(art.Max.X - side, art.Center.Y - side * 0.5f), new Vector2(side));
        var bubble = Rect.FromSize(disc.Center - new Vector2(drawn * 0.5f, drawn * 0.5f), new Vector2(drawn, drawn));
        AppMarks.DrawRoundFace(frame, bubble, appletId, hover);
        _ = fallbackAsset;

        frame.Text.DrawEllipsized(caption.RightSlice(side), label,
            new TextStyle(FontRole.Caption, gold, TextAlign.Center, 1f, 0.78f));
        if (badge > 0)
        {
            var radius = frame.Units(4.6f);
            var center = new Vector2(bubble.Max.X - radius * 0.15f, bubble.Min.Y + radius * 0.15f);
            frame.Paint.FillCircle(center, radius, frame.Theme.Palette.Negative);
            frame.Text.Draw(center, badge > 9 ? "9+" : badge.ToString(CultureInfo.InvariantCulture),
                new TextStyle(FontRole.Caption, Vector4.One, TextAlign.Center, 1f, 0.64f));
        }

        if (frame.Input.ConsumeClick(cell))
        {
            pressed();
        }
    }

}
