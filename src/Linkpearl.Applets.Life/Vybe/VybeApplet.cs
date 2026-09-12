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
using Linkpearl.Time;
using Linkpearl.Applets.Life.Calendar;

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

    public bool Allowed => true;

    private bool RaceLocked =>
        VybeChrome.IsLalafell(game.RaceId, game.TribeId, game.RaceName, game.TribeName);

    private static readonly string[] Tabs = { "Home", "Discover", "Gallery", "Messages", "Profile" };
    private static readonly string[] TabKeys =
    {
        "vybe.tab.home", "vybe.tab.discover", "vybe.tab.gallery", "vybe.tab.messages", "vybe.tab.profile",
    };

    private readonly IGameSession game;
    private readonly HostPaths paths;
    private readonly IPearlHub pearl;
    private readonly DisplayPreferences display;
    private readonly BadgeBook badges;
    private readonly IFilePicker files;
    private readonly IGifDesk gifs;
    private readonly ILifestream lifestream;
    private readonly IFeedbackDesk desk;
    private readonly HostEnvironment environment;
    private readonly CalendarBook calendar;
    private readonly ChatMarks marks;
    private readonly VybeState state;
    private readonly VybeBook book;
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
    private float hottPlusTravel;
    private int hottLane;
    private float storyTravel;
    private bool storyLane;
    private PearlPost[] demoWall = [];
    private PearlPost[] groupWall = [];
    private string chatAnchor = "";
    private bool photoDrag;
    private bool photoWait;
    private string laneDrag = string.Empty;
    private float laneHold;
    private float viewZoom = 1f;
    private Vector2 viewFocus = new(0.5f, 0.5f);
    private string viewLook = "";
    private bool viewDrag;

    public VybeApplet(IGameSession game, HostPaths paths, IPearlHub pearl, DisplayPreferences display,
        BadgeBook badges, HandsetProfileDesk profiles, IFilePicker files, ILifestream lifestream,
        IFeedbackDesk desk, IGifDesk gifs, HostEnvironment environment, CalendarBook calendar, ChatMarks marks)
    {
        this.game = game;
        this.paths = paths;
        this.pearl = pearl;
        this.display = display;
        this.badges = badges;
        this.files = files;
        this.gifs = gifs;
        this.lifestream = lifestream;
        this.desk = desk;
        this.environment = environment;
        this.calendar = calendar;
        this.marks = marks;
        book = VybeBook.Load(paths);
        state = VybeState.Load(paths, game.Character.Name);
        state.Sit(book);
        profiles.Add(this);
        if (state.UsesHandsetIdentity || state.UsesHandsetProfile)
        {
            AcceptHandsetProfile(HandsetName(), HandsetLook.Honorific(display));
        }
    }

    private string HandsetName()
    {
        var linked = ShownName.Linked(game.Character.Name, pearl.Current.MeName);
        var name = HandsetLook.Name(display, linked,
            GlassName.IsPatron(badges, pearl.Current, display, environment.IsDevelopment));
        return name.Length > 0 ? name : game.Character.Name;
    }

    private string ProfileName()
    {
        var linked = ShownName.Linked(game.Character.Name, pearl.Current.MeName);
        return ShownName.Preferred(display, linked, state.DisplayName, "You", FancyName());
    }

    private string EditableName()
    {
        var linked = ShownName.Linked(game.Character.Name, pearl.Current.MeName);
        var handset = ShownName.Source(display, linked, FancyName());
        var stored = state.DisplayName.Trim();
        if (stored.Length > 0)
        {
            return stored;
        }

        return handset.Length > 0 ? handset : "You";
    }

    private string ProfileHonorific()
    {
        var honor = state.Honorific.Trim();
        return honor.Length > 0 ? honor : HandsetLook.Honorific(display);
    }

    private string ProfileMeta() => state.Pronouns.Trim();

    private static string SocialHandle(string handle)
    {
        var trimmed = handle.Trim();
        if (trimmed.Length == 0)
        {
            return string.Empty;
        }

        return trimmed.StartsWith("@", StringComparison.Ordinal) ? trimmed : "@" + trimmed;
    }

    private void EnsureVybeSeat()
    {
        if (state.HasAccount)
        {
            return;
        }

        var snap = pearl.Current;
        var name = HandsetName();
        if (name.Length == 0)
        {
            name = snap.MeName.Length > 0 ? snap.MeName : game.Character.Name;
        }

        if (name.Length == 0)
        {
            return;
        }

        if (name.Length > 24)
        {
            name = name[..24];
        }

        var handle = snap.MeHandle.Length > 0 ? snap.MeHandle : name;
        if (book.TryHandle(handle, out var seat))
        {
            profileStamp++;
            state.EnterSeat(seat);
            state.Save(paths);
            return;
        }

        var pass = book.TryJoin(name, handle);
        if (!pass.Ok || pass.Seat is null)
        {
            return;
        }

        book.Save(paths);
        profileStamp++;
        state.EnterSeat(pass.Seat);
        state.Save(paths);
    }

    private void DrawProfileIdentity(in AppletFrame frame, ref Stack stack, string name, string handle, string meta,
        string about, bool night, bool plusMember = false, string time = "", string honorific = "")
    {
        var tone = VybeChrome.Tone(night);
        var honor = honorific.Trim();
        if (honor.Length > 0)
        {
            var honorH = MathF.Max(frame.Units(16f), frame.Text.LineHeight(FontRole.CaptionStrong));
            DrawFlowHonor(frame, stack.Take(honorH), honor, night);
        }

        var nameH = MathF.Max(frame.Units(22f), frame.Text.LineHeight(FontRole.Title));
        var nameRow = stack.Take(nameH);
        if (plusMember)
        {
            var tagW = MathF.Min(VybeChrome.PlusTagWidth(frame), nameRow.Width * 0.38f);
            DrawFlowName(frame, nameRow.Inset(new Edges(0f, 0f, tagW + frame.Units(6f), 0f)), name, night);
            VybeChrome.PlusTag(frame, nameRow.RightSlice(tagW).Inset(new Edges(0f, frame.Units(3f))));
        }
        else
        {
            DrawFlowName(frame, nameRow, name, night);
        }
        var tag = SocialHandle(handle);
        if (tag.Length > 0)
        {
            var handleH = MathF.Max(frame.Units(20f), frame.Text.LineHeight(FontRole.Body));
            frame.Text.DrawEllipsized(stack.Take(handleH), tag,
                new TextStyle(FontRole.Body, tone.Mute));
        }

        if (meta.Length > 0)
        {
            frame.Text.DrawEllipsized(stack.Take(frame.Units(15f)), meta,
                new TextStyle(FontRole.Caption, tone.Mute));
        }

        if (time.Length > 0)
        {
            frame.Text.DrawEllipsized(stack.Take(frame.Units(15f)), time,
                new TextStyle(FontRole.Caption, tone.Mute));
        }

        if (about.Length > 0)
        {
            frame.Text.DrawWrapped(stack.Take(frame.Units(40f)), about,
                new TextStyle(FontRole.Caption, tone.Ink));
        }
    }

    private void DrawOwnProfileFacts(in AppletFrame frame, ref Stack stack, bool night) =>
        DrawProfileFacts(frame, ref stack, JoinShown(state.Genders, night), JoinShown(state.Sexualities, night),
            state.DmsOpen, JoinShown(state.Intents, night), state.Relationship, night, state.Race);

    private void DrawLaneFold(in AppletFrame frame, ref Stack stack, int[]? marks, bool night, string key)
    {
        if (!string.Equals(state.LaneMapKey, key, StringComparison.Ordinal))
        {
            state.LaneMapKey = key;
            state.LaneMapOpen = false;
        }

        if (VybeLaneMap.DrawFold(frame, ref stack, marks, night, state.LaneMapOpen))
        {
            state.LaneMapOpen = !state.LaneMapOpen;
        }
    }

    private static void DrawProfileFacts(in AppletFrame frame, ref Stack stack, string gender, string sexuality,
        bool? dmsOpen, string intent, string relationship, bool night, string race = "")
    {
        var tone = VybeChrome.Tone(night);
        DrawFactRow(frame, ref stack, "Race", race, tone);
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
        if (primary)
        {
            VybeChrome.WashFill(frame, area, radius);
            frame.Text.DrawIn(area, label,
                new TextStyle(FontRole.BodyStrong, Vector4.One, TextAlign.Center));
            return;
        }

        VybeChrome.WashFill(frame, area, radius);
        frame.Text.DrawIn(area, label,
            new TextStyle(FontRole.BodyStrong, Vector4.One, TextAlign.Center));
        _ = tone;
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
        GlassName.IsPatron(badges, pearl.Current, display, environment.IsDevelopment);

    private void DrawFlowName(in AppletFrame frame, Rect area, string name, bool night) =>
        NameMark.DrawName(frame, area, name, display, VybeChrome.Tone(night).Ink, FancyName(), nameClock);

    private void DrawFlowHonor(in AppletFrame frame, Rect area, string honorific, bool night) =>
        NameMark.DrawTitle(frame, area, honorific, display, VybeChrome.Tone(night).Ink, FancyName(), nameClock);

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

        var title = ShownName.ClampTitle(honorific).Trim();
        if (title.Length > 0)
        {
            state.Honorific = title;
        }
        var face = HandsetLook.CopyPortrait(paths, state.ProfileFacePath, badges, "afterdark-profile-face");
        state.ProfileFacePath = face.Path;
        state.FaceZoom = face.Zoom;
        state.FaceFocusX = face.FocusX;
        state.FaceFocusY = face.FocusY;
        var banner = HandsetLook.CopyBanner(paths, state.ProfileBannerPath, display, "afterdark-profile-banner");
        state.ProfileBannerPath = banner.Path;
        state.BannerZoom = banner.Zoom;
        state.BannerFocusX = banner.FocusX;
        state.BannerFocusY = banner.FocusY;
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
        EnsureVybeSeat();
        if (!state.HasAccount)
        {
            state.Page = state.OnAuthSheet ? state.Page : NightPage.Auth;
            return;
        }

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

    public bool CanGoBack =>
        marks.Busy || marks.HasReply || talkLook.Length > 0 || talkAlbum ||
        (!state.HasAccount
            ? state.Page is NightPage.AuthLogin or NightPage.AuthCreate
            : state.Page != NightPage.Tabs);

    public bool Back()
    {
        if (marks.Dismiss())
        {
            return true;
        }

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

        if (!state.HasAccount)
        {
            if (state.Page is NightPage.AuthLogin or NightPage.AuthCreate)
            {
                state.AuthNote = string.Empty;
                state.Page = NightPage.Auth;
                state.Scroll = 0f;
                return true;
            }

            return false;
        }

        if (state.Page == NightPage.Tabs)
        {
            return false;
        }

        if (state.Page == NightPage.OnboardIdentity && !state.Onboarded)
        {
            SignOutVybe();
            return true;
        }

        if (state.Page is NightPage.OnboardReady or NightPage.OnboardAbout or NightPage.OnboardIntent)
        {
            state.Page = NightPage.OnboardIdentity;
            state.Scroll = 0f;
            return true;
        }

        if (state.Page == NightPage.LaneSurvey && state.LaneAsk > 0 && !state.LaneDone)
        {
            state.LaneAsk--;
            state.Scroll = 0f;
            return true;
        }

        state.Back();
        return true;
    }

    private void SyncOwnRace()
    {
        var named = SceneBook.NamedRace(game.RaceId, game.RaceName);
        if (VybeChrome.IsLalafell(state.Race))
        {
            state.Race = named;
            return;
        }

        if (state.Race.Length == 0 && named.Length > 0)
        {
            state.Race = named;
        }
    }

    public void Compose(in AppletFrame frame)
    {
        nameClock += frame.DeltaSeconds;
        state.DropExpiredStory();
        if (state.Page is NightPage.Story or NightPage.StoryComments && state.StoryIndex < 0 &&
            !state.HasOwnStory)
        {
            state.Page = NightPage.Tabs;
        }

        if (RaceLocked)
        {
            VybeChrome.Stage(frame);
            DrawRaceLock(frame, frame.Content.Inset(frame.Units(20f)));
            return;
        }

        state.Bind(pearl.Current);
        VybeDemo.Seed(state, paths, !demoHearts, game.Character.WorldName);
        demoHearts = true;
        demoWall = VybeDemo.Posts(paths);

        groupWall = VybeClubs.Wall(state, VybeGroups.Posts(paths));

        state.PlusBlocked = RaceLocked;
        SyncOwnRace();
        if (state.PlusBlocked && state.Night)
        {
            state.Mode = SocialMode.Daylight;
            state.Save(paths);
        }

        EnsureVybeSeat();

        if (!state.Night)
        {
            state.HidePlusLanes();
        }

        if (!state.HasAccount && !state.OnAuthSheet)
        {
            state.Page = NightPage.Auth;
        }
        else if (state.HasAccount && state.OnAuthSheet)
        {
            state.Page = state.Onboarded ? NightPage.Tabs : NightPage.OnboardIdentity;
        }
        else if (state.HasAccount && !state.Onboarded &&
                 state.Page is NightPage.OnboardIntent or NightPage.OnboardAbout)
        {
            state.Page = NightPage.OnboardReady;
            state.Scroll = 0f;
        }

        VybeChrome.Stage(frame);
        FinishPhotoPick(frame);
        if (state.Page == NightPage.PlacePhoto)
        {
            DrawPlacePhoto(frame, frame.Content);
            return;
        }

        if (state.OnAuthSheet)
        {
            DrawAuth(frame, frame.Content.Inset(frame.Units(16f)));
        }
        else if (state.Page == NightPage.Gate)
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
        else if (state.Page == NightPage.LaneSurvey)
        {
            DrawLaneSurvey(frame, frame.Content.Inset(frame.Units(16f)));
        }
        else if (state.Page == NightPage.PostComments)
        {
            DrawPostComments(frame, frame.Content);
        }
        else
        {
            var chrome = frame.Units(60f);
            var nav = frame.Units(60f);
            if (state.Page == NightPage.Chat)
            {
                DrawChat(frame, frame.Content.Inset(new Edges(frame.Units(10f), frame.Units(8f), frame.Units(10f),
                    frame.Units(10f))));
                var tone = VybeChrome.Tone(state.Night);
                marks.DrawMenu(frame, frame.Content, tone.Ink, tone.Mute, tone.Accent, tone.Card);
            }
            else if (state.Page == NightPage.PhotoView)
            {
                DrawPhotoView(frame, frame.Content);
            }
            else
            {
            DrawHead(frame, frame.Content.TopSlice(chrome));
            DrawNav(frame, frame.Content.BottomSlice(nav));
            var side = ContentBleed() ? 0f : frame.Units(14f);
            var top = ContentBleed() ? chrome : chrome + frame.Units(8f);
            var body = frame.Content.Inset(new Edges(side, top, side,
                nav + frame.Units(8f)));
            var clip = new Rect(new Vector2(frame.Content.Min.X, body.Min.Y),
                new Vector2(frame.Content.Max.X, body.Max.Y));
            if (state.Page == NightPage.Compose)
            {
                frame.Paint.PushClip(body);
                DrawStack(frame, body);
                frame.Paint.PopClip();
            }
            else if (state.Page != NightPage.Tabs)
            {
                VybeChrome.Wheel(frame, body, state,
                    state.Page == NightPage.Filters
                        ? frame.Units(state.Night ? 3800f : 3200f)
                        : state.Page == NightPage.Gallery
                            ? GalleryBoardHeight(frame, body.Width)
                            : state.Page == NightPage.Person
                                ? frame.Units(state.Night && state.LaneMapOpen ? 1680f : 1200f)
                            : state.Page == NightPage.Settings
                                ? frame.Units(state.PlusAgreed ? 2100f : 1600f)
                            : frame.Units(900f));
                frame.Paint.PushClip(clip);
                DrawStack(frame, body.Translate(new Vector2(0f, -state.Scroll)));
                frame.Paint.PopClip();
            }
            else
            {
                DrawTabBody(frame, body, clip);
            }

            DrawTalkMenu(frame, body, state.Night);

            if (state.SharePostId.Length > 0 && state.Page != NightPage.ShareSend)
            {
                DrawShareSheet(frame, frame.Content.Inset(new Edges(frame.Units(14f), chrome, frame.Units(14f),
                    nav + frame.Units(8f))));
            }

            if (state.DropPostId.Length > 0)
            {
                DrawDropPostSheet(frame, frame.Content.Inset(new Edges(frame.Units(14f), chrome, frame.Units(14f),
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

    private static void DrawRaceLock(in AppletFrame frame, Rect area)
    {
        var tone = VybeChrome.Tone(true);
        var stack = new Stack(area, StackAxis.Vertical, frame.Units(12f));
        stack.Take(frame.Units(36f));
        VybeChrome.LockMark(frame, stack.Take(frame.Units(80f)));
        frame.Text.DrawIn(stack.Take(frame.Units(28f)), "You've been locked out",
            new TextStyle(FontRole.Title, tone.Ink, TextAlign.Center));
        frame.Text.DrawWrapped(stack.Take(frame.Units(72f)),
            "VYBE does not allow Lalafell appearances, including Plainsfolk and Dunesfolk. This lock is only temporary.",
            new TextStyle(FontRole.Body, tone.Ink, TextAlign.Center));
        frame.Text.DrawWrapped(stack.Take(frame.Units(56f)),
            "Change back to any other race and access returns right away. Nothing is saved from this lockout.",
            new TextStyle(FontRole.Caption, tone.Mute, TextAlign.Center));
    }

    private void DrawTabBody(in AppletFrame frame, Rect body, Rect clip)
    {
        VybeChrome.Wheel(frame, body, state,
            state.Tab == NightTab.Discover && state.DiscoverPane == 0
                ? frame.Units(4200f)
                : state.Tab == NightTab.Discover && state.DiscoverPane == 2
                    ? frame.Units(2400f)
                : state.Tab == NightTab.Discover && state.DiscoverPane == 3
                    ? frame.Units(1100f)
                : state.Tab == NightTab.Gallery
                    ? GalleryBoardHeight(frame, body.Width)
                : state.Tab == NightTab.Profile
                    ? frame.Units(state.Night && state.LaneDone && state.LaneMapOpen ? 2100f : 1680f)
                : state.Tab == NightTab.Home
                    ? frame.Units(state.Night ? 2520f : 2240f)
                    : frame.Units(1400f));
        frame.Paint.PushClip(clip);
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
        DrawWelcomeCard(frame, stack.Take(frame.Units(86f)), night);

        VybeChrome.Title(frame, stack.Take(frame.Units(20f)), "Stories", night);
        DrawStoryRail(frame, stack.Take(frame.Units(132f)), night);

        var hotHead = stack.Take(frame.Units(22f));
        VybeChrome.Title(frame, hotHead.LeftSlice(hotHead.Width * 0.62f), "Who's Hot", night);
        frame.Text.DrawIn(hotHead.RightSlice(frame.Units(56f)), "See all",
            new TextStyle(FontRole.CaptionStrong, tone.Accent, TextAlign.Right));
        if (frame.Input.ConsumeClick(hotHead.RightSlice(frame.Units(56f))))
        {
            state.DiscoverPane = 0;
            state.PeoplePlus = false;
            state.Tab = NightTab.Discover;
            state.Scroll = 0f;
        }

        DrawHottRail(frame, stack.Take(frame.Units(220f)), night, plusLane: false);

        if (night)
        {
            var plusHead = stack.Take(frame.Units(22f));
            VybeChrome.Title(frame, plusHead.LeftSlice(plusHead.Width * 0.62f), "VYBE+ People", night);
            frame.Text.DrawIn(plusHead.RightSlice(frame.Units(56f)), "See all",
                new TextStyle(FontRole.CaptionStrong, tone.Accent, TextAlign.Right));
            if (frame.Input.ConsumeClick(plusHead.RightSlice(frame.Units(56f))))
            {
                state.DiscoverPane = 0;
                state.PeoplePlus = true;
                state.Tab = NightTab.Discover;
                state.Scroll = 0f;
            }

            DrawHottRail(frame, stack.Take(frame.Units(220f)), night, plusLane: true);
        }

        VybeChrome.Kicker(frame, stack.Take(frame.Units(14f)), "CREATE A POST", night);
        DrawComposeCue(frame, stack.Take(frame.Units(64f)), night, night && state.FeedPick == FeedPick.Plus);
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
                state.FeedPick == FeedPick.Plus
                    ? state.PlusBlocked
                        ? "VYBE+ is not available on Lalafell characters."
                        : "No VYBE+ posts yet."
                    : state.FeedPick == FeedPick.Groups
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
        var radius = frame.Units(18f);
        var live = pearl.Current.SignedIn;
        var pip = live ? VybeChrome.Online : new Vector4(0.52f, 0.52f, 0.56f, 1f);
        var glass = new Vector4(0.045f, 0.045f, 0.055f, 0.58f);

        frame.Paint.Fill(area, glass, radius);
        frame.Paint.PushClip(area);
        frame.Paint.FillGradient(area.LeftSlice(area.Width * 0.62f), tone.Accent with { W = 0.12f },
            tone.Accent with { W = 0f }, GradientAxis.Horizontal);
        frame.Paint.Glow(area.LeftSlice(frame.Units(56f)).Inset(frame.Units(8f)), tone.Accent with { W = 0.22f },
            frame.Units(16f), frame.Units(10f));
        frame.Paint.PopClip();
        frame.Paint.Stroke(area, new Vector4(1f, 1f, 1f, 0.10f), frame.Units(1f), radius);

        var inner = area.Inset(new Edges(frame.Units(12f), frame.Units(10f)));
        var faceR = frame.Units(22f);
        var face = Rect.FromSize(new Vector2(inner.Min.X, inner.Center.Y - faceR),
            new Vector2(faceR * 2f, faceR * 2f));
        var facePath = OwnFacePath();
        if (VybeChrome.StillReady(facePath))
        {
            DrawFace(frame, face.Center, faceR, string.Empty, tone.Accent, night, facePath);
        }
        else
        {
            VybeChrome.EmptyPortrait(frame, face.Center, faceR, night);
            VybeChrome.AddPlus(frame, face.Center, faceR * 0.32f, night);
        }

        frame.Paint.StrokeCircle(face.Center, faceR, tone.Accent, frame.Units(1.8f));
        var pipAt = face.Center + new Vector2(faceR * 0.70f, faceR * 0.70f);
        frame.Paint.FillCircle(pipAt, frame.Units(5.4f), new Vector4(0.04f, 0.04f, 0.05f, 1f));
        frame.Paint.FillCircle(pipAt, frame.Units(3.4f), pip);

        var status = live ? PhoneLanguages.T("vybe.online") : PhoneLanguages.T("vybe.away");
        var statusW = frame.Text.Measure(status, FontRole.Caption).X + frame.Units(28f);
        var trailW = statusW + frame.Units(16f);
        var trail = inner.RightSlice(trailW);
        frame.Paint.FillCircle(new Vector2(trail.Min.X + frame.Units(4f), trail.Center.Y), frame.Units(2.6f), pip);
        frame.Text.DrawEllipsized(
            new Rect(new Vector2(trail.Min.X + frame.Units(12f), trail.Min.Y),
                new Vector2(trail.Max.X - frame.Units(14f), trail.Max.Y)),
            status, new TextStyle(FontRole.Caption, live ? pip : tone.Mute));
        frame.Text.DrawIn(trail.RightSlice(frame.Units(12f)), "›",
            new TextStyle(FontRole.Caption, new Vector4(1f, 1f, 1f, 0.38f), TextAlign.Center));

        var copy = inner.Inset(new Edges(faceR * 2f + frame.Units(10f), frame.Units(2f), trailW + frame.Units(6f),
            frame.Units(2f)));
        var rows = new Stack(copy, StackAxis.Vertical, frame.Units(2f));
        frame.Text.DrawEllipsized(rows.Take(frame.Units(13f)), PhoneLanguages.T("vybe.welcome"),
            new TextStyle(FontRole.Caption, tone.Mute));
        DrawFlowName(frame, rows.Take(frame.Units(22f)), WelcomeName(), night);
        frame.Text.DrawEllipsized(rows.Take(frame.Units(16f)),
            night ? PhoneLanguages.T("vybe.ready.night") : PhoneLanguages.T("vybe.ready.day"),
            new TextStyle(FontRole.Caption, new Vector4(1f, 1f, 1f, 0.62f)));

        if (frame.Input.ConsumeClick(face))
        {
            OpenOwnStill(face: true);
        }
        else if (frame.Input.ConsumeClick(area))
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

    private bool CanPostPlus() => state.Night && state.PlusAgreed && !state.PlusBlocked;

    private void BeginCompose(bool plusPreferred, bool keepQuote = false)
    {
        if (!keepQuote)
        {
            state.QuoteOf = string.Empty;
        }

        state.Caption = string.Empty;
        state.DraftMedia.Clear();
        state.DraftTags.Clear();
        state.DraftTagDraft = string.Empty;
        state.DraftDescriptors.Clear();
        state.DraftDescOpen = false;
        state.DraftSheet = ComposeSheet.None;
        state.DraftSfwOk = false;
        state.DraftStory = false;
        state.DraftStoryPermanent = false;
        state.AudienceEveryone = true;
        state.DraftPlus = plusPreferred && CanPostPlus();
        if (VybeClubs.TryFind(state, state.ActingAsClubId, out var actingClub) && actingClub.PlusOnly)
        {
            state.DraftPlus = CanPostPlus();
            state.DraftStory = false;
        }
        if (state.QuoteOf.Length > 0 && BoardPost(state.QuoteOf) is { } cited && VybeDemo.IsPlusPost(cited))
        {
            state.DraftPlus = CanPostPlus();
        }

        state.DraftRating = state.DraftPlus ? ContentRating.None : ContentRating.Sfw;
        state.Open(NightPage.Compose);
    }

    private void DrawComposeCue(in AppletFrame frame, Rect area, bool night, bool plusHint)
    {
        var tone = VybeChrome.Tone(night);
        VybeChrome.PostSheet(frame, area);
        var inner = area.Inset(new Edges(frame.Units(10f), frame.Units(8f)));
        var faceR = frame.Units(16f);
        var asClub = VybeClubs.TryFind(state, state.ActingAsClubId, out var cueClub);
        DrawFace(frame, new Vector2(inner.Min.X + faceR, inner.Center.Y), faceR,
            asClub ? cueClub.FacePath : OwnFacePath().Length > 0 ? string.Empty : pearl.Current.MeAvatarUrl,
            tone.Accent, night, asClub ? cueClub.FacePath : OwnFacePath());
        var go = inner.RightSlice(frame.Units(96f)).Inset(new Edges(frame.Units(4f), frame.Units(6f)));
        var send = VybeChrome.ComposeSend(frame, go,
            asClub ? "Post as " + cueClub.Name : plusHint ? "Post to VYBE+" : "Post to VYBE", true,
            plusHint || (asClub && cueClub.PlusOnly));
        var field = inner.Inset(new Edges(faceR * 2f + frame.Units(8f), frame.Units(6f), frame.Units(104f),
            frame.Units(6f)));
        frame.Paint.Fill(field, new Vector4(1f, 1f, 1f, 0.05f), field.Height * 0.5f);
        frame.Paint.Stroke(field, new Vector4(1f, 1f, 1f, 0.10f), frame.Units(1f), field.Height * 0.5f);
        frame.Text.DrawEllipsized(field.Inset(new Edges(frame.Units(12f), 0f)),
            plusHint ? "What's the vibe tonight?" : "What's the Vybe?",
            new TextStyle(FontRole.Caption, new Vector4(1f, 1f, 1f, 0.55f)));
        if (send || frame.Input.ConsumeClick(area))
        {
            BeginCompose(plusHint);
        }
    }

    private PearlPost[] OwnPosted(bool night)
    {
        var keep = new List<PearlPost>();
        for (var index = 0; index < state.Posted.Count; index++)
        {
            var post = state.Posted[index];
            if (IsGroupPost(post))
            {
                continue;
            }

            if ((night || !VybeDemo.IsPlusPost(post)) && !PearlGone(post.Id))
            {
                keep.Add(post);
            }
        }

        return keep.ToArray();
    }

    private bool CommitStory()
    {
        var media = state.DraftMedia.Count > 0 ? state.DraftMedia[0] : state.StoryMedia;
        if (state.Caption.Trim().Length == 0 && media.Length == 0)
        {
            return false;
        }

        state.OwnStory = state.Caption.Trim();
        state.StoryMedia = media;
        state.OwnStoryPermanent = false;
        state.OwnStoryAtUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        state.OwnStoryPlace = game.ZoneName.Trim();
        state.OwnStoryComments.Clear();
        pearl.PublishStory(state.OwnStory, media);
        state.Caption = string.Empty;
        state.QuoteOf = string.Empty;
        state.DraftMedia.Clear();
        state.DraftTags.Clear();
        state.DraftTagDraft = string.Empty;
        state.DraftDescriptors.Clear();
        state.DraftSheet = ComposeSheet.None;
        state.DraftSfwOk = false;
        state.DraftStory = false;
        state.DraftPlus = false;
        state.ViewedStories.Remove(-1);
        state.StoryIndex = -1;
        state.Page = NightPage.Tabs;
        state.Tab = NightTab.Home;
        if (state.OwnStory.Length > 0 || media.Length > 0)
        {
            state.Open(NightPage.Story);
        }

        state.Save(paths);
        return true;
    }

    private bool CommitPost()
    {
        if (state.DraftStory)
        {
            return CommitStory();
        }

        if (state.Caption.Length == 0 && state.DraftMedia.Count == 0 && state.QuoteOf.Length == 0)
        {
            return false;
        }

        if (!VybePostTags.HasAny(state.DraftTags))
        {
            return false;
        }

        var plus = state.DraftPlus;
        if (plus && !VybePostMark.PlusRatingPicked(state.DraftRating))
        {
            return false;
        }
        if (plus && !CanPostPlus())
        {
            if (state.PlusBlocked)
            {
                return false;
            }

            state.Open(NightPage.Gate);
            return false;
        }

        plus = plus && CanPostPlus();
        if (VybeClubs.TryFind(state, state.ActingAsClubId, out var acting) && acting.PlusOnly)
        {
            plus = CanPostPlus();
        }

        var quoted = BoardPost(state.QuoteOf);
        if (quoted is { } cited && VybeDemo.IsPlusPost(cited) && !plus)
        {
            return false;
        }

        var media = new PearlMedia[Math.Min(4, state.DraftMedia.Count)];
        for (var index = 0; index < media.Length; index++)
        {
            media[index] = new PearlMedia("mine-media-" + index, state.DraftMedia[index], 0, 0);
        }

        var marks = VybePostTags.Join(state.DraftTags);
        var body = state.Caption.Trim();
        if (marks.Length > 0)
        {
            body = body.Length == 0 ? marks : body + "\n" + marks;
        }

        var stamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString(CultureInfo.InvariantCulture);
        var rating = plus ? state.DraftRating : ContentRating.Sfw;
        var clubId = VybeClubs.TryFind(state, state.ActingAsClubId, out var club) ? club.Id : string.Empty;
        var authorId = clubId.Length > 0 ? "club:" + clubId : "me";
        var authorName = clubId.Length > 0 ? club.Name : ProfileName();
        var authorHandle = clubId.Length > 0 ? VybeClubs.Handle(clubId) : state.Handle;
        var authorFace = clubId.Length > 0
            ? club.FacePath
            : OwnFacePath().Length > 0 ? OwnFacePath() : pearl.Current.MeAvatarUrl;
        var post = new PearlPost(
            (plus ? "plus-mine-" : "mine-") + stamp,
            authorId,
            authorName,
            authorHandle,
            authorFace,
            body,
            "now",
            true,
            false,
            0,
            0,
            0,
            false,
            quoted?.Id ?? string.Empty,
            quoted?.AuthorName ?? string.Empty,
            quoted?.Body ?? string.Empty,
            media,
            plus ? VybePostMark.Plus : VybePostMark.Vybe,
            VybePostMark.Wire(rating),
            plus ? state.DraftDescriptors.ToArray() : [],
            state.DraftTags.ToArray());

        if (clubId.Length > 0 && VybeClubs.TryFind(state, clubId, out var owner))
        {
            owner.Posts.Insert(0, post);
            state.Save(paths);
        }
        else
        {
            state.Posted.Insert(0, post);
        }

        if (!plus)
        {
            pearl.PublishPost(body, state.AudienceEveryone, state.DraftMedia.ToArray(), state.QuoteOf);
        }

        state.Caption = string.Empty;
        state.QuoteOf = string.Empty;
        state.DraftMedia.Clear();
        state.DraftTags.Clear();
        state.DraftTagDraft = string.Empty;
        state.DraftDescriptors.Clear();
        state.DraftSheet = ComposeSheet.None;
        state.DraftSfwOk = false;
        state.DraftRating = ContentRating.Sfw;
        state.DraftPlus = false;
        state.DraftStory = false;
        state.Page = NightPage.Tabs;
        state.Tab = NightTab.Home;
        if (clubId.Length > 0)
        {
            state.FeedPick = FeedPick.Groups;
        }
        else if (plus)
        {
            state.FeedPick = FeedPick.Plus;
        }

        pearl.WatchFeed(state.FeedEveryone ? "foryou" : "following");
        return true;
    }

    private void DrawHottRail(in AppletFrame frame, Rect area, bool night, bool plusLane)
    {
        var people = HottPeople(plusLane);
        if (people.Count == 0)
        {
            VybeChrome.Mute(frame, area, plusLane ? "No VYBE+ people yet." : "No one nearby yet.", night);
            return;
        }

        var gap = frame.Units(8f);
        var cardW = (area.Width - gap) * 0.5f;
        var span = people.Count * (cardW + gap) - gap;
        var max = MathF.Max(0f, span - area.Width);
        var travel = plusLane ? state.HottPlusDrag : state.HottDrag;
        var lane = plusLane ? 2 : 1;
        if (frame.Input.IsHeld() && hottLane == 0 && frame.Input.IsHovering(area))
        {
            hottLane = lane;
        }

        if (hottLane == lane && frame.Input.IsHeld())
        {
            if (plusLane)
            {
                hottPlusTravel += MathF.Abs(frame.Input.PointerDelta.X);
            }
            else
            {
                hottTravel += MathF.Abs(frame.Input.PointerDelta.X);
            }

            travel = Math.Clamp(travel - frame.Input.PointerDelta.X, 0f, max);
        }
        else
        {
            travel = Math.Clamp(travel, 0f, max);
        }

        if (plusLane)
        {
            state.HottPlusDrag = travel;
        }
        else
        {
            state.HottDrag = travel;
        }

        frame.Paint.PushClip(area);
        var x = area.Min.X - travel;
        for (var index = 0; index < people.Count; index++)
        {
            var cell = Rect.FromSize(new Vector2(x, area.Min.Y), new Vector2(cardW, area.Height));
            if (cell.Max.X > area.Min.X && cell.Min.X < area.Max.X)
            {
                DrawHottCard(frame, cell, people[index], night, true, plusLane);
            }

            x += cardW + gap;
        }

        frame.Paint.PopClip();
        if (!frame.Input.IsHeld())
        {
            var swipe = plusLane ? hottPlusTravel : hottTravel;
            if (swipe >= frame.Units(12f))
            {
                frame.Input.ConsumeClick(area);
            }

            if (plusLane)
            {
                hottPlusTravel = 0f;
            }
            else
            {
                hottTravel = 0f;
            }

            if (hottLane == lane)
            {
                hottLane = 0;
            }
        }
    }

    private List<ScenePerson> HottPeople(bool plusLane)
    {
        var picks = new List<ScenePerson>();
        var seen = new HashSet<int>();
        var here = game.Character.WorldName;
        for (var index = 0; index < state.Roster.Count; index++)
        {
            var person = state.Roster[index];
            if (!HottFits(person, plusLane) || state.Blocked.Contains(person.Id) || !seen.Add(person.Id))
            {
                continue;
            }

            picks.Add(person);
        }

        var extras = VybeDemo.People(paths);
        for (var index = 0; index < extras.Length; index++)
        {
            var person = extras[index];
            if (here.Length > 0 && (index == 0 || index == 11))
            {
                person = person with { World = here };
            }

            if (!HottFits(person, plusLane) || state.Blocked.Contains(person.Id) || !seen.Add(person.Id))
            {
                continue;
            }

            picks.Add(person);
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

    private static bool HottFits(ScenePerson person, bool plusLane) =>
        !VybeChrome.IsLalafell(person.Race) &&
        (plusLane ? person.NightOnly || person.PlusMember : !person.NightOnly);

    private void DrawHottCard(in AppletFrame frame, Rect area, ScenePerson person, bool night,
        bool swipeLock = false, bool plusLane = false)
    {
        var tone = VybeChrome.Tone(night);
        DrawPersonCover(frame, area, person, night);
        frame.Paint.FillGradient(area, new Vector4(0f, 0f, 0f, 0.08f), new Vector4(0f, 0f, 0f, 0.82f),
            GradientAxis.Vertical);
        var live = person.Online;
        var pip = live ? VybeChrome.Online : new Vector4(0.52f, 0.52f, 0.56f, 1f);
        var status = live ? "Active now" : "Offline";
        var statusW = frame.Text.Measure(status, FontRole.Caption).X + frame.Units(18f);
        var chip = Rect.FromSize(
            new Vector2(area.Min.X + frame.Units(6f), area.Min.Y + frame.Units(6f)),
            new Vector2(MathF.Min(statusW, area.Width - frame.Units(16f)), frame.Units(16f)));
        frame.Paint.Fill(chip, new Vector4(0f, 0f, 0f, 0.55f), chip.Height * 0.5f);
        var dot = new Vector2(chip.Min.X + frame.Units(7f), chip.Center.Y);
        frame.Paint.FillCircle(dot, frame.Units(3.4f), pip);
        frame.Text.DrawEllipsized(chip.Inset(new Edges(frame.Units(13f), 0f, frame.Units(5f), 0f)), status,
            new TextStyle(FontRole.Caption, VybeChrome.Night.Ink));
        var likes = PersonLikeCount(person.Id);
        var likeText = likes.ToString(CultureInfo.InvariantCulture);
        var likeW = frame.Units(22f) + frame.Text.Measure(likeText, FontRole.Caption).X + frame.Units(10f);
        var heart = Rect.FromSize(
            new Vector2(area.Max.X - likeW - frame.Units(6f), area.Min.Y + frame.Units(6f)),
            new Vector2(likeW, frame.Units(22f)));
        DrawHottHeart(frame, heart, state.LikedPeople.Contains(person.Id), likeText, tone);
        var copy = area.Inset(new Edges(frame.Units(8f), 0f, frame.Units(8f), frame.Units(8f)));
        var facts = copy.BottomSlice(frame.Units(50f));
        var nameRow = facts.TopSlice(frame.Units(16f));
        if (VybeDemo.HasPlusAccount(person))
        {
            var tagW = MathF.Min(VybeChrome.PlusTagWidth(frame), nameRow.Width * 0.46f);
            frame.Text.DrawEllipsized(nameRow.Inset(new Edges(0f, 0f, tagW + frame.Units(4f), 0f)), person.Name,
                new TextStyle(FontRole.CaptionStrong, VybeChrome.Night.Ink));
            VybeChrome.PlusTag(frame, nameRow.RightSlice(tagW));
        }
        else
        {
            frame.Text.DrawEllipsized(nameRow, person.Name,
                new TextStyle(FontRole.CaptionStrong, VybeChrome.Night.Ink));
        }

        var place = PeopleFindBook.RegionOf(person.World);
        var race = (person.Race ?? string.Empty).Trim();
        if (VybeChrome.IsLalafell(race))
        {
            race = string.Empty;
        }

        var meta = race.Length > 0 ? place + " · " + race : place;
        frame.Text.DrawEllipsized(
            Rect.FromSize(new Vector2(facts.Min.X, nameRow.Max.Y), new Vector2(facts.Width, frame.Units(14f))),
            meta, new TextStyle(FontRole.Caption, VybeChrome.Night.Ink with { W = 0.90f }));
        frame.Text.DrawEllipsized(facts.BottomSlice(frame.Units(16f)),
            HottMatch(person).ToString(CultureInfo.InvariantCulture) + "% match",
            new TextStyle(FontRole.CaptionStrong, tone.Accent));
        if (swipeLock && (plusLane ? hottPlusTravel : hottTravel) >= frame.Units(12f))
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

    private static void DrawHottHeart(in AppletFrame frame, Rect area, bool filled, string count, NightPalette tone)
    {
        frame.Paint.Fill(area, new Vector4(0f, 0f, 0f, 0.55f), area.Height * 0.5f);
        var ink = filled ? tone.Accent : Vector4.One;
        var face = tone with { Accent = ink, Mute = ink };
        VybeChrome.PostGlyph(frame, area, VybeChrome.LikeGlyph, count, filled, face, "♡");
    }

    private int HottMatch(in ScenePerson person)
    {
        var home = game.Character.WorldName;
        var homeDc = PeopleFindBook.DataCenterOf(home);
        var mine = PeopleFindBook.Mine(state);
        if (TryPeopleCard(person, out var card))
        {
            return PeopleFindBook.Score(card, state.PeopleFind, home, homeDc, mine,
                state.LaneDone ? state.LaneMarks : null);
        }

        var score = 62;
        if (home.Length > 0 && string.Equals(person.World, home, StringComparison.OrdinalIgnoreCase))
        {
            score += 8;
        }
        else if (home.Length > 0 &&
                 string.Equals(PeopleFindBook.DataCenterOf(person.World), homeDc, StringComparison.OrdinalIgnoreCase))
        {
            score += 5;
        }

        if (person.Online)
        {
            score += 4;
        }

        score += PeopleFindBook.SharedCount(person.Tags, mine) * 6;
        var lanes = VybeLaneMap.Match(VybeLaneMap.Seed(person.GateId.Length > 0 ? person.GateId : person.Name),
            state.PeopleFind.LaneWant, state.LaneDone ? state.LaneMarks : null);
        if (lanes > 0)
        {
            score += lanes / 6;
        }

        return Math.Clamp(score, 58, 99);
    }

    private int PersonLikeCount(int personId)
    {
        var extra = state.LikedPeople.Contains(personId) ? 1 : 0;
        if (personId < 0)
        {
            return extra;
        }

        return 4 + Math.Abs(personId % 23) + extra;
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

        var night = state.Night;
        var find = state.PeopleFind;
        find.Pulse = MathF.Max(0f, find.Pulse - frame.DeltaSeconds);
        find.PassFade = MathF.Max(0f, find.PassFade - frame.DeltaSeconds);
        findDeck = PeopleFindBook.Deck(paths);
        find.Query = state.Search;
        find.SearchOpen = state.DiscoverSearchOpen;

        var stack = new Stack(area, StackAxis.Vertical, frame.Units(10f));
        var chrome = state.DiscoverPane == 0 ? frame.Units(14f) : 0f;
        DrawDiscoverHead(frame, PadX(stack.Take(frame.Units(28f)), chrome), find, night);
        if (state.DiscoverSearchOpen)
        {
            var typed = frame.TextField.Draw("ad-find", PadX(stack.Take(frame.Units(34f)), chrome), state.Search,
                "Search people, posts, tags, groups");
            BindDiscoverSearch(typed);
            if (state.Search.Length >= 2)
            {
                pearl.NoteQuery(state.Search);
            }
        }

        DrawDiscoverPanes(frame, PadX(stack.Take(frame.Units(34f)), chrome), night);
        DrawDiscoverFilters(frame, ref stack, find, night, chrome);

        if (state.DiscoverPane == 0)
        {
            DrawPeopleDiscovery(frame, ref stack, night);
            return;
        }

        if (state.DiscoverPane == 3)
        {
            DrawGroups(frame, ref stack, night);
            return;
        }

        if (state.DiscoverPane == 1)
        {
            if (night)
            {
                var next = DrawPlusSplit(frame, stack.Take(frame.Units(56f)), "Posts", state.DiscoverPostPlus, night);
                if (next != state.DiscoverPostPlus)
                {
                    state.DiscoverPostPlus = next;
                    state.Scroll = 0f;
                }
            }

            DrawDiscoverPosts(frame, ref stack, night);
            return;
        }

        if (night)
        {
            var next = DrawPlusSplit(frame, stack.Take(frame.Units(56f)), "VYBE", state.DiscoverHashPlus, night);
            if (next != state.DiscoverHashPlus)
            {
                state.DiscoverHashPlus = next;
                state.Scroll = 0f;
            }
        }
        else
        {
            state.DiscoverHashPlus = false;
        }

        DrawDiscoverHashtags(frame, ref stack, night);
    }

    private void DrawDiscoverFilters(in AppletFrame frame, ref Stack stack, PeopleFindState find, bool night,
        float chrome)
    {
        if (chrome <= 0.5f)
        {
            DrawAppliedFilters(frame, ref stack, find, night);
            return;
        }

        var padded = new Stack(PadX(stack.Remaining, chrome), StackAxis.Vertical, frame.Units(10f));
        var top = padded.Remaining.Min.Y;
        DrawAppliedFilters(frame, ref padded, find, night);
        var used = padded.Remaining.Min.Y - top;
        if (used > 0.5f)
        {
            stack.Take(used);
        }
    }

    private void DrawDiscoverHead(in AppletFrame frame, Rect area, PeopleFindState find, bool night)
    {
        var tone = VybeChrome.Tone(night);
        VybeChrome.Title(frame, area.Inset(new Edges(0f, 0f, frame.Units(72f), 0f)), "Discover", night);
        var tools = area.RightSlice(frame.Units(64f));
        var search = tools.LeftSlice(frame.Units(28f));
        var filter = tools.RightSlice(frame.Units(28f));
        DrawSearchGlyph(frame, search.Center, frame.Units(8f),
            state.DiscoverSearchOpen ? tone.Accent : tone.Ink);
        DrawFilterGlyph(frame, filter.Center, frame.Units(9f), find.Count() > 0 ? tone.Accent : tone.Ink);
        if (frame.Input.ConsumeClick(search))
        {
            state.DiscoverSearchOpen = !state.DiscoverSearchOpen;
            find.SearchOpen = state.DiscoverSearchOpen;
            if (!state.DiscoverSearchOpen)
            {
                BindDiscoverSearch(string.Empty);
            }
        }

        if (frame.Input.ConsumeClick(filter))
        {
            state.Peek(NightPage.Filters);
        }
    }

    private void BindDiscoverSearch(string typed)
    {
        state.Search = typed;
        state.PeopleFind.Query = typed;
        state.HashQuery = typed;
        state.GroupQuery = typed;
    }

    private void DrawDiscoverPosts(in AppletFrame frame, ref Stack stack, bool night)
    {
        var shownPosts = 0;
        foreach (var wallPost in VisibleDiscover(DiscoverBoard()))
        {
            DrawPearlCard(frame, stack.Take(frame.Units(PostCardHeight(frame, wallPost))), wallPost);
            shownPosts++;
        }

        if (shownPosts == 0)
        {
            VybeChrome.Mute(frame, stack.Take(frame.Units(28f)),
                state.DiscoverPostPlus
                    ? "No VYBE+ posts yet."
                    : pearl.Current.FeedLive ? "Nothing on the feed yet." : "Pearlgate is not hosting posts yet.",
                night);
        }
    }

    private void DrawDiscoverHashtags(in AppletFrame frame, ref Stack stack, bool night)
    {
        var plus = night && state.DiscoverHashPlus;
        VybeChrome.Kicker(frame, stack.Take(frame.Units(14f)),
            plus ? "VYBE+ RECOMMENDED" : "VYBE RECOMMENDED", night);
        var picks = HashPicks(plus, state.Search, state.PeopleFind);
        if (picks.Count == 0)
        {
            VybeChrome.Mute(frame, stack.Take(frame.Units(28f)),
                state.Search.Trim().Length > 0
                    ? "No recommended tags match that search."
                    : plus ? "No VYBE+ recommendations yet." : "No VYBE recommendations yet.",
                night);
        }
        else
        {
            DrawHashFlow(frame, ref stack, picks, night);
        }

        var live = HashPicks(LiveHashes(HashBoard()), state.Search, picks, state.PeopleFind);
        if (live.Count > 0)
        {
            VybeChrome.Kicker(frame, stack.Take(frame.Units(14f)), "ON THE FEED", night);
            DrawHashFlow(frame, ref stack, live, night);
        }

        VybeChrome.Kicker(frame, stack.Take(frame.Units(14f)), "MATCHING POSTS", night);
        var hits = 0;
        foreach (var wallPost in VisibleDiscover(HashBoard()))
        {
            DrawPearlCard(frame, stack.Take(frame.Units(PostCardHeight(frame, wallPost))), wallPost);
            hits++;
        }

        if (hits == 0)
        {
            VybeChrome.Mute(frame, stack.Take(frame.Units(28f)),
                plus ? "No VYBE+ posts for that tag." : "No VYBE posts for that tag.", night);
        }
    }

    private PearlPost[] HashBoard() =>
        LanePosts(state.Night && state.DiscoverHashPlus,
            PackWall(state.Posted.ToArray(), demoWall, groupWall, pearl.Current.Feed));

    private static List<string> HashPicks(bool plus, string query, PeopleFindState find) =>
        HashPicks(VybePostTags.Lane(plus), query, [], find);

    private static List<string> HashPicks(IReadOnlyList<string> source, string query, IReadOnlyList<string> skip,
        PeopleFindState find)
    {
        var picks = new List<string>();
        for (var index = 0; index < source.Count; index++)
        {
            var tag = source[index];
            var slug = VybePostTags.Normalize(tag);
            if (slug.Length == 0 || !PeopleFindBook.FitsHash(tag, find, query))
            {
                continue;
            }

            if (SkippedHash(skip, slug) || SkippedHash(picks, slug))
            {
                continue;
            }

            picks.Add(VybePostTags.Show(tag));
        }

        return picks;
    }

    private static bool SkippedHash(IReadOnlyList<string> tags, string slug)
    {
        for (var index = 0; index < tags.Count; index++)
        {
            if (string.Equals(VybePostTags.Normalize(tags[index]), slug, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private void DrawHashFlow(in AppletFrame frame, ref Stack stack, IReadOnlyList<string> tags, bool night)
    {
        var gap = frame.Units(6f);
        var rowH = frame.Units(28f);
        var pad = frame.Units(10f);
        var row = stack.Take(rowH);
        var x = 0f;
        for (var index = 0; index < tags.Count; index++)
        {
            var label = VybePostTags.Show(tags[index]);
            if (label.Length == 0)
            {
                continue;
            }

            var wide = MathF.Min(row.Width, frame.Text.Measure(label, FontRole.CaptionStrong).X + pad * 2f);
            if (x > 0f && x + wide > row.Width)
            {
                row = stack.Take(rowH);
                x = 0f;
            }

            DrawHashChip(frame,
                Rect.FromSize(new Vector2(row.Min.X + x, row.Min.Y), new Vector2(wide, row.Height)),
                label, night);
            x += wide + gap;
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
        var cardH = frame.Units(236f);
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
            if (VybeChrome.IsLalafell(person.Race) ||
                person.NightOnly != (state.Night && state.PeoplePlus) ||
                state.Blocked.Contains(person.Id) || !seen.Add(person.Id))
            {
                continue;
            }

            picks.Add(person);
        }

        for (var index = 0; index < state.Roster.Count; index++)
        {
            var person = state.Roster[index];
            if (VybeChrome.IsLalafell(person.Race) ||
                person.NightOnly != (state.Night && state.PeoplePlus) ||
                state.Blocked.Contains(person.Id) || !seen.Add(person.Id))
            {
                continue;
            }

            if (state.Passes(person))
            {
                picks.Add(person);
            }
        }

        picks.RemoveAll(person => !state.Passes(person));

        return picks;
    }

    private void DrawPersonCover(in AppletFrame frame, Rect area, ScenePerson person, bool night,
        float radius = -1f)
    {
        if (radius < 0f)
        {
            radius = frame.Units(16f);
        }
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
        if (night)
        {
            var next = DrawPlusSplit(frame, stack.Take(frame.Units(56f)), "Gallery", state.GalleryPlus, night);
            if (next != state.GalleryPlus)
            {
                state.GalleryPlus = next;
                state.Scroll = 0f;
            }
        }

        DrawGallerySearch(frame, stack.Take(frame.Units(36f)), night);
        DrawGalleryRecs(frame, ref stack, state.Night && state.GalleryPlus, night);
        DrawMasonryGallery(frame, stack.TakeRemaining(), GalleryBoard(), night);
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

    private static string[] GalleryRecs(bool plus)
    {
        var lane = VybePostTags.Lane(plus);
        var take = Math.Min(10, lane.Length);
        if (take == lane.Length)
        {
            return lane;
        }

        var picks = new string[take];
        Array.Copy(lane, picks, take);
        return picks;
    }

    private float GalleryRecsHeight(in AppletFrame frame, float width)
    {
        var options = GalleryRecs(state.Night && state.GalleryPlus);
        var wrap = VybePostTags.WrapHeight(frame,
            Rect.FromSize(Vector2.Zero, new Vector2(MathF.Max(1f, width), frame.Units(80f))), options);
        return frame.Units(18f) + wrap;
    }

    private void DrawGalleryRecs(in AppletFrame frame, ref Stack stack, bool plus, bool night)
    {
        var options = GalleryRecs(plus);
        VybeChrome.Kicker(frame, stack.Take(frame.Units(14f)), "RECOMMENDED", night);
        var board = stack.Take(VybePostTags.WrapHeight(frame, stack.Remaining, options));
        var gap = frame.Units(6f);
        var rowH = frame.Units(26f);
        var pad = frame.Units(10f);
        var x = board.Min.X;
        var y = board.Min.Y;
        var needle = VybePostTags.Normalize(state.GalleryQuery);
        for (var index = 0; index < options.Length; index++)
        {
            var label = VybePostTags.Format(options[index]);
            var slug = VybePostTags.Normalize(options[index]);
            var wide = MathF.Min(board.Width, frame.Text.Measure(label, FontRole.CaptionStrong).X + pad * 2f);
            if (x > board.Min.X && x + wide > board.Max.X)
            {
                x = board.Min.X;
                y += rowH + gap;
            }

            var cell = Rect.FromSize(new Vector2(x, y), new Vector2(wide, rowH));
            var on = needle.Length > 0 && string.Equals(needle, slug, StringComparison.Ordinal);
            if (VybeChrome.OutlineChip(frame, cell, label, on, plus) )
            {
                state.GalleryQuery = on ? string.Empty : label;
                state.Scroll = 0f;
            }

            x += wide + gap;
        }
    }

    private void DrawMasonryGallery(in AppletFrame frame, Rect area, PearlPost[] posts, bool night)
    {
        var shots = GalleryShots(posts, state.GalleryQuery);
        if (shots.Count == 0)
        {
            VybeChrome.Mute(frame, area.TopSlice(frame.Units(36f)),
                state.GalleryQuery.Trim().Length > 0
                    ? "No photos match that tag."
                    : state.GalleryPlus ? "No VYBE+ photos yet." : "No photos yet.", night);
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
            if (shot.Plus)
            {
                var tagW = MathF.Min(VybeChrome.PlusTagWidth(frame), dest.Width * 0.5f);
                VybeChrome.PlusTag(frame, Rect.FromSize(
                    new Vector2(dest.Max.X - tagW - frame.Units(6f), dest.Min.Y + frame.Units(6f)),
                    new Vector2(tagW, frame.Units(16f))));
            }

            DrawGalleryTags(frame, dest, shot.Tags, shot.Plus);
            var act = dest.BottomSlice(frame.Units(26f));
            frame.Paint.Fill(act, new Vector4(0f, 0f, 0f, 0.42f));
            var acted = BoardPost(shot.PostId) is { } tilePost && DrawFeedActions(frame, act, tilePost, night);
            if (!acted && frame.Input.ConsumeClick(dest))
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
        var shots = GalleryShots(GalleryBoard(), state.GalleryQuery);
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

        return frame.Units(84f) + GalleryRecsHeight(frame, width) + MathF.Max(left, right) + frame.Units(48f);
    }

    private static List<GalleryShot> GalleryShots(PearlPost[] posts, string query)
    {
        var shots = new List<GalleryShot>();
        for (var index = 0; index < posts.Length; index++)
        {
            var post = posts[index];
            if (post.Media.Length == 0 || !PostHasHash(post, query))
            {
                continue;
            }

            var marks = VybePostTags.Collect(post);
            for (var media = 0; media < post.Media.Length; media++)
            {
                var still = post.Media[media];
                if (still.Url.Length == 0)
                {
                    continue;
                }

                shots.Add(new GalleryShot(still.Url, post.Id, still.Width, still.Height, VybeDemo.IsPlusPost(post),
                    marks));
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

    private readonly record struct GalleryShot(
        string Url, string PostId, int Width, int Height, bool Plus, string[] Tags);

    private static void DrawGalleryTags(in AppletFrame frame, Rect dest, string[] tags, bool plus)
    {
        if (tags is not { Length: > 0 })
        {
            return;
        }

        var band = dest.BottomSlice(frame.Units(22f));
        frame.Paint.FillGradient(band, new Vector4(0f, 0f, 0f, 0f), new Vector4(0f, 0f, 0f, 0.62f),
            GradientAxis.Vertical);
        var shown = Take(tags, 3);
        var marks = new string[shown.Length];
        for (var index = 0; index < shown.Length; index++)
        {
            marks[index] = VybePostTags.Show(shown[index]);
        }

        frame.Text.DrawEllipsized(band.Inset(new Edges(frame.Units(6f), 0f, frame.Units(6f), frame.Units(2f))),
            string.Join("  ", marks), new TextStyle(FontRole.CaptionStrong, plus ? Vector4.One : FindInk));
    }

    private void DrawFeedPicks(in AppletFrame frame, Rect head, bool night)
    {
        var count = night ? 4 : 3;
        var cell = head.Width / count;
        if (DrawFeedPick(frame, FeedSlice(head, 0, cell), "For You", FeedPick.ForYou, night))
        {
            pearl.WatchFeed("foryou");
        }

        if (DrawFeedPick(frame, FeedSlice(head, 1, cell), "Following", FeedPick.Following, night))
        {
            pearl.WatchFeed("following");
        }

        DrawFeedPick(frame, FeedSlice(head, 2, cell), "Groups", FeedPick.Groups, night);
        if (night)
        {
            DrawFeedPick(frame, FeedSlice(head, 3, cell), "VYBE+", FeedPick.Plus, night);
        }
    }

    private bool DrawFeedPick(in AppletFrame frame, Rect area, string label, FeedPick pick, bool night)
    {
        if (!VybeChrome.Segment(frame, area, label, state.FeedPick == pick, night))
        {
            return false;
        }

        state.FeedPick = pick;
        return true;
    }

    private static Rect FeedSlice(Rect area, int index, float cell) =>
        Rect.FromSize(new Vector2(area.Min.X + cell * index, area.Min.Y), new Vector2(cell, area.Height));

    private bool DrawPlusSplit(in AppletFrame frame, Rect area, string sfw, bool plusOn, bool night)
    {
        _ = night;
        var gap = frame.Units(8f);
        var half = (area.Width - gap) * 0.5f;
        var next = plusOn;
        if (VybeChrome.DestCard(frame, area.LeftSlice(half), sfw, "SFW Community", !plusOn, false))
        {
            next = false;
        }

        if (VybeChrome.DestCard(frame, area.RightSlice(half), "VYBE+", "18+ Community", plusOn, true))
        {
            next = true;
        }

        return next;
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
            BeginCompose(night && state.FeedPick == FeedPick.Plus);
        }

        var shown = 0;
        foreach (var wallPost in VisibleFeed(HomeBoard()))
        {
            DrawPearlCard(frame, stack.Take(frame.Units(PostCardHeight(frame, wallPost))), wallPost);
            shown++;
        }

        if (shown == 0)
        {
            VybeChrome.Mute(frame, stack.Take(frame.Units(36f)),
                state.FeedPick == FeedPick.Plus
                    ? state.PlusBlocked
                        ? "VYBE+ is not available on Lalafell characters."
                        : "No VYBE+ posts yet."
                    : pearl.Current.FeedLive ? "Nothing shared yet." : "Pearlgate is not hosting a feed yet.",
                state.Night);
        }
    }

    private void DrawMessages(in AppletFrame frame, Rect area)
    {
        var night = state.Night;
        var snapshot = pearl.Current;
        var stack = new Stack(area, StackAxis.Vertical, frame.Units(10f));
        if (state.Page == NightPage.Inbox &&
            VybeChrome.Back(frame, stack.Take(frame.Units(28f)), PhoneLanguages.T("vybe.tab.messages"), night))
        {
            state.Back();
            return;
        }

        VybeChrome.Title(frame, stack.Take(frame.Units(28f)), PhoneLanguages.T("vybe.tab.messages"), night);
        var tabs = stack.Take(frame.Units(34f));
        var pane = VybeChrome.TextTabs(frame, tabs,
            [PhoneLanguages.T("vybe.chats"), PhoneLanguages.T("vybe.requests")], state.MessagePane, night);
        VybeChrome.Hairline(frame, tabs, night);
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

        var list = new Stack(stack.Remaining, StackAxis.Vertical, 0f);
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
                VybeChrome.Mute(frame, list.Take(frame.Units(22f)).Inset(new Edges(frame.Units(4f), frame.Units(6f), 0f, 0f)),
                    PhoneLanguages.T("vybe.favorites"), night);
            }

            DrawPearlInbox(frame, list.Take(frame.Units(64f)), chat, night);
            favored++;
            shown++;
        }

        foreach (var id in state.Connected)
        {
            var key = VybeState.LocalTalkKey(id);
            if (state.TalkHidden(key) || !state.TalkStarred(key) || !state.TryFind(id, out var person) ||
                (!night && person.NightOnly))
            {
                continue;
            }

            if (favored == 0)
            {
                VybeChrome.Mute(frame, list.Take(frame.Units(22f)).Inset(new Edges(frame.Units(4f), frame.Units(6f), 0f, 0f)),
                    PhoneLanguages.T("vybe.favorites"), night);
            }

            DrawInboxRow(frame, list.Take(frame.Units(64f)), person);
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

            DrawPearlInbox(frame, list.Take(frame.Units(64f)), chat, night);
            shown++;
        }

        foreach (var id in state.Connected)
        {
            var key = VybeState.LocalTalkKey(id);
            if (state.TalkHidden(key) || state.TalkStarred(key) || !state.TryFind(id, out var person) ||
                (!night && person.NightOnly))
            {
                continue;
            }

            DrawInboxRow(frame, list.Take(frame.Units(64f)), person);
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
        bool? live = chat.IsGroup ? null : false;
        var plus = false;
        if (state.TryFindGate(chat.OtherUserId, out var person))
        {
            face = person.AvatarUrl;
            wash = person.Wash;
            if (!chat.IsGroup)
            {
                live = person.Online;
                plus = VybeDemo.HasPlusAccount(person);
            }
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
            TalkAgo(chat.LastMessageAtUnix), chat.UnreadCount > 0, state.TalkStarred(key), night, live, plus);
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

    private bool PeopleBleed() =>
        state.Page == NightPage.Tabs && state.Tab == NightTab.Discover && state.DiscoverPane == 0;

    private bool ContentBleed() => ProfileBleed() || PeopleBleed();

    private void DrawMe(in AppletFrame frame, Rect area)
    {
        if (VybeClubs.TryFind(state, state.ActingAsClubId, out _))
        {
            DrawClubPage(frame, area, state.ActingAsClubId, night: state.Night);
            return;
        }

        var night = state.Night;
        var tone = VybeChrome.Tone(night);
        var hero = DrawProfileHero(frame, area, night, OwnBannerPath(), tone.AccentDim,
            string.Empty, OwnFacePath(), tone.Accent, own: true, back: false);
        if (hero.Save)
        {
            state.Open(NightPage.Saves);
        }

        if (hero.Edit)
        {
            state.Open(NightPage.OnboardIdentity);
        }

        if (hero.Face)
        {
            OpenOwnStill(face: true);
        }
        else if (hero.Banner)
        {
            OpenOwnStill(face: false);
        }

        var pad = frame.Units(14f);
        var stack = new Stack(
            new Rect(new Vector2(area.Min.X + pad, hero.InfoTop), new Vector2(area.Max.X - pad, area.Max.Y)),
            StackAxis.Vertical, frame.Units(6f));
        DrawProfileIdentity(frame, ref stack, ProfileName(), state.Handle, ProfileMeta(),
            state.About.Length > 0 ? state.About : "Tap the pencil to write a bio.", night, state.PlusAgreed,
            ZoneClock.Line(display.OwnTimeZoneId, display.Use24HourClock), ProfileHonorific());
        DrawOwnProfileFacts(frame, ref stack, night);
        if (state.Night && state.LaneDone)
        {
            DrawLaneFold(frame, ref stack, state.LaneMarks, night, "me");
        }

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

        DrawPlusSwitch(frame, ref stack, night);
        VybeChrome.Kicker(frame, stack.Take(frame.Units(14f)), "CREATE A POST", night);
        DrawComposeCue(frame, stack.Take(frame.Units(64f)), night, night);
        DrawProfileShelf(frame, ref stack, WithoutHidden(PackWall(OwnPosted(night), mine)), own: true, night);
    }

    private void DrawHead(in AppletFrame frame, Rect strip)
    {
        var night = state.Night;
        var tone = VybeChrome.Tone(night);
        var snap = pearl.Current;
        frame.Paint.Fill(strip, tone.Ground);
        frame.Paint.Fill(strip.BottomSlice(frame.Units(1f)), tone.Faint);
        var inner = strip.Inset(new Edges(frame.Units(14f), 0f));
        VybeChrome.Brand(frame, inner.LeftSlice(inner.Width * 0.55f), night);
        var tools = inner.RightSlice(frame.Units(72f));
        var bell = tools.LeftSlice(frame.Units(32f));
        var gear = tools.RightSlice(frame.Units(32f));
        DrawHomeBell(frame, bell, tone.Ink);
        if (snap.Notes.Length + snap.UnreadTotal > 0)
        {
            frame.Paint.FillCircle(bell.Max - new Vector2(frame.Units(6f), frame.Units(18f)), frame.Units(4f),
                tone.Accent);
        }

        DrawHomeGear(frame, gear, tone.Ink);
        if (frame.Input.ConsumeClick(bell))
        {
            state.Open(NightPage.Alerts);
        }
        else if (frame.Input.ConsumeClick(gear))
        {
            state.Open(NightPage.Settings);
        }

        frame.Input.Claim(strip);
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
            frame.Text.DrawIn(label, PhoneLanguages.T(TabKeys[index]),
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
            unread, state.TalkStarred(key), night, person.Online, VybeDemo.HasPlusAccount(person));
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
        string preview, string author, string when, bool unread, bool starred, bool night, bool? live,
        bool plusMember = false)
    {
        var tone = VybeChrome.Tone(night);
        frame.Paint.Fill(area.Inset(new Edges(0f, frame.Units(2f))), new Vector4(0.16f, 0.16f, 0.17f, 0.34f),
            frame.Units(12f));
        var faceR = frame.Units(20f);
        var face = area.LeftSlice(frame.Units(52f));
        DrawFace(frame, face.Center, faceR, avatar, wash, night);
        if (live is { } online)
        {
            VybeChrome.LivePip(frame, face.Center, faceR, online);
        }

        var trail = area.RightSlice(frame.Units(48f));
        var dots = trail.BottomSlice(frame.Units(22f));
        DrawTalkDots(frame, dots, tone.Mute);
        if (when.Length > 0)
        {
            frame.Text.DrawIn(trail.TopSlice(frame.Units(18f)), when,
                new TextStyle(FontRole.Caption, unread ? tone.Ink : tone.Mute, TextAlign.Right));
        }

        var copy = area.Inset(new Edges(frame.Units(56f), frame.Units(12f), frame.Units(50f), frame.Units(12f)));
        var name = copy.TopSlice(frame.Units(20f));
        if (starred)
        {
            var star = name.RightSlice(frame.Units(18f));
            DrawTalkStar(frame, star, true);
            name = name.Inset(new Edges(0f, 0f, frame.Units(22f), 0f));
        }

        if (plusMember)
        {
            var tagW = MathF.Min(VybeChrome.PlusTagWidth(frame), name.Width * 0.42f);
            VybeChrome.PlusTag(frame, name.RightSlice(tagW).Inset(new Edges(0f, frame.Units(2f))));
            name = name.Inset(new Edges(0f, 0f, tagW + frame.Units(4f), 0f));
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

        VybeChrome.Hairline(frame, area.Inset(new Edges(frame.Units(56f), 0f, 0f, 0f)), night);
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
        var hasProfile = TryTalkUserId(open.Key, out var profileId);
        var rows = hasProfile ? 3 : 2;
        var box = Rect.FromSize(
            new Vector2(
                Math.Clamp(open.At.X, bounds.Min.X, bounds.Max.X - width),
                Math.Clamp(open.At.Y, bounds.Min.Y, bounds.Max.Y - rowH * rows - frame.Units(8f))),
            new Vector2(width, rowH * rows + frame.Units(8f)));
        frame.Paint.Fill(box, tone.Card, frame.Units(10f));
        frame.Paint.Stroke(box, tone.Faint, frame.Units(1f), frame.Units(10f));
        var inner = box.Inset(frame.Units(4f));
        var profile = hasProfile ? inner.TopSlice(rowH) : default;
        var rest = hasProfile ? inner.Inset(new Edges(0f, rowH, 0f, 0f)) : inner;
        var star = rest.TopSlice(rowH);
        var drop = rest.BottomSlice(rowH);
        var favored = state.TalkStarred(open.Key);
        if (hasProfile)
        {
            DrawTalkMenuRow(frame, profile, "View profile", tone.Ink, tone.AccentDim);
        }

        DrawTalkMenuRow(frame, star, favored ? "Unfavorite" : "Favorite", tone.Ink, tone.AccentDim);
        DrawTalkMenuRow(frame, drop, "Delete chat", tone.Danger, tone.Danger with { W = 0.16f });
        if (talkMenuSkip)
        {
            talkMenuSkip = false;
            frame.Input.Claim(box);
            return;
        }

        if (hasProfile && frame.Input.ConsumeClick(profile))
        {
            CloseTalkMenu();
            OpenPerson(profileId);
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

    private bool TryTalkUserId(string key, out string userId)
    {
        userId = string.Empty;
        if (key.StartsWith("l:", StringComparison.Ordinal) &&
            int.TryParse(key.AsSpan(2), CultureInfo.InvariantCulture, out var personId) &&
            state.TryFind(personId, out var local))
        {
            userId = local.GateId.Length > 0 ? local.GateId : local.Id.ToString(CultureInfo.InvariantCulture);
            return userId.Length > 0;
        }

        if (!key.StartsWith("p:", StringComparison.Ordinal))
        {
            return false;
        }

        var chatId = key[2..];
        foreach (var chat in pearl.Current.Chats)
        {
            if (!string.Equals(chat.Id, chatId, StringComparison.Ordinal) || chat.IsGroup ||
                chat.OtherUserId.Length == 0)
            {
                continue;
            }

            userId = chat.OtherUserId;
            return true;
        }

        return false;
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
        var side = MathF.Min(area.Width, area.Height);
        var box = Rect.FromSize(area.Center - new Vector2(side, side) * 0.5f, new Vector2(side, side));
        frame.Text.DrawIn(box, "★", new TextStyle(FontRole.BodyStrong, ink, TextAlign.Center));
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
        var own = state.HasOwnStory ? 1 : 0;
        var slots = 1 + own + storyPeople.Count;
        var gap = frame.Units(10f);
        var cellW = frame.Units(78f);
        var span = slots * (cellW + gap) - gap;
        var max = MathF.Max(0f, span - stories.Width);
        var travel = state.StoryDrag;
        var held = frame.Input.IsHeld();
        var over = frame.Input.IsHovering(stories);
        if (held && over && !storyLane && hottLane == 0 &&
            MathF.Abs(frame.Input.PointerDelta.X) >= MathF.Abs(frame.Input.PointerDelta.Y))
        {
            storyLane = true;
        }

        if (storyLane && held)
        {
            frame.Input.Claim(stories);
            storyTravel += MathF.Abs(frame.Input.PointerDelta.X);
            travel = Math.Clamp(travel - frame.Input.PointerDelta.X, 0f, max);
        }
        else
        {
            travel = Math.Clamp(travel, 0f, max);
        }

        state.StoryDrag = travel;
        var swiping = storyTravel >= frame.Units(12f);
        var nameH = frame.Units(26f);
        var ringH = stories.Height - nameH;
        var radius = frame.Units(30f);
        var face = frame.Units(26f);

        frame.Paint.PushClip(stories);
        var x = stories.Min.X - travel;
        var addCell = Rect.FromSize(new Vector2(x, stories.Min.Y), new Vector2(cellW, stories.Height));
        DrawStoryAdd(frame, addCell, ringH, radius, nameH, night);
        if (!swiping && frame.Input.ConsumeClick(addCell))
        {
            state.Caption = state.OwnStory;
            state.StoryMedia = string.Empty;
            state.DraftStoryPermanent = false;
            state.Open(NightPage.StoryCompose);
        }

        x += cellW + gap;
        if (own == 1)
        {
            var self = Rect.FromSize(new Vector2(x, stories.Min.Y), new Vector2(cellW, stories.Height));
            DrawStoryFace(frame, self, snap.MeAvatarUrl, tone.Accent, "You",
                state.ViewedStories.Contains(-1), night, ringH, radius, face, nameH);
            if (!swiping && frame.Input.ConsumeClick(self))
            {
                state.StoryIndex = -1;
                state.ViewedStories.Add(-1);
                state.Open(NightPage.Story);
                state.Save(paths);
            }

            x += cellW + gap;
        }

        for (var index = 0; index < storyPeople.Count; index++)
        {
            var person = storyPeople[index];
            var cell = Rect.FromSize(new Vector2(x, stories.Min.Y), new Vector2(cellW, stories.Height));
            if (cell.Max.X > stories.Min.X && cell.Min.X < stories.Max.X)
            {
                DrawStoryFace(frame, cell, person.AvatarUrl, person.Wash, StoryName(person),
                    state.ViewedStories.Contains(person.Id), night, ringH, radius, face, nameH);
                if (!swiping && frame.Input.ConsumeClick(cell))
                {
                    state.StoryIndex = person.Id;
                    state.ViewedStories.Add(person.Id);
                    state.Open(NightPage.Story);
                    state.Save(paths);
                }
            }

            x += cellW + gap;
        }

        frame.Paint.PopClip();
        if (!held)
        {
            if (swiping)
            {
                frame.Input.ConsumeClick(stories);
            }

            storyTravel = 0f;
            storyLane = false;
        }
    }

    private void DrawStoryFace(in AppletFrame frame, Rect area, string avatar, Vector4 wash, string name,
        bool seen, bool night, float ringH, float radius, float face, float nameH)
    {
        var ring = area.TopSlice(ringH);
        VybeChrome.StoryRing(frame, ring.Center, radius, seen, night);
        DrawFace(frame, ring.Center, face, avatar, wash, night);
        DrawStoryName(frame, area.BottomSlice(nameH), name, night);
    }

    private static void DrawStoryName(in AppletFrame frame, Rect area, string name, bool night)
    {
        frame.Text.DrawEllipsized(area.Inset(new Edges(frame.Units(2f), 0f)), name,
            new TextStyle(FontRole.Caption, VybeChrome.Tone(night).Mute, TextAlign.Center));
    }

    private static string StoryName(ScenePerson person)
    {
        var handle = person.Handle.Trim().TrimStart('@');
        var name = person.Name.Trim();
        var space = name.IndexOf(' ');
        var first = space > 0 ? name[..space] : name;
        if (first.Length > 0 && first.Length <= 10)
        {
            return first;
        }

        if (handle.Length > 0)
        {
            return handle;
        }

        return first.Length > 0 ? first : "Story";
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

    private static void DrawStoryAdd(in AppletFrame frame, Rect area, float ringH, float radius, float nameH,
        bool night)
    {
        var tone = VybeChrome.Tone(night);
        var ring = area.TopSlice(ringH);
        frame.Paint.StrokeCircle(ring.Center, radius, tone.Mute, frame.Units(1.8f));
        frame.Text.DrawIn(ring, "+",
            new TextStyle(FontRole.Title, tone.Mute, TextAlign.Center));
        DrawStoryName(frame, area.BottomSlice(nameH), "Add", night);
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

        var side = size * 2f;
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
        var mark = VybePostTags.Format(tag);
        var on = string.Equals(VybePostTags.Normalize(state.Hashtag), VybePostTags.Normalize(mark),
            StringComparison.Ordinal);
        if (VybeChrome.Chip(frame, area, mark, on, night))
        {
            if (on)
            {
                state.Hashtag = string.Empty;
                BindDiscoverSearch(string.Empty);
            }
            else
            {
                state.Hashtag = mark;
                BindDiscoverSearch(mark);
            }
        }
    }

    private Rect FlushPost(in AppletFrame frame, Rect area)
    {
        if (ContentBleed())
        {
            return area;
        }

        var pad = frame.Units(14f);
        return new Rect(new Vector2(area.Min.X - pad, area.Min.Y),
            new Vector2(area.Max.X + pad, area.Max.Y));
    }

    private void DrawPearlCard(in AppletFrame frame, Rect area, PearlPost post)
    {
        area = FlushPost(frame, area);
        var night = state.Night;
        var tone = VybeChrome.Tone(night);
        VybeChrome.PostSheet(frame, area, flush: true);
        var inner = area.Inset(new Edges(frame.Units(12f), frame.Units(10f)));
        var wash = tone.AccentDim;
        if (state.TryFindGate(post.AuthorId, out var author))
        {
            wash = author.Wash;
        }

        var avatarHit = inner.LeftSlice(frame.Units(32f)).TopSlice(frame.Units(32f));
        VybeChrome.StoryRing(frame, avatarHit.Center, frame.Units(13f), false, night);
        DrawFace(frame, avatarHit.Center, frame.Units(12f), post.AuthorAvatarUrl, wash, night);
        var grouped = VybeGroups.TryGroup(post, state, out var group);
        var handle = grouped
            ? group.Name
            : post.AuthorHandle.Length > 0
                ? (post.AuthorHandle.StartsWith('@') ? post.AuthorHandle : "@" + post.AuthorHandle)
                : string.Empty;
        var more = inner.RightSlice(frame.Units(28f)).TopSlice(frame.Units(28f));
        var head = inner.Inset(new Edges(frame.Units(40f), 0f, frame.Units(72f), 0f)).TopSlice(frame.Units(32f));
        frame.Text.DrawEllipsized(head.TopSlice(frame.Units(16f)),
            grouped ? post.AuthorName + " · Group" : post.AuthorName + (handle.Length > 0 ? "  " + handle : string.Empty),
            new TextStyle(FontRole.CaptionStrong, tone.Ink));
        VybeChrome.Mute(frame, head.BottomSlice(frame.Units(14f)),
            grouped ? group.Name : handle, night);
        var when = inner.Inset(new Edges(0f, 0f, frame.Units(30f), 0f)).RightSlice(frame.Units(40f))
            .TopSlice(frame.Units(16f));
        frame.Text.DrawEllipsized(when, post.When,
            new TextStyle(FontRole.Caption, tone.Mute, TextAlign.Right));
        frame.Text.DrawIn(more, "···",
            new TextStyle(FontRole.Title, tone.Mute, TextAlign.Center));
        if (!SheetBlocking() && frame.Input.ConsumeClick(more.Expand(frame.Units(8f))))
        {
            if (post.Mine)
            {
                state.DropPostId = post.Id;
            }
            else
            {
                OpenStaffReport("vybe_post", post.Id, post.AuthorName + ": " + post.Body);
            }

            return;
        }
        var plusLane = IsPlusLane(post);
        var cursor = frame.Units(36f);
        if (plusLane)
        {
            var meta = inner.Inset(new Edges(0f, cursor, 0f, frame.Units(28f))).TopSlice(frame.Units(22f));
            var badgeW = MathF.Min(meta.Width * 0.52f, frame.Units(110f));
            VybeChrome.LaneBadge(frame, meta.LeftSlice(badgeW), true);
            var rating = VybePostMark.Label(VybePostMark.Read(post.ContentRating));
            if (rating.Length > 0 && rating is not ("SFW" or "Not set"))
            {
                var chipW = MathF.Min(meta.Width - badgeW - frame.Units(8f),
                    frame.Text.Measure(rating, FontRole.CaptionStrong).X + frame.Units(16f));
                VybeChrome.TagChip(frame, Rect.FromSize(
                    new Vector2(meta.Min.X + badgeW + frame.Units(6f), meta.Min.Y),
                    new Vector2(chipW, meta.Height)), rating, true);
            }

            cursor += frame.Units(28f);
        }

        if (post.QuoteAuthor.Length > 0 || post.QuoteBody.Length > 0)
        {
            var quote = inner.Inset(new Edges(0f, cursor, 0f, 0f)).TopSlice(frame.Units(40f));
            frame.Paint.Fill(quote, new Vector4(1f, 1f, 1f, 0.04f), frame.Units(10f));
            frame.Paint.Fill(quote.LeftSlice(frame.Units(2.5f)), tone.Accent with { W = 0.85f }, frame.Units(1.2f));
            VybeChrome.Kicker(frame, quote.TopSlice(frame.Units(14f)).Inset(new Edges(frame.Units(10f), 0f)),
                post.QuoteAuthor, night);
            VybeChrome.Mute(frame, quote.BottomSlice(frame.Units(22f)).Inset(new Edges(frame.Units(10f), 0f)),
                post.QuoteBody, night);
            cursor += frame.Units(44f);
        }

        var caption = CardCaption(post);
        if (caption.Length > 0)
        {
            var bodyH = PostBodyPixels(frame, caption, inner.Width);
            frame.Text.DrawWrapped(inner.Inset(new Edges(0f, cursor, 0f, frame.Units(28f))).TopSlice(bodyH),
                caption, new TextStyle(FontRole.Caption, tone.Ink));
            cursor += bodyH + frame.Units(2f);
        }

        var tags = VybePostTags.Collect(post);
        if (tags.Length > 0)
        {
            var tagH = CardTagHeight(frame, tags, inner.Width);
            DrawCardTags(frame, inner.Inset(new Edges(0f, cursor, 0f, frame.Units(28f))).TopSlice(tagH), tags, plusLane);
            cursor += tagH + frame.Units(6f);
        }

        if (post.Media.Length > 0)
        {
            var plateH = post.Media.Length == 1
                ? ComposeStillHeight(frame, area.Width, post.Media[0].Url)
                : frame.Units(168f);
            var plate = area.Inset(new Edges(0f, inner.Min.Y - area.Min.Y + cursor, 0f, frame.Units(28f)))
                .TopSlice(plateH);
            if (post.Media.Length == 1)
            {
                DrawStillWhole(frame, plate, post.Media[0].Url, night);
                if (frame.Input.ConsumeClick(plate))
                {
                    state.ViewMedia = post.Media[0].Url;
                    state.PostKey = post.Id;
                    state.Open(NightPage.PhotoView);
                }
            }
            else
            {
                DrawMediaPlate(frame, plate, post, night);
            }
        }

        VybeChrome.Hairline(frame, inner.Inset(new Edges(0f, 0f, 0f, frame.Units(28f))).BottomSlice(frame.Units(1f)));
        if (!SheetBlocking() && DrawFeedActions(frame, inner.BottomSlice(frame.Units(26f)), post, night))
        {
            return;
        }

        if (SheetBlocking())
        {
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
            if ((!state.Night && person.NightOnly) || state.Blocked.Contains(person.Id) || !seen.Add(person.Id))
            {
                continue;
            }

            list.Add(person);
        }

        foreach (var person in state.Roster)
        {
            if (state.Blocked.Contains(person.Id) ||
                !seen.Add(person.Id) ||
                (!state.Night && person.NightOnly) ||
                (!state.LiveStories.Contains(person.Id) && person.Line.Length == 0))
            {
                continue;
            }

            list.Add(person);
        }

        return list;
    }

    private void HeartPost(string postId, bool baseline)
    {
        if (postId.Length == 0)
        {
            return;
        }

        state.FlipHeart(postId);
        if (pearl.Current.SignedIn && pearl.PostById(postId) is not null)
        {
            pearl.LikePost(postId, state.PostHearted(postId, baseline));
        }

        state.Save(paths);
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

    private void OpenPostComments(string postId)
    {
        if (postId.Length == 0)
        {
            return;
        }

        state.PostKey = postId;
        pearl.WatchPost(postId);
        state.CommentDraft = string.Empty;
        state.Open(NightPage.PostComments);
    }

    private string CommentMark(in PearlPost post)
    {
        var lines = LivePostComments(post.Id);
        var count = lines.Count > 0 ? lines.Count : post.Comments;
        return count.ToString(CultureInfo.InvariantCulture);
    }

    private List<PearlComment> LivePostComments(string postId)
    {
        var packed = new List<PearlComment>();
        var remote = pearl.CommentsFor(postId);
        for (var index = 0; index < remote.Count; index++)
        {
            RememberComment(packed, remote[index]);
        }

        var local = state.PostCommentsFor(postId);
        for (var index = 0; index < local.Count; index++)
        {
            RememberComment(packed, local[index]);
        }

        return packed;
    }

    private static void RememberComment(List<PearlComment> packed, PearlComment line)
    {
        var body = (line.Body ?? string.Empty).Trim();
        if (body.Length == 0)
        {
            return;
        }

        for (var index = 0; index < packed.Count; index++)
        {
            if (string.Equals(packed[index].Author, line.Author, StringComparison.OrdinalIgnoreCase) &&
                string.Equals((packed[index].Body ?? string.Empty).Trim(), body, StringComparison.Ordinal))
            {
                return;
            }
        }

        packed.Add(line with { Body = body });
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
        if (state.FeedPick == FeedPick.Plus)
        {
            return state.PlusBlocked
                ? []
                : WithoutHidden(WithoutGroups(LanePosts(true,
                    PackWall(state.Posted.ToArray(), demoWall, pearl.Current.Feed))));
        }

        if (state.FeedPick == FeedPick.Groups)
        {
            return WithoutHidden(state.Night
                ? PackWall(LanePosts(false, groupWall), LanePosts(true, groupWall))
                : LanePosts(false, groupWall));
        }

        return BoardFeed();
    }

    private PearlPost[] BoardFeed() =>
        WithoutHidden(WithoutGroups(LanePosts(false, PackWall(state.Posted.ToArray(), demoWall, pearl.Current.Feed))));

    private PearlPost[] OpenBoard()
    {
        var raw = PackWall(state.Posted.ToArray(), demoWall, groupWall, pearl.Current.Feed);
        var lane = state.Night ? PackWall(LanePosts(false, raw), LanePosts(true, raw)) : LanePosts(false, raw);
        return WithoutHidden(lane);
    }

    private PearlPost[] GalleryBoard() => WithoutHidden(LanePosts(state.Night && state.GalleryPlus,
        PackWall(state.Posted.ToArray(), demoWall, groupWall, pearl.Current.Feed)));

    private PearlPost[] DiscoverBoard() => WithoutHidden(LanePosts(state.Night && state.DiscoverPostPlus,
        PackWall(state.Posted.ToArray(), demoWall, groupWall, pearl.Current.Feed)));

    private bool PearlGone(string id) =>
        id.Length > 0 && state.HiddenPearls.Contains(id);

    private PearlPost[] WithoutHidden(PearlPost[] posts)
    {
        if (posts.Length == 0 || state.HiddenPearls.Count == 0)
        {
            return posts;
        }

        var keep = new List<PearlPost>(posts.Length);
        for (var index = 0; index < posts.Length; index++)
        {
            if (!PearlGone(posts[index].Id))
            {
                keep.Add(posts[index]);
            }
        }

        return keep.ToArray();
    }

    private static PearlPost[] PackWall(params PearlPost[][] walls)
    {
        var count = 0;
        for (var index = 0; index < walls.Length; index++)
        {
            count += walls[index].Length;
        }

        var packed = new PearlPost[count];
        var at = 0;
        for (var index = 0; index < walls.Length; index++)
        {
            walls[index].CopyTo(packed, at);
            at += walls[index].Length;
        }

        return packed;
    }

    private static bool IsGroupPost(PearlPost post) =>
        post.AuthorHandle.StartsWith("group:", StringComparison.Ordinal) ||
        post.AuthorId.StartsWith("club:", StringComparison.Ordinal);

    private PearlPost[] WithoutGroups(PearlPost[] posts)
    {
        var keep = new List<PearlPost>(posts.Length);
        for (var index = 0; index < posts.Length; index++)
        {
            if (!IsGroupPost(posts[index]))
            {
                keep.Add(posts[index]);
            }
        }

        return keep.ToArray();
    }

    private bool IsPlusLane(PearlPost post) =>
        VybeDemo.IsPlusPost(post) || (VybeGroups.TryGroup(post, state, out var group) && group.PlusOnly);

    private PearlPost[] LanePosts(bool plus, PearlPost[] posts)
    {
        var keep = new List<PearlPost>(posts.Length);
        for (var index = 0; index < posts.Length; index++)
        {
            if (IsPlusLane(posts[index]) == plus)
            {
                keep.Add(posts[index]);
            }
        }

        return keep.ToArray();
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

        for (var index = 0; index < state.Posted.Count; index++)
        {
            if (string.Equals(state.Posted[index].Id, id, StringComparison.Ordinal))
            {
                return state.Night || !VybeDemo.IsPlusPost(state.Posted[index]) ? state.Posted[index] : null;
            }
        }

        for (var index = 0; index < demoWall.Length; index++)
        {
            if (string.Equals(demoWall[index].Id, id, StringComparison.Ordinal))
            {
                return state.Night || !VybeDemo.IsPlusPost(demoWall[index]) ? demoWall[index] : null;
            }
        }

        for (var index = 0; index < groupWall.Length; index++)
        {
            if (string.Equals(groupWall[index].Id, id, StringComparison.Ordinal))
            {
                return groupWall[index];
            }
        }

        for (var index = 0; index < state.StoryReposts.Count; index++)
        {
            if (string.Equals(state.StoryReposts[index].Id, id, StringComparison.Ordinal))
            {
                return state.StoryReposts[index];
            }
        }

        for (var index = 0; index < state.StorySaves.Count; index++)
        {
            if (string.Equals(state.StorySaves[index].Id, id, StringComparison.Ordinal))
            {
                return state.StorySaves[index];
            }
        }

        return null;
    }

    private PearlPost[] PersonBoard(string gateId)
    {
        var theirs = new List<PearlPost>();
        if (string.Equals(gateId, "me", StringComparison.Ordinal))
        {
            theirs.AddRange(OwnPosted(state.Night));
        }

        for (var index = 0; index < demoWall.Length; index++)
        {
            if (string.Equals(demoWall[index].AuthorId, gateId, StringComparison.Ordinal) &&
                (state.Night || !VybeDemo.IsPlusPost(demoWall[index])))
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
            if (state.Hashtag.Length > 0 && !PostHasHash(post, state.Hashtag))
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

    private IEnumerable<PearlPost> VisibleDiscover(PearlPost[] posts)
    {
        var find = state.PeopleFind;
        var home = game.Character.WorldName;
        foreach (var post in posts)
        {
            if (state.Hashtag.Length > 0 && !PostHasHash(post, state.Hashtag))
            {
                continue;
            }

            if (!PeopleFindBook.FitsPost(post, find, state, findDeck, home, state.Search))
            {
                continue;
            }

            yield return post;
        }
    }

    private static bool PostHasHash(PearlPost post, string tag)
    {
        var needle = tag.Trim().TrimStart('#');
        if (needle.Length == 0)
        {
            return true;
        }

        if (post.Body.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return true;
        }

        var marks = post.Hashtags;
        if (marks is not { Length: > 0 })
        {
            return false;
        }

        for (var index = 0; index < marks.Length; index++)
        {
            if (VybePostTags.Normalize(marks[index]).IndexOf(VybePostTags.Normalize(needle),
                    StringComparison.Ordinal) >= 0)
            {
                return true;
            }
        }

        return false;
    }

    private static List<string> LiveHashes(PearlPost[] posts)
    {
        var tags = new List<string>();
        foreach (var post in posts)
        {
            var marks = post.Hashtags;
            if (marks is { Length: > 0 })
            {
                for (var mark = 0; mark < marks.Length; mark++)
                {
                    var shown = VybePostTags.Show(marks[mark]);
                    if (shown.Length > 0 && !tags.Contains(shown, StringComparer.OrdinalIgnoreCase) &&
                        tags.Count < 12)
                    {
                        tags.Add(shown);
                    }
                }
            }

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
                    if (!tags.Contains(tag, StringComparer.OrdinalIgnoreCase) && tags.Count < 12)
                    {
                        tags.Add(tag);
                    }
                }

                from = end;
            }
        }

        return tags;
    }

    private static string CardCaption(PearlPost post) => VybePostTags.Caption(post.Body);

    private static float CardTagHeight(in AppletFrame frame, string[] tags, float width)
    {
        var shown = tags.Length > 6 ? tags[..6] : tags;
        return VybePostTags.WrapHeight(frame, Rect.FromSize(Vector2.Zero, new Vector2(width, frame.Units(80f))),
            shown);
    }

    private static void DrawCardTags(in AppletFrame frame, Rect area, string[] tags, bool plus)
    {
        var gap = frame.Units(6f);
        var rowH = frame.Units(26f);
        var pad = frame.Units(10f);
        var x = area.Min.X;
        var y = area.Min.Y;
        var count = Math.Min(6, tags.Length);
        for (var index = 0; index < count; index++)
        {
            var label = VybePostTags.Show(tags[index]);
            if (label.Length == 0)
            {
                continue;
            }

            var wide = MathF.Min(area.Width, frame.Text.Measure(label, FontRole.CaptionStrong).X + pad * 2f);
            if (x > area.Min.X && x + wide > area.Max.X)
            {
                x = area.Min.X;
                y += rowH + gap;
            }

            if (y + rowH > area.Max.Y + 0.5f)
            {
                break;
            }

            VybeChrome.TagChip(frame, Rect.FromSize(new Vector2(x, y), new Vector2(wide, rowH)), label, plus);
            x += wide + gap;
        }
    }

    private static float PostBodyPixels(in AppletFrame frame, string caption, float wrapWidth)
    {
        if (caption.Length == 0)
        {
            return 0f;
        }

        return frame.Text.MeasureWrapped(caption, FontRole.Caption, MathF.Max(1f, wrapWidth)).Y
            + frame.Units(4f);
    }

    private float PostCardHeight(in AppletFrame frame, PearlPost post)
    {
        var scale = MathF.Max(frame.Scale, 0.01f);
        var wrap = MathF.Max(frame.Units(40f), frame.Content.Width - frame.Units(24f));
        var mediaW = MathF.Max(frame.Units(40f), frame.Content.Width);
        var height = 118f;
        if (IsPlusLane(post))
        {
            height += 28f;
        }

        if (post.QuoteBody.Length > 0 || post.QuoteAuthor.Length > 0)
        {
            height += 44f;
        }

        var caption = CardCaption(post);
        if (caption.Length > 0)
        {
            height += PostBodyPixels(frame, caption, wrap) / scale + 2f;
        }

        var tags = VybePostTags.Collect(post);
        if (tags.Length > 0)
        {
            height += CardTagHeight(frame, tags, wrap) / scale + 6f;
        }

        if (post.Media.Length == 1)
        {
            height += ComposeStillHeight(frame, mediaW, post.Media[0].Url) / scale + 4f;
        }
        else if (post.Media.Length > 0)
        {
            height += 172f;
        }

        return height;
    }

    private readonly record struct ProfileHeroHits(
        float InfoTop, bool Banner, bool Face, bool Flag, bool Block, bool Save, bool Edit, bool Back);

    private ProfileHeroHits DrawProfileHero(in AppletFrame frame, Rect area, bool night, string bannerPath,
        Vector4 bannerWash, string faceUrl, string facePath, Vector4 faceWash, bool own, bool back,
        bool tools = true)
    {
        var tone = VybeChrome.Tone(night);
        var pad = frame.Units(14f);
        var coverH = frame.Units(124f);
        var cover = new Rect(new Vector2(area.Min.X, area.Min.Y),
            new Vector2(area.Max.X, area.Min.Y + coverH));
        var bannerEmpty = !DrawBleedStill(frame, cover, bannerPath);
        if (bannerEmpty)
        {
            if (own)
            {
                VybeChrome.EmptyBanner(frame, cover, night);
                VybeChrome.AddPlus(frame, cover.Center, frame.Units(14f), night);
            }
            else
            {
                frame.Paint.Fill(cover, bannerWash);
            }
        }

        var faceR = frame.Units(40f);
        var faceCenter = new Vector2(area.Min.X + pad + faceR, cover.Max.Y + faceR * 0.42f);
        var faceEmpty = own && !VybeChrome.StillReady(facePath) && faceUrl.Length == 0;
        if (faceEmpty)
        {
            VybeChrome.EmptyPortrait(frame, faceCenter, faceR, night);
            VybeChrome.AddPlus(frame, faceCenter, faceR * 0.32f, night);
        }
        else
        {
            DrawFace(frame, faceCenter, faceR, faceUrl, faceWash, night, facePath);
        }

        frame.Paint.StrokeCircle(faceCenter, faceR, tone.Mute, frame.Units(1.8f));

        var faceHit = Rect.FromSize(faceCenter - new Vector2(faceR, faceR), new Vector2(faceR * 2f));

        var tool = frame.Units(32f);
        var gap = frame.Units(8f);
        var count = 2f;
        var toolsBox = Rect.FromSize(
            new Vector2(area.Max.X - pad - tool * count - gap, cover.Max.Y + frame.Units(10f)),
            new Vector2(tool * count + gap, tool));
        var saveHit = own && tools ? toolsBox.LeftSlice(tool) : Rect.Empty;
        var editHit = own && tools ? toolsBox.RightSlice(tool) : Rect.Empty;
        var blockHit = !own && tools ? toolsBox.LeftSlice(tool) : Rect.Empty;
        var flag = !own && tools ? toolsBox.RightSlice(tool) : Rect.Empty;
        if (own && tools)
        {
            VybeChrome.SaveMark(frame, saveHit, tone.Ink);
            VybeChrome.EditMark(frame, editHit, tone.Ink);
        }
        else if (!own && tools)
        {
            VybeChrome.BlockMark(frame, blockHit, tone.Ink);
            VybeChrome.ReportFlag(frame, flag, tone.Ink);
        }

        var backHit = Rect.Empty;
        var wentBack = false;
        if (back)
        {
            backHit = Rect.FromSize(new Vector2(cover.Min.X + frame.Units(8f), cover.Min.Y + frame.Units(8f)),
                new Vector2(frame.Units(32f), frame.Units(32f)));
            VybeChrome.BackChip(frame, backHit, tone.Ink);
            wentBack = frame.Input.ConsumeClick(backHit);
        }

        var saved = own && frame.Input.ConsumeClick(saveHit);
        var blocked = !own && frame.Input.ConsumeClick(blockHit);
        var flagged = !own && !blocked && frame.Input.ConsumeClick(flag);
        var edited = own && frame.Input.ConsumeClick(editHit);
        var faced = frame.Input.ConsumeClick(faceHit);
        var bannered = !faced && !flagged && !blocked && !saved && !edited && !wentBack &&
                       frame.Input.ConsumeClick(cover);
        var infoTop = MathF.Max(faceCenter.Y + faceR, tools ? toolsBox.Max.Y : faceCenter.Y + faceR) +
                      frame.Units(12f);
        _ = backHit;
        return new ProfileHeroHits(infoTop, bannered, faced, flagged, blocked, saved, edited, wentBack);
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

        DrawOwnStill(frame, area, texture, path, ownFace: false, circle: false);
        return true;
    }

    private void DrawOwnStill(in AppletFrame frame, Rect area, ITextureHandle texture, string path, bool ownFace,
        bool circle)
    {
        float zoom;
        Vector2 focus;
        if (ownFace && path.Length > 0 &&
            string.Equals(path, state.ProfileFacePath, StringComparison.OrdinalIgnoreCase))
        {
            zoom = state.FaceZoom;
            focus = new Vector2(state.FaceFocusX, state.FaceFocusY);
        }
        else if (!ownFace && path.Length > 0 &&
                 string.Equals(path, state.ProfileBannerPath, StringComparison.OrdinalIgnoreCase))
        {
            zoom = state.BannerZoom;
            focus = new Vector2(state.BannerFocusX, state.BannerFocusY);
        }
        else
        {
            var handsetFace = HandsetLook.PortraitFile(paths, badges);
            if (ownFace && badges.PortraitFile.Length > 0 &&
                string.Equals(path, handsetFace, StringComparison.OrdinalIgnoreCase))
            {
                zoom = badges.PortraitZoom;
                focus = badges.PortraitFocus;
            }
            else
            {
                var uv = CoverFit.Uv(texture.Size, area.Size);
                if (circle)
                {
                    frame.Paint.ImageRounded(texture, area, uv.Min, uv.Max, Vector4.One,
                        MathF.Min(area.Width, area.Height) * 0.5f);
                }
                else
                {
                    frame.Paint.Image(texture, area, uv.Min, uv.Max, Vector4.One);
                }

                return;
            }
        }

        if (zoom < 1f)
        {
            var tone = VybeChrome.Tone(state.Night);
            if (circle)
            {
                frame.Paint.FillCircle(area.Center, MathF.Min(area.Width, area.Height) * 0.5f, tone.Ground);
            }
            else
            {
                frame.Paint.Fill(area, tone.Card);
            }
        }

        CoverFit.Placed(texture.Size, area, zoom, focus, out var dest, out var crop);
        if (circle)
        {
            frame.Paint.ImageRounded(texture, dest, crop.Min, crop.Max, Vector4.One,
                MathF.Min(dest.Width, dest.Height) * 0.5f);
            return;
        }

        frame.Paint.Image(texture, dest, crop.Min, crop.Max, Vector4.One);
    }

    private void OpenProfileReport(string target, string title) =>
        OpenStaffReport("user", target, title);

    private void OpenStaffReport(string kind, string target, string title)
    {
        state.ReportOpen = true;
        state.ReportFresh = true;
        state.ReportReason = 0;
        state.ReportDetail = string.Empty;
        state.ReportKind = kind;
        state.ReportTarget = target;
        state.ReportTitle = title;
    }

    private void CloseProfileReport()
    {
        state.ReportOpen = false;
        state.ReportFresh = false;
        state.ReportReason = 0;
        state.ReportDetail = string.Empty;
        state.ReportKind = "user";
        state.ReportTarget = string.Empty;
        state.ReportTitle = string.Empty;
    }

    private void SubmitProfileReport()
    {
        if (state.ReportReason <= 0)
        {
            return;
        }

        var target = state.ReportTarget.Trim();
        var reason = StaffReports.ReasonAt(state.ReportReason);
        var kind = state.ReportKind.Length > 0 ? state.ReportKind : "user";
        var detail = kind + ": " + (state.ReportTitle.Length > 0 ? state.ReportTitle : "Unknown") +
                     (state.ReportDetail.Trim().Length > 0 ? "\n\n" + state.ReportDetail.Trim() : string.Empty);
        if (target.Length == 0 || (kind == "user" && (target.Length < 8 || target.StartsWith("demo:", StringComparison.Ordinal))))
        {
            CloseProfileReport();
            return;
        }

        IReadOnlyList<PearlReportLine>? lines = null;
        if (kind == "chat")
        {
            lines = StaffReports.ChatWindow(pearl, target);
        }

        StaffReportDispatch.File(pearl, desk, game.Character.Name, game.Character.WorldName, kind, target, reason,
            detail, lines);
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
            StaffReports.Reasons, state.ReportReason);
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
        var ready = state.ReportReason > 0;
        VybeChrome.Primary(frame, send, "Submit", night);
        var dismiss = !state.ReportFresh &&
            (frame.Input.WasClicked(cancel) ||
             (!card.Contains(frame.Input.Pointer) && frame.Input.WasClicked(area)));
        state.ReportFresh = false;
        if (dismiss)
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
                DrawOwnStill(frame, dest, texture, localPath, ownFace: true, circle: true);
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

    private ITextureHandle? StillTexture(in AppletFrame frame, string url)
    {
        if (url.Length == 0)
        {
            return null;
        }

        if (File.Exists(url))
        {
            return frame.Textures.FromFile(url);
        }

        pearl.PrefetchMedia(url);
        var path = pearl.LocalMedia(url);
        return path is { Length: > 0 } ? frame.Textures.FromFile(path) : null;
    }

    private float ComposeStillHeight(in AppletFrame frame, float width, string url)
    {
        var texture = StillTexture(frame, url);
        if (texture is not { IsReady: true } || texture.Size.X <= 0f)
        {
            return frame.Units(210f);
        }

        var height = width * (texture.Size.Y / texture.Size.X);
        return Math.Clamp(height, frame.Units(140f), frame.Units(360f));
    }

    private void DrawStillWhole(in AppletFrame frame, Rect area, string url, bool night)
    {
        frame.Paint.Fill(area, new Vector4(0.03f, 0.03f, 0.035f, 1f), frame.Units(12f));
        frame.Paint.Stroke(area, new Vector4(1f, 1f, 1f, 0.08f), frame.Units(1f), frame.Units(12f));
        var texture = StillTexture(frame, url);
        if (texture is not { IsReady: true })
        {
            _ = night;
            return;
        }

        var dest = CoverFit.Contained(texture.Size, area.Inset(frame.Units(4f)));
        frame.Paint.ImageRounded(texture, dest, Vector2.Zero, Vector2.One, Vector4.One, frame.Units(10f));
    }

    private void DrawStillFit(in AppletFrame frame, Rect area, string url, bool night)
    {
        var wash = night
            ? new Vector4(0.04f, 0.04f, 0.05f, 1f)
            : new Vector4(0.06f, 0.06f, 0.07f, 1f);
        frame.Paint.Fill(area, wash, frame.Units(12f));
        var texture = StillTexture(frame, url);
        if (texture is not { IsReady: true })
        {
            return;
        }

        var dest = CoverFit.Snapped(CoverFit.Contained(texture.Size, area.Inset(frame.Units(6f))));
        frame.Paint.Image(texture, dest, Vector4.One);
    }

    private void DrawProfileShelf(in AppletFrame frame, ref Stack stack, PearlPost[] posts, bool own, bool night)
    {
        var panes = stack.Take(frame.Units(40f));
        var next = VybeChrome.ProfileMarks(frame, panes, Math.Clamp(state.ProfilePane, 0, 3), night);
        if (next != state.ProfilePane)
        {
            state.ProfilePane = next;
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
                DrawRepostShelf(frame, ref stack, night);
                return;
            default:
                var reposts = own ? WithoutHidden(state.StoryReposts.ToArray()) : [];
                if (reposts.Length > 0)
                {
                    VybeChrome.Kicker(frame, stack.Take(frame.Units(14f)), "REPOSTS", night);
                    for (var index = 0; index < reposts.Length; index++)
                    {
                        DrawPearlCard(frame, stack.Take(frame.Units(PostCardHeight(frame, reposts[index]))),
                            reposts[index]);
                    }
                }

                if (posts.Length == 0)
                {
                    if (!own || reposts.Length == 0)
                    {
                        VybeChrome.Mute(frame, stack.Take(frame.Units(36f)),
                            own
                                ? pearl.Current.SignedIn ? "Your posts will land here."
                                    : "Sign in from You to show your posts."
                                : "No posts yet.",
                            night);
                    }

                    return;
                }

                if (own && reposts.Length > 0)
                {
                    VybeChrome.Kicker(frame, stack.Take(frame.Units(14f)), "POSTS", night);
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
        if (own && VybeChrome.Chip(frame, stack.Take(frame.Units(34f)),
                night && CanPostPlus() ? "Create a VYBE or VYBE+ group" : "Create a group", false, night))
        {
            BeginClubCreate(night);
            return;
        }

        var rows = new List<SceneGroup>();
        if (own)
        {
            for (var index = 0; index < state.Clubs.Count; index++)
            {
                var club = state.Clubs[index];
                if (!club.PlusOnly || night)
                {
                    rows.Add(VybeClubs.Scene(club));
                }
            }
        }

        for (var index = 0; index < VybeGroups.Catalog.Length; index++)
        {
            var group = VybeGroups.Catalog[index];
            if (group.PlusOnly && !night)
            {
                continue;
            }

            if (own ? VybeGroups.Joined(state, group.Id) : group.Suggested)
            {
                rows.Add(group);
            }
        }

        if (rows.Count == 0)
        {
            VybeChrome.Mute(frame, stack.Take(frame.Units(36f)),
                own ? "Your groups land here. Create one to post, share photos, and host events."
                    : "No public groups yet.", night);
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

    private void DrawRepostShelf(in AppletFrame frame, ref Stack stack, bool night)
    {
        var shown = WithoutHidden(state.StoryReposts.ToArray());
        if (shown.Length == 0)
        {
            VybeChrome.Mute(frame, stack.Take(frame.Units(36f)), "Reposts land here.", night);
            return;
        }

        for (var index = 0; index < shown.Length; index++)
        {
            DrawPearlCard(frame, stack.Take(frame.Units(PostCardHeight(frame, shown[index]))), shown[index]);
        }
    }

    private PearlPost[] SavedBoard()
    {
        var hits = new List<PearlPost>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < state.StorySaves.Count; index++)
        {
            var post = state.StorySaves[index];
            if (PearlGone(post.Id) || !seen.Add(post.Id))
            {
                continue;
            }

            hits.Add(post);
        }

        foreach (var post in OpenBoard())
        {
            if (PearlGone(post.Id) || !state.KeptPosts.Contains(post.Id) || !seen.Add(post.Id))
            {
                continue;
            }

            hits.Add(post);
        }

        return hits.ToArray();
    }

    private void KeepPost(PearlPost post)
    {
        var was = state.KeptPosts.Contains(post.Id);
        state.ToggleKeptPost(post.Id, PostShots(post));
        if (was)
        {
            DropStorySave(post.Id);
        }
        else
        {
            state.HiddenPearls.Remove(post.Id);
            RememberStorySave(post);
        }

        state.Save(paths);
    }

    private void RememberStorySave(PearlPost post)
    {
        for (var index = 0; index < state.StorySaves.Count; index++)
        {
            if (string.Equals(state.StorySaves[index].Id, post.Id, StringComparison.Ordinal))
            {
                return;
            }
        }

        state.StorySaves.Insert(0, post);
    }

    private void DropStorySave(string id)
    {
        for (var index = state.StorySaves.Count - 1; index >= 0; index--)
        {
            if (string.Equals(state.StorySaves[index].Id, id, StringComparison.Ordinal))
            {
                state.StorySaves.RemoveAt(index);
            }
        }
    }

    private string FeedRepostId(string postId) => "repost-" + postId;

    private bool PostShared(string postId)
    {
        if (postId.Length == 0)
        {
            return false;
        }

        var id = FeedRepostId(postId);
        for (var index = 0; index < state.StoryReposts.Count; index++)
        {
            if (string.Equals(state.StoryReposts[index].Id, id, StringComparison.Ordinal))
            {
                return !PearlGone(id);
            }
        }

        return false;
    }

    private int PostShares(PearlPost post)
    {
        var on = PostShared(post.Id);
        if (on == post.Reposted)
        {
            return post.Reposts;
        }

        return on ? post.Reposts + 1 : Math.Max(0, post.Reposts - 1);
    }

    private void SharePost(PearlPost post)
    {
        if (post.Id.Length == 0)
        {
            return;
        }

        var id = FeedRepostId(post.Id);
        state.HiddenPearls.Remove(id);
        for (var index = 0; index < state.StoryReposts.Count; index++)
        {
            if (string.Equals(state.StoryReposts[index].Id, id, StringComparison.Ordinal))
            {
                state.Save(paths);
                return;
            }
        }

        state.StoryReposts.Insert(0, new PearlPost(
            id,
            "me",
            ProfileName(),
            state.Handle,
            OwnFacePath().Length > 0 ? OwnFacePath() : pearl.Current.MeAvatarUrl,
            post.Body ?? string.Empty,
            "now",
            true,
            false,
            0,
            0,
            Math.Max(1, post.Reposts + 1),
            true,
            post.Id,
            post.AuthorName ?? string.Empty,
            post.Body ?? string.Empty,
            post.Media ?? [],
            "repost",
            post.ContentRating,
            post.Descriptors,
            post.Hashtags));
        if (pearl.Current.SignedIn && pearl.PostById(post.Id) is not null)
        {
            pearl.Repost(post.Id);
        }

        state.ProfilePane = 3;
        state.Save(paths);
    }

    private bool DrawFeedActions(in AppletFrame frame, Rect bar, PearlPost post, bool night)
    {
        var tone = VybeChrome.Tone(night);
        var slot = bar.Width / 4f;
        var likeHit = Rect.FromSize(bar.Min, new Vector2(slot, bar.Height));
        var commentHit = Rect.FromSize(new Vector2(bar.Min.X + slot, bar.Min.Y), new Vector2(slot, bar.Height));
        var repostHit = Rect.FromSize(new Vector2(bar.Min.X + slot * 2f, bar.Min.Y), new Vector2(slot, bar.Height));
        var saveHit = Rect.FromSize(new Vector2(bar.Min.X + slot * 3f, bar.Min.Y), new Vector2(slot, bar.Height));
        var liked = state.PostHearted(post);
        var shared = PostShared(post.Id) || post.Reposted;
        var kept = state.KeptPosts.Contains(post.Id);
        VybeChrome.PostGlyph(frame, likeHit, VybeChrome.LikeGlyph, CompactCount(state.PostHearts(post)), liked, tone,
            "♥");
        VybeChrome.PostGlyph(frame, commentHit, VybeChrome.CommentGlyph, CommentMark(post), false, tone, "💬");
        VybeChrome.PostGlyph(frame, repostHit, VybeChrome.RepostGlyph, CompactCount(PostShares(post)), shared, tone,
            "↻");
        VybeChrome.PostGlyph(frame, saveHit, VybeChrome.SaveGlyph, kept ? "1" : string.Empty, kept, tone, "⇩");
        if (frame.Input.ConsumeClick(likeHit))
        {
            HeartPost(post.Id, post.Liked);
            return true;
        }

        if (frame.Input.ConsumeClick(commentHit))
        {
            OpenPostComments(post.Id);
            return true;
        }

        if (frame.Input.ConsumeClick(repostHit.Expand(frame.Units(4f))))
        {
            SharePost(post);
            return true;
        }

        if (frame.Input.ConsumeClick(saveHit))
        {
            KeepPost(post);
            return true;
        }

        return false;
    }

    private static IEnumerable<string> PostShots(PearlPost post)
    {
        foreach (var media in post.Media)
        {
            if (media.Url.Length > 0)
            {
                yield return media.Url;
            }
        }
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
                if (!SheetBlocking() && frame.Input.ConsumeClick(shot))
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

        if (VybeChrome.Chip(frame, stack.Take(frame.Units(36f)),
                post is { } keep && state.KeptPosts.Contains(keep.Id) ? "Unsave" : "Save", false, night) &&
            post is not null)
        {
            KeepPost(post.Value);
            state.SharePostId = string.Empty;
        }

        if (VybeChrome.Chip(frame, stack.Take(frame.Units(36f)), "Quote", false, night) && post is not null)
        {
            state.QuoteOf = post.Value.Id;
            state.SharePostId = string.Empty;
            BeginCompose(VybeDemo.IsPlusPost(post.Value), keepQuote: true);
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

    private bool SheetBlocking() =>
        state.DropPostId.Length > 0 || state.ReportOpen;

    private bool CanDropPost(PearlPost post) => post.Id.Length > 0;

    private void DropPost(string id)
    {
        if (id.Length == 0)
        {
            return;
        }

        for (var index = state.Posted.Count - 1; index >= 0; index--)
        {
            if (string.Equals(state.Posted[index].Id, id, StringComparison.Ordinal))
            {
                state.Posted.RemoveAt(index);
            }
        }

        for (var club = 0; club < state.Clubs.Count; club++)
        {
            var posts = state.Clubs[club].Posts;
            for (var index = posts.Count - 1; index >= 0; index--)
            {
                if (string.Equals(posts[index].Id, id, StringComparison.Ordinal))
                {
                    posts.RemoveAt(index);
                }
            }
        }

        for (var index = state.StoryReposts.Count - 1; index >= 0; index--)
        {
            if (string.Equals(state.StoryReposts[index].Id, id, StringComparison.Ordinal))
            {
                state.StoryReposts.RemoveAt(index);
            }
        }

        state.KeptPosts.Remove(id);
        DropStorySave(id);
        state.HiddenPearls.Add(id);
        if (id.StartsWith("repost-", StringComparison.Ordinal))
        {
            state.HiddenPearls.Add(id["repost-".Length..]);
        }

        if (string.Equals(state.PostKey, id, StringComparison.Ordinal))
        {
            state.PostKey = string.Empty;
            if (state.Page is NightPage.Post or NightPage.PostComments or NightPage.PhotoView)
            {
                state.Back();
            }
        }

        state.DropPostId = string.Empty;
        state.Save(paths);
    }

    private void DrawDropPostSheet(in AppletFrame frame, Rect body)
    {
        var night = state.Night;
        var sheet = body.BottomSlice(frame.Units(168f));
        frame.Paint.Fill(body, new Vector4(0f, 0f, 0f, 0.46f));
        VybeChrome.Plate(frame, sheet, frame.Units(16f), night);
        var stack = new Stack(sheet.Inset(frame.Units(12f)), StackAxis.Vertical, frame.Units(8f));
        VybeChrome.Title(frame, stack.Take(frame.Units(22f)), "Remove post", night);
        VybeChrome.Mute(frame, stack.Take(frame.Units(28f)),
            "This removes the post from your profile.", night);
        var dim = new Rect(body.Min, new Vector2(body.Max.X, sheet.Min.Y));
        var kill = stack.Take(frame.Units(40f));
        VybeChrome.Primary(frame, kill, "Delete post", night);
        if (frame.Input.ConsumeClick(kill))
        {
            DropPost(state.DropPostId);
            return;
        }

        var cancel = stack.Take(frame.Units(36f));
        if (VybeChrome.Chip(frame, cancel, "Cancel", false, night) || frame.Input.ConsumeClick(dim))
        {
            state.DropPostId = string.Empty;
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
