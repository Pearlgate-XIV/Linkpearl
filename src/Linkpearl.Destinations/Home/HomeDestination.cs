using System.Globalization;
using Linkpearl.Applets;
using Linkpearl.Badges;
using Linkpearl.Cards;
using Linkpearl.Destinations.Profile;
using Linkpearl.Geometry;
using Linkpearl.Layout;
using Linkpearl.Media;
using Linkpearl.Modules;
using Linkpearl.Net;
using Linkpearl.Painting;
using Linkpearl.Platform;
using Linkpearl.Preferences;
using Linkpearl.Talk;
using Linkpearl.Time;

namespace Linkpearl.Destinations.Home;

public sealed class HomeDestination : IDestinationScreen, ISectionedDestination
{
    private readonly IClock clock;
    private readonly IGameSession game;
    private readonly IPearlHub pearl;
    private readonly ITalk talk;
    private readonly DestinationHub hub;
    private readonly DisplayPreferences display;
    private readonly HostPaths paths;
    private readonly ITextureSource textures;
    private readonly BadgeBook badges;
    private readonly bool development;
    private readonly ProfileChrome profile;
    private readonly AnnouncementShelf announcements;
    private readonly NoticeLedger notices;
    private readonly HomeWeatherCard weather;

    public HomeDestination(IClock clock, IGameSession game, IPearlHub pearl, ITalk talk, DestinationHub hub,
        DisplayPreferences display, HostPaths paths, ITextureSource textures, BadgeBook badges, IFilePicker files,
        IWeatherOracle weather, NoticeLedger notices, bool development, HandsetProfileDesk profiles)
    {
        this.clock = clock;
        this.game = game;
        this.pearl = pearl;
        this.talk = talk;
        this.hub = hub;
        this.display = display;
        this.paths = paths;
        this.textures = textures;
        this.badges = badges;
        this.development = development;
        this.notices = notices;
        profile = new ProfileChrome(badges, paths, textures, files, pearl, game, display, development, profiles);
        announcements = new AnnouncementShelf(pearl, talk, clock, hub, notices);
        this.weather = new HomeWeatherCard(game, clock, weather);
    }

    public ProfileChrome Profile => profile;

    public DestinationTab Tab => DestinationTab.Home;

    public string Glyph => "⌂";

    public string Label => "Home";

    public int CurrentSection => profile.OverlayOpen
        ? HomePane.Profile
        : announcements.IsOpen
            ? HomePane.Announcements
            : HomePane.Dashboard;

    public void ShowSection(int section)
    {
        if (section == HomePane.Announcements)
        {
            announcements.ShowList();
            profile.Close();
            return;
        }

        if (section == HomePane.Profile)
        {
            announcements.Close();
            profile.OpenEdit();
            return;
        }

        announcements.Close();
        profile.Close();
    }

    public void OpenAnnouncement(string announcementId)
    {
        profile.Close();
        announcements.ShowNotice(announcementId);
    }

    public bool CanGoBack => profile.OverlayOpen || announcements.IsOpen;

    public bool Back()
    {
        if (profile.Back())
        {
            return true;
        }

        return announcements.Back();
    }

    public float Compose(in AppletFrame frame)
    {
        if (announcements.IsOpen)
        {
            return announcements.Compose(frame);
        }

        var inset = frame.Units(18f);
        var gap = frame.Units(11f);
        var content = frame.Content.Inset(new Edges(inset, frame.Units(8f), inset, frame.Units(6f)));
        var stack = new Stack(content, StackAxis.Vertical, gap);
        var snapshot = pearl.Current;
        badges.Sync(snapshot.SignedIn && snapshot.FounderSeat > 0 && snapshot.FounderSeat <= FounderFaces.SeatLimit,
            game.JobName, development, GlassName.IsPatron(badges, snapshot, display, development));

        if (profile.OverlayOpen)
        {
            return profile.DrawOverlay(frame);
        }

        var header = stack.Take(frame.Units(162f));
        DrawBanner(frame, new Rect(new Vector2(frame.Content.Min.X, frame.Content.Min.Y),
            new Vector2(frame.Content.Max.X, header.Max.Y)));
        DrawHeader(frame, header, snapshot);

        weather.Draw(frame, CardBand(stack.Take(frame.Units(132f))));
        DrawHero(frame, CardBand(stack.Take(frame.Units(48f))), snapshot);

        var gridBudget = stack.Remaining.Height;
        var rowHeight = MathF.Max(0f, (gridBudget - gap * 3f) / 3f);

        DrawPair(frame, CardBand(stack.Take(rowHeight)), gap, snapshot, isMessagesAndParty: true);
        DrawPair(frame, CardBand(stack.Take(rowHeight)), gap, snapshot, isMessagesAndParty: false);
        DrawMarketAndEvent(frame, CardBand(stack.Take(rowHeight)), gap, snapshot);

        return (content.Height - stack.Remaining.Height) + inset * 2f;
    }

    private void DrawBanner(in AppletFrame frame, Rect area)
    {
        if (!display.UsingBanner || area.Height <= 1f)
        {
            return;
        }

        var texture = textures.FromFile(BannerFiles.Absolute(paths, display.CustomBannerFile));
        if (texture is null || !texture.IsReady)
        {
            return;
        }

        frame.Paint.PushClip(area);
        var crop = CoverFit.Uv(texture.Size, area.Size);
        frame.Paint.Image(texture, area, crop.Min, crop.Max, Vector4.One);
        var wash = frame.Theme.Palette.SurfaceSunken;
        frame.Paint.Fill(area, wash with { W = 0.22f });
        frame.Paint.FillGradient(area.BottomSlice(area.Height * 0.55f), wash with { W = 0.08f },
            wash with { W = 0.62f }, GradientAxis.Vertical);
        frame.Paint.PopClip();
    }

    private void DrawHeader(in AppletFrame frame, Rect row, PearlSnapshot snapshot)
    {
        var gold = frame.Theme.Palette.WarmAccent;
        var hush = display.Hushed(game.IsInDuty || game.IsInCutscene);
        var waiting = this.notices.Count(snapshot, talk, clock);
        HomeHeaderTools.Draw(frame, frame.Content, waiting, hush, out var notice, out var settings);

        if (frame.Input.ConsumeClick(settings))
        {
            hub.Open(DestinationTab.Settings);
        }

        if (frame.Input.ConsumeClick(notice))
        {
            hub.Open(DestinationTab.Home, HomePane.Announcements);
        }

        var name = GlassName.ProfileName(display, ShownName.Linked(game.Character.Name, snapshot.MeName));
        if (name.Length == 0)
        {
            name = "Linkpearl";
        }
        var job = game.JobName.Length > 0 ? TitleCase(game.JobName) : "Warrior of Light";
        var world = snapshot.MeWorld.Length > 0 ? snapshot.MeWorld : game.Character.WorldName;
        if (world.Length == 0)
        {
            world = game.ZoneName;
        }

        var card = row.Inset(new Edges(0f, frame.Units(28f) + frame.Units(4f), 0f, 0f));
        profile.DrawCard(frame, card, name, job, world, game.MapPlace, game.JobIconId);
    }

    private void DrawHero(in AppletFrame frame, Rect row, PearlSnapshot snapshot)
    {
        CardChrome.DrawGold(frame, row);
        var gold = frame.Theme.Palette.WarmAccent;
        var inset = row.Inset(new Edges(frame.Units(12f), frame.Units(8f), frame.Units(12f), frame.Units(8f)));
        DrawAnnouncementMark(frame, inset.LeftSlice(frame.Units(22f)));
        HomeMarks.Draw(frame.Paint, inset.RightSlice(frame.Units(12f)), HomeMark.Chevron, gold with { W = 0.7f });

        var body = inset.Inset(new Edges(frame.Units(28f), 0f, frame.Units(16f), 0f));
        frame.Text.DrawEllipsized(body.TopSlice(frame.Units(14f)), "LINKPEARL ANNOUNCEMENTS",
            new TextStyle(FontRole.CaptionStrong, gold));

        string title;
        var notices = snapshot.Announcements ?? [];
        var latest = notices.Length > 0 ? notices[0] : default(PearlAnnouncement?);
        if (latest is { } notice)
        {
            title = notice.Title;
        }
        else if (!snapshot.SignedIn)
        {
            title = LinkPearlgate;
        }
        else
        {
            title = "No announcements.";
        }

        frame.Text.DrawEllipsized(body.BottomSlice(frame.Units(14f)), title,
            new TextStyle(FontRole.Caption, frame.Theme.Palette.Ink));

        if (frame.Input.ConsumeClick(row))
        {
            hub.Open(DestinationTab.Home, HomePane.Announcements);
        }
    }

    private void DrawAnnouncementMark(in AppletFrame frame, Rect area)
    {
        HomeMarks.Draw(frame, area, HomeMark.Mask, frame.Theme.Palette.WarmAccent);
    }

    // 5% narrower than the glass, split across both sides so the gutter between pairs stays put.
    private static Rect CardBand(Rect row) => row.Inset(new Edges(row.Width * 0.025f, 0f));

    private void DrawPair(in AppletFrame frame, Rect row, float gap, PearlSnapshot snapshot, bool isMessagesAndParty)
    {
        var half = (row.Width - gap) * 0.5f;
        if (isMessagesAndParty)
        {
            DrawMessages(frame, row.LeftSlice(half), snapshot);
            DrawParty(frame, row.RightSlice(half));
        }
        else
        {
            DrawRetainer(frame, row.LeftSlice(half), snapshot);
            DrawPeople(frame, row.RightSlice(half), snapshot);
        }
    }

    private const string LinkPearlgate = "Link to Pearlgate";

    private void DrawMarketAndEvent(in AppletFrame frame, Rect row, float gap, PearlSnapshot snapshot)
    {
        var half = (row.Width - gap) * 0.5f;
        DrawOffCard(frame, row.LeftSlice(half), HomeMark.Market, "MARKET", LinkPearlgate,
            string.Empty, snapshot.SignedIn ? DestinationTab.Explore : DestinationTab.You,
            snapshot.SignedIn ? ExplorePane.Places : 0);
        if (snapshot.SignedIn && snapshot.Stories.Length > 0)
        {
            var story = FirstUnseenOrFirst(snapshot);
            DrawWidget(frame, row.RightSlice(half), HomeMark.Event, "EVENTS", story.AuthorName,
                story.HasUnseen ? "New story" : "Story", story.HasUnseen ? 1 : 0, DestinationTab.Social,
                SocialPane.Feed);
            return;
        }

        DrawOffCard(frame, row.RightSlice(half), HomeMark.Event, "EVENTS", LinkPearlgate,
            string.Empty, snapshot.SignedIn ? DestinationTab.Explore : DestinationTab.You,
            snapshot.SignedIn ? ExplorePane.Events : 0);
    }

    private void DrawMessages(in AppletFrame frame, Rect area, PearlSnapshot snapshot)
    {
        var received = LatestReceived(4);
        if (received.Count == 0)
        {
            DrawWidget(frame, area, HomeMark.Messages, "MESSAGES",
                snapshot.SignedIn ? "No chats yet" : LinkPearlgate, string.Empty,
                0, snapshot.SignedIn ? DestinationTab.Social : DestinationTab.You,
                snapshot.SignedIn ? SocialPane.Messages : 0);
            return;
        }

        var body = DrawGoldHeader(frame, area, HomeMark.Messages, "MESSAGES", string.Empty);
        if (talk.UnreadTotal > 0)
        {
            DrawBadge(frame, area, talk.UnreadTotal, frame.Theme.Palette.WarmAccent);
        }

        if (frame.Input.ConsumeClick(area.TopSlice(body.Min.Y - area.Min.Y)))
        {
            hub.Open(DestinationTab.Social, SocialPane.Messages);
        }

        var stack = new Stack(body, StackAxis.Vertical, frame.Units(5f));
        var rowH = PreviewRowHeight(frame);
        for (var index = 0; index < received.Count && stack.Remaining.Height >= rowH; index++)
        {
            var (thread, line) = received[index];
            var name = line.Sender.Length > 0 ? line.Sender : thread.Title;
            if (DrawPreviewRow(frame, stack.Take(rowH), name, line.Body))
            {
                OpenMessagePerson(thread);
            }
        }

        if (stack.Remaining.Height > 1f && frame.Input.ConsumeClick(stack.Remaining))
        {
            hub.Open(DestinationTab.Social, SocialPane.Messages);
        }
    }

    private void OpenMessagePerson(TalkThread thread)
    {
        if (thread.Kind == TalkKind.Tell)
        {
            hub.OpenProfile(thread.Id);
            return;
        }

        hub.OpenTalk(thread.Id);
    }

    private List<(TalkThread Thread, TalkLine Line)> LatestReceived(int max)
    {
        var hits = new List<(TalkThread, TalkLine)>(max);
        var inbox = talk.Inbox();
        var scanned = 0;
        for (var index = 0; index < inbox.Count && hits.Count < max && scanned < 16; index++)
        {
            var thread = inbox[index];
            scanned++;
            if (LastIncoming(thread.Id) is not TalkLine line)
            {
                continue;
            }

            hits.Add((thread, line));
        }

        return hits;
    }

    private TalkLine? LastIncoming(string threadId)
    {
        var lines = talk.Lines(threadId);
        for (var index = lines.Count - 1; index >= 0; index--)
        {
            if (!lines[index].Mine && lines[index].Body.Length > 0)
            {
                return lines[index];
            }
        }

        return null;
    }

    private void DrawParty(in AppletFrame frame, Rect area)
    {
        var count = game.IsInParty
            ? game.PartySize.ToString(CultureInfo.InvariantCulture) + "/8"
            : string.Empty;
        if (game.IsInParty)
        {
            var zone = game.ZoneName.Length > 0 ? game.ZoneName : "Party chat";
            DrawWidget(frame, area, HomeMark.Party, "PARTY", game.IsInDuty ? "In duty" : "In party", zone, 0,
                DestinationTab.Social, SocialPane.Linkshells, TalkIds.Party, meta: count, members: game.PartySize);
            return;
        }

        DrawWidget(frame, area, HomeMark.Party, "PARTY", LinkPearlgate, string.Empty, 0,
            DestinationTab.Social, SocialPane.Linkshells, TalkIds.Party);
    }

    private void DrawRetainer(in AppletFrame frame, Rect area, PearlSnapshot snapshot)
    {
        if (!snapshot.SignedIn)
        {
            DrawOffCard(frame, area, HomeMark.Retainer, "RETAINER", LinkPearlgate, string.Empty,
                DestinationTab.You, 0);
            return;
        }

        if (snapshot.Retainers.Length == 0)
        {
            DrawOffCard(frame, area, HomeMark.Retainer, "RETAINER", "No retainers yet", string.Empty,
                DestinationTab.You, 0);
            return;
        }

        var retainer = snapshot.Retainers[0];
        var gil = retainer.Gil.ToString("N0", CultureInfo.InvariantCulture) + " gil";
        var stock = retainer.ItemsOnSale == 1
            ? "1 on sale"
            : retainer.ItemsOnSale.ToString(CultureInfo.InvariantCulture) + " on sale";
        DrawWidget(frame, area, HomeMark.Retainer, "RETAINER", retainer.Name, gil + " · " + stock, 0,
            DestinationTab.You, 0);
    }

    private void DrawPeople(in AppletFrame frame, Rect area, PearlSnapshot snapshot)
    {
        var roster = talk.Friends();
        var online = new List<GameFriend>();
        for (var index = 0; index < roster.Count; index++)
        {
            if (roster[index].Online)
            {
                online.Add(roster[index]);
            }
        }

        if (online.Count == 0)
        {
            var gate = snapshot.SignedIn ? snapshot.People.Length : 0;
            if (gate > 0)
            {
                DrawWidget(frame, area, HomeMark.Friends, "FRIENDS ONLINE",
                    "None in Eorzea", "On Pearlgate", 0,
                    DestinationTab.Social, SocialPane.People, people: snapshot);
                return;
            }

            if (roster.Count > 0)
            {
                DrawWidget(frame, area, HomeMark.Friends, "FRIENDS ONLINE",
                    "None online", string.Empty, 0, DestinationTab.Social, SocialPane.People);
                return;
            }

            DrawWidget(frame, area, HomeMark.Friends, "FRIENDS ONLINE",
                "No friends yet", string.Empty, 0, DestinationTab.Social, SocialPane.People);
            return;
        }

        var count = online.Count.ToString(CultureInfo.InvariantCulture);
        var body = DrawGoldHeader(frame, area, HomeMark.Friends, "FRIENDS ONLINE", count);
        if (frame.Input.ConsumeClick(area.TopSlice(body.Min.Y - area.Min.Y)))
        {
            hub.Open(DestinationTab.Social, SocialPane.People);
        }

        var stack = new Stack(body, StackAxis.Vertical, frame.Units(5f));
        var rowH = PreviewRowHeight(frame);
        for (var index = 0; index < online.Count && stack.Remaining.Height >= rowH; index++)
        {
            var friend = online[index];
            var where = friend.Place.Length > 0 ? friend.Place : friend.World;
            if (DrawPreviewRow(frame, stack.Take(rowH), friend.Name, where))
            {
                hub.OpenProfile(TalkIds.Tell(friend.Name, friend.World));
            }
        }

        if (stack.Remaining.Height > 1f && frame.Input.ConsumeClick(stack.Remaining))
        {
            hub.Open(DestinationTab.Social, SocialPane.People);
        }
    }

    private void DrawOffCard(in AppletFrame frame, Rect area, HomeMark mark, string kicker, string title,
        string detail, DestinationTab tab, int pane) =>
        DrawWidget(frame, area, mark, kicker, title, detail, 0, tab, pane);

    private static Rect DrawGoldHeader(in AppletFrame frame, Rect area, HomeMark mark, string kicker, string meta)
    {
        CardChrome.DrawGold(frame, area);
        var gold = frame.Theme.Palette.WarmAccent;
        var inset = area.Inset(new Edges(frame.Units(10f), frame.Units(7f), frame.Units(10f), frame.Units(7f)));
        var headerH = MathF.Max(frame.Text.LineHeight(FontRole.CaptionStrong), frame.Units(13f));
        var stack = new Stack(inset, StackAxis.Vertical, frame.Units(5f));
        var header = stack.Take(headerH);
        HomeMarks.Draw(frame, header.LeftSlice(frame.Units(14f)), mark, gold);
        HomeMarks.Draw(frame.Paint, header.RightSlice(frame.Units(11f)), HomeMark.Chevron, gold with { W = 0.65f });
        var kickerArea = header.Inset(new Edges(frame.Units(18f), 0f, frame.Units(13f), 0f));
        if (meta.Length > 0)
        {
            var metaWidth = frame.Text.Measure(meta, FontRole.Caption).X + frame.Units(4f);
            frame.Text.DrawIn(kickerArea.RightSlice(metaWidth), meta,
                new TextStyle(FontRole.Caption, frame.Theme.Palette.InkMuted, TextAlign.Right));
            kickerArea = kickerArea.Inset(new Edges(0f, 0f, metaWidth, 0f));
        }

        frame.Text.DrawEllipsized(kickerArea, kicker, new TextStyle(FontRole.CaptionStrong, gold));
        return stack.Remaining;
    }

    private static float PreviewRowHeight(in AppletFrame frame) =>
        frame.Text.LineHeight(FontRole.CaptionStrong) + frame.Units(1f) +
        frame.Text.LineHeight(FontRole.Caption);

    private static bool DrawPreviewRow(in AppletFrame frame, Rect row, string title, string detail)
    {
        var nameH = frame.Text.LineHeight(FontRole.CaptionStrong);
        frame.Text.DrawEllipsized(row.TopSlice(nameH), title,
            new TextStyle(FontRole.CaptionStrong, frame.Theme.Palette.Ink));
        frame.Text.DrawEllipsized(row.Inset(new Edges(0f, nameH + frame.Units(1f), 0f, 0f)), detail,
            new TextStyle(FontRole.Caption, frame.Theme.Palette.InkMuted));
        return frame.Input.ConsumeClick(row);
    }

    private void DrawWidget(in AppletFrame frame, Rect area, HomeMark mark, string kicker, string title,
        string detail, int badge, DestinationTab tab, int pane, string talkId = "", string avatarName = "",
        string when = "", int members = 0, PearlSnapshot? people = null, string meta = "")
    {
        CardChrome.DrawGold(frame, area);
        var gold = frame.Theme.Palette.WarmAccent;
        var inset = area.Inset(frame.Units(12f));
        var header = inset.TopSlice(frame.Units(18f));
        var icon = header.LeftSlice(frame.Units(18f));
        HomeMarks.Draw(frame, icon, mark, gold);
        var chevron = header.RightSlice(frame.Units(12f));
        HomeMarks.Draw(frame.Paint, chevron, HomeMark.Chevron, gold with { W = 0.65f });
        var kickerArea = header.Inset(new Edges(frame.Units(22f), 0f, frame.Units(14f), 0f));
        if (meta.Length > 0)
        {
            var metaWidth = frame.Text.Measure(meta, FontRole.Caption).X + frame.Units(4f);
            frame.Text.DrawIn(kickerArea.RightSlice(metaWidth), meta,
                new TextStyle(FontRole.Caption, frame.Theme.Palette.InkMuted, TextAlign.Right));
            kickerArea = kickerArea.Inset(new Edges(0f, 0f, metaWidth, 0f));
        }

        frame.Text.DrawEllipsized(kickerArea, kicker, new TextStyle(FontRole.CaptionStrong, gold));
        if (badge > 0)
        {
            DrawBadge(frame, area, badge, gold);
        }

        var body = inset.Inset(new Edges(0f, frame.Units(20f), 0f, 0f));
        if (title.Length > 0)
        {
            frame.Text.DrawEllipsized(body.TopSlice(frame.Units(14f)), title,
                new TextStyle(FontRole.CaptionStrong, frame.Theme.Palette.Ink));
        }

        if (detail.Length > 0)
        {
            var detailBottom = members > 0 || people is not null
                ? frame.Units(16f)
                : 0f;
            frame.Text.DrawEllipsized(body.Inset(new Edges(0f, frame.Units(15f), 0f, detailBottom)), detail,
                new TextStyle(FontRole.Caption, frame.Theme.Palette.InkMuted));
        }

        if (members > 0)
        {
            DrawMemberPips(frame, body.BottomSlice(frame.Units(14f)), members);
        }
        else if (people is { } snapshot)
        {
            DrawAvatars(frame, body.BottomSlice(frame.Units(18f)), snapshot, 4);
        }

        if (frame.Input.ConsumeClick(area))
        {
            OpenCard(tab, pane, talkId);
        }
    }

    private void OpenCard(DestinationTab tab, int pane, string talkId)
    {
        if (talkId.Length > 0)
        {
            hub.OpenTalk(talkId);
            return;
        }

        hub.Open(tab, pane);
    }

    private static void DrawInitial(in AppletFrame frame, Rect area, string name)
    {
        var given = GivenName(name);
        frame.Paint.FillCircle(area.Center, area.Width * 0.42f, frame.Theme.Palette.SurfaceRaised);
        frame.Paint.StrokeCircle(area.Center, area.Width * 0.42f, frame.Theme.Palette.WarmAccent, frame.Units(1.2f));
        frame.Text.DrawIn(area, given.Length > 0 ? given[..1] : "?",
            new TextStyle(FontRole.CaptionStrong, frame.Theme.Palette.Ink, TextAlign.Center));
    }

    private static void DrawAvatars(in AppletFrame frame, Rect row, PearlSnapshot snapshot, int max)
    {
        var shown = Math.Min(snapshot.People.Length, max);
        for (var index = 0; index < shown; index++)
        {
            var cell = Rect.FromSize(new Vector2(row.Min.X + index * frame.Units(14f), row.Min.Y),
                new Vector2(frame.Units(16f), row.Height));
            DrawInitial(frame, cell, snapshot.People[index].DisplayName);
        }
    }

    private static void DrawMemberPips(in AppletFrame frame, Rect row, int members)
    {
        var shown = Math.Min(Math.Max(members, 0), 8);
        var colors = new[]
        {
            new Vector4(0.45f, 0.62f, 0.98f, 1f),
            new Vector4(0.30f, 0.78f, 0.53f, 1f),
            new Vector4(0.91f, 0.38f, 0.42f, 1f),
            new Vector4(0.72f, 0.52f, 0.96f, 1f),
        };
        for (var index = 0; index < shown; index++)
        {
            var center = new Vector2(row.Min.X + frame.Units(6f) + index * frame.Units(12f), row.Center.Y);
            frame.Paint.Fill(Rect.FromSize(center - new Vector2(frame.Units(4f), frame.Units(4f)),
                new Vector2(frame.Units(8f), frame.Units(8f))), colors[index % colors.Length], frame.Units(2f));
        }
    }

    private static void DrawBadge(in AppletFrame frame, Rect host, int count, Vector4 fill)
    {
        var radius = frame.Units(7f);
        var center = new Vector2(host.Max.X - radius - frame.Units(4f), host.Min.Y + radius + frame.Units(4f));
        frame.Paint.FillCircle(center, radius, fill);
        var label = count > 9 ? "9+" : count.ToString(CultureInfo.InvariantCulture);
        frame.Text.Draw(center, label,
            new TextStyle(FontRole.Caption, frame.Theme.Palette.SurfaceSunken, TextAlign.Center));
    }

    private static string TitleCase(string value)
    {
        if (value.Length == 0)
        {
            return value;
        }

        return char.ToUpper(value[0], CultureInfo.InvariantCulture) + value[1..];
    }

    private static ReadOnlySpan<char> GivenName(string name)
    {
        var space = name.IndexOf(' ');
        return space < 0 ? name.AsSpan() : name.AsSpan(0, space);
    }

    private static PearlStory FirstUnseenOrFirst(PearlSnapshot snapshot)
    {
        for (var index = 0; index < snapshot.Stories.Length; index++)
        {
            if (snapshot.Stories[index].HasUnseen)
            {
                return snapshot.Stories[index];
            }
        }

        return snapshot.Stories[0];
    }
}
