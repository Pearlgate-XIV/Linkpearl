using Linkpearl.Applets;
using Linkpearl.Geometry;
using Linkpearl.Layout;
using Linkpearl.Media;
using Linkpearl.Net;
using Linkpearl.Painting;

namespace Linkpearl.Applets.Life.AfterDark;

public sealed partial class AfterDarkApplet
{
    private void DrawGate(in AppletFrame frame, Rect area)
    {
        var stack = new Stack(area, StackAxis.Vertical, frame.Units(10f));
        AfterDarkChrome.StackedMark(frame, stack.Take(frame.Units(88f)), night: true);
        AfterDarkChrome.Mute(frame, stack.Take(frame.Units(48f)),
            "A private, adults only corner of the suite. Neon nights, unhurried, yours.", true);
        AfterDarkChrome.Mute(frame, stack.Take(frame.Units(40f)),
            "By entering you confirm you are 18 or older. Be kind, be discreet.", true);

        var rules = stack.Take(frame.Units(36f));
        AfterDarkChrome.Glow(frame, rules, frame.Units(12f), true, true);
        frame.Text.DrawIn(rules, "Read the community rules",
            new TextStyle(FontRole.CaptionStrong, AfterDarkChrome.Night.Accent, TextAlign.Center));
        if (frame.Input.ConsumeClick(rules))
        {
            state.Open(NightPage.Rules);
            return;
        }

        var enter = stack.Take(frame.Units(44f));
        AfterDarkChrome.Primary(frame, enter, "Enter After Dark", true);
        if (frame.Input.ConsumeClick(enter))
        {
            state.Consented = true;
            state.Mode = SocialMode.AfterDark;
            state.Page = state.Onboarded ? NightPage.Tabs : NightPage.OnboardIdentity;
            state.Save(paths);
            return;
        }

        var leave = stack.Take(frame.Units(36f));
        frame.Text.DrawIn(leave, "Not now",
            new TextStyle(FontRole.BodyStrong, AfterDarkChrome.Night.Mute, TextAlign.Center));
        if (frame.Input.ConsumeClick(leave))
        {
            state.StartWash(toNight: false);
        }
    }

    private void DrawRules(in AppletFrame frame, Rect area)
    {
        AfterDarkChrome.Wheel(frame, area, state, frame.Units(720f));
        var shifted = area.Translate(new Vector2(0f, -state.Scroll));
        var stack = new Stack(shifted, StackAxis.Vertical, frame.Units(8f));
        if (AfterDarkChrome.Back(frame, stack.Take(frame.Units(28f)), "After Dark Community Rules", true))
        {
            state.Page = NightPage.Gate;
            state.Scroll = 0f;
            return;
        }

        string[] blocks =
        {
            "Adults only (18+). Content involving minors or child-like characters is banned.",
            "Consent first. No unsolicited explicit messages, pressure, or contacting people who declined.",
            "Respect boundaries. Do not bypass blocks or ask others to reach someone for you.",
            "Illegal content is a permanent ban. Fantasy is not an exemption.",
            "Protect privacy. Doxxing is a permanent ban.",
            "Respect creators. Do not post stolen, leaked, or claimed work.",
            "No spam, scams, or venue ads here — use classifieds for that.",
            "Be respectful. Harassment, hate, threats, and stalking are out.",
            "Respect moderation. Do not evade bans. Appeals are welcome if they stay civil.",
        };
        for (var index = 0; index < blocks.Length; index++)
        {
            var card = stack.Take(frame.Units(64f));
            AfterDarkChrome.Plate(frame, card, frame.Units(10f), true);
            frame.Text.DrawWrapped(card.Inset(frame.Units(8f)), blocks[index],
                new TextStyle(FontRole.Caption, AfterDarkChrome.Night.Ink));
        }
    }

    private void DrawOnboard(in AppletFrame frame, Rect area)
    {
        var night = state.Night;
        AfterDarkChrome.Wheel(frame, area, state, frame.Units(720f));
        var shifted = area.Translate(new Vector2(0f, -state.Scroll));
        var stack = new Stack(shifted, StackAxis.Vertical, frame.Units(8f));
        var title = state.Page switch
        {
            NightPage.OnboardIntent => "Choose your intents",
            NightPage.OnboardAbout => "Say hello",
            NightPage.OnboardReady => "You are all set",
            _ => "Make your entrance",
        };
        if (AfterDarkChrome.Back(frame, stack.Take(frame.Units(28f)), title, night))
        {
            if (state.Onboarded)
            {
                state.Page = NightPage.Tabs;
            }
            else if (state.Page == NightPage.OnboardIdentity)
            {
                state.Page = state.Night && !state.Consented ? NightPage.Gate : NightPage.Tabs;
            }
            else
            {
                state.Page--;
            }

            return;
        }

        if (state.Page == NightPage.OnboardIdentity)
        {
            AfterDarkChrome.Mute(frame, stack.Take(frame.Units(28f)),
                "This is the first thing people see in Discover.", night);
            state.DisplayName = frame.TextField.Draw("ad-name", stack.Take(frame.Units(34f)), state.DisplayName,
                "Display name");
            state.Handle = frame.TextField.Draw("ad-handle", stack.Take(frame.Units(34f)), state.Handle, "Handle");
            state.Pronouns = frame.TextField.Draw("ad-pronouns", stack.Take(frame.Units(34f)), state.Pronouns,
                "Pronouns");
            DrawWrapChips(frame, stack.Take(frame.Units(88f)), SceneBook.Genders, state.Genders, night);
        }
        else if (state.Page == NightPage.OnboardIntent)
        {
            AfterDarkChrome.Mute(frame, stack.Take(frame.Units(28f)),
                "Choose everything that fits. It shapes who finds you.", night);
            var intents = night ? SceneBook.Intents : SceneBook.DayIntents;
            DrawWrapChips(frame, stack.Take(frame.Units(96f)), intents, state.Intents, night);
        }
        else if (state.Page == NightPage.OnboardAbout)
        {
            AfterDarkChrome.Mute(frame, stack.Take(frame.Units(20f)), "A line or two goes a long way.", night);
            state.About = frame.TextField.Draw("ad-about", stack.Take(frame.Units(64f)), state.About,
                "Introduce yourself");
            AfterDarkChrome.Kicker(frame, stack.Take(frame.Units(14f)), "VIBE", night);
            DrawWrapChips(frame, stack.Take(frame.Units(72f)), SceneBook.Tone, state.Tags, night);
            AfterDarkChrome.Kicker(frame, stack.Take(frame.Units(14f)), "ORIENTATION", night);
            DrawPick(frame, stack.Take(frame.Units(56f)), SceneBook.Sexuality, state.Sexuality, value =>
            {
                state.Sexuality = value;
            }, night);
            AfterDarkChrome.Kicker(frame, stack.Take(frame.Units(14f)), "STATUS", night);
            DrawPick(frame, stack.Take(frame.Units(56f)), SceneBook.Relationships, state.Relationship, value =>
            {
                state.Relationship = value;
            }, night);
        }
        else
        {
            AfterDarkChrome.Mute(frame, stack.Take(frame.Units(28f)),
                "A couple of last touches, then step inside.", night);
            var row = stack.Take(frame.Units(36f));
            if (AfterDarkChrome.Chip(frame, row, state.Discoverable ? "Discoverable" : "Hidden", state.Discoverable,
                    night))
            {
                state.Discoverable = !state.Discoverable;
            }
        }

        var go = stack.Take(frame.Units(44f));
        AfterDarkChrome.Primary(frame, go, state.Page == NightPage.OnboardReady ? "Enter" : "Continue", night);
        if (frame.Input.ConsumeClick(go))
        {
            AdvanceOnboard();
        }
    }

    private void AdvanceOnboard()
    {
        if (state.Page == NightPage.OnboardIdentity)
        {
            state.Page = NightPage.OnboardIntent;
            state.Save(paths);
            return;
        }

        if (state.Page == NightPage.OnboardIntent)
        {
            state.Page = NightPage.OnboardAbout;
            state.Save(paths);
            return;
        }

        if (state.Page == NightPage.OnboardAbout)
        {
            state.Page = NightPage.OnboardReady;
            state.Save(paths);
            return;
        }

        state.Onboarded = true;
        state.Page = NightPage.Tabs;
        state.Save(paths);
    }

    private void DrawStack(in AppletFrame frame, Rect area)
    {
        switch (state.Page)
        {
            case NightPage.FeedWall:
                DrawFeed(frame, area);
                break;
            case NightPage.Person:
                DrawPerson(frame, area);
                break;
            case NightPage.Post:
                DrawPost(frame, area);
                break;
            case NightPage.Compose:
                DrawCompose(frame, area);
                break;
            case NightPage.Filters:
                DrawFilters(frame, area);
                break;
            case NightPage.Chat:
                DrawChat(frame, area);
                break;
            case NightPage.Requests:
                DrawRequests(frame, area);
                break;
            case NightPage.Settings:
                DrawSettings(frame, area);
                break;
            case NightPage.Gallery:
                DrawGallery(frame, area);
                break;
            case NightPage.Likes:
                DrawLikes(frame, area);
                break;
            case NightPage.Search:
                DrawSearch(frame, area);
                break;
            case NightPage.Story:
                DrawStory(frame, area);
                break;
            case NightPage.StoryCompose:
                DrawStoryCompose(frame, area);
                break;
            case NightPage.Following:
                DrawPeopleList(frame, area, "Following", connected: true);
                break;
            case NightPage.Followers:
                DrawPeopleList(frame, area, "Followers", connected: false);
                break;
            case NightPage.PhotoPick:
                DrawPhotoPick(frame, area);
                break;
            case NightPage.PhotoView:
                DrawPhotoView(frame, area);
                break;
            case NightPage.ShareSend:
                DrawShareSend(frame, area);
                break;
            default:
                DrawBlocked(frame, area);
                break;
        }
    }

    private void DrawSearch(in AppletFrame frame, Rect area)
    {
        var night = state.Night;
        var stack = new Stack(area, StackAxis.Vertical, frame.Units(8f));
        if (AfterDarkChrome.Back(frame, stack.Take(frame.Units(28f)), "Search", night))
        {
            state.Back();
            return;
        }

        state.Search = frame.TextField.Draw("ad-search", stack.Take(frame.Units(36f)), state.Search,
            "People, posts, handles");
        if (state.Search.Length >= 2)
        {
            pearl.NoteQuery(state.Search);
        }
        AfterDarkChrome.Kicker(frame, stack.Take(frame.Units(14f)), "PEOPLE", night);
        var people = 0;
        for (var index = 0; index < state.Roster.Count && people < 4; index++)
        {
            var person = state.Roster[index];
            if (!state.Passes(person))
            {
                continue;
            }

            DrawMayKnow(frame, stack.Take(frame.Units(56f)), person);
            people++;
        }

        AfterDarkChrome.Kicker(frame, stack.Take(frame.Units(14f)), "POSTS", night);
        var posts = 0;
        foreach (var wallPost in VisibleFeed(pearl.Current.SearchPosts.Length > 0
                     ? pearl.Current.SearchPosts
                     : pearl.Current.Feed))
        {
            DrawPearlCard(frame, stack.Take(frame.Units(PostCardHeight(frame, wallPost))), wallPost);
            posts++;
        }

        if (people == 0 && posts == 0)
        {
            AfterDarkChrome.Mute(frame, stack.Take(frame.Units(28f)), "No matches yet.", night);
        }
    }

    private void DrawStory(in AppletFrame frame, Rect area)
    {
        var night = state.Night;
        ScenePerson person;
        if (state.StoryIndex < 0)
        {
            person = new ScenePerson(-1, string.Empty, state.DisplayName, state.Handle, "You", state.OwnStory, true, 0,
                false, AfterDarkChrome.Tone(night).Accent, Array.Empty<string>(), Array.Empty<string>(),
                pearl.Current.MeAvatarUrl);
        }
        else if (!state.TryFind(state.StoryIndex, out person))
        {
            state.Back();
            return;
        }
        frame.Paint.Fill(area, person.Wash with { W = 1f }, frame.Units(16f));
        var stack = new Stack(area.Inset(frame.Units(14f)), StackAxis.Vertical, frame.Units(8f));
        if (AfterDarkChrome.Back(frame, stack.Take(frame.Units(28f)), person.Name, night: true))
        {
            state.Back();
            return;
        }

        AfterDarkChrome.Mute(frame, stack.Take(frame.Units(18f)), person.Handle, true);
        frame.Text.DrawWrapped(stack.Take(frame.Units(120f)),
            person.Line.Length > 0 ? person.Line : "No story yet.",
            new TextStyle(FontRole.BodyStrong, AfterDarkChrome.Night.Ink));
        if (frame.Input.ConsumeClick(area.BottomSlice(frame.Units(80f))))
        {
            state.PersonIndex = person.Id < 0 ? state.PersonIndex : person.Id;
            if (person.Id >= 0)
            {
                state.Open(NightPage.Person);
            }
            else
            {
                state.Back();
            }
        }
    }

    private void DrawStoryCompose(in AppletFrame frame, Rect area)
    {
        var night = state.Night;
        var stack = new Stack(area, StackAxis.Vertical, frame.Units(8f));
        if (AfterDarkChrome.Back(frame, stack.Take(frame.Units(28f)), "Your story", night))
        {
            state.Back();
            return;
        }

        state.Caption = frame.TextField.Draw("ad-story", stack.Take(frame.Units(88f)), state.Caption,
            "What are you up to?");
        if (AfterDarkChrome.Chip(frame, stack.Take(frame.Units(32f)),
                state.StoryMedia.Length > 0 ? "Photo attached" : "Add photo", state.StoryMedia.Length > 0, night))
        {
            state.PickingAvatar = false;
            state.Open(NightPage.PhotoPick);
        }

        var post = stack.Take(frame.Units(44f));
        AfterDarkChrome.Primary(frame, post, state.Caption.Length > 0 || state.StoryMedia.Length > 0
            ? "Share to stories"
            : "Remove story", night);
        if (frame.Input.ConsumeClick(post))
        {
            state.OwnStory = state.Caption;
            if (state.OwnStory.Length > 0 || state.StoryMedia.Length > 0)
            {
                pearl.PublishStory(state.OwnStory, state.StoryMedia);
            }

            state.Caption = string.Empty;
            state.ViewedStories.Remove(-1);
            state.StoryIndex = -1;
            state.Page = NightPage.Tabs;
            if (state.OwnStory.Length > 0)
            {
                state.Open(NightPage.Story);
            }

            state.Save(paths);
        }
    }

    private void DrawPerson(in AppletFrame frame, Rect area)
    {
        var night = state.Night;
        if (!state.TryFind(state.PersonIndex, out var person))
        {
            state.Back();
            return;
        }

        var stack = new Stack(area, StackAxis.Vertical, frame.Units(8f));
        if (AfterDarkChrome.Back(frame, stack.Take(frame.Units(28f)), person.Name, night))
        {
            state.Back();
            return;
        }

        var hero = stack.Take(frame.Units(96f));
        frame.Paint.Fill(hero, person.Wash with { W = 0.85f }, frame.Units(16f));
        DrawFace(frame, new Vector2(hero.Min.X + frame.Units(36f), hero.Max.Y - frame.Units(6f)), frame.Units(26f),
            person.AvatarUrl, person.Wash, night);

        AfterDarkChrome.Mute(frame, stack.Take(frame.Units(16f)),
            person.Handle + " · " + person.World + (person.Online ? " · Online" : " · Away"), night);
        frame.Text.DrawWrapped(stack.Take(frame.Units(40f)), person.Line,
            new TextStyle(FontRole.Body, AfterDarkChrome.Tone(night).Ink));

        var chips = stack.Take(frame.Units(28f));
        var shownIntents = person.Intents.Where(tag => night || tag != "ERP").ToArray();
        if (shownIntents.Length > 0)
        {
            var w = chips.Width / shownIntents.Length;
            for (var index = 0; index < shownIntents.Length; index++)
            {
                var cell = Rect.FromSize(new Vector2(chips.Min.X + w * index + frame.Units(2f), chips.Min.Y),
                    new Vector2(w - frame.Units(4f), chips.Height));
                AfterDarkChrome.Chip(frame, cell, shownIntents[index], true, night);
            }
        }

        var linked = state.Connected.Contains(person.Id);
        var row = stack.Take(frame.Units(40f));
        var left = row.LeftSlice(row.Width * 0.48f);
        var right = row.RightSlice(row.Width * 0.48f);
        AfterDarkChrome.Primary(frame, left, linked ? "Message" : LinkVerb(night), night);
        if (frame.Input.ConsumeClick(left))
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
        }

        AfterDarkChrome.Glow(frame, right, frame.Units(12f), false, night);
        frame.Text.DrawIn(right, linked ? LinkedVerb(night) : "Block",
            new TextStyle(FontRole.CaptionStrong,
                linked ? AfterDarkChrome.Tone(night).Mute : AfterDarkChrome.Tone(night).Danger, TextAlign.Center));
        if (frame.Input.ConsumeClick(right))
        {
            if (linked)
            {
                state.FollowPerson(person, pearl, false);
            }
            else
            {
                state.Blocked.Add(person.Id);
                state.Connected.Remove(person.Id);
                state.Back();
                state.Save(paths);
            }
        }

        AfterDarkChrome.Kicker(frame, stack.Take(frame.Units(14f)), "POSTS", night);
        if (person.GateId.Length > 0)
        {
            pearl.WatchProfile(person.GateId);
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

        var theirs = string.Equals(pearl.Current.WatchedUserId, person.GateId, StringComparison.Ordinal)
            ? pearl.Current.ProfilePosts
            : [];
        if (state.ProfilePane == 1)
        {
            DrawPhotoGrid(frame, stack, theirs, night);
            return;
        }

        foreach (var wallPost in theirs)
        {
            DrawPearlCard(frame, stack.Take(frame.Units(PostCardHeight(frame, wallPost))), wallPost);
        }

        if (theirs.Length == 0)
        {
            AfterDarkChrome.Mute(frame, stack.Take(frame.Units(28f)), "No posts yet.", night);
        }
    }

    private void DrawPost(in AppletFrame frame, Rect area)
    {
        var night = state.Night;
        pearl.WatchPost(state.PostKey);
        var post = pearl.PostById(state.PostKey);
        if (post is null)
        {
            AfterDarkChrome.Mute(frame, area.TopSlice(frame.Units(40f)), "That post is not on Pearlgate yet.", night);
            if (AfterDarkChrome.Back(frame, area.TopSlice(frame.Units(28f)), "Post", night))
            {
                state.Back();
            }

            return;
        }

        var ready = post.Value;
        var stack = new Stack(area, StackAxis.Vertical, frame.Units(8f));
        if (AfterDarkChrome.Back(frame, stack.Take(frame.Units(28f)), "Post", night))
        {
            state.Back();
            return;
        }

        DrawPearlCard(frame, stack.Take(frame.Units(PostCardHeight(frame, ready))), ready);
        AfterDarkChrome.Kicker(frame, stack.Take(frame.Units(14f)), "COMMENTS", night);
        var lines = pearl.CommentsFor(ready.Id);
        if (lines.Count > 0)
        {
            foreach (var comment in lines)
            {
                var row = stack.Take(frame.Units(44f));
                AfterDarkChrome.Plate(frame, row, frame.Units(10f), night);
                AfterDarkChrome.Title(frame, row.TopSlice(frame.Units(18f)).Inset(new Edges(frame.Units(8f), 0f)),
                    comment.Author, night);
                AfterDarkChrome.Mute(frame, row.BottomSlice(frame.Units(18f)).Inset(new Edges(frame.Units(8f), 0f)),
                    comment.Body, night);
            }
        }
        else
        {
            AfterDarkChrome.Mute(frame, stack.Take(frame.Units(24f)), "No comments yet. Be the first.", night);
        }

        state.CommentDraft = frame.TextField.Draw("ad-comment", stack.Take(frame.Units(36f)), state.CommentDraft,
            "Write a comment…");
        var send = stack.Take(frame.Units(40f));
        AfterDarkChrome.Primary(frame, send, "Comment", night);
        if (frame.Input.ConsumeClick(send) && state.CommentDraft.Length > 0)
        {
            pearl.CommentOn(ready.Id, state.CommentDraft);
            state.CommentDraft = string.Empty;
        }
    }

    private void DrawCompose(in AppletFrame frame, Rect area)
    {
        var night = state.Night;
        var stack = new Stack(area, StackAxis.Vertical, frame.Units(8f));
        if (AfterDarkChrome.Back(frame, stack.Take(frame.Units(28f)), "New Post", night))
        {
            state.Back();
            return;
        }

        if (state.QuoteOf.Length > 0)
        {
            var quoted = pearl.PostById(state.QuoteOf);
            AfterDarkChrome.Kicker(frame, stack.Take(frame.Units(14f)), "QUOTING", night);
            AfterDarkChrome.Mute(frame, stack.Take(frame.Units(28f)),
                quoted is { } q ? q.AuthorName + " · " + q.Body : state.QuoteOf, night);
        }

        state.Caption = frame.TextField.Draw("ad-caption", stack.Take(frame.Units(88f)), state.Caption,
            night ? "What's the vibe tonight?" : "What's on your mind?");
        var photos = stack.Take(frame.Units(32f));
        if (AfterDarkChrome.Chip(frame, photos, state.DraftMedia.Count == 0 ? "Photo" : "Photos " + state.DraftMedia.Count,
                state.DraftMedia.Count > 0, night))
        {
            state.PickingAvatar = false;
            state.Open(NightPage.PhotoPick);
        }

        if (state.DraftMedia.Count > 0)
        {
            var row = stack.Take(frame.Units(72f));
            var cell = row.Width / Math.Min(4, state.DraftMedia.Count);
            for (var index = 0; index < state.DraftMedia.Count && index < 4; index++)
            {
                var shot = Rect.FromSize(new Vector2(row.Min.X + cell * index + frame.Units(2f), row.Min.Y),
                    new Vector2(cell - frame.Units(4f), row.Height));
                DrawStill(frame, shot, state.DraftMedia[index], night);
            }
        }

        var aud = stack.Take(frame.Units(32f));
        if (AfterDarkChrome.Chip(frame, aud.LeftSlice(aud.Width * 0.48f), "Everyone", state.AudienceEveryone, night))
        {
            state.AudienceEveryone = true;
        }

        if (AfterDarkChrome.Chip(frame, aud.RightSlice(aud.Width * 0.48f),
                night ? "Connections" : "Following", !state.AudienceEveryone, night))
        {
            state.AudienceEveryone = false;
        }

        var post = stack.Take(frame.Units(44f));
        AfterDarkChrome.Primary(frame, post, "Post", night);
        if (frame.Input.ConsumeClick(post) && (state.Caption.Length > 0 || state.DraftMedia.Count > 0 ||
                                              state.QuoteOf.Length > 0))
        {
            pearl.PublishPost(state.Caption, state.AudienceEveryone, state.DraftMedia.ToArray(), state.QuoteOf);
            state.Caption = string.Empty;
            state.QuoteOf = string.Empty;
            state.DraftMedia.Clear();
            state.Page = NightPage.Tabs;
            state.Tab = NightTab.Home;
            pearl.WatchFeed(state.FeedEveryone ? "foryou" : "following");
        }
    }

    private void DrawFilters(in AppletFrame frame, Rect area)
    {
        var night = state.Night;
        AfterDarkChrome.Wheel(frame, area, state, frame.Units(520f));
        var shifted = area.Translate(new Vector2(0f, -state.Scroll));
        var stack = new Stack(shifted, StackAxis.Vertical, frame.Units(8f));
        if (AfterDarkChrome.Back(frame, stack.Take(frame.Units(28f)), "Filters", night))
        {
            state.Back();
            return;
        }

        AfterDarkChrome.Mute(frame, stack.Take(frame.Units(32f)),
            "Chips cycle through neutral, include, and exclude.", night);
        foreach (var tag in SceneBook.FilterTags(night))
        {
            var pole = state.Pole(tag);
            var row = stack.Take(frame.Units(32f));
            var on = pole != FilterPole.Neutral;
            AfterDarkChrome.Glow(frame, row, frame.Units(10f), on, night);
            var mark = pole == FilterPole.Include ? "+" : pole == FilterPole.Exclude ? "−" : "·";
            frame.Text.DrawIn(row.Inset(new Edges(frame.Units(12f), 0f)), mark + "  " + tag,
                new TextStyle(FontRole.CaptionStrong, AfterDarkChrome.Tone(night).Ink));
            if (frame.Input.ConsumeClick(row))
            {
                state.CycleFilter(tag);
            }
        }

        var clear = stack.Take(frame.Units(36f));
        AfterDarkChrome.Primary(frame, clear, "Clear all", night);
        if (frame.Input.ConsumeClick(clear))
        {
            state.Filters.Clear();
            state.Vibe = 0;
            state.Hashtag = string.Empty;
            state.Save(paths);
        }
    }

    private void DrawChat(in AppletFrame frame, Rect area)
    {
        var night = state.Night;
        if (state.ChatKey.Length > 0)
        {
            DrawPearlChat(frame, area, night);
            return;
        }

        if (!state.TryFind(state.ChatIndex, out var person))
        {
            state.Page = NightPage.Tabs;
            state.Tab = NightTab.Messages;
            return;
        }

        var stack = new Stack(area, StackAxis.Vertical, frame.Units(8f));
        if (AfterDarkChrome.Back(frame, stack.Take(frame.Units(28f)), person.Name, night))
        {
            state.Back();
            return;
        }

        var thread = state.Thread(person.Id);
        if (thread.Count == 0)
        {
            AfterDarkChrome.Mute(frame, stack.Take(frame.Units(28f)), "No messages yet. Say hello.", night);
        }

        foreach (var line in thread)
        {
            var row = stack.Take(frame.Units(48f));
            var bubble = line.Mine
                ? row.RightSlice(row.Width * 0.82f)
                : row.LeftSlice(row.Width * 0.82f);
            AfterDarkChrome.Bubble(frame, bubble, line.Body, line.Mine, night);
        }

        state.Draft = frame.TextField.Draw("ad-dm", stack.Take(frame.Units(40f)), state.Draft, "Write a message…");
        var send = stack.Take(frame.Units(40f));
        AfterDarkChrome.Primary(frame, send, "Send", night);
        if (frame.Input.ConsumeClick(send) && state.Draft.Length > 0)
        {
            thread.Add(new ChatLine(true, state.Draft, "now"));
            state.Draft = string.Empty;
            state.FollowPerson(person, pearl, true);
            state.Save(paths);
        }
    }

    private void DrawPearlChat(in AppletFrame frame, Rect area, bool night)
    {
        pearl.WatchChat(state.ChatKey);
        var title = "Chat";
        foreach (var chat in pearl.Current.Chats)
        {
            if (string.Equals(chat.Id, state.ChatKey, StringComparison.Ordinal))
            {
                title = chat.Title;
                break;
            }
        }

        var stack = new Stack(area, StackAxis.Vertical, frame.Units(8f));
        if (AfterDarkChrome.Back(frame, stack.Take(frame.Units(28f)), title, night))
        {
            state.ChatKey = string.Empty;
            state.Back();
            return;
        }

        var lines = pearl.LinesFor(state.ChatKey);
        if (lines.Count == 0)
        {
            AfterDarkChrome.Mute(frame, stack.Take(frame.Units(28f)), "No messages yet.", night);
        }

        foreach (var line in lines)
        {
            var row = stack.Take(frame.Units(48f));
            var bubble = line.Mine
                ? row.RightSlice(row.Width * 0.82f)
                : row.LeftSlice(row.Width * 0.82f);
            AfterDarkChrome.Bubble(frame, bubble, line.Body, line.Mine, night);
        }

        state.Draft = frame.TextField.Draw("ad-pearl-dm", stack.Take(frame.Units(40f)), state.Draft, "Write a message…");
        var send = stack.Take(frame.Units(40f));
        AfterDarkChrome.Primary(frame, send, "Send", night);
        if (frame.Input.ConsumeClick(send) && state.Draft.Length > 0)
        {
            pearl.SendChat(state.ChatKey, state.Draft);
            state.Draft = string.Empty;
        }
    }

    private void DrawRequests(in AppletFrame frame, Rect area)
    {
        var night = state.Night;
        var stack = new Stack(area, StackAxis.Vertical, frame.Units(8f));
        if (AfterDarkChrome.Back(frame, stack.Take(frame.Units(28f)), "Requests", night))
        {
            state.Back();
            return;
        }

        if (state.Incoming.Count == 0)
        {
            AfterDarkChrome.Mute(frame, stack.Take(frame.Units(36f)),
                night ? "No requests. Intros you receive land here." : "No follow requests right now.", night);
            return;
        }

        foreach (var id in state.Incoming.ToArray())
        {
            if (!state.TryFind(id, out var person))
            {
                continue;
            }

            if (!night && person.NightOnly)
            {
                continue;
            }

            var row = stack.Take(frame.Units(72f));
            AfterDarkChrome.Plate(frame, row, frame.Units(12f), night);
            var inner = row.Inset(frame.Units(8f));
            AfterDarkChrome.Title(frame, inner.TopSlice(frame.Units(20f)),
                person.Name + (night ? " wants to connect" : " followed you"), night);
            var btns = inner.BottomSlice(frame.Units(28f));
            if (AfterDarkChrome.Chip(frame, btns.LeftSlice(btns.Width * 0.48f), "Accept", true, night))
            {
                state.Connected.Add(id);
                state.Incoming.Remove(id);
                state.FollowersSeed++;
                state.Save(paths);
            }

            if (AfterDarkChrome.Chip(frame, btns.RightSlice(btns.Width * 0.48f), "Decline", false, night))
            {
                state.Incoming.Remove(id);
                state.Save(paths);
            }
        }
    }

    private void DrawSettings(in AppletFrame frame, Rect area)
    {
        var night = state.Night;
        var tone = AfterDarkChrome.Tone(night);
        var stack = new Stack(area, StackAxis.Vertical, frame.Units(10f));
        if (AfterDarkChrome.Back(frame, stack.Take(frame.Units(28f)), "Settings", night))
        {
            state.Back();
            return;
        }

        AfterDarkChrome.Kicker(frame, stack.Take(frame.Units(14f)), "MODE", night);
        AfterDarkChrome.Mute(frame, stack.Take(frame.Units(40f)),
            "Daylight is the default, SFW suite. After Dark is an 18+ opt-in. Hold the sun or moon on Home to flip.",
            night);

        var switcher = stack.Take(frame.Units(44f));
        AfterDarkChrome.Plate(frame, switcher, frame.Units(14f), night);
        var dayHit = switcher.LeftSlice(switcher.Width * 0.5f).Inset(frame.Units(4f));
        var nightHit = switcher.RightSlice(switcher.Width * 0.5f).Inset(frame.Units(4f));
        AfterDarkChrome.Glow(frame, dayHit, frame.Units(10f), !night, night);
        AfterDarkChrome.Glow(frame, nightHit, frame.Units(10f), night, night);
        frame.Text.DrawIn(dayHit, "Daylight",
            new TextStyle(FontRole.CaptionStrong, !night ? tone.AccentInk : tone.Mute, TextAlign.Center));
        frame.Text.DrawIn(nightHit, "After Dark",
            new TextStyle(FontRole.CaptionStrong, night ? tone.AccentInk : tone.Mute, TextAlign.Center));
        if (frame.Input.ConsumeClick(dayHit) && night)
        {
            state.StartWash(toNight: false);
        }

        if (frame.Input.ConsumeClick(nightHit) && !night)
        {
            state.StartWash(toNight: true);
        }

        DrawToggle(frame, stack.Take(frame.Units(44f)), "Discoverable", state.Discoverable, night, () =>
        {
            state.Discoverable = !state.Discoverable;
            state.Save(paths);
        });

        if (AfterDarkChrome.Chip(frame, stack.Take(frame.Units(36f)), "Blocked", false, night))
        {
            state.Open(NightPage.Blocked);
        }

        if (AfterDarkChrome.Chip(frame, stack.Take(frame.Units(36f)), "Likes", false, night))
        {
            state.Open(NightPage.Likes);
        }

        if (AfterDarkChrome.Chip(frame, stack.Take(frame.Units(36f)), "Gallery", false, night))
        {
            state.Open(NightPage.Gallery);
        }

        if (AfterDarkChrome.Chip(frame, stack.Take(frame.Units(36f)), "Following", false, night))
        {
            state.Open(NightPage.Following);
        }

        if (AfterDarkChrome.Chip(frame, stack.Take(frame.Units(36f)), "Edit profile", false, night))
        {
            state.Open(NightPage.OnboardIdentity);
        }
    }

    private void DrawGallery(in AppletFrame frame, Rect area)
    {
        var night = state.Night;
        var stack = new Stack(area, StackAxis.Vertical, frame.Units(8f));
        if (AfterDarkChrome.Back(frame, stack.Take(frame.Units(28f)), "Gallery", night))
        {
            state.Back();
            return;
        }

        DrawPhotoGrid(frame, stack, pearl.Current.Feed, night);
    }

    private void DrawLikes(in AppletFrame frame, Rect area)
    {
        var night = state.Night;
        var stack = new Stack(area, StackAxis.Vertical, frame.Units(8f));
        if (AfterDarkChrome.Back(frame, stack.Take(frame.Units(28f)), "Likes", night))
        {
            state.Back();
            return;
        }

        var shown = 0;
        foreach (var wallPost in pearl.Current.Feed)
        {
            if (!wallPost.Liked)
            {
                continue;
            }

            DrawPearlCard(frame, stack.Take(frame.Units(PostCardHeight(frame, wallPost))), wallPost);
            shown++;
        }

        if (shown == 0)
        {
            AfterDarkChrome.Mute(frame, stack.Take(frame.Units(28f)), "No likes on this feed yet.", night);
        }
    }

    private void DrawPhotoPick(in AppletFrame frame, Rect area)
    {
        var night = state.Night;
        var stack = new Stack(area, StackAxis.Vertical, frame.Units(8f));
        if (AfterDarkChrome.Back(frame, stack.Take(frame.Units(28f)), "Choose a photo", night))
        {
            state.Back();
            return;
        }

        var shots = GalleryFiles.List(paths);
        if (shots.Count == 0)
        {
            AfterDarkChrome.Mute(frame, stack.Take(frame.Units(40f)),
                "Take a still in Camera first. Those photos land here.", night);
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
                DrawStill(frame, shot, shots[index + col].Path, night);
                if (!frame.Input.ConsumeClick(shot))
                {
                    continue;
                }

                var path = shots[index + col].Path;
                if (state.PickingAvatar)
                {
                    pearl.SetAvatar(path);
                    state.PickingAvatar = false;
                }
                else if (state.ReturnTo == NightPage.StoryCompose)
                {
                    state.StoryMedia = path;
                }
                else if (state.DraftMedia.Count < 4 && !state.DraftMedia.Contains(path))
                {
                    state.DraftMedia.Add(path);
                }

                state.Back();
            }
        }
    }

    private void DrawPhotoView(in AppletFrame frame, Rect area)
    {
        var night = state.Night;
        var stack = new Stack(area, StackAxis.Vertical, frame.Units(8f));
        if (AfterDarkChrome.Back(frame, stack.Take(frame.Units(28f)), "Photo", night))
        {
            state.Back();
            return;
        }

        DrawStill(frame, stack.Take(frame.Units(280f)), state.ViewMedia, night);
        var row = stack.Take(frame.Units(36f));
        if (AfterDarkChrome.Chip(frame, row.LeftSlice(row.Width * 0.48f), "Open post", false, night) &&
            state.PostKey.Length > 0)
        {
            OpenPost(state.PostKey);
        }

        if (AfterDarkChrome.Chip(frame, row.RightSlice(row.Width * 0.48f), night ? "Share" : "Repost", false, night))
        {
            state.SharePostId = state.PostKey;
        }
    }

    private void DrawShareSend(in AppletFrame frame, Rect area)
    {
        var night = state.Night;
        var stack = new Stack(area, StackAxis.Vertical, frame.Units(8f));
        if (AfterDarkChrome.Back(frame, stack.Take(frame.Units(28f)), "Send", night))
        {
            state.SharePostId = string.Empty;
            state.Back();
            return;
        }

        if (!pearl.Current.SignedIn)
        {
            AfterDarkChrome.Mute(frame, stack.Take(frame.Units(36f)), "Sign in from You to send.", night);
            return;
        }

        var shown = 0;
        foreach (var chat in pearl.Current.Chats)
        {
            var row = stack.Take(frame.Units(48f));
            AfterDarkChrome.Plate(frame, row, frame.Units(10f), night);
            AfterDarkChrome.Title(frame, row.Inset(frame.Units(10f)), chat.Title, night);
            if (frame.Input.ConsumeClick(row))
            {
                var cited = pearl.PostById(state.SharePostId);
                var preview = cited is { } p
                    ? "post:" + p.Id + " " + (p.Body.Length > 0 ? p.Body : p.AuthorName)
                    : "post:" + state.SharePostId;
                pearl.SendChat(chat.Id, preview);
                state.SharePostId = string.Empty;
                state.ChatKey = chat.Id;
                pearl.WatchChat(chat.Id);
                state.Open(NightPage.Chat);
            }

            shown++;
        }

        if (shown == 0)
        {
            AfterDarkChrome.Mute(frame, stack.Take(frame.Units(28f)), "No chats yet.", night);
        }
    }

    private void DrawBlocked(in AppletFrame frame, Rect area)
    {
        var night = state.Night;
        var stack = new Stack(area, StackAxis.Vertical, frame.Units(8f));
        if (AfterDarkChrome.Back(frame, stack.Take(frame.Units(28f)), "Blocked", night))
        {
            state.Back();
            return;
        }

        if (state.Blocked.Count == 0)
        {
            AfterDarkChrome.Mute(frame, stack.Take(frame.Units(28f)), "No one blocked.", night);
            return;
        }

        foreach (var id in state.Blocked.ToArray())
        {
            if (!state.TryFind(id, out var person))
            {
                continue;
            }

            var row = stack.Take(frame.Units(44f));
            AfterDarkChrome.Plate(frame, row, frame.Units(10f), night);
            AfterDarkChrome.Title(frame, row.Inset(new Edges(frame.Units(10f), 0f, frame.Units(80f), 0f)), person.Name,
                night);
            if (AfterDarkChrome.Chip(frame, row.RightSlice(frame.Units(76f)).Inset(frame.Units(6f)), "Unblock", false,
                    night))
            {
                state.Blocked.Remove(id);
                state.Save(paths);
            }
        }
    }

    private void DrawPeopleList(in AppletFrame frame, Rect area, string title, bool connected)
    {
        var night = state.Night;
        var stack = new Stack(area, StackAxis.Vertical, frame.Units(8f));
        if (AfterDarkChrome.Back(frame, stack.Take(frame.Units(28f)), title, night))
        {
            state.Back();
            return;
        }

        var shown = 0;
        foreach (var person in state.Roster)
        {
            if (connected)
            {
                if (!state.Connected.Contains(person.Id))
                {
                    continue;
                }
            }
            else if (!state.Incoming.Contains(person.Id))
            {
                continue;
            }

            DrawMayKnow(frame, stack.Take(frame.Units(56f)), person);
            shown++;
        }

        if (shown == 0)
        {
            AfterDarkChrome.Mute(frame, stack.Take(frame.Units(36f)),
                connected
                    ? "Follow people in Discover and they land here."
                    : "Followers and requests you accept land here.", night);
        }
    }

    private static void DrawToggle(in AppletFrame frame, Rect area, string label, bool on, bool night, Action flip)
    {
        AfterDarkChrome.Plate(frame, area, frame.Units(12f), night);
        AfterDarkChrome.Title(frame, area.Inset(new Edges(frame.Units(12f), 0f, frame.Units(64f), 0f)), label, night);
        var knob = area.RightSlice(frame.Units(52f)).Inset(new Edges(0f, frame.Units(10f), frame.Units(10f),
            frame.Units(10f)));
        AfterDarkChrome.Glow(frame, knob, frame.Units(10f), on, night);
        frame.Text.DrawIn(knob, on ? "On" : "Off",
            new TextStyle(FontRole.CaptionStrong, AfterDarkChrome.Tone(night).Ink, TextAlign.Center));
        if (frame.Input.ConsumeClick(area))
        {
            flip();
        }
    }

    private static void DrawPick(in AppletFrame frame, Rect area, string[] options, string current, Action<string> pick,
        bool night)
    {
        var cols = Math.Min(4, Math.Max(1, options.Length));
        var w = area.Width / cols;
        var h = area.Height / MathF.Max(1, (int)MathF.Ceiling(options.Length / (float)cols));
        for (var index = 0; index < options.Length; index++)
        {
            var cell = Rect.FromSize(
                new Vector2(area.Min.X + w * (index % cols) + frame.Units(2f),
                    area.Min.Y + h * (index / cols) + frame.Units(2f)),
                new Vector2(w - frame.Units(4f), h - frame.Units(4f)));
            var tag = options[index];
            if (AfterDarkChrome.Chip(frame, cell, tag, string.Equals(current, tag, StringComparison.Ordinal), night))
            {
                pick(tag);
            }
        }
    }

    private static void DrawWrapChips(in AppletFrame frame, Rect area, string[] options, List<string> selected,
        bool night)
    {
        var cols = 4;
        var rows = (int)MathF.Ceiling(options.Length / (float)cols);
        var w = area.Width / cols;
        var h = area.Height / MathF.Max(rows, 1);
        for (var index = 0; index < options.Length; index++)
        {
            var cell = Rect.FromSize(
                new Vector2(area.Min.X + w * (index % cols) + frame.Units(2f),
                    area.Min.Y + h * (index / cols) + frame.Units(2f)),
                new Vector2(w - frame.Units(4f), h - frame.Units(4f)));
            var tag = options[index];
            var on = selected.Contains(tag);
            if (AfterDarkChrome.Chip(frame, cell, tag, on, night))
            {
                if (!selected.Remove(tag))
                {
                    selected.Add(tag);
                }
            }
        }
    }
}
