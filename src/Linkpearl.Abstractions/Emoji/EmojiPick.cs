using Linkpearl.Applets;
using Linkpearl.Geometry;
using Linkpearl.Layout;
using Linkpearl.Painting;

namespace Linkpearl.Emoji;

public sealed class EmojiPick
{
    private readonly EmojiRecent recent = new();
    private string query = string.Empty;
    private EmojiGroup group = EmojiGroup.Faces;
    private float scroll;
    private string holdGlyph = string.Empty;
    private float holdFor;
    private string? variantOf;
    private bool skip;
    private int hunt;

    public void Open()
    {
        skip = true;
        variantOf = null;
        holdGlyph = string.Empty;
        holdFor = 0f;
    }

    public void Draw(in AppletFrame frame, Rect area, Vector4 ink, Vector4 mute, Vector4 accent, Vector4 card,
        Action<string> insert)
    {
        recent.Bind(frame.Paths);
        if (skip)
        {
            skip = false;
            frame.Input.Claim(area);
            PaintChrome(frame, area, card);
            return;
        }

        PaintChrome(frame, area, card);
        var inner = area.Inset(new Edges(frame.Units(8f), frame.Units(8f), frame.Units(8f), frame.Units(6f)));
        var search = inner.TopSlice(frame.Units(32f));
        var tabs = inner.BottomSlice(frame.Units(34f));
        var board = new Rect(new Vector2(inner.Min.X, search.Max.Y + frame.Units(6f)),
            new Vector2(inner.Max.X, tabs.Min.Y - frame.Units(4f)));
        DrawSearch(frame, search, mute, ink);
        DrawTabs(frame, tabs, ink, mute, accent);
        frame.Input.Claim(tabs);
        DrawBoard(frame, board, ink, mute, accent, insert);
        if (variantOf is { } open)
        {
            DrawVariants(frame, board, open, card, ink, mute, insert);
        }

        frame.Input.Claim(area);
    }

    private static void PaintChrome(in AppletFrame frame, Rect area, Vector4 card) =>
        frame.Paint.Fill(area, card, frame.Units(16f));

    private void DrawSearch(in AppletFrame frame, Rect area, Vector4 mute, Vector4 ink)
    {
        frame.Paint.Fill(area, mute with { W = 0.14f }, frame.Units(12f));
        var next = frame.TextField.Draw("emoji-hunt-" + hunt,
            area.Inset(new Edges(frame.Units(10f), frame.Units(4f))), query, "Search emoji");
        if (!string.Equals(next, query, StringComparison.Ordinal))
        {
            query = next;
            scroll = 0f;
        }

        _ = ink;
    }

    private void DrawTabs(in AppletFrame frame, Rect area, Vector4 ink, Vector4 mute, Vector4 accent)
    {
        frame.Paint.Fill(area.TopSlice(frame.Units(1f)), mute with { W = 0.22f });
        var cellW = area.Width / EmojiShelf.Groups.Length;
        for (var index = 0; index < EmojiShelf.Groups.Length; index++)
        {
            var pack = EmojiShelf.Groups[index];
            var cell = Rect.FromSize(new Vector2(area.Min.X + cellW * index, area.Min.Y),
                new Vector2(cellW, area.Height));
            var on = group == pack.Group && query.Trim().Length == 0;
            if (on)
            {
                frame.Paint.Fill(cell.Inset(new Edges(frame.Units(2f), frame.Units(4f))), accent with { W = 0.18f },
                    frame.Units(8f));
            }

            EmojiArt.DrawOrMark(frame, cell.Inset(frame.Units(3f)), pack.Mark, on ? ink : mute);
            if (on)
            {
                frame.Paint.Fill(cell.BottomSlice(frame.Units(2f)).Inset(new Edges(frame.Units(4f), 0f)), accent,
                    frame.Units(1f));
            }

            if (frame.Input.WasPressed(cell) || frame.Input.ConsumeClick(cell))
            {
                if (group != pack.Group || query.Length > 0)
                {
                    hunt++;
                }

                group = pack.Group;
                query = string.Empty;
                scroll = 0f;
                variantOf = null;
                frame.Input.Claim(area);
            }
        }
    }

    private void DrawBoard(in AppletFrame frame, Rect area, Vector4 ink, Vector4 mute, Vector4 accent,
        Action<string> insert)
    {
        var marks = Shown();
        if (marks.Count == 0)
        {
            frame.Text.DrawIn(area, query.Trim().Length > 0 ? "No matching emoji." : "No recent emoji yet.",
                new TextStyle(FontRole.Caption, mute, TextAlign.Center));
            return;
        }

        if (query.Trim().Length == 0 && group != EmojiGroup.Recent)
        {
            frame.Text.DrawIn(area.TopSlice(frame.Units(14f)), TitleOf(group),
                new TextStyle(FontRole.CaptionStrong, mute));
            area = area.Inset(new Edges(0f, frame.Units(18f), 0f, 0f));
        }
        else if (query.Trim().Length == 0)
        {
            frame.Text.DrawIn(area.TopSlice(frame.Units(14f)), "RECENT",
                new TextStyle(FontRole.CaptionStrong, mute));
            area = area.Inset(new Edges(0f, frame.Units(18f), 0f, 0f));
        }

        var cols = Math.Clamp((int)(area.Width / frame.Units(36f)), 6, 9);
        var gap = frame.Units(2f);
        var cell = (area.Width - gap * (cols - 1)) / cols;
        var rows = (marks.Count + cols - 1) / cols;
        var plane = rows * (cell + gap);
        frame.Paint.PushClip(area);
        var first = Math.Max(0, (int)(scroll / (cell + gap)) * cols);
        var last = Math.Min(marks.Count, first + cols * ((int)(area.Height / (cell + gap)) + 3));
        for (var index = first; index < last; index++)
        {
            var col = index % cols;
            var row = index / cols;
            var dest = Rect.FromSize(
                new Vector2(area.Min.X + (cell + gap) * col, area.Min.Y + (cell + gap) * row - scroll),
                new Vector2(cell, cell));
            DrawCell(frame, dest, area, marks[index], ink, accent, insert);
        }

        frame.Paint.PopClip();
        ScrollSlider.Apply(frame, area, ref scroll, plane);
    }

    private void DrawCell(in AppletFrame frame, Rect area, Rect clip, EmojiMark mark, Vector4 ink, Vector4 accent,
        Action<string> insert)
    {
        if (!area.Overlaps(clip))
        {
            return;
        }

        var shown = ShownGlyph(mark);
        var live = clip.Contains(frame.Input.Pointer);
        if (live && frame.Input.IsHovering(area))
        {
            frame.Paint.Fill(area, accent with { W = 0.16f }, frame.Units(8f));
        }

        EmojiArt.DrawOrMark(frame, area.Inset(frame.Units(2f)), shown, ink);
        if (!live)
        {
            return;
        }

        if (frame.Input.PressedInside(area))
        {
            holdGlyph = mark.Value;
            holdFor = 0f;
        }

        if (holdGlyph == mark.Value && frame.Input.IsHeld())
        {
            holdFor += frame.DeltaSeconds;
            if (mark.Tones && holdFor >= 0.38f)
            {
                variantOf = mark.Value;
                holdGlyph = string.Empty;
            }
        }

        if (!frame.Input.ConsumeClick(area))
        {
            return;
        }

        if (holdFor >= 0.38f && mark.Tones)
        {
            variantOf = mark.Value;
            holdFor = 0f;
            return;
        }

        holdFor = 0f;
        holdGlyph = string.Empty;
        Pick(shown, mark.Value, insert);
    }

    private void DrawVariants(in AppletFrame frame, Rect bounds, string glyph, Vector4 card, Vector4 ink, Vector4 mute,
        Action<string> insert)
    {
        var width = MathF.Min(bounds.Width, frame.Units(220f));
        var height = frame.Units(40f);
        var box = Rect.FromSize(
            new Vector2(bounds.Center.X - width * 0.5f, bounds.Min.Y + frame.Units(8f)),
            new Vector2(width, height));
        frame.Paint.Fill(box, card, frame.Units(12f));
        frame.Paint.Stroke(box, mute with { W = 0.35f }, frame.Units(1f), frame.Units(12f));
        var inner = box.Inset(frame.Units(4f));
        var cellW = inner.Width / EmojiBits.Tones.Length;
        for (var index = 0; index < EmojiBits.Tones.Length; index++)
        {
            var tone = EmojiBits.Tones[index];
            var cell = Rect.FromSize(new Vector2(inner.Min.X + cellW * index, inner.Min.Y),
                new Vector2(cellW, inner.Height));
            var face = EmojiBits.WithTone(glyph, tone);
            if (frame.Input.IsHovering(cell))
            {
                frame.Paint.Fill(cell, mute with { W = 0.18f }, frame.Units(8f));
            }

            EmojiArt.DrawOrMark(frame, cell.Inset(frame.Units(1f)), face, ink);
            if (frame.Input.ConsumeClick(cell))
            {
                recent.RememberTone(glyph, tone);
                Pick(face, glyph, insert);
                variantOf = null;
                return;
            }
        }

        if (frame.Input.ConsumeClick(bounds) && !box.Contains(frame.Input.Pointer))
        {
            variantOf = null;
        }
    }

    private void Pick(string glyph, string baseGlyph, Action<string> insert)
    {
        insert(glyph);
        recent.Remember(glyph);
        if (EmojiShelf.CanTone(baseGlyph))
        {
            var tone = glyph.Length > baseGlyph.Length ? glyph[baseGlyph.Length..] : string.Empty;
            recent.RememberTone(baseGlyph, tone);
        }
    }

    private string ShownGlyph(EmojiMark mark)
    {
        if (!mark.Tones)
        {
            return mark.Value;
        }

        return EmojiBits.WithTone(mark.Value, recent.ToneOf(mark.Value));
    }

    private List<EmojiMark> Shown()
    {
        var needle = query.Trim();
        if (needle.Length > 0)
        {
            return EmojiHunt.Find(needle, EmojiShelf.All);
        }

        if (group != EmojiGroup.Recent)
        {
            return EmojiShelf.Of(group).ToList();
        }

        var ranked = recent.Ranked();
        var hits = new List<EmojiMark>();
        for (var index = 0; index < ranked.Count; index++)
        {
            hits.Add(new EmojiMark(EmojiKind.Unicode, ranked[index], "recent", "recent", EmojiGroup.Recent, false));
        }

        return hits;
    }

    private static string TitleOf(EmojiGroup group)
    {
        for (var index = 0; index < EmojiShelf.Groups.Length; index++)
        {
            if (EmojiShelf.Groups[index].Group == group)
            {
                return EmojiShelf.Groups[index].Title.ToUpperInvariant();
            }
        }

        return string.Empty;
    }
}
