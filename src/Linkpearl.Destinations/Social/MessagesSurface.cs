using System.Globalization;
using Linkpearl.Applets;
using Linkpearl.Cards;
using Linkpearl.Geometry;
using Linkpearl.Layout;
using Linkpearl.Painting;
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

    public MessagesSurface(ITalk talk, IClock clock, IGameSession game, DisplayPreferences display)
    {
        this.talk = talk;
        this.clock = clock;
        this.game = game;
        this.display = display;
    }

    public bool ThreadOpen => openId.Length > 0 && profileId.Length == 0;

    public bool ProfileOpen => profileId.Length > 0;

    public void ShowInbox(int pane) => inboxPane = pane;

    public void Open(string threadId)
    {
        if (openId != threadId)
        {
            draft = string.Empty;
            stickBottom = true;
            threadOffset = 0f;
            seenGeneration = -1;
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
            return content.Height - stack.Remaining.Height;
        }

        if (direct)
        {
            for (var index = 0; index < nearby.Count; index++)
            {
                var hint = nearby[index];
                var row = stack.Take(frame.Units(56f));
                CardChrome.DrawGold(frame, row);
                DrawNearbyRow(frame, row.Inset(frame.Units(12f)), hint);
                if (frame.Input.ConsumeClick(row))
                {
                    OpenTell(hint.Name, hint.World);
                    return content.Height - stack.Remaining.Height;
                }
            }
        }

        if (direct && LooksLikeTell(trimmed) && !HasExactNearby(nearby, trimmed))
        {
            var tellRow = stack.Take(frame.Units(48f));
            CardChrome.DrawGold(frame, tellRow);
            var label = "Tell " + trimmed;
            frame.Text.DrawIn(tellRow.Inset(frame.Units(12f)), label,
                new TextStyle(FontRole.BodyStrong, frame.Theme.Palette.WarmAccent));
            if (frame.Input.ConsumeClick(tellRow))
            {
                ParseTellQuery(trimmed, out var name, out var world);
                OpenTell(name, world);
                return content.Height - stack.Remaining.Height;
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

            var row = stack.Take(frame.Units(72f));
            CardChrome.DrawGold(frame, row);
            DrawThreadRow(frame, row.Inset(frame.Units(12f)), thread);
            if (frame.Input.ConsumeClick(row))
            {
                Open(thread.Id);
            }

            shown++;
        }

        if (shown == 0 && nearby.Count == 0)
        {
            DrawEmpty(frame, stack.Take(frame.Units(40f)),
                trimmed.Length > 0
                    ? "Nothing matches."
                    : InboxEmptyCopy());
        }

        return content.Height - stack.Remaining.Height;
    }

    private float DrawThread(in AppletFrame frame)
    {
        talk.MarkRead(openId);
        var thread = talk.Find(openId);
        var inset = frame.Units(12f);
        var area = frame.Content.Inset(new Edges(inset, inset, inset, inset));
        var header = area.TopSlice(frame.Units(36f));
        var replies = thread?.CanSend == true ? frame.Units(36f) : 0f;
        var composer = area.BottomSlice(frame.Units(44f));
        var tray = replies > 0f
            ? new Rect(new Vector2(area.Min.X, composer.Min.Y - replies - frame.Units(6f)),
                new Vector2(area.Max.X, composer.Min.Y - frame.Units(6f)))
            : composer;
        var messages = new Rect(new Vector2(area.Min.X, header.Max.Y + frame.Units(8f)),
            new Vector2(area.Max.X, (replies > 0f ? tray.Min.Y : composer.Min.Y) - frame.Units(8f)));

        DrawHeader(frame, header, thread);
        if (openId.Length == 0)
        {
            return frame.Content.Height;
        }

        DrawLines(frame, messages, thread);
        if (replies > 0f)
        {
            DrawReplies(frame, tray, thread);
        }

        DrawComposer(frame, composer, thread);

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
            DrawEmpty(frame, stack.Take(frame.Units(72f)), "This person is not saved yet.");
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
                DrawFact(frame, stack.Take(frame.Units(48f)), "Number", person.Number);
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
        CardChrome.Draw(frame, composer);
        var inner = composer.Inset(frame.Units(6f));
        var send = inner.RightSlice(frame.Units(56f));
        var field = inner.Inset(new Edges(0f, 0f, send.Width + frame.Units(6f), 0f));
        var canSend = thread?.CanSend == true;
        var hint = ComposerHint(thread);
        if (!canSend)
        {
            frame.Text.DrawIn(field.Inset(frame.Units(6f)), hint,
                new TextStyle(FontRole.Caption, frame.Theme.Palette.InkFaint));
            return;
        }

        draft = frame.TextField.Draw("messages-draft", field, draft, hint, 400, out var submitted);
        var sendInk = draft.Trim().Length > 0 ? frame.Theme.Palette.Accent : frame.Theme.Palette.InkFaint;
        frame.Text.DrawIn(send, "Send", new TextStyle(FontRole.CaptionStrong, sendInk, TextAlign.Center));
        if ((submitted || frame.Input.WasPressed(send) || frame.Input.ConsumeClick(send)) &&
            draft.Trim().Length > 0)
        {
            SendDraft();
        }
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
        talk.Send(openId, "Here · " + zone + " · " + job);
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
        var group = thread?.Kind is TalkKind.Party or TalkKind.Alliance or TalkKind.Linkshell
            or TalkKind.CrossWorldLinkshell or TalkKind.FreeCompany or TalkKind.Novice;
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
            heights[index] = MeasureLine(frame, viewport.Width, lines[index], group);
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
                DrawLine(frame, row, lines[index], group);
            }

            cursor += height + gap;
        }

        frame.Paint.PopClip();
    }

    private static float MeasureLine(in AppletFrame frame, float width, TalkLine line, bool group)
    {
        var bubbleWidth = width * 0.78f;
        var textWidth = MathF.Max(bubbleWidth - frame.Units(16f), 8f);
        var body = frame.Text.MeasureWrapped(line.Body, FontRole.Body, textWidth).Y;
        var sender = group && !line.Mine ? frame.Units(16f) : 0f;
        return MathF.Max(body + frame.Units(16f) + sender, frame.Units(32f));
    }

    private static void DrawLine(in AppletFrame frame, Rect row, TalkLine line, bool group)
    {
        var bubbleWidth = row.Width * 0.78f;
        var bubble = line.Mine ? row.RightSlice(bubbleWidth) : row.LeftSlice(bubbleWidth);
        var radius = frame.Units(12f);
        var fill = line.Mine ? frame.Theme.Palette.Accent with { W = 0.88f } : frame.Theme.Palette.SurfaceRaised;
        var ink = line.Mine ? frame.Theme.Palette.AccentInk : frame.Theme.Palette.Ink;
        var top = bubble;
        if (group && !line.Mine)
        {
            frame.Text.DrawEllipsized(bubble.TopSlice(frame.Units(14f)), line.Sender,
                new TextStyle(FontRole.Caption, frame.Theme.Palette.InkMuted));
            top = bubble.Inset(new Edges(0f, frame.Units(16f), 0f, 0f));
        }

        frame.Paint.Fill(top, fill, radius);
        var copy = top.Inset(frame.Units(8f));
        frame.Paint.PushClip(copy);
        frame.Text.DrawWrapped(copy, line.Body, new TextStyle(FontRole.Body, ink));
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

        var preview = thread.Preview.Length > 0 ? thread.Preview : thread.Subtitle;
        frame.Text.DrawEllipsized(body.Inset(new Edges(0f, frame.Units(22f), thread.Unread > 0 ? frame.Units(28f) : 0f,
            0f)), preview, new TextStyle(FontRole.Caption, frame.Theme.Palette.InkMuted));
        if (thread.Unread > 0)
        {
            var badge = body.RightSlice(frame.Units(22f)).BottomSlice(frame.Units(22f));
            frame.Paint.FillCircle(badge.Center, frame.Units(8f), frame.Theme.Palette.WarmAccent);
            var label = thread.Unread > 9 ? "9+" : thread.Unread.ToString(CultureInfo.InvariantCulture);
            frame.Text.Draw(badge.Center, label,
                new TextStyle(FontRole.Caption, frame.Theme.Palette.SurfaceSunken, TextAlign.Center));
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
            or TalkKind.CrossWorldLinkshell or TalkKind.FreeCompany or TalkKind.Novice),
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
            TalkKind.Pearl => "Pearlgate chats list here. Sending on the gate is not wired yet.",
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