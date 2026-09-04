using Linkpearl.Applets;
using Linkpearl.Cards;
using Linkpearl.Geometry;
using Linkpearl.Layout;
using Linkpearl.Net;
using Linkpearl.Painting;
using Linkpearl.Platform;
using Linkpearl.Preferences;
using Linkpearl.Talk;
using Linkpearl.Time;

namespace Linkpearl.Destinations.Social;

public sealed class SocialDestination : IDestinationScreen, ISectionedDestination
{
    private static readonly (string Label, int Pane)[] Sections =
    {
        ("Feed", SocialPane.Feed),
        ("Direct", SocialPane.Messages),
        ("Messages", SocialPane.Linkshells),
        ("People", SocialPane.People),
    };

    private readonly IPearlHub pearl;
    private readonly ITalk talk;
    private readonly MessagesSurface messages;
    private readonly LiveChatSurface feed;
    private readonly PhonePad phone = new();
    private int selectedSection = SocialPane.Feed;
    private string peopleQuery = string.Empty;

    public SocialDestination(IPearlHub pearl, IClock clock, ITalk talk, IGameSession game, DisplayPreferences display,
        ITalkPopouts popouts)
    {
        this.pearl = pearl;
        this.talk = talk;
        messages = new MessagesSurface(talk, clock, game, display, pearl, popouts);
        feed = new LiveChatSurface(talk, display);
    }

    public DestinationTab Tab => DestinationTab.Social;

    public string Glyph => "👥";

    public string Label => "Social";

    public int CurrentSection => selectedSection;

    public void ShowSection(int section)
    {
        selectedSection = section is SocialPane.Feed or SocialPane.Messages or SocialPane.People
            or SocialPane.Communities or SocialPane.Linkshells or SocialPane.Phone
            ? section
            : SocialPane.Feed;
        messages.ShowInbox(selectedSection);
        messages.Close();
    }

    public void OpenThread(string threadId)
    {
        if (threadId is TalkIds.Live or TalkIds.LiveSay or TalkIds.LiveShout or TalkIds.LiveYell
            or TalkIds.LiveParty)
        {
            selectedSection = SocialPane.Feed;
            messages.Close();
            return;
        }

        selectedSection = RoomThread(talk.Find(threadId)) ? SocialPane.Linkshells : SocialPane.Messages;
        messages.ShowInbox(selectedSection);
        messages.Open(threadId);
    }

    public void OpenProfile(string peerId) => messages.OpenProfile(peerId);

    public bool CanGoBack => messages.ProfileOpen || messages.ThreadOpen;

    public bool Back() => messages.Back();

    public float Compose(in AppletFrame frame)
    {
        if (messages.ProfileOpen)
        {
            return messages.Compose(frame);
        }

        if (selectedSection is SocialPane.Messages or SocialPane.Linkshells)
        {
            return ComposeMessages(frame);
        }

        if (selectedSection == SocialPane.Feed)
        {
            return ComposeFeed(frame);
        }

        var inset = frame.Units(14f);
        var content = frame.Content.Inset(inset);
        var stack = new Stack(content, StackAxis.Vertical, frame.Units(10f));
        var snapshot = pearl.Current;

        DrawHeading(frame, stack.Take(frame.Units(30f)));
        DrawSectionTabs(frame, stack.Take(frame.Units(32f)));

        if (selectedSection == SocialPane.Phone)
        {
            return DrawPhone(frame, stack, content, inset);
        }

        if (selectedSection == SocialPane.Communities)
        {
            DrawEmpty(frame, stack.TakeRemaining(),
                "Communities are not on Pearlgate yet (muster and yellow pages are off).");
            return content.Height + inset * 2f;
        }

        DrawPeople(frame, stack, snapshot);

        return (content.Height - stack.Remaining.Height) + inset * 2f;
    }

    private float ComposeFeed(in AppletFrame frame)
    {
        var inset = frame.Units(14f);
        var content = frame.Content.Inset(inset);
        var stack = new Stack(content, StackAxis.Vertical, frame.Units(10f));
        DrawHeading(frame, stack.Take(frame.Units(30f)));
        DrawSectionTabs(frame, stack.Take(frame.Units(32f)));
        var restArea = stack.TakeRemaining();
        var used = feed.Compose(frame.WithContent(restArea));
        return (content.Height - restArea.Height) + used + inset * 2f;
    }

    private float ComposeMessages(in AppletFrame frame)
    {
        if (messages.ThreadOpen)
        {
            return messages.Compose(frame);
        }

        var inset = frame.Units(14f);
        var content = frame.Content.Inset(inset);
        var stack = new Stack(content, StackAxis.Vertical, frame.Units(10f));
        DrawHeading(frame, stack.Take(frame.Units(30f)));
        DrawSectionTabs(frame, stack.Take(frame.Units(32f)));
        var restArea = stack.TakeRemaining();
        var used = messages.Compose(frame.WithContent(restArea));
        return (content.Height - restArea.Height) + used + inset * 2f;
    }

    private void DrawHeading(in AppletFrame frame, Rect row)
    {
        frame.Text.DrawIn(row, SectionTitle(), new TextStyle(FontRole.Title, frame.Theme.Palette.Ink));
    }

    private string SectionTitle()
    {
        for (var index = 0; index < Sections.Length; index++)
        {
            if (Sections[index].Pane == selectedSection)
            {
                return Sections[index].Label;
            }
        }

        return "Social";
    }

    private void DrawSectionTabs(in AppletFrame frame, Rect row)
    {
        var gold = frame.Theme.Palette.WarmAccent;
        var radius = row.Height * 0.5f;
        frame.Paint.Fill(row, frame.Theme.Palette.SurfaceOverlay, radius);
        frame.Paint.Stroke(row, gold with { W = 0.32f }, frame.Units(1f), radius);
        var cellWidth = row.Width / Sections.Length;
        for (var index = 0; index < Sections.Length; index++)
        {
            var cell = row.Translate(new Vector2(index * cellWidth, 0f)).WithWidth(cellWidth);
            var isActive = Sections[index].Pane == selectedSection;
            if (isActive)
            {
                frame.Paint.Fill(cell.Inset(frame.Units(2f)), gold with { W = 0.22f }, radius);
            }

            frame.Text.DrawIn(cell, Sections[index].Label,
                new TextStyle(FontRole.CaptionStrong, isActive ? gold : frame.Theme.Palette.InkMuted, TextAlign.Center));
            if (frame.Input.ConsumeClick(cell))
            {
                ShowSection(Sections[index].Pane);
            }
        }
    }

    private static bool RoomThread(TalkThread? thread) =>
        thread is { Kind: TalkKind.Party or TalkKind.Alliance or TalkKind.Linkshell or TalkKind.CrossWorldLinkshell
            or TalkKind.FreeCompany or TalkKind.Novice };

    private float DrawPhone(in AppletFrame frame, Stack stack, Rect content, float inset)
    {
        var rest = stack.TakeRemaining();
        phone.Compose(frame.WithContent(rest), talk, messages);
        return content.Height + inset * 2f;
    }

    private void DrawPeople(in AppletFrame frame, Stack stack, PearlSnapshot snapshot)
    {
        if (snapshot.SignedIn)
        {
            DrawFindFriends(frame, ref stack, snapshot);
        }

        var peers = talk.Peers();
        var friends = talk.Friends();
        var query = peopleQuery.Trim();
        var shownEorzea = DrawEorzeaFriends(frame, ref stack, friends, peers, query);
        var hints = talk.SuggestTells();
        if (peers.Count > 0)
        {
            frame.Text.DrawIn(stack.Take(frame.Units(18f)), "Talked to",
                new TextStyle(FontRole.CaptionStrong, frame.Theme.Palette.InkMuted));
            for (var index = 0; index < peers.Count; index++)
            {
                var peer = peers[index];
                var row = stack.Take(frame.Units(56f));
                CardChrome.DrawGold(frame, row);
                DrawPeer(frame, row.Inset(frame.Units(12f)), peer);
                if (frame.Input.ConsumeClick(row))
                {
                    messages.OpenProfile(peer.Id);
                }
            }
        }

        if (hints.Count > 0)
        {
            frame.Text.DrawIn(stack.Take(frame.Units(18f)), "Nearby to tell",
                new TextStyle(FontRole.CaptionStrong, frame.Theme.Palette.InkMuted));
            for (var index = 0; index < hints.Count; index++)
            {
                var hint = hints[index];
                if (AlreadyPeer(peers, hint.Name) || AlreadyGameFriend(friends, hint.Name))
                {
                    continue;
                }

                var row = stack.Take(frame.Units(56f));
                CardChrome.DrawGold(frame, row);
                DrawHint(frame, row.Inset(frame.Units(12f)), hint);
                if (frame.Input.ConsumeClick(row))
                {
                    messages.OpenProfile(TalkIds.Tell(hint.Name, hint.World));
                }
            }
        }

        if (!snapshot.SignedIn)
        {
            if (peers.Count == 0 && hints.Count == 0 && shownEorzea == 0)
            {
                DrawEmpty(frame, stack.Take(frame.Units(72f)),
                    "Eorzea friends show up here. Sign in from You to add Pearlgate contacts too.");
            }

            return;
        }

        var shownGate = 0;
        for (var index = 0; index < snapshot.People.Length; index++)
        {
            if (AlreadyPeer(peers, snapshot.People[index].DisplayName))
            {
                continue;
            }

            if (shownGate == 0)
            {
                frame.Text.DrawIn(stack.Take(frame.Units(18f)), "Friends",
                    new TextStyle(FontRole.CaptionStrong, frame.Theme.Palette.InkMuted));
            }

            var person = snapshot.People[index];
            var row = stack.Take(frame.Units(72f));
            CardChrome.DrawGold(frame, row);
            var inner = row.Inset(frame.Units(12f));
            DrawPerson(frame, inner.Inset(new Edges(0f, 0f, frame.Units(72f), 0f)), person);
            if (Chip(frame, inner.RightSlice(frame.Units(64f)).TopSlice(frame.Units(28f)), "Remove"))
            {
                pearl.RemoveFriend(person.Id);
            }
            else if (frame.Input.ConsumeClick(row))
            {
                messages.OpenProfile(TalkIds.Person(person.Id));
            }

            shownGate++;
        }

        if (peers.Count == 0 && hints.Count == 0 && shownGate == 0 && shownEorzea == 0 &&
            peopleQuery.Trim().Length < 2)
        {
            DrawEmpty(frame, stack.Take(frame.Units(72f)),
                "No friends yet. Game friends appear here; search a name or Pearlgate number to add more.");
        }
    }

    private void DrawFindFriends(in AppletFrame frame, ref Stack stack, PearlSnapshot snapshot)
    {
        if (snapshot.MyNumber.Length > 0)
        {
            frame.Text.DrawEllipsized(stack.Take(frame.Units(16f)), "Your number " + snapshot.MyNumber,
                new TextStyle(FontRole.Caption, frame.Theme.Palette.InkMuted));
        }

        var searchRow = stack.Take(frame.Units(36f));
        peopleQuery = frame.TextField.Draw("people-find", searchRow, peopleQuery, "Name, handle, or number");
        pearl.NoteQuery(peopleQuery);

        var trimmed = peopleQuery.Trim();
        if (LooksLikeNumber(trimmed))
        {
            var addRow = stack.Take(frame.Units(36f));
            frame.Text.DrawEllipsized(addRow.LeftSlice(addRow.Width - frame.Units(88f)), "Add by number",
                new TextStyle(FontRole.Caption, frame.Theme.Palette.InkMuted));
            if (Chip(frame, addRow.RightSlice(frame.Units(80f)), "Add"))
            {
                pearl.AddFriend(string.Empty, trimmed);
                peopleQuery = string.Empty;
            }
        }

        if (trimmed.Length < 2)
        {
            return;
        }

        var shown = 0;
        for (var index = 0; index < snapshot.SearchHits.Length; index++)
        {
            var hit = snapshot.SearchHits[index];
            if (hit.Id.Length == 0 || string.Equals(hit.Id, snapshot.MeId, StringComparison.Ordinal) ||
                AlreadyFriend(snapshot, hit.Id))
            {
                continue;
            }

            if (shown == 0)
            {
                frame.Text.DrawIn(stack.Take(frame.Units(18f)), "Find",
                    new TextStyle(FontRole.CaptionStrong, frame.Theme.Palette.InkMuted));
            }

            var row = stack.Take(frame.Units(64f));
            CardChrome.DrawGold(frame, row);
            var inner = row.Inset(frame.Units(12f));
            frame.Text.DrawEllipsized(inner.LeftSlice(inner.Width - frame.Units(72f)).TopSlice(frame.Units(22f)),
                hit.Title, new TextStyle(FontRole.BodyStrong, frame.Theme.Palette.Ink));
            frame.Text.DrawEllipsized(inner.LeftSlice(inner.Width - frame.Units(72f)).BottomSlice(frame.Units(18f)),
                hit.Subtitle, new TextStyle(FontRole.Caption, frame.Theme.Palette.InkMuted));
            if (Chip(frame, inner.RightSlice(frame.Units(64f)).TopSlice(frame.Units(28f)), "Add"))
            {
                pearl.AddFriend(hit.Id, trimmed);
                peopleQuery = string.Empty;
            }
            else if (frame.Input.ConsumeClick(row))
            {
                messages.OpenProfile(TalkIds.Person(hit.Id));
            }

            shown++;
        }
    }

    private static bool AlreadyFriend(PearlSnapshot snapshot, string userId)
    {
        for (var index = 0; index < snapshot.People.Length; index++)
        {
            if (string.Equals(snapshot.People[index].Id, userId, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static bool LooksLikeNumber(string value)
    {
        var digits = 0;
        for (var index = 0; index < value.Length; index++)
        {
            var ch = value[index];
            if (char.IsDigit(ch))
            {
                digits++;
                continue;
            }

            if (ch is not ' ' and not '-' and not '+')
            {
                return false;
            }
        }

        return digits >= 7;
    }

    private static bool Chip(in AppletFrame frame, Rect area, string label)
    {
        frame.Paint.Fill(area.Inset(frame.Units(2f)), frame.Theme.Palette.Accent, frame.Units(999f));
        frame.Text.DrawIn(area, label, new TextStyle(FontRole.Caption, frame.Theme.Palette.AccentInk, TextAlign.Center));
        return frame.Input.ConsumeClick(area);
    }

    private int DrawEorzeaFriends(in AppletFrame frame, ref Stack stack, IReadOnlyList<GameFriend> friends,
        IReadOnlyList<TalkPeer> peers, string query)
    {
        var shown = 0;
        for (var index = 0; index < friends.Count; index++)
        {
            var friend = friends[index];
            if (AlreadyPeer(peers, friend.Name))
            {
                continue;
            }

            if (query.Length >= 2 &&
                !friend.Name.Contains(query, StringComparison.OrdinalIgnoreCase) &&
                !friend.World.Contains(query, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (shown == 0)
            {
                frame.Text.DrawIn(stack.Take(frame.Units(18f)), "Eorzea",
                    new TextStyle(FontRole.CaptionStrong, frame.Theme.Palette.InkMuted));
            }

            var row = stack.Take(frame.Units(56f));
            CardChrome.DrawGold(frame, row);
            DrawGameFriend(frame, row.Inset(frame.Units(12f)), friend);
            if (frame.Input.ConsumeClick(row))
            {
                messages.OpenProfile(TalkIds.Tell(friend.Name, friend.World));
            }

            shown++;
        }

        return shown;
    }

    private static void DrawGameFriend(in AppletFrame frame, Rect inset, GameFriend friend)
    {
        var stack = new Stack(inset, StackAxis.Vertical, frame.Units(2f));
        frame.Text.DrawIn(stack.Take(frame.Units(20f)), friend.Name,
            new TextStyle(FontRole.BodyStrong, frame.Theme.Palette.Ink));
        var status = friend.Online ? "Online" : "Offline";
        var where = friend.Place.Length > 0 ? friend.Place : friend.World;
        var detail = where.Length > 0 ? status + " · " + where : status;
        frame.Text.DrawIn(stack.Take(frame.Units(16f)), detail,
            new TextStyle(FontRole.Caption,
                friend.Online ? frame.Theme.Palette.WarmAccent : frame.Theme.Palette.InkMuted));
    }

    private static bool AlreadyGameFriend(IReadOnlyList<GameFriend> friends, string name)
    {
        for (var index = 0; index < friends.Count; index++)
        {
            if (friends[index].Name.Equals(name, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static void DrawPeer(in AppletFrame frame, Rect inset, TalkPeer peer)
    {
        var stack = new Stack(inset, StackAxis.Vertical, frame.Units(2f));
        frame.Text.DrawIn(stack.Take(frame.Units(20f)), peer.Name,
            new TextStyle(FontRole.BodyStrong, frame.Theme.Palette.Ink));
        var gate = peer.OnPearlgate ? "Pearlgate" : "Tell";
        var detail = peer.World.Length > 0 ? gate + " · " + peer.World : gate;
        frame.Text.DrawIn(stack.Take(frame.Units(16f)), detail,
            new TextStyle(FontRole.Caption, frame.Theme.Palette.InkMuted));
    }

    private static bool AlreadyPeer(IReadOnlyList<TalkPeer> peers, string name)
    {
        for (var index = 0; index < peers.Count; index++)
        {
            if (peers[index].Name.Equals(name, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static void DrawHint(in AppletFrame frame, Rect inset, GamePeerHint hint)
    {
        var stack = new Stack(inset, StackAxis.Vertical, frame.Units(2f));
        frame.Text.DrawIn(stack.Take(frame.Units(20f)), hint.Name,
            new TextStyle(FontRole.BodyStrong, frame.Theme.Palette.Ink));
        var detail = hint.World.Length > 0 ? hint.Reason + " · " + hint.World : hint.Reason;
        frame.Text.DrawIn(stack.Take(frame.Units(16f)), detail,
            new TextStyle(FontRole.Caption, frame.Theme.Palette.InkMuted));
    }

    private static void DrawPerson(in AppletFrame frame, Rect inset, PearlPerson person)
    {
        var stack = new Stack(inset, StackAxis.Vertical, frame.Units(3f));
        frame.Text.DrawIn(stack.Take(frame.Units(20f)), person.DisplayName,
            new TextStyle(FontRole.BodyStrong, frame.Theme.Palette.Ink));
        var handle = person.Handle.Length > 0 ? "@" + person.Handle : person.PhoneNumber;
        var detail = handle.Length > 0 ? handle + " · Tell" : "Tell";
        frame.Text.DrawIn(stack.Take(frame.Units(18f)), detail,
            new TextStyle(FontRole.Caption, frame.Theme.Palette.InkMuted));
    }

    private static void DrawEmpty(in AppletFrame frame, Rect area, string text)
    {
        frame.Text.DrawWrapped(area.TopSlice(frame.Units(72f)), text,
            new TextStyle(FontRole.Body, frame.Theme.Palette.InkMuted, TextAlign.Center));
    }
}