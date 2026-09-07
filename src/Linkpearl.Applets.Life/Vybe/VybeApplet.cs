using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Linkpearl.Applets;
using Linkpearl.Badges;
using Linkpearl.Feedback;
using Linkpearl.Chat;
using Linkpearl.Emoji;
using Linkpearl.Input;
using Linkpearl.Geometry;
using Linkpearl.Layout;
using Linkpearl.Media;
using Linkpearl.Modules;
using Linkpearl.Net;
using Linkpearl.Painting;
using Linkpearl.Platform;
using Linkpearl.Preferences;

namespace Linkpearl.Applets.Life.Vybe;

public sealed partial class VybeApplet : IApplet, IHandsetProfileSink
{
    public static readonly AppletManifest Manifest = new()
    {
        Id = "afterdark",
        DisplayNameKey = "VYBE",
        Family = AppletFamily.Social,
        Glyph = "☽",
        HomeOrder = 9,
        Capabilities = AppletCapabilities.AgeRestricted | AppletCapabilities.RequiresAccount,
    };

    private static readonly string[] Tabs = { "Home", "Discover", "Gallery", "Messages", "Profile" };

    private readonly IGameSession game;
    private readonly HostPaths paths;
    private readonly IPearlHub pearl;
    private readonly DisplayPreferences display;
    private readonly BadgeBook badges;
    private readonly IFilePicker files;
    private readonly ILifestream lifestream;
    private readonly IFeedbackDesk desk;
    private readonly VybeState state;
    private readonly ChatTray talkTray = new();
    private bool talkAlbum;
    private bool talkAlbumLock;
    private string talkLook = "";
    private TalkMenu? talkMenu;
    private bool talkMenuSkip;
    private bool talkMenuEat;
    private float nameClock;
    private int profileStamp;
    private bool demoHearts;
    private float hottTravel;
    private PearlPost[] demoWall = [];
    private PearlPost[] groupWall = [];
    private string chatAnchor = "";

    public VybeApplet(IGameSession game, HostPaths paths, IPearlHub pearl, DisplayPreferences display,
        BadgeBook badges, HandsetProfileDesk profiles, IFilePicker files, ILifestream lifestream,
        IFeedbackDesk desk)
    {
        this.game = game;
        this.paths = paths;
        this.pearl = pearl;
        this.display = display;
        this.badges = badges;
        this.files = files;
        this.lifestream = lifestream;
        this.desk = desk;
        state = VybeState.Load(paths, game.Character.Name);
        profiles.Add(this);
        if (state.UsesHandsetIdentity || state.UsesHandsetProfile)
        {
            AcceptHandsetProfile(HandsetName(), HandsetLook.Honorific(display));
        }
    }

    private string HandsetName()
    {
        var linked = ShownName.Linked(game.Character.Name, pearl.Current.MeName);
        var name = HandsetLook.Name(display, linked);
        return name.Length > 0 ? name : game.Character.Name;
    }

    private string ProfileName()
    {
        var linked = ShownName.Linked(game.Character.Name, pearl.Current.MeName);
        return ShownName.Preferred(display, linked, state.DisplayName, "You");
    }

    private string EditableName()
    {
        var linked = ShownName.Linked(game.Character.Name, pearl.Current.MeName);
        var handset = ShownName.Source(display, linked);
        var stored = state.DisplayName.Trim();
        if (stored.Length > 0)
        {
            return stored;
        }

        return handset.Length > 0 ? handset : "You";
    }

    private string ProfileHonorific() => state.Honorific.Trim();

    private string ProfileMeta()
    {
        var honor = ProfileHonorific();
        var pronouns = state.Pronouns.Trim();
        if (honor.Length > 0 && pronouns.Length > 0)
        {
            return honor + " · " + pronouns;
        }

        return honor.Length > 0 ? honor : pronouns;
    }

    private static string SocialHandle(string handle)
    {
        var trimmed = handle.Trim();
        if (trimmed.Length == 0)
        {
            return string.Empty;
        }

        return trimmed.StartsWith("@", StringComparison.Ordinal) ? trimmed : "@" + trimmed;
    }

    private void DrawProfileIdentity(in AppletFrame frame, ref Stack stack, string name, string handle, string meta,
        string about, bool night)
    {
        var tone = VybeChrome.Tone(night);
        var nameH = MathF.Max(frame.Units(22f), frame.Text.LineHeight(FontRole.Title));
        DrawFlowName(frame, stack.Take(nameH), name, night);
        var tag = SocialHandle(handle);
        if (tag.Length > 0)
        {
            frame.Text.DrawEllipsized(stack.Take(frame.Units(16f)), tag,
                new TextStyle(FontRole.Caption, tone.Mute));
        }

        if (meta.Length > 0)
        {
            frame.Text.DrawEllipsized(stack.Take(frame.Units(15f)), meta,
                new TextStyle(FontRole.Caption, tone.Mute));
        }

        if (about.Length > 0)
        {
            frame.Text.DrawWrapped(stack.Take(frame.Units(40f)), about,
                new TextStyle(FontRole.Caption, tone.Ink));
        }
    }

    private void DrawOwnProfileFacts(in AppletFrame frame, ref Stack stack, bool night) =>
        DrawProfileFacts(frame, ref stack, JoinShown(state.Genders, night), state.Sexuality, state.DmsOpen,
            JoinShown(state.Intents, night), state.Relationship, night);

    private static void DrawProfileFacts(in AppletFrame frame, ref Stack stack, string gender, string sexuality,
        bool? dmsOpen, string intent, string relationship, bool night)
    {
        var tone = VybeChrome.Tone(night);
        DrawFactRow(frame, ref stack, "Gender", gender, tone);
        DrawFactRow(frame, ref stack, "Sexuality", sexuality, tone);
        if (dmsOpen is { } open)
        {
            DrawFactRow(frame, ref stack, "DMs", open ? "Open" : "Closed", tone);
        }

        DrawFactRow(frame, ref stack, "Interests", intent, tone);
        var status = relationship.Trim();
        if (status.Length > 0 &&
            !status.Equals("Rather not say", StringComparison.OrdinalIgnoreCase) &&
            !status.Equals("Prefer Not To Say", StringComparison.OrdinalIgnoreCase))
        {
            DrawFactRow(frame, ref stack, "Relationship", status, tone);
        }
    }

    private static void DrawFactRow(in AppletFrame frame, ref Stack stack, string label, string value, NightPalette tone)
    {
        var shown = value.Trim();
        if (shown.Length == 0)
        {
            return;
        }

        var labelW = frame.Units(88f);
        var gap = frame.Units(8f);
        var valueW = MathF.Max(frame.Units(80f), stack.Remaining.Width - labelW - gap);
        var wrap = frame.Text.MeasureWrapped(shown, FontRole.Caption, valueW);
        var line = frame.Text.LineHeight(FontRole.Caption);
        var row = stack.Take(MathF.Max(line, wrap.Y));
        frame.Text.DrawEllipsized(row.LeftSlice(labelW).TopSlice(line), label,
            new TextStyle(FontRole.Caption, tone.Mute));
        frame.Text.DrawWrapped(row.Inset(new Edges(labelW + gap, 0f, 0f, 0f)), shown,
            new TextStyle(FontRole.Caption, tone.Ink));
    }

    private readonly record struct ProfileActionHits(bool Follow, bool Contact);

    private static ProfileActionHits DrawProfileActions(in AppletFrame frame, ref Stack stack, bool following,
        bool canContact, bool night)
    {
        var tone = VybeChrome.Tone(night);
        var row = stack.Take(frame.Units(38f));
        var gap = frame.Units(8f);
        var follow = canContact ? row.LeftSlice((row.Width - gap) * 0.5f) : row;
        var contact = canContact
            ? new Rect(new Vector2(follow.Max.X + gap, row.Min.Y), row.Max)
            : Rect.Empty;
        DrawActionPill(frame, follow, following ? "Following" : "Follow", !following, tone);
        if (canContact)
        {
            DrawActionPill(frame, contact, "Contact", false, tone);
        }

        return new ProfileActionHits(frame.Input.ConsumeClick(follow),
            canContact && frame.Input.ConsumeClick(contact));
    }

    private static void DrawActionPill(in AppletFrame frame, Rect area, string label, bool primary, NightPalette tone)
    {
        var radius = area.Height * 0.5f;
        var hover = frame.Input.IsHovering(area);
        if (primary)
        {
            var wash = hover
                ? new Vector4(MathF.Min(1f, tone.Accent.X + 0.06f), MathF.Min(1f, tone.Accent.Y + 0.06f),
                    MathF.Min(1f, tone.Accent.Z + 0.06f), 1f)
                : tone.Accent;
            frame.Paint.Fill(area, wash, radius);
            frame.Text.DrawIn(area, label,
                new TextStyle(FontRole.BodyStrong, tone.AccentInk, TextAlign.Center));
            return;
        }

        frame.Paint.Fill(area, new Vector4(1f, 1f, 1f, hover ? 0.14f : 0.08f), radius);
        frame.Paint.Stroke(area, new Vector4(1f, 1f, 1f, hover ? 0.32f : 0.18f), frame.Units(1.2f), radius);
        frame.Text.DrawIn(area, label,
            new TextStyle(FontRole.BodyStrong, tone.Ink, TextAlign.Center));
    }

    private static string JoinShown(IReadOnlyList<string> values, bool night)
    {
        var parts = new List<string>();
        for (var index = 0; index < values.Count; index++)
        {
            var tag = values[index];
            if (tag.Length == 0 ||
                string.Equals(tag, "Sharing", StringComparison.Ordinal) ||
                (!night && string.Equals(tag, "ERP", StringComparison.Ordinal)))
            {
                continue;
            }

            parts.Add(tag);
        }

        return parts.Count == 0 ? string.Empty : string.Join(", ", parts);
    }

    private bool FancyName() =>
        GlassName.IsPatron(badges, pearl.Current, display.TestingAccount);

    private void DrawFlowName(in AppletFrame frame, Rect area, string name, bool night) =>
        NameMark.DrawName(frame, area, name, display, VybeChrome.Tone(night).Ink, FancyName(), nameClock);

    public void AcceptHandsetProfile(string name, string honorific)
    {
        var shown = name.Trim();
        if (shown.Length == 0)
        {
            shown = HandsetName();
        }

        if (shown.Length > 0)
        {
            state.DisplayName = shown;
        }

        state.Honorific = ShownName.ClampTitle(honorific);
        state.ProfileFacePath = HandsetLook.ReplaceStill(paths, state.ProfileFacePath,
            HandsetLook.PortraitFile(paths, badges), "afterdark-profile-face");
        state.ProfileBannerPath = HandsetLook.ReplaceStill(paths, state.ProfileBannerPath,
            HandsetLook.BannerFile(paths, display), "afterdark-profile-banner");
        state.UsesHandsetProfile = false;
        state.UsesHandsetIdentity = false;
        profileStamp++;
        state.Save(paths);
    }

    private string OwnFacePath() => state.ProfileFacePath;

    private string OwnBannerPath() => state.ProfileBannerPath;

    AppletManifest IApplet.Manifest => Manifest;

    public AppletBadge Badge
    {
        get
        {
            var n = pearl.Current.Notes.Length + pearl.Current.UnreadTotal;
            return n > 0 ? new AppletBadge(Math.Min(n, 9)) : AppletBadge.None;
        }
    }

    public string Place => state.Page == NightPage.Tabs
        ? Tabs[(int)state.Tab]
        : state.Page.ToString();

    public void Enter(AppletEntry entry)
    {
        pearl.WatchFeed(state.FeedEveryone ? "foryou" : "following");
        if (entry.RouteHint is not { Length: > 0 } hint)
        {
            return;
        }

        for (var index = 0; index < Tabs.Length; index++)
        {
            if (string.Equals(Tabs[index], hint, StringComparison.OrdinalIgnoreCase))
            {
                state.Tab = (NightTab)index;
                state.Page = NightPage.Tabs;
                return;
            }
        }

        if (Enum.TryParse<NightPage>(hint, true, out var page))
        {
            state.Page = page;
        }
    }

    public void Leave()
    {
        state.Save(paths);
    }

    public bool CanGoBack => state.Page != NightPage.Tabs;

    public bool Back()
    {
        if (talkLook.Length > 0)
        {
            talkLook = string.Empty;
            return true;
        }

        if (talkAlbum)
        {
            talkAlbum = false;
            talkAlbumLock = false;
            return true;
        }

        if (state.Page == NightPage.Tabs)
        {
            return false;
        }

        if (state.Page == NightPage.OnboardReady)
        {
            state.Page = NightPage.OnboardAbout;
            return true;
        }

        if (state.Page == NightPage.OnboardAbout)
        {
            state.Page = NightPage.OnboardIntent;
            return true;
        }

        if (state.Page == NightPage.OnboardIntent)
        {
            state.Page = NightPage.OnboardIdentity;
            return true;
        }

        state.Back();
        return true;
    }

    public void Compose(in AppletFrame frame)
    {
        nameClock += frame.DeltaSeconds;
        state.Bind(pearl.Current);
        VybeDemo.Seed(state, paths, !demoHearts, game.Character.WorldName);
        demoHearts = true;
        if (demoWall.Length == 0)
        {
            demoWall = VybeDemo.Posts(paths);
        }

        if (groupWall.Length == 0)
        {
            groupWall = VybeGroups.Posts(paths);
        }

        VybeChrome.Stage(frame);
        if (state.Page == NightPage.Gate)
        {
            DrawGate(frame, frame.Content.Inset(frame.Units(16f)));
        }
        else if (state.Page == NightPage.Rules)
        {
            DrawRules(frame, frame.Content.Inset(frame.Units(14f)));
        }
        else if (state.Page is NightPage.OnboardIdentity or NightPage.OnboardIntent or NightPage.OnboardAbout
                 or NightPage.OnboardReady)
        {
            DrawOnboard(frame, frame.Content.Inset(frame.Units(16f)));
        }
        else
        {
            var chrome = frame.Units(60f);
            var nav = frame.Units(60f);
            if (state.Page == NightPage.Chat)
            {
                DrawChat(frame, frame.Content.Inset(new Edges(frame.Units(10f), frame.Units(8f), frame.Units(10f),
                    frame.Units(10f))));
            }
            else
            {
            DrawHead(frame, frame.Content.TopSlice(chrome));
            DrawNav(frame, frame.Content.BottomSlice(nav));
            var side = ProfileBleed() ? 0f : frame.Units(14f);
            var top = ProfileBleed() ? chrome : chrome + frame.Units(8f);
            var body = frame.Content.Inset(new Edges(side, top, side,
                nav + frame.Units(8f)));
            if (state.Page != NightPage.Tabs)
            {
                VybeChrome.Wheel(frame, body, state,
                    state.Page == NightPage.Filters && state.DiscoverPane == 0
                        ? frame.Units(3200f)
                        : state.Page == NightPage.Gallery
                            ? GalleryBoardHeight(frame, body.Width)
                            : state.Page == NightPage.Person
                                ? frame.Units(1200f)
                            : frame.Units(900f));
                frame.Paint.PushClip(body);
                DrawStack(frame, body.Translate(new Vector2(0f, -state.Scroll)));
                frame.Paint.PopClip();
            }
            else
            {
                DrawTabBody(frame, body);
            }

            DrawTalkMenu(frame, body, state.Night);

            if (state.SharePostId.Length > 0 && state.Page != NightPage.ShareSend)
            {
                DrawShareSheet(frame, frame.Content.Inset(new Edges(frame.Units(14f), chrome, frame.Units(14f),
                    nav + frame.Units(8f))));
            }

            if (state.ReportOpen)
            {
                DrawProfileReport(frame, frame.Content.Inset(new Edges(frame.Units(14f), chrome, frame.Units(14f),
                    nav + frame.Units(8f))));
            }
            }
        }

        if (pearl.Current.Notice.Length > 0 && state.Page is NightPage.Tabs or NightPage.Compose)
        {
            VybeChrome.Mute(frame, frame.Content.TopSlice(frame.Units(18f)).Inset(new Edges(frame.Units(16f), 0f)),
                pearl.Current.Notice, state.Night);
        }

        state.TickWash(frame.DeltaSeconds, paths);
        VybeChrome.Wash(frame, state.Wash, state.WashToNight);
    }

    private void DrawTabBody(in AppletFrame frame, Rect body)
    {
        VybeChrome.Wheel(frame, body, state,
            state.Tab == NightTab.Discover && state.DiscoverPane == 0
                ? frame.Units(2800f)
                : state.Tab == NightTab.Discover && state.DiscoverPane == 3
                    ? frame.Units(1100f)
                : state.Tab == NightTab.Gallery
                    ? GalleryBoardHeight(frame, body.Width)
                : state.Tab == NightTab.Profile
                    ? frame.Units(1680f)
                : state.Tab == NightTab.Home
                    ? frame.Units(2200f)
                    : frame.Units(1400f));
        frame.Paint.PushClip(body);
        var shifted = body.Translate(new Vector2(0f, -state.Scroll));
        switch (state.Tab)
        {
            case NightTab.Gallery:
                pearl.WatchFeed(state.FeedEveryone ? "foryou" : "following");
                DrawGalleryTab(frame, shifted);
                break;
            case NightTab.Discover:
                DrawDiscover(frame, shifted);
                break;
            case NightTab.Messages:
                DrawMessages(frame, shifted);
                break;
            case NightTab.Profile:
                pearl.WatchProfile("me");
                DrawMe(frame, shifted);
                break;
            default:
                pearl.WatchFeed(state.FeedEveryone ? "foryou" : "following");
                DrawHome(frame, shifted);
                break;
        }

        frame.Paint.PopClip();
    }

    private void DrawHome(in AppletFrame frame, Rect area)
    {
        var night = state.Night;
        var tone = VybeChrome.Tone(night);
        var snap = pearl.Current;
        var stack = new Stack(area, StackAxis.Vertical, frame.Units(10f));
        DrawWelcomeCard(frame, stack.Take(frame.Units(78f)), night);

        VybeChrome.Title(frame, stack.Take(frame.Units(20f)), "Stories", night);
        DrawStoryRail(frame, stack.Take(frame.Units(72f)), night);

        var hotHead = stack.Take(frame.Units(22f));
        VybeChrome.Title(frame, hotHead.LeftSlice(hotHead.Width * 0.62f), "Who's Hot", night);
        frame.Text.DrawIn(hotHead.RightSlice(frame.Units(56f)), "See all",
            new TextStyle(FontRole.CaptionStrong, tone.Accent, TextAlign.Right));
        if (frame.Input.ConsumeClick(hotHead.RightSlice(frame.Units(56f))))
        {
            state.DiscoverPane = 0;
            state.Tab = NightTab.Discover;
            state.Scroll = 0f;
        }

        DrawHottRail(frame, stack.Take(frame.Units(168f)), night);

        VybeChrome.Title(frame, stack.Take(frame.Units(20f)), "Feed", night);
        DrawFeedPicks(frame, stack.Take(frame.Units(32f)), night);
        var shown = 0;
        foreach (var wallPost in VisibleFeed(HomeBoard()))
        {
            DrawPearlCard(frame, stack.Take(frame.Units(PostCardHeight(frame, wallPost))), wallPost);
            shown++;
        }

        if (shown == 0)
        {
            VybeChrome.Mute(frame, stack.Take(frame.Units(36f)),
                state.FeedPick == FeedPick.Groups
                    ? "No group posts yet."
                    : snap.SignedIn
                        ? snap.FeedLive ? "Nothing on the feed yet." : "Pearlgate is not hosting posts yet."
                        : "Sign in from You to see the live feed.",
                night);
        }
    }

    private void DrawWelcomeCard(in AppletFrame frame, Rect area, bool night)
    {
        var tone = VybeChrome.Tone(night);
        var radius = area.Height * 0.5f;
        var pink = new Vector4(0.980f, 0.280f, 0.720f, 0.95f);
        var cyan = new Vector4(0.180f, 0.880f, 1.000f, 0.95f);
        var glass = new Vector4(0.050f, 0.050f, 0.080f, 0.72f);
        var live = pearl.Current.SignedIn;
        var pip = live ? VybeChrome.Online : new Vector4(0.52f, 0.52f, 0.56f, 1f);

        frame.Paint.Fill(area, glass, radius);
        frame.Paint.PushClip(area.LeftSlice(area.Width * 0.56f));
        frame.Paint.Stroke(area, pink, frame.Units(1.6f), radius);
        frame.Paint.PopClip();
        frame.Paint.PushClip(area.RightSlice(area.Width * 0.56f));
        frame.Paint.Stroke(area, cyan, frame.Units(1.6f), radius);
        frame.Paint.PopClip();

        var inner = area.Inset(new Edges(frame.Units(10f), frame.Units(8f)));
        var faceR = frame.Units(18f);
        var face = inner.LeftSlice(faceR * 2f);
        DrawFace(frame, face.Center, faceR, pearl.Current.MeAvatarUrl, tone.Accent, night, state.ProfileFacePath);
        frame.Paint.StrokeCircle(face.Center, faceR, pink, frame.Units(1.5f));
        var pipAt = face.Center + new Vector2(faceR * 0.72f, faceR * 0.62f);
        frame.Paint.FillCircle(pipAt, frame.Units(5.2f), glass with { W = 1f });
        frame.Paint.FillCircle(pipAt, frame.Units(3.6f), pip);

        var trailW = frame.Units(70f);
        var trail = inner.RightSlice(trailW);
        var pillH = frame.Units(18f);
        var pillW = frame.Units(52f);
        var pill = Rect.FromSize(
            new Vector2(trail.Min.X, trail.Center.Y - pillH * 0.5f),
            new Vector2(pillW, pillH));
        frame.Paint.Fill(pill, pip with { W = 0.16f }, pillH * 0.5f);
        frame.Paint.Stroke(pill, pip with { W = 0.85f }, frame.Units(1.1f), pillH * 0.5f);
        frame.Paint.FillCircle(new Vector2(pill.Min.X + frame.Units(8f), pill.Center.Y), frame.Units(2.4f), pip);
        frame.Text.DrawIn(pill.Inset(new Edges(frame.Units(14f), 0f, frame.Units(6f), 0f)),
            live ? "Online" : "Away",
            new TextStyle(FontRole.Caption, tone.Ink));
        frame.Text.DrawIn(trail.RightSlice(frame.Units(14f)), "›",
            new TextStyle(FontRole.Body, tone.Ink, TextAlign.Center));

        var copy = inner.Inset(new Edges(faceR * 2f + frame.Units(8f), frame.Units(2f), trailW + frame.Units(4f),
            frame.Units(2f)));
        var rows = new Stack(copy, StackAxis.Vertical, frame.Units(1f));
        frame.Text.DrawEllipsized(rows.Take(frame.Units(12f)), "WELCOME BACK,",
            new TextStyle(FontRole.Caption, tone.Mute));
        frame.Text.DrawEllipsized(rows.Take(frame.Units(22f)), WelcomeName(),
            new TextStyle(FontRole.Title, tone.Ink));
        frame.Text.DrawEllipsized(rows.Take(frame.Units(16f)),
            night ? "Ready for the night?" : "Ready to vibe?",
            new TextStyle(FontRole.Caption, tone.Ink));

        if (frame.Input.ConsumeClick(area))
        {
            state.Tab = NightTab.Profile;
            state.Scroll = 0f;
        }
    }

    private string WelcomeName()
    {
        var name = ProfileName();
        var space = name.IndexOf(' ');
        return space > 0 ? name[..space] : name;
    }

    private void DrawHottRail(in AppletFrame frame, Rect area, bool night)
    {
        var people = HottPeople();
        if (people.Count == 0)
        {
            people.AddRange(VybeDemo.People(paths));
        }

        if (people.Count == 0)
        {
            return;
        }

        var gap = frame.Units(8f);
        var cardW = (area.Width - gap * 2f) / 3f;
        var span = people.Count * (cardW + gap) - gap;
        var max = MathF.Max(0f, span - area.Width);
        if (frame.Input.IsHovering(area) && frame.Input.IsHeld())
        {
            hottTravel += MathF.Abs(frame.Input.PointerDelta.X);
            state.HottDrag = Math.Clamp(state.HottDrag - frame.Input.PointerDelta.X, 0f, max);
        }
        else
        {
            state.HottDrag = Math.Clamp(state.HottDrag, 0f, max);
        }

        frame.Paint.PushClip(area);
        var x = area.Min.X - state.HottDrag;
        for (var index = 0; index < people.Count; index++)
        {
            var cell = Rect.FromSize(new Vector2(x, area.Min.Y), new Vector2(cardW, area.Height));
            if (cell.Max.X > area.Min.X && cell.Min.X < area.Max.X)
            {
                DrawHottCard(frame, cell, people[index], night, true);
            }

            x += cardW + gap;
        }

        frame.Paint.PopClip();
        if (!frame.Input.IsHeld())
        {
            hottTravel = 0f;
        }
    }

    private List<ScenePerson> HottPeople()
    {
        var picks = new List<ScenePerson>();
        var seen = new HashSet<int>();
        var here = game.Character.WorldName;
        for (var index = 0; index < state.Roster.Count; index++)
        {
            var person = state.Roster[index];
            if (!state.Blocked.Contains(person.Id) && seen.Add(person.Id))
            {
                picks.Add(person);
            }
        }

        var extras = VybeDemo.People(paths);
        for (var index = 0; index < extras.Length; index++)
        {
            var person = extras[index];
            if (here.Length > 0 && (index == 0 || index == extras.Length - 1))
            {
                person = person with { World = here };
            }

            if (!state.Blocked.Contains(person.Id) && seen.Add(person.Id))
            {
                picks.Add(person);
            }
        }

        picks.Sort((left, right) =>
        {
            var leftHere = here.Length > 0 &&
                           string.Equals(left.World, here, StringComparison.OrdinalIgnoreCase) ? 0 : 1;
            var rightHere = here.Length > 0 &&
                            string.Equals(right.World, here, StringComparison.OrdinalIgnoreCase) ? 0 : 1;
            if (leftHere != rightHere)
            {
                return leftHere - rightHere;
            }

            var worlds = string.Compare(left.World, right.World, StringComparison.OrdinalIgnoreCase);
            return worlds != 0 ? worlds : string.Compare(left.Name, right.Name, StringComparison.OrdinalIgnoreCase);
        });
        return picks;
    }

    private void DrawHottCard(in AppletFrame frame, Rect area, ScenePerson person, bool night,
        bool swipeLock = false)
    {
        var tone = VybeChrome.Tone(night);
        DrawPersonCover(frame, area, person, night);
        frame.Paint.FillGradient(area, new Vector4(0f, 0f, 0f, 0.08f), new Vector4(0f, 0f, 0f, 0.82f),
            GradientAxis.Vertical);
        var heart = Rect.FromSize(new Vector2(area.Max.X - frame.Units(26f), area.Min.Y + frame.Units(6f)),
            new Vector2(frame.Units(20f), frame.Units(20f)));
        var liked = state.LikedPeople.Contains(person.Id);
        DrawHottHeart(frame, heart, tone.Accent, liked);
        var live = person.Online;
        var pip = live ? VybeChrome.Online : new Vector4(0.52f, 0.52f, 0.56f, 1f);
        var status = live ? "Active now" : "Offline";
        var statusW = frame.Text.Measure(status, FontRole.Caption).X + frame.Units(18f);
        var chip = Rect.FromSize(
            new Vector2(area.Min.X + frame.Units(6f), area.Min.Y + frame.Units(6f)),
            new Vector2(MathF.Min(statusW, area.Width - frame.Units(32f)), frame.Units(16f)));
        frame.Paint.Fill(chip, new Vector4(0f, 0f, 0f, 0.55f), chip.Height * 0.5f);
        var dot = new Vector2(chip.Min.X + frame.Units(7f), chip.Center.Y);
        frame.Paint.FillCircle(dot, frame.Units(3.4f), pip);
        frame.Text.DrawEllipsized(chip.Inset(new Edges(frame.Units(13f), 0f, frame.Units(5f), 0f)), status,
            new TextStyle(FontRole.Caption, VybeChrome.Night.Ink));
        var copy = area.Inset(new Edges(frame.Units(8f), 0f, frame.Units(8f), frame.Units(8f)));
        var nameRow = copy.BottomSlice(frame.Units(34f)).TopSlice(frame.Units(16f));
        frame.Text.DrawEllipsized(nameRow, person.Name,
            new TextStyle(FontRole.CaptionStrong, VybeChrome.Night.Ink));
        var miles = 3 + Math.Abs(person.Id % 13);
        var age = 21 + Math.Abs(person.Id % 8);
        frame.Text.DrawEllipsized(copy.BottomSlice(frame.Units(16f)),
            age.ToString(CultureInfo.InvariantCulture) + " • " + miles.ToString(CultureInfo.InvariantCulture) +
            " mi away",
            new TextStyle(FontRole.Caption, VybeChrome.Night.Ink with { W = 0.88f }));
        if (swipeLock && hottTravel >= frame.Units(12f))
        {
            return;
        }

        if (frame.Input.ConsumeClick(heart))
        {
            state.ToggleLikedPerson(person.Id);
            state.Save(paths);
            return;
        }

        if (frame.Input.ConsumeClick(area))
        {
            OpenPerson(person.GateId);
        }
    }

    private static void DrawHottHeart(in AppletFrame frame, Rect area, Vector4 ink, bool filled)
    {
        var c = area.Center;
        var s = MathF.Min(area.Width, area.Height) * 0.28f;
        if (filled)
        {
            frame.Paint.FillCircle(c + new Vector2(-s * 0.38f, -s * 0.18f), s * 0.40f, ink);
            frame.Paint.FillCircle(c + new Vector2(s * 0.38f, -s * 0.18f), s * 0.40f, ink);
            frame.Paint.Line(c + new Vector2(-s * 0.72f, 0f), c + new Vector2(0f, s * 0.82f), ink,
                MathF.Max(1.6f, s * 0.42f));
            frame.Paint.Line(c + new Vector2(s * 0.72f, 0f), c + new Vector2(0f, s * 0.82f), ink,
                MathF.Max(1.6f, s * 0.42f));
            return;
        }

        var stroke = MathF.Max(1.4f, s * 0.28f);
        frame.Paint.StrokeCircle(c + new Vector2(-s * 0.38f, -s * 0.18f), s * 0.38f, ink, stroke);
        frame.Paint.StrokeCircle(c + new Vector2(s * 0.38f, -s * 0.18f), s * 0.38f, ink, stroke);
        frame.Paint.Line(c + new Vector2(-s * 0.72f, 0f), c + new Vector2(0f, s * 0.82f), ink, stroke);
        frame.Paint.Line(c + new Vector2(s * 0.72f, 0f), c + new Vector2(0f, s * 0.82f), ink, stroke);
    }

    private void DrawDiscoverPanes(in AppletFrame frame, Rect panes, bool night)
    {
        var next = VybeChrome.TextTabs(frame, panes,
            ["People", "Posts", "Hashtags", "Groups"], state.DiscoverPane, night);
        if (next != state.DiscoverPane)
        {
            state.DiscoverPane = next;
            if (next != 0)
            {
                state.Scroll = 0f;
            }
        }

    }

    private void DrawDiscover(in AppletFrame frame, Rect area)
    {
        if (state.DiscoverPane > 3)
        {
            state.DiscoverPane = 0;
        }

        if (state.DiscoverPane == 0)
        {
            DrawPeopleDiscovery(frame, area);
            return;
        }

        if (state.DiscoverPane == 3)
        {
            DrawGroups(frame, area);
            return;
        }

        var night = state.Night;
        var tone = VybeChrome.Tone(night);
        var stack = new Stack(area, StackAxis.Vertical, frame.Units(10f));
        var head = stack.Take(frame.Units(28f));
        VybeChrome.Title(frame, head.LeftSlice(head.Width * 0.62f), "Discover", night);
        var tools = head.RightSlice(frame.Units(64f));
        var search = tools.LeftSlice(frame.Units(28f));
        var filter = tools.RightSlice(frame.Units(28f));
        DrawSearchGlyph(frame, search.Center, frame.Units(8f),
            state.DiscoverSearchOpen ? tone.Accent : tone.Ink);
        DrawFilterGlyph(frame, filter.Center, frame.Units(9f), tone.Ink);
        if (frame.Input.ConsumeClick(search))
        {
            state.DiscoverSearchOpen = !state.DiscoverSearchOpen;
            if (!state.DiscoverSearchOpen)
            {
                state.Search = string.Empty;
            }
        }

        if (frame.Input.ConsumeClick(filter))
        {
            state.Open(NightPage.Filters);
        }

        if (state.DiscoverSearchOpen)
        {
            state.Search = frame.TextField.Draw("ad-find", stack.Take(frame.Units(34f)), state.Search,
                "Search people and posts");
            if (state.Search.Length >= 2)
            {
                pearl.NoteQuery(state.Search);
            }
        }

        DrawDiscoverPanes(frame, stack.Take(frame.Units(34f)), night);

        if (state.DiscoverPane == 1)
        {
            DrawDiscoverPosts(frame, ref stack, night);
            return;
        }

        if (state.DiscoverPane == 2)
        {
            DrawDiscoverHashtags(frame, ref stack, night);
            return;
        }

        DrawDiscoverPeople(frame, ref stack, night);
    }

    private void DrawDiscoverPosts(in AppletFrame frame, ref Stack stack, bool night)
    {
        var shownPosts = 0;
        foreach (var wallPost in VisibleFeed(BoardFeed()))
        {
            DrawPearlCard(frame, stack.Take(frame.Units(PostCardHeight(frame, wallPost))), wallPost);
            shownPosts++;
        }

        if (shownPosts == 0)
        {
            VybeChrome.Mute(frame, stack.Take(frame.Units(28f)),
                pearl.Current.FeedLive ? "Nothing on the feed yet." : "Pearlgate is not hosting posts yet.",
                night);
        }
    }

    private void DrawDiscoverHashtags(in AppletFrame frame, ref Stack stack, bool night)
    {
        var tags = LiveHashes(BoardFeed());
        if (tags.Count == 0)
        {
            VybeChrome.Mute(frame, stack.Take(frame.Units(28f)), "Hashtags show up when people use them.",
                night);
        }

        for (var index = 0; index < tags.Count; index += 2)
        {
            var row = stack.Take(frame.Units(32f));
            DrawHashChip(frame, row.LeftSlice(row.Width * 0.48f), tags[index], night);
            if (index + 1 < tags.Count)
            {
                DrawHashChip(frame, row.RightSlice(row.Width * 0.48f), tags[index + 1], night);
            }
        }

        VybeChrome.Kicker(frame, stack.Take(frame.Units(14f)), "MATCHING POSTS", night);
        var hits = 0;
        foreach (var wallPost in VisibleFeed(BoardFeed()))
        {
            DrawPearlCard(frame, stack.Take(frame.Units(PostCardHeight(frame, wallPost))), wallPost);
            hits++;
        }

        if (hits == 0)
        {
            VybeChrome.Mute(frame, stack.Take(frame.Units(28f)), "No matching posts.", night);
        }
    }

    private void DrawDiscoverPeople(in AppletFrame frame, ref Stack stack, bool night)
    {
        var picks = DiscoverPeople();
        if (picks.Count == 0)
        {
            VybeChrome.Mute(frame, stack.Take(frame.Units(28f)),
                "Nobody matches this search.", night);
            return;
        }

        var gap = frame.Units(8f);
        var cardH = frame.Units(208f);
        for (var index = 0; index < picks.Count; index += 2)
        {
            var row = stack.Take(cardH);
            var half = (row.Width - gap) * 0.5f;
            DrawHottCard(frame, Rect.FromSize(row.Min, new Vector2(half, row.Height)), picks[index], night);
            if (index + 1 < picks.Count)
            {
                DrawHottCard(frame,
                    Rect.FromSize(new Vector2(row.Min.X + half + gap, row.Min.Y), new Vector2(half, row.Height)),
                    picks[index + 1], night);
            }
        }
    }

    private List<ScenePerson> DiscoverPeople()
    {
        var picks = new List<ScenePerson>();
        var seen = new HashSet<int>();
        var extras = VybeDemo.People(paths);
        for (var index = 0; index < extras.Length; index++)
        {
            var person = extras[index];
            if (!state.Blocked.Contains(person.Id) && seen.Add(person.Id))
            {
                picks.Add(person);
            }
        }

        for (var index = 0; index < state.Roster.Count; index++)
        {
            var person = state.Roster[index];
            if (state.Blocked.Contains(person.Id) || !seen.Add(person.Id))
            {
                continue;
            }

            if (state.Search.Length == 0 || state.Passes(person))
            {
                picks.Add(person);
            }
        }

        if (state.Search.Length > 0)
        {
            picks.RemoveAll(person => !state.Passes(person));
        }

        return picks;
    }

    private void DrawPersonCover(in AppletFrame frame, Rect area, ScenePerson person, bool night)
    {
        var radius = frame.Units(16f);
        if (person.AvatarUrl.Length > 0 && File.Exists(person.AvatarUrl))
        {
            var texture = frame.Textures.FromFile(person.AvatarUrl);
            if (texture is { IsReady: true })
            {
                var uv = CoverFit.Uv(texture.Size, area.Size);
                frame.Paint.ImageRounded(texture, area, uv.Min, uv.Max, Vector4.One, radius);
                return;
            }
        }

        if (person.AvatarUrl.Length > 0)
        {
            pearl.PrefetchMedia(person.AvatarUrl);
            var path = pearl.LocalMedia(person.AvatarUrl);
            if (path is { Length: > 0 })
            {
                var texture = frame.Textures.FromFile(path);
                if (texture is { IsReady: true })
                {
                    var uv = CoverFit.Uv(texture.Size, area.Size);
                    frame.Paint.ImageRounded(texture, area, uv.Min, uv.Max, Vector4.One, radius);
                    return;
                }
            }
        }

        frame.Paint.Fill(area, person.Wash, radius);
        VybeChrome.Portrait(frame, area.Center - new Vector2(0f, frame.Units(16f)), frame.Units(28f),
            person.Wash, night);
    }

    private void DrawGalleryTab(in AppletFrame frame, Rect area)
    {
        var night = state.Night;
        var stack = new Stack(area, StackAxis.Vertical, frame.Units(10f));
        VybeChrome.Title(frame, stack.Take(frame.Units(28f)), "Gallery", night);
        DrawGallerySearch(frame, stack.Take(frame.Units(36f)), night);
        DrawMasonryGallery(frame, stack.TakeRemaining(), BoardFeed(), night);
    }

    private void DrawGallerySearch(in AppletFrame frame, Rect area, bool night)
    {
        var tone = VybeChrome.Tone(night);
        frame.Paint.Fill(area, tone.CardHi, frame.Units(12f));
        var next = frame.TextField.Draw("vybe-gallery-q", area.Inset(new Edges(frame.Units(12f), frame.Units(4f))),
            state.GalleryQuery, "Search #tags");
        if (!string.Equals(next, state.GalleryQuery, StringComparison.Ordinal))
        {
            state.GalleryQuery = next;
            state.Scroll = 0f;
        }
    }

    private void DrawMasonryGallery(in AppletFrame frame, Rect area, PearlPost[] posts, bool night)
    {
        var shots = GalleryShots(posts, state.GalleryQuery);
        if (shots.Count == 0)
        {
            VybeChrome.Mute(frame, area.TopSlice(frame.Units(36f)),
                state.GalleryQuery.Trim().Length > 0 ? "No photos match that tag." : "No photos yet.", night);
            return;
        }

        var gap = frame.Units(8f);
        var colW = (area.Width - gap) * 0.5f;
        var left = 0f;
        var right = 0f;
        for (var index = 0; index < shots.Count; index++)
        {
            var shot = shots[index];
            var height = MasonryTileHeight(colW, shot.Width, shot.Height);
            var useLeft = left <= right;
            var dest = Rect.FromSize(
                new Vector2(area.Min.X + (useLeft ? 0f : colW + gap), area.Min.Y + (useLeft ? left : right)),
                new Vector2(colW, height));
            DrawStill(frame, dest, shot.Url, night);
            if (frame.Input.ConsumeClick(dest))
            {
                state.ViewMedia = shot.Url;
                state.PostKey = shot.PostId;
                state.Open(NightPage.PhotoView);
            }

            if (useLeft)
            {
                left += height + gap;
            }
            else
            {
                right += height + gap;
            }
        }
    }

    private float GalleryBoardHeight(in AppletFrame frame, float width)
    {
        var gap = frame.Units(8f);
        var colW = MathF.Max(1f, (width - gap) * 0.5f);
        var left = 0f;
        var right = 0f;
        var shots = GalleryShots(BoardFeed(), state.GalleryQuery);
        for (var index = 0; index < shots.Count; index++)
        {
            var height = MasonryTileHeight(colW, shots[index].Width, shots[index].Height) + gap;
            if (left <= right)
            {
                left += height;
            }
            else
            {
                right += height;
            }
        }

        return frame.Units(84f) + MathF.Max(left, right) + frame.Units(48f);
    }

    private static List<GalleryShot> GalleryShots(PearlPost[] posts, string query)
    {
        var shots = new List<GalleryShot>();
        for (var index = 0; index < posts.Length; index++)
        {
            var post = posts[index];
            if (post.Media.Length == 0 || !GalleryTagHit(post.Body, query))
            {
                continue;
            }

            for (var media = 0; media < post.Media.Length; media++)
            {
                var still = post.Media[media];
                if (still.Url.Length == 0)
                {
                    continue;
                }

                shots.Add(new GalleryShot(still.Url, post.Id, still.Width, still.Height));
            }
        }

        return shots;
    }

    private static bool GalleryTagHit(string body, string query)
    {
        var needle = query.Trim().TrimStart('#');
        if (needle.Length == 0)
        {
            return true;
        }

        var from = 0;
        while (from < body.Length)
        {
            var hash = body.IndexOf('#', from);
            if (hash < 0)
            {
                return false;
            }

            var end = hash + 1;
            while (end < body.Length && char.IsLetterOrDigit(body[end]))
            {
                end++;
            }

            if (end > hash + 1 &&
                body.AsSpan(hash + 1, end - hash - 1).Contains(needle, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            from = end;
        }

        return false;
    }

    private static float MasonryTileHeight(float width, int mediaW, int mediaH)
    {
        var ratio = mediaW > 0 && mediaH > 0 ? mediaH / (float)mediaW : 1f;
        ratio = Math.Clamp(ratio, 0.62f, 1.72f);
        return width * ratio;
    }

    private readonly record struct GalleryShot(string Url, string PostId, int Width, int Height);

    private void DrawFeedPicks(in AppletFrame frame, Rect head, bool night)
    {
        var cell = head.Width / 3f;
        var forYou = Rect.FromSize(head.Min, new Vector2(cell, head.Height));
        var following = Rect.FromSize(new Vector2(head.Min.X + cell, head.Min.Y), new Vector2(cell, head.Height));
        var groups = head.RightSlice(cell);
        if (VybeChrome.Segment(frame, forYou, "For You", state.FeedPick == FeedPick.ForYou, night))
        {
            state.FeedPick = FeedPick.ForYou;
            pearl.WatchFeed("foryou");
            state.Scroll = 0f;
        }

        if (VybeChrome.Segment(frame, following, "Following", state.FeedPick == FeedPick.Following, night))
        {
            state.FeedPick = FeedPick.Following;
            pearl.WatchFeed("following");
            state.Scroll = 0f;
        }

        if (VybeChrome.Segment(frame, groups, "Groups", state.FeedPick == FeedPick.Groups, night))
        {
            state.FeedPick = FeedPick.Groups;
            state.Scroll = 0f;
        }
    }

    private void DrawFeed(in AppletFrame frame, Rect area)
    {
        var night = state.Night;
        var stack = new Stack(area, StackAxis.Vertical, frame.Units(10f));
        if (state.Page == NightPage.FeedWall &&
            VybeChrome.Back(frame, stack.Take(frame.Units(28f)), "Feed", night))
        {
            state.Back();
            return;
        }

        var head = stack.Take(frame.Units(32f));
        DrawFeedPicks(frame, head.Inset(new Edges(0f, 0f, frame.Units(40f), 0f)), night);

        var compose = head.RightSlice(frame.Units(36f));
        VybeChrome.Primary(frame, compose, "+", night);
        if (frame.Input.ConsumeClick(compose))
        {
            state.Caption = string.Empty;
            state.QuoteOf = string.Empty;
            state.DraftMedia.Clear();
            state.AudienceEveryone = true;
            state.Open(NightPage.Compose);
        }

        var shown = 0;
        foreach (var wallPost in VisibleFeed(BoardFeed()))
        {
            DrawPearlCard(frame, stack.Take(frame.Units(PostCardHeight(frame, wallPost))), wallPost);
            shown++;
        }

        if (shown == 0)
        {
            VybeChrome.Mute(frame, stack.Take(frame.Units(36f)),
                pearl.Current.FeedLive ? "Nothing shared yet." : "Pearlgate is not hosting a feed yet.",
                state.Night);
        }
    }

    private void DrawMessages(in AppletFrame frame, Rect area)
    {
        var night = state.Night;
        var snapshot = pearl.Current;
        var stack = new Stack(area, StackAxis.Vertical, frame.Units(10f));
        if (state.Page == NightPage.Inbox &&
            VybeChrome.Back(frame, stack.Take(frame.Units(28f)), "Messages", night))
        {
            state.Back();
            return;
        }

        VybeChrome.Title(frame, stack.Take(frame.Units(28f)), "Messages", night);
        var pane = VybeChrome.TextTabs(frame, stack.Take(frame.Units(34f)),
            ["Your chats", "Requests"], state.MessagePane, night);
        if (pane != state.MessagePane)
        {
            state.MessagePane = pane;
            state.Scroll = 0f;
        }

        if (state.MessagePane == 1)
        {
            DrawMessageRequests(frame, ref stack, night);
            return;
        }

        if (!snapshot.SignedIn && snapshot.Chats.Length == 0 && state.Connected.Count == 0)
        {
            VybeChrome.Mute(frame, stack.Take(frame.Units(48f)),
                "Sign in from You to load Pearlgate chats.", night);
            return;
        }

        var list = new Stack(stack.Remaining, StackAxis.Vertical, frame.Units(8f));
        var shown = 0;
        var favored = 0;
        foreach (var chat in snapshot.Chats)
        {
            var key = VybeState.PearlTalkKey(chat.Id);
            if (state.TalkHidden(key) || !state.TalkStarred(key))
            {
                continue;
            }

            if (favored == 0)
            {
                VybeChrome.Mute(frame, list.Take(frame.Units(18f)), "Favorites", night);
            }

            DrawPearlInbox(frame, list.Take(frame.Units(72f)), chat, night);
            favored++;
            shown++;
        }

        foreach (var id in state.Connected)
        {
            var key = VybeState.LocalTalkKey(id);
            if (state.TalkHidden(key) || !state.TalkStarred(key) || !state.TryFind(id, out var person))
            {
                continue;
            }

            if (favored == 0)
            {
                VybeChrome.Mute(frame, list.Take(frame.Units(18f)), "Favorites", night);
            }

            DrawInboxRow(frame, list.Take(frame.Units(72f)), person);
            favored++;
            shown++;
        }

        foreach (var chat in snapshot.Chats)
        {
            var key = VybeState.PearlTalkKey(chat.Id);
            if (state.TalkHidden(key) || state.TalkStarred(key))
            {
                continue;
            }

            DrawPearlInbox(frame, list.Take(frame.Units(72f)), chat, night);
            shown++;
        }

        foreach (var id in state.Connected)
        {
            var key = VybeState.LocalTalkKey(id);
            if (state.TalkHidden(key) || state.TalkStarred(key) || !state.TryFind(id, out var person))
            {
                continue;
            }

            DrawInboxRow(frame, list.Take(frame.Units(72f)), person);
            shown++;
        }

        if (shown == 0)
        {
            VybeChrome.Mute(frame, stack.Take(frame.Units(36f)), "No conversations yet.", night);
        }
    }

    private void DrawMessageRequests(in AppletFrame frame, ref Stack stack, bool night)
    {
        var shown = 0;
        foreach (var id in state.Incoming.ToArray())
        {
            if (!state.TryFind(id, out var person) || (!night && person.NightOnly))
            {
                continue;
            }

            var row = stack.Take(frame.Units(80f));
            VybeChrome.Plate(frame, row, frame.Units(12f), night);
            var inner = row.Inset(frame.Units(8f));
            DrawFace(frame, inner.LeftSlice(frame.Units(40f)).Center, frame.Units(16f), person.AvatarUrl,
                person.Wash, night);
            var copy = inner.Inset(new Edges(frame.Units(48f), 0f, 0f, 0f));
            VybeChrome.Title(frame, copy.TopSlice(frame.Units(20f)), person.Name, night);
            VybeChrome.Mute(frame, copy.TopSlice(frame.Units(38f)).BottomSlice(frame.Units(16f)),
                night ? "Wants to connect" : "Wants to follow you", night);
            var btns = copy.BottomSlice(frame.Units(26f));
            if (VybeChrome.Chip(frame, btns.LeftSlice(btns.Width * 0.48f), "Accept", true, night))
            {
                state.Connected.Add(id);
                state.Incoming.Remove(id);
                state.FollowersSeed++;
                state.Save(paths);
            }

            if (VybeChrome.Chip(frame, btns.RightSlice(btns.Width * 0.48f), "Decline", false, night))
            {
                state.Incoming.Remove(id);
                state.Save(paths);
            }

            shown++;
        }

        if (shown == 0)
        {
            VybeChrome.Mute(frame, stack.Take(frame.Units(36f)),
                night ? "No requests. Intros you receive land here." : "No message requests right now.",
                night);
        }
    }

    private void DrawPearlInbox(in AppletFrame frame, Rect area, PearlChat chat, bool night)
    {
        var wash = VybeChrome.Tone(night).CardHi;
        var face = string.Empty;
        if (state.TryFindGate(chat.OtherUserId, out var person))
        {
            face = person.AvatarUrl;
            wash = person.Wash;
        }

        var preview = chat.Preview.Length > 0 ? ChatBits.Preview(chat.Preview) : "No messages yet";
        var author = string.Empty;
        var lines = pearl.LinesFor(chat.Id);
        if (lines.Count > 0)
        {
            var last = lines[^1];
            preview = ChatBits.Preview(last.Body);
            if (chat.IsGroup && last.Author.Length > 0 && !last.Mine)
            {
                author = last.Author;
            }
        }

        var key = VybeState.PearlTalkKey(chat.Id);
        var dots = DrawTalkInbox(frame, area, chat.Title, face, wash, preview, author,
            TalkAgo(chat.LastMessageAtUnix), chat.UnreadCount > 0, state.TalkStarred(key), night);
        if (OpenTalkMenu(frame, area, dots, key))
        {
            return;
        }

        if (frame.Input.ConsumeClick(area))
        {
            state.ChatKey = chat.Id;
            state.ChatIndex = -1;
            pearl.WatchChat(chat.Id);
            state.Open(NightPage.Chat);
        }
    }

    private void DrawAlerts(in AppletFrame frame, Rect area)
    {
        var night = state.Night;
        var stack = new Stack(area, StackAxis.Vertical, frame.Units(8f));
        if (state.Page == NightPage.Alerts &&
            VybeChrome.Back(frame, stack.Take(frame.Units(28f)), "Notifications", night))
        {
            state.Back();
            return;
        }

        var head = stack.Take(frame.Units(28f));
        VybeChrome.Title(frame, head.LeftSlice(head.Width * 0.7f), "Notifications", night);

        var empty = true;
        foreach (var note in pearl.Current.Notes)
        {
            empty = false;
            var row = stack.Take(frame.Units(52f));
            VybeChrome.Plate(frame, row, frame.Units(12f), night);
            var inner = row.Inset(frame.Units(10f));
            VybeChrome.Portrait(frame, inner.LeftSlice(frame.Units(28f)).Center, frame.Units(12f),
                VybeChrome.Tone(night).CardHi, night);
            var body = inner.Inset(new Edges(frame.Units(36f), 0f, 0f, 0f));
            var line = note.ActorName + (note.Line.Length > 0 ? " " + note.Line : " " + note.Kind);
            frame.Text.DrawEllipsized(body.TopSlice(frame.Units(16f)), line,
                new TextStyle(FontRole.CaptionStrong, VybeChrome.Tone(night).Ink));
            VybeChrome.Mute(frame, body.BottomSlice(frame.Units(14f)), note.When, night);
            if (frame.Input.ConsumeClick(row))
            {
                if (note.PostId.Length > 0)
                {
                    OpenPost(note.PostId);
                }
                else if (note.ActorId.Length > 0)
                {
                    OpenPerson(note.ActorId);
                }
            }
        }

        if (!pearl.Current.NotesLive && pearl.Current.SignedIn)
        {
            VybeChrome.Mute(frame, stack.Take(frame.Units(24f)), "Activity alerts are not on Pearlgate yet.",
                night);
        }
        foreach (var chat in pearl.Current.Chats)
        {
            if (chat.UnreadCount <= 0)
            {
                continue;
            }

            empty = false;
            var row = stack.Take(frame.Units(52f));
            VybeChrome.Plate(frame, row, frame.Units(12f), night);
            var inner = row.Inset(frame.Units(10f));
            frame.Text.DrawEllipsized(inner.TopSlice(frame.Units(16f)), chat.Title,
                new TextStyle(FontRole.CaptionStrong, VybeChrome.Tone(night).Ink));
            VybeChrome.Mute(frame, inner.BottomSlice(frame.Units(14f)),
                chat.Preview.Length > 0 ? chat.Preview : "New message", night);
            if (frame.Input.ConsumeClick(row))
            {
                state.ChatKey = chat.Id;
                pearl.WatchChat(chat.Id);
                state.Open(NightPage.Chat);
            }
        }

        foreach (var notice in pearl.Current.Announcements)
        {
            empty = false;
            var row = stack.Take(frame.Units(52f));
            VybeChrome.Plate(frame, row, frame.Units(12f), night);
            var inner = row.Inset(frame.Units(10f));
            frame.Text.DrawEllipsized(inner.TopSlice(frame.Units(16f)), notice.Title,
                new TextStyle(FontRole.CaptionStrong, VybeChrome.Tone(night).Ink));
            VybeChrome.Mute(frame, inner.BottomSlice(frame.Units(14f)), notice.Body, night);
        }

        if (empty)
        {
            VybeChrome.Mute(frame, stack.Take(frame.Units(28f)), "You're caught up.", night);
        }
    }

    private bool ProfileBleed() =>
        state.Page == NightPage.Person || (state.Page == NightPage.Tabs && state.Tab == NightTab.Profile);

    private void DrawMe(in AppletFrame frame, Rect area)
    {
        var night = state.Night;
        var tone = VybeChrome.Tone(night);
        var hero = DrawProfileHero(frame, area, night, OwnBannerPath(), tone.AccentDim,
            OwnFacePath().Length > 0 ? string.Empty : pearl.Current.MeAvatarUrl, OwnFacePath(), tone.Accent,
            edit: true, back: false);
        if (hero.Flag && !state.ReportOpen)
        {
            OpenProfileReport("me", ProfileName());
        }

        if (hero.Edit)
        {
            state.Open(NightPage.OnboardIdentity);
        }

        if (hero.Face)
        {
            state.PickingAvatar = true;
            state.PickingBanner = false;
            state.Open(NightPage.PhotoPick);
        }
        else if (hero.Banner)
        {
            state.PickingAvatar = false;
            state.PickingBanner = true;
            state.Open(NightPage.PhotoPick);
        }

        var pad = frame.Units(14f);
        var stack = new Stack(
            new Rect(new Vector2(area.Min.X + pad, hero.InfoTop), new Vector2(area.Max.X - pad, area.Max.Y)),
            StackAxis.Vertical, frame.Units(6f));
        DrawProfileIdentity(frame, ref stack, ProfileName(), state.Handle, ProfileMeta(),
            state.About.Length > 0 ? state.About : "Tap the pencil to write a bio.", night);
        DrawOwnProfileFacts(frame, ref stack, night);
        var acts = DrawProfileActions(frame, ref stack, following: false, state.DmsOpen, night);
        if (acts.Follow)
        {
            state.Open(NightPage.Followers);
        }

        if (acts.Contact)
        {
            state.Open(NightPage.Inbox);
        }

        var mine = pearl.Current.WatchedUserId == "me" ? pearl.Current.ProfilePosts : [];
        var likes = 0;
        for (var index = 0; index < mine.Length; index++)
        {
            likes += Math.Max(0, mine[index].Likes);
        }

        var stats = DrawSocialStats(frame, stack.Take(frame.Units(44f)), night,
            CompactCount(mine.Length),
            CompactCount(pearl.Current.Followers),
            CompactCount(pearl.Current.Following),
            CompactCount(likes));
        if (frame.Input.ConsumeClick(stats.Posts))
        {
            state.Open(NightPage.FeedWall);
        }

        if (frame.Input.ConsumeClick(stats.Followers))
        {
            state.Open(NightPage.Followers);
        }

        if (frame.Input.ConsumeClick(stats.Following))
        {
            state.Open(NightPage.Following);
        }

        if (frame.Input.ConsumeClick(stats.Likes))
        {
            state.Open(NightPage.Likes);
        }

        DrawProfileShelf(frame, ref stack, mine, own: true, night);
    }

    private void DrawHead(in AppletFrame frame, Rect strip)
    {
        var night = state.Night;
        var tone = VybeChrome.Tone(night);
        var snap = pearl.Current;
        frame.Input.Claim(strip);
        frame.Paint.Fill(strip, tone.Ground);
        frame.Paint.Fill(strip.BottomSlice(frame.Units(1f)), tone.Faint);
        var inner = strip.Inset(new Edges(frame.Units(14f), 0f));
        VybeChrome.Title(frame, inner.LeftSlice(inner.Width * 0.55f), "VYBE", night);
        var tools = inner.RightSlice(frame.Units(64f));
        var bell = tools.LeftSlice(frame.Units(28f));
        var gear = tools.RightSlice(frame.Units(28f));
        DrawHomeBell(frame, bell, tone.Ink);
        if (snap.Notes.Length + snap.UnreadTotal > 0)
        {
            frame.Paint.FillCircle(bell.Max - new Vector2(frame.Units(6f), frame.Units(18f)), frame.Units(4f),
                tone.Accent);
        }

        if (frame.Input.ConsumeClick(bell))
        {
            state.Open(NightPage.Alerts);
        }

        DrawHomeGear(frame, gear, tone.Ink);
        if (frame.Input.ConsumeClick(gear))
        {
            state.Open(NightPage.Settings);
        }
    }

    private void DrawNav(in AppletFrame frame, Rect strip)
    {
        var night = state.Night;
        var tone = VybeChrome.Tone(night);
        frame.Input.Claim(strip);
        frame.Paint.Fill(strip, tone.Ground);
        frame.Paint.Fill(strip.TopSlice(frame.Units(1f)), tone.Faint);
        var width = strip.Width / Tabs.Length;
        for (var index = 0; index < Tabs.Length; index++)
        {
            var cell = Rect.FromSize(new Vector2(strip.Min.X + width * index, strip.Min.Y),
                new Vector2(width, strip.Height));
            var on = state.Page == NightPage.Tabs && (int)state.Tab == index;
            var ink = on ? tone.Accent : tone.Mute;
            var glyphR = frame.Units(8.5f);
            var nameGap = frame.Units(1.5f);
            var labelH = frame.Units(13f);
            var pack = glyphR * 2f + nameGap + labelH;
            var top = MathF.Max((cell.Height - pack) * 0.5f, 0f);
            var glyphCenter = new Vector2(cell.Center.X, cell.Min.Y + top + glyphR);
            DrawTabGlyph(frame, glyphCenter, glyphR, index, ink);
            if (index == (int)NightTab.Messages && MessageNoticeCount() > 0)
            {
                frame.Paint.FillCircle(glyphCenter + new Vector2(glyphR * 0.85f, -glyphR * 0.75f),
                    frame.Units(3.6f), tone.Accent);
            }

            var label = new Rect(
                new Vector2(cell.Min.X, glyphCenter.Y + glyphR + nameGap),
                new Vector2(cell.Max.X, glyphCenter.Y + glyphR + nameGap + labelH));
            frame.Text.DrawIn(label, Tabs[index],
                new TextStyle(FontRole.Caption, ink, TextAlign.Center));
            if (frame.Input.WasClicked(cell))
            {
                state.Tab = (NightTab)index;
                state.Page = NightPage.Tabs;
                state.Scroll = 0f;
                state.ChatKey = string.Empty;
                if (index == 0)
                {
                    state.Hashtag = string.Empty;
                }
            }
        }
    }

    private void DrawHotCard(in AppletFrame frame, Rect area, ScenePerson person)
    {
        frame.Paint.Fill(area, person.Wash, frame.Units(16f));
        frame.Paint.FillGradient(area, new Vector4(0f, 0f, 0f, 0f), new Vector4(0f, 0f, 0f, 0.72f),
            GradientAxis.Vertical);
        frame.Text.DrawIn(area.BottomSlice(frame.Units(36f)).Inset(new Edges(frame.Units(8f), 0f, 0f,
                frame.Units(16f))), person.Name,
            new TextStyle(FontRole.BodyStrong, VybeChrome.Night.Ink));
        frame.Text.DrawIn(area.BottomSlice(frame.Units(16f)).Inset(new Edges(frame.Units(8f), 0f)), person.World,
            new TextStyle(FontRole.Caption, VybeChrome.Night.Mute));
        if (frame.Input.ConsumeClick(area))
        {
            OpenPerson(person.GateId);
        }
    }

    private void DrawDiscoverTile(in AppletFrame frame, Rect area, ScenePerson person)
    {
        var night = state.Night;
        frame.Paint.Fill(area, person.Wash, frame.Units(14f));
        VybeChrome.Title(frame, area.BottomSlice(frame.Units(32f)).Inset(new Edges(frame.Units(8f), 0f, 0f,
            frame.Units(14f))), person.Name, night);
        VybeChrome.Mute(frame, area.BottomSlice(frame.Units(14f)).Inset(new Edges(frame.Units(8f), 0f)),
            person.World, night);
        if (person.Online)
        {
            frame.Paint.FillCircle(new Vector2(area.Max.X - frame.Units(12f), area.Min.Y + frame.Units(12f)),
                frame.Units(4f), VybeChrome.Online);
        }

        if (frame.Input.ConsumeClick(area))
        {
            OpenPerson(person.GateId);
        }
    }

    private void DrawMayKnow(in AppletFrame frame, Rect area, ScenePerson person)
    {
        var night = state.Night;
        VybeChrome.Plate(frame, area, frame.Units(12f), night);
        var inner = area.Inset(frame.Units(8f));
        DrawFace(frame, inner.LeftSlice(frame.Units(40f)).Center, frame.Units(16f), person.AvatarUrl, person.Wash,
            night);
        var copy = inner.Inset(new Edges(frame.Units(48f), 0f, frame.Units(84f), 0f));
        VybeChrome.Title(frame, copy.TopSlice(frame.Units(20f)), person.Name, night);
        VybeChrome.Mute(frame, copy.BottomSlice(frame.Units(16f)), person.Handle + " · " + person.World, night);
        var linked = state.Connected.Contains(person.Id);
        var go = inner.RightSlice(frame.Units(80f)).Inset(new Edges(0f, frame.Units(10f)));
        VybeChrome.Primary(frame, go, linked ? "Message" : LinkVerb(night), night);
        if (frame.Input.ConsumeClick(go))
        {
            if (linked)
            {
                state.ChatIndex = person.Id;
                state.Open(NightPage.Chat);
            }
            else
            {
                state.FollowPerson(person, pearl, true);
            }

            return;
        }

        if (frame.Input.ConsumeClick(area))
        {
            OpenPerson(person.GateId);
        }
    }

    private void DrawInboxRow(in AppletFrame frame, Rect area, ScenePerson person)
    {
        var night = state.Night;
        var thread = state.Thread(person.Id);
        var preview = thread.Count > 0 ? ChatBits.Preview(thread[^1].Body) : "Say hello";
        var when = thread.Count > 0 ? TalkWhen(thread[^1].When) : "";
        var unread = thread.Count > 0 && !thread[^1].Mine;
        var key = VybeState.LocalTalkKey(person.Id);
        var dots = DrawTalkInbox(frame, area, person.Name, person.AvatarUrl, person.Wash, preview, string.Empty, when,
            unread, state.TalkStarred(key), night);
        if (OpenTalkMenu(frame, area, dots, key))
        {
            return;
        }

        if (frame.Input.ConsumeClick(area))
        {
            state.ChatIndex = person.Id;
            state.Open(NightPage.Chat);
        }
    }

    private Rect DrawTalkInbox(in AppletFrame frame, Rect area, string title, string avatar, Vector4 wash,
        string preview, string author, string when, bool unread, bool starred, bool night)
    {
        var tone = VybeChrome.Tone(night);
        frame.Paint.Fill(area, new Vector4(0.16f, 0.16f, 0.17f, 0.78f), frame.Units(14f));
        var face = area.LeftSlice(frame.Units(56f));
        DrawFace(frame, face.Center, frame.Units(20f), avatar, wash, night);
        var dots = area.RightSlice(frame.Units(40f));
        DrawTalkDots(frame, dots, tone.Mute);
        var meta = area.RightSlice(frame.Units(62f)).LeftSlice(frame.Units(36f));
        if (when.Length > 0)
        {
            frame.Text.DrawIn(meta.TopSlice(frame.Units(18f)), when,
                new TextStyle(FontRole.Caption, tone.Mute, TextAlign.Right));
        }

        if (unread)
        {
            var mark = new Vector2(meta.Center.X + frame.Units(6f), meta.Min.Y + frame.Units(34f));
            frame.Paint.FillCircle(mark, frame.Units(5.5f), tone.Accent);
            frame.Paint.FillCircle(mark, frame.Units(2.1f), Vector4.One);
        }

        var copy = area.Inset(new Edges(frame.Units(60f), frame.Units(12f), frame.Units(66f), frame.Units(12f)));
        var name = copy.TopSlice(frame.Units(22f));
        if (starred)
        {
            DrawTalkStar(frame, name.RightSlice(frame.Units(16f)), true);
            name = name.Inset(new Edges(0f, 0f, frame.Units(18f), 0f));
        }

        frame.Text.DrawEllipsized(name, title,
            new TextStyle(FontRole.BodyStrong, tone.Ink));
        var line = copy.BottomSlice(frame.Units(18f));
        if (author.Length > 0)
        {
            var who = author + ": ";
            var wide = MathF.Min(line.Width * 0.45f, frame.Text.Measure(who, FontRole.Caption).X);
            var whoBox = line.LeftSlice(wide);
            frame.Text.DrawEllipsized(whoBox, who,
                new TextStyle(FontRole.Caption, new Vector4(0.31f, 0.65f, 0.90f, 1f)));
            EmojiText.DrawEllipsized(frame, line.Inset(new Edges(wide, 0f, 0f, 0f)), preview, tone.Mute);
        }
        else
        {
            EmojiText.DrawEllipsized(frame, line, preview, tone.Mute);
        }

        return dots;
    }

    private bool OpenTalkMenu(in AppletFrame frame, Rect row, Rect dots, string key)
    {
        if (talkMenuEat)
        {
            if (frame.Input.ConsumeClick(row) ||
                frame.Input.ConsumeClick(row, PointerButton.Secondary) ||
                !frame.Input.IsHeld())
            {
                talkMenuEat = false;
            }

            return true;
        }

        if (talkMenu is not null)
        {
            return true;
        }

        if (frame.Input.ConsumeClick(dots) || frame.Input.ConsumeClick(row, PointerButton.Secondary))
        {
            talkMenu = new TalkMenu(key, frame.Input.Pointer);
            talkMenuSkip = true;
            return true;
        }

        return false;
    }

    private void DrawTalkMenu(in AppletFrame frame, Rect bounds, bool night)
    {
        if (talkMenuEat && talkMenu is null)
        {
            if (frame.Input.ConsumeClick(bounds) ||
                frame.Input.ConsumeClick(bounds, PointerButton.Secondary) ||
                !frame.Input.IsHeld())
            {
                talkMenuEat = false;
            }

            return;
        }

        if (talkMenu is not { } open)
        {
            return;
        }

        var tone = VybeChrome.Tone(night);
        var width = frame.Units(168f);
        var rowH = frame.Units(34f);
        var box = Rect.FromSize(
            new Vector2(
                Math.Clamp(open.At.X, bounds.Min.X, bounds.Max.X - width),
                Math.Clamp(open.At.Y, bounds.Min.Y, bounds.Max.Y - rowH * 2f - frame.Units(8f))),
            new Vector2(width, rowH * 2f + frame.Units(8f)));
        frame.Paint.Fill(box, tone.Card, frame.Units(10f));
        frame.Paint.Stroke(box, tone.Faint, frame.Units(1f), frame.Units(10f));
        var inner = box.Inset(frame.Units(4f));
        var star = inner.TopSlice(rowH);
        var drop = inner.BottomSlice(rowH);
        var favored = state.TalkStarred(open.Key);
        DrawTalkMenuRow(frame, star, favored ? "Unfavorite" : "Favorite", tone.Ink, tone.AccentDim);
        DrawTalkMenuRow(frame, drop, "Delete chat", tone.Danger, tone.Danger with { W = 0.16f });
        if (talkMenuSkip)
        {
            talkMenuSkip = false;
            frame.Input.Claim(box);
            return;
        }

        if (frame.Input.ConsumeClick(star))
        {
            state.ToggleTalkStar(open.Key);
            state.Save(paths);
            CloseTalkMenu();
            return;
        }

        if (frame.Input.ConsumeClick(drop))
        {
            state.HideTalk(open.Key);
            if (open.Key.StartsWith("p:", StringComparison.Ordinal) &&
                string.Equals(state.ChatKey, open.Key[2..], StringComparison.Ordinal))
            {
                state.ChatKey = string.Empty;
            }

            if (open.Key.StartsWith("l:", StringComparison.Ordinal) &&
                int.TryParse(open.Key.AsSpan(2), CultureInfo.InvariantCulture, out var personId))
            {
                state.Thread(personId).Clear();
                state.Connected.Remove(personId);
                if (state.ChatIndex == personId)
                {
                    state.ChatIndex = -1;
                }
            }

            state.Save(paths);
            CloseTalkMenu();
            return;
        }

        if (box.Contains(frame.Input.Pointer))
        {
            frame.Input.Claim(box);
            return;
        }

        if (frame.Input.ConsumeClick(bounds) || frame.Input.ConsumeClick(bounds, PointerButton.Secondary))
        {
            CloseTalkMenu();
        }
    }

    private void CloseTalkMenu()
    {
        talkMenu = null;
        talkMenuSkip = false;
        talkMenuEat = true;
    }

    private static void DrawTalkMenuRow(in AppletFrame frame, Rect area, string label, Vector4 ink, Vector4 hover)
    {
        if (frame.Input.IsHovering(area))
        {
            frame.Paint.Fill(area, hover, frame.Units(8f));
        }

        frame.Text.DrawIn(area.Inset(new Edges(frame.Units(10f), 0f)), label,
            new TextStyle(FontRole.CaptionStrong, ink));
    }

    private static void DrawTalkDots(in AppletFrame frame, Rect area, Vector4 ink)
    {
        var c = area.Center;
        var r = frame.Units(1.5f);
        var gap = frame.Units(5f);
        frame.Paint.FillCircle(c + new Vector2(0f, -gap), r, ink);
        frame.Paint.FillCircle(c, r, ink);
        frame.Paint.FillCircle(c + new Vector2(0f, gap), r, ink);
    }

    private static void DrawTalkStar(in AppletFrame frame, Rect area, bool on)
    {
        var gold = new Vector4(1f, 0.84f, 0.18f, 1f);
        var ink = on ? gold : frame.Theme.Palette.InkMuted;
        var texture = frame.Textures.FromFile(AppIconCatalog.Glyph(frame.Paths, "music-save.png"));
        if (texture is { IsReady: true })
        {
            frame.Paint.Image(texture, area.Inset(frame.Units(1f)), ink);
            return;
        }

        frame.Paint.FillCircle(area.Center, frame.Units(4f), ink);
    }

    private static string TalkAgo(long unix)
    {
        if (unix <= 0)
        {
            return string.Empty;
        }

        var delta = DateTimeOffset.UtcNow - DateTimeOffset.FromUnixTimeSeconds(unix);
        if (delta.TotalSeconds < 45)
        {
            return "Now";
        }

        if (delta.TotalMinutes < 60)
        {
            return ((int)delta.TotalMinutes).ToString(CultureInfo.InvariantCulture) + "m";
        }

        if (delta.TotalHours < 24)
        {
            return ((int)delta.TotalHours).ToString(CultureInfo.InvariantCulture) + "h";
        }

        return ((int)delta.TotalDays).ToString(CultureInfo.InvariantCulture) + "d";
    }

    private static string TalkWhen(string when)
    {
        if (when.Length == 0)
        {
            return string.Empty;
        }

        if (when.Equals("Yesterday", StringComparison.OrdinalIgnoreCase))
        {
            return "1d";
        }

        if (when.Equals("Now", StringComparison.OrdinalIgnoreCase))
        {
            return "Now";
        }

        return when;
    }

    private void DrawStoryRail(in AppletFrame frame, Rect stories, bool night)
    {
        var snap = pearl.Current;
        var tone = VybeChrome.Tone(night);
        var storyPeople = StoryPeople();
        var own = state.OwnStory.Length > 0 ? 1 : 0;
        var slots = 1 + own + storyPeople.Count;
        var storyW = MathF.Max(frame.Units(54f), stories.Width / Math.Max(slots, 1));
        var addCell = Rect.FromSize(stories.Min, new Vector2(storyW - frame.Units(6f), stories.Height));
        DrawStoryAdd(frame, addCell, night);
        if (frame.Input.ConsumeClick(addCell))
        {
            state.Caption = state.OwnStory;
            state.StoryMedia = string.Empty;
            state.Open(NightPage.StoryCompose);
        }

        var slot = 1;
        if (own == 1)
        {
            var self = Rect.FromSize(new Vector2(stories.Min.X + storyW, stories.Min.Y),
                new Vector2(storyW - frame.Units(6f), stories.Height));
            var seen = state.ViewedStories.Contains(-1);
            VybeChrome.StoryRing(frame, self.TopSlice(frame.Units(40f)).Center, frame.Units(16f), seen, night);
            DrawFace(frame, self.TopSlice(frame.Units(40f)).Center, frame.Units(14f), snap.MeAvatarUrl, tone.Accent,
                night);
            VybeChrome.Mute(frame, self.BottomSlice(frame.Units(16f)), "You", night);
            if (frame.Input.ConsumeClick(self))
            {
                state.StoryIndex = -1;
                state.ViewedStories.Add(-1);
                state.Open(NightPage.Story);
                state.Save(paths);
            }

            slot++;
        }

        for (var index = 0; index < storyPeople.Count; index++)
        {
            var person = storyPeople[index];
            var cell = Rect.FromSize(new Vector2(stories.Min.X + storyW * (slot + index), stories.Min.Y),
                new Vector2(storyW - frame.Units(6f), stories.Height));
            var seen = state.ViewedStories.Contains(person.Id);
            VybeChrome.StoryRing(frame, cell.TopSlice(frame.Units(40f)).Center, frame.Units(16f), seen, night);
            DrawFace(frame, cell.TopSlice(frame.Units(40f)).Center, frame.Units(14f), person.AvatarUrl, person.Wash,
                night);
            VybeChrome.Mute(frame, cell.BottomSlice(frame.Units(16f)),
                person.Handle.Length > 0 ? person.Handle.TrimStart('@') : person.Name, night);
            if (frame.Input.ConsumeClick(cell))
            {
                state.StoryIndex = person.Id;
                state.ViewedStories.Add(person.Id);
                state.Open(NightPage.Story);
                state.Save(paths);
            }
        }
    }

    private ScenePerson? FirstPassable()
    {
        for (var index = 0; index < state.Roster.Count; index++)
        {
            var person = state.Roster[index];
            if (!state.Connected.Contains(person.Id) && state.Passes(person))
            {
                return person;
            }
        }

        return null;
    }

    private void DrawMatchCard(in AppletFrame frame, Rect area, ScenePerson person, bool night)
    {
        var tone = VybeChrome.Tone(night);
        frame.Paint.Fill(area, person.Wash, frame.Units(18f));
        frame.Paint.FillGradient(area, new Vector4(0f, 0f, 0f, 0.05f), new Vector4(0f, 0f, 0f, 0.78f),
            GradientAxis.Vertical);
        var copy = area.Inset(new Edges(frame.Units(14f), 0f, frame.Units(14f), frame.Units(14f)));
        var name = copy.BottomSlice(frame.Units(86f)).TopSlice(frame.Units(24f));
        frame.Text.DrawEllipsized(name, person.Name,
            new TextStyle(FontRole.Title, VybeChrome.Night.Ink));
        VybeChrome.Mute(frame, copy.BottomSlice(frame.Units(62f)).TopSlice(frame.Units(16f)),
            person.World + (person.Online ? " · Online" : ""), night);
        var line = person.Line.Length > 0 ? person.Line : person.Handle;
        frame.Text.DrawEllipsized(copy.BottomSlice(frame.Units(46f)).TopSlice(frame.Units(16f)), line,
            new TextStyle(FontRole.Caption, VybeChrome.Night.Mute));
        var actions = copy.BottomSlice(frame.Units(36f));
        var pass = actions.LeftSlice(actions.Width * 0.46f);
        var like = actions.RightSlice(actions.Width * 0.46f);
        frame.Paint.Fill(pass, new Vector4(1f, 1f, 1f, 0.16f), pass.Height * 0.5f);
        frame.Text.DrawIn(pass, "Pass",
            new TextStyle(FontRole.CaptionStrong, VybeChrome.Night.Ink, TextAlign.Center));
        frame.Paint.Fill(like, tone.Accent, like.Height * 0.5f);
        frame.Text.DrawIn(like, LinkVerb(night),
            new TextStyle(FontRole.CaptionStrong, tone.AccentInk, TextAlign.Center));
        if (frame.Input.ConsumeClick(like))
        {
            state.FollowPerson(person, pearl, true);
            return;
        }

        if (frame.Input.ConsumeClick(pass))
        {
            return;
        }

        if (frame.Input.ConsumeClick(area))
        {
            OpenPerson(person.GateId);
        }
    }

    private static void DrawSearchGlyph(in AppletFrame frame, Vector2 center, float size, Vector4 ink)
    {
        var stroke = MathF.Max(1.3f, size * 0.18f);
        frame.Paint.StrokeCircle(center, size * 0.85f, ink, stroke);
        frame.Paint.Line(center + new Vector2(size * 0.62f, size * 0.62f),
            center + new Vector2(size * 1.15f, size * 1.15f), ink, stroke);
    }

    private static void DrawMailGlyph(in AppletFrame frame, Vector2 center, float size, Vector4 ink)
    {
        var stroke = MathF.Max(1.3f, size * 0.18f);
        var box = Rect.FromSize(center - new Vector2(size, size * 0.7f), new Vector2(size * 2f, size * 1.4f));
        frame.Paint.Stroke(box, ink, stroke, size * 0.22f);
        frame.Paint.Line(box.Min + new Vector2(size * 0.15f, size * 0.2f), center + new Vector2(0f, size * 0.15f), ink,
            stroke);
        frame.Paint.Line(center + new Vector2(0f, size * 0.15f), box.Max - new Vector2(size * 0.15f, size * 1.0f), ink,
            stroke);
    }

    private static void DrawStoryAdd(in AppletFrame frame, Rect area, bool night)
    {
        var tone = VybeChrome.Tone(night);
        frame.Paint.StrokeCircle(area.TopSlice(frame.Units(40f)).Center, frame.Units(16f), tone.Mute, frame.Units(1.4f));
        frame.Text.DrawIn(area.TopSlice(frame.Units(40f)), "+",
            new TextStyle(FontRole.Title, tone.Mute, TextAlign.Center));
        VybeChrome.Mute(frame, area.BottomSlice(frame.Units(16f)), "Add Story", night);
    }

    private static void DrawHomeBell(in AppletFrame frame, Rect area, Vector4 ink)
    {
        var size = MathF.Min(area.Width, area.Height) * 0.35f;
        var center = area.Center;
        frame.Paint.FillCircle(center + new Vector2(0f, -size * 0.68f), size * 0.11f, ink);
        var body = Rect.FromSize(center - new Vector2(size * 0.52f, size * 0.58f),
            new Vector2(size * 1.04f, size * 1.02f));
        frame.Paint.Fill(body, ink, size * 0.52f, Corner.Top);
        var lip = Rect.FromSize(center - new Vector2(size * 0.66f, -size * 0.30f),
            new Vector2(size * 1.32f, size * 0.22f));
        frame.Paint.Fill(lip, ink, size * 0.10f);
        frame.Paint.FillCircle(center + new Vector2(0f, size * 0.58f), size * 0.12f, ink);
    }

    private static void DrawHomeGear(in AppletFrame frame, Rect area, Vector4 ink)
    {
        var size = MathF.Min(area.Width, area.Height) * 0.414f;
        var side = size * 1.49f;
        var boxed = Rect.FromSize(area.Center - new Vector2(side * 0.5f, side * 0.5f), new Vector2(side, side));
        var texture = frame.Textures.FromFile(AppIconCatalog.Glyph(frame.Paths, "settings.png"));
        if (texture is not { IsReady: true })
        {
            texture = frame.Textures.FromFile(AppIconCatalog.Absolute(frame.Paths, "settings.png"));
        }

        if (texture is { IsReady: true })
        {
            var dest = CoverFit.Contained(texture.Size, CoverFit.InscribedSquare(boxed));
            if (!dest.IsEmpty)
            {
                frame.Paint.Image(texture, dest, ink);
                return;
            }
        }

        var stroke = MathF.Max(1.2f, size * 0.16f);
        var center = area.Center;
        frame.Paint.StrokeCircle(center, size * 0.36f, ink, stroke);
        frame.Paint.StrokeCircle(center, size * 0.14f, ink, stroke);
        for (var tooth = 0; tooth < 6; tooth++)
        {
            var angle = tooth * (MathF.PI / 3f);
            var dir = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
            frame.Paint.Line(center + dir * (size * 0.46f), center + dir * (size * 0.78f), ink, stroke);
        }
    }

    private static void DrawFilterGlyph(in AppletFrame frame, Vector2 center, float size, Vector4 ink)
    {
        var stroke = MathF.Max(1.3f, size * 0.18f);
        frame.Paint.Line(center + new Vector2(-size, -size * 0.5f), center + new Vector2(size, -size * 0.5f), ink,
            stroke);
        frame.Paint.Line(center + new Vector2(-size * 0.6f, 0f), center + new Vector2(size * 0.6f, 0f), ink, stroke);
        frame.Paint.Line(center + new Vector2(-size * 0.25f, size * 0.5f), center + new Vector2(size * 0.25f, size * 0.5f),
            ink, stroke);
    }

    private static void DrawTabGlyph(in AppletFrame frame, Vector2 center, float size, int tab, Vector4 ink)
    {
        var stroke = MathF.Max(1.3f, size * 0.18f);
        switch (tab)
        {
            case 0:
                if (DrawMusicTabGlyph(frame, center, size, "music-home.png", ink))
                {
                    return;
                }

                frame.Paint.Line(center + new Vector2(0f, -size), center + new Vector2(-size, size * 0.2f), ink, stroke);
                frame.Paint.Line(center + new Vector2(0f, -size), center + new Vector2(size, size * 0.2f), ink, stroke);
                frame.Paint.Line(center + new Vector2(-size * 0.7f, size), center + new Vector2(-size * 0.7f, size * 0.15f),
                    ink, stroke);
                frame.Paint.Line(center + new Vector2(size * 0.7f, size), center + new Vector2(size * 0.7f, size * 0.15f),
                    ink, stroke);
                break;
            case 1:
                frame.Paint.StrokeCircle(center, size * 0.7f, ink, stroke);
                frame.Paint.Line(center + new Vector2(size * 0.5f, size * 0.5f),
                    center + new Vector2(size * 0.95f, size * 0.95f), ink, stroke);
                break;
            case 2:
                if (DrawMusicTabGlyph(frame, center, size, "vybe-gallery.png", ink))
                {
                    return;
                }

                var frameBox = Rect.FromSize(center - new Vector2(size * 0.9f, size * 0.75f),
                    new Vector2(size * 1.8f, size * 1.5f));
                frame.Paint.Stroke(frameBox, ink, stroke, size * 0.22f);
                frame.Paint.FillCircle(center + new Vector2(size * 0.32f, -size * 0.22f), size * 0.16f, ink);
                frame.Paint.Line(center + new Vector2(-size * 0.7f, size * 0.45f),
                    center + new Vector2(-size * 0.1f, -size * 0.05f), ink, stroke);
                frame.Paint.Line(center + new Vector2(-size * 0.1f, -size * 0.05f),
                    center + new Vector2(size * 0.7f, size * 0.45f), ink, stroke);
                break;
            case 3:
                var mail = Rect.FromSize(center - new Vector2(size * 0.95f, size * 0.62f),
                    new Vector2(size * 1.9f, size * 1.24f));
                frame.Paint.Stroke(mail, ink, stroke, size * 0.22f);
                frame.Paint.Line(mail.Min + new Vector2(stroke, stroke),
                    center + new Vector2(0f, size * 0.18f), ink, stroke);
                frame.Paint.Line(new Vector2(mail.Max.X - stroke, mail.Min.Y + stroke),
                    center + new Vector2(0f, size * 0.18f), ink, stroke);
                break;
            default:
                if (DrawMusicTabGlyph(frame, center, size, "music-profile.png", ink))
                {
                    return;
                }

                frame.Paint.StrokeCircle(center + new Vector2(0f, -size * 0.25f), size * 0.4f, ink, stroke);
                frame.Paint.Stroke(Rect.FromSize(center + new Vector2(-size * 0.85f, size * 0.15f),
                    new Vector2(size * 1.7f, size * 0.7f)), ink, stroke, size * 0.7f);
                break;
        }
    }

    private static bool DrawMusicTabGlyph(in AppletFrame frame, Vector2 center, float size, string file, Vector4 ink)
    {
        var texture = frame.Textures.FromFile(AppIconCatalog.Glyph(frame.Paths, file));
        if (texture is not { IsReady: true })
        {
            return false;
        }

        var side = size * 2.2f;
        var dest = Rect.FromSize(center - new Vector2(side * 0.5f, side * 0.5f), new Vector2(side, side));
        frame.Paint.Image(texture, dest, ink);
        return true;
    }

    private void DrawModeMark(in AppletFrame frame, Rect area)
    {
        var mark = VybeChrome.Wordmark(frame, area, state.Night, state.HoldMark);
        state.TickHold(frame.Input.IsHovering(mark), frame.Input.IsHeld(), frame.DeltaSeconds);
    }

    private void DrawHashChip(in AppletFrame frame, Rect area, string tag, bool night)
    {
        if (VybeChrome.Chip(frame, area, tag, state.Hashtag == tag, night))
        {
            state.Hashtag = state.Hashtag == tag ? string.Empty : tag;
        }
    }

    private void DrawPearlCard(in AppletFrame frame, Rect area, PearlPost post)
    {
        var night = state.Night;
        var tone = VybeChrome.Tone(night);
        var inner = area.Inset(new Edges(0f, frame.Units(4f), 0f, frame.Units(4f)));
        var wash = tone.AccentDim;
        if (state.TryFindGate(post.AuthorId, out var author))
        {
            wash = author.Wash;
        }

        var avatarHit = inner.LeftSlice(frame.Units(32f)).TopSlice(frame.Units(32f));
        VybeChrome.StoryRing(frame, avatarHit.Center, frame.Units(13f), false, night);
        DrawFace(frame, avatarHit.Center, frame.Units(12f), post.AuthorAvatarUrl, wash, night);
        var grouped = VybeGroups.TryGroup(post, out var group);
        var handle = grouped
            ? group.Name
            : post.AuthorHandle.Length > 0
                ? (post.AuthorHandle.StartsWith('@') ? post.AuthorHandle : "@" + post.AuthorHandle)
                : string.Empty;
        var head = inner.Inset(new Edges(frame.Units(40f), 0f, frame.Units(52f), 0f)).TopSlice(frame.Units(32f));
        frame.Text.DrawEllipsized(head.TopSlice(frame.Units(16f)),
            grouped ? post.AuthorName + " · Group" : post.AuthorName + (handle.Length > 0 ? "  " + handle : string.Empty),
            new TextStyle(FontRole.CaptionStrong, tone.Ink));
        VybeChrome.Mute(frame, head.BottomSlice(frame.Units(14f)),
            grouped ? group.Name : handle, night);
        var when = inner.RightSlice(frame.Units(48f)).TopSlice(frame.Units(18f));
        frame.Text.DrawEllipsized(when, post.When,
            new TextStyle(FontRole.Caption, tone.Mute, TextAlign.Right));
        var cursor = frame.Units(36f);
        if (post.QuoteAuthor.Length > 0 || post.QuoteBody.Length > 0)
        {
            var quote = inner.Inset(new Edges(0f, cursor, 0f, 0f)).TopSlice(frame.Units(40f));
            VybeChrome.Glow(frame, quote, frame.Units(8f), false, night);
            VybeChrome.Kicker(frame, quote.TopSlice(frame.Units(14f)).Inset(new Edges(frame.Units(8f), 0f)),
                post.QuoteAuthor, night);
            VybeChrome.Mute(frame, quote.BottomSlice(frame.Units(22f)).Inset(new Edges(frame.Units(8f), 0f)),
                post.QuoteBody, night);
            cursor += frame.Units(44f);
        }

        if (post.Body.Length > 0)
        {
            var bodyH = PostBodyPixels(frame, post, inner.Width);
            frame.Text.DrawWrapped(inner.Inset(new Edges(0f, cursor, 0f, frame.Units(28f))).TopSlice(bodyH),
                post.Body, new TextStyle(FontRole.Caption, tone.Ink));
            cursor += bodyH + frame.Units(2f);
        }

        if (post.Media.Length > 0)
        {
            var plate = inner.Inset(new Edges(0f, cursor, 0f, frame.Units(28f))).TopSlice(frame.Units(120f));
            DrawMediaPlate(frame, plate, post, night);
            cursor += frame.Units(124f);
        }

        var bar = inner.BottomSlice(frame.Units(22f));
        var slot = bar.Width / 4f;
        var commentHit = Rect.FromSize(bar.Min, new Vector2(slot, bar.Height));
        var repostHit = Rect.FromSize(new Vector2(bar.Min.X + slot, bar.Min.Y), new Vector2(slot, bar.Height));
        var likeHit = Rect.FromSize(new Vector2(bar.Min.X + slot * 2f, bar.Min.Y), new Vector2(slot, bar.Height));
        var shareHit = Rect.FromSize(new Vector2(bar.Min.X + slot * 3f, bar.Min.Y), new Vector2(slot, bar.Height));
        DrawPostAction(frame, commentHit, "💬", post.Comments.ToString(), false, tone);
        DrawPostAction(frame, repostHit, "↻", post.Reposts.ToString(), post.Reposted, tone);
        DrawPostAction(frame, likeHit, post.Liked ? "♥" : "♡", post.Likes.ToString(), post.Liked, tone);
        DrawPostAction(frame, shareHit, "↗", string.Empty, false, tone);
        if (frame.Input.ConsumeClick(commentHit))
        {
            OpenPost(post.Id);
            return;
        }

        if (frame.Input.ConsumeClick(repostHit))
        {
            pearl.Repost(post.Id);
            return;
        }

        if (frame.Input.ConsumeClick(likeHit))
        {
            pearl.LikePost(post.Id, !post.Liked);
            return;
        }

        if (frame.Input.ConsumeClick(shareHit))
        {
            state.SharePostId = post.Id;
            return;
        }

        if (frame.Input.ConsumeClick(avatarHit) ||
            (frame.Input.ConsumeClick(head) && post.AuthorId.Length > 0))
        {
            OpenPerson(post.AuthorId);
            return;
        }

        if (frame.Input.ConsumeClick(area))
        {
            OpenPost(post.Id);
        }
    }

    private static void DrawPostAction(in AppletFrame frame, Rect area, string glyph, string count, bool on,
        NightPalette tone)
    {
        var label = count.Length > 0 ? glyph + " " + count : glyph;
        frame.Text.DrawIn(area, label,
            new TextStyle(FontRole.Caption, on ? tone.Accent : tone.Mute, TextAlign.Center));
    }

    private readonly record struct ProfileStatHits(Rect Posts, Rect Followers, Rect Following, Rect Likes);

    private static ProfileStatHits DrawSocialStats(in AppletFrame frame, Rect area, bool night, string posts,
        string followers, string following, string likes)
    {
        var w = area.Width / 4f;
        var postsHit = Rect.FromSize(area.Min, new Vector2(w, area.Height));
        var followersHit = Rect.FromSize(new Vector2(area.Min.X + w, area.Min.Y), new Vector2(w, area.Height));
        var followingHit = Rect.FromSize(new Vector2(area.Min.X + w * 2f, area.Min.Y), new Vector2(w, area.Height));
        var likesHit = Rect.FromSize(new Vector2(area.Min.X + w * 3f, area.Min.Y), new Vector2(w, area.Height));
        DrawStat(frame, postsHit, posts, "posts", night);
        DrawStat(frame, followersHit, followers, "followers", night);
        DrawStat(frame, followingHit, following, "following", night);
        DrawStat(frame, likesHit, likes, "likes", night);
        return new ProfileStatHits(postsHit, followersHit, followingHit, likesHit);
    }

    private static void DrawStat(in AppletFrame frame, Rect area, string value, string label, bool night)
    {
        var tone = VybeChrome.Tone(night);
        frame.Text.DrawIn(area.TopSlice(area.Height * 0.58f), value,
            new TextStyle(FontRole.BodyStrong, tone.Ink, TextAlign.Left));
        frame.Text.DrawIn(area.BottomSlice(area.Height * 0.42f), label,
            new TextStyle(FontRole.Caption, tone.Mute, TextAlign.Left));
    }

    private static string CompactCount(int value)
    {
        if (value >= 1_000_000)
        {
            return (value / 1_000_000f).ToString("0.#", CultureInfo.InvariantCulture) + "M";
        }

        if (value >= 1000)
        {
            return (value / 1000f).ToString("0.#", CultureInfo.InvariantCulture) + "K";
        }

        return value.ToString(CultureInfo.InvariantCulture);
    }

    private List<ScenePerson> StoryPeople()
    {
        var list = new List<ScenePerson>();
        var seen = new HashSet<int>();
        var extras = VybeDemo.People(paths);
        for (var index = 0; index < extras.Length; index++)
        {
            var person = extras[index];
            if (!state.Blocked.Contains(person.Id) && seen.Add(person.Id))
            {
                list.Add(person);
            }
        }

        foreach (var person in state.Roster)
        {
            if (state.Blocked.Contains(person.Id) ||
                !seen.Add(person.Id) ||
                (!state.LiveStories.Contains(person.Id) && person.Line.Length == 0))
            {
                continue;
            }

            list.Add(person);
        }

        return list;
    }

    private void OpenPost(string postId)
    {
        if (postId.Length == 0)
        {
            return;
        }

        state.PostKey = postId;
        pearl.WatchPost(postId);
        state.Open(NightPage.Post);
    }

    private void OpenPerson(string userId)
    {
        if (userId.Length == 0)
        {
            return;
        }

        state.PersonKey = userId;
        if (state.TryFindGate(userId, out var person))
        {
            state.PersonIndex = person.Id;
        }

        pearl.WatchProfile(userId);
        state.Open(NightPage.Person);
    }

    private PearlPost[] HomeBoard()
    {
        VybeGroups.Seed(state);
        if (state.FeedPick == FeedPick.Groups)
        {
            return groupWall;
        }

        return BoardFeed();
    }

    private PearlPost[] BoardFeed()
    {
        var live = pearl.Current.Feed;
        if (live.Length == 0)
        {
            return demoWall;
        }

        var packed = new PearlPost[demoWall.Length + live.Length];
        demoWall.CopyTo(packed, 0);
        live.CopyTo(packed, demoWall.Length);
        return packed;
    }

    private PearlPost? BoardPost(string id)
    {
        if (id.Length == 0)
        {
            return null;
        }

        var live = pearl.PostById(id);
        if (live is not null)
        {
            return live;
        }

        for (var index = 0; index < demoWall.Length; index++)
        {
            if (string.Equals(demoWall[index].Id, id, StringComparison.Ordinal))
            {
                return demoWall[index];
            }
        }

        for (var index = 0; index < groupWall.Length; index++)
        {
            if (string.Equals(groupWall[index].Id, id, StringComparison.Ordinal))
            {
                return groupWall[index];
            }
        }

        return null;
    }

    private PearlPost[] PersonBoard(string gateId)
    {
        var theirs = new List<PearlPost>();
        for (var index = 0; index < demoWall.Length; index++)
        {
            if (string.Equals(demoWall[index].AuthorId, gateId, StringComparison.Ordinal))
            {
                theirs.Add(demoWall[index]);
            }
        }

        if (string.Equals(pearl.Current.WatchedUserId, gateId, StringComparison.Ordinal))
        {
            theirs.AddRange(pearl.Current.ProfilePosts);
        }

        return theirs.ToArray();
    }

    private IEnumerable<PearlPost> VisibleFeed(PearlPost[] posts)
    {
        foreach (var post in posts)
        {
            if (state.Hashtag.Length > 0 &&
                post.Body.IndexOf(state.Hashtag.TrimStart('#'), StringComparison.OrdinalIgnoreCase) < 0)
            {
                continue;
            }

            if (state.Search.Length > 0 &&
                post.Body.IndexOf(state.Search, StringComparison.OrdinalIgnoreCase) < 0 &&
                post.AuthorName.IndexOf(state.Search, StringComparison.OrdinalIgnoreCase) < 0)
            {
                continue;
            }

            yield return post;
        }
    }

    private static List<string> LiveHashes(PearlPost[] posts)
    {
        var tags = new List<string>();
        foreach (var post in posts)
        {
            var body = post.Body;
            var from = 0;
            while (from < body.Length)
            {
                var hash = body.IndexOf('#', from);
                if (hash < 0)
                {
                    break;
                }

                var end = hash + 1;
                while (end < body.Length && char.IsLetterOrDigit(body[end]))
                {
                    end++;
                }

                if (end > hash + 1)
                {
                    var tag = body[hash..end];
                    if (!tags.Contains(tag, StringComparer.OrdinalIgnoreCase) && tags.Count < 4)
                    {
                        tags.Add(tag);
                    }
                }

                from = end;
            }
        }

        return tags;
    }

    private static float PostBodyPixels(in AppletFrame frame, PearlPost post, float wrapWidth)
    {
        var measured = frame.Text.MeasureWrapped(post.Body, FontRole.Caption, MathF.Max(1f, wrapWidth)).Y;
        return MathF.Max(frame.Units(36f), measured + frame.Units(4f));
    }

    private static float PostCardHeight(in AppletFrame frame, PearlPost post)
    {
        var height = 92f;
        if (post.QuoteBody.Length > 0 || post.QuoteAuthor.Length > 0)
        {
            height += 44f;
        }

        if (post.Body.Length > 0)
        {
            var wrap = MathF.Max(frame.Units(40f), frame.Content.Width - frame.Units(16f));
            height += PostBodyPixels(frame, post, wrap) / MathF.Max(frame.Scale, 0.01f) + 2f;
        }

        if (post.Media.Length > 0)
        {
            height += 124f;
        }

        return height;
    }

    private readonly record struct ProfileHeroHits(
        float InfoTop, bool Banner, bool Face, bool Flag, bool Edit, bool Back);

    private ProfileHeroHits DrawProfileHero(in AppletFrame frame, Rect area, bool night, string bannerPath,
        Vector4 bannerWash, string faceUrl, string facePath, Vector4 faceWash, bool edit, bool back)
    {
        var tone = VybeChrome.Tone(night);
        var pad = frame.Units(14f);
        var coverH = frame.Units(124f);
        var cover = new Rect(new Vector2(area.Min.X, area.Min.Y),
            new Vector2(area.Max.X, area.Min.Y + coverH));
        if (!DrawBleedStill(frame, cover, bannerPath))
        {
            frame.Paint.Fill(cover, bannerWash);
        }

        var faceR = frame.Units(40f);
        var faceCenter = new Vector2(area.Min.X + pad + faceR, cover.Max.Y + faceR * 0.42f);
        frame.Paint.FillCircle(faceCenter, faceR + frame.Units(4f), tone.Ground);
        DrawFace(frame, faceCenter, faceR, faceUrl, faceWash, night, facePath);
        frame.Paint.StrokeCircle(faceCenter, faceR, tone.Faint, frame.Units(2f));
        var faceHit = Rect.FromSize(faceCenter - new Vector2(faceR, faceR), new Vector2(faceR * 2f));

        var tool = frame.Units(32f);
        var gap = frame.Units(8f);
        var tools = Rect.FromSize(
            new Vector2(area.Max.X - pad - tool * (edit ? 2f : 1f) - (edit ? gap : 0f),
                cover.Max.Y + frame.Units(10f)),
            new Vector2(tool * (edit ? 2f : 1f) + (edit ? gap : 0f), tool));
        var flag = edit ? tools.LeftSlice(tool) : tools;
        var editHit = edit ? tools.RightSlice(tool) : Rect.Empty;
        VybeChrome.ReportFlag(frame, flag, tone.Ink);
        if (edit)
        {
            VybeChrome.EditMark(frame, editHit, tone.Ink);
        }

        var backHit = Rect.Empty;
        var wentBack = false;
        if (back)
        {
            backHit = Rect.FromSize(new Vector2(cover.Min.X + frame.Units(8f), cover.Min.Y + frame.Units(8f)),
                new Vector2(frame.Units(36f), frame.Units(32f)));
            frame.Text.DrawIn(backHit, "‹", new TextStyle(FontRole.Title, tone.Ink, TextAlign.Center));
            wentBack = frame.Input.ConsumeClick(backHit);
        }

        var flagged = frame.Input.ConsumeClick(flag);
        var edited = edit && frame.Input.ConsumeClick(editHit);
        var faced = frame.Input.ConsumeClick(faceHit);
        var bannered = !faced && !flagged && !edited && !wentBack && frame.Input.ConsumeClick(cover);
        var infoTop = MathF.Max(faceCenter.Y + faceR, tools.Max.Y) + frame.Units(12f);
        _ = backHit;
        return new ProfileHeroHits(infoTop, bannered, faced, flagged, edited, wentBack);
    }

    private bool DrawBleedStill(in AppletFrame frame, Rect area, string path)
    {
        if (path.Length == 0 || !File.Exists(path))
        {
            return false;
        }

        var texture = frame.Textures.FromFile(path);
        if (texture is not { IsReady: true })
        {
            return false;
        }

        var uv = CoverFit.Uv(texture.Size, area.Size);
        frame.Paint.Image(texture, area, uv.Min, uv.Max, Vector4.One);
        return true;
    }

    private static readonly string[] ProfileReportReasons =
    {
        "Select reason",
        "Spam",
        "Harassment or bullying",
        "Hate speech",
        "Inappropriate content",
        "Impersonation",
        "Scam or fraud",
        "Something else",
    };

    private void OpenProfileReport(string target, string title)
    {
        state.ReportOpen = true;
        state.ReportReason = 0;
        state.ReportDetail = string.Empty;
        state.ReportTarget = target;
        state.ReportTitle = title;
    }

    private void CloseProfileReport()
    {
        state.ReportOpen = false;
        state.ReportReason = 0;
        state.ReportDetail = string.Empty;
        state.ReportTarget = string.Empty;
        state.ReportTitle = string.Empty;
    }

    private void SubmitProfileReport()
    {
        if (state.ReportReason <= 0 || desk.Busy)
        {
            return;
        }

        var reason = ProfileReportReasons[Math.Clamp(state.ReportReason, 1, ProfileReportReasons.Length - 1)];
        var body = "Profile: " + (state.ReportTitle.Length > 0 ? state.ReportTitle : "Unknown") +
                   "\nId: " + state.ReportTarget +
                   (state.ReportDetail.Trim().Length > 0 ? "\n\n" + state.ReportDetail.Trim() : string.Empty);
        desk.Send(new FeedbackNote("VYBE profile report · " + reason, body, game.Character.Name,
            game.Character.WorldName));
        CloseProfileReport();
    }

    private void DrawProfileReport(in AppletFrame frame, Rect area)
    {
        var night = state.Night;
        var tone = VybeChrome.Tone(night);
        frame.Paint.Fill(area, new Vector4(0f, 0f, 0f, 0.62f));
        var card = Rect.FromSize(
            new Vector2(area.Min.X + frame.Units(8f), area.Center.Y - frame.Units(168f)),
            new Vector2(area.Width - frame.Units(16f), frame.Units(300f)));
        VybeChrome.Plate(frame, card, frame.Units(16f), night);
        var stack = new Stack(card.Inset(frame.Units(14f)), StackAxis.Vertical, frame.Units(8f));
        VybeChrome.Title(frame, stack.Take(frame.Units(26f)), "Report", night);
        VybeChrome.Mute(frame, stack.Take(frame.Units(16f)), "Select reason", night);
        var reason = stack.Take(frame.Units(40f));
        frame.Paint.Fill(reason, tone.CardHi, frame.Units(10f));
        state.ReportReason = frame.TextField.Combo("vybe-report-reason", reason.Inset(frame.Units(6f)),
            ProfileReportReasons, state.ReportReason);
        VybeChrome.Mute(frame, stack.Take(frame.Units(28f)), "Add additional information (optional)", night);
        var note = stack.Take(frame.Units(72f));
        frame.Paint.Fill(note, tone.CardHi, frame.Units(10f));
        state.ReportDetail = frame.TextField.Write("vybe-report-detail", note.Inset(frame.Units(8f)),
            state.ReportDetail, "Add additional information (optional)", 800);
        var row = stack.Take(frame.Units(40f));
        var cancel = row.LeftSlice(row.Width * 0.48f);
        var send = row.RightSlice(row.Width * 0.48f);
        VybeChrome.Glow(frame, cancel, frame.Units(12f), false, night);
        frame.Text.DrawIn(cancel, "Cancel",
            new TextStyle(FontRole.CaptionStrong, tone.Ink, TextAlign.Center));
        var ready = state.ReportReason > 0 && !desk.Busy;
        VybeChrome.Primary(frame, send, desk.Busy ? "Sending…" : "Submit", night);
        if (frame.Input.WasClicked(cancel) ||
            (!card.Contains(frame.Input.Pointer) && frame.Input.WasClicked(area)))
        {
            frame.TextField.Release();
            CloseProfileReport();
            return;
        }

        if (ready && frame.Input.ConsumeClick(send))
        {
            SubmitProfileReport();
        }
    }

    private bool DrawLocalStill(in AppletFrame frame, Rect area, string path, bool night)
    {
        if (path.Length == 0 || !File.Exists(path))
        {
            return false;
        }

        var texture = frame.Textures.FromFile(path);
        if (texture is not { IsReady: true })
        {
            return false;
        }

        var uv = CoverFit.Uv(texture.Size, area.Size);
        frame.Paint.ImageRounded(texture, area, uv.Min, uv.Max, Vector4.One, frame.Units(16f));
        _ = night;
        return true;
    }

    private void DrawFace(in AppletFrame frame, Vector2 center, float radius, string url, Vector4 wash, bool night,
        string localPath = "")
    {
        if (localPath.Length > 0 && File.Exists(localPath))
        {
            var texture = frame.Textures.FromFile(localPath);
            if (texture is { IsReady: true })
            {
                var dest = Rect.FromSize(center - new Vector2(radius, radius),
                    new Vector2(radius * 2f, radius * 2f));
                var handsetFace = HandsetLook.PortraitFile(paths, badges);
                var crop = badges.PortraitFile.Length > 0 &&
                           string.Equals(localPath, handsetFace, StringComparison.OrdinalIgnoreCase)
                    ? CoverFit.Framed(texture.Size, dest.Size, badges.PortraitZoom, badges.PortraitFocus)
                    : CoverFit.Uv(texture.Size, dest.Size);
                frame.Paint.ImageRounded(texture, dest, crop.Min, crop.Max, Vector4.One, radius);
                return;
            }
        }

        if (url.Length > 0 && File.Exists(url))
        {
            var texture = frame.Textures.FromFile(url);
            if (texture is { IsReady: true })
            {
                var dest = Rect.FromSize(center - new Vector2(radius, radius),
                    new Vector2(radius * 2f, radius * 2f));
                var uv = CoverFit.Uv(texture.Size, dest.Size);
                frame.Paint.ImageRounded(texture, dest, uv.Min, uv.Max, Vector4.One, radius);
                return;
            }
        }

        if (url.Length > 0)
        {
            pearl.PrefetchMedia(url);
            var path = pearl.LocalMedia(url);
            if (path is { Length: > 0 })
            {
                var texture = frame.Textures.FromFile(path);
                if (texture is { IsReady: true })
                {
                    var dest = Rect.FromSize(center - new Vector2(radius, radius),
                        new Vector2(radius * 2f, radius * 2f));
                    var uv = CoverFit.Uv(texture.Size, dest.Size);
                    frame.Paint.ImageRounded(texture, dest, uv.Min, uv.Max, Vector4.One, radius);
                    return;
                }
            }
        }

        VybeChrome.Portrait(frame, center, radius, wash, night);
    }

    private void DrawMediaPlate(in AppletFrame frame, Rect area, PearlPost post, bool night)
    {
        var count = Math.Min(4, post.Media.Length);
        if (count <= 0)
        {
            return;
        }

        var tiles = new Rect[count];
        if (count == 1)
        {
            tiles[0] = area;
        }
        else if (count == 2)
        {
            tiles[0] = area.LeftSlice(area.Width * 0.5f - frame.Units(2f));
            tiles[1] = area.RightSlice(area.Width * 0.5f - frame.Units(2f));
        }
        else
        {
            tiles[0] = area.LeftSlice(area.Width * 0.5f - frame.Units(2f));
            var right = area.RightSlice(area.Width * 0.5f - frame.Units(2f));
            tiles[1] = right.TopSlice(right.Height * 0.5f - frame.Units(2f));
            tiles[2] = right.BottomSlice(right.Height * 0.5f - frame.Units(2f));
            if (count == 4)
            {
                tiles[0] = area.LeftSlice(area.Width * 0.5f - frame.Units(2f)).TopSlice(area.Height * 0.5f - frame.Units(2f));
                tiles[3] = area.LeftSlice(area.Width * 0.5f - frame.Units(2f))
                    .BottomSlice(area.Height * 0.5f - frame.Units(2f));
            }
        }

        for (var index = 0; index < count; index++)
        {
            DrawStill(frame, tiles[index], post.Media[index].Url, night);
            if (frame.Input.ConsumeClick(tiles[index]))
            {
                state.ViewMedia = post.Media[index].Url;
                state.PostKey = post.Id;
                state.Open(NightPage.PhotoView);
            }
        }
    }

    private void DrawStill(in AppletFrame frame, Rect area, string url, bool night)
    {
        VybeChrome.Plate(frame, area, frame.Units(8f), night);
        if (url.Length == 0)
        {
            return;
        }

        ITextureHandle? texture = null;
        if (File.Exists(url))
        {
            texture = frame.Textures.FromFile(url);
        }
        else
        {
            pearl.PrefetchMedia(url);
            var path = pearl.LocalMedia(url);
            if (path is { Length: > 0 })
            {
                texture = frame.Textures.FromFile(path);
            }
        }

        if (texture is { IsReady: true })
        {
            var uv = CoverFit.Uv(texture.Size, area.Size);
            frame.Paint.ImageRounded(texture, area, uv.Min, uv.Max, Vector4.One, frame.Units(8f));
        }
    }

    private void DrawProfileShelf(in AppletFrame frame, ref Stack stack, PearlPost[] posts, bool own, bool night)
    {
        var panes = stack.Take(frame.Units(40f));
        var next = VybeChrome.ProfileMarks(frame, panes, Math.Clamp(state.ProfilePane, 0, 3), night);
        if (next != state.ProfilePane)
        {
            state.ProfilePane = next;
            state.Scroll = 0f;
        }

        switch (state.ProfilePane)
        {
            case 1:
                DrawPhotoGrid(frame, stack, posts, night);
                return;
            case 2:
                DrawProfileGroups(frame, ref stack, own, night);
                return;
            case 3:
                DrawSavedShelf(frame, ref stack, night);
                return;
            default:
                if (posts.Length == 0)
                {
                    VybeChrome.Mute(frame, stack.Take(frame.Units(36f)),
                        own
                            ? pearl.Current.SignedIn ? "Your posts will land here."
                                : "Sign in from You to show your posts."
                            : "No posts yet.",
                        night);
                    return;
                }

                foreach (var wallPost in posts)
                {
                    DrawPearlCard(frame, stack.Take(frame.Units(PostCardHeight(frame, wallPost))), wallPost);
                }

                return;
        }
    }

    private void DrawProfileGroups(in AppletFrame frame, ref Stack stack, bool own, bool night)
    {
        VybeGroups.Seed(state);
        var tone = VybeChrome.Tone(night);
        var rows = new List<SceneGroup>();
        for (var index = 0; index < VybeGroups.Catalog.Length; index++)
        {
            var group = VybeGroups.Catalog[index];
            if (own ? VybeGroups.Joined(state, group.Id) : group.Suggested)
            {
                rows.Add(group);
            }
        }

        if (rows.Count == 0)
        {
            VybeChrome.Mute(frame, stack.Take(frame.Units(36f)),
                own ? "Join a group and it lands here." : "No public groups yet.", night);
            return;
        }

        for (var index = 0; index < rows.Count; index++)
        {
            DrawGroupRow(frame, stack.Take(frame.Units(68f)), rows[index], tone);
        }
    }

    private void DrawSavedShelf(in AppletFrame frame, ref Stack stack, bool night)
    {
        var kept = SavedBoard();
        if (kept.Length == 0)
        {
            VybeChrome.Mute(frame, stack.Take(frame.Units(36f)), "Saved posts land here.", night);
            return;
        }

        foreach (var wallPost in kept)
        {
            DrawPearlCard(frame, stack.Take(frame.Units(PostCardHeight(frame, wallPost))), wallPost);
        }
    }

    private PearlPost[] SavedBoard()
    {
        var hits = new List<PearlPost>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var post in BoardFeed())
        {
            if (!post.Liked)
            {
                continue;
            }

            if (seen.Add(post.Id))
            {
                hits.Add(post);
            }
        }

        return hits.ToArray();
    }

    private void DrawPhotoGrid(in AppletFrame frame, Stack stack, PearlPost[] posts, bool night)
    {
        var shots = new List<(string Url, string PostId)>();
        foreach (var post in posts)
        {
            foreach (var media in post.Media)
            {
                if (media.Url.Length > 0)
                {
                    shots.Add((media.Url, post.Id));
                }
            }
        }

        if (shots.Count == 0)
        {
            VybeChrome.Mute(frame, stack.Take(frame.Units(36f)), "No photos yet.", night);
            return;
        }

        var gap = frame.Units(3f);
        var cell = (stack.Remaining.Width - gap * 2f) / 3f;
        for (var index = 0; index < shots.Count; index += 3)
        {
            var row = stack.Take(cell);
            for (var col = 0; col < 3 && index + col < shots.Count; col++)
            {
                var shot = Rect.FromSize(
                    new Vector2(row.Min.X + (cell + gap) * col, row.Min.Y), new Vector2(cell, cell));
                DrawStill(frame, shot, shots[index + col].Url, night);
                if (frame.Input.ConsumeClick(shot))
                {
                    state.ViewMedia = shots[index + col].Url;
                    state.PostKey = shots[index + col].PostId;
                    state.Open(NightPage.PhotoView);
                }
            }
        }
    }

    private void DrawShareSheet(in AppletFrame frame, Rect body)
    {
        var night = state.Night;
        var sheet = body.BottomSlice(frame.Units(220f));
        frame.Paint.Fill(body, new Vector4(0f, 0f, 0f, 0.35f));
        VybeChrome.Plate(frame, sheet, frame.Units(16f), night);
        var stack = new Stack(sheet.Inset(frame.Units(12f)), StackAxis.Vertical, frame.Units(8f));
        VybeChrome.Title(frame, stack.Take(frame.Units(24f)), night ? "Share" : "Repost", night);
        var post = BoardPost(state.SharePostId);
        if (VybeChrome.Chip(frame, stack.Take(frame.Units(36f)), night ? "Share to feed" : "Repost", false, night) &&
            post is not null)
        {
            pearl.Repost(post.Value.Id);
            state.SharePostId = string.Empty;
        }

        if (VybeChrome.Chip(frame, stack.Take(frame.Units(36f)), "Quote", false, night) && post is not null)
        {
            state.QuoteOf = post.Value.Id;
            state.Caption = string.Empty;
            state.DraftMedia.Clear();
            state.SharePostId = string.Empty;
            state.Open(NightPage.Compose);
        }

        if (VybeChrome.Chip(frame, stack.Take(frame.Units(36f)), "Send", false, night))
        {
            state.Open(NightPage.ShareSend);
        }

        if (VybeChrome.Chip(frame, stack.Take(frame.Units(36f)), "Story", false, night) && post is not null)
        {
            state.Caption = post.Value.Body;
            state.StoryMedia = post.Value.Media.Length > 0 ? post.Value.Media[0].Url : string.Empty;
            state.SharePostId = string.Empty;
            state.Open(NightPage.StoryCompose);
        }

        if (VybeChrome.Chip(frame, stack.Take(frame.Units(32f)), "Cancel", false, night) ||
            frame.Input.ConsumeClick(body.TopSlice(body.Height - sheet.Height)))
        {
            state.SharePostId = string.Empty;
        }
    }

    private int MessageNoticeCount()
    {
        var unread = state.Incoming.Count;
        foreach (var chat in pearl.Current.Chats)
        {
            unread += chat.UnreadCount;
        }

        return unread;
    }

    private static string LinkVerb(bool night) => night ? "Connect" : "Follow";

    private static string LinkedVerb(bool night) => night ? "Connected" : "Following";

    private readonly record struct TalkMenu(string Key, Vector2 At);
}
