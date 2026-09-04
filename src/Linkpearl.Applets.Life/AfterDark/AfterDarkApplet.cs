using System.Globalization;
using Linkpearl.Applets;
using Linkpearl.Geometry;
using Linkpearl.Layout;
using Linkpearl.Media;
using Linkpearl.Modules;
using Linkpearl.Net;
using Linkpearl.Painting;
using Linkpearl.Platform;

namespace Linkpearl.Applets.Life.AfterDark;

public sealed partial class AfterDarkApplet : IApplet
{
    public static readonly AppletManifest Manifest = new()
    {
        Id = "afterdark",
        DisplayNameKey = "Daylight",
        Family = AppletFamily.Social,
        Glyph = "☽",
        HomeOrder = 9,
        Capabilities = AppletCapabilities.AgeRestricted | AppletCapabilities.RequiresAccount,
    };

    private static readonly string[] Tabs = { "Home", "Discover", "Messages", "Alerts", "Profile" };

    private readonly IGameSession game;
    private readonly HostPaths paths;
    private readonly IPearlHub pearl;
    private readonly AfterDarkState state;

    public AfterDarkApplet(IGameSession game, HostPaths paths, IPearlHub pearl)
    {
        this.game = game;
        this.paths = paths;
        this.pearl = pearl;
        state = AfterDarkState.Load(paths, game.Character.Name);
    }

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
        state.Bind(pearl.Current);
        AfterDarkChrome.Fill(frame, state.Night);
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
            var nav = frame.Units(52f);
            DrawNav(frame, frame.Content.BottomSlice(nav));
            var body = frame.Content.Inset(new Edges(frame.Units(14f), frame.Units(10f), frame.Units(14f),
                nav + frame.Units(8f)));
            if (state.Page != NightPage.Tabs)
            {
                AfterDarkChrome.Wheel(frame, body, state, frame.Units(900f));
                frame.Paint.PushClip(body);
                DrawStack(frame, body.Translate(new Vector2(0f, -state.Scroll)));
                frame.Paint.PopClip();
            }
            else
            {
                DrawTabBody(frame, body);
            }

            if (state.SharePostId.Length > 0 && state.Page != NightPage.ShareSend)
            {
                DrawShareSheet(frame, frame.Content.Inset(new Edges(frame.Units(14f), 0f, frame.Units(14f),
                    nav + frame.Units(8f))));
            }
        }

        if (pearl.Current.Notice.Length > 0 && state.Page is NightPage.Tabs or NightPage.Compose)
        {
            AfterDarkChrome.Mute(frame, frame.Content.TopSlice(frame.Units(18f)).Inset(new Edges(frame.Units(16f), 0f)),
                pearl.Current.Notice, state.Night);
        }

        state.TickWash(frame.DeltaSeconds, paths);
        AfterDarkChrome.Wash(frame, state.Wash, state.WashToNight);
    }

    private void DrawTabBody(in AppletFrame frame, Rect body)
    {
        AfterDarkChrome.Wheel(frame, body, state, frame.Units(1400f));
        frame.Paint.PushClip(body);
        var shifted = body.Translate(new Vector2(0f, -state.Scroll));
        switch (state.Tab)
        {
            case NightTab.Discover:
                DrawDiscover(frame, shifted);
                break;
            case NightTab.Messages:
                DrawMessages(frame, shifted);
                break;
            case NightTab.Alerts:
                DrawAlerts(frame, shifted);
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
        var tone = AfterDarkChrome.Tone(night);
        var snap = pearl.Current;
        var stack = new Stack(area, StackAxis.Vertical, frame.Units(10f));
        var head = stack.Take(frame.Units(28f));
        DrawModeMark(frame, head.LeftSlice(head.Width * 0.62f));
        var search = head.RightSlice(frame.Units(56f)).LeftSlice(frame.Units(26f));
        var bell = head.RightSlice(frame.Units(26f));
        frame.Paint.StrokeCircle(search.Center, frame.Units(7f), tone.Ink, frame.Units(1.4f));
        frame.Paint.Line(search.Center + new Vector2(frame.Units(5f), frame.Units(5f)),
            search.Center + new Vector2(frame.Units(10f), frame.Units(10f)), tone.Ink, frame.Units(1.4f));
        if (frame.Input.ConsumeClick(search))
        {
            state.Search = string.Empty;
            state.Open(NightPage.Search);
        }

        DrawBellGlyph(frame, bell.Center, frame.Units(9f), tone.Ink);
        if (snap.Notes.Length + snap.UnreadTotal > 0)
        {
            frame.Paint.FillCircle(bell.Max - new Vector2(frame.Units(6f), frame.Units(18f)), frame.Units(4f),
                tone.Accent);
        }

        if (frame.Input.ConsumeClick(bell))
        {
            state.Tab = NightTab.Alerts;
        }

        AfterDarkChrome.Kicker(frame, stack.Take(frame.Units(14f)), "STORIES", night);
        var stories = stack.Take(frame.Units(64f));
        var storyPeople = StoryPeople();
        var own = state.OwnStory.Length > 0 ? 1 : 0;
        var slots = 1 + own + Math.Min(4, storyPeople.Count);
        var storyW = stories.Width / Math.Max(slots, 1);
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
            AfterDarkChrome.StoryRing(frame, self.TopSlice(frame.Units(40f)).Center, frame.Units(16f), seen, night);
            DrawFace(frame, self.TopSlice(frame.Units(40f)).Center, frame.Units(14f), snap.MeAvatarUrl, tone.Accent,
                night);
            AfterDarkChrome.Mute(frame, self.BottomSlice(frame.Units(16f)), "You", night);
            if (frame.Input.ConsumeClick(self))
            {
                state.StoryIndex = -1;
                state.ViewedStories.Add(-1);
                state.Open(NightPage.Story);
                state.Save(paths);
            }

            slot++;
        }

        for (var index = 0; index < storyPeople.Count && index < 4; index++)
        {
            var person = storyPeople[index];
            var cell = Rect.FromSize(new Vector2(stories.Min.X + storyW * (slot + index), stories.Min.Y),
                new Vector2(storyW - frame.Units(6f), stories.Height));
            var seen = state.ViewedStories.Contains(person.Id);
            AfterDarkChrome.StoryRing(frame, cell.TopSlice(frame.Units(40f)).Center, frame.Units(16f), seen, night);
            DrawFace(frame, cell.TopSlice(frame.Units(40f)).Center, frame.Units(14f), person.AvatarUrl, person.Wash,
                night);
            AfterDarkChrome.Mute(frame, cell.BottomSlice(frame.Units(16f)), person.Name, night);
            if (frame.Input.ConsumeClick(cell))
            {
                state.StoryIndex = person.Id;
                state.ViewedStories.Add(person.Id);
                state.Open(NightPage.Story);
                state.Save(paths);
            }
        }

        var checkin = stack.Take(frame.Units(44f));
        AfterDarkChrome.Plate(frame, checkin, frame.Units(16f), night);
        AfterDarkChrome.Mute(frame, checkin.Inset(new Edges(frame.Units(12f), 0f, frame.Units(72f), 0f)),
            night ? "What's the vibe tonight?" : "What's on your mind?", night);
        var postBtn = checkin.RightSlice(frame.Units(64f)).Inset(frame.Units(6f));
        AfterDarkChrome.Primary(frame, postBtn, "Post", night);
        if (frame.Input.ConsumeClick(postBtn) || frame.Input.ConsumeClick(checkin))
        {
            state.Caption = string.Empty;
            state.QuoteOf = string.Empty;
            state.DraftMedia.Clear();
            state.Open(NightPage.Compose);
        }

        var tabs = stack.Take(frame.Units(32f));
        if (AfterDarkChrome.Chip(frame, tabs.LeftSlice(tabs.Width * 0.48f), "Following", !state.FeedEveryone, night))
        {
            state.FeedEveryone = false;
            pearl.WatchFeed("following");
        }

        if (AfterDarkChrome.Chip(frame, tabs.RightSlice(tabs.Width * 0.48f), "For You", state.FeedEveryone, night))
        {
            state.FeedEveryone = true;
            pearl.WatchFeed("foryou");
        }

        var tags = LiveHashes(snap.Feed);
        if (tags.Count > 0)
        {
            AfterDarkChrome.Kicker(frame, stack.Take(frame.Units(14f)), "TRENDING", night);
            var row = stack.Take(frame.Units(28f));
            var tw = row.Width / tags.Count;
            for (var index = 0; index < tags.Count; index++)
            {
                var chip = Rect.FromSize(new Vector2(row.Min.X + tw * index + frame.Units(2f), row.Min.Y),
                    new Vector2(tw - frame.Units(4f), row.Height));
                if (AfterDarkChrome.Chip(frame, chip, tags[index], state.Hashtag == tags[index], night))
                {
                    state.Hashtag = state.Hashtag == tags[index] ? string.Empty : tags[index];
                    state.Tab = NightTab.Discover;
                    state.DiscoverPane = 1;
                    state.Scroll = 0f;
                }
            }
        }

        if (!snap.SignedIn)
        {
            AfterDarkChrome.Mute(frame, stack.Take(frame.Units(36f)), "Sign in from You to load the feed.", night);
            return;
        }

        if (!snap.FeedLive)
        {
            AfterDarkChrome.Mute(frame, stack.Take(frame.Units(36f)),
                "Pearlgate is not hosting a public feed yet. You can still write a post.", night);
        }

        var shown = 0;
        foreach (var wallPost in VisibleFeed(snap.Feed))
        {
            DrawPearlCard(frame, stack.Take(frame.Units(PostCardHeight(frame, wallPost))), wallPost);
            shown++;
        }

        if (shown == 0 && snap.FeedLive)
        {
            AfterDarkChrome.Mute(frame, stack.Take(frame.Units(36f)), "Nothing on the feed yet. Be the first to post.",
                night);
        }
    }

    private void DrawDiscover(in AppletFrame frame, Rect area)
    {
        var night = state.Night;
        var tone = AfterDarkChrome.Tone(night);
        var stack = new Stack(area, StackAxis.Vertical, frame.Units(10f));
        var head = stack.Take(frame.Units(28f));
        AfterDarkChrome.Title(frame, head.LeftSlice(head.Width * 0.7f), "Discover", night);
        var filter = head.RightSlice(frame.Units(32f));
        DrawFilterGlyph(frame, filter.Center, frame.Units(9f), tone.Ink);
        if (frame.Input.ConsumeClick(filter))
        {
            state.Open(NightPage.Filters);
        }

        state.Search = frame.TextField.Draw("ad-find", stack.Take(frame.Units(34f)), state.Search,
            "Search people and posts");
        if (state.Search.Length >= 2)
        {
            pearl.NoteQuery(state.Search);
        }

        var panes = stack.Take(frame.Units(32f));
        if (AfterDarkChrome.Chip(frame, panes.LeftSlice(panes.Width / 3f).Inset(new Edges(frame.Units(2f), 0f)),
                "People", state.DiscoverPane == 0, night))
        {
            state.DiscoverPane = 0;
        }

        if (AfterDarkChrome.Chip(frame, Rect.FromSize(new Vector2(panes.Min.X + panes.Width / 3f, panes.Min.Y),
                new Vector2(panes.Width / 3f, panes.Height)).Inset(new Edges(frame.Units(2f), 0f)),
                "Posts", state.DiscoverPane == 2, night))
        {
            state.DiscoverPane = 2;
        }

        if (AfterDarkChrome.Chip(frame, panes.RightSlice(panes.Width / 3f).Inset(new Edges(frame.Units(2f), 0f)),
                "Hashtags", state.DiscoverPane == 1, night))
        {
            state.DiscoverPane = 1;
        }

        if (state.DiscoverPane == 1)
        {
            var tags = LiveHashes(pearl.Current.Feed);
            if (tags.Count == 0)
            {
                AfterDarkChrome.Mute(frame, stack.Take(frame.Units(28f)), "Hashtags show up when people use them.",
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

            AfterDarkChrome.Kicker(frame, stack.Take(frame.Units(14f)), "MATCHING POSTS", night);
            var hits = 0;
            foreach (var wallPost in VisibleFeed(pearl.Current.Feed))
            {
                DrawPearlCard(frame, stack.Take(frame.Units(PostCardHeight(frame, wallPost))), wallPost);
                hits++;
            }

            if (hits == 0)
            {
                AfterDarkChrome.Mute(frame, stack.Take(frame.Units(28f)), "No matching posts.", night);
            }

            return;
        }

        if (state.DiscoverPane == 2)
        {
            AfterDarkChrome.Kicker(frame, stack.Take(frame.Units(14f)), "POSTS", night);
            var shownPosts = 0;
            var source = state.Search.Length >= 2 ? pearl.Current.SearchPosts : pearl.Current.Feed;
            foreach (var wallPost in VisibleFeed(source))
            {
                DrawPearlCard(frame, stack.Take(frame.Units(PostCardHeight(frame, wallPost))), wallPost);
                shownPosts++;
            }

            if (shownPosts == 0)
            {
                AfterDarkChrome.Mute(frame, stack.Take(frame.Units(28f)),
                    pearl.Current.FeedLive ? "Nothing on the feed yet." : "Pearlgate is not hosting posts yet.",
                    night);
            }

            return;
        }

        if (night)
        {
            AfterDarkChrome.Kicker(frame, stack.Take(frame.Units(14f)), "WHO'S AROUND", night);
            var featured = 0;
            for (var index = 0; index < state.Roster.Count && featured < 2; index++)
            {
                var person = state.Roster[index];
                if (!state.Passes(person))
                {
                    continue;
                }

                featured++;
            }

            if (featured == 0)
            {
                AfterDarkChrome.Mute(frame, stack.Take(frame.Units(28f)), "Nobody around yet.", night);
            }
            else
            {
                var hot = stack.Take(frame.Units(92f));
                featured = 0;
                for (var index = 0; index < state.Roster.Count && featured < 2; index++)
                {
                    var person = state.Roster[index];
                    if (!state.Passes(person))
                    {
                        continue;
                    }

                    var cell = featured == 0 ? hot.LeftSlice(hot.Width * 0.48f) : hot.RightSlice(hot.Width * 0.48f);
                    DrawHotCard(frame, cell, person);
                    featured++;
                }
            }

            AfterDarkChrome.Kicker(frame, stack.Take(frame.Units(14f)), "FIND YOUR VIBE", night);
            var vibes = new[] { "Near You", "Online Now", "New Here", "VIP Only" };
            var vibeRow = stack.Take(frame.Units(52f));
            var vw = vibeRow.Width / 4f;
            for (var index = 0; index < 4; index++)
            {
                var cell = Rect.FromSize(new Vector2(vibeRow.Min.X + vw * index + frame.Units(2f), vibeRow.Min.Y),
                    new Vector2(vw - frame.Units(4f), vibeRow.Height));
                if (AfterDarkChrome.Chip(frame, cell, vibes[index], state.Vibe == index + 1, night))
                {
                    state.Vibe = state.Vibe == index + 1 ? 0 : index + 1;
                }
            }
        }

        AfterDarkChrome.Kicker(frame, stack.Take(frame.Units(14f)),
            night ? "WHO'S OUT" : "PEOPLE YOU MAY KNOW", night);
        if (!state.SignedIn)
        {
            AfterDarkChrome.Mute(frame, stack.Take(frame.Units(36f)),
                "Sign in from You so Discover can find people.", night);
            return;
        }

        var listed = 0;
        for (var index = 0; index < state.Roster.Count && listed < 8; index++)
        {
            var person = state.Roster[index];
            if (state.Connected.Contains(person.Id) || !state.Passes(person))
            {
                continue;
            }

            DrawMayKnow(frame, stack.Take(frame.Units(56f)), person);
            listed++;
        }

        if (listed == 0)
        {
            AfterDarkChrome.Mute(frame, stack.Take(frame.Units(28f)),
                state.Roster.Count == 0 ? "Nobody nearby yet." : "You're following everyone nearby.", night);
        }
    }

    private void DrawFeed(in AppletFrame frame, Rect area)
    {
        var night = state.Night;
        var stack = new Stack(area, StackAxis.Vertical, frame.Units(10f));
        if (state.Page == NightPage.FeedWall &&
            AfterDarkChrome.Back(frame, stack.Take(frame.Units(28f)), "Feed", night))
        {
            state.Back();
            return;
        }

        var head = stack.Take(frame.Units(32f));
        if (AfterDarkChrome.Chip(frame, head.LeftSlice(head.Width * 0.42f), "Following", !state.FeedEveryone, night))
        {
            state.FeedEveryone = false;
        }

        if (AfterDarkChrome.Chip(frame,
                Rect.FromSize(new Vector2(head.Min.X + head.Width * 0.42f, head.Min.Y),
                    new Vector2(head.Width * 0.42f, head.Height)), "For You", state.FeedEveryone, night))
        {
            state.FeedEveryone = true;
        }

        var compose = head.RightSlice(frame.Units(36f));
        AfterDarkChrome.Glow(frame, compose, frame.Units(10f), true, night);
        frame.Text.DrawIn(compose, "+",
            new TextStyle(FontRole.Title, AfterDarkChrome.Tone(night).Accent, TextAlign.Center));
        if (frame.Input.ConsumeClick(compose))
        {
            state.Caption = string.Empty;
            state.AudienceEveryone = true;
            state.Open(NightPage.Compose);
        }

        var shown = 0;
        foreach (var wallPost in VisibleFeed(pearl.Current.Feed))
        {
            DrawPearlCard(frame, stack.Take(frame.Units(PostCardHeight(frame, wallPost))), wallPost);
            shown++;
        }

        if (shown == 0)
        {
            AfterDarkChrome.Mute(frame, stack.Take(frame.Units(36f)),
                pearl.Current.FeedLive ? "Nothing shared yet." : "Pearlgate is not hosting a feed yet.",
                state.Night);
        }
    }

    private void DrawMessages(in AppletFrame frame, Rect area)
    {
        var night = state.Night;
        var snapshot = pearl.Current;
        var stack = new Stack(area, StackAxis.Vertical, frame.Units(10f));
        AfterDarkChrome.Title(frame, stack.Take(frame.Units(28f)), "Messages", night);
        if (!snapshot.SignedIn)
        {
            AfterDarkChrome.Mute(frame, stack.Take(frame.Units(48f)),
                "Sign in from You to load Pearlgate chats.", night);
            return;
        }

        var shown = 0;
        foreach (var chat in snapshot.Chats)
        {
            DrawPearlInbox(frame, stack.Take(frame.Units(64f)), chat, night);
            shown++;
        }

        foreach (var id in state.Connected)
        {
            if (!state.TryFind(id, out var person))
            {
                continue;
            }

            DrawInboxRow(frame, stack.Take(frame.Units(64f)), person);
            shown++;
        }

        if (shown == 0)
        {
            AfterDarkChrome.Mute(frame, stack.Take(frame.Units(36f)), "No conversations yet.", night);
        }
    }

    private void DrawPearlInbox(in AppletFrame frame, Rect area, PearlChat chat, bool night)
    {
        AfterDarkChrome.Plate(frame, area, frame.Units(12f), night);
        var inner = area.Inset(frame.Units(8f));
        AfterDarkChrome.Title(frame, inner.TopSlice(frame.Units(20f)), chat.Title, night);
        var preview = chat.Preview.Length > 0 ? chat.Preview : "No messages yet";
        AfterDarkChrome.Mute(frame, inner.BottomSlice(frame.Units(16f)), preview, night);
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
        var head = stack.Take(frame.Units(28f));
        AfterDarkChrome.Title(frame, head.LeftSlice(head.Width * 0.7f), "Notifications", night);

        var empty = true;
        foreach (var note in pearl.Current.Notes)
        {
            empty = false;
            var row = stack.Take(frame.Units(52f));
            AfterDarkChrome.Plate(frame, row, frame.Units(12f), night);
            var inner = row.Inset(frame.Units(10f));
            AfterDarkChrome.Portrait(frame, inner.LeftSlice(frame.Units(28f)).Center, frame.Units(12f),
                AfterDarkChrome.Tone(night).CardHi, night);
            var body = inner.Inset(new Edges(frame.Units(36f), 0f, 0f, 0f));
            var line = note.ActorName + (note.Line.Length > 0 ? " " + note.Line : " " + note.Kind);
            frame.Text.DrawEllipsized(body.TopSlice(frame.Units(16f)), line,
                new TextStyle(FontRole.CaptionStrong, AfterDarkChrome.Tone(night).Ink));
            AfterDarkChrome.Mute(frame, body.BottomSlice(frame.Units(14f)), note.When, night);
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
            AfterDarkChrome.Mute(frame, stack.Take(frame.Units(24f)), "Activity alerts are not on Pearlgate yet.",
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
            AfterDarkChrome.Plate(frame, row, frame.Units(12f), night);
            var inner = row.Inset(frame.Units(10f));
            frame.Text.DrawEllipsized(inner.TopSlice(frame.Units(16f)), chat.Title,
                new TextStyle(FontRole.CaptionStrong, AfterDarkChrome.Tone(night).Ink));
            AfterDarkChrome.Mute(frame, inner.BottomSlice(frame.Units(14f)),
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
            AfterDarkChrome.Plate(frame, row, frame.Units(12f), night);
            var inner = row.Inset(frame.Units(10f));
            frame.Text.DrawEllipsized(inner.TopSlice(frame.Units(16f)), notice.Title,
                new TextStyle(FontRole.CaptionStrong, AfterDarkChrome.Tone(night).Ink));
            AfterDarkChrome.Mute(frame, inner.BottomSlice(frame.Units(14f)), notice.Body, night);
        }

        if (empty)
        {
            AfterDarkChrome.Mute(frame, stack.Take(frame.Units(28f)), "You're caught up.", night);
        }
    }

    private void DrawMe(in AppletFrame frame, Rect area)
    {
        var night = state.Night;
        var tone = AfterDarkChrome.Tone(night);
        var stack = new Stack(area, StackAxis.Vertical, frame.Units(10f));
        var cover = stack.Take(frame.Units(88f));
        frame.Paint.Fill(cover, tone.AccentDim, frame.Units(16f));
        DrawFace(frame, new Vector2(cover.Min.X + frame.Units(36f), cover.Max.Y - frame.Units(8f)), frame.Units(26f),
            pearl.Current.MeAvatarUrl, tone.Accent, night);
        if (frame.Input.ConsumeClick(Rect.FromSize(
                new Vector2(cover.Min.X + frame.Units(10f), cover.Max.Y - frame.Units(42f)),
                new Vector2(frame.Units(52f), frame.Units(52f)))))
        {
            state.PickingAvatar = true;
            state.Open(NightPage.PhotoPick);
        }

        AfterDarkChrome.Title(frame, stack.Take(frame.Units(24f)), state.DisplayName, night);
        AfterDarkChrome.Mute(frame, stack.Take(frame.Units(18f)), state.Handle, night);
        AfterDarkChrome.Mute(frame, stack.Take(frame.Units(32f)),
            state.About.Length > 0 ? state.About : "Tap Edit Profile to write a bio.", night);

        var mine = pearl.Current.WatchedUserId == "me" ? pearl.Current.ProfilePosts : [];
        var stats = stack.Take(frame.Units(48f));
        var postsHit = stats.LeftSlice(stats.Width / 3f);
        var followersHit = Rect.FromSize(new Vector2(stats.Min.X + stats.Width / 3f, stats.Min.Y),
            new Vector2(stats.Width / 3f, stats.Height));
        var followingHit = stats.RightSlice(stats.Width / 3f);
        DrawStat(frame, postsHit, mine.Length.ToString(CultureInfo.InvariantCulture), "Posts", night);
        DrawStat(frame, followersHit, pearl.Current.Followers.ToString(CultureInfo.InvariantCulture), "Followers",
            night);
        DrawStat(frame, followingHit, pearl.Current.Following.ToString(CultureInfo.InvariantCulture), "Following",
            night);
        if (frame.Input.ConsumeClick(postsHit))
        {
            state.Open(NightPage.FeedWall);
        }

        if (frame.Input.ConsumeClick(followersHit))
        {
            state.Open(NightPage.Followers);
        }

        if (frame.Input.ConsumeClick(followingHit))
        {
            state.Open(NightPage.Following);
        }

        var actions = stack.Take(frame.Units(40f));
        AfterDarkChrome.Primary(frame, actions.Inset(new Edges(0f, 0f, frame.Units(48f), 0f)), "Edit Profile", night);
        if (frame.Input.ConsumeClick(actions.Inset(new Edges(0f, 0f, frame.Units(48f), 0f))))
        {
            state.Open(NightPage.OnboardIdentity);
        }

        var gear = actions.RightSlice(frame.Units(40f));
        AfterDarkChrome.Glow(frame, gear, frame.Units(10f), false, night);
        frame.Paint.StrokeCircle(gear.Center, frame.Units(8f), tone.Ink, frame.Units(1.4f));
        if (frame.Input.ConsumeClick(gear))
        {
            state.Open(NightPage.Settings);
        }

        var interests = state.Intents.Where(tag => night || tag != "ERP").Take(4).ToArray();
        var tags = stack.Take(frame.Units(28f));
        if (interests.Length == 0)
        {
            if (AfterDarkChrome.Chip(frame, tags, "Add intents", false, night))
            {
                state.Open(NightPage.OnboardIntent);
            }
        }
        else
        {
            var tw = tags.Width / interests.Length;
            for (var index = 0; index < interests.Length; index++)
            {
                var chip = Rect.FromSize(new Vector2(tags.Min.X + tw * index + frame.Units(2f), tags.Min.Y),
                    new Vector2(tw - frame.Units(4f), tags.Height));
                if (AfterDarkChrome.Chip(frame, chip, interests[index], state.Pole(interests[index]) != FilterPole.Neutral,
                        night))
                {
                    state.CycleFilter(interests[index]);
                    state.Tab = NightTab.Discover;
                    state.DiscoverPane = 0;
                    state.Scroll = 0f;
                }
            }
        }

        var panes = stack.Take(frame.Units(32f));
        if (AfterDarkChrome.Chip(frame, panes.LeftSlice(panes.Width * 0.48f), "Posts", state.ProfilePane == 0, night))
        {
            state.ProfilePane = 0;
        }

        if (AfterDarkChrome.Chip(frame, panes.RightSlice(panes.Width * 0.48f), "Photos", state.ProfilePane == 1, night))
        {
            state.ProfilePane = 1;
        }

        if (state.ProfilePane == 1)
        {
            DrawPhotoGrid(frame, stack, mine, night);
            return;
        }

        if (mine.Length == 0)
        {
            AfterDarkChrome.Mute(frame, stack.Take(frame.Units(36f)),
                pearl.Current.SignedIn ? "Your posts will land here." : "Sign in from You to show your posts.",
                night);
            return;
        }

        foreach (var wallPost in mine)
        {
            DrawPearlCard(frame, stack.Take(frame.Units(PostCardHeight(frame, wallPost))), wallPost);
        }
    }

    private void DrawNav(in AppletFrame frame, Rect strip)
    {
        var night = state.Night;
        var tone = AfterDarkChrome.Tone(night);
        frame.Input.Claim(strip);
        frame.Paint.Fill(strip, tone.Card);
        var width = strip.Width / Tabs.Length;
        for (var index = 0; index < Tabs.Length; index++)
        {
            var cell = Rect.FromSize(new Vector2(strip.Min.X + width * index, strip.Min.Y),
                new Vector2(width, strip.Height));
            var on = state.Page == NightPage.Tabs && (int)state.Tab == index;
            var ink = on ? tone.Accent : tone.Mute;
            DrawTabGlyph(frame, cell.TopSlice(frame.Units(26f)).Center, frame.Units(8f), index, ink);
            frame.Text.DrawIn(cell.BottomSlice(frame.Units(16f)), Tabs[index],
                new TextStyle(FontRole.Caption, ink, TextAlign.Center));
            if (index == 2 && (state.Connected.Count > 0 || pearl.Current.UnreadTotal > 0))
            {
                frame.Paint.FillCircle(cell.Center + new Vector2(frame.Units(10f), -frame.Units(10f)), frame.Units(3.5f),
                    tone.Accent);
            }

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
            new TextStyle(FontRole.BodyStrong, AfterDarkChrome.Night.Ink));
        frame.Text.DrawIn(area.BottomSlice(frame.Units(16f)).Inset(new Edges(frame.Units(8f), 0f)), person.World,
            new TextStyle(FontRole.Caption, AfterDarkChrome.Night.Mute));
        if (frame.Input.ConsumeClick(area))
        {
            OpenPerson(person.GateId);
        }
    }

    private void DrawDiscoverTile(in AppletFrame frame, Rect area, ScenePerson person)
    {
        var night = state.Night;
        frame.Paint.Fill(area, person.Wash, frame.Units(14f));
        AfterDarkChrome.Title(frame, area.BottomSlice(frame.Units(32f)).Inset(new Edges(frame.Units(8f), 0f, 0f,
            frame.Units(14f))), person.Name, night);
        AfterDarkChrome.Mute(frame, area.BottomSlice(frame.Units(14f)).Inset(new Edges(frame.Units(8f), 0f)),
            person.World, night);
        if (person.Online)
        {
            frame.Paint.FillCircle(new Vector2(area.Max.X - frame.Units(12f), area.Min.Y + frame.Units(12f)),
                frame.Units(4f), AfterDarkChrome.Online);
        }

        if (frame.Input.ConsumeClick(area))
        {
            OpenPerson(person.GateId);
        }
    }

    private void DrawMayKnow(in AppletFrame frame, Rect area, ScenePerson person)
    {
        var night = state.Night;
        AfterDarkChrome.Plate(frame, area, frame.Units(12f), night);
        var inner = area.Inset(frame.Units(8f));
        DrawFace(frame, inner.LeftSlice(frame.Units(40f)).Center, frame.Units(16f), person.AvatarUrl, person.Wash,
            night);
        var copy = inner.Inset(new Edges(frame.Units(48f), 0f, frame.Units(84f), 0f));
        AfterDarkChrome.Title(frame, copy.TopSlice(frame.Units(20f)), person.Name, night);
        AfterDarkChrome.Mute(frame, copy.BottomSlice(frame.Units(16f)), person.Handle + " · " + person.World, night);
        var linked = state.Connected.Contains(person.Id);
        var go = inner.RightSlice(frame.Units(80f)).Inset(new Edges(0f, frame.Units(10f)));
        AfterDarkChrome.Primary(frame, go, linked ? "Message" : LinkVerb(night), night);
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
        AfterDarkChrome.Plate(frame, area, frame.Units(12f), night);
        var inner = area.Inset(frame.Units(8f));
        DrawFace(frame, inner.LeftSlice(frame.Units(40f)).Center, frame.Units(16f), person.AvatarUrl, person.Wash,
            night);
        var copy = inner.Inset(new Edges(frame.Units(48f), 0f, 0f, 0f));
        AfterDarkChrome.Title(frame, copy.TopSlice(frame.Units(20f)), person.Name, night);
        var thread = state.Thread(person.Id);
        var last = thread.Count > 0 ? thread[^1].Body : "Say hello";
        var when = thread.Count > 0 ? thread[^1].When : person.World;
        AfterDarkChrome.Mute(frame, copy.BottomSlice(frame.Units(16f)), when + " · " + last, night);
        if (frame.Input.ConsumeClick(area))
        {
            state.ChatIndex = person.Id;
            state.Open(NightPage.Chat);
        }
    }

    private static void DrawStoryAdd(in AppletFrame frame, Rect area, bool night)
    {
        var tone = AfterDarkChrome.Tone(night);
        frame.Paint.StrokeCircle(area.TopSlice(frame.Units(40f)).Center, frame.Units(16f), tone.Mute, frame.Units(1.4f));
        frame.Text.DrawIn(area.TopSlice(frame.Units(40f)), "+",
            new TextStyle(FontRole.Title, tone.Mute, TextAlign.Center));
        AfterDarkChrome.Mute(frame, area.BottomSlice(frame.Units(16f)), "Add", night);
    }

    private static void DrawBellGlyph(in AppletFrame frame, Vector2 center, float size, Vector4 ink)
    {
        var stroke = MathF.Max(1.3f, size * 0.18f);
        frame.Paint.Stroke(Rect.FromSize(center - new Vector2(size * 0.7f, size * 0.7f),
            new Vector2(size * 1.4f, size * 1.2f)), ink, stroke, size * 0.7f, Corner.Top);
        frame.Paint.Line(center + new Vector2(-size * 0.85f, size * 0.45f),
            center + new Vector2(size * 0.85f, size * 0.45f), ink, stroke);
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
                frame.Paint.Stroke(Rect.FromSize(center - new Vector2(size, size * 0.55f),
                    new Vector2(size * 2f, size * 1.2f)), ink, stroke, size * 0.35f);
                break;
            case 3:
                DrawBellGlyph(frame, center, size, ink);
                break;
            default:
                frame.Paint.StrokeCircle(center + new Vector2(0f, -size * 0.25f), size * 0.4f, ink, stroke);
                frame.Paint.Stroke(Rect.FromSize(center + new Vector2(-size * 0.85f, size * 0.15f),
                    new Vector2(size * 1.7f, size * 0.7f)), ink, stroke, size * 0.7f);
                break;
        }
    }

    private void DrawModeMark(in AppletFrame frame, Rect area)
    {
        var mark = AfterDarkChrome.Wordmark(frame, area, state.Night, state.HoldMark);
        state.TickHold(frame.Input.IsHovering(mark), frame.Input.IsHeld(), frame.DeltaSeconds);
    }

    private void DrawHashChip(in AppletFrame frame, Rect area, string tag, bool night)
    {
        if (AfterDarkChrome.Chip(frame, area, tag, state.Hashtag == tag, night))
        {
            state.Hashtag = state.Hashtag == tag ? string.Empty : tag;
        }
    }

    private void DrawPearlCard(in AppletFrame frame, Rect area, PearlPost post)
    {
        var night = state.Night;
        var tone = AfterDarkChrome.Tone(night);
        AfterDarkChrome.Plate(frame, area, frame.Units(12f), night);
        var inner = area.Inset(frame.Units(10f));
        var wash = tone.AccentDim;
        if (state.TryFindGate(post.AuthorId, out var author))
        {
            wash = author.Wash;
        }

        var avatarHit = inner.LeftSlice(frame.Units(28f)).TopSlice(frame.Units(28f));
        DrawFace(frame, avatarHit.Center, frame.Units(12f), post.AuthorAvatarUrl, wash, night);
        var head = inner.Inset(new Edges(frame.Units(34f), 0f, 0f, 0f)).TopSlice(frame.Units(32f));
        AfterDarkChrome.Title(frame, head.TopSlice(frame.Units(16f)), post.AuthorName, night);
        var handle = post.AuthorHandle.Length > 0
            ? (post.AuthorHandle.StartsWith('@') ? post.AuthorHandle : "@" + post.AuthorHandle)
            : "";
        AfterDarkChrome.Mute(frame, head.BottomSlice(frame.Units(14f)),
            (handle.Length > 0 ? handle + " · " : "") + post.When, night);
        var cursor = frame.Units(36f);
        if (post.QuoteAuthor.Length > 0 || post.QuoteBody.Length > 0)
        {
            var quote = inner.Inset(new Edges(0f, cursor, 0f, 0f)).TopSlice(frame.Units(40f));
            AfterDarkChrome.Glow(frame, quote, frame.Units(8f), false, night);
            AfterDarkChrome.Kicker(frame, quote.TopSlice(frame.Units(14f)).Inset(new Edges(frame.Units(8f), 0f)),
                post.QuoteAuthor, night);
            AfterDarkChrome.Mute(frame, quote.BottomSlice(frame.Units(22f)).Inset(new Edges(frame.Units(8f), 0f)),
                post.QuoteBody, night);
            cursor += frame.Units(44f);
        }

        if (post.Body.Length > 0)
        {
            AfterDarkChrome.Mute(frame, inner.Inset(new Edges(0f, cursor, 0f, frame.Units(28f))).TopSlice(frame.Units(32f)),
                post.Body, night);
            cursor += frame.Units(34f);
        }

        if (post.Media.Length > 0)
        {
            var plate = inner.Inset(new Edges(0f, cursor, 0f, frame.Units(26f))).TopSlice(frame.Units(120f));
            DrawMediaPlate(frame, plate, post, night);
            cursor += frame.Units(124f);
        }

        var bar = inner.BottomSlice(frame.Units(22f));
        var likeHit = bar.LeftSlice(bar.Width * 0.33f);
        var commentHit = bar.Inset(new Edges(bar.Width * 0.33f, 0f, bar.Width * 0.33f, 0f));
        var shareHit = bar.RightSlice(bar.Width * 0.33f);
        frame.Text.DrawIn(likeHit, (post.Liked ? "♥ " : "♡ ") + post.Likes,
            new TextStyle(FontRole.CaptionStrong, post.Liked ? tone.Accent : tone.Mute));
        frame.Text.DrawIn(commentHit, post.Comments + " comments",
            new TextStyle(FontRole.Caption, tone.Mute, TextAlign.Center));
        var shareLabel = night ? "Share" : "Repost";
        frame.Text.DrawIn(shareHit, post.Reposted ? shareLabel + "ed" : shareLabel,
            new TextStyle(FontRole.Caption, post.Reposted ? tone.Accent : tone.Mute, TextAlign.Right));
        if (frame.Input.ConsumeClick(likeHit))
        {
            pearl.LikePost(post.Id, !post.Liked);
            return;
        }

        if (frame.Input.ConsumeClick(commentHit))
        {
            OpenPost(post.Id);
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

    private static void DrawStat(in AppletFrame frame, Rect area, string value, string label, bool night)
    {
        var tone = AfterDarkChrome.Tone(night);
        AfterDarkChrome.Plate(frame, area.Inset(new Edges(frame.Units(4f), 0f)), frame.Units(10f), night);
        frame.Text.DrawIn(area.TopSlice(area.Height * 0.55f), value,
            new TextStyle(FontRole.BodyStrong, tone.Ink, TextAlign.Center));
        frame.Text.DrawIn(area.BottomSlice(area.Height * 0.45f), label,
            new TextStyle(FontRole.Caption, tone.Mute, TextAlign.Center));
    }

    private List<ScenePerson> StoryPeople()
    {
        var list = new List<ScenePerson>();
        foreach (var person in state.Roster)
        {
            if (state.Passes(person) &&
                (state.LiveStories.Contains(person.Id) || person.Line.Length > 0))
            {
                list.Add(person);
            }
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

    private static float PostCardHeight(in AppletFrame frame, PearlPost post)
    {
        var height = 92f;
        if (post.QuoteBody.Length > 0 || post.QuoteAuthor.Length > 0)
        {
            height += 44f;
        }

        if (post.Body.Length > 0)
        {
            height += 34f;
        }

        if (post.Media.Length > 0)
        {
            height += 124f;
        }

        return height;
    }

    private void DrawFace(in AppletFrame frame, Vector2 center, float radius, string url, Vector4 wash, bool night)
    {
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

        AfterDarkChrome.Portrait(frame, center, radius, wash, night);
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
        AfterDarkChrome.Plate(frame, area, frame.Units(8f), night);
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
            AfterDarkChrome.Mute(frame, stack.Take(frame.Units(36f)), "No photos yet.", night);
            return;
        }

        for (var index = 0; index < shots.Count; index += 3)
        {
            var row = stack.Take(frame.Units(86f));
            var cell = (row.Width - frame.Units(8f)) / 3f;
            for (var col = 0; col < 3 && index + col < shots.Count; col++)
            {
                var shot = Rect.FromSize(
                    new Vector2(row.Min.X + (cell + frame.Units(4f)) * col, row.Min.Y), new Vector2(cell, cell));
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
        AfterDarkChrome.Plate(frame, sheet, frame.Units(16f), night);
        var stack = new Stack(sheet.Inset(frame.Units(12f)), StackAxis.Vertical, frame.Units(8f));
        AfterDarkChrome.Title(frame, stack.Take(frame.Units(24f)), night ? "Share" : "Repost", night);
        var post = pearl.PostById(state.SharePostId);
        if (AfterDarkChrome.Chip(frame, stack.Take(frame.Units(36f)), night ? "Share to feed" : "Repost", false, night) &&
            post is not null)
        {
            pearl.Repost(post.Value.Id);
            state.SharePostId = string.Empty;
        }

        if (AfterDarkChrome.Chip(frame, stack.Take(frame.Units(36f)), "Quote", false, night) && post is not null)
        {
            state.QuoteOf = post.Value.Id;
            state.Caption = string.Empty;
            state.DraftMedia.Clear();
            state.SharePostId = string.Empty;
            state.Open(NightPage.Compose);
        }

        if (AfterDarkChrome.Chip(frame, stack.Take(frame.Units(36f)), "Send", false, night))
        {
            state.Open(NightPage.ShareSend);
        }

        if (AfterDarkChrome.Chip(frame, stack.Take(frame.Units(36f)), "Story", false, night) && post is not null)
        {
            state.Caption = post.Value.Body;
            state.StoryMedia = post.Value.Media.Length > 0 ? post.Value.Media[0].Url : string.Empty;
            state.SharePostId = string.Empty;
            state.Open(NightPage.StoryCompose);
        }

        if (AfterDarkChrome.Chip(frame, stack.Take(frame.Units(32f)), "Cancel", false, night) ||
            frame.Input.ConsumeClick(body.TopSlice(body.Height - sheet.Height)))
        {
            state.SharePostId = string.Empty;
        }
    }

    private static string LinkVerb(bool night) => night ? "Connect" : "Follow";

    private static string LinkedVerb(bool night) => night ? "Connected" : "Following";
}
