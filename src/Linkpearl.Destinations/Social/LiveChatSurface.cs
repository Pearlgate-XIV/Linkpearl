using Linkpearl.Applets;
using Linkpearl.Chat;
using Linkpearl.Geometry;
using Linkpearl.Input;
using Linkpearl.Painting;
using Linkpearl.Platform;
using Linkpearl.Preferences;
using Linkpearl.Talk;

namespace Linkpearl.Destinations.Social;

internal sealed class LiveChatSurface
{
    private static readonly (string Label, string ThreadId)[] Channels =
    {
        ("Say", TalkIds.LiveSay),
        ("Shout", TalkIds.LiveShout),
        ("Yell", TalkIds.LiveYell),
        ("Party", TalkIds.LiveParty),
    };

    private readonly ITalk talk;
    private readonly DisplayPreferences display;
    private readonly IChatBridge chat;
    private readonly Action<string, string> openTell;
    private string draft = string.Empty;
    private string sendId = TalkIds.LiveSay;
    private float threadOffset;
    private bool stickBottom = true;
    private bool dragging;
    private int seenGeneration = -1;
    private FeedMenu? menu;
    private readonly ChatPick pick = new();

    private readonly IGifDesk gifs;
    private readonly ChatMarks marks;
    private readonly Action<string, string, string>? reportLine;
    private readonly ILifestream? lifestream;

    public LiveChatSurface(ITalk talk, DisplayPreferences display, IChatBridge chat, Action<string, string> openTell,
        IGifDesk gifs, ChatMarks marks, Action<string, string, string>? reportLine = null,
        ILifestream? lifestream = null)
    {
        this.talk = talk;
        this.display = display;
        this.chat = chat;
        this.openTell = openTell;
        this.gifs = gifs;
        this.marks = marks;
        this.reportLine = reportLine;
        this.lifestream = lifestream;
    }

    public bool HasMenu => menu is not null;

    public bool CloseMenu()
    {
        if (menu is null)
        {
            return false;
        }

        menu = null;
        return true;
    }

    public float Compose(in AppletFrame frame)
    {
        talk.MarkRead(TalkIds.Live);
        var thread = talk.Find(TalkIds.Live);
        var partyReady = talk.Find(TalkIds.Party)?.CanSend == true;
        var area = frame.Content.Inset(new Edges(frame.Units(4f), 0f, frame.Units(4f), frame.Units(2f)));
        var sendBar = area.BottomSlice(frame.Units(38f));
        var replyH = marks.ComposerHeight(frame, TalkIds.Live);
        var composer = new Rect(new Vector2(area.Min.X, sendBar.Min.Y - frame.Units(52f) - replyH),
            new Vector2(area.Max.X, sendBar.Min.Y - frame.Units(8f)));
        var filters = new Rect(area.Min, new Vector2(area.Max.X, area.Min.Y + frame.Units(36f)));
        var log = new Rect(new Vector2(area.Min.X, filters.Max.Y + frame.Units(8f)),
            new Vector2(area.Max.X, composer.Min.Y - frame.Units(8f)));
        DrawFilters(frame, filters);
        pick.Begin();
        DrawLines(frame, log, thread, partyReady);
        DrawComposer(frame, composer, thread, partyReady);
        pick.End(frame);
        DrawChannels(frame, sendBar, partyReady);
        DrawLineMenu(frame, frame.Content);
        marks.DrawMenu(frame, frame.Content, frame.Theme.Palette.Ink, frame.Theme.Palette.InkMuted,
            frame.Theme.Palette.Accent, frame.Theme.Palette.SurfaceRaised);

        return frame.Content.Height;
    }

    private void DrawFilters(in AppletFrame frame, Rect row)
    {
        var gap = frame.Units(6f);
        var width = (row.Width - gap * 3f) / 4f;
        DrawFilterChip(frame, Rect.FromSize(row.Min, new Vector2(width, row.Height)), "Say", "SAY",
            display.FeedShowSay, value => display.FeedShowSay = value);
        DrawFilterChip(frame,
            Rect.FromSize(new Vector2(row.Min.X + width + gap, row.Min.Y), new Vector2(width, row.Height)),
            "Shout", "SHOUT", display.FeedShowShout, value => display.FeedShowShout = value);
        DrawFilterChip(frame,
            Rect.FromSize(new Vector2(row.Min.X + (width + gap) * 2f, row.Min.Y), new Vector2(width, row.Height)),
            "Yell", "YELL", display.FeedShowYell, value => display.FeedShowYell = value);
        DrawFilterChip(frame,
            Rect.FromSize(new Vector2(row.Min.X + (width + gap) * 3f, row.Min.Y), new Vector2(width, row.Height)),
            "Party", "PARTY", display.FeedShowParty, value => display.FeedShowParty = value);
    }

    private void DrawFilterChip(in AppletFrame frame, Rect cell, string label, string tag, bool on, Action<bool> set)
    {
        var radius = cell.Height * 0.5f;
        if (on)
        {
            frame.Paint.Fill(cell, ChannelWash(tag), radius);
            frame.Paint.Stroke(cell, ChannelLine(tag), frame.Units(1.4f), radius);
            frame.Text.DrawIn(cell, label,
                new TextStyle(FontRole.CaptionStrong, ChannelLine(tag), TextAlign.Center));
        }
        else
        {
            frame.Paint.Fill(cell, frame.Theme.Palette.SurfaceOverlay with { W = 0.55f }, radius);
            frame.Text.DrawIn(cell, label,
                new TextStyle(FontRole.CaptionStrong, frame.Theme.Palette.InkMuted, TextAlign.Center));
        }

        if (frame.Input.ConsumeClick(cell))
        {
            set(!on);
            stickBottom = true;
            seenGeneration = -1;
        }
    }

    private void DrawChannels(in AppletFrame frame, Rect row, bool partyReady)
    {
        var radius = row.Height * 0.5f;
        frame.Paint.Fill(row, frame.Theme.Palette.SurfaceOverlay with { W = 0.62f }, radius);
        var pad = frame.Units(3f);
        var inner = row.Inset(pad);
        var width = inner.Width / Channels.Length;
        for (var index = 0; index < Channels.Length; index++)
        {
            var cell = Rect.FromSize(new Vector2(inner.Min.X + index * width, inner.Min.Y),
                new Vector2(width, inner.Height));
            var partyChip = Channels[index].ThreadId == TalkIds.LiveParty;
            var waiting = partyChip && !partyReady;
            var active = sendId == Channels[index].ThreadId;
            var tag = TagOf(Channels[index].ThreadId);
            if (active)
            {
                frame.Paint.Fill(cell, ChannelWash(tag), cell.Height * 0.5f);
                frame.Paint.Stroke(cell, ChannelLine(tag), frame.Units(1.4f), cell.Height * 0.5f);
            }

            var ink = waiting && !active
                ? frame.Theme.Palette.InkFaint
                : active
                    ? ChannelLine(tag)
                    : frame.Theme.Palette.InkMuted;
            frame.Text.DrawIn(cell, Channels[index].Label,
                new TextStyle(FontRole.CaptionStrong, ink, TextAlign.Center));
            if (frame.Input.ConsumeClick(cell))
            {
                sendId = Channels[index].ThreadId;
                stickBottom = true;
                seenGeneration = -1;
            }
        }
    }

    private void DrawComposer(in AppletFrame frame, Rect composer, TalkThread? thread, bool partyReady)
    {
        var replyH = marks.ComposerHeight(frame, TalkIds.Live);
        if (replyH > 0f && marks.DrawReply(frame, composer.TopSlice(replyH), TalkIds.Live, frame.Theme.Palette.Ink,
                frame.Theme.Palette.InkMuted, frame.Theme.Palette.SurfaceRaised))
        {
            composer = composer.Inset(new Edges(0f, replyH + frame.Units(4f), 0f, 0f));
        }

        var live = frame.TextField.Owns("feed-draft");
        var radius = composer.Height * 0.5f;
        frame.Paint.Fill(composer, frame.Theme.Palette.SurfaceOverlay with { W = live ? 0.88f : 0.72f }, radius);
        if (live)
        {
            frame.Paint.Stroke(composer, frame.Theme.Palette.WarmAccent with { W = 0.55f }, frame.Units(1.4f),
                radius);
        }
        var inner = composer.Inset(new Edges(frame.Units(14f), frame.Units(4f), frame.Units(6f), frame.Units(4f)));
        var send = inner.RightSlice(frame.Units(52f));
        var field = new Rect(inner.Min, new Vector2(send.Min.X - frame.Units(8f), inner.Max.Y));
        var partyMode = sendId == TalkIds.LiveParty;
        var canSend = thread?.CanSend == true && (!partyMode || partyReady);
        if (!canSend)
        {
            var blocked = partyMode
                ? "Join a party to talk here."
                : "Chat is not available right now.";
            frame.Text.DrawIn(field, blocked,
                new TextStyle(FontRole.Caption, frame.Theme.Palette.InkFaint));
            return;
        }

        draft = frame.TextField.Draw("feed-draft", field, draft, ChannelHint(), 400, out var submitted, true);
        var ready = draft.Trim().Length > 0;
        var sendPad = send.Inset(frame.Units(4f));
        var sendTag = TagOf(sendId);
        if (ready)
        {
            frame.Paint.Fill(sendPad, ChannelWash(sendTag), sendPad.Height * 0.5f);
            frame.Paint.Stroke(sendPad, ChannelLine(sendTag), frame.Units(1.4f), sendPad.Height * 0.5f);
            frame.Text.DrawIn(sendPad, "Send",
                new TextStyle(FontRole.CaptionStrong, ChannelLine(sendTag), TextAlign.Center));
        }
        else
        {
            frame.Text.DrawIn(sendPad, "Send",
                new TextStyle(FontRole.CaptionStrong, frame.Theme.Palette.InkFaint, TextAlign.Center));
        }

        if ((submitted || frame.Input.WasPressed(send) || frame.Input.ConsumeClick(send)) && ready)
        {
            var outgoing = draft.Trim();
            draft = string.Empty;
            var sent = marks.Seal(TalkIds.Live, outgoing);
            talk.Send(sendId, sent);
            var lines = talk.Lines(TalkIds.Live);
            if (lines.Count > 0)
            {
                var last = lines[^1];
                marks.CatchSent(TalkIds.Live, "me", last.Mine ? last.At.ToString("O") : string.Empty, sent);
            }
            else
            {
                marks.CatchSent(TalkIds.Live, "me", string.Empty, sent);
            }

            stickBottom = true;
            frame.TextField.Focus("feed-draft");
        }
    }

    private string ChannelHint()
    {
        for (var index = 0; index < Channels.Length; index++)
        {
            if (Channels[index].ThreadId == sendId)
            {
                return Channels[index].Label + " · /wave or :dance:";
            }
        }

        return "Say";
    }

    private void DrawLines(in AppletFrame frame, Rect viewport, TalkThread? thread, bool partyReady)
    {
        if (viewport.Height <= 0f)
        {
            return;
        }

        var lines = VisibleLines(talk.Lines(TalkIds.Live));
        if (lines.Count == 0)
        {
            DrawEmpty(frame, viewport, thread, partyReady);
            return;
        }

        var gap = frame.Units(8f);
        var heights = new float[lines.Count];
        var total = 0f;
        for (var index = 0; index < lines.Count; index++)
        {
            heights[index] = MeasureLine(frame, viewport.Width, lines[index]);
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

        var hovering = menu is null && frame.Input.IsHovering(viewport);
        if (hovering)
        {
            var wheel = frame.Input.ScrollDelta;
            if (MathF.Abs(wheel) > 0.01f)
            {
                stickBottom = false;
                threadOffset = Scalar.Clamp(threadOffset - wheel * 48f * frame.Scale, 0f, maxOffset);
            }
        }

        if (pick.Busy)
        {
            dragging = false;
        }

        if (menu is null && !pick.Busy && (frame.Input.PressedInside(viewport) || dragging))
        {
            if (frame.Input.IsHeld())
            {
                var delta = frame.Input.PointerDelta.Y;
                if (dragging || MathF.Abs(delta) > 2f)
                {
                    dragging = true;
                    stickBottom = false;
                    threadOffset = Scalar.Clamp(threadOffset - delta, 0f, maxOffset);
                    frame.Input.Claim(viewport);
                }
            }
            else
            {
                dragging = false;
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
        try
        {
            var cursor = viewport.Min.Y - threadOffset;
            for (var index = 0; index < lines.Count; index++)
            {
                var height = heights[index];
                var row = new Rect(new Vector2(viewport.Min.X, cursor),
                    new Vector2(viewport.Max.X, cursor + height));
                if (row.Max.Y >= viewport.Min.Y && row.Min.Y <= viewport.Max.Y)
                {
                    DrawLine(frame, row, lines[index]);
                }

                cursor += height + gap;
            }
        }
        finally
        {
            frame.Paint.PopClip();
        }
    }

    private void DrawEmpty(in AppletFrame frame, Rect viewport, TalkThread? thread, bool partyReady)
    {
        string copy;
        if (!display.FeedShowSay && !display.FeedShowShout && !display.FeedShowYell && !display.FeedShowParty)
        {
            copy = "Turn a channel on to see chat here.";
        }
        else if (sendId == TalkIds.LiveParty && !partyReady)
        {
            copy = "Join a party to talk here.";
        }
        else
        {
            var zone = thread?.Subtitle ?? string.Empty;
            copy = zone.Length > 0
                ? "Live chat stays here until you log off · " + zone
                : "Live chat stays here until you log off.";
        }

        frame.Text.DrawIn(viewport, copy,
            new TextStyle(FontRole.Body, frame.Theme.Palette.InkMuted, TextAlign.Center));
    }

    private List<TalkLine> VisibleLines(IReadOnlyList<TalkLine> source)
    {
        var list = new List<TalkLine>(source.Count);
        for (var index = 0; index < source.Count; index++)
        {
            var line = source[index];
            if (display.FeedShows(line.Tag))
            {
                list.Add(line);
            }
        }

        return list;
    }

    private float MeasureLine(in AppletFrame frame, float width, TalkLine line) =>
        ChatBits.NamedRowHeight(frame, width, line.Body,
            marks.Cite(LineKey(line), TalkIds.Live, line.Body, line.Mine)) +
        marks.Band(frame, LineKey(line));

    private void DrawLine(in AppletFrame frame, Rect row, TalkLine line)
    {
        var bubbleWidth = row.Width * 0.82f;
        var bubble = line.Mine ? row.RightSlice(bubbleWidth) : row.LeftSlice(bubbleWidth);
        var radius = frame.Units(16f);
        var lineColor = ChannelLine(line.Tag);
        var who = line.Mine ? "You" : line.Sender.Length > 0 ? line.Sender : "Them";
        if (line.Tag.Length > 0)
        {
            who += " · " + line.Tag;
        }

        var nameRow = bubble.TopSlice(frame.Units(14f));
        frame.Text.DrawEllipsized(nameRow, who,
            new TextStyle(FontRole.Caption, lineColor,
                line.Mine ? TextAlign.Right : TextAlign.Left));
        var top = bubble.Inset(new Edges(0f, frame.Units(16f), 0f, 0f));
        frame.Paint.Fill(top, ChannelWash(line.Tag), radius);
        frame.Paint.Stroke(top, lineColor, frame.Units(1.5f), radius);
        var key = LineKey(line);
        var cite = marks.Cite(key, TalkIds.Live, line.Body ?? string.Empty, line.Mine);
        var band = marks.Band(frame, key);
        var copy = top.Inset(new Edges(frame.Units(10f), frame.Units(8f), frame.Units(10f),
            frame.Units(8f) + band));
        frame.Paint.PushClip(copy);
        try
        {
            ChatBits.Draw(frame, copy, line.Body ?? string.Empty, lineColor, lineColor with { W = 0.72f }, lifestream, gifs,
                cite);
        }
        finally
        {
            frame.Paint.PopClip();
        }
        if (ChatBits.TryPlain(frame, copy, line.Body ?? string.Empty, cite, out var face, out var text))
        {
            pick.Face(frame, face, key, text, lineColor);
        }
        if (band > 0f)
        {
            marks.DrawBand(frame, top.BottomSlice(band).Inset(new Edges(frame.Units(8f), 0f)), key);
        }

        if (menu is null && !marks.Busy && frame.Input.ConsumeClick(bubble, PointerButton.Secondary))
        {
            menu = new FeedMenu(line.Sender, line.World, frame.Input.Cursor, key, line.Body ?? string.Empty, line.Mine);
        }
    }

    private static string LineKey(TalkLine line) =>
        ChatMarks.Key(TalkIds.Live, line.Mine ? "me" : line.Sender, line.At.ToString("O"), line.Body);

    private void DrawLineMenu(in AppletFrame frame, Rect bounds)
    {
        if (menu is not { } open)
        {
            return;
        }

        var labels = open.Mine || open.Name.Length == 0
            ? new[] { "Copy", "React", "Reply" }
            : reportLine is null
                ? new[] { "Copy", "React", "Reply", "Target", "Send tell", "Invite to party", "Add friend" }
                : new[] { "Copy", "React", "Reply", "Target", "Send tell", "Invite to party", "Add friend", "Report" };
        if (ChatLinks.First(ChatPick.Plain(open.Body), out _))
        {
            var extra = new string[labels.Length + 1];
            extra[0] = labels[0];
            extra[1] = "Open link";
            Array.Copy(labels, 1, extra, 2, labels.Length - 1);
            labels = extra;
        }
        var width = frame.Units(176f);
        var rowH = frame.Units(34f);
        var height = rowH * labels.Length + frame.Units(8f);
        var left = Math.Clamp(open.At.X, bounds.Min.X, bounds.Max.X - width);
        var top = Math.Clamp(open.At.Y, bounds.Min.Y, bounds.Max.Y - height);
        var box = Rect.FromSize(new Vector2(left, top), new Vector2(width, height));
        var gold = frame.Theme.Palette.WarmAccent;
        var radius = frame.Units(10f);
        frame.Paint.Fill(box, frame.Theme.Palette.SurfaceRaised with { W = 0.98f }, radius);
        frame.Paint.Stroke(box, gold with { W = 0.55f }, frame.Theme.Metrics.Hairline, radius);
        for (var index = 0; index < labels.Length; index++)
        {
            var row = Rect.FromSize(box.Min + new Vector2(0f, frame.Units(4f) + index * rowH),
                new Vector2(width, rowH)).Inset(new Edges(frame.Units(4f), 0f));
            var hover = frame.Input.IsHovering(row);
            if (hover)
            {
                frame.Paint.Fill(row, gold with { W = 0.20f }, frame.Units(8f));
            }

            frame.Text.DrawIn(row.Inset(new Edges(frame.Units(10f), 0f)), labels[index],
                new TextStyle(FontRole.CaptionStrong, hover ? gold : frame.Theme.Palette.Ink));
            if (frame.Input.ConsumeClick(row))
            {
                RunLineMenu(frame, labels[index], open);
                return;
            }
        }

        frame.Input.ConsumeClick(box);
        frame.Input.ConsumeClick(box, PointerButton.Secondary);
        frame.Input.Claim(box);
        if (frame.Input.ConsumeClick(bounds) || frame.Input.ConsumeClick(bounds, PointerButton.Secondary))
        {
            menu = null;
        }
    }

    private void RunLineMenu(in AppletFrame frame, string label, FeedMenu open)
    {
        menu = null;
        if (label == "Copy")
        {
            pick.Copy(frame, open.Key, open.Body);
            return;
        }

        if (label == "Open link")
        {
            pick.Open(open.Body);
            return;
        }

        if (label == "React")
        {
            marks.BeginReact(open.Key);
            return;
        }

        if (label == "Reply")
        {
            marks.BeginReply(TalkIds.Live, open.Mine ? "You" : open.Name, open.Body);
            return;
        }

        if (label == "Target")
        {
            chat.TargetPlayer(open.Name, open.World);
            return;
        }

        if (label == "Send tell")
        {
            openTell(open.Name, open.World);
            return;
        }

        if (label == "Invite to party")
        {
            chat.InviteToParty(open.Name, open.World);
            return;
        }

        if (label == "Report")
        {
            reportLine?.Invoke(open.Name, open.World, open.Body);
            return;
        }

        talk.RequestFriend(open.Name, open.World);
    }

    private readonly record struct FeedMenu(string Name, string World, Vector2 At, string Key, string Body, bool Mine);

    private static string TagOf(string threadId) => threadId switch
    {
        TalkIds.LiveShout => "SHOUT",
        TalkIds.LiveYell => "YELL",
        TalkIds.LiveParty => "PARTY",
        _ => "SAY",
    };

    private static Vector4 ChannelLine(string tag) => tag switch
    {
        "SHOUT" => new Vector4(0.96f, 0.48f, 0.14f, 1f),
        "YELL" => new Vector4(0.98f, 0.84f, 0.16f, 1f),
        "PARTY" => new Vector4(0.35f, 0.62f, 1f, 1f),
        "EMOTE" => new Vector4(0.86f, 0.62f, 1f, 1f),
        _ => new Vector4(1f, 1f, 1f, 1f),
    };

    private static Vector4 ChannelWash(string tag)
    {
        var line = ChannelLine(tag);
        return line with { W = 0.10f };
    }
}
