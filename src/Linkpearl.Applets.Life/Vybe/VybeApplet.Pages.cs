using System.Globalization;
using Linkpearl.Applets;
using Linkpearl.Applets.Life.Camera;
using Linkpearl.Chat;
using Linkpearl.Geometry;
using Linkpearl.Layout;
using Linkpearl.Media;
using Linkpearl.Net;
using Linkpearl.Painting;
using Linkpearl.Preferences;

namespace Linkpearl.Applets.Life.Vybe;

public sealed partial class VybeApplet
{
    private void DrawGate(in AppletFrame frame, Rect area)
    {
        var stack = new Stack(area, StackAxis.Vertical, frame.Units(10f));
        VybeChrome.StackedMark(frame, stack.Take(frame.Units(88f)), night: true);
        VybeChrome.Mute(frame, stack.Take(frame.Units(48f)),
            "A private, adults only corner of the suite. Neon nights, unhurried, yours.", true);
        VybeChrome.Mute(frame, stack.Take(frame.Units(40f)),
            "By entering you confirm you are 18 or older. Be kind, be discreet.", true);

        var rules = stack.Take(frame.Units(36f));
        VybeChrome.Glow(frame, rules, frame.Units(12f), true, true);
        frame.Text.DrawIn(rules, "Read the community rules",
            new TextStyle(FontRole.CaptionStrong, VybeChrome.Night.Accent, TextAlign.Center));
        if (frame.Input.ConsumeClick(rules))
        {
            state.Open(NightPage.Rules);
            return;
        }

        var enter = stack.Take(frame.Units(44f));
        VybeChrome.Primary(frame, enter, "Enter After Dark", true);
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
            new TextStyle(FontRole.BodyStrong, VybeChrome.Night.Mute, TextAlign.Center));
        if (frame.Input.ConsumeClick(leave))
        {
            state.StartWash(toNight: false);
        }
    }

    private void DrawRules(in AppletFrame frame, Rect area)
    {
        VybeChrome.Wheel(frame, area, state, frame.Units(720f));
        var shifted = area.Translate(new Vector2(0f, -state.Scroll));
        var stack = new Stack(shifted, StackAxis.Vertical, frame.Units(8f));
        if (VybeChrome.Back(frame, stack.Take(frame.Units(28f)), "After Dark Community Rules", true))
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
            VybeChrome.Plate(frame, card, frame.Units(10f), true);
            frame.Text.DrawWrapped(card.Inset(frame.Units(8f)), blocks[index],
                new TextStyle(FontRole.Caption, VybeChrome.Night.Ink));
        }
    }

    private void DrawOnboard(in AppletFrame frame, Rect area)
    {
        var night = state.Night;
        VybeChrome.Wheel(frame, area, state, frame.Units(1760f));
        var shifted = area.Translate(new Vector2(0f, -state.Scroll));
        var stack = new Stack(shifted, StackAxis.Vertical, frame.Units(8f));
        var title = state.Page switch
        {
            NightPage.OnboardIntent => "Choose your interests",
            NightPage.OnboardAbout => "Say hello",
            NightPage.OnboardReady => "You are all set",
            _ => state.Onboarded ? "Edit profile" : "Make your entrance",
        };
        if (VybeChrome.Back(frame, stack.Take(frame.Units(28f)), title, night))
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
            VybeChrome.Mute(frame, stack.Take(frame.Units(28f)),
                "This is the first thing people see in Discover.", night);
            var shown = EditableName();
            var next = frame.TextField.Draw("ad-name-" + profileStamp, stack.Take(frame.Units(34f)), shown,
                "Display name");
            if (next != shown)
            {
                state.DisplayName = next;
            }

            var honor = ProfileHonorific();
            var honorNext = frame.TextField.Draw("ad-honor-" + profileStamp, stack.Take(frame.Units(34f)), honor,
                "Honorific");
            if (honorNext != honor)
            {
                state.Honorific = ShownName.ClampTitle(honorNext);
            }

            state.Handle = frame.TextField.Draw("ad-handle", stack.Take(frame.Units(34f)), state.Handle, "Handle");
            state.Pronouns = frame.TextField.Draw("ad-pronouns", stack.Take(frame.Units(34f)), state.Pronouns,
                "Pronouns");
            VybeChrome.Kicker(frame, stack.Take(frame.Units(14f)), "BIO", night);
            state.About = frame.TextField.Draw("ad-bio", stack.Take(frame.Units(72f)), state.About,
                "Write a short bio");
            VybeChrome.Kicker(frame, stack.Take(frame.Units(14f)), "GENDER", night);
            DrawWrapChips(frame, stack.Take(frame.Units(88f)), SceneBook.Genders, state.Genders, night);
            VybeChrome.Kicker(frame, stack.Take(frame.Units(14f)), "SEXUALITY", night);
            DrawPick(frame, stack.Take(frame.Units(56f)), SceneBook.Sexuality, state.Sexuality, value =>
            {
                state.Sexuality = value;
            }, night);
            VybeChrome.Kicker(frame, stack.Take(frame.Units(14f)), "DMS", night);
            var dms = stack.Take(frame.Units(32f));
            var dmsOpen = dms.LeftSlice(dms.Width * 0.48f);
            var dmsShut = dms.RightSlice(dms.Width * 0.48f);
            if (VybeChrome.Chip(frame, dmsOpen, "Open", state.DmsOpen, night))
            {
                state.DmsOpen = true;
            }

            if (VybeChrome.Chip(frame, dmsShut, "Closed", !state.DmsOpen, night))
            {
                state.DmsOpen = false;
            }

            VybeChrome.Kicker(frame, stack.Take(frame.Units(14f)), "INTERESTS", night);
            var intents = night ? SceneBook.Intents : SceneBook.DayIntents;
            DrawWrapChips(frame, stack.Take(frame.Units(128f)), intents, state.Intents, night);
            VybeChrome.Kicker(frame, stack.Take(frame.Units(14f)), "RELATIONSHIP", night);
            DrawPick(frame, stack.Take(frame.Units(56f)), SceneBook.Relationships, state.Relationship, value =>
            {
                state.Relationship = value;
            }, night);
        }
        else if (state.Page == NightPage.OnboardIntent)
        {
            VybeChrome.Mute(frame, stack.Take(frame.Units(28f)),
                "Choose everything that fits. It shapes who finds you.", night);
            var intents = night ? SceneBook.Intents : SceneBook.DayIntents;
            DrawWrapChips(frame, stack.Take(frame.Units(128f)), intents, state.Intents, night);
        }
        else if (state.Page == NightPage.OnboardAbout)
        {
            VybeChrome.Mute(frame, stack.Take(frame.Units(20f)), "A line or two goes a long way.", night);
            state.About = frame.TextField.Draw("ad-about", stack.Take(frame.Units(64f)), state.About,
                "Introduce yourself");
            VybeChrome.Kicker(frame, stack.Take(frame.Units(14f)), "VIBE", night);
            DrawWrapChips(frame, stack.Take(frame.Units(72f)), SceneBook.Tone, state.Tags, night);
            VybeChrome.Kicker(frame, stack.Take(frame.Units(14f)), "ORIENTATION", night);
            DrawPick(frame, stack.Take(frame.Units(56f)), SceneBook.Sexuality, state.Sexuality, value =>
            {
                state.Sexuality = value;
            }, night);
            VybeChrome.Kicker(frame, stack.Take(frame.Units(14f)), "STATUS", night);
            DrawPick(frame, stack.Take(frame.Units(56f)), SceneBook.Relationships, state.Relationship, value =>
            {
                state.Relationship = value;
            }, night);
        }
        else
        {
            VybeChrome.Mute(frame, stack.Take(frame.Units(28f)),
                "A couple of last touches, then step inside.", night);
            var row = stack.Take(frame.Units(36f));
            if (VybeChrome.Chip(frame, row, state.Discoverable ? "Discoverable" : "Hidden", state.Discoverable,
                    night))
            {
                state.Discoverable = !state.Discoverable;
            }
        }

        var go = stack.Take(frame.Units(44f));
        var goLabel = state.Onboarded && state.Page == NightPage.OnboardIdentity
            ? "Save"
            : state.Page == NightPage.OnboardReady
                ? "Enter"
                : "Continue";
        VybeChrome.Primary(frame, go, goLabel, night);
        if (frame.Input.ConsumeClick(go))
        {
            AdvanceOnboard();
        }
    }

    private void AdvanceOnboard()
    {
        if (state.Page == NightPage.OnboardIdentity)
        {
            if (state.Onboarded)
            {
                state.Page = NightPage.Tabs;
                state.Tab = NightTab.Profile;
                state.Save(paths);
                return;
            }

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
                if (state.DiscoverPane == 0)
                {
                    DrawPeopleFilters(frame, area);
                }
                else
                {
                    DrawFilters(frame, area);
                }

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
            case NightPage.Inbox:
                DrawMessages(frame, area);
                break;
            case NightPage.Alerts:
                DrawAlerts(frame, area);
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
        if (VybeChrome.Back(frame, stack.Take(frame.Units(28f)), "Search", night))
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
        VybeChrome.Kicker(frame, stack.Take(frame.Units(14f)), "PEOPLE", night);
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

        VybeChrome.Kicker(frame, stack.Take(frame.Units(14f)), "POSTS", night);
        var posts = 0;
        foreach (var wallPost in VisibleFeed(BoardFeed()))
        {
            DrawPearlCard(frame, stack.Take(frame.Units(PostCardHeight(frame, wallPost))), wallPost);
            posts++;
        }

        if (people == 0 && posts == 0)
        {
            VybeChrome.Mute(frame, stack.Take(frame.Units(28f)), "No matches yet.", night);
        }
    }

    private void DrawStory(in AppletFrame frame, Rect area)
    {
        var night = state.Night;
        ScenePerson person;
        if (state.StoryIndex < 0)
        {
            person = new ScenePerson(-1, string.Empty, ProfileName(), state.Handle, "You", state.OwnStory, true, 0,
                false, VybeChrome.Tone(night).Accent, Array.Empty<string>(), Array.Empty<string>(),
                pearl.Current.MeAvatarUrl);
        }
        else if (!state.TryFind(state.StoryIndex, out person))
        {
            state.Back();
            return;
        }
        var still = person.Id < 0 ? state.StoryMedia : VybeDemo.StoryStill(paths, person.GateId);
        frame.Paint.Fill(area, person.Wash with { W = 1f }, frame.Units(16f));
        if (still.Length > 0)
        {
            DrawStill(frame, area, still, true);
        }

        frame.Paint.FillGradient(area, new Vector4(0f, 0f, 0f, 0.35f), new Vector4(0f, 0f, 0f, 0.62f),
            GradientAxis.Vertical);
        var stack = new Stack(area.Inset(frame.Units(14f)), StackAxis.Vertical, frame.Units(8f));
        if (VybeChrome.Back(frame, stack.Take(frame.Units(28f)), person.Name, night: true))
        {
            state.Back();
            return;
        }

        VybeChrome.Mute(frame, stack.Take(frame.Units(18f)), person.Handle, true);
        frame.Text.DrawWrapped(stack.Take(frame.Units(80f)),
            person.Line.Length > 0 ? person.Line : "No story yet.",
            new TextStyle(FontRole.BodyStrong, VybeChrome.Night.Ink));
        if (frame.Input.ConsumeClick(area.RightSlice(area.Width * 0.28f)))
        {
            StepStory(1);
            return;
        }

        if (frame.Input.ConsumeClick(area.LeftSlice(area.Width * 0.22f)))
        {
            StepStory(-1);
            return;
        }

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

    private void StepStory(int step)
    {
        var people = StoryPeople();
        if (people.Count == 0)
        {
            state.Back();
            return;
        }

        var index = -1;
        for (var i = 0; i < people.Count; i++)
        {
            if (people[i].Id == state.StoryIndex)
            {
                index = i;
                break;
            }
        }

        var next = state.StoryIndex < 0 ? (step > 0 ? 0 : people.Count - 1) : index + step;
        if (next < 0 || next >= people.Count)
        {
            state.Back();
            return;
        }

        state.StoryIndex = people[next].Id;
        state.ViewedStories.Add(people[next].Id);
        state.Save(paths);
    }

    private void DrawStoryCompose(in AppletFrame frame, Rect area)
    {
        var night = state.Night;
        var stack = new Stack(area, StackAxis.Vertical, frame.Units(8f));
        if (VybeChrome.Back(frame, stack.Take(frame.Units(28f)), "Your story", night))
        {
            state.Back();
            return;
        }

        state.Caption = frame.TextField.Draw("ad-story", stack.Take(frame.Units(88f)), state.Caption,
            "What are you up to?");
        if (VybeChrome.Chip(frame, stack.Take(frame.Units(32f)),
                state.StoryMedia.Length > 0 ? "Photo attached" : "Add photo", state.StoryMedia.Length > 0, night))
        {
            state.PickingAvatar = false;
            state.Open(NightPage.PhotoPick);
        }

        var post = stack.Take(frame.Units(44f));
        VybeChrome.Primary(frame, post, state.Caption.Length > 0 || state.StoryMedia.Length > 0
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

        var tone = VybeChrome.Tone(night);
        var hero = DrawProfileHero(frame, area, night, string.Empty, person.Wash with { W = 0.92f },
            person.AvatarUrl, person.AvatarUrl, person.Wash, edit: false, back: true);
        if (hero.Back)
        {
            state.Back();
            return;
        }

        if (hero.Flag && !state.ReportOpen)
        {
            OpenProfileReport(person.GateId.Length > 0 ? person.GateId : person.Id.ToString(CultureInfo.InvariantCulture),
                person.Name);
        }

        var pad = frame.Units(14f);
        var stack = new Stack(
            new Rect(new Vector2(area.Min.X + pad, hero.InfoTop), new Vector2(area.Max.X - pad, area.Max.Y)),
            StackAxis.Vertical, frame.Units(6f));
        var status = person.Online ? "Online" : "Away";
        var meta = person.World.Length > 0 ? person.World + " · " + status : status;
        DrawProfileIdentity(frame, ref stack, person.Name, person.Handle, meta, person.Line, night);
        DrawPersonFacts(frame, ref stack, person, night);

        var linked = state.Connected.Contains(person.Id);
        var acts = DrawProfileActions(frame, ref stack, linked, PersonAllowsContact(person), night);
        if (acts.Follow)
        {
            state.FollowPerson(person, pearl, !linked);
        }

        if (acts.Contact)
        {
            state.ChatIndex = person.Id;
            state.Open(NightPage.Chat);
        }
        if (person.GateId.Length > 0)
        {
            pearl.WatchProfile(person.GateId);
        }

        DrawProfileShelf(frame, ref stack, PersonBoard(person.GateId), own: false, night);
    }

    private void DrawPersonFacts(in AppletFrame frame, ref Stack stack, ScenePerson person, bool night)
    {
        var gender = person.Gender;
        var sexuality = person.Sexuality;
        var relationship = person.Relationship;
        var dms = person.DmsOpen;
        var intent = JoinShown(person.Intents, night);
        if (TryPeopleCard(person, out var card))
        {
            if (gender.Length == 0)
            {
                gender = card.Gender;
            }

            if (sexuality.Length == 0)
            {
                sexuality = card.Sexuality;
            }

            if (relationship.Length == 0)
            {
                relationship = card.Relationship;
            }

            dms ??= card.DmsOpen;
            if (intent.Length == 0)
            {
                intent = JoinShown(card.LookingFor, night);
            }
        }

        DrawProfileFacts(frame, ref stack, gender, sexuality, dms, intent, relationship, night);
    }

    private bool TryPeopleCard(ScenePerson person, out PeopleCard card)
    {
        if (findDeck.Length == 0)
        {
            findDeck = PeopleFindBook.Deck(paths);
        }

        for (var index = 0; index < findDeck.Length; index++)
        {
            var hit = findDeck[index];
            if (hit.Id == person.Id ||
                string.Equals(hit.GateId, person.GateId, StringComparison.Ordinal))
            {
                card = hit;
                return true;
            }
        }

        card = default;
        return false;
    }

    private bool PersonAllowsContact(ScenePerson person)
    {
        if (person.DmsOpen is { } open)
        {
            return open;
        }

        return !TryPeopleCard(person, out var card) || card.DmsOpen;
    }

    private void DrawPost(in AppletFrame frame, Rect area)
    {
        var night = state.Night;
        pearl.WatchPost(state.PostKey);
        var post = BoardPost(state.PostKey);
        if (post is null)
        {
            VybeChrome.Mute(frame, area.TopSlice(frame.Units(40f)), "That post is not on Pearlgate yet.", night);
            if (VybeChrome.Back(frame, area.TopSlice(frame.Units(28f)), "Post", night))
            {
                state.Back();
            }

            return;
        }

        var ready = post.Value;
        var stack = new Stack(area, StackAxis.Vertical, frame.Units(8f));
        if (VybeChrome.Back(frame, stack.Take(frame.Units(28f)), "Post", night))
        {
            state.Back();
            return;
        }

        DrawPearlCard(frame, stack.Take(frame.Units(PostCardHeight(frame, ready))), ready);
        VybeChrome.Kicker(frame, stack.Take(frame.Units(14f)), "COMMENTS", night);
        var lines = pearl.CommentsFor(ready.Id);
        if (lines.Count > 0)
        {
            foreach (var comment in lines)
            {
                var row = stack.Take(frame.Units(44f));
                VybeChrome.Plate(frame, row, frame.Units(10f), night);
                VybeChrome.Title(frame, row.TopSlice(frame.Units(18f)).Inset(new Edges(frame.Units(8f), 0f)),
                    comment.Author, night);
                VybeChrome.Mute(frame, row.BottomSlice(frame.Units(18f)).Inset(new Edges(frame.Units(8f), 0f)),
                    comment.Body, night);
            }
        }
        else
        {
            VybeChrome.Mute(frame, stack.Take(frame.Units(24f)), "No comments yet. Be the first.", night);
        }

        state.CommentDraft = frame.TextField.Draw("ad-comment", stack.Take(frame.Units(36f)), state.CommentDraft,
            "Write a comment…");
        var send = stack.Take(frame.Units(40f));
        VybeChrome.Primary(frame, send, "Comment", night);
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
        if (VybeChrome.Back(frame, stack.Take(frame.Units(28f)), "New Post", night))
        {
            state.Back();
            return;
        }

        if (state.QuoteOf.Length > 0)
        {
            var quoted = BoardPost(state.QuoteOf);
            VybeChrome.Kicker(frame, stack.Take(frame.Units(14f)), "QUOTING", night);
            VybeChrome.Mute(frame, stack.Take(frame.Units(28f)),
                quoted is { } q ? q.AuthorName + " · " + q.Body : state.QuoteOf, night);
        }

        state.Caption = frame.TextField.Draw("ad-caption", stack.Take(frame.Units(88f)), state.Caption,
            night ? "What's the vibe tonight?" : "What's on your mind?");
        var photos = stack.Take(frame.Units(32f));
        if (VybeChrome.Chip(frame, photos, state.DraftMedia.Count == 0 ? "Photo" : "Photos " + state.DraftMedia.Count,
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
        if (VybeChrome.Chip(frame, aud.LeftSlice(aud.Width * 0.48f), "Everyone", state.AudienceEveryone, night))
        {
            state.AudienceEveryone = true;
        }

        if (VybeChrome.Chip(frame, aud.RightSlice(aud.Width * 0.48f),
                night ? "Connections" : "Following", !state.AudienceEveryone, night))
        {
            state.AudienceEveryone = false;
        }

        var post = stack.Take(frame.Units(44f));
        VybeChrome.Primary(frame, post, "Post", night);
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
        VybeChrome.Wheel(frame, area, state, frame.Units(520f));
        var shifted = area.Translate(new Vector2(0f, -state.Scroll));
        var stack = new Stack(shifted, StackAxis.Vertical, frame.Units(8f));
        if (VybeChrome.Back(frame, stack.Take(frame.Units(28f)), "Filters", night))
        {
            state.Back();
            return;
        }

        VybeChrome.Mute(frame, stack.Take(frame.Units(32f)),
            "Chips cycle through neutral, include, and exclude.", night);
        foreach (var tag in SceneBook.FilterTags(night))
        {
            var pole = state.Pole(tag);
            var row = stack.Take(frame.Units(32f));
            var on = pole != FilterPole.Neutral;
            VybeChrome.Glow(frame, row, frame.Units(10f), on, night);
            var mark = pole == FilterPole.Include ? "+" : pole == FilterPole.Exclude ? "−" : "·";
            frame.Text.DrawIn(row.Inset(new Edges(frame.Units(12f), 0f)), mark + "  " + tag,
                new TextStyle(FontRole.CaptionStrong, VybeChrome.Tone(night).Ink));
            if (frame.Input.ConsumeClick(row))
            {
                state.CycleFilter(tag);
            }
        }

        var clear = stack.Take(frame.Units(36f));
        VybeChrome.Primary(frame, clear, "Clear all", night);
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
            state.Back();
            return;
        }

        var thread = state.Thread(person.Id);
        talkTray.TickFiles(files, body => PushTalk(thread, body, person));
        if (DrawTalkRoom(frame, area, person.Name, person.Online ? "Active now" : person.World, person.AvatarUrl,
                person.Wash, person.Online, night, thread, "ad-dm", body => PushTalk(thread, body, person)))
        {
            return;
        }

        if (state.Draft.Trim().Length == 0)
        {
            return;
        }

        PushTalk(thread, state.Draft.Trim(), person);
        state.Draft = string.Empty;
    }

    private void PushTalk(List<ChatLine> thread, string body, ScenePerson person)
    {
        if (body.Length == 0)
        {
            return;
        }

        thread.Add(new ChatLine(true, body, TalkStamp()));
        state.FollowPerson(person, pearl, true);
        state.Save(paths);
        state.Scroll = float.MaxValue;
    }

    private void DrawPearlChat(in AppletFrame frame, Rect area, bool night)
    {
        pearl.WatchChat(state.ChatKey);
        var title = "Chat";
        var other = "";
        foreach (var chat in pearl.Current.Chats)
        {
            if (!string.Equals(chat.Id, state.ChatKey, StringComparison.Ordinal))
            {
                continue;
            }

            title = chat.Title;
            other = chat.OtherUserId;
            break;
        }

        var face = "";
        if (other.Length > 0 && state.TryFindGate(other, out var person))
        {
            face = person.AvatarUrl;
        }

        var lines = new List<ChatLine>();
        foreach (var line in pearl.LinesFor(state.ChatKey))
        {
            lines.Add(new ChatLine(line.Mine, line.Body, line.When));
        }

        talkTray.TickFiles(files, body =>
        {
            pearl.SendChat(state.ChatKey, body);
            state.Scroll = float.MaxValue;
        });
        if (DrawTalkRoom(frame, area, title, other.Length > 0 ? "Pearlgate" : "", face, VybeChrome.Tone(night).Accent,
                false, night, lines, "ad-pearl-dm", body =>
                {
                    pearl.SendChat(state.ChatKey, body);
                    state.Scroll = float.MaxValue;
                }))
        {
            return;
        }

        if (state.Draft.Trim().Length == 0)
        {
            return;
        }

        pearl.SendChat(state.ChatKey, state.Draft.Trim());
        state.Draft = string.Empty;
        state.Scroll = float.MaxValue;
    }

    private bool DrawTalkRoom(in AppletFrame frame, Rect area, string title, string status, string avatar, Vector4 wash,
        bool live, bool night, IReadOnlyList<ChatLine> lines, string fieldId, Action<string> sendBit)
    {
        if (talkLook.Length > 0)
        {
            DrawTalkLook(frame, area);
            return true;
        }

        if (talkAlbum)
        {
            DrawTalkAlbum(frame, area, sendBit);
            return true;
        }

        var tone = VybeChrome.Tone(night);
        var head = area.TopSlice(frame.Units(56f));
        var sheetH = talkTray.SheetHeight(frame);
        var composer = area.BottomSlice(frame.Units(56f) + sheetH);
        var bar = composer.TopSlice(frame.Units(56f));
        var sheet = composer.BottomSlice(sheetH);
        var thread = new Rect(new Vector2(area.Min.X, head.Max.Y + frame.Units(4f)),
            new Vector2(area.Max.X, bar.Min.Y - frame.Units(4f)));

        if (DrawTalkHead(frame, head, title, status, avatar, wash, live, night))
        {
            return true;
        }

        DrawTalkThread(frame, thread, title, avatar, wash, night, lines);
        var hold = DrawTalkComposer(frame, bar, fieldId, tone, sendBit);
        var fields = frame.TextField;
        talkTray.DrawSheet(frame, sheet, tone.Ink, tone.Mute, tone.Accent, tone.Card, files, TalkGallery(),
            glyph =>
            {
                state.Draft = fields.Insert(fieldId, state.Draft, glyph);
                fields.Focus(fieldId);
            }, sendBit, () =>
            {
                talkAlbum = true;
                talkAlbumLock = true;
                talkTray.Close();
            });
        return hold;
    }

    private string TalkGallery()
    {
        var library = PhotoLibrary.Load(paths);
        if (library.GposeFolderReady())
        {
            return library.GposeFolder;
        }

        return paths.State("photos");
    }

    private void DrawTalkAlbum(in AppletFrame frame, Rect area, Action<string> sendBit)
    {
        var night = state.Night;
        var tone = VybeChrome.Tone(night);
        var pick = !talkAlbumLock;
        if (talkAlbumLock && !frame.Input.IsHeld())
        {
            talkAlbumLock = false;
        }

        var stack = new Stack(area, StackAxis.Vertical, frame.Units(8f));
        if (VybeChrome.Back(frame, stack.Take(frame.Units(28f)), "Gallery", night))
        {
            talkAlbum = false;
            talkAlbumLock = false;
            return;
        }

        var shots = GalleryFiles.List(paths);
        if (shots.Count == 0)
        {
            VybeChrome.Mute(frame, stack.Take(frame.Units(40f)),
                "No photos in Camera yet. Take a still, then pick it here.", night);
            return;
        }

        VybeChrome.Mute(frame, stack.Take(frame.Units(16f)), "Pick a photo to send", night);
        for (var index = 0; index < shots.Count; index += 3)
        {
            var row = stack.Take(frame.Units(96f));
            var gap = frame.Units(6f);
            var cell = (row.Width - gap * 2f) / 3f;
            for (var col = 0; col < 3 && index + col < shots.Count; col++)
            {
                var shot = Rect.FromSize(
                    new Vector2(row.Min.X + (cell + gap) * col, row.Min.Y),
                    new Vector2(cell, cell));
                DrawStill(frame, shot, shots[index + col].Path, night);
                if (!pick || !frame.Input.ConsumeClick(shot))
                {
                    continue;
                }

                sendBit(ChatBits.Pic(shots[index + col].Path));
                talkAlbum = false;
                talkAlbumLock = false;
                return;
            }
        }
    }

    private string TalkLocation()
    {
        var zone = game.ZoneName.Length > 0 ? game.ZoneName : "Unknown zone";
        var world = game.Character.WorldName;
        var map = game.MapCoords;
        var aetheryte = lifestream.NearestAetheryte(game.TerritoryId);
        return ChatBits.Location(zone, world, game.TerritoryId, map.X, map.Y, aetheryte);
    }

    private bool DrawTalkHead(in AppletFrame frame, Rect area, string title, string status, string avatar, Vector4 wash,
        bool live, bool night)
    {
        var tone = VybeChrome.Tone(night);
        var back = area.LeftSlice(frame.Units(32f));
        DrawTalkBack(frame, back, tone.Ink);
        if (frame.Input.ConsumeClick(back))
        {
            state.ChatKey = string.Empty;
            state.Back();
            return true;
        }

        var face = area.Inset(new Edges(frame.Units(36f), frame.Units(8f), 0f, frame.Units(8f))).LeftSlice(frame.Units(40f));
        DrawFace(frame, face.Center, frame.Units(16f), avatar, wash, night);
        if (live)
        {
            frame.Paint.FillCircle(face.Center + new Vector2(frame.Units(11f), frame.Units(11f)), frame.Units(4.2f),
                VybeChrome.Online);
        }

        var copy = area.Inset(new Edges(frame.Units(82f), frame.Units(8f), frame.Units(8f), frame.Units(8f)));
        frame.Text.DrawEllipsized(copy.TopSlice(frame.Units(22f)), title,
            new TextStyle(FontRole.BodyStrong, tone.Ink));
        if (status.Length > 0)
        {
            frame.Text.DrawEllipsized(copy.BottomSlice(frame.Units(16f)), status,
                new TextStyle(FontRole.Caption, live ? VybeChrome.Online : tone.Mute));
        }

        return false;
    }

    private void DrawTalkThread(in AppletFrame frame, Rect area, string title, string avatar, Vector4 wash, bool night,
        IReadOnlyList<ChatLine> lines)
    {
        var key = state.ChatKey.Length > 0 ? "p:" + state.ChatKey : "l:" + state.ChatIndex.ToString(CultureInfo.InvariantCulture);
        var fresh = !string.Equals(chatAnchor, key, StringComparison.Ordinal);
        if (fresh)
        {
            chatAnchor = key;
            state.Scroll = float.MaxValue;
            frame.TextField.Focus(state.ChatKey.Length > 0 ? "ad-pearl-dm" : "ad-dm");
        }

        if (lines.Count == 0)
        {
            DrawTalkEmpty(frame, area, title, avatar, wash, night);
            return;
        }

        var gap = frame.Units(6f);
        var heights = new float[lines.Count];
        var total = frame.Units(10f);
        for (var index = 0; index < lines.Count; index++)
        {
            heights[index] = ChatBits.BubbleHeight(frame, area.Width, lines[index].Body);
            total += heights[index] + gap;
        }

        var maxScroll = MathF.Max(0f, total - area.Height);
        if (fresh || state.Scroll > maxScroll)
        {
            state.Scroll = maxScroll;
        }

        VybeChrome.Wheel(frame, area, state, total);
        frame.Paint.PushClip(area);
        var cursor = area.Min.Y - state.Scroll + frame.Units(8f);
        for (var index = 0; index < lines.Count; index++)
        {
            var line = lines[index];
            var row = Rect.FromSize(new Vector2(area.Min.X, cursor), new Vector2(area.Width, heights[index]));
            var grouped = index + 1 < lines.Count && lines[index + 1].Mine == line.Mine;
            DrawTalkBubble(frame, row, line, avatar, wash, grouped, night);
            cursor += heights[index] + gap;
        }

        frame.Paint.PopClip();
    }

    private void DrawTalkEmpty(in AppletFrame frame, Rect area, string title, string avatar, Vector4 wash, bool night)
    {
        var tone = VybeChrome.Tone(night);
        var face = area.Center - new Vector2(0f, frame.Units(18f));
        DrawFace(frame, face, frame.Units(28f), avatar, wash, night);
        frame.Text.DrawIn(Rect.FromSize(new Vector2(area.Min.X, face.Y + frame.Units(36f)),
                new Vector2(area.Width, frame.Units(22f))), title,
            new TextStyle(FontRole.BodyStrong, tone.Ink, TextAlign.Center));
        frame.Text.DrawIn(Rect.FromSize(new Vector2(area.Min.X, face.Y + frame.Units(58f)),
                new Vector2(area.Width, frame.Units(18f))), "Say hello to start the chat",
            new TextStyle(FontRole.Caption, tone.Mute, TextAlign.Center));
    }

    private void DrawTalkBubble(in AppletFrame frame, Rect row, ChatLine line, string avatar, Vector4 wash, bool grouped,
        bool night)
    {
        var tone = VybeChrome.Tone(night);
        var bit = ChatBits.Read(line.Body);
        var maxW = row.Width * 0.78f;
        var bubbleW = bit.Kind == ChatBitKind.Pic
            ? ChatBits.StillBox(frame, maxW, bit.Path).X
            : bit.Kind is ChatBitKind.Gif or ChatBitKind.Sticker or ChatBitKind.Place
            ? maxW
            : MathF.Max(frame.Units(56f),
                MathF.Min(maxW, frame.Text.MeasureWrapped(line.Body, FontRole.Body, maxW - frame.Units(20f)).X +
                                frame.Units(20f)));
        var incoming = new Vector4(0.165f, 0.175f, 0.210f, 0.96f);
        Rect bubble;
        if (line.Mine)
        {
            bubble = row.RightSlice(bubbleW);
            frame.Paint.Fill(bubble, tone.Accent, frame.Units(18f));
            ChatBits.Draw(frame, bubble.Inset(new Edges(frame.Units(10f), frame.Units(7f), frame.Units(10f),
                    frame.Units(16f))), line.Body, tone.AccentInk, tone.AccentInk with { W = 0.72f }, lifestream);
            frame.Text.DrawIn(bubble.BottomSlice(frame.Units(14f)).Inset(new Edges(frame.Units(10f), 0f)),
                line.When.Length > 0 ? line.When : "Now",
                new TextStyle(FontRole.Caption, tone.AccentInk with { W = 0.72f }, TextAlign.Right, 1f, 0.85f));
            OpenTalkPic(frame, bubble, bit);
            return;
        }

        var face = row.LeftSlice(frame.Units(28f));
        if (!grouped)
        {
            DrawFace(frame, new Vector2(face.Center.X, row.Max.Y - frame.Units(14f)), frame.Units(10f), avatar, wash,
                night);
        }

        bubble = Rect.FromSize(new Vector2(row.Min.X + frame.Units(32f), row.Min.Y),
            new Vector2(bubbleW, row.Height));
        frame.Paint.Fill(bubble, incoming, frame.Units(18f));
        ChatBits.Draw(frame, bubble.Inset(new Edges(frame.Units(10f), frame.Units(7f), frame.Units(10f),
                frame.Units(16f))), line.Body, tone.Ink, tone.Mute, lifestream);
        frame.Text.DrawIn(bubble.BottomSlice(frame.Units(14f)).Inset(new Edges(frame.Units(10f), 0f)),
            line.When.Length > 0 ? line.When : "Now",
            new TextStyle(FontRole.Caption, tone.Mute, TextAlign.Left, 1f, 0.85f));
        OpenTalkPic(frame, bubble, bit);
    }

    private void OpenTalkPic(in AppletFrame frame, Rect bubble, ChatBit bit)
    {
        if (bit.Kind != ChatBitKind.Pic || bit.Path.Length == 0)
        {
            return;
        }

        if (frame.Input.ConsumeClick(bubble))
        {
            talkLook = bit.Path;
        }
    }

    private void DrawTalkLook(in AppletFrame frame, Rect area)
    {
        var night = state.Night;
        var tone = VybeChrome.Tone(night);
        frame.Paint.Fill(area, tone.Ground);
        var stack = new Stack(area, StackAxis.Vertical, frame.Units(8f));
        if (VybeChrome.Back(frame, stack.Take(frame.Units(28f)), "Photo", night))
        {
            talkLook = string.Empty;
            return;
        }

        var stage = stack.Remaining.Inset(frame.Units(8f));
        if (talkLook.Length == 0 || !File.Exists(talkLook))
        {
            VybeChrome.Mute(frame, stage.TopSlice(frame.Units(36f)), "Photo is gone.", night);
            return;
        }

        var texture = frame.Textures.FromFile(talkLook);
        if (texture is not { IsReady: true })
        {
            return;
        }

        var dest = CoverFit.Contained(texture.Size, stage);
        frame.Paint.ImageRounded(texture, dest, Vector2.Zero, Vector2.One, Vector4.One, frame.Units(12f));
    }

    private bool DrawTalkComposer(in AppletFrame frame, Rect area, string fieldId, NightPalette tone,
        Action<string> sendBit)
    {
        var idle = tone.Mute;
        var slot = frame.Units(32f);
        var gap = frame.Units(2f);
        var mid = area.Center.Y;
        var top = mid - slot * 0.5f;
        Rect Slot(float x) => Rect.FromSize(new Vector2(x, top), new Vector2(slot, slot));
        var plus = Slot(area.Min.X);
        var send = Slot(area.Max.X - slot);
        var faces = Slot(send.Min.X - gap - slot);
        var pin = Slot(faces.Min.X - gap - slot);
        talkTray.DrawPlus(frame, plus, idle);
        if (talkTray.DrawPlace(frame, pin, idle))
        {
            sendBit(TalkLocation());
        }

        talkTray.DrawFaces(frame, faces, idle);
        var field = new Rect(new Vector2(plus.Max.X + gap, top),
            new Vector2(pin.Min.X - gap, top + slot));
        frame.Paint.Fill(field, new Vector4(0.10f, 0.10f, 0.12f, 0.94f), field.Height * 0.5f);
        state.Draft = frame.TextField.Draw(fieldId, field.Inset(new Edges(frame.Units(12f), frame.Units(4f))),
            state.Draft, "Message", 400, out var submitted, true);
        var ready = state.Draft.Trim().Length > 0;
        frame.Paint.FillCircle(send.Center, frame.Units(14f), ready ? tone.Accent : tone.CardHi);
        DrawTalkSend(frame.Paint, send.Center, frame.Units(5.5f), ready ? tone.AccentInk : tone.Mute);
        var go = submitted || frame.Input.ConsumeClick(send);
        frame.Input.Claim(area);
        return !go || !ready;
    }

    private static void DrawTalkBack(in AppletFrame frame, Rect area, Vector4 ink)
    {
        var c = area.Center;
        var s = frame.Units(7f);
        var stroke = frame.Units(1.8f);
        frame.Paint.Line(c + new Vector2(s * 0.35f, -s), c + new Vector2(-s * 0.65f, 0f), ink, stroke);
        frame.Paint.Line(c + new Vector2(-s * 0.65f, 0f), c + new Vector2(s * 0.35f, s), ink, stroke);
        frame.Paint.Line(c + new Vector2(-s * 0.65f, 0f), c + new Vector2(s * 0.85f, 0f), ink, stroke);
    }

    private static void DrawTalkSend(IPaintSurface paint, Vector2 center, float size, Vector4 ink)
    {
        var tip = center + new Vector2(size * 1.15f, 0f);
        var top = center + new Vector2(-size, -size * 0.85f);
        var bot = center + new Vector2(-size, size * 0.85f);
        var mid = center + new Vector2(-size * 0.12f, 0f);
        paint.Line(tip, top, ink, size * 0.28f);
        paint.Line(tip, bot, ink, size * 0.28f);
        paint.Line(top, mid, ink, size * 0.24f);
        paint.Line(bot, mid, ink, size * 0.24f);
    }

    private static string TalkStamp()
    {
        var now = DateTime.Now;
        return now.ToString("h:mm tt", CultureInfo.InvariantCulture);
    }

    private void DrawRequests(in AppletFrame frame, Rect area)
    {
        var night = state.Night;
        var stack = new Stack(area, StackAxis.Vertical, frame.Units(8f));
        if (VybeChrome.Back(frame, stack.Take(frame.Units(28f)), "Requests", night))
        {
            state.Back();
            return;
        }

        if (state.Incoming.Count == 0)
        {
            VybeChrome.Mute(frame, stack.Take(frame.Units(36f)),
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
            VybeChrome.Plate(frame, row, frame.Units(12f), night);
            var inner = row.Inset(frame.Units(8f));
            VybeChrome.Title(frame, inner.TopSlice(frame.Units(20f)),
                person.Name + (night ? " wants to connect" : " followed you"), night);
            var btns = inner.BottomSlice(frame.Units(28f));
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
        }
    }

    private void DrawSettings(in AppletFrame frame, Rect area)
    {
        var night = state.Night;
        var tone = VybeChrome.Tone(night);
        var stack = new Stack(area, StackAxis.Vertical, frame.Units(10f));
        if (VybeChrome.Back(frame, stack.Take(frame.Units(28f)), "Settings", night))
        {
            state.Back();
            return;
        }

        VybeChrome.Kicker(frame, stack.Take(frame.Units(14f)), "MODE", night);
        VybeChrome.Mute(frame, stack.Take(frame.Units(40f)),
            "VYBE is the default, SFW suite. After Dark is an 18+ opt-in. Hold the sun or moon on Home to flip.",
            night);

        var switcher = stack.Take(frame.Units(44f));
        VybeChrome.Plate(frame, switcher, frame.Units(14f), night);
        var dayHit = switcher.LeftSlice(switcher.Width * 0.5f).Inset(frame.Units(4f));
        var nightHit = switcher.RightSlice(switcher.Width * 0.5f).Inset(frame.Units(4f));
        VybeChrome.Glow(frame, dayHit, frame.Units(10f), !night, night);
        VybeChrome.Glow(frame, nightHit, frame.Units(10f), night, night);
        frame.Text.DrawIn(dayHit, "VYBE",
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

        if (VybeChrome.Chip(frame, stack.Take(frame.Units(36f)), "Blocked", false, night))
        {
            state.Open(NightPage.Blocked);
        }

        if (VybeChrome.Chip(frame, stack.Take(frame.Units(36f)), "Likes", false, night))
        {
            state.Open(NightPage.Likes);
        }

        if (VybeChrome.Chip(frame, stack.Take(frame.Units(36f)), "Gallery", false, night))
        {
            state.Open(NightPage.Gallery);
        }

        if (VybeChrome.Chip(frame, stack.Take(frame.Units(36f)), "Following", false, night))
        {
            state.Open(NightPage.Following);
        }

        if (VybeChrome.Chip(frame, stack.Take(frame.Units(36f)), "Edit profile", false, night))
        {
            state.Open(NightPage.OnboardIdentity);
        }
    }

    private void DrawGallery(in AppletFrame frame, Rect area)
    {
        var night = state.Night;
        var stack = new Stack(area, StackAxis.Vertical, frame.Units(8f));
        if (VybeChrome.Back(frame, stack.Take(frame.Units(28f)), "Gallery", night))
        {
            state.Back();
            return;
        }

        DrawGallerySearch(frame, stack.Take(frame.Units(36f)), night);
        DrawMasonryGallery(frame, stack.TakeRemaining(), BoardFeed(), night);
    }

    private void DrawLikes(in AppletFrame frame, Rect area)
    {
        var night = state.Night;
        var stack = new Stack(area, StackAxis.Vertical, frame.Units(8f));
        if (VybeChrome.Back(frame, stack.Take(frame.Units(28f)), "Likes", night))
        {
            state.Back();
            return;
        }

        var shown = 0;
        foreach (var wallPost in BoardFeed())
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
            VybeChrome.Mute(frame, stack.Take(frame.Units(28f)), "No likes on this feed yet.", night);
        }
    }

    private void DrawPhotoPick(in AppletFrame frame, Rect area)
    {
        var night = state.Night;
        var stack = new Stack(area, StackAxis.Vertical, frame.Units(8f));
        if (VybeChrome.Back(frame, stack.Take(frame.Units(28f)), "Choose a photo", night))
        {
            state.Back();
            return;
        }

        var shots = GalleryFiles.List(paths);
        if (shots.Count == 0)
        {
            VybeChrome.Mute(frame, stack.Take(frame.Units(40f)),
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
                    state.ProfileFacePath = path;
                    state.PickingAvatar = false;
                    state.Save(paths);
                }
                else if (state.PickingBanner)
                {
                    state.ProfileBannerPath = path;
                    state.PickingBanner = false;
                    state.Save(paths);
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
        if (VybeChrome.Back(frame, stack.Take(frame.Units(28f)), "Photo", night))
        {
            state.Back();
            return;
        }

        DrawStill(frame, stack.Take(frame.Units(280f)), state.ViewMedia, night);
        var row = stack.Take(frame.Units(36f));
        if (VybeChrome.Chip(frame, row.LeftSlice(row.Width * 0.48f), "Open post", false, night) &&
            state.PostKey.Length > 0)
        {
            OpenPost(state.PostKey);
        }

        if (VybeChrome.Chip(frame, row.RightSlice(row.Width * 0.48f), night ? "Share" : "Repost", false, night))
        {
            state.SharePostId = state.PostKey;
        }
    }

    private void DrawShareSend(in AppletFrame frame, Rect area)
    {
        var night = state.Night;
        var stack = new Stack(area, StackAxis.Vertical, frame.Units(8f));
        if (VybeChrome.Back(frame, stack.Take(frame.Units(28f)), "Send", night))
        {
            state.SharePostId = string.Empty;
            state.Back();
            return;
        }

        if (!pearl.Current.SignedIn)
        {
            VybeChrome.Mute(frame, stack.Take(frame.Units(36f)), "Sign in from You to send.", night);
            return;
        }

        var shown = 0;
        foreach (var chat in pearl.Current.Chats)
        {
            var row = stack.Take(frame.Units(48f));
            VybeChrome.Plate(frame, row, frame.Units(10f), night);
            VybeChrome.Title(frame, row.Inset(frame.Units(10f)), chat.Title, night);
            if (frame.Input.ConsumeClick(row))
            {
                var cited = BoardPost(state.SharePostId);
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
            VybeChrome.Mute(frame, stack.Take(frame.Units(28f)), "No chats yet.", night);
        }
    }

    private void DrawBlocked(in AppletFrame frame, Rect area)
    {
        var night = state.Night;
        var stack = new Stack(area, StackAxis.Vertical, frame.Units(8f));
        if (VybeChrome.Back(frame, stack.Take(frame.Units(28f)), "Blocked", night))
        {
            state.Back();
            return;
        }

        if (state.Blocked.Count == 0)
        {
            VybeChrome.Mute(frame, stack.Take(frame.Units(28f)), "No one blocked.", night);
            return;
        }

        foreach (var id in state.Blocked.ToArray())
        {
            if (!state.TryFind(id, out var person))
            {
                continue;
            }

            var row = stack.Take(frame.Units(44f));
            VybeChrome.Plate(frame, row, frame.Units(10f), night);
            VybeChrome.Title(frame, row.Inset(new Edges(frame.Units(10f), 0f, frame.Units(80f), 0f)), person.Name,
                night);
            if (VybeChrome.Chip(frame, row.RightSlice(frame.Units(76f)).Inset(frame.Units(6f)), "Unblock", false,
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
        if (VybeChrome.Back(frame, stack.Take(frame.Units(28f)), title, night))
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
            VybeChrome.Mute(frame, stack.Take(frame.Units(36f)),
                connected
                    ? "Follow people in Discover and they land here."
                    : "Followers and requests you accept land here.", night);
        }
    }

    private static void DrawToggle(in AppletFrame frame, Rect area, string label, bool on, bool night, Action flip)
    {
        VybeChrome.Plate(frame, area, frame.Units(12f), night);
        VybeChrome.Title(frame, area.Inset(new Edges(frame.Units(12f), 0f, frame.Units(64f), 0f)), label, night);
        var knob = area.RightSlice(frame.Units(52f)).Inset(new Edges(0f, frame.Units(10f), frame.Units(10f),
            frame.Units(10f)));
        VybeChrome.Glow(frame, knob, frame.Units(10f), on, night);
        frame.Text.DrawIn(knob, on ? "On" : "Off",
            new TextStyle(FontRole.CaptionStrong, VybeChrome.Tone(night).Ink, TextAlign.Center));
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
            if (VybeChrome.Chip(frame, cell, tag, string.Equals(current, tag, StringComparison.Ordinal), night))
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
            if (VybeChrome.Chip(frame, cell, tag, on, night))
            {
                if (!selected.Remove(tag))
                {
                    selected.Add(tag);
                }
            }
        }
    }
}
