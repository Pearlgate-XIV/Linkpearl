using Linkpearl.Applets;
using Linkpearl.Geometry;
using Linkpearl.Painting;
using Linkpearl.Preferences;
using Linkpearl.Talk;

namespace Linkpearl.Destinations.Social;

internal sealed class LiveChatSurface
{
    private static readonly (string Label, string ThreadId)[] Channels =
    {
        ("SAY", TalkIds.LiveSay),
        ("SHOUT", TalkIds.LiveShout),
        ("YELL", TalkIds.LiveYell),
        ("PARTY", TalkIds.LiveParty),
    };

    private readonly ITalk talk;
    private readonly DisplayPreferences display;
    private string draft = string.Empty;
    private string sendId = TalkIds.LiveSay;
    private float threadOffset;
    private bool stickBottom = true;
    private bool dragging;
    private int seenGeneration = -1;

    public LiveChatSurface(ITalk talk, DisplayPreferences display)
    {
        this.talk = talk;
        this.display = display;
    }

    public float Compose(in AppletFrame frame)
    {
        talk.MarkRead(TalkIds.Live);
        var thread = talk.Find(TalkIds.Live);
        var partyReady = talk.Find(TalkIds.Party)?.CanSend == true;
        var inset = frame.Units(12f);
        var area = frame.Content.Inset(inset);
        var chips = area.BottomSlice(frame.Units(32f));
        var composer = new Rect(new Vector2(area.Min.X, chips.Min.Y - frame.Units(50f)),
            new Vector2(area.Max.X, chips.Min.Y - frame.Units(6f)));
        var pane = new Rect(area.Min, new Vector2(area.Max.X, composer.Min.Y - frame.Units(8f)));

        frame.Paint.Fill(pane, frame.Theme.Palette.SurfaceSunken with { W = 0.90f }, frame.Units(14f));
        var filters = pane.Inset(new Edges(frame.Units(8f), frame.Units(8f), frame.Units(8f), 0f))
            .TopSlice(frame.Units(26f));
        var log = new Rect(new Vector2(pane.Min.X + frame.Units(12f), filters.Max.Y + frame.Units(6f)),
            new Vector2(pane.Max.X - frame.Units(12f), pane.Max.Y - frame.Units(8f)));
        DrawFilters(frame, filters);
        DrawLines(frame, log, thread, partyReady);
        DrawComposer(frame, composer, thread, partyReady);
        DrawChannels(frame, chips, partyReady);

        return frame.Content.Height;
    }

    private void DrawFilters(in AppletFrame frame, Rect row)
    {
        var gap = frame.Units(5f);
        var width = (row.Width - gap * 3f) / 4f;
        DrawFilter(frame, Rect.FromSize(row.Min, new Vector2(width, row.Height)), "SAY", display.FeedShowSay,
            value => display.FeedShowSay = value);
        DrawFilter(frame,
            Rect.FromSize(new Vector2(row.Min.X + width + gap, row.Min.Y), new Vector2(width, row.Height)),
            "SHOUT", display.FeedShowShout, value => display.FeedShowShout = value);
        DrawFilter(frame,
            Rect.FromSize(new Vector2(row.Min.X + (width + gap) * 2f, row.Min.Y), new Vector2(width, row.Height)),
            "YELL", display.FeedShowYell, value => display.FeedShowYell = value);
        DrawFilter(frame,
            Rect.FromSize(new Vector2(row.Min.X + (width + gap) * 3f, row.Min.Y), new Vector2(width, row.Height)),
            "PARTY", display.FeedShowParty, value => display.FeedShowParty = value);
    }

    private void DrawFilter(in AppletFrame frame, Rect cell, string label, bool on, Action<bool> set)
    {
        var gold = frame.Theme.Palette.WarmAccent;
        var fill = on ? gold with { W = 0.30f } : frame.Theme.Palette.SurfaceOverlay with { W = 0.40f };
        var ink = on ? gold : frame.Theme.Palette.InkFaint;
        frame.Paint.Fill(cell, fill, cell.Height * 0.5f);
        frame.Paint.Stroke(cell, gold with { W = on ? 0.62f : 0.16f }, frame.Theme.Metrics.Hairline,
            cell.Height * 0.5f);
        frame.Text.DrawIn(cell, label, new TextStyle(FontRole.CaptionStrong, ink, TextAlign.Center));
        if (frame.Input.ConsumeClick(cell))
        {
            set(!on);
            stickBottom = true;
            seenGeneration = -1;
        }
    }

    private void DrawChannels(in AppletFrame frame, Rect row, bool partyReady)
    {
        var gap = frame.Units(6f);
        var width = (row.Width - gap * (Channels.Length - 1)) / Channels.Length;
        for (var index = 0; index < Channels.Length; index++)
        {
            var cell = Rect.FromSize(new Vector2(row.Min.X + index * (width + gap), row.Min.Y),
                new Vector2(width, row.Height));
            var partyChip = Channels[index].ThreadId == TalkIds.LiveParty;
            var waiting = partyChip && !partyReady;
            var active = sendId == Channels[index].ThreadId;
            var gold = frame.Theme.Palette.WarmAccent;
            var fill = active ? gold with { W = 0.28f } : frame.Theme.Palette.SurfaceOverlay;
            var ink = waiting && !active
                ? frame.Theme.Palette.InkFaint
                : active
                    ? gold
                    : frame.Theme.Palette.InkMuted;
            frame.Paint.Fill(cell, fill, cell.Height * 0.5f);
            frame.Paint.Stroke(cell, gold with { W = active ? 0.70f : waiting ? 0.14f : 0.28f },
                frame.Theme.Metrics.Hairline, cell.Height * 0.5f);
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
        var glass = frame.Theme.Palette.SurfaceOverlay with { W = 0.34f };
        frame.Paint.Fill(composer, glass, frame.Units(12f));
        frame.Paint.Stroke(composer, frame.Theme.Palette.WarmAccent with { W = 0.22f }, frame.Theme.Metrics.Hairline,
            frame.Units(12f));
        var inner = composer.Inset(frame.Units(6f));
        var send = inner.RightSlice(frame.Units(56f));
        var field = new Rect(inner.Min, new Vector2(send.Min.X - frame.Units(6f), inner.Max.Y));
        var partyMode = sendId == TalkIds.LiveParty;
        var canSend = thread?.CanSend == true && (!partyMode || partyReady);
        if (!canSend)
        {
            var blocked = partyMode
                ? "Join a party to talk here."
                : "Chat is not available right now.";
            frame.Text.DrawIn(field.Inset(frame.Units(6f)), blocked,
                new TextStyle(FontRole.Caption, frame.Theme.Palette.InkFaint));
            return;
        }

        draft = frame.TextField.Draw("feed-draft", field, draft, ChannelHint(), 400, out var submitted, true);
        var sendInk = draft.Trim().Length > 0 ? frame.Theme.Palette.Accent : frame.Theme.Palette.InkFaint;
        frame.Text.DrawIn(send, "Send", new TextStyle(FontRole.CaptionStrong, sendInk, TextAlign.Center));
        if ((submitted || frame.Input.WasPressed(send) || frame.Input.ConsumeClick(send)) &&
            draft.Trim().Length > 0)
        {
            var outgoing = draft.Trim();
            draft = string.Empty;
            talk.Send(sendId, outgoing);
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
                return Channels[index].Label;
            }
        }

        return "SAY";
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

        var hovering = frame.Input.IsHovering(viewport);
        if (hovering)
        {
            var wheel = frame.Input.ScrollDelta;
            if (MathF.Abs(wheel) > 0.01f)
            {
                stickBottom = false;
                threadOffset = Scalar.Clamp(threadOffset - wheel * 48f * frame.Scale, 0f, maxOffset);
            }
        }

        if (frame.Input.PressedInside(viewport) || dragging)
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

        frame.Paint.PopClip();
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

    private static float MeasureLine(in AppletFrame frame, float width, TalkLine line)
    {
        var bubbleWidth = width * 0.78f;
        var textWidth = MathF.Max(bubbleWidth - frame.Units(16f), 8f);
        var body = frame.Text.MeasureWrapped(line.Body, FontRole.Body, textWidth).Y;
        return MathF.Max(body + frame.Units(32f), frame.Units(44f));
    }

    private static void DrawLine(in AppletFrame frame, Rect row, TalkLine line)
    {
        var bubbleWidth = row.Width * 0.78f;
        var bubble = line.Mine ? row.RightSlice(bubbleWidth) : row.LeftSlice(bubbleWidth);
        var radius = frame.Units(12f);
        var fill = line.Mine
            ? frame.Theme.Palette.Accent with { W = 0.38f }
            : frame.Theme.Palette.SurfaceRaised with { W = 0.36f };
        var ink = line.Mine ? frame.Theme.Palette.AccentInk : frame.Theme.Palette.Ink;
        var who = line.Mine ? "ME" : line.Sender.Length > 0 ? line.Sender : "Them";
        if (line.Tag.Length > 0)
        {
            who += " · " + line.Tag;
        }

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
        frame.Text.DrawWrapped(copy, line.Body, new TextStyle(FontRole.Body, ink));
        frame.Paint.PopClip();
    }
}
