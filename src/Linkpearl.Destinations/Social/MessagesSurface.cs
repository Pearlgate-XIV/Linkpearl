using System.Globalization;
using System.Numerics;
using Linkpearl.Applets;
using Linkpearl.Cards;
using Linkpearl.Chat;
using Linkpearl.Emoji;
using Linkpearl.Geometry;
using Linkpearl.Input;
using Linkpearl.Layout;
using Linkpearl.Net;
using Linkpearl.Painting;
using Linkpearl.Phone;
using Linkpearl.Platform;
using Linkpearl.Preferences;
using Linkpearl.Talk;
using Linkpearl.Time;

namespace Linkpearl.Destinations.Social;

internal sealed class MessagesSurface
{
    private readonly ITalk talk;
    private readonly IClock clock;
    private readonly IGameSession game;
    private readonly DisplayPreferences display;
    private readonly IPearlHub pearl;
    private readonly ITalkPopouts popouts;
    private readonly IFilePicker files;
    private readonly ChatTray tray = new();
    private string openId = string.Empty;
    private string profileId = string.Empty;
    private string filter = string.Empty;
    private int lastFilterLength;
    private bool suppressAutofill;
    private string draft = string.Empty;
    private string noteDraft = string.Empty;
    private float threadOffset;
    private bool stickBottom = true;
    private bool profileFromThread;
    private int seenGeneration = -1;

    private int inboxPane = SocialPane.Messages;
    private float inboxScroll;
    private InboxMenu? inboxMenu;

    public MessagesSurface(ITalk talk, IClock clock, IGameSession game, DisplayPreferences display, IPearlHub pearl,
        ITalkPopouts popouts, IFilePicker files)
    {
        this.talk = talk;
        this.clock = clock;
        this.game = game;
        this.display = display;
        this.pearl = pearl;
        this.popouts = popouts;
        this.files = files;
    }

    public bool ThreadOpen => openId.Length > 0 && profileId.Length == 0;

    public bool ProfileOpen => profileId.Length > 0;

    public void ShowInbox(int pane) => inboxPane = pane;

    public void ShowDirectInbox()
    {
        RememberNote();
        inboxPane = SocialPane.Messages;
        openId = string.Empty;
        profileId = string.Empty;
        profileFromThread = false;
        draft = string.Empty;
        noteDraft = string.Empty;
        inboxMenu = null;
        inboxScroll = 0f;
    }

    public void Open(string threadId)
    {
        if (openId != threadId)
        {
            draft = string.Empty;
            stickBottom = true;
            threadOffset = 0f;
            seenGeneration = -1;
            tray.Close();
        }

        profileId = string.Empty;
        profileFromThread = false;
        openId = threadId;
        talk.MarkRead(threadId);
    }

    public void OpenProfile(string peerId)
    {
        if (peerId.Length == 0)
        {
            return;
        }

        profileFromThread = openId.Length > 0 && profileId.Length == 0;
        profileId = peerId;
        noteDraft = talk.FindPeer(peerId)?.Note ?? string.Empty;
    }

    public void Close()
    {
        RememberNote();
        openId = string.Empty;
        profileId = string.Empty;
        profileFromThread = false;
        draft = string.Empty;
        filter = string.Empty;
        lastFilterLength = 0;
        suppressAutofill = false;
        noteDraft = string.Empty;
        inboxMenu = null;
        tray.Close();
    }

    public bool Back()
    {
        if (inboxMenu is not null)
        {
            inboxMenu = null;
            return true;
        }

        if (profileId.Length > 0)
        {
            RememberNote();
            profileId = string.Empty;
            noteDraft = string.Empty;
            profileFromThread = false;
            return true;
        }

        if (openId.Length > 0)
        {
            openId = string.Empty;
            draft = string.Empty;
            return true;
        }

        return false;
    }

    public float Compose(in AppletFrame frame)
    {
        if (profileId.Length > 0)
        {
            return DrawProfile(frame);
        }

        return openId.Length > 0 ? DrawThread(frame) : DrawInbox(frame);
    }

    private float DrawInbox(in AppletFrame frame)
    {
        var content = frame.Content;
        var stack = new Stack(content, StackAxis.Vertical, frame.Units(12f));
        var threads = talk.Inbox();

        var searchRow = stack.Take(frame.Units(44f));
        CardChrome.DrawGold(frame, searchRow);
        filter = frame.TextField.Draw("messages-filter",
            searchRow.Inset(new Edges(frame.Units(14f), frame.Units(8f))), filter, InboxPlaceholder(),
            64, out var submitted);

        if (filter.Length < lastFilterLength)
        {
            suppressAutofill = true;
        }
        else if (filter.Length > lastFilterLength)
        {
            suppressAutofill = false;
        }

        lastFilterLength = filter.Length;
        var missed = 0;
        var listed = 0;
        for (var index = 0; index < threads.Count; index++)
        {
            if (!InInbox(threads[index]))
            {
                continue;
            }

            listed++;
            missed += Math.Max(0, threads[index].Unread);
        }

        if (missed > 0)
        {
            var countRow = stack.Take(frame.Units(18f));
            var copy = missed == 1 ? "1 unread message" : missed.ToString(CultureInfo.InvariantCulture) + " unread messages";
            frame.Text.DrawIn(countRow, copy,
                new TextStyle(FontRole.CaptionStrong, frame.Theme.Palette.Negative));
        }

        var trimmed = filter.Trim();
        var direct = inboxPane != SocialPane.Linkshells;
        var nearby = direct && trimmed.Length >= 2 ? talk.SearchNearby(trimmed) : [];
        if (direct && !suppressAutofill)
        {
            TryAutofillNearby(nearby);
            trimmed = filter.Trim();
            nearby = trimmed.Length >= 2 ? talk.SearchNearby(trimmed) : nearby;
        }

        if (direct && submitted && TryOpenFromQuery(nearby, trimmed))
        {
            return content.Height;
        }

        var body = stack.TakeRemaining();
        if (body.Height <= 0f)
        {
            return content.Height;
        }

        var plane = Math.Max(body.Height + 1f,
            frame.Units(84f) * (listed + nearby.Count + 4));
        var shifted = body.Translate(new Vector2(0f, -inboxScroll));
        var list = new Stack(Rect.FromSize(shifted.Min, new Vector2(body.Width, plane)), StackAxis.Vertical,
            frame.Units(12f));
        frame.Paint.PushClip(body);

        if (direct)
        {
            for (var index = 0; index < nearby.Count; index++)
            {
                var hint = nearby[index];
                var row = list.Take(frame.Units(56f));
                CardChrome.DrawGold(frame, row);
                DrawNearbyRow(frame, row.Inset(frame.Units(12f)), hint);
                if (inboxMenu is null && frame.Input.ConsumeClick(row))
                {
                    OpenTell(hint.Name, hint.World);
                }
            }
        }

        if (direct && LooksLikeTell(trimmed) && !HasExactNearby(nearby, trimmed))
        {
            var tellRow = list.Take(frame.Units(48f));
            CardChrome.DrawGold(frame, tellRow);
            var label = "Tell " + trimmed;
            frame.Text.DrawIn(tellRow.Inset(frame.Units(12f)), label,
                new TextStyle(FontRole.BodyStrong, frame.Theme.Palette.WarmAccent));
            if (inboxMenu is null && frame.Input.ConsumeClick(tellRow))
            {
                ParseTellQuery(trimmed, out var name, out var world);
                OpenTell(name, world);
            }
        }

        var shown = 0;
        for (var index = 0; index < threads.Count; index++)
        {
            var thread = threads[index];
            if (!InInbox(thread) || (trimmed.Length > 0 && !Matches(thread, trimmed)))
            {
                continue;
            }

            var row = list.Take(frame.Units(72f));
            CardChrome.DrawGold(frame, row);
            var inner = row.Inset(frame.Units(12f));
            var offerAdd = CanOfferFriend(thread);
            var text = offerAdd ? inner.Inset(new Edges(0f, 0f, frame.Units(72f), 0f)) : inner;
            DrawThreadRow(frame, text, thread);
            if (offerAdd && Chip(frame, inner.RightSlice(frame.Units(64f)).TopSlice(frame.Units(28f)), "Add"))
            {
                OfferFriend(thread);
            }
            else
            {
                HandleInboxRow(frame, row, thread);
            }

            shown++;
        }

        if (shown == 0 && nearby.Count == 0)
        {
            DrawEmpty(frame, list.Take(frame.Units(40f)),
                trimmed.Length > 0
                    ? "Nothing matches."
                    : InboxEmptyCopy());
        }

        var listHeight = plane - list.Remaining.Height;
        frame.Paint.PopClip();

        if (inboxMenu is null && frame.Input.IsHovering(body) && MathF.Abs(frame.Input.ScrollDelta) > 0.01f)
        {
            inboxScroll -= frame.Input.ScrollDelta * frame.Units(28f);
        }

        inboxScroll = Math.Clamp(inboxScroll, 0f, MathF.Max(0f, listHeight - body.Height));
        DrawInboxMenu(frame, body);
        return content.Height;
    }

    private void HandleInboxRow(in AppletFrame frame, Rect row, TalkThread thread)
    {
        if (inboxMenu is not null)
        {
            return;
        }

        if (frame.Input.ConsumeClick(row, PointerButton.Secondary))
        {
            inboxMenu = new InboxMenu(thread.Id, thread.Title, frame.Input.Pointer);
            return;
        }

        if (frame.Input.ConsumeClick(row))
        {
            Open(thread.Id);
        }
    }

    private void DrawInboxMenu(in AppletFrame frame, Rect bounds)
    {
        if (inboxMenu is not { } open)
        {
            return;
        }

        var remove = inboxPane == SocialPane.Linkshells ? "Remove chat" : "Remove from Direct";
        var width = frame.Units(176f);
        var rowH = frame.Units(34f);
        var box = Rect.FromSize(
            new Vector2(
                Math.Clamp(open.At.X, bounds.Min.X, bounds.Max.X - width),
                Math.Clamp(open.At.Y, bounds.Min.Y, bounds.Max.Y - rowH - frame.Units(8f))),
            new Vector2(width, rowH + frame.Units(8f)));
        var gold = frame.Theme.Palette.WarmAccent;
        var radius = frame.Units(10f);
        frame.Paint.Fill(box, frame.Theme.Palette.SurfaceRaised with { W = 0.98f }, radius);
        frame.Paint.Stroke(box, gold with { W = 0.55f }, frame.Theme.Metrics.Hairline, radius);
        var row = box.Inset(new Edges(frame.Units(4f), frame.Units(4f)));
        var hover = frame.Input.IsHovering(row);
        if (hover)
        {
            frame.Paint.Fill(row, frame.Theme.Palette.Negative with { W = 0.18f }, frame.Units(8f));
        }

        frame.Text.DrawIn(row.Inset(new Edges(frame.Units(10f), 0f)), remove,
            new TextStyle(FontRole.CaptionStrong,
                hover ? frame.Theme.Palette.Negative : frame.Theme.Palette.Ink));
        if (frame.Input.ConsumeClick(row))
        {
            talk.HideThread(open.Id);
            if (string.Equals(openId, open.Id, StringComparison.Ordinal))
            {
                openId = string.Empty;
            }

            inboxMenu = null;
            return;
        }

        frame.Input.ConsumeClick(box);
        frame.Input.ConsumeClick(box, PointerButton.Secondary);
        frame.Input.Claim(box);
        if (frame.Input.ConsumeClick(bounds) || frame.Input.ConsumeClick(bounds, PointerButton.Secondary))
        {
            inboxMenu = null;
        }
    }

    private readonly record struct InboxMenu(string Id, string Title, Vector2 At);

    private float DrawThread(in AppletFrame frame)
    {
        talk.MarkRead(openId);
        var thread = talk.Find(openId);
        RememberFriendLookup(thread);
        var inset = frame.Units(12f);
        var area = frame.Content.Inset(new Edges(inset, inset, inset, inset));
        var header = area.TopSlice(frame.Units(36f));
        var friendH = CanOfferFriend(thread) ? frame.Units(36f) : 0f;
        var friendBar = friendH > 0f
            ? new Rect(new Vector2(area.Min.X, header.Max.Y + frame.Units(6f)),
                new Vector2(area.Max.X, header.Max.Y + frame.Units(6f) + friendH))
            : header;
        var replies = thread?.CanSend == true ? frame.Units(36f) : 0f;
        var sheetH = tray.SheetHeight(frame);
        var composer = area.BottomSlice(frame.Units(44f) + sheetH);
        var bar = composer.TopSlice(frame.Units(44f));
        var sheet = composer.BottomSlice(sheetH);
        var replyRow = replies > 0f
            ? new Rect(new Vector2(area.Min.X, bar.Min.Y - replies - frame.Units(6f)),
                new Vector2(area.Max.X, bar.Min.Y - frame.Units(6f)))
            : bar;
        var messagesTop = friendH > 0f ? friendBar.Max.Y + frame.Units(8f) : header.Max.Y + frame.Units(8f);
        var messages = new Rect(new Vector2(area.Min.X, messagesTop),
            new Vector2(area.Max.X, (replies > 0f ? replyRow.Min.Y : bar.Min.Y) - frame.Units(8f)));

        DrawHeader(frame, header, thread);
        if (openId.Length == 0)
        {
            return frame.Content.Height;
        }

        if (friendH > 0f)
        {
            DrawThreadFriend(frame, friendBar, thread);
        }

        frame.Paint.Fill(messages, frame.Theme.Palette.SurfaceSunken with { W = 0.90f }, frame.Units(14f));
        DrawLines(frame, messages.Inset(frame.Units(12f)), thread);
        if (replies > 0f)
        {
            DrawReplies(frame, replyRow, thread);
        }

        if (tray.Pane == ChatTrayPane.Attach)
        {
            tray.Close();
        }

        DrawComposer(frame, bar, thread);
        var fields = frame.TextField;
        tray.DrawSheet(frame, sheet, frame.Theme.Palette.Ink, frame.Theme.Palette.InkMuted,
            frame.Theme.Palette.Accent, frame.Theme.Palette.SurfaceRaised, files, string.Empty,
            glyph =>
            {
                draft = fields.Insert("messages-draft", draft, glyph);
                fields.Focus("messages-draft");
            }, SendBit);

        return frame.Content.Height;
    }

    private void DrawHeader(in AppletFrame frame, Rect header, TalkThread? thread)
    {
        var back = header.LeftSlice(frame.Units(28f));
        frame.Text.DrawIn(back, "‹", new TextStyle(FontRole.Title, frame.Theme.Palette.Accent, TextAlign.Center));
        if (frame.Input.ConsumeClick(back) || frame.Input.ConsumeClick(header.LeftSlice(frame.Units(40f))))
        {
            Close();
            return;
        }

        var title = thread?.Title ?? "Messages";
        var subtitle = thread?.Subtitle ?? string.Empty;
        var textArea = header.Inset(new Edges(frame.Units(32f), 0f, frame.Units(22f), 0f));
        frame.Text.DrawEllipsized(textArea.TopSlice(frame.Units(20f)), title,
            new TextStyle(FontRole.BodyStrong, frame.Theme.Palette.Ink));
        if (subtitle.Length > 0)
        {
            frame.Text.DrawEllipsized(textArea.BottomSlice(frame.Units(16f)), subtitle,
                new TextStyle(FontRole.Caption, frame.Theme.Palette.InkMuted));
        }

        if (thread is { Kind: TalkKind.Tell })
        {
            var open = header.RightSlice(frame.Units(22f));
            frame.Text.DrawIn(open, "›",
                new TextStyle(FontRole.Title, frame.Theme.Palette.WarmAccent, TextAlign.Center));
            if (frame.Input.ConsumeClick(textArea) || frame.Input.ConsumeClick(open))
            {
                OpenProfile(thread.Value.Id);
            }
        }
    }

    private void RememberFriendLookup(TalkThread? thread)
    {
        if (!pearl.Current.SignedIn || thread is not { Kind: TalkKind.Tell } tell)
        {
            return;
        }

        if (tell.Title.Length >= 2)
        {
            pearl.NoteQuery(tell.Title);
        }
    }

    private bool CanOfferFriend(TalkThread? thread)
    {
        if (thread is not { } row)
        {
            return false;
        }

        if (row.Kind == TalkKind.Pearl)
        {
            var chat = ChatOf(row.Id);
            return chat is { IsGroup: false } && chat.Value.OtherUserId.Length > 0 &&
                !IsPearlFriend(chat.Value.OtherUserId) && !IsNamedFriend(row.Title, FriendWorld(row.Subtitle));
        }

        return row.Kind == TalkKind.Tell && talk.ShouldOfferFriend(row.Title, FriendWorld(row.Subtitle));
    }

    private bool IsNamedFriend(string name, string world)
    {
        if (name.Length == 0)
        {
            return false;
        }

        var friends = talk.Friends();
        for (var index = 0; index < friends.Count; index++)
        {
            if (SameName(friends[index].Name, name) &&
                (world.Length == 0 || friends[index].World.Length == 0 ||
                 SameName(friends[index].World, world)))
            {
                return true;
            }
        }

        var people = pearl.Current.People;
        for (var index = 0; index < people.Length; index++)
        {
            if (SameName(people[index].DisplayName, name))
            {
                return true;
            }
        }

        return false;
    }

    private bool IsPearlFriend(string userId)
    {
        if (userId.Length == 0)
        {
            return false;
        }

        var people = pearl.Current.People;
        for (var index = 0; index < people.Length; index++)
        {
            if (string.Equals(people[index].Id, userId, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static bool SameName(string left, string right) =>
        left.Equals(right, StringComparison.OrdinalIgnoreCase);

    private static string FriendWorld(string subtitle) =>
        subtitle.Length == 0 || subtitle.Equals("Tell", StringComparison.OrdinalIgnoreCase)
            ? string.Empty
            : subtitle;

    private void DrawThreadFriend(in AppletFrame frame, Rect row, TalkThread? thread)
    {
        CardChrome.DrawGold(frame, row);
        frame.Text.DrawIn(row, "Add friend",
            new TextStyle(FontRole.BodyStrong, frame.Theme.Palette.WarmAccent, TextAlign.Center));
        if (frame.Input.ConsumeClick(row))
        {
            OfferFriend(thread);
        }
    }

    private void OfferFriend(TalkThread? thread)
    {
        if (thread is not { } row)
        {
            return;
        }

        var world = FriendWorld(row.Subtitle);
        if (row.Kind == TalkKind.Tell)
        {
            if (!TalkIds.TryParseTell(row.Id, out var name, out var home))
            {
                name = row.Title;
                home = world;
            }

            talk.RequestFriend(name, home.Length > 0 ? home : world);
            return;
        }

        if (IsNamedFriend(row.Title, world))
        {
            return;
        }

        if (TryResolveFriend(row, out var userId, out var number))
        {
            if (IsPearlFriend(userId))
            {
                return;
            }

            pearl.AddFriend(userId, number);
            return;
        }

        if (row.Title.Length >= 2)
        {
            pearl.NoteQuery(row.Title);
        }
    }

    private bool TryResolveFriend(TalkThread thread, out string userId, out string number)
    {
        userId = string.Empty;
        number = string.Empty;
        var snapshot = pearl.Current;
        if (thread.Kind == TalkKind.Pearl)
        {
            var chat = ChatOf(thread.Id);
            if (chat is { IsGroup: false } && chat.Value.OtherUserId.Length > 0)
            {
                userId = chat.Value.OtherUserId;
                return true;
            }

            return false;
        }

        var peer = talk.FindPeer(thread.Id);
        if (peer is { Number.Length: > 0 })
        {
            number = peer.Value.Number;
        }

        for (var index = 0; index < snapshot.People.Length; index++)
        {
            var person = snapshot.People[index];
            if (!SameName(person.DisplayName, thread.Title))
            {
                continue;
            }

            userId = person.Id;
            number = person.PhoneNumber;
            return true;
        }

        for (var index = 0; index < snapshot.SearchHits.Length; index++)
        {
            var hit = snapshot.SearchHits[index];
            if (hit.Id.Length == 0 || string.Equals(hit.Id, snapshot.MeId, StringComparison.Ordinal))
            {
                continue;
            }

            if (!SameName(hit.Title, thread.Title))
            {
                continue;
            }

            userId = hit.Id;
            return true;
        }

        return number.Length > 0;
    }

    private PearlChat? ChatOf(string threadId)
    {
        const string prefix = "pearl:";
        if (!threadId.StartsWith(prefix, StringComparison.Ordinal))
        {
            return null;
        }

        var id = threadId[prefix.Length..];
        var chats = pearl.Current.Chats;
        for (var index = 0; index < chats.Length; index++)
        {
            if (string.Equals(chats[index].Id, id, StringComparison.Ordinal))
            {
                return chats[index];
            }
        }

        return null;
    }

    private static bool Chip(in AppletFrame frame, Rect area, string label)
    {
        frame.Paint.Fill(area.Inset(frame.Units(2f)), frame.Theme.Palette.Accent, frame.Units(999f));
        frame.Text.DrawIn(area, label, new TextStyle(FontRole.Caption, frame.Theme.Palette.AccentInk, TextAlign.Center));
        return frame.Input.ConsumeClick(area);
    }

    private float DrawProfile(in AppletFrame frame)
    {
        var peer = talk.FindPeer(profileId);
        var inset = frame.Units(14f);
        var content = frame.Content.Inset(inset);
        var stack = new Stack(content, StackAxis.Vertical, frame.Units(10f));

        var header = stack.Take(frame.Units(36f));
        var back = header.LeftSlice(frame.Units(28f));
        frame.Text.DrawIn(back, "‹", new TextStyle(FontRole.Title, frame.Theme.Palette.Accent, TextAlign.Center));
        frame.Text.DrawIn(header.Inset(new Edges(frame.Units(32f), 0f, 0f, 0f)), "Profile",
            new TextStyle(FontRole.BodyStrong, frame.Theme.Palette.Ink));
        if (frame.Input.ConsumeClick(back) || frame.Input.ConsumeClick(header.LeftSlice(frame.Units(40f))))
        {
            CloseProfile();
            return frame.Content.Height;
        }

        if (peer is null)
        {
            if (!TryDrawGateProfile(frame, ref stack, profileId))
            {
                DrawEmpty(frame, stack.Take(frame.Units(72f)), "This person is not saved yet.");
            }

            return (content.Height - stack.Remaining.Height) + inset * 2f;
        }

        var person = peer.Value;
        var hero = stack.Take(frame.Units(88f));
        CardChrome.DrawGold(frame, hero);
        DrawProfileHero(frame, hero.Inset(frame.Units(12f)), person);

        if (person.OnPearlgate)
        {
            DrawFact(frame, stack.Take(frame.Units(48f)), "Pearlgate",
                person.Handle.Length > 0 ? "@" + person.Handle : "On Pearlgate");
            if (person.Number.Length > 0)
            {
                DrawFact(frame, stack.Take(frame.Units(48f)), "Number", LineNumbers.Show(person.Number));
            }
        }
        else
        {
            DrawFact(frame, stack.Take(frame.Units(48f)), "Pearlgate", "Not on Pearlgate. Tells still save here.");
        }

        var when = UnixAgo.Format(person.LastAt, clock.Now);
        var history = person.LineCount > 0
            ? person.LineCount.ToString(CultureInfo.InvariantCulture) + " saved" +
              (when.Length > 0 ? " · " + when : string.Empty)
            : "No tells saved yet";
        DrawFact(frame, stack.Take(frame.Units(48f)), "History", history);

        frame.Text.DrawIn(stack.Take(frame.Units(16f)), "Note",
            new TextStyle(FontRole.CaptionStrong, frame.Theme.Palette.InkMuted));
        var noteRow = stack.Take(frame.Units(44f));
        CardChrome.Draw(frame, noteRow);
        noteDraft = frame.TextField.Draw("profile-note", noteRow.Inset(frame.Units(10f)), noteDraft, "Private note",
            200, out var submitted);
        if (submitted)
        {
            talk.SetNote(profileId, noteDraft);
        }

        var message = stack.Take(frame.Units(44f));
        CardChrome.DrawGold(frame, message);
        frame.Text.DrawIn(message, "Message",
            new TextStyle(FontRole.BodyStrong, frame.Theme.Palette.WarmAccent, TextAlign.Center));
        if (frame.Input.ConsumeClick(message))
        {
            RememberNote();
            var tellId = person.TellId.Length > 0
                ? person.TellId
                : talk.StartTell(person.Name, person.World);
            profileId = string.Empty;
            profileFromThread = false;
            Open(tellId);
        }

        return (content.Height - stack.Remaining.Height) + inset * 2f;
    }

    private void CloseProfile()
    {
        RememberNote();
        profileId = string.Empty;
        noteDraft = string.Empty;
        profileFromThread = false;
    }

    private void RememberNote()
    {
        if (profileId.Length == 0)
        {
            return;
        }

        talk.SetNote(profileId, noteDraft);
    }

    private static void DrawProfileHero(in AppletFrame frame, Rect inset, TalkPeer person)
    {
        var avatar = inset.LeftSlice(frame.Units(56f));
        frame.Paint.FillCircle(avatar.Center, avatar.Width * 0.38f, frame.Theme.Palette.SurfaceRaised);
        frame.Paint.StrokeCircle(avatar.Center, avatar.Width * 0.38f, frame.Theme.Palette.WarmAccent,
            frame.Units(1.4f));
        var initial = person.Name.Length > 0 ? person.Name.AsSpan(0, 1) : "?".AsSpan();
        frame.Text.DrawIn(avatar, initial,
            new TextStyle(FontRole.Title, frame.Theme.Palette.Ink, TextAlign.Center));
        var text = inset.Inset(new Edges(frame.Units(64f), 0f, 0f, 0f));
        frame.Text.DrawEllipsized(text.TopSlice(frame.Units(24f)), person.Name,
            new TextStyle(FontRole.Title, frame.Theme.Palette.Ink));
        var detail = person.World.Length > 0 ? person.World : "Tell";
        frame.Text.DrawEllipsized(text.Inset(new Edges(0f, frame.Units(26f), 0f, 0f)), detail,
            new TextStyle(FontRole.Caption, frame.Theme.Palette.InkMuted));
    }

    private bool TryDrawGateProfile(in AppletFrame frame, ref Stack stack, string peerId)
    {
        if (!TalkIds.TryParsePerson(peerId, out var userId))
        {
            return false;
        }

        var snapshot = pearl.Current;
        PearlPerson? friend = null;
        for (var index = 0; index < snapshot.People.Length; index++)
        {
            if (string.Equals(snapshot.People[index].Id, userId, StringComparison.Ordinal))
            {
                friend = snapshot.People[index];
                break;
            }
        }

        PearlHit hit = default;
        var hasHit = false;
        for (var index = 0; index < snapshot.SearchHits.Length; index++)
        {
            if (string.Equals(snapshot.SearchHits[index].Id, userId, StringComparison.Ordinal))
            {
                hit = snapshot.SearchHits[index];
                hasHit = true;
                break;
            }
        }

        if (friend is null && !hasHit)
        {
            return false;
        }

        var name = friend?.DisplayName ?? hit.Title;
        var detail = friend is { Handle.Length: > 0 } ? "@" + friend.Value.Handle : hit.Subtitle;
        var hero = stack.Take(frame.Units(88f));
        CardChrome.DrawGold(frame, hero);
        var inset = hero.Inset(frame.Units(12f));
        var avatar = inset.LeftSlice(frame.Units(56f));
        frame.Paint.FillCircle(avatar.Center, avatar.Width * 0.38f, frame.Theme.Palette.SurfaceRaised);
        frame.Paint.StrokeCircle(avatar.Center, avatar.Width * 0.38f, frame.Theme.Palette.WarmAccent,
            frame.Units(1.4f));
        var initial = name.Length > 0 ? name.AsSpan(0, 1) : "?".AsSpan();
        frame.Text.DrawIn(avatar, initial,
            new TextStyle(FontRole.Title, frame.Theme.Palette.Ink, TextAlign.Center));
        var text = inset.Inset(new Edges(frame.Units(64f), 0f, 0f, 0f));
        frame.Text.DrawEllipsized(text.TopSlice(frame.Units(24f)), name,
            new TextStyle(FontRole.Title, frame.Theme.Palette.Ink));
        frame.Text.DrawEllipsized(text.Inset(new Edges(0f, frame.Units(26f), 0f, 0f)),
            detail.Length > 0 ? detail : "Pearlgate",
            new TextStyle(FontRole.Caption, frame.Theme.Palette.InkMuted));

        if (friend is { } person)
        {
            if (person.Handle.Length > 0)
            {
                DrawFact(frame, stack.Take(frame.Units(48f)), "Handle", "@" + person.Handle);
            }

            if (person.PhoneNumber.Length > 0)
            {
                DrawFact(frame, stack.Take(frame.Units(48f)), "Number", LineNumbers.Show(person.PhoneNumber));
            }

            DrawFact(frame, stack.Take(frame.Units(48f)), "Linkpearl",
                person.IsMutual ? "Registered · mutual" : "Registered");
        }

        if (name.Length > 0)
        {
            var message = stack.Take(frame.Units(44f));
            CardChrome.DrawGold(frame, message);
            frame.Text.DrawIn(message, "Message",
                new TextStyle(FontRole.BodyStrong, frame.Theme.Palette.WarmAccent, TextAlign.Center));
            if (frame.Input.ConsumeClick(message))
            {
                CloseProfile();
                Open(talk.StartTell(name, string.Empty));
            }
        }

        return true;
    }

    private static void DrawFact(in AppletFrame frame, Rect row, string kicker, string value)
    {
        CardChrome.Draw(frame, row);
        var inner = row.Inset(frame.Units(12f));
        frame.Text.DrawIn(inner.TopSlice(frame.Units(14f)), kicker,
            new TextStyle(FontRole.CaptionStrong, frame.Theme.Palette.WarmAccent));
        frame.Text.DrawEllipsized(inner.BottomSlice(frame.Units(18f)), value,
            new TextStyle(FontRole.Caption, frame.Theme.Palette.Ink));
    }

    private void DrawComposer(in AppletFrame frame, Rect composer, TalkThread? thread)
    {
        var live = frame.TextField.Owns("messages-draft");
        var glass = frame.Theme.Palette.SurfaceOverlay with { W = live ? 0.48f : 0.34f };
        frame.Paint.Fill(composer, glass, frame.Units(12f));
        frame.Paint.Stroke(composer, frame.Theme.Palette.WarmAccent with { W = live ? 0.55f : 0.22f },
            live ? frame.Units(1.4f) : frame.Theme.Metrics.Hairline, frame.Units(12f));
        var inner = composer.Inset(frame.Units(6f));
        var pop = inner.LeftSlice(frame.Units(26f));
        var send = inner.RightSlice(frame.Units(52f));
        var faces = inner.RightSlice(frame.Units(82f)).LeftSlice(frame.Units(26f));
        var field = new Rect(new Vector2(pop.Max.X + frame.Units(4f), inner.Min.Y),
            new Vector2(faces.Min.X - frame.Units(4f), inner.Max.Y));
        var armed = popouts.IsArmed(openId);
        DrawPopoutToggle(frame, pop, armed);
        if (frame.Input.ConsumeClick(pop) && openId.Length > 0)
        {
            popouts.Toggle(openId);
        }

        var canSend = thread?.CanSend == true;
        var hint = ComposerHint(thread);
        if (!canSend)
        {
            frame.Text.DrawIn(field.Inset(frame.Units(6f)), hint,
                new TextStyle(FontRole.Caption, frame.Theme.Palette.InkFaint));
            return;
        }

        tray.DrawFaces(frame, faces, Vector4.One);
        draft = frame.TextField.Draw("messages-draft", field, draft, hint, 400, out var submitted, true);
        var sendInk = draft.Trim().Length > 0 ? frame.Theme.Palette.Accent : frame.Theme.Palette.InkFaint;
        frame.Text.DrawIn(send, "Send", new TextStyle(FontRole.CaptionStrong, sendInk, TextAlign.Center));
        if ((submitted || frame.Input.WasPressed(send) || frame.Input.ConsumeClick(send)) &&
            draft.Trim().Length > 0)
        {
            SendDraft();
            frame.TextField.Focus("messages-draft");
        }
    }

    private static void DrawPopoutToggle(in AppletFrame frame, Rect area, bool on)
    {
        var gold = frame.Theme.Palette.WarmAccent;
        var box = area.Inset(frame.Units(3f));
        if (on)
        {
            frame.Paint.Fill(box, gold with { W = 0.88f }, frame.Units(6f));
            frame.Text.DrawIn(box, "↗",
                new TextStyle(FontRole.CaptionStrong, frame.Theme.Palette.AccentInk, TextAlign.Center));
            return;
        }

        frame.Paint.Stroke(box, gold with { W = 0.55f }, frame.Theme.Metrics.Hairline, frame.Units(6f));
        frame.Text.DrawIn(box, "↗", new TextStyle(FontRole.CaptionStrong, gold, TextAlign.Center));
    }

    private void DrawReplies(in AppletFrame frame, Rect row, TalkThread? thread)
    {
        var replies = display.Replies;
        var chips = replies.Count + 1;
        var gap = frame.Units(6f);
        var width = (row.Width - gap * (chips - 1)) / chips;
        var place = Rect.FromSize(row.Min, new Vector2(width, row.Height));
        CardChrome.Draw(frame, place);
        frame.Text.DrawIn(place, "Place",
            new TextStyle(FontRole.CaptionStrong, frame.Theme.Palette.WarmAccent, TextAlign.Center));
        if (frame.Input.ConsumeClick(place))
        {
            SharePlace();
        }

        for (var index = 0; index < replies.Count; index++)
        {
            var cell = Rect.FromSize(new Vector2(row.Min.X + (index + 1) * (width + gap), row.Min.Y),
                new Vector2(width, row.Height));
            CardChrome.Draw(frame, cell);
            frame.Text.DrawEllipsized(cell.Inset(frame.Units(4f)), replies[index],
                new TextStyle(FontRole.Caption, frame.Theme.Palette.Ink, TextAlign.Center));
            if (frame.Input.ConsumeClick(cell) && thread?.CanSend == true)
            {
                talk.Send(openId, replies[index]);
                stickBottom = true;
            }
        }
    }

    private void SharePlace()
    {
        var zone = game.ZoneName.Length > 0 ? game.ZoneName : "Unknown zone";
        var job = game.JobName.Length > 0 ? game.JobName : "Unknown job";
        SendBit(ChatBits.Place(zone + " · " + job, string.Empty));
    }

    private void SendBit(string body)
    {
        if (body.Length == 0 || openId.Length == 0)
        {
            return;
        }

        talk.Send(openId, body);
        stickBottom = true;
    }

    private void SendDraft()
    {
        var outgoing = draft.Trim();
        if (outgoing.Length == 0)
        {
            return;
        }

        draft = string.Empty;
        talk.Send(openId, outgoing);
        stickBottom = true;
    }

    private void DrawLines(in AppletFrame frame, Rect viewport, TalkThread? thread)
    {
        if (viewport.Height <= 0f)
        {
            return;
        }

        var lines = talk.Lines(openId);
        if (lines.Count == 0)
        {
            DrawEmpty(frame, viewport, EmptyThreadCopy(thread));
            return;
        }

        var gap = frame.Units(8f);
        var heights = new float[lines.Count];
        var total = 0f;
        for (var index = 0; index < lines.Count; index++)
        {
            heights[index] = ChatBits.NamedRowHeight(frame, viewport.Width, lines[index].Body);
            total += heights[index] + (index == 0 ? 0f : gap);
        }

        var maxOffset = MathF.Max(total - viewport.Height, 0f);
        if (talk.Generation != seenGeneration)
        {
            seenGeneration = talk.Generation;
            if (stickBottom)
            {
                threadOffset = maxOffset;
            }
        }

        if (frame.Input.IsHovering(viewport))
        {
            var wheel = frame.Input.ScrollDelta;
            if (MathF.Abs(wheel) > 0.01f)
            {
                stickBottom = false;
                threadOffset = Scalar.Clamp(threadOffset - wheel * 48f * frame.Scale, 0f, maxOffset);
            }
        }

        if (stickBottom)
        {
            threadOffset = maxOffset;
        }
        else
        {
            threadOffset = Scalar.Clamp(threadOffset, 0f, maxOffset);
            if (threadOffset >= maxOffset - 1.5f)
            {
                stickBottom = true;
            }
        }

        frame.Paint.PushClip(viewport);
        var cursor = viewport.Min.Y - threadOffset;
        for (var index = 0; index < lines.Count; index++)
        {
            var height = heights[index];
            var row = new Rect(new Vector2(viewport.Min.X, cursor),
                new Vector2(viewport.Max.X, cursor + height));
            if (row.Max.Y >= viewport.Min.Y && row.Min.Y <= viewport.Max.Y)
            {
                DrawLine(frame, row, lines[index], thread?.Title ?? string.Empty);
            }

            cursor += height + gap;
        }

        frame.Paint.PopClip();
    }

    private static float MeasureLine(in AppletFrame frame, float width, TalkLine line) =>
        ChatBits.NamedRowHeight(frame, width, line.Body);

    private static void DrawLine(in AppletFrame frame, Rect row, TalkLine line, string peerName)
    {
        var bubbleWidth = row.Width * 0.78f;
        var bubble = line.Mine ? row.RightSlice(bubbleWidth) : row.LeftSlice(bubbleWidth);
        var radius = frame.Units(12f);
        var fill = line.Mine
            ? frame.Theme.Palette.Accent with { W = 0.38f }
            : frame.Theme.Palette.SurfaceRaised with { W = 0.36f };
        var ink = frame.Theme.Palette.Ink;
        var who = line.Mine ? "ME" : line.Sender.Length > 0 ? line.Sender : peerName.Length > 0 ? peerName : "Them";
        var nameRow = bubble.TopSlice(frame.Units(14f));
        frame.Text.DrawEllipsized(nameRow, who,
            new TextStyle(FontRole.CaptionStrong, frame.Theme.Palette.WarmAccent,
                line.Mine ? TextAlign.Right : TextAlign.Left));
        var top = bubble.Inset(new Edges(0f, frame.Units(16f), 0f, 0f));
        frame.Paint.Fill(top, fill, radius);
        frame.Paint.Stroke(top, frame.Theme.Palette.WarmAccent with { W = line.Mine ? 0.36f : 0.20f },
            frame.Theme.Metrics.Hairline, radius);
        var copy = top.Inset(frame.Units(8f));
        frame.Paint.PushClip(copy);
        ChatBits.Draw(frame, copy, line.Body, ink, frame.Theme.Palette.InkMuted);
        frame.Paint.PopClip();
    }

    private void DrawThreadRow(in AppletFrame frame, Rect inset, TalkThread thread)
    {
        var avatar = inset.LeftSlice(frame.Units(40f)).TopSlice(frame.Units(40f));
        DrawAvatar(frame, avatar, thread);
        var body = inset.Inset(new Edges(frame.Units(48f), 0f, 0f, 0f));
        var header = body.TopSlice(frame.Units(20f));
        frame.Text.DrawEllipsized(header.Inset(new Edges(0f, 0f, frame.Units(44f), 0f)), thread.Title,
            new TextStyle(FontRole.BodyStrong, frame.Theme.Palette.Ink));
        var when = UnixAgo.Format(thread.LastAt, clock.Now);
        if (when.Length > 0)
        {
            frame.Text.DrawIn(header.RightSlice(frame.Units(44f)), when,
                new TextStyle(FontRole.Caption, frame.Theme.Palette.InkFaint, TextAlign.Right));
        }

        var preview = thread.Preview.Length > 0 ? ChatBits.Preview(thread.Preview) : thread.Subtitle;
        EmojiText.DrawEllipsized(frame, body.Inset(new Edges(0f, frame.Units(22f), thread.Unread > 0 ? frame.Units(28f) : 0f,
            0f)), preview, frame.Theme.Palette.InkMuted);
        if (thread.Unread > 0)
        {
            var badge = body.RightSlice(frame.Units(22f)).BottomSlice(frame.Units(22f));
            frame.Paint.FillCircle(badge.Center, frame.Units(9f), frame.Theme.Palette.Negative);
            var label = thread.Unread > 9 ? "9+" : thread.Unread.ToString(CultureInfo.InvariantCulture);
            frame.Text.Draw(badge.Center, label,
                new TextStyle(FontRole.Caption, Vector4.One, TextAlign.Center));
        }
    }

    private static void DrawAvatar(in AppletFrame frame, Rect area, TalkThread thread)
    {
        frame.Paint.FillCircle(area.Center, area.Width * 0.42f, frame.Theme.Palette.SurfaceRaised);
        frame.Paint.StrokeCircle(area.Center, area.Width * 0.42f, frame.Theme.Palette.WarmAccent, frame.Units(1.2f));
        if (thread.Kind == TalkKind.Tell && thread.Title.Length > 0)
        {
            frame.Text.DrawIn(area, thread.Title.AsSpan(0, 1),
                new TextStyle(FontRole.BodyStrong, frame.Theme.Palette.Ink, TextAlign.Center));
            return;
        }

        frame.Text.DrawIn(area, Glyph(thread.Kind),
            new TextStyle(FontRole.CaptionStrong, frame.Theme.Palette.WarmAccent, TextAlign.Center));
    }

    private bool InInbox(TalkThread thread) => inboxPane switch
    {
        SocialPane.Linkshells => thread.Kind is TalkKind.Party or TalkKind.Alliance or TalkKind.Linkshell
            or TalkKind.CrossWorldLinkshell or TalkKind.FreeCompany or TalkKind.Novice,
        _ => thread.Kind is not (TalkKind.Party or TalkKind.Alliance or TalkKind.Linkshell
            or TalkKind.CrossWorldLinkshell or TalkKind.FreeCompany or TalkKind.Novice or TalkKind.Live),
    };

    private string InboxPlaceholder() =>
        inboxPane == SocialPane.Linkshells ? "Find a room" : "Find nearby";

    private void OpenTell(string name, string world)
    {
        filter = string.Empty;
        lastFilterLength = 0;
        suppressAutofill = false;
        Open(talk.StartTell(name, world));
    }

    private void TryAutofillNearby(IReadOnlyList<GamePeerHint> nearby)
    {
        var trimmed = filter.Trim();
        if (trimmed.Length < 2 || nearby.Count == 0)
        {
            return;
        }

        if (!TryUniquePrefix(nearby, trimmed, out var hit))
        {
            return;
        }

        var filled = FormatPeer(hit);
        if (filled.Equals(filter, StringComparison.Ordinal))
        {
            return;
        }

        filter = filled;
        lastFilterLength = filled.Length;
    }

    private bool TryOpenFromQuery(IReadOnlyList<GamePeerHint> nearby, string trimmed)
    {
        if (nearby.Count == 1)
        {
            OpenTell(nearby[0].Name, nearby[0].World);
            return true;
        }

        if (TryUniquePrefix(nearby, trimmed, out var hit))
        {
            OpenTell(hit.Name, hit.World);
            return true;
        }

        if (!LooksLikeTell(trimmed))
        {
            return false;
        }

        ParseTellQuery(trimmed, out var name, out var world);
        OpenTell(name, world);
        return true;
    }

    private static bool TryUniquePrefix(IReadOnlyList<GamePeerHint> nearby, string trimmed, out GamePeerHint hit)
    {
        hit = default;
        ParseTellQuery(trimmed, out var nameQuery, out var worldQuery);
        if (nameQuery.Length < 2)
        {
            return false;
        }

        var matches = 0;
        var indexHit = -1;
        for (var index = 0; index < nearby.Count; index++)
        {
            if (!nearby[index].Name.StartsWith(nameQuery, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (worldQuery.Length > 0 &&
                !nearby[index].World.StartsWith(worldQuery, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            matches++;
            indexHit = index;
            if (matches > 1)
            {
                return false;
            }
        }

        if (matches != 1)
        {
            return false;
        }

        hit = nearby[indexHit];
        return true;
    }

    private static bool HasExactNearby(IReadOnlyList<GamePeerHint> nearby, string trimmed)
    {
        ParseTellQuery(trimmed, out var nameQuery, out var worldQuery);
        for (var index = 0; index < nearby.Count; index++)
        {
            if (!nearby[index].Name.Equals(nameQuery, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (worldQuery.Length == 0 ||
                nearby[index].World.Equals(worldQuery, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static void DrawNearbyRow(in AppletFrame frame, Rect inset, GamePeerHint hint)
    {
        var stack = new Stack(inset, StackAxis.Vertical, frame.Units(2f));
        frame.Text.DrawIn(stack.Take(frame.Units(20f)), hint.Name,
            new TextStyle(FontRole.BodyStrong, frame.Theme.Palette.Ink));
        var detail = hint.World.Length > 0 ? hint.Reason + " · " + hint.World : hint.Reason;
        frame.Text.DrawIn(stack.Take(frame.Units(16f)), detail,
            new TextStyle(FontRole.Caption, frame.Theme.Palette.InkMuted));
    }

    private static string FormatPeer(GamePeerHint hint) =>
        hint.World.Length == 0 ? hint.Name : hint.Name + "@" + hint.World;

    private string InboxEmptyCopy() => inboxPane == SocialPane.Linkshells
        ? "Party and equipped linkshells live here."
        : "Tells you send and receive are saved here.";

    private static void DrawEmpty(in AppletFrame frame, Rect area, string text)
    {
        frame.Text.DrawIn(area, text,
            new TextStyle(FontRole.Body, frame.Theme.Palette.InkMuted, TextAlign.Center));
    }

    private static string Glyph(TalkKind kind) => kind switch
    {
        TalkKind.Party => "△",
        TalkKind.Alliance => "⚔",
        TalkKind.Tell => "✉",
        TalkKind.Linkshell => "◯",
        TalkKind.CrossWorldLinkshell => "◈",
        TalkKind.FreeCompany => "⌂",
        TalkKind.Novice => "✧",
        TalkKind.Pearl => "✦",
        _ => "💬",
    };

    private static string ComposerHint(TalkThread? thread)
    {
        if (thread is null)
        {
            return "Message";
        }

        if (!thread.Value.CanSend)
        {
            return thread.Value.Kind switch
            {
                TalkKind.Party => "Join a party to talk here",
                TalkKind.Alliance => "Alliance is not available",
                TalkKind.Pearl => "Pearlgate send is not wired yet",
                _ => "Chat is not available right now",
            };
        }

        return thread.Value.Kind switch
        {
            TalkKind.Party => "Party",
            TalkKind.Alliance => "Alliance",
            TalkKind.Tell => "Tell " + thread.Value.Title,
            TalkKind.Linkshell => thread.Value.Title,
            TalkKind.CrossWorldLinkshell => thread.Value.Title,
            _ => "Message",
        };
    }

    private static string EmptyThreadCopy(TalkThread? thread)
    {
        if (thread is null)
        {
            return "No messages yet.";
        }

        return thread.Value.Kind switch
        {
            TalkKind.Party => "Party chat will collect here. Type below when you are in a party.",
            TalkKind.Tell => "Say something. Tells are saved on this character even if they are not on Pearlgate.",
            TalkKind.Linkshell or TalkKind.CrossWorldLinkshell => "This room is your linkshell. Type below.",
            TalkKind.Pearl => "No messages yet.",
            _ => "No messages yet.",
        };
    }

    private static bool Matches(TalkThread thread, string query) =>
        thread.Title.Contains(query, StringComparison.OrdinalIgnoreCase) ||
        thread.Preview.Contains(query, StringComparison.OrdinalIgnoreCase) ||
        thread.Subtitle.Contains(query, StringComparison.OrdinalIgnoreCase);

    private static bool LooksLikeTell(string query)
    {
        if (query.Length < 3)
        {
            return false;
        }

        return query.Contains(' ', StringComparison.Ordinal) || query.Contains('@', StringComparison.Ordinal);
    }

    private static void ParseTellQuery(string query, out string name, out string world)
    {
        var at = query.LastIndexOf('@');
        if (at < 0)
        {
            name = query.Trim();
            world = string.Empty;
            return;
        }

        name = query[..at].Trim();
        world = query[(at + 1)..].Trim();
    }
}