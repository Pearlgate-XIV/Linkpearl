using System.Globalization;
using System.Numerics;
using Linkpearl.Applets;
using Linkpearl.Audio;
using Linkpearl.Badges;
using Linkpearl.Geometry;
using Linkpearl.Layout;
using Linkpearl.Media;
using Linkpearl.Net;
using Linkpearl.Painting;
using Linkpearl.Phone;
using Linkpearl.Preferences;

namespace Linkpearl.Applets.Life.Phone;

public sealed class PhoneApplet : IApplet
{
    private static readonly string[] Keys =
    {
        "1", "2", "3",
        "4", "5", "6",
        "7", "8", "9",
        "*", "0", "#",
    };

    private static readonly string[] Letters =
    {
        "", "ABC", "DEF",
        "GHI", "JKL", "MNO",
        "PQRS", "TUV", "WXYZ",
        "", "+", "",
    };

    private static readonly Vector4 Night = new(0.05f, 0.05f, 0.06f, 1f);
    private static readonly Vector4 Ink = new(0.96f, 0.96f, 0.97f, 1f);
    private static readonly Vector4 Muted = new(0.62f, 0.63f, 0.66f, 1f);
    private static readonly Vector4 Pill = new(0.18f, 0.18f, 0.20f, 1f);
    private static readonly Vector4 Chip = new(0.28f, 0.28f, 0.30f, 1f);
    private static readonly Vector4 Green = new(0.18f, 0.72f, 0.38f, 1f);
    private static readonly Vector4 Bubble = new(0.20f, 0.45f, 0.92f, 1f);

    private static readonly Vector4[] Faces =
    {
        new(0.35f, 0.42f, 0.72f, 1f),
        new(0.22f, 0.55f, 0.48f, 1f),
        new(0.72f, 0.42f, 0.28f, 1f),
        new(0.55f, 0.38f, 0.68f, 1f),
        new(0.28f, 0.52f, 0.68f, 1f),
        new(0.62f, 0.48f, 0.22f, 1f),
    };

    public static readonly AppletManifest Manifest = new()
    {
        Id = "phone",
        DisplayNameKey = "Phone",
        Family = AppletFamily.Life,
        Glyph = "☎",
        HomeOrder = 1,
    };

    private readonly IHandsetLine line;
    private readonly IBroadcastSense sense;
    private readonly DisplayPreferences display;
    private readonly IPearlHub pearl;
    private readonly BadgeBook badges;
    private int tab;
    private string openNumber = string.Empty;
    private string draft = string.Empty;
    private string contactName = string.Empty;
    private string contactNumber = string.Empty;
    private string contactRace = string.Empty;
    private string contactWorld = string.Empty;
    private string suggestedNumber = string.Empty;
    private string search = string.Empty;
    private bool showSearch;
    private bool addingContact;
    private bool composing;
    private string composeNumber = string.Empty;
    private float listScroll;
    private float noteScroll;

    public PhoneApplet(IHandsetLine line, IBroadcastSense sense, DisplayPreferences display, IPearlHub pearl,
        BadgeBook badges)
    {
        this.line = line;
        this.sense = sense;
        this.display = display;
        this.pearl = pearl;
        this.badges = badges;
    }

    AppletManifest IApplet.Manifest => Manifest;

    public AppletBadge Badge => line.UnreadTotal > 0 ? new AppletBadge(line.UnreadTotal) : AppletBadge.None;

    public string Place => tab switch
    {
        1 => "messages",
        2 => "contacts",
        _ => "keypad",
    };

    public bool CanGoBack =>
        addingContact || composing || openNumber.Length > 0 || line.State != LineState.Idle;

    public void Enter(AppletEntry entry)
    {
        if (string.Equals(entry.RouteHint, "messages", StringComparison.Ordinal))
        {
            tab = 1;
        }
        else if (string.Equals(entry.RouteHint, "contacts", StringComparison.Ordinal))
        {
            tab = 2;
        }
    }

    public void Leave()
    {
    }

    public bool Back()
    {
        if (openNumber.Length > 0)
        {
            openNumber = string.Empty;
            draft = string.Empty;
            return true;
        }

        if (composing)
        {
            composing = false;
            composeNumber = string.Empty;
            return true;
        }

        if (showSearch)
        {
            showSearch = false;
            search = string.Empty;
            return true;
        }

        if (addingContact)
        {
            CloseContactForm();
            return true;
        }

        return false;
    }

    public void Compose(in AppletFrame frame)
    {
        line.Tick(frame.DeltaSeconds);
        var content = frame.Content.Inset(new Edges(frame.Units(10f), frame.Units(6f), frame.Units(10f),
            frame.Units(4f)));
        if (line.State != LineState.Idle)
        {
            DrawCall(frame, content);
            return;
        }

        var nav = content.BottomSlice(frame.Units(58f)).Inset(new Edges(frame.Units(18f), frame.Units(4f),
            frame.Units(18f), frame.Units(6f)));
        var body = new Rect(content.Min, new Vector2(content.Max.X, nav.Min.Y - frame.Units(4f)));
        if (tab == 1)
        {
            DrawMessages(frame, body);
        }
        else if (tab == 2)
        {
            DrawContacts(frame, body);
        }
        else
        {
            DrawKeypad(frame, body);
        }

        DrawNav(frame, nav);
    }

    private void DrawNav(in AppletFrame frame, Rect row)
    {
        frame.Paint.Fill(row, Pill, row.Height * 0.5f);
        var width = row.Width / 3f;
        DrawNavItem(frame, row.Translate(new Vector2(0f, 0f)).WithWidth(width), 0, "Phone");
        DrawNavItem(frame, row.Translate(new Vector2(width, 0f)).WithWidth(width), 1, "Messages");
        DrawNavItem(frame, row.Translate(new Vector2(width * 2f, 0f)).WithWidth(width), 2, "Contacts");
    }

    private void DrawNavItem(in AppletFrame frame, Rect cell, int index, string label)
    {
        var on = index == tab;
        var icon = cell.TopSlice(cell.Height * 0.62f).Inset(frame.Units(4f));
        if (on)
        {
            var mark = Rect.FromSize(
                new Vector2(icon.Center.X - frame.Units(18f), icon.Center.Y - frame.Units(14f)),
                new Vector2(frame.Units(36f), frame.Units(28f)));
            frame.Paint.Fill(mark, Chip, mark.Height * 0.5f);
        }

        DrawTabGlyph(frame, icon.Center, frame.Units(8f), index, Ink);
        frame.Text.DrawIn(cell.BottomSlice(frame.Units(16f)), label,
            new TextStyle(FontRole.Caption, on ? Ink : Muted, TextAlign.Center, scale: 0.92f));
        if (frame.Input.ConsumeClick(cell))
        {
            tab = index;
            openNumber = string.Empty;
            composing = false;
            composeNumber = string.Empty;
            listScroll = 0f;
        }
    }

    private static void DrawTabGlyph(in AppletFrame frame, Vector2 center, float size, int index, Vector4 color)
    {
        if (index == 0)
        {
            for (var row = 0; row < 3; row++)
            {
                for (var col = 0; col < 3; col++)
                {
                    frame.Paint.FillCircle(
                        center + new Vector2((col - 1) * size * 0.72f, (row - 1) * size * 0.72f),
                        size * 0.16f, color);
                }
            }

            return;
        }

        if (index == 1)
        {
            var bubble = Rect.FromSize(center - new Vector2(size * 1.1f, size * 0.72f),
                new Vector2(size * 2.2f, size * 1.35f));
            frame.Paint.Fill(bubble, color, size * 0.45f);
            frame.Paint.FillCircle(center + new Vector2(-size * 0.55f, size * 0.82f), size * 0.22f, color);
            return;
        }

        frame.Paint.FillCircle(center + new Vector2(0f, -size * 0.35f), size * 0.42f, color);
        var body = Rect.FromSize(center + new Vector2(-size * 0.72f, size * 0.12f),
            new Vector2(size * 1.44f, size * 0.82f));
        frame.Paint.Fill(body, color, size * 0.55f);
    }

    private void DrawKeypad(in AppletFrame frame, Rect area)
    {
        var page = new Stack(area, StackAxis.Vertical, frame.Units(8f));
        DrawLineCard(frame, page.Take(frame.Units(52f)));
        var rest = page.TakeRemaining();
        var cluster = frame.Units(348f);
        var lift = MathF.Max(frame.Units(8f), (rest.Height - cluster) * 0.42f);
        var stack = new Stack(rest.Inset(new Edges(0f, lift, 0f, 0f)), StackAxis.Vertical, frame.Units(10f));
        var dial = stack.Take(frame.Units(52f));
        frame.Paint.Fill(dial, Pill, dial.Height * 0.42f);
        frame.Paint.Stroke(dial, Chip, frame.Units(1.2f), dial.Height * 0.42f);
        var back = dial.RightSlice(frame.Units(36f));
        var field = dial.Inset(new Edges(frame.Units(12f), frame.Units(8f), frame.Units(36f), frame.Units(8f)));
        line.Dial = frame.TextField.Draw("phone-dial", field, LineNumbers.Show(line.Dial), "Enter number");

        frame.Text.DrawIn(back, "⌫", new TextStyle(FontRole.Title, Muted, TextAlign.Center));
        if (frame.Input.ConsumeClick(back))
        {
            line.Backspace();
        }

        DrawKeys(frame, stack.Take(frame.Units(228f)));
        var callRow = stack.Take(frame.Units(56f));
        var radius = frame.Units(24f);
        frame.Paint.FillCircle(callRow.Center, radius, Green);
        var mark = Rect.FromSize(callRow.Center - new Vector2(radius, radius),
            new Vector2(radius * 2f, radius * 2f));
        AppMarks.DrawMark(frame, mark, "phone");
        if (frame.Input.ConsumeClick(Rect.FromSize(callRow.Center - new Vector2(radius, radius),
                new Vector2(radius * 2f, radius * 2f))))
        {
            line.Call();
        }
    }

    private void DrawLineCard(in AppletFrame frame, Rect area)
    {
        frame.Paint.Fill(area, Pill, area.Height * 0.42f);
        var face = area.LeftSlice(frame.Units(52f)).Inset(frame.Units(8f));
        DrawHomePortrait(frame, face);
        var text = area.Inset(new Edges(frame.Units(52f), frame.Units(8f), frame.Units(12f), frame.Units(8f)));
        var snap = pearl.Current;
        var number = line.OwnNumber;
        if (snap.SignedIn && number.Length > 0)
        {
            var name = snap.MeName.Length > 0 ? snap.MeName : "You";
            frame.Text.DrawEllipsized(text.TopSlice(frame.Units(16f)), name,
                new TextStyle(FontRole.CaptionStrong, Muted));
            frame.Text.DrawEllipsized(text.BottomSlice(frame.Units(18f)), LineNumbers.Show(number),
                new TextStyle(FontRole.BodyStrong, Ink));
            return;
        }

        frame.Text.DrawEllipsized(text.TopSlice(frame.Units(16f)), "Your number",
            new TextStyle(FontRole.CaptionStrong, Muted));
        frame.Text.DrawEllipsized(text.BottomSlice(frame.Units(18f)),
            snap.SignedIn ? "Getting a Pearlgate number…" : "Sign in with Pearlgate",
            new TextStyle(FontRole.Caption, Muted));
    }

    private void DrawHomePortrait(in AppletFrame frame, Rect area)
    {
        var square = CoverFit.InscribedSquare(area);
        var radius = MathF.Min(square.Width, square.Height) * 0.5f;
        var center = square.Center;
        if (badges.PortraitFile.Length > 0)
        {
            var texture = frame.Textures.FromFile(PortraitFiles.Absolute(frame.Paths, badges.PortraitFile));
            if (texture is { IsReady: true })
            {
                var crop = CoverFit.Framed(texture.Size, square.Size, badges.PortraitZoom, badges.PortraitFocus);
                frame.Paint.ImageRounded(texture, square, crop.Min, crop.Max, Vector4.One, radius);
                frame.Paint.StrokeCircle(center, radius, Chip, MathF.Max(1.1f, frame.Units(1.2f)));
                return;
            }
        }

        DrawAvatar(frame, square, pearl.Current.MeName.Length > 0 ? pearl.Current.MeName : "You", chat: false);
    }

    private void DrawChrome(in AppletFrame frame, Rect row, string title, bool search, bool add)
    {
        frame.Text.DrawIn(row.LeftSlice(row.Width * 0.55f), title,
            new TextStyle(FontRole.Title, Ink));
        var tools = row.RightSlice(frame.Units(add ? 72f : 36f));
        if (add)
        {
            var plus = tools.LeftSlice(frame.Units(36f));
            frame.Text.DrawIn(plus, "+", new TextStyle(FontRole.Title, Ink, TextAlign.Center));
            if (frame.Input.ConsumeClick(plus))
            {
                OpenContactForm(line.Dial);
            }
        }

        var glass = tools.RightSlice(frame.Units(36f));
        frame.Text.DrawIn(glass, "⌕", new TextStyle(FontRole.Title, Ink, TextAlign.Center));
        if (search && frame.Input.ConsumeClick(glass))
        {
            showSearch = !showSearch;
            if (!showSearch)
            {
                this.search = string.Empty;
            }
        }
    }

    private void DrawKeys(in AppletFrame frame, Rect pad)
    {
        var gap = frame.Units(4f);
        var cellWidth = (pad.Width - gap * 2f) / 3f;
        var cellHeight = (pad.Height - gap * 3f) / 4f;
        for (var index = 0; index < Keys.Length; index++)
        {
            var column = index % 3;
            var row = index / 3;
            var cell = Rect.FromSize(
                new Vector2(pad.Min.X + column * (cellWidth + gap), pad.Min.Y + row * (cellHeight + gap)),
                new Vector2(cellWidth, cellHeight));
            frame.Text.DrawIn(cell.Inset(new Edges(0f, frame.Units(4f), 0f, frame.Units(14f))), Keys[index],
                new TextStyle(FontRole.Display, Ink, TextAlign.Center));
            if (Letters[index].Length > 0)
            {
                frame.Text.DrawIn(cell.BottomSlice(frame.Units(16f)), Letters[index],
                    new TextStyle(FontRole.Caption, Muted, TextAlign.Center, scale: 0.85f));
            }

            if (frame.Input.ConsumeClick(cell))
            {
                if (index == 10 && line.Dial.Length == 0)
                {
                    line.AppendDigit('+');
                }
                else
                {
                    line.AppendDigit(Keys[index][0]);
                }
            }
        }
    }


    private void DrawCall(in AppletFrame frame, Rect area)
    {
        var stack = new Stack(area, StackAxis.Vertical, frame.Units(10f));
        var live = line.State == LineState.Live;
        frame.Text.DrawIn(stack.Take(frame.Units(18f)), live ? "On a call" : "Calling…",
            new TextStyle(FontRole.CaptionStrong, Green, TextAlign.Center));
        frame.Text.DrawIn(stack.Take(frame.Units(28f)), line.PeerName,
            new TextStyle(FontRole.Title, Ink, TextAlign.Center));
        frame.Text.DrawIn(stack.Take(frame.Units(18f)), LineNumbers.Show(line.PeerNumber),
            new TextStyle(FontRole.Caption, Muted, TextAlign.Center));
        frame.Text.DrawIn(stack.Take(frame.Units(20f)), Clock(line.Elapsed),
            new TextStyle(FontRole.BodyStrong, Ink, TextAlign.Center));
        frame.Text.DrawWrapped(stack.Take(frame.Units(36f)),
            "Nothing is sent until the other line answers on Pearlgate voice. This keeps your mic and speaker ready.",
            new TextStyle(FontRole.Caption, Muted, TextAlign.Center));

        sense.RefreshPoints();
        DrawDevicePick(frame, ref stack, "phone-listen", "Listen on", true);
        DrawDevicePick(frame, ref stack, "phone-talk", "Talk on", false);

        var mutes = stack.Take(frame.Units(40f));
        if (DarkChip(frame, mutes.LeftSlice(mutes.Width * 0.48f), line.SpeakerMuted ? "Sound off" : "Mute call"))
        {
            line.ToggleSpeakerMute();
        }

        if (DarkChip(frame, mutes.RightSlice(mutes.Width * 0.48f), line.MicMuted ? "Mic off" : "Mute mic"))
        {
            line.ToggleMicMute();
        }

        var end = stack.Take(frame.Units(52f));
        frame.Paint.FillCircle(end.Center, frame.Units(22f), frame.Theme.Palette.Negative);
        frame.Text.DrawIn(end, "End",
            new TextStyle(FontRole.CaptionStrong, Ink, TextAlign.Center));
        if (frame.Input.ConsumeClick(end))
        {
            line.HangUp();
        }
    }

    private void DrawDevicePick(in AppletFrame frame, ref Stack stack, string id, string title, bool speakers)
    {
        frame.Text.DrawIn(stack.Take(frame.Units(16f)), title,
            new TextStyle(FontRole.CaptionStrong, Muted));
        var ports = Ports(speakers);
        var labels = new string[ports.Count + 1];
        labels[0] = "Same as Settings";
        var current = speakers ? display.CallSpeakerId : display.CallMicrophoneId;
        var selected = 0;
        for (var index = 0; index < ports.Count; index++)
        {
            labels[index + 1] = ports[index].Name;
            if (current.Length > 0 && string.Equals(current, ports[index].Id, StringComparison.Ordinal))
            {
                selected = index + 1;
            }
        }

        var row = stack.Take(frame.Units(36f));
        frame.Paint.Fill(row, Pill, frame.Units(12f));
        var picked = frame.TextField.Combo(id, row.Inset(frame.Units(8f)), labels, selected);
        if (picked == selected)
        {
            return;
        }

        var next = picked <= 0 ? string.Empty : ports[picked - 1].Id;
        if (speakers)
        {
            display.CallSpeakerId = next;
        }
        else
        {
            display.CallMicrophoneId = next;
        }

        sense.RoutePhone(display.ActiveCallSpeaker, display.ActiveCallMicrophone);
    }

    private List<AudioPoint> Ports(bool speakers)
    {
        var list = new List<AudioPoint>();
        var points = sense.Points;
        for (var index = 0; index < points.Count; index++)
        {
            var point = points[index];
            var input = point.Id.StartsWith("in:", StringComparison.Ordinal) ||
                        point.Id.StartsWith("wavein:", StringComparison.Ordinal);
            if (speakers == input)
            {
                continue;
            }

            list.Add(point);
        }

        return list;
    }

    private void DrawMessages(in AppletFrame frame, Rect area)
    {
        if (openNumber.Length > 0)
        {
            DrawThread(frame, area, openNumber);
            return;
        }

        if (composing)
        {
            DrawComposePick(frame, area);
            return;
        }

        var stack = new Stack(area, StackAxis.Vertical, frame.Units(6f));
        DrawChrome(frame, stack.Take(frame.Units(32f)), "Messages", search: true, add: false);
        if (showSearch)
        {
            search = frame.TextField.Draw("phone-search", stack.Take(frame.Units(34f)), search, "Search");
        }

        var list = stack.TakeRemaining();
        var threads = line.Threads;
        var cursor = new Stack(list.Translate(new Vector2(0f, -listScroll)), StackAxis.Vertical, 0f);
        frame.Paint.PushClip(list);
        var shown = 0;
        for (var index = 0; index < threads.Count; index++)
        {
            var row = threads[index];
            if (!Matches(search, row.Title, row.Number, row.Preview))
            {
                continue;
            }

            shown++;
            var cell = cursor.Take(frame.Units(64f));
            DrawAvatar(frame, cell.LeftSlice(frame.Units(52f)).Inset(frame.Units(6f)), row.Title, chat: true);
            var text = cell.Inset(new Edges(frame.Units(56f), frame.Units(10f), frame.Units(56f), frame.Units(10f)));
            frame.Text.DrawEllipsized(text.TopSlice(frame.Units(20f)), row.Title,
                new TextStyle(FontRole.BodyStrong, Ink));
            frame.Text.DrawEllipsized(text.BottomSlice(frame.Units(18f)), row.Preview,
                new TextStyle(FontRole.Caption, Muted));
            frame.Text.DrawIn(cell.RightSlice(frame.Units(52f)).Inset(new Edges(0f, frame.Units(12f), frame.Units(4f), 0f)),
                When(row.LastUnix),
                new TextStyle(FontRole.Caption, Muted, TextAlign.Right, scale: 0.9f));
            if (row.Unread > 0)
            {
                var badge = Rect.FromSize(new Vector2(cell.Max.X - frame.Units(18f), cell.Max.Y - frame.Units(22f)),
                    new Vector2(frame.Units(10f), frame.Units(10f)));
                frame.Paint.FillCircle(badge.Center, frame.Units(5f), Bubble);
            }

            if (frame.Input.ConsumeClick(cell))
            {
                openNumber = row.Number;
                line.MarkRead(row.Number);
                noteScroll = 0f;
            }
        }

        if (shown == 0)
        {
            frame.Text.DrawWrapped(list.Inset(frame.Units(16f)),
                "Texts on this phone stay here. They are not Direct tells.",
                new TextStyle(FontRole.Caption, Muted, TextAlign.Center));
        }

        var used = list.Height - cursor.Remaining.Height + listScroll;
        frame.Paint.PopClip();
        Wheel(frame, list, ref listScroll, used);

        var fab = Rect.FromSize(new Vector2(list.Max.X - frame.Units(52f), list.Max.Y - frame.Units(52f)),
            new Vector2(frame.Units(44f), frame.Units(44f)));
        frame.Paint.FillCircle(fab.Center, fab.Width * 0.5f, Bubble);
        frame.Text.DrawIn(fab, "✎", new TextStyle(FontRole.Title, Ink, TextAlign.Center));
        if (frame.Input.ConsumeClick(fab))
        {
            composing = true;
            composeNumber = string.Empty;
            showSearch = false;
            listScroll = 0f;
        }
    }

    private void DrawComposePick(in AppletFrame frame, Rect area)
    {
        var stack = new Stack(area, StackAxis.Vertical, frame.Units(8f));
        var header = stack.Take(frame.Units(32f));
        frame.Text.DrawIn(header.LeftSlice(frame.Units(28f)), "‹",
            new TextStyle(FontRole.Title, Ink, TextAlign.Center));
        frame.Text.DrawIn(header.Inset(new Edges(frame.Units(32f), 0f, 0f, 0f)), "New message",
            new TextStyle(FontRole.Title, Ink));
        if (frame.Input.ConsumeClick(header.LeftSlice(frame.Units(40f))))
        {
            composing = false;
            composeNumber = string.Empty;
            return;
        }

        var row = stack.Take(frame.Units(40f));
        frame.Paint.Fill(row, Pill, row.Height * 0.42f);
        frame.Paint.Stroke(row, Chip, frame.Units(1.2f), row.Height * 0.42f);
        var done = row.RightSlice(frame.Units(64f)).Inset(frame.Units(6f));
        var field = row.Inset(new Edges(frame.Units(12f), frame.Units(6f), frame.Units(68f), frame.Units(6f)));
        composeNumber = LineNumbers.Canonical(frame.TextField.Draw("phone-compose-number", field,
            LineNumbers.Show(composeNumber), "123-4567", 16, out var submitted));
        frame.Paint.Fill(done, composeNumber.Trim().Length > 0 ? Bubble : Chip, done.Height * 0.45f);
        frame.Text.DrawIn(done, "Done",
            new TextStyle(FontRole.CaptionStrong, Ink, TextAlign.Center));
        if ((submitted || frame.Input.ConsumeClick(done)) && composeNumber.Trim().Length > 0)
        {
            OpenCompose(composeNumber);
            return;
        }

        frame.Text.DrawIn(stack.Take(frame.Units(18f)), "Contacts",
            new TextStyle(FontRole.CaptionStrong, Muted));
        var list = stack.TakeRemaining();
        var contacts = line.Contacts;
        var cursor = new Stack(list.Translate(new Vector2(0f, -listScroll)), StackAxis.Vertical, 0f);
        frame.Paint.PushClip(list);
        var shown = 0;
        for (var index = 0; index < contacts.Count; index++)
        {
            var person = contacts[index];
            shown++;
            var cell = cursor.Take(frame.Units(56f));
            DrawAvatar(frame, cell.LeftSlice(frame.Units(52f)).Inset(frame.Units(8f)), person.Name, chat: false);
            var text = cell.Inset(new Edges(frame.Units(56f), frame.Units(8f), frame.Units(8f), frame.Units(8f)));
            frame.Text.DrawEllipsized(text.TopSlice(frame.Units(20f)), person.Name,
                new TextStyle(FontRole.BodyStrong, Ink));
            frame.Text.DrawEllipsized(text.BottomSlice(frame.Units(16f)), LineNumbers.Show(person.Number),
                new TextStyle(FontRole.Caption, Muted));
            if (frame.Input.ConsumeClick(cell))
            {
                OpenCompose(person.Number);
            }
        }

        if (shown == 0)
        {
            frame.Text.DrawWrapped(list.Inset(frame.Units(16f)),
                "No contacts yet. Type a number above, then Done.",
                new TextStyle(FontRole.Caption, Muted, TextAlign.Center));
        }

        var used = list.Height - cursor.Remaining.Height + listScroll;
        frame.Paint.PopClip();
        Wheel(frame, list, ref listScroll, used);
    }

    private void OpenCompose(string number)
    {
        var next = number.Trim();
        if (next.Length == 0)
        {
            return;
        }

        openNumber = next;
        composing = false;
        composeNumber = string.Empty;
        draft = string.Empty;
        noteScroll = 0f;
        line.MarkRead(next);
    }

    private void DrawThread(in AppletFrame frame, Rect area, string number)
    {
        var stack = new Stack(area, StackAxis.Vertical, frame.Units(8f));
        var header = stack.Take(frame.Units(36f));
        frame.Text.DrawIn(header.LeftSlice(frame.Units(28f)), "‹",
            new TextStyle(FontRole.Title, Ink, TextAlign.Center));
        DrawAvatar(frame, header.Translate(new Vector2(frame.Units(28f), 0f)).WithWidth(frame.Units(32f)),
            line.TitleOf(number), chat: false);
        frame.Text.DrawEllipsized(header.Inset(new Edges(frame.Units(64f), 0f, 0f, 0f)), line.TitleOf(number),
            new TextStyle(FontRole.BodyStrong, Ink));
        if (frame.Input.ConsumeClick(header.LeftSlice(frame.Units(40f))))
        {
            openNumber = string.Empty;
            return;
        }

        var composer = stack.Remaining.BottomSlice(frame.Units(40f));
        var log = new Rect(stack.Remaining.Min, new Vector2(stack.Remaining.Max.X, composer.Min.Y - frame.Units(8f)));
        var notes = line.Notes(number);
        frame.Paint.PushClip(log);
        var cursor = log.Min.Y - noteScroll;
        for (var index = 0; index < notes.Count; index++)
        {
            var note = notes[index];
            var inset = note.Mine ? frame.Units(48f) : 0f;
            var trail = note.Mine ? 0f : frame.Units(48f);
            var bubble = Rect.FromSize(new Vector2(log.Min.X + inset, cursor),
                new Vector2(log.Width - inset - trail, frame.Units(36f)));
            frame.Paint.Fill(bubble, note.Mine ? Bubble with { W = 0.55f } : Pill, frame.Units(14f));
            frame.Text.DrawEllipsized(bubble.Inset(frame.Units(8f)), note.Body,
                new TextStyle(FontRole.Caption, Ink));
            cursor += frame.Units(44f);
        }

        frame.Paint.PopClip();
        Wheel(frame, log, ref noteScroll, cursor - (log.Min.Y - noteScroll));

        frame.Paint.Fill(composer, Pill, composer.Height * 0.5f);
        draft = frame.TextField.Draw("phone-sms", composer.Inset(new Edges(frame.Units(12f), frame.Units(6f),
            frame.Units(64f), frame.Units(6f))), draft, "Text", 240, out var sent);
        var send = composer.RightSlice(frame.Units(58f)).Inset(frame.Units(6f));
        frame.Text.DrawIn(send, "Send",
            new TextStyle(FontRole.CaptionStrong, Bubble, TextAlign.Center));
        if ((sent || frame.Input.ConsumeClick(send)) && draft.Trim().Length > 0)
        {
            line.SendNote(number, draft);
            draft = string.Empty;
        }
    }

    private void DrawContacts(in AppletFrame frame, Rect area)
    {
        if (addingContact)
        {
            DrawContactForm(frame, area);
            return;
        }

        var stack = new Stack(area, StackAxis.Vertical, frame.Units(6f));
        DrawChrome(frame, stack.Take(frame.Units(32f)), "Phone", search: true, add: true);
        if (showSearch)
        {
            search = frame.TextField.Draw("phone-search", stack.Take(frame.Units(34f)), search, "Search contacts");
        }

        var list = stack.TakeRemaining();
        var contacts = line.Contacts;
        var cursor = new Stack(list.Translate(new Vector2(0f, -listScroll)), StackAxis.Vertical, 0f);
        frame.Paint.PushClip(list);
        var shown = 0;
        for (var index = 0; index < contacts.Count; index++)
        {
            var person = contacts[index];
            if (!Matches(search, person.Name, person.Number, ContactFacts(person)))
            {
                continue;
            }

            shown++;
            var extras = ContactFacts(person);
            var row = cursor.Take(frame.Units(extras.Length > 0 ? 68f : 56f));
            DrawAvatar(frame, row.LeftSlice(frame.Units(52f)).Inset(frame.Units(8f)), person.Name, chat: false);
            var text = row.Inset(new Edges(frame.Units(56f), frame.Units(8f), frame.Units(8f), frame.Units(8f)));
            var mark = person.FromGate ? "Pearlgate · " : "";
            frame.Text.DrawEllipsized(text.TopSlice(frame.Units(18f)), person.Name,
                new TextStyle(FontRole.BodyStrong, Ink));
            frame.Text.DrawEllipsized(text.Translate(new Vector2(0f, frame.Units(20f))).WithHeight(frame.Units(16f)),
                mark + LineNumbers.Show(person.Number), new TextStyle(FontRole.Caption, Muted));
            if (extras.Length > 0)
            {
                frame.Text.DrawEllipsized(text.BottomSlice(frame.Units(16f)), extras,
                    new TextStyle(FontRole.Caption, Muted));
            }

            if (frame.Input.ConsumeClick(row))
            {
                line.Dial = person.Number;
                tab = 0;
            }
        }

        if (shown == 0)
        {
            frame.Text.DrawWrapped(list.Inset(frame.Units(16f)),
                "Tap + to save a number. Pearlgate friends fill in for you.",
                new TextStyle(FontRole.Caption, Muted, TextAlign.Center));
        }

        var used = list.Height - cursor.Remaining.Height + listScroll;
        frame.Paint.PopClip();
        Wheel(frame, list, ref listScroll, used);
    }

    private void DrawContactForm(in AppletFrame frame, Rect area)
    {
        var stack = new Stack(area, StackAxis.Vertical, frame.Units(8f));
        var header = stack.Take(frame.Units(32f));
        frame.Text.DrawIn(header.LeftSlice(frame.Units(28f)), "‹",
            new TextStyle(FontRole.Title, Ink, TextAlign.Center));
        frame.Text.DrawIn(header.Inset(new Edges(frame.Units(32f), 0f, 0f, 0f)), "New contact",
            new TextStyle(FontRole.Title, Ink));
        if (frame.Input.ConsumeClick(header.LeftSlice(frame.Units(40f))))
        {
            CloseContactForm();
            return;
        }

        contactNumber = LineNumbers.Canonical(DrawLabeledField(frame, stack.Take(frame.Units(52f)),
            "phone-contact-number", "Number", LineNumbers.Show(contactNumber), "123-4567", 16));
        if (!LineNumbers.Same(contactNumber, suggestedNumber))
        {
            ApplyContactHint();
        }

        contactName = DrawLabeledField(frame, stack.Take(frame.Units(52f)), "phone-contact-name",
            "Name", contactName, "Name", 48);
        contactRace = DrawLabeledField(frame, stack.Take(frame.Units(52f)), "phone-contact-race",
            "Race", contactRace, "Race", 32);
        contactWorld = DrawLabeledField(frame, stack.Take(frame.Units(52f)), "phone-contact-world",
            "Home world", contactWorld, "Home world", 32);

        var hint = line.SuggestContact(contactNumber);
        frame.Text.DrawWrapped(stack.Take(frame.Units(28f)),
            hint.FromGate
                ? "Linked with Pearlgate — filled from their account."
                : "If they are on Pearlgate, their name, race, and world fill in.",
            new TextStyle(FontRole.Caption, Muted));

        var actions = stack.Take(frame.Units(40f));
        var save = actions.LeftSlice(actions.Width * 0.5f).Inset(new Edges(0f, 0f, frame.Units(4f), 0f));
        var cancel = actions.RightSlice(actions.Width * 0.5f).Inset(new Edges(frame.Units(4f), 0f, 0f, 0f));
        var ready = contactNumber.Trim().Length > 0;
        frame.Paint.Fill(save, ready ? Green : Chip, save.Height * 0.45f);
        frame.Text.DrawIn(save, "Save", new TextStyle(FontRole.CaptionStrong, Ink, TextAlign.Center));
        if (ready && frame.Input.ConsumeClick(save))
        {
            line.SaveContact(contactName, contactNumber, contactRace, contactWorld);
            CloseContactForm();
        }

        if (DarkChip(frame, cancel, "Cancel"))
        {
            CloseContactForm();
        }
    }

    private void OpenContactForm(string number)
    {
        addingContact = true;
        showSearch = false;
        contactNumber = number;
        contactName = string.Empty;
        contactRace = string.Empty;
        contactWorld = string.Empty;
        suggestedNumber = string.Empty;
        ApplyContactHint();
    }

    private void CloseContactForm()
    {
        addingContact = false;
        contactName = string.Empty;
        contactNumber = string.Empty;
        contactRace = string.Empty;
        contactWorld = string.Empty;
        suggestedNumber = string.Empty;
    }

    private void ApplyContactHint()
    {
        var hint = line.SuggestContact(contactNumber);
        suggestedNumber = contactNumber;
        if (hint.Name.Length > 0)
        {
            contactName = hint.Name;
        }

        if (hint.Race.Length > 0)
        {
            contactRace = hint.Race;
        }

        if (hint.World.Length > 0)
        {
            contactWorld = hint.World;
        }

        if (hint.Number.Length > 0)
        {
            contactNumber = hint.Number;
            suggestedNumber = hint.Number;
        }
    }

    private static string DrawLabeledField(in AppletFrame frame, Rect area, string id, string label, string value,
        string placeholder, int maxLength)
    {
        frame.Paint.Fill(area, Pill, area.Height * 0.28f);
        frame.Paint.Stroke(area, Chip, frame.Units(1.1f), area.Height * 0.28f);
        var inner = area.Inset(new Edges(frame.Units(12f), frame.Units(6f), frame.Units(10f), frame.Units(6f)));
        frame.Text.DrawIn(inner.TopSlice(frame.Units(14f)), label,
            new TextStyle(FontRole.CaptionStrong, Muted));
        return frame.TextField.Draw(id, inner.BottomSlice(frame.Units(22f)), value, placeholder, maxLength,
            out _);
    }

    private static string ContactFacts(LineContact person)
    {
        var race = person.Race ?? string.Empty;
        var world = person.World ?? string.Empty;
        if (race.Length == 0)
        {
            return world;
        }

        return world.Length == 0 ? race : race + " · " + world;
    }

    private static void DrawAvatar(in AppletFrame frame, Rect area, string name, bool chat)
    {
        var radius = MathF.Min(area.Width, area.Height) * 0.5f;
        frame.Paint.FillCircle(area.Center, radius, FaceOf(name));
        var letter = (name ?? string.Empty).Trim();
        var glyph = letter.Length > 0 ? letter[..1].ToUpperInvariant() : "?";
        frame.Text.DrawIn(area, glyph, new TextStyle(FontRole.BodyStrong, Ink, TextAlign.Center));
        if (chat)
        {
            var pin = area.Center + new Vector2(radius * 0.62f, radius * 0.62f);
            frame.Paint.FillCircle(pin, frame.Units(7f), Bubble);
        }
    }

    private static Vector4 FaceOf(string name)
    {
        var hash = 0;
        for (var index = 0; index < name.Length; index++)
        {
            hash = (hash * 31) + name[index];
        }

        return Faces[(hash & int.MaxValue) % Faces.Length];
    }

    private static bool Matches(string query, string title, string number, string preview)
    {
        if (query.Trim().Length == 0)
        {
            return true;
        }

        return title.Contains(query, StringComparison.OrdinalIgnoreCase) ||
               number.Contains(query, StringComparison.OrdinalIgnoreCase) ||
               preview.Contains(query, StringComparison.OrdinalIgnoreCase);
    }

    private static string When(long unix)
    {
        if (unix <= 0)
        {
            return string.Empty;
        }

        var stamp = DateTimeOffset.FromUnixTimeSeconds(unix).ToLocalTime();
        var today = DateTimeOffset.Now;
        if (stamp.Date == today.Date)
        {
            return stamp.ToString("h:mm tt", CultureInfo.CurrentCulture);
        }

        if ((today.Date - stamp.Date).TotalDays < 7)
        {
            return stamp.ToString("ddd", CultureInfo.CurrentCulture);
        }

        return stamp.ToString("MMM d", CultureInfo.CurrentCulture);
    }

    private static void Wheel(in AppletFrame frame, Rect area, ref float offset, float content)
    {
        if (frame.Input.IsHovering(area) && MathF.Abs(frame.Input.ScrollDelta) > 0.01f)
        {
            offset -= frame.Input.ScrollDelta * frame.Units(24f);
        }

        offset = Math.Clamp(offset, 0f, MathF.Max(0f, content - area.Height));
    }

    private static bool DarkChip(in AppletFrame frame, Rect area, string label)
    {
        frame.Paint.Fill(area, Pill, area.Height * 0.5f);
        frame.Text.DrawIn(area, label, new TextStyle(FontRole.CaptionStrong, Ink, TextAlign.Center));
        return frame.Input.ConsumeClick(area);
    }

    private static string Clock(float seconds)
    {
        var whole = Math.Max(0, (int)seconds);
        return (whole / 60).ToString("00", CultureInfo.InvariantCulture) + ":" +
               (whole % 60).ToString("00", CultureInfo.InvariantCulture);
    }
}
