using System.Numerics;
using Linkpearl.Applets;
using Linkpearl.Feedback;
using Linkpearl.Cards;
using Linkpearl.Chat;
using Linkpearl.Geometry;
using Linkpearl.Input;
using Linkpearl.Layout;
using Linkpearl.Modules;
using Linkpearl.Net;
using Linkpearl.Painting;
using Linkpearl.Phone;
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
        ("Friends", SocialPane.People),
    };

    private readonly IPearlHub pearl;
    private readonly ITalk talk;
    private readonly IChatBridge chat;
    private readonly DisplayPreferences display;
    private readonly MessagesSurface messages;
    private readonly LiveChatSurface feed;
    private readonly PhonePad phone = new();
    private readonly FriendBook friendsBook;
    private int selectedSection = SocialPane.Messages;
    private string peopleQuery = string.Empty;
    private float peopleScroll;
    private readonly IGameSession game;
    private readonly IFeedbackDesk desk;
    private FriendMenu? menu;
    private bool reportOpen;
    private bool reportFresh;
    private int reportReason;
    private string reportDetail = string.Empty;
    private string reportName = string.Empty;
    private string reportWorld = string.Empty;
    private string reportId = string.Empty;

    public SocialDestination(IPearlHub pearl, IClock clock, ITalk talk, IGameSession game, DisplayPreferences display,
        ITalkPopouts popouts, IChatBridge chat, HostPaths paths, IFilePicker files, IGifDesk gifs, ChatMarks marks,
        IFeedbackDesk desk)
    {
        this.pearl = pearl;
        this.talk = talk;
        this.chat = chat;
        this.display = display;
        this.game = game;
        this.desk = desk;
        friendsBook = new FriendBook(paths);
        messages = new MessagesSurface(talk, clock, game, display, pearl, popouts, files, gifs, marks, desk);
        feed = new LiveChatSurface(talk, display, chat, OpenTellFromPeople, gifs, marks,
            (name, world, body) =>
            {
                reportOpen = true;
                reportFresh = true;
                reportReason = 0;
                reportDetail = body;
                reportName = name;
                reportWorld = world;
                reportId = FindGatePerson(name)?.Id ?? name;
            });
    }

    public DestinationTab Tab => DestinationTab.Social;

    public string Glyph => "👥";

    public string Label => PhoneLanguages.T("nav.social");

    public int CurrentSection => selectedSection;

    public void ShowSection(int section)
    {
        selectedSection = section is SocialPane.Feed or SocialPane.Messages or SocialPane.People
            or SocialPane.Communities or SocialPane.Linkshells or SocialPane.Phone
            ? section
            : SocialPane.Feed;
        messages.ShowInbox(selectedSection);
        messages.Close();
        menu = null;
        menu = null;
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

    public bool CanGoBack => reportOpen || menu is not null || feed.HasMenu || messages.ProfileOpen || messages.ThreadOpen;

    public bool Back()
    {
        if (reportOpen)
        {
            reportOpen = false;
            return true;
        }

        if (menu is not null)
        {
            menu = null;
            return true;
        }

        if (feed.CloseMenu())
        {
            return true;
        }

        return messages.Back();
    }

    public float Compose(in AppletFrame frame)
    {
        if (reportOpen)
        {
            var result = HandsetReportSheet.Draw(frame, frame.Content, "Report", "social-report",
                ref reportReason, ref reportDetail, ref reportFresh);
            if (result == ReportSheetResult.Cancel)
            {
                reportOpen = false;
            }
            else if (result == ReportSheetResult.Submit)
            {
                var reason = StaffReports.ReasonAt(reportReason);
                var detail = reportName + (reportWorld.Length > 0 ? " @ " + reportWorld : string.Empty) +
                             (reportDetail.Trim().Length > 0 ? "\n\n" + reportDetail.Trim() : string.Empty);
                var id = reportId.Length > 0 ? reportId : (reportName.Length > 0 ? reportName : "unknown");
                StaffReportDispatch.File(pearl, desk, game.Character.Name, game.Character.WorldName, "user", id, reason,
                    detail);
                reportOpen = false;
            }

            return frame.Content.Height;
        }

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

        DrawOnlineFirstToggle(frame, stack.Take(frame.Units(36f)));

        var body = stack.TakeRemaining();
        if (body.Height <= 0f)
        {
            return;
        }

        var query = peopleQuery.Trim();
        var peers = talk.Peers();
        var friends = talk.Friends();
        var hints = talk.SuggestTells();
        var shifted = body.Translate(new Vector2(0f, -peopleScroll));
        var plane = Math.Max(body.Height + 1f,
            frame.Units(80f) * (friends.Count + peers.Count + hints.Count + snapshot.People.Length + 8));
        var list = new Stack(Rect.FromSize(shifted.Min, new Vector2(body.Width, plane)), StackAxis.Vertical,
            frame.Units(10f));
        frame.Paint.PushClip(body);

        var shownEorzea = DrawEorzeaFriends(frame, ref list, friends, query);
        var shownPeers = DrawTalkedPeers(frame, ref list, peers, friends, query);
        var shownHints = DrawNearbyHints(frame, ref list, hints, peers, friends, query);
        var shownGate = snapshot.SignedIn
            ? DrawGateFriends(frame, ref list, snapshot, peers, friends, query)
            : 0;

        if (shownEorzea + shownPeers + shownHints + shownGate == 0)
        {
            var copy = snapshot.SignedIn && query.Length < 2
                ? "No friends yet. Game friends appear here; search a name or Pearlgate number to add more."
                : snapshot.SignedIn
                    ? "No one matches that search."
                    : "Eorzea friends show up here. Sign in from You to add Pearlgate contacts too.";
            DrawEmpty(frame, list.Take(frame.Units(72f)), copy);
        }

        var listHeight = plane - list.Remaining.Height;
        frame.Paint.PopClip();

        ScrollSlider.Apply(frame, body, ref peopleScroll, listHeight, live: menu is null);
        DrawFriendMenu(frame, body);
    }

    private void DrawFindFriends(in AppletFrame frame, ref Stack stack, PearlSnapshot snapshot)
    {
        if (snapshot.MyNumber.Length > 0)
        {
            frame.Text.DrawEllipsized(stack.Take(frame.Units(16f)),
                "Your number " + LineNumbers.Show(snapshot.MyNumber),
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

    private void DrawOnlineFirstToggle(in AppletFrame frame, Rect row)
    {
        var toggle = row.RightSlice(frame.Units(58f));
        frame.Text.DrawEllipsized(row.Inset(new Edges(0f, 0f, toggle.Width + frame.Units(8f), 0f)),
            "Move online to top",
            new TextStyle(FontRole.CaptionStrong, frame.Theme.Palette.Ink));
        DrawPeopleSwitch(frame, toggle, friendsBook.OnlineFirst);
        if (frame.Input.ConsumeClick(row))
        {
            friendsBook.SetOnlineFirst(!friendsBook.OnlineFirst);
        }
    }

    private static void DrawPeopleSwitch(in AppletFrame frame, Rect area, bool on)
    {
        var gold = frame.Theme.Palette.WarmAccent;
        var height = MathF.Min(area.Height, frame.Units(22f));
        var width = MathF.Min(area.Width, frame.Units(52f));
        var track = Rect.FromSize(new Vector2(area.Max.X - width, area.Center.Y - height * 0.5f),
            new Vector2(width, height));
        var radius = height * 0.5f;
        if (on)
        {
            frame.Paint.Fill(track, gold with { W = 0.92f }, radius);
        }
        else
        {
            frame.Paint.Fill(track, frame.Theme.Palette.SurfaceSunken with { W = 0.82f }, radius);
            frame.Paint.Stroke(track, gold with { W = 0.28f }, frame.Theme.Metrics.Hairline, radius);
        }

        var knob = height * 0.38f;
        var knobX = on ? track.Max.X - knob - frame.Units(4f) : track.Min.X + knob + frame.Units(4f);
        frame.Paint.FillCircle(new Vector2(knobX, track.Center.Y), knob,
            on ? new Vector4(0.96f, 0.90f, 0.78f, 1f) : frame.Theme.Palette.InkMuted);
    }

    private int DrawEorzeaFriends(in AppletFrame frame, ref Stack stack, IReadOnlyList<GameFriend> friends,
        string query)
    {
        var matched = new List<GameFriend>(friends.Count);
        for (var index = 0; index < friends.Count; index++)
        {
            var friend = friends[index];
            if (query.Length >= 2 &&
                !friend.Name.Contains(query, StringComparison.OrdinalIgnoreCase) &&
                !friend.World.Contains(query, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            matched.Add(friend);
        }

        if (matched.Count == 0)
        {
            return 0;
        }

        var starred = new List<GameFriend>();
        var rest = new List<GameFriend>();
        for (var index = 0; index < matched.Count; index++)
        {
            var friend = matched[index];
            if (friendsBook.IsStarred(friend))
            {
                starred.Add(friend);
            }
            else
            {
                rest.Add(friend);
            }
        }

        starred.Sort(CompareName);
        var shown = DrawFriendGroup(frame, ref stack, "Favorites", starred);
        if (!friendsBook.OnlineFirst)
        {
            rest.Sort(CompareName);
            for (var index = 0; index < rest.Count; index++)
            {
                DrawFriendCard(frame, ref stack, rest[index]);
                shown++;
            }

            return shown;
        }

        var online = new List<GameFriend>();
        var offline = new List<GameFriend>();
        for (var index = 0; index < rest.Count; index++)
        {
            if (rest[index].Online)
            {
                online.Add(rest[index]);
            }
            else
            {
                offline.Add(rest[index]);
            }
        }

        online.Sort(CompareName);
        offline.Sort(CompareName);
        shown += DrawFriendGroup(frame, ref stack, "Online", online);
        shown += DrawFriendGroup(frame, ref stack, "Offline", offline);
        return shown;
    }

    private static int CompareName(GameFriend left, GameFriend right)
    {
        var name = string.Compare(left.Name, right.Name, StringComparison.OrdinalIgnoreCase);
        return name != 0 ? name : string.Compare(left.World, right.World, StringComparison.OrdinalIgnoreCase);
    }

    private int DrawFriendGroup(in AppletFrame frame, ref Stack stack, string title, List<GameFriend> group)
    {
        if (group.Count == 0)
        {
            return 0;
        }

        frame.Text.DrawIn(stack.Take(frame.Units(18f)), title,
            new TextStyle(FontRole.CaptionStrong, frame.Theme.Palette.InkMuted));
        for (var index = 0; index < group.Count; index++)
        {
            DrawFriendCard(frame, ref stack, group[index]);
        }

        return group.Count;
    }

    private void DrawFriendCard(in AppletFrame frame, ref Stack stack, GameFriend friend)
    {
        var gate = FindGatePerson(friend.Name);
        var row = stack.Take(frame.Units(gate is null ? 56f : 72f));
        CardChrome.DrawGold(frame, row);
        var star = row.RightSlice(frame.Units(40f)).Inset(new Edges(0f, frame.Units(10f), frame.Units(8f),
            frame.Units(10f)));
        var copy = row.Inset(new Edges(frame.Units(12f), frame.Units(8f), star.Width + frame.Units(8f),
            frame.Units(8f)));
        DrawGameFriend(frame, copy, friend, gate);
        DrawFriendStar(frame, star, friendsBook.IsStarred(friend));
        if (frame.Input.ConsumeClick(star))
        {
            friendsBook.ToggleStar(friend);
            return;
        }

        HandleFriendRow(frame, row, friend.Name, friend.World, () =>
            OpenTellFromPeople(friend.Name, friend.World));
    }

    private static void DrawFriendStar(in AppletFrame frame, Rect area, bool on)
    {
        var gold = new Vector4(1f, 0.84f, 0.18f, 1f);
        var ink = on ? gold : frame.Theme.Palette.InkMuted;
        var texture = frame.Textures.FromFile(AppIconCatalog.Glyph(frame.Paths, "music-save.png"));
        if (texture is { IsReady: true })
        {
            var side = MathF.Min(area.Width, area.Height);
            var dest = Rect.FromSize(area.Center - new Vector2(side * 0.5f, side * 0.5f), new Vector2(side, side));
            frame.Paint.Image(texture, dest, ink);
            return;
        }

        frame.Text.DrawIn(area, "★", new TextStyle(FontRole.Title, ink, TextAlign.Center));
    }

    private int DrawTalkedPeers(in AppletFrame frame, ref Stack stack, IReadOnlyList<TalkPeer> peers,
        IReadOnlyList<GameFriend> friends, string query)
    {
        var shown = 0;
        for (var index = 0; index < peers.Count; index++)
        {
            var peer = peers[index];
            if (AlreadyGameFriend(friends, peer.Name))
            {
                continue;
            }

            if (query.Length >= 2 &&
                !peer.Name.Contains(query, StringComparison.OrdinalIgnoreCase) &&
                !peer.World.Contains(query, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (shown == 0)
            {
                frame.Text.DrawIn(stack.Take(frame.Units(18f)), "Talked to",
                    new TextStyle(FontRole.CaptionStrong, frame.Theme.Palette.InkMuted));
            }

            var row = stack.Take(frame.Units(HasGateLine(peer.Handle, peer.Number) ? 72f : 56f));
            CardChrome.DrawGold(frame, row);
            DrawPeer(frame, row.Inset(frame.Units(12f)), peer);
            HandleFriendRow(frame, row, peer.Name, peer.World, () => messages.OpenProfile(peer.Id));
            shown++;
        }

        return shown;
    }

    private int DrawNearbyHints(in AppletFrame frame, ref Stack stack, IReadOnlyList<GamePeerHint> hints,
        IReadOnlyList<TalkPeer> peers, IReadOnlyList<GameFriend> friends, string query)
    {
        var shown = 0;
        for (var index = 0; index < hints.Count; index++)
        {
            var hint = hints[index];
            if (AlreadyPeer(peers, hint.Name) || AlreadyGameFriend(friends, hint.Name))
            {
                continue;
            }

            if (query.Length >= 2 &&
                !hint.Name.Contains(query, StringComparison.OrdinalIgnoreCase) &&
                !hint.World.Contains(query, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (shown == 0)
            {
                frame.Text.DrawIn(stack.Take(frame.Units(18f)), "Nearby to tell",
                    new TextStyle(FontRole.CaptionStrong, frame.Theme.Palette.InkMuted));
            }

            var row = stack.Take(frame.Units(56f));
            CardChrome.DrawGold(frame, row);
            DrawHint(frame, row.Inset(frame.Units(12f)), hint);
            HandleFriendRow(frame, row, hint.Name, hint.World, () =>
                OpenTellFromPeople(hint.Name, hint.World));
            shown++;
        }

        return shown;
    }

    private int DrawGateFriends(in AppletFrame frame, ref Stack stack, PearlSnapshot snapshot,
        IReadOnlyList<TalkPeer> peers, IReadOnlyList<GameFriend> friends, string query)
    {
        var shown = 0;
        for (var index = 0; index < snapshot.People.Length; index++)
        {
            var person = snapshot.People[index];
            if (AlreadyPeer(peers, person.DisplayName) || AlreadyGameFriend(friends, person.DisplayName))
            {
                continue;
            }

            if (query.Length >= 2 &&
                !person.DisplayName.Contains(query, StringComparison.OrdinalIgnoreCase) &&
                !person.Handle.Contains(query, StringComparison.OrdinalIgnoreCase) &&
                !person.PhoneNumber.Contains(query, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (shown == 0)
            {
                frame.Text.DrawIn(stack.Take(frame.Units(18f)), "Linkpearl",
                    new TextStyle(FontRole.CaptionStrong, frame.Theme.Palette.InkMuted));
            }

            var row = stack.Take(frame.Units(72f));
            CardChrome.DrawGold(frame, row);
            var inner = row.Inset(frame.Units(12f));
            DrawPerson(frame, inner.Inset(new Edges(0f, 0f, frame.Units(72f), 0f)), person);
            if (Chip(frame, inner.RightSlice(frame.Units(64f)).TopSlice(frame.Units(28f)), "Remove"))
            {
                pearl.RemoveFriend(person.Id);
            }
            else
            {
                HandleFriendRow(frame, row, person.DisplayName, string.Empty, () =>
                    messages.OpenProfile(TalkIds.Person(person.Id)));
            }

            shown++;
        }

        return shown;
    }

    private void OpenTellFromPeople(string name, string world)
    {
        selectedSection = SocialPane.Messages;
        messages.Open(talk.StartTell(name, world));
    }

    private void HandleFriendRow(in AppletFrame frame, Rect row, string name, string world, Action primary)
    {
        if (menu is not null)
        {
            return;
        }

        if (frame.Input.ConsumeClick(row, PointerButton.Secondary))
        {
            menu = new FriendMenu(name, world, ContactId(name), frame.Input.Pointer);
            return;
        }

        if (frame.Input.ConsumeClick(row))
        {
            primary();
        }
    }

    private void DrawFriendMenu(in AppletFrame frame, Rect bounds)
    {
        if (menu is not { } open)
        {
            return;
        }

        var labels = new List<string> { "Send tell", "Invite to party", "Report" };
        if (open.ContactId.Length > 0)
        {
            labels.Add("Linkpearl contact");
        }

        var width = frame.Units(176f);
        var rowH = frame.Units(34f);
        var height = rowH * labels.Count + frame.Units(8f);
        var left = Math.Clamp(open.At.X, bounds.Min.X, bounds.Max.X - width);
        var top = Math.Clamp(open.At.Y, bounds.Min.Y, bounds.Max.Y - height);
        var box = Rect.FromSize(new Vector2(left, top), new Vector2(width, height));
        var gold = frame.Theme.Palette.WarmAccent;
        var radius = frame.Units(10f);
        frame.Paint.Fill(box, frame.Theme.Palette.SurfaceRaised with { W = 0.98f }, radius);
        frame.Paint.Stroke(box, gold with { W = 0.55f }, frame.Theme.Metrics.Hairline, radius);
        for (var index = 0; index < labels.Count; index++)
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
                RunFriendMenu(labels[index], open);
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

    private void RunFriendMenu(string label, FriendMenu open)
    {
        menu = null;
        if (label == "Send tell" && open.Name.Length > 0)
        {
            OpenTellFromPeople(open.Name, open.World);
            return;
        }

        if (label == "Invite to party" && open.Name.Length > 0)
        {
            chat.InviteToParty(open.Name, open.World);
            return;
        }

        if (label == "Linkpearl contact" && open.ContactId.Length > 0)
        {
            messages.OpenProfile(open.ContactId);
            return;
        }

        if (label == "Report")
        {
            reportOpen = true;
            reportFresh = true;
            reportReason = 0;
            reportDetail = string.Empty;
            reportName = open.Name;
            reportWorld = open.World;
            reportId = BareUserId(open.ContactId);
            if (reportId.Length == 0)
            {
                reportId = FindGatePerson(open.Name)?.Id ?? open.Name;
            }
        }
    }

    private string ContactId(string name)
    {
        var snapshot = pearl.Current;
        if (snapshot.SignedIn)
        {
            for (var index = 0; index < snapshot.People.Length; index++)
            {
                if (snapshot.People[index].DisplayName.Equals(name, StringComparison.OrdinalIgnoreCase))
                {
                    return TalkIds.Person(snapshot.People[index].Id);
                }
            }
        }

        var peers = talk.Peers();
        for (var index = 0; index < peers.Count; index++)
        {
            if (peers[index].OnPearlgate && peers[index].Name.Equals(name, StringComparison.OrdinalIgnoreCase))
            {
                return peers[index].Id;
            }
        }

        return string.Empty;
    }

    private static string BareUserId(string contactId)
    {
        const string prefix = "person:";
        if (contactId.StartsWith(prefix, StringComparison.Ordinal))
        {
            return contactId[prefix.Length..];
        }

        return string.Empty;
    }

    private readonly record struct FriendMenu(string Name, string World, string ContactId, Vector2 At);

    private PearlPerson? FindGatePerson(string name)
    {
        if (name.Length == 0 || !pearl.Current.SignedIn)
        {
            return null;
        }

        var people = pearl.Current.People;
        for (var index = 0; index < people.Length; index++)
        {
            if (people[index].DisplayName.Equals(name, StringComparison.OrdinalIgnoreCase))
            {
                return people[index];
            }
        }

        return null;
    }

    private static bool HasGateLine(string handle, string number) =>
        handle.Length > 0 || number.Length > 0;

    private static string GateLine(string handle, string number)
    {
        var tag = handle.Length > 0 ? "@" + handle : string.Empty;
        var phone = number.Length > 0 ? LineNumbers.Show(number) : string.Empty;
        if (tag.Length > 0 && phone.Length > 0)
        {
            return tag + " · " + phone;
        }

        return tag.Length > 0 ? tag : phone;
    }

    private static void DrawGameFriend(in AppletFrame frame, Rect inset, GameFriend friend, PearlPerson? gate)
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
        if (gate is { } person && HasGateLine(person.Handle, person.PhoneNumber))
        {
            frame.Text.DrawIn(stack.Take(frame.Units(16f)), GateLine(person.Handle, person.PhoneNumber),
                new TextStyle(FontRole.Caption, frame.Theme.Palette.WarmAccent));
        }
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

    private void DrawPeer(in AppletFrame frame, Rect inset, TalkPeer peer)
    {
        var stack = new Stack(inset, StackAxis.Vertical, frame.Units(2f));
        frame.Text.DrawIn(stack.Take(frame.Units(20f)), peer.Name,
            new TextStyle(FontRole.BodyStrong, frame.Theme.Palette.Ink));
        var gate = peer.OnPearlgate ? "Pearlgate" : "Tell";
        var detail = peer.World.Length > 0 ? gate + " · " + peer.World : gate;
        var zone = WorldZones.ForPerson(string.Empty, peer.Id);
        if (zone.Length > 0)
        {
            detail += " · " + ZoneClock.Line(zone, display.Use24HourClock);
        }

        frame.Text.DrawIn(stack.Take(frame.Units(16f)), detail,
            new TextStyle(FontRole.Caption, frame.Theme.Palette.InkMuted));
        if (HasGateLine(peer.Handle, peer.Number))
        {
            frame.Text.DrawIn(stack.Take(frame.Units(16f)), GateLine(peer.Handle, peer.Number),
                new TextStyle(FontRole.Caption, frame.Theme.Palette.WarmAccent));
        }
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

    private void DrawPerson(in AppletFrame frame, Rect inset, PearlPerson person)
    {
        var stack = new Stack(inset, StackAxis.Vertical, frame.Units(3f));
        frame.Text.DrawIn(stack.Take(frame.Units(20f)), person.DisplayName,
            new TextStyle(FontRole.BodyStrong, frame.Theme.Palette.Ink));
        var gate = GateLine(person.Handle, person.PhoneNumber);
        var detail = gate.Length > 0 ? gate : "Pearlgate";
        var zone = WorldZones.ForPerson(person.TimeZoneId, person.Id);
        if (zone.Length > 0)
        {
            detail += " · " + ZoneClock.Line(zone, display.Use24HourClock);
        }

        frame.Text.DrawIn(stack.Take(frame.Units(18f)), detail,
            new TextStyle(FontRole.Caption, frame.Theme.Palette.WarmAccent));
    }

    private static void DrawEmpty(in AppletFrame frame, Rect area, string text)
    {
        frame.Text.DrawWrapped(area.TopSlice(frame.Units(72f)), text,
            new TextStyle(FontRole.Body, frame.Theme.Palette.InkMuted, TextAlign.Center));
    }
}