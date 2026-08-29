using Linkpearl.Applets;
using Linkpearl.Cards;
using Linkpearl.Geometry;
using Linkpearl.Layout;
using Linkpearl.Painting;
using Linkpearl.Talk;

namespace Linkpearl.Destinations.Social;

internal sealed class PhonePad
{
    private static readonly string[] Keys =
    {
        "1", "2", "3",
        "4", "5", "6",
        "7", "8", "9",
        "*", "0", "#",
    };

    private string digits = string.Empty;
    private string nameDraft = string.Empty;

    public void Compose(in AppletFrame frame, ITalk talk, MessagesSurface messages)
    {
        var stack = new Stack(frame.Content, StackAxis.Vertical, frame.Units(8f));
        frame.Text.DrawIn(stack.Take(frame.Units(18f)), "Dial a tell, or match a saved number.",
            new TextStyle(FontRole.Caption, frame.Theme.Palette.InkMuted));

        var display = stack.Take(frame.Units(40f));
        CardChrome.DrawGold(frame, display);
        var shown = digits.Length > 0 ? digits : " ";
        frame.Text.DrawIn(display.Inset(frame.Units(10f)), shown,
            new TextStyle(FontRole.Title, frame.Theme.Palette.Ink, TextAlign.Center));

        nameDraft = frame.TextField.Draw("phone-name", stack.Take(frame.Units(36f)), nameDraft, "Name@World");

        var pad = stack.Take(frame.Units(196f));
        DrawKeys(frame, pad);

        var actions = stack.Take(frame.Units(40f));
        if (Chip(frame, actions.LeftSlice(actions.Width * 0.48f), "Clear"))
        {
            digits = string.Empty;
        }

        if (Chip(frame, actions.RightSlice(actions.Width * 0.48f), "Call"))
        {
            Dial(talk, messages);
        }
    }

    private void DrawKeys(in AppletFrame frame, Rect pad)
    {
        var gap = frame.Units(6f);
        var cellWidth = (pad.Width - gap * 2f) / 3f;
        var cellHeight = (pad.Height - gap * 3f) / 4f;
        for (var index = 0; index < Keys.Length; index++)
        {
            var column = index % 3;
            var row = index / 3;
            var cell = Rect.FromSize(
                new Vector2(pad.Min.X + column * (cellWidth + gap), pad.Min.Y + row * (cellHeight + gap)),
                new Vector2(cellWidth, cellHeight));
            CardChrome.Draw(frame, cell);
            frame.Text.DrawIn(cell, Keys[index],
                new TextStyle(FontRole.Title, frame.Theme.Palette.Ink, TextAlign.Center));
            if (frame.Input.ConsumeClick(cell))
            {
                if (digits.Length < 16)
                {
                    digits += Keys[index];
                }
            }
        }
    }

    private void Dial(ITalk talk, MessagesSurface messages)
    {
        var typed = nameDraft.Trim();
        if (typed.Length > 0)
        {
            ParseName(typed, out var name, out var world);
            if (name.Length > 0)
            {
                messages.Open(talk.StartTell(name, world));
                return;
            }
        }

        var peers = talk.Peers();
        for (var index = 0; index < peers.Count; index++)
        {
            if (digits.Length > 0 && peers[index].Number.Equals(digits, StringComparison.Ordinal))
            {
                messages.OpenProfile(peers[index].Id);
                return;
            }
        }

        var hints = talk.SuggestTells();
        if (hints.Count > 0)
        {
            messages.Open(talk.StartTell(hints[0].Name, hints[0].World));
        }
    }

    private static void ParseName(string typed, out string name, out string world)
    {
        var at = typed.LastIndexOf('@');
        if (at < 0)
        {
            name = typed;
            world = string.Empty;
            return;
        }

        name = typed[..at].Trim();
        world = typed[(at + 1)..].Trim();
    }

    private static bool Chip(in AppletFrame frame, Rect area, string label)
    {
        CardChrome.DrawGold(frame, area);
        frame.Text.DrawIn(area, label,
            new TextStyle(FontRole.BodyStrong, frame.Theme.Palette.WarmAccent, TextAlign.Center));
        return frame.Input.ConsumeClick(area);
    }
}
