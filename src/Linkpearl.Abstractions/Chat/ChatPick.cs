using Linkpearl.Applets;
using Linkpearl.Emoji;
using Linkpearl.Geometry;
using Linkpearl.Input;
using Linkpearl.Painting;

namespace Linkpearl.Chat;

public sealed class ChatPick
{
    private readonly List<TextRun> runs = [];
    private readonly List<ChatLink> links = [];
    private string key = string.Empty;
    private string body = string.Empty;
    private int from;
    private int to;
    private string pressKey = string.Empty;
    private int pressIndex;
    private int pressLen = 1;
    private Vector2 pressAt;
    private float hold;
    private bool blocked;
    private bool saw;
    private Rect chip;
    private bool chipOn;

    public bool Busy { get; private set; }

    public static string Plain(string body)
    {
        var bit = ChatBits.Read(body);
        return bit.Kind == ChatBitKind.Text ? bit.Body : ChatBits.Preview(body);
    }

    public void Begin()
    {
        saw = false;
        chipOn = false;
    }

    public void Face(in AppletFrame frame, Rect area, string lineKey, string text, Vector4 ink)
    {
        if (area.Width < 1f || area.Height < 1f || string.IsNullOrEmpty(text))
        {
            return;
        }

        runs.Clear();
        EmojiText.Collect(frame, area, text, runs);
        ChatLinks.Find(text, links);
        var linkInk = new Vector4(0.45f, 0.82f, 1f, 1f);
        if (string.Equals(key, lineKey, StringComparison.Ordinal) && to > from)
        {
            PaintRange(frame, frame.Theme.Palette.Accent with { W = 0.34f });
            PlaceChip(frame, area);
        }

        PaintLinks(frame, text, linkInk);

        Track(frame, area, lineKey, text);
        _ = ink;
    }

    public void End(in AppletFrame frame)
    {
        if (chipOn)
        {
            DrawChip(frame);
        }

        if (!frame.TextField.TakingKeys && frame.Input.CopyChord() && to > from && body.Length > 0)
        {
            Copy(frame, key, body);
        }

        if (frame.Input.EscapePressed() && (to > from || Busy))
        {
            Clear();
            return;
        }

        if (!Busy && frame.Input.PointerReleased() && !saw && !chipOn)
        {
            from = 0;
            to = 0;
        }

        if (!frame.Input.IsHeld())
        {
            Busy = false;
            blocked = false;
            pressKey = string.Empty;
        }
    }

    public bool Copy(in AppletFrame frame, string lineKey, string source)
    {
        var clip = string.Equals(key, lineKey, StringComparison.Ordinal) && to > from
            ? Slice()
            : Plain(source);
        if (clip.Length == 0)
        {
            return false;
        }

        frame.TextField.PutClipboard(clip);
        return true;
    }

    public bool Open(string source)
    {
        if (to > from && ChatLinks.First(Slice(), out var picked))
        {
            return ChatWeb.Open(picked.Href);
        }

        return ChatLinks.First(Plain(source), out var link) && ChatWeb.Open(link.Href);
    }

    private void Track(in AppletFrame frame, Rect area, string lineKey, string text)
    {
        var hovering = frame.Input.IsHovering(area);
        if (hovering || (Busy && string.Equals(pressKey, lineKey, StringComparison.Ordinal)))
        {
            saw = true;
        }

        if (frame.Input.WasPressed(area))
        {
            pressKey = lineKey;
            pressAt = frame.Input.Pointer;
            Hit(frame.Input.Pointer, out pressIndex, out pressLen);
            hold = 0f;
            Busy = false;
            blocked = false;
            body = text;
            key = lineKey;
        }

        if (pressKey.Length == 0 || !string.Equals(pressKey, lineKey, StringComparison.Ordinal))
        {
            TryTapLink(frame, area, text);
            return;
        }

        if (frame.Input.IsHeld())
        {
            hold += frame.DeltaSeconds;
            var delta = frame.Input.Pointer - pressAt;
            if (!Busy && !blocked)
            {
                if (MathF.Abs(delta.Y) > 8f && MathF.Abs(delta.Y) > MathF.Abs(delta.X) * 1.15f)
                {
                    blocked = true;
                }
                else if (hold > 0.32f ||
                         (MathF.Abs(delta.X) > 6f && MathF.Abs(delta.X) >= MathF.Abs(delta.Y)))
                {
                    Busy = true;
                    from = pressIndex;
                    to = pressIndex + pressLen;
                }
            }

            if (Busy)
            {
                Hit(frame.Input.Pointer, out var hit, out var hitLen);
                Span(pressIndex, pressLen, hit, hitLen);
                body = text;
                key = lineKey;
                frame.Input.Claim(area);
            }

            return;
        }

        TryTapLink(frame, area, text);
    }

    private void TryTapLink(in AppletFrame frame, Rect area, string text)
    {
        if (Busy || blocked || !frame.Input.WasClicked(area))
        {
            return;
        }

        Hit(frame.Input.Pointer, out var index, out _);
        if (ChatLinks.At(text, index, links, out var link))
        {
            ChatWeb.Open(link.Href);
        }
    }

    private void Span(int start, int startLen, int hit, int hitLen)
    {
        if (start <= hit)
        {
            from = start;
            to = hit + Math.Max(1, hitLen);
        }
        else
        {
            from = hit;
            to = start + Math.Max(1, startLen);
        }

        from = Math.Clamp(from, 0, body.Length);
        to = Math.Clamp(to, from, body.Length);
    }

    private void Hit(Vector2 pointer, out int index, out int length)
    {
        index = 0;
        length = 1;
        if (runs.Count == 0)
        {
            return;
        }

        var best = 0;
        var bestDist = float.MaxValue;
        for (var i = 0; i < runs.Count; i++)
        {
            var run = runs[i];
            if (run.Box.Contains(pointer))
            {
                index = run.Start;
                length = Math.Max(1, run.Length);
                return;
            }

            var cx = Math.Clamp(pointer.X, run.Box.Min.X, run.Box.Max.X);
            var cy = Math.Clamp(pointer.Y, run.Box.Min.Y, run.Box.Max.Y);
            var dx = pointer.X - cx;
            var dy = pointer.Y - cy;
            var dist = dx * dx + dy * dy;
            if (dist < bestDist)
            {
                bestDist = dist;
                best = i;
            }
        }

        index = runs[best].Start;
        length = Math.Max(1, runs[best].Length);
    }

    private void PaintRange(in AppletFrame frame, Vector4 wash)
    {
        for (var i = 0; i < runs.Count; i++)
        {
            var run = runs[i];
            if (run.Start + run.Length <= from || run.Start >= to)
            {
                continue;
            }

            frame.Paint.Fill(run.Box, wash, 2f);
        }
    }

    private void PaintLinks(in AppletFrame frame, string text, Vector4 linkInk)
    {
        if (links.Count == 0)
        {
            return;
        }

        var style = EmojiText.Style(text, linkInk);
        for (var i = 0; i < runs.Count; i++)
        {
            var run = runs[i];
            if (!Covered(run.Start, run.Length))
            {
                continue;
            }

            frame.Paint.Line(new Vector2(run.Box.Min.X, run.Box.Max.Y - 1.2f),
                new Vector2(run.Box.Max.X, run.Box.Max.Y - 1.2f), linkInk, 1.2f);
            frame.Text.Draw(run.Box.Min, text.AsSpan(run.Start, run.Length),
                new TextStyle(style.Role, linkInk, TextAlign.Left, style.LineSpacing, style.Scale));
        }
    }

    private bool Covered(int start, int length)
    {
        var end = start + length;
        for (var i = 0; i < links.Count; i++)
        {
            var link = links[i];
            if (start < link.Start + link.Length && end > link.Start)
            {
                return true;
            }
        }

        return false;
    }

    private void PlaceChip(in AppletFrame frame, Rect area)
    {
        for (var i = 0; i < runs.Count; i++)
        {
            var run = runs[i];
            if (run.Start + run.Length <= from || run.Start >= to)
            {
                continue;
            }

            var width = frame.Units(52f);
            var height = frame.Units(22f);
            var left = Math.Clamp(run.Box.Min.X, area.Min.X, Math.Max(area.Min.X, area.Max.X - width));
            var top = run.Box.Min.Y - height - frame.Units(4f);
            if (top < area.Min.Y - frame.Units(18f))
            {
                top = run.Box.Max.Y + frame.Units(4f);
            }

            chip = Rect.FromSize(new Vector2(left, top), new Vector2(width, height));
            chipOn = true;
            saw = true;
            return;
        }
    }

    private void DrawChip(in AppletFrame frame)
    {
        var gold = frame.Theme.Palette.WarmAccent;
        frame.Paint.Fill(chip, frame.Theme.Palette.SurfaceRaised with { W = 0.96f }, chip.Height * 0.5f);
        frame.Paint.Stroke(chip, gold with { W = 0.7f }, frame.Theme.Metrics.Hairline, chip.Height * 0.5f);
        frame.Text.DrawIn(chip, "Copy",
            new TextStyle(FontRole.CaptionStrong, gold, TextAlign.Center));
        if (frame.Input.ConsumeClick(chip) || frame.Input.WasClicked(chip))
        {
            Copy(frame, key, body);
            saw = true;
        }
    }

    private string Slice()
    {
        var lo = Math.Clamp(from, 0, body.Length);
        var hi = Math.Clamp(to, lo, body.Length);
        return hi > lo ? body[lo..hi] : string.Empty;
    }

    private void Clear()
    {
        from = 0;
        to = 0;
        Busy = false;
        blocked = false;
        pressKey = string.Empty;
        key = string.Empty;
        body = string.Empty;
    }
}
