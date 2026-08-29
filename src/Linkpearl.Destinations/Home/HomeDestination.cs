using System.Globalization;
using Linkpearl.Applets;
using Linkpearl.Cards;
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
    private readonly AnnouncementShelf announcements;

    public HomeDestination(IClock clock, IGameSession game, IPearlHub pearl, ITalk talk, DestinationHub hub,
        DisplayPreferences display, HostPaths paths, ITextureSource textures)
    {
        this.clock = clock;
        this.game = game;
        this.pearl = pearl;
        this.talk = talk;
        this.hub = hub;
        this.display = display;
        this.paths = paths;
        this.textures = textures;
        announcements = new AnnouncementShelf(pearl, clock);
    }

    public DestinationTab Tab => DestinationTab.Home;

    public string Glyph => "⌂";

    public string Label => "Home";

    public int CurrentSection => announcements.IsOpen ? HomePane.Announcements : HomePane.Dashboard;

    public void ShowSection(int section)
    {
        if (section == HomePane.Announcements)
        {
            announcements.ShowList();
            return;
        }

        announcements.Close();
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

        var heroHeight = MathF.Max(frame.Units(148f), frame.Content.Height * 0.28f);
        var header = stack.Take(heroHeight);
        DrawBanner(frame, new Rect(new Vector2(frame.Content.Min.X, frame.Content.Min.Y),
            new Vector2(frame.Content.Max.X, header.Max.Y)));
        DrawHeader(frame, header, snapshot);

        var featured = MathF.Max(frame.Units(100f), frame.Content.Height * 0.155f);
        DrawHero(frame, CardBand(stack.Take(featured)), snapshot);

        var gridBudget = stack.Remaining.Height;
        var packed = gridBudget > gap * 2f
            ? (gridBudget - gap * 2f) / 3f
            : frame.Units(96f);
        var rowHeight = MathF.Max(packed * 0.8f, frame.Units(74f));

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
        var ink = frame.Theme.Palette.Ink;

        var bell = row.TopSlice(frame.Units(24f)).RightSlice(frame.Units(26f));
        HomeMarks.Draw(frame.Paint, bell, HomeMark.Bell, gold with { W = 0.82f });
        if (talk.UnreadTotal > 0 && !display.Hushed(game.IsInDuty || game.IsInCutscene))
        {
            DrawBadge(frame, bell, talk.UnreadTotal, gold);
        }

        if (frame.Input.ConsumeClick(bell))
        {
            hub.Open(DestinationTab.Social, SocialPane.Messages);
        }

        var fullName = DisplayName(snapshot);
        var given = GivenName(fullName);
        var hour = clock.Now.Hour;
        var greet = hour < 12 ? "Good morning," : hour < 18 ? "Good afternoon," : "Good evening,";
        var text = row.Inset(new Edges(0f, frame.Units(2f), frame.Units(32f), frame.Units(4f)));
        var cursor = new Stack(text, StackAxis.Vertical, frame.Units(5f));

        frame.Text.DrawIn(cursor.Take(frame.Units(18f)), greet,
            new TextStyle(FontRole.Caption, frame.Theme.Palette.InkMuted));

        var nameRow = cursor.Take(frame.Units(46f));
        frame.Text.DrawEllipsized(nameRow, given, new TextStyle(FontRole.Display, ink));
        if (frame.Input.ConsumeClick(nameRow))
        {
            hub.Open(DestinationTab.You);
        }

        var job = game.JobName.Length > 0 ? TitleCase(game.JobName) : "Warrior of Light";
        var jobRow = cursor.Take(frame.Units(20f));
        var gem = jobRow.LeftSlice(frame.Units(16f));
        HomeMarks.Draw(frame.Paint, gem, HomeMark.Diamond, gold);
        var jobText = jobRow.Inset(new Edges(frame.Units(20f), 0f, 0f, 0f));
        frame.Text.DrawEllipsized(jobText, job, new TextStyle(FontRole.CaptionStrong, gold));

        var jobWidth = frame.Text.Measure(job, FontRole.CaptionStrong).X + frame.Units(20f);
        var rule = cursor.Take(frame.Units(6f));
        var ruleEnd = MathF.Min(jobWidth + frame.Units(8f), rule.Width);
        frame.Paint.Line(new Vector2(rule.Min.X, rule.Center.Y),
            new Vector2(rule.Min.X + ruleEnd, rule.Center.Y), gold with { W = 0.55f }, frame.Units(1f));

        var weather = game.WeatherName;
        if (weather.Length > 0)
        {
            DrawMeta(frame, cursor.Take(frame.Units(18f)), WeatherGlyph(weather), weather, ink);
        }

        var place = game.ZoneName;
        if (place.Length == 0)
        {
            place = snapshot.MeWorld.Length > 0 ? snapshot.MeWorld : game.Character.WorldName;
        }

        if (place.Length > 0)
        {
            DrawMeta(frame, cursor.Take(frame.Units(18f)), HomeMark.Place, place, ink);
        }
    }

    private void DrawHero(in AppletFrame frame, Rect row, PearlSnapshot snapshot)
    {
        CardChrome.DrawGold(frame, row);
        frame.Paint.Glow(row.LeftSlice(frame.Units(6f)), frame.Theme.Palette.Accent with { W = 0.20f },
            frame.Units(12f), frame.Units(8f));
        var gold = frame.Theme.Palette.WarmAccent;
        var inset = row.Inset(frame.Units(14f));
        HomeMarks.Draw(frame.Paint, inset.RightSlice(frame.Units(16f)), HomeMark.Chevron, gold with { W = 0.7f });

        var icon = inset.LeftSlice(frame.Units(48f));
        frame.Paint.FillCircle(icon.Center, frame.Units(20f), new Vector4(0.28f, 0.12f, 0.16f, 0.9f));
        frame.Paint.StrokeCircle(icon.Center, frame.Units(20f), gold, frame.Units(1.4f));
        HomeMarks.Draw(frame.Paint, icon, HomeMark.Mask, frame.Theme.Palette.Ink);

        var body = inset.Inset(new Edges(frame.Units(56f), 0f, frame.Units(18f), 0f));
        frame.Text.DrawEllipsized(body.TopSlice(frame.Units(16f)), "LINKPEARL ANNOUNCEMENTS",
            new TextStyle(FontRole.CaptionStrong, gold));

        string title;
        string detail;
        string when;
        var latest = snapshot.Announcements.Length > 0 ? snapshot.Announcements[0] : default(PearlAnnouncement?);
        if (latest is { } notice)
        {
            title = notice.Title;
            detail = AnnouncementShelf.Snippet(notice.Body);
            when = UnixAgo.Format(notice.CreatedAtUnix, clock.Now);
        }
        else if (!snapshot.SignedIn)
        {
            title = "Sign in from You";
            detail = "Announcements load after Pearlgate.";
            when = string.Empty;
        }
        else
        {
            title = "No announcements";
            detail = "Notices from Linkpearl will sit here when they exist.";
            when = string.Empty;
        }

        frame.Text.DrawEllipsized(body.Inset(new Edges(0f, frame.Units(20f), 0f, frame.Units(36f))), title,
            new TextStyle(FontRole.Title, frame.Theme.Palette.Ink));
        frame.Text.DrawEllipsized(body.Inset(new Edges(0f, frame.Units(44f), 0f, frame.Units(18f))), detail,
            new TextStyle(FontRole.Caption, frame.Theme.Palette.InkMuted));
        if (when.Length > 0)
        {
            frame.Text.DrawIn(body.BottomSlice(frame.Units(16f)), when,
                new TextStyle(FontRole.Caption, frame.Theme.Palette.InkFaint));
        }

        if (frame.Input.ConsumeClick(row))
        {
            hub.Open(DestinationTab.Home, HomePane.Announcements);
        }
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
            DrawRetainer(frame, row.LeftSlice(half));
            DrawPeople(frame, row.RightSlice(half), snapshot);
        }
    }

    private void DrawMarketAndEvent(in AppletFrame frame, Rect row, float gap, PearlSnapshot snapshot)
    {
        var half = (row.Width - gap) * 0.5f;
        DrawOffCard(frame, row.LeftSlice(half), HomeMark.Market, "MARKET WATCH", "Not on Pearlgate",
            "Yellow Pages is off.", DestinationTab.Explore, ExplorePane.Places);
        if (snapshot.Stories.Length > 0)
        {
            var story = FirstUnseenOrFirst(snapshot);
            DrawWidget(frame, row.RightSlice(half), HomeMark.Event, "EVENT STARTING", story.AuthorName,
                story.HasUnseen ? "New story" : "Story", story.HasUnseen ? 1 : 0, DestinationTab.Social,
                SocialPane.Feed);
        }
        else
        {
            DrawOffCard(frame, row.RightSlice(half), HomeMark.Event, "EVENT STARTING", "Not on Pearlgate",
                "Muster is off.", DestinationTab.Explore, ExplorePane.Events);
        }
    }

    private void DrawMessages(in AppletFrame frame, Rect area, PearlSnapshot snapshot)
    {
        var latest = FirstActive(talk.Inbox());
        if (latest is { } thread)
        {
            DrawWidget(frame, area, HomeMark.Messages, "MESSAGES", thread.Title, thread.Preview, talk.UnreadTotal,
                DestinationTab.Social, SocialPane.Messages, thread.Id, thread.Title, UnixAgo.Format(thread.LastAt, clock.Now));
            return;
        }

        DrawWidget(frame, area, HomeMark.Messages, "MESSAGES", "Ready to talk",
            snapshot.SignedIn ? "Party, tells, and linkshells." : "Type here instead of the chat box.",
            0, DestinationTab.Social, SocialPane.Messages);
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

        DrawWidget(frame, area, HomeMark.Party, "PARTY", "No party", "Opens party chat when you join one.", 0,
            DestinationTab.Social, SocialPane.Linkshells, TalkIds.Party);
    }

    private void DrawRetainer(in AppletFrame frame, Rect area) =>
        DrawOffCard(frame, area, HomeMark.Retainer, "RETAINER", "Not wired", "Retainers stay in the game client.",
            DestinationTab.You, 0);

    private void DrawPeople(in AppletFrame frame, Rect area, PearlSnapshot snapshot)
    {
        var count = snapshot.SignedIn ? snapshot.People.Length : 0;
        var title = count > 0 ? count.ToString(CultureInfo.InvariantCulture) : "0";
        DrawWidget(frame, area, HomeMark.Friends, "FRIENDS ONLINE", title,
            snapshot.SignedIn ? "On Pearlgate" : "Sign in from You", 0,
            snapshot.SignedIn ? DestinationTab.Social : DestinationTab.You,
            snapshot.SignedIn ? SocialPane.People : 0, people: snapshot);
    }

    private void DrawOffCard(in AppletFrame frame, Rect area, HomeMark mark, string kicker, string title,
        string detail, DestinationTab tab, int pane) =>
        DrawWidget(frame, area, mark, kicker, title, detail, 0, tab, pane);

    private void DrawWidget(in AppletFrame frame, Rect area, HomeMark mark, string kicker, string title,
        string detail, int badge, DestinationTab tab, int pane, string talkId = "", string avatarName = "",
        string when = "", int members = 0, PearlSnapshot? people = null, string meta = "")
    {
        CardChrome.DrawGold(frame, area);
        var gold = frame.Theme.Palette.WarmAccent;
        var inset = area.Inset(frame.Units(12f));
        var header = inset.TopSlice(frame.Units(16f));
        var icon = header.LeftSlice(frame.Units(16f));
        HomeMarks.Draw(frame.Paint, icon, mark, gold);
        var chevron = header.RightSlice(frame.Units(12f));
        HomeMarks.Draw(frame.Paint, chevron, HomeMark.Chevron, gold with { W = 0.65f });
        var kickerArea = header.Inset(new Edges(frame.Units(20f), 0f, frame.Units(14f), 0f));
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
        if (avatarName.Length > 0)
        {
            var avatar = body.TopSlice(frame.Units(28f)).LeftSlice(frame.Units(28f));
            DrawInitial(frame, avatar, avatarName);
            var copy = body.Inset(new Edges(frame.Units(32f), 0f, 0f, 0f));
            var nameRow = copy.TopSlice(frame.Units(16f));
            if (when.Length > 0)
            {
                var whenWidth = frame.Text.Measure(when, FontRole.Caption).X + frame.Units(2f);
                frame.Text.DrawIn(nameRow.RightSlice(whenWidth), when,
                    new TextStyle(FontRole.Caption, frame.Theme.Palette.InkFaint, TextAlign.Right));
                nameRow = nameRow.Inset(new Edges(0f, 0f, whenWidth, 0f));
            }

            frame.Text.DrawEllipsized(nameRow, title,
                new TextStyle(FontRole.BodyStrong, frame.Theme.Palette.Ink));
            frame.Text.DrawEllipsized(copy.Inset(new Edges(0f, frame.Units(16f), 0f, 0f)), detail,
                new TextStyle(FontRole.Caption, frame.Theme.Palette.InkMuted));
        }
        else
        {
            var friendsLive = mark == HomeMark.Friends && people is { SignedIn: true } &&
                !title.Equals("0", StringComparison.Ordinal);
            var titleColor = title.Equals("Not wired", StringComparison.Ordinal) ||
                title.Equals("Not on Pearlgate", StringComparison.Ordinal)
                ? frame.Theme.Palette.InkMuted
                : friendsLive
                    ? frame.Theme.Palette.Positive
                    : frame.Theme.Palette.Ink;
            frame.Text.DrawEllipsized(body.TopSlice(frame.Units(20f)), title,
                new TextStyle(FontRole.BodyStrong, titleColor));
            frame.Text.DrawEllipsized(body.Inset(new Edges(0f, frame.Units(20f), 0f, frame.Units(18f))), detail,
                new TextStyle(FontRole.Caption, frame.Theme.Palette.InkMuted));
            if (members > 0)
            {
                DrawMemberPips(frame, body.BottomSlice(frame.Units(14f)), members);
            }
            else if (people is { } snapshot)
            {
                DrawAvatars(frame, body.BottomSlice(frame.Units(18f)), snapshot, 4);
            }
            else if (mark == HomeMark.Retainer)
            {
                HomeMarks.Draw(frame.Paint, body.BottomSlice(frame.Units(22f)).RightSlice(frame.Units(28f)),
                    HomeMark.Pouch, gold);
            }
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

    private static void DrawMeta(in AppletFrame frame, Rect row, HomeMark mark, string label, Vector4 ink)
    {
        HomeMarks.Draw(frame.Paint, row.LeftSlice(frame.Units(14f)), mark, ink with { W = 0.92f });
        frame.Text.DrawEllipsized(row.Inset(new Edges(frame.Units(18f), 0f, 0f, 0f)), label,
            new TextStyle(FontRole.Caption, ink));
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

    private string DisplayName(PearlSnapshot snapshot)
    {
        if (snapshot.MeName.Length > 0)
        {
            return snapshot.MeName;
        }

        return game.Character.Name.Length > 0 ? game.Character.Name : "Linkpearl";
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

    private HomeMark WeatherGlyph(string weather)
    {
        if (Contains(weather, "fog") || Contains(weather, "cloud") || Contains(weather, "overcast") ||
            Contains(weather, "gloom") || Contains(weather, "dust") || Contains(weather, "mist"))
        {
            return HomeMark.Cloud;
        }

        if (Contains(weather, "rain") || Contains(weather, "shower") || Contains(weather, "thunder") ||
            Contains(weather, "storm"))
        {
            return HomeMark.Rain;
        }

        if (Contains(weather, "snow") || Contains(weather, "blizzard") || Contains(weather, "frost"))
        {
            return HomeMark.Cloud;
        }

        if (Night(clock.Now) && (Contains(weather, "fair") || Contains(weather, "clear") || weather.Length == 0))
        {
            return HomeMark.Moon;
        }

        return HomeMark.Sun;
    }

    private static bool Contains(string haystack, string needle) =>
        haystack.Contains(needle, StringComparison.OrdinalIgnoreCase);

    private static bool Night(DateTimeOffset local) => local.Hour < 7 || local.Hour >= 19;

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

    private static TalkThread? FirstActive(IReadOnlyList<TalkThread> inbox)
    {
        for (var index = 0; index < inbox.Count; index++)
        {
            if (inbox[index].LastAt > DateTimeOffset.MinValue || inbox[index].Unread > 0 || inbox[index].Pinned)
            {
                return inbox[index];
            }
        }

        return null;
    }
}
