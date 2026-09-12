using System.Globalization;
using Linkpearl.Chat;
using Linkpearl.Net;
using Linkpearl.Platform;
using Linkpearl.Time;

namespace Linkpearl.Talk;

public sealed class TalkInbox : ITalk, IDisposable
{
    private const int MaxLiveLines = 500;
    private const int MaxTellLines = 400;

    private readonly IChatBridge chat;
    private readonly IPearlHub pearl;
    private readonly IClock clock;
    private readonly IGameSession game;
    private readonly TalkShelf shelf;
    private readonly object gate = new();
    private readonly Dictionary<string, Room> rooms = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> extraNotes = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> hidden = new(StringComparer.OrdinalIgnoreCase);
    private ulong boundId;
    private int generation;

    public event Action<string, TalkLine>? LinePosted;

    public TalkInbox(IChatBridge chat, IPearlHub pearl, IClock clock, IGameSession game, string talkDirectory)
    {
        this.chat = chat;
        this.pearl = pearl;
        this.clock = clock;
        this.game = game;
        shelf = new TalkShelf(talkDirectory);
        chat.LineReceived += HandleLine;
        game.CharacterChanged += HandleCharacterChanged;
        game.LoggedOut += HandleLoggedOut;
        BindCharacter();
    }

    public int Generation
    {
        get
        {
            lock (gate)
            {
                return generation;
            }
        }
    }

    public int UnreadTotal
    {
        get
        {
            lock (gate)
            {
                var total = 0;
                foreach (var room in rooms.Values)
                {
                    total += room.Unread;
                }

                return total;
            }
        }
    }

    public IReadOnlyList<TalkThread> Inbox()
    {
        lock (gate)
        {
            BindCharacter();
            EnsureRooms();
            var snapshot = pearl.Current;
            var list = new List<TalkThread>(rooms.Count + snapshot.Chats.Length + 4);
            foreach (var pair in rooms)
            {
                if (pair.Value.Kind == TalkKind.Live || hidden.Contains(pair.Key))
                {
                    continue;
                }

                list.Add(ToThread(pair.Value));
            }

            AppendPearl(list, snapshot);
            list.Sort(Compare);
            return list;
        }
    }

    public IReadOnlyList<TalkLine> Lines(string threadId)
    {
        if (threadId.StartsWith("pearl:", StringComparison.Ordinal))
        {
            var chatId = threadId["pearl:".Length..];
            pearl.WatchChat(chatId);
            var rows = pearl.LinesFor(chatId);
            var mapped = new TalkLine[rows.Count];
            for (var index = 0; index < rows.Count; index++)
            {
                var row = rows[index];
                mapped[index] = new TalkLine(row.Author, row.Body, clock.Now, row.Mine);
            }

            return mapped;
        }

        lock (gate)
        {
            BindCharacter();
            if (!rooms.TryGetValue(threadId, out var room))
            {
                return [];
            }

            return room.Lines.ToArray();
        }
    }

    public TalkThread? Find(string threadId)
    {
        lock (gate)
        {
            BindCharacter();
            EnsureRooms();
            if (rooms.TryGetValue(threadId, out var room))
            {
                return ToThread(room);
            }
        }

        var chats = pearl.Current.Chats;
        for (var index = 0; index < chats.Length; index++)
        {
            if (string.Equals(TalkIds.Pearl(chats[index].Id), threadId, StringComparison.Ordinal))
            {
                return PearlThread(chats[index], true);
            }
        }

        return null;
    }

    public IReadOnlyList<TalkPeer> Peers()
    {
        lock (gate)
        {
            BindCharacter();
            var snapshot = pearl.Current;
            var list = new List<TalkPeer>();
            foreach (var room in rooms.Values)
            {
                if (room.Kind != TalkKind.Tell)
                {
                    continue;
                }

                if (room.Lines.Count == 0 && room.Note.Length == 0 && room.LastAt == DateTimeOffset.MinValue)
                {
                    continue;
                }

                list.Add(ToPeer(room, snapshot));
            }

            list.Sort(static (left, right) => right.LastAt.CompareTo(left.LastAt));
            return list;
        }
    }

    public TalkPeer? FindPeer(string peerId)
    {
        lock (gate)
        {
            BindCharacter();
            var snapshot = pearl.Current;
            if (TalkIds.TryParsePerson(peerId, out var userId))
            {
                return PersonPeer(userId, snapshot);
            }

            if (TalkIds.TryParseTell(peerId, out var name, out var world))
            {
                if (rooms.TryGetValue(peerId, out var room))
                {
                    return ToPeer(room, snapshot);
                }

                return Overlay(new TalkPeer(peerId, peerId, name, world, NoteOf(peerId), DateTimeOffset.MinValue, 0,
                    false, string.Empty, string.Empty), snapshot);
            }
        }

        return null;
    }

    public IReadOnlyList<TalkThread> Urgent(int max)
    {
        var inbox = Inbox();
        var picked = new List<TalkThread>(Math.Max(max, 0));
        for (var index = 0; index < inbox.Count && picked.Count < max; index++)
        {
            if (inbox[index].Unread > 0)
            {
                picked.Add(inbox[index]);
            }
        }

        return picked;
    }

    public IReadOnlyList<GamePeerHint> SuggestTells()
    {
        var hints = new List<GamePeerHint>();
        var party = chat.PartyMembers;
        for (var index = 0; index < party.Count; index++)
        {
            hints.Add(new GamePeerHint(party[index].Name, party[index].World, "Party"));
        }

        lock (gate)
        {
            foreach (var room in rooms.Values)
            {
                if (room.Kind != TalkKind.Tell || room.Title.Length == 0)
                {
                    continue;
                }

                if (AlreadyHinted(hints, room.Title, room.World))
                {
                    continue;
                }

                hints.Add(new GamePeerHint(room.Title, room.World, "Tell"));
            }
        }

        var nearby = chat.NearbyPlayers;
        var added = 0;
        for (var index = 0; index < nearby.Count && added < 12; index++)
        {
            var peer = nearby[index];
            if (AlreadyHinted(hints, peer.Name, peer.World))
            {
                continue;
            }

            hints.Add(new GamePeerHint(peer.Name, peer.World, "Nearby"));
            added++;
        }

        var roster = chat.Friends;
        for (var index = 0; index < roster.Count; index++)
        {
            var friend = roster[index];
            if (AlreadyHinted(hints, friend.Name, friend.World))
            {
                continue;
            }

            hints.Add(new GamePeerHint(friend.Name, friend.World, friend.Online ? "Friend · Online" : "Friend"));
        }

        return hints;
    }

    public IReadOnlyList<GameFriend> Friends() => chat.Friends;

    public bool ShouldOfferFriend(string name, string world) => chat.ShouldOfferFriend(name, world);

    public void RequestFriend(string name, string world) => chat.RequestFriend(name, world);

    public IReadOnlyList<GamePeerHint> SearchNearby(string query)
    {
        var trimmed = query.Trim();
        if (trimmed.Length == 0)
        {
            return [];
        }

        var at = trimmed.LastIndexOf('@');
        var nameQuery = at < 0 ? trimmed : trimmed[..at].Trim();
        var worldQuery = at < 0 ? string.Empty : trimmed[(at + 1)..].Trim();
        if (nameQuery.Length == 0)
        {
            return [];
        }

        var hits = new List<GamePeerHint>();
        var nearby = chat.NearbyPlayers;
        var party = chat.PartyMembers;
        for (var index = 0; index < nearby.Count; index++)
        {
            var peer = nearby[index];
            if (!peer.Name.Contains(nameQuery, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (worldQuery.Length > 0 &&
                !peer.World.Contains(worldQuery, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var reason = PartyOwns(party, peer) ? "Party" : "Nearby";
            hits.Add(new GamePeerHint(peer.Name, peer.World, reason));
        }

        var roster = chat.Friends;
        for (var index = 0; index < roster.Count; index++)
        {
            var friend = roster[index];
            if (!friend.Name.Contains(nameQuery, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (worldQuery.Length > 0 &&
                !friend.World.Contains(worldQuery, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (AlreadyHinted(hits, friend.Name, friend.World))
            {
                continue;
            }

            hits.Add(new GamePeerHint(friend.Name, friend.World, friend.Online ? "Friend · Online" : "Friend"));
        }

        hits.Sort((left, right) => CompareNearby(left, right, nameQuery));
        if (hits.Count > 12)
        {
            hits.RemoveRange(12, hits.Count - 12);
        }

        return hits;
    }

    public void MarkRead(string threadId)
    {
        lock (gate)
        {
            if (rooms.TryGetValue(threadId, out var room) && room.Unread != 0)
            {
                room.Unread = 0;
                generation++;
                if (room.Kind == TalkKind.Tell)
                {
                    Flush();
                }
            }
        }
    }

    public void Send(string threadId, string body)
    {
        var trimmed = body.Trim();
        if (trimmed.Length == 0)
        {
            return;
        }

        if (threadId.StartsWith("pearl:", StringComparison.Ordinal))
        {
            pearl.SendChat(threadId["pearl:".Length..], trimmed);
            lock (gate)
            {
                generation++;
            }

            return;
        }

        if (!chat.CanSend)
        {
            chat.Print("Chat is not available right now.");
            return;
        }

        if (threadId is TalkIds.Party or TalkIds.LiveParty)
        {
            if (!chat.InParty)
            {
                chat.Print("You are not in a party.");
                return;
            }

            chat.Send(GameChannel.Party, 0, trimmed);
            RememberOutgoing(game.Character.Name, game.Character.WorldName, trimmed, GameChannel.Party);
            return;
        }

        if (threadId == TalkIds.Alliance)
        {
            chat.Send(GameChannel.Alliance, 0, trimmed);
            RememberOutgoing(game.Character.Name, game.Character.WorldName, trimmed, GameChannel.Alliance);
            return;
        }

        if (threadId == TalkIds.FreeCompany)
        {
            chat.Send(GameChannel.FreeCompany, 0, trimmed);
            RememberOutgoing(game.Character.Name, game.Character.WorldName, trimmed, GameChannel.FreeCompany);
            return;
        }

        if (threadId == TalkIds.Novice)
        {
            chat.Send(GameChannel.Novice, 0, trimmed);
            RememberOutgoing(game.Character.Name, game.Character.WorldName, trimmed, GameChannel.Novice);
            return;
        }

        if (threadId is TalkIds.Live or TalkIds.LiveSay)
        {
            chat.Send(GameChannel.Say, 0, trimmed);
            RememberOutgoing(game.Character.Name, game.Character.WorldName, trimmed, GameChannel.Say);
            return;
        }

        if (threadId == TalkIds.LiveShout)
        {
            chat.Send(GameChannel.Shout, 0, trimmed);
            RememberOutgoing(game.Character.Name, game.Character.WorldName, trimmed, GameChannel.Shout);
            return;
        }

        if (threadId == TalkIds.LiveYell)
        {
            chat.Send(GameChannel.Yell, 0, trimmed);
            RememberOutgoing(game.Character.Name, game.Character.WorldName, trimmed, GameChannel.Yell);
            return;
        }

        if (TalkIds.TryParseSlot(threadId, "ls:", out var ls))
        {
            chat.Send(GameChannel.Linkshell, ls, trimmed);
            RememberOutgoing(game.Character.Name, game.Character.WorldName, trimmed, GameChannel.Linkshell, ls);
            return;
        }

        if (TalkIds.TryParseSlot(threadId, "cwls:", out var cwls))
        {
            chat.Send(GameChannel.CrossWorldLinkshell, cwls, trimmed);
            RememberOutgoing(game.Character.Name, game.Character.WorldName, trimmed, GameChannel.CrossWorldLinkshell,
                cwls);
            return;
        }

        if (TalkIds.TryParseTell(threadId, out var name, out var world))
        {
            if (world.Length == 0)
            {
                lock (gate)
                {
                    if (rooms.TryGetValue(threadId, out var room) && room.World.Length > 0)
                    {
                        world = room.World;
                    }
                }
            }

            chat.SendTell(name, world, trimmed);
            RememberOutgoing(name, world, trimmed);
        }
    }

    public string StartTell(string name, string world)
    {
        var trimmedName = name.Trim();
        var trimmedWorld = world.Trim();
        lock (gate)
        {
            BindCharacter();
            var room = FindTellRoom(trimmedName, trimmedWorld);
            if (room is null)
            {
                var id = TalkIds.Tell(trimmedName, trimmedWorld);
                room = new Room
                {
                    Id = id,
                    Kind = TalkKind.Tell,
                    Title = trimmedName,
                    World = trimmedWorld,
                    Subtitle = trimmedWorld.Length > 0 ? trimmedWorld : "Tell",
                    Note = NoteOf(id),
                };
                rooms[id] = room;
            }

            if (trimmedWorld.Length > 0 && room.World.Length == 0)
            {
                room.World = trimmedWorld;
                room.Subtitle = trimmedWorld;
            }

            room.LastAt = clock.Now;
            Reveal(room.Id);
            generation++;
            Flush();
            return room.Id;
        }
    }

    public void HideThread(string threadId)
    {
        if (threadId.Length == 0)
        {
            return;
        }

        lock (gate)
        {
            BindCharacter();
            if (!hidden.Add(threadId))
            {
                return;
            }

            generation++;
            Flush();
        }
    }

    public void SetNote(string peerId, string note)
    {
        var trimmed = note.Trim();
        lock (gate)
        {
            BindCharacter();
            if (TalkIds.TryParseTell(peerId, out var name, out var world))
            {
                if (!rooms.TryGetValue(peerId, out var room))
                {
                    room = new Room
                    {
                        Id = peerId,
                        Kind = TalkKind.Tell,
                        Title = name,
                        World = world,
                        Subtitle = world.Length > 0 ? world : "Tell",
                    };
                    rooms[peerId] = room;
                }

                room.Note = trimmed;
            }
            else
            {
                extraNotes[peerId] = trimmed;
            }

            generation++;
            Flush();
        }
    }

    public void Dispose()
    {
        chat.LineReceived -= HandleLine;
        game.CharacterChanged -= HandleCharacterChanged;
        game.LoggedOut -= HandleLoggedOut;
        lock (gate)
        {
            Flush();
        }
    }

    private void HandleCharacterChanged(CharacterIdentity identity)
    {
        lock (gate)
        {
            BindCharacter();
        }
    }

    private void HandleLoggedOut()
    {
        lock (gate)
        {
            Flush();
            rooms.Clear();
            extraNotes.Clear();
            hidden.Clear();
            boundId = 0;
            generation++;
        }
    }

    private void BindCharacter()
    {
        var id = game.Character.ContentId;
        if (id == boundId)
        {
            return;
        }

        if (boundId != 0UL)
        {
            Flush();
        }

        if (boundId == 0UL && id != 0UL)
        {
            boundId = id;
            AbsorbShelf(id);
            Flush();
            generation++;
            return;
        }

        ForgetTells();
        ForgetLive();
        extraNotes.Clear();
        hidden.Clear();
        boundId = id;
        if (id == 0UL)
        {
            generation++;
            return;
        }

        AbsorbShelf(id);
        generation++;
    }

    private void AbsorbShelf(ulong contentId)
    {
        var file = shelf.Load(contentId);
        if (file.Hidden is { Length: > 0 } hid)
        {
            for (var index = 0; index < hid.Length; index++)
            {
                if (hid[index] is { Length: > 0 } id)
                {
                    hidden.Add(id);
                }
            }
        }

        if (file.Notes is not null)
        {
            foreach (var pair in file.Notes)
            {
                if (!extraNotes.ContainsKey(pair.Key))
                {
                    extraNotes[pair.Key] = pair.Value;
                }
            }
        }

        var threads = file.Threads;
        if (threads is null)
        {
            return;
        }

        for (var index = 0; index < threads.Length; index++)
        {
            var stored = threads[index];
            if (stored.Id is not { Length: > 0 } roomId)
            {
                continue;
            }

            var loaded = FromShelf(stored);
            if (!rooms.TryGetValue(roomId, out var live))
            {
                rooms[roomId] = loaded;
                continue;
            }

            MergeTell(live, loaded);
        }
    }

    private static void MergeTell(Room live, Room loaded)
    {
        if (live.Title.Length == 0)
        {
            live.Title = loaded.Title;
        }

        if (live.World.Length == 0)
        {
            live.World = loaded.World;
            live.Subtitle = loaded.Subtitle;
        }

        if (live.Note.Length == 0)
        {
            live.Note = loaded.Note;
        }

        if (loaded.Lines.Count == 0)
        {
            return;
        }

        var cutoff = live.Lines.Count > 0 ? live.Lines[0].At : DateTimeOffset.MaxValue;
        var insert = 0;
        for (var index = 0; index < loaded.Lines.Count; index++)
        {
            if (loaded.Lines[index].At >= cutoff)
            {
                break;
            }

            insert = index + 1;
        }

        if (insert > 0)
        {
            live.Lines.InsertRange(0, loaded.Lines.GetRange(0, insert));
            if (live.Lines.Count > MaxTellLines)
            {
                live.Lines.RemoveRange(0, live.Lines.Count - MaxTellLines);
            }
        }
    }

    private void ForgetLive()
    {
        if (!rooms.TryGetValue(TalkIds.Live, out var live))
        {
            return;
        }

        live.Lines.Clear();
        live.Preview = string.Empty;
        live.LastAt = DateTimeOffset.MinValue;
        live.Unread = 0;
    }

    private void ForgetTells()
    {
        var drop = new List<string>();
        foreach (var pair in rooms)
        {
            if (pair.Value.Kind == TalkKind.Tell)
            {
                drop.Add(pair.Key);
            }
        }

        for (var index = 0; index < drop.Count; index++)
        {
            rooms.Remove(drop[index]);
        }
    }

    private void Flush()
    {
        if (boundId == 0UL)
        {
            return;
        }

        var tells = new List<TalkShelf.RoomSnapshot>();
        foreach (var room in rooms.Values)
        {
            if (room.Kind != TalkKind.Tell)
            {
                continue;
            }

            tells.Add(new TalkShelf.RoomSnapshot(room.Id, room.Kind, room.Title, room.World, room.Note, room.Preview,
                room.LastAt, room.Unread, room.Lines));
        }

        shelf.Save(boundId, tells, extraNotes, hidden);
    }

    private void HandleLine(GameChatLine line)
    {
        var id = ThreadIdOf(line);
        if (id.Length == 0)
        {
            return;
        }

        TalkLine? posted = null;
        lock (gate)
        {
            BindCharacter();
            if (!rooms.TryGetValue(id, out var room))
            {
                room = NewRoom(id, line);
                room.Note = NoteOf(id);
                rooms[id] = room;
            }

            if (IsDuplicate(room, line))
            {
                if (line.Channel == GameChannel.Party && rooms.TryGetValue(TalkIds.Live, out var live))
                {
                    IsDuplicate(live, line);
                }

                return;
            }

            var added = new TalkLine(line.Sender, line.Body, line.Received, line.Mine, TagOf(line.Channel),
                line.SenderWorld);
            room.Lines.Add(added);
            var cap = room.Kind == TalkKind.Tell ? MaxTellLines : MaxLiveLines;
            if (room.Lines.Count > cap)
            {
                room.Lines.RemoveRange(0, room.Lines.Count - cap);
            }

            room.LastAt = line.Received;
            room.Preview = line.Body;
            if (line.Channel == GameChannel.Tell)
            {
                RememberTellPeer(room, line);
            }

            if (!line.Mine && room.Kind != TalkKind.Live)
            {
                room.Unread++;
            }

            generation++;
            Reveal(id);
            if (room.Kind == TalkKind.Tell)
            {
                Flush();
            }

            posted = added;
            if (line.Channel == GameChannel.Party)
            {
                MirrorLive(line, added);
            }
        }

        if (posted is { } arrived)
        {
            LinePosted?.Invoke(id, arrived);
        }
    }

    private void MirrorLive(GameChatLine line, TalkLine added)
    {
        if (!rooms.TryGetValue(TalkIds.Live, out var live))
        {
            EnsureChannel(TalkIds.Live, TalkKind.Live, 0, "Feed",
                game.ZoneName.Length > 0 ? game.ZoneName : "Say · Shout · Yell");
            live = rooms[TalkIds.Live];
        }

        if (IsDuplicate(live, line))
        {
            return;
        }

        live.Lines.Add(added);
        if (live.Lines.Count > MaxLiveLines)
        {
            live.Lines.RemoveRange(0, live.Lines.Count - MaxLiveLines);
        }

        live.LastAt = line.Received;
        live.Preview = line.Body;
    }

    private void RememberOutgoing(string sender, string world, string body, GameChannel channel = GameChannel.Tell,
        int channelIndex = 0) =>
        HandleLine(new GameChatLine(channel, channelIndex, sender, world, body, clock.Now, true));

    private bool IsDuplicate(Room room, GameChatLine line)
    {
        if (room.Lines.Count == 0)
        {
            return false;
        }

        var self = game.Character.Name;
        var tag = TagOf(line.Channel);
        for (var index = room.Lines.Count - 1; index >= 0; index--)
        {
            var last = room.Lines[index];
            if (Math.Abs((line.Received - last.At).TotalSeconds) >= 4d)
            {
                break;
            }

            if (last.Body != line.Body || last.Tag != tag)
            {
                continue;
            }

            if (last.Mine == line.Mine)
            {
                return true;
            }

            var selfLine = PlayerNames.Same(line.Sender, self) || PlayerNames.Same(last.Sender, self);
            if (!selfLine && !last.Mine && !line.Mine)
            {
                continue;
            }

            if (last.Mine && !line.Mine && selfLine)
            {
                return true;
            }

            if (!last.Mine && line.Mine && selfLine)
            {
                room.Lines[index] = last with { Mine = true };
                return true;
            }
        }

        return false;
    }

    private static void RememberTellPeer(Room room, GameChatLine line)
    {
        if (line.Mine)
        {
            if (TalkIds.TryParseTell(room.Id, out var name, out var world))
            {
                if (room.Title.Length == 0)
                {
                    room.Title = name;
                }

                if (room.World.Length == 0)
                {
                    room.World = world;
                }
            }

            room.Subtitle = room.World.Length > 0 ? room.World : "Tell";
            return;
        }

        room.Title = line.Sender;
        room.World = line.SenderWorld;
        room.Subtitle = line.SenderWorld.Length > 0 ? line.SenderWorld : "Tell";
    }

    private void EnsureRooms()
    {
        EnsureChannel(TalkIds.Live, TalkKind.Live, 0, "Feed",
            game.ZoneName.Length > 0 ? game.ZoneName : "Say · Shout · Yell");
        EnsureChannel(TalkIds.Party, TalkKind.Party, 0, "Party",
            chat.InParty ? PartySubtitle() : "Not in a party");
        EnsureChannel(TalkIds.FreeCompany, TalkKind.FreeCompany, 0, "Free Company", "Company chat");
        EnsureChannel(TalkIds.Novice, TalkKind.Novice, 0, "Novice", "Novice network");
        if (chat.InAlliance)
        {
            EnsureChannel(TalkIds.Alliance, TalkKind.Alliance, 0, "Alliance", "Alliance chat");
        }

        var shells = chat.LinkshellNames(false);
        for (var index = 0; index < shells.Count; index++)
        {
            var name = shells[index];
            if (name.Length == 0)
            {
                continue;
            }

            var slot = index + 1;
            EnsureChannel(TalkIds.Linkshell(slot), TalkKind.Linkshell, slot, name, "Linkshell " + slot.ToString(CultureInfo.InvariantCulture));
        }

        var cross = chat.LinkshellNames(true);
        for (var index = 0; index < cross.Count; index++)
        {
            var name = cross[index];
            if (name.Length == 0)
            {
                continue;
            }

            var slot = index + 1;
            EnsureChannel(TalkIds.CrossWorld(slot), TalkKind.CrossWorldLinkshell, slot, name,
                "Cross-world " + slot.ToString(CultureInfo.InvariantCulture));
        }
    }

    private void EnsureChannel(string id, TalkKind kind, int slot, string title, string subtitle)
    {
        if (rooms.TryGetValue(id, out var room))
        {
            room.Title = title;
            room.Subtitle = subtitle;
            room.Kind = kind;
            room.ChannelIndex = slot;
            return;
        }

        rooms[id] = new Room
        {
            Id = id,
            Kind = kind,
            ChannelIndex = slot,
            Title = title,
            Subtitle = subtitle,
        };
    }

    private string PartySubtitle()
    {
        var count = chat.PartyMembers.Count + (game.IsLoggedIn ? 1 : 0);
        if (game.IsInDuty && game.ZoneName.Length > 0)
        {
            return game.ZoneName;
        }

        return count > 1
            ? count.ToString(CultureInfo.InvariantCulture) + " in party"
            : "Party chat";
    }

    private TalkThread ToThread(Room room)
    {
        var canSend = room.Kind != TalkKind.Pearl && chat.CanSend && RoomCanSend(room);
        var preview = !string.IsNullOrEmpty(room.Preview)
            ? room.Preview
            : room.Kind == TalkKind.Party && !chat.InParty
                ? "Join a party to talk here."
                : "No messages yet";
        var pinned = room.Kind == TalkKind.Party && chat.InParty;
        return new TalkThread(room.Id, room.Kind, room.ChannelIndex, room.Title, room.Subtitle, preview, room.LastAt,
            room.Unread, pinned, canSend);
    }

    private bool RoomCanSend(Room room) => room.Kind switch
    {
        TalkKind.Party => chat.InParty,
        TalkKind.Alliance => chat.InAlliance,
        TalkKind.Tell => true,
        TalkKind.Linkshell => true,
        TalkKind.CrossWorldLinkshell => true,
        TalkKind.FreeCompany => true,
        TalkKind.Novice => true,
        TalkKind.Live => true,
        _ => false,
    };

    private TalkPeer ToPeer(Room room, PearlSnapshot snapshot)
    {
        var tellId = room.Id;
        var note = room.Note.Length > 0 ? room.Note : NoteOf(tellId);
        return Overlay(new TalkPeer(tellId, tellId, room.Title, room.World, note, room.LastAt, room.Lines.Count, false,
            string.Empty, string.Empty), snapshot);
    }

    private TalkPeer? PersonPeer(string userId, PearlSnapshot snapshot)
    {
        for (var index = 0; index < snapshot.People.Length; index++)
        {
            var person = snapshot.People[index];
            if (!string.Equals(person.Id, userId, StringComparison.Ordinal))
            {
                continue;
            }

            var tellId = MatchingTellId(person.DisplayName);
            var note = tellId.Length > 0 && rooms.TryGetValue(tellId, out var room)
                ? room.Note
                : NoteOf(TalkIds.Person(userId));
            var last = DateTimeOffset.MinValue;
            var lines = 0;
            if (tellId.Length > 0 && rooms.TryGetValue(tellId, out var saved))
            {
                last = saved.LastAt;
                lines = saved.Lines.Count;
                if (saved.Note.Length > 0)
                {
                    note = saved.Note;
                }
            }

            return new TalkPeer(TalkIds.Person(userId), tellId, person.DisplayName, string.Empty, note, last, lines,
                true, person.Handle, person.PhoneNumber);
        }

        return null;
    }

    private static TalkPeer Overlay(TalkPeer peer, PearlSnapshot snapshot)
    {
        for (var index = 0; index < snapshot.People.Length; index++)
        {
            var person = snapshot.People[index];
            if (!person.DisplayName.Equals(peer.Name, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            return peer with
            {
                OnPearlgate = true,
                Handle = person.Handle,
                Number = person.PhoneNumber,
            };
        }

        return peer;
    }

    private string MatchingTellId(string name)
    {
        foreach (var room in rooms.Values)
        {
            if (room.Kind == TalkKind.Tell && room.Title.Equals(name, StringComparison.OrdinalIgnoreCase))
            {
                return room.Id;
            }
        }

        return string.Empty;
    }

    private Room? FindTellRoom(string name, string world)
    {
        if (name.Length == 0)
        {
            return null;
        }

        if (rooms.TryGetValue(TalkIds.Tell(name, world), out var exact))
        {
            return exact;
        }

        Room? match = null;
        foreach (var room in rooms.Values)
        {
            if (room.Kind != TalkKind.Tell || !TellNamesMatch(room, name))
            {
                continue;
            }

            if (world.Length > 0 && room.World.Length > 0 &&
                !room.World.Equals(world, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (match is null || room.LastAt > match.LastAt || room.Lines.Count > match.Lines.Count)
            {
                match = room;
            }
        }

        return match;
    }

    private static bool TellNamesMatch(Room room, string name)
    {
        if (room.Title.Equals(name, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return TalkIds.TryParseTell(room.Id, out var stored, out _) &&
               stored.Equals(name, StringComparison.OrdinalIgnoreCase);
    }

    private bool Reveal(string threadId)
    {
        if (!hidden.Remove(threadId))
        {
            return false;
        }

        generation++;
        return true;
    }

    private string NoteOf(string id) => extraNotes.TryGetValue(id, out var note) ? note : string.Empty;

    private static Room FromShelf(TalkShelf.ShelfThread stored)
    {
        var id = stored.Id ?? string.Empty;
        var parsedName = string.Empty;
        var parsedWorld = string.Empty;
        if (TalkIds.TryParseTell(id, out var name, out var world))
        {
            parsedName = name;
            parsedWorld = world;
        }

        var title = stored.Title is { Length: > 0 } savedTitle ? savedTitle : parsedName;
        var savedWorld = stored.World is { Length: > 0 } w ? w : parsedWorld;
        var room = new Room
        {
            Id = id,
            Kind = TalkKind.Tell,
            Title = title,
            World = savedWorld,
            Subtitle = savedWorld.Length > 0 ? savedWorld : "Tell",
            Note = stored.Note ?? string.Empty,
            Preview = stored.Preview ?? string.Empty,
            LastAt = UnixOrMin(stored.LastAtUnix),
            Unread = Math.Max(stored.Unread, 0),
        };
        var lines = stored.Lines;
        if (lines is not null)
        {
            for (var index = 0; index < lines.Length; index++)
            {
                var line = lines[index];
                var at = UnixOrMin(line.AtUnix);
                room.Lines.Add(new TalkLine(line.Sender ?? string.Empty, line.Body ?? string.Empty, at, line.Mine));
            }
        }

        return room;
    }

    private void AppendPearl(List<TalkThread> list, PearlSnapshot snapshot)
    {
        for (var index = 0; index < snapshot.Chats.Length; index++)
        {
            var thread = PearlThread(snapshot.Chats[index], snapshot.SignedIn);
            if (hidden.Contains(thread.Id))
            {
                continue;
            }

            list.Add(thread);
        }
    }

    private static TalkThread PearlThread(PearlChat chat, bool canSend)
    {
        DateTimeOffset last;
        try
        {
            last = chat.LastMessageAtUnix > 0
                ? DateTimeOffset.FromUnixTimeSeconds(chat.LastMessageAtUnix)
                : DateTimeOffset.MinValue;
        }
        catch (ArgumentOutOfRangeException)
        {
            last = DateTimeOffset.MinValue;
        }
        var preview = string.IsNullOrEmpty(chat.Preview) ? "No messages yet" : chat.Preview;
        return new TalkThread(TalkIds.Pearl(chat.Id ?? string.Empty), TalkKind.Pearl, 0,
            chat.Title ?? string.Empty, "Pearlgate", preview, last,
            chat.UnreadCount, false, canSend);
    }

    private static Room NewRoom(string id, GameChatLine line)
    {
        var kind = KindOf(line.Channel);
        var title = kind == TalkKind.Tell ? line.Sender : TitleOf(kind, line.ChannelIndex);
        var subtitle = kind == TalkKind.Tell
            ? (line.SenderWorld.Length > 0 ? line.SenderWorld : "Tell")
            : SubtitleOf(kind, line.ChannelIndex);
        if (kind == TalkKind.Tell && line.Mine && TalkIds.TryParseTell(id, out var name, out var world))
        {
            title = name;
            subtitle = world.Length > 0 ? world : "Tell";
        }

        return new Room
        {
            Id = id,
            Kind = kind,
            ChannelIndex = line.ChannelIndex,
            Title = title,
            Subtitle = subtitle,
            World = kind == TalkKind.Tell && line.Mine && TalkIds.TryParseTell(id, out _, out var peerWorld)
                ? peerWorld
                : line.SenderWorld,
        };
    }

    private static string ThreadIdOf(GameChatLine line) => line.Channel switch
    {
        GameChannel.Party => TalkIds.Party,
        GameChannel.Alliance => TalkIds.Alliance,
        GameChannel.FreeCompany => TalkIds.FreeCompany,
        GameChannel.Novice => TalkIds.Novice,
        GameChannel.Tell => TalkIds.Tell(line.Sender, line.SenderWorld),
        GameChannel.Linkshell => TalkIds.Linkshell(line.ChannelIndex),
        GameChannel.CrossWorldLinkshell => TalkIds.CrossWorld(line.ChannelIndex),
        GameChannel.Say or GameChannel.Shout or GameChannel.Yell => TalkIds.Live,
        _ => string.Empty,
    };

    private static TalkKind KindOf(GameChannel channel) => channel switch
    {
        GameChannel.Party => TalkKind.Party,
        GameChannel.Alliance => TalkKind.Alliance,
        GameChannel.FreeCompany => TalkKind.FreeCompany,
        GameChannel.Novice => TalkKind.Novice,
        GameChannel.Tell => TalkKind.Tell,
        GameChannel.Linkshell => TalkKind.Linkshell,
        GameChannel.CrossWorldLinkshell => TalkKind.CrossWorldLinkshell,
        GameChannel.Say or GameChannel.Shout or GameChannel.Yell => TalkKind.Live,
        _ => TalkKind.Party,
    };

    private static string TitleOf(TalkKind kind, int slot) => kind switch
    {
        TalkKind.Party => "Party",
        TalkKind.Alliance => "Alliance",
        TalkKind.FreeCompany => "Free Company",
        TalkKind.Novice => "Novice",
        TalkKind.Linkshell => "Linkshell " + slot.ToString(CultureInfo.InvariantCulture),
        TalkKind.CrossWorldLinkshell => "Cross-world " + slot.ToString(CultureInfo.InvariantCulture),
        TalkKind.Live => "Feed",
        _ => "Chat",
    };

    private static string SubtitleOf(TalkKind kind, int slot) => kind switch
    {
        TalkKind.Party => "Party chat",
        TalkKind.Alliance => "Alliance chat",
        TalkKind.FreeCompany => "Company chat",
        TalkKind.Novice => "Novice network",
        TalkKind.Linkshell => "LS" + slot.ToString(CultureInfo.InvariantCulture),
        TalkKind.CrossWorldLinkshell => "CWLS" + slot.ToString(CultureInfo.InvariantCulture),
        TalkKind.Live => "Say · Shout · Yell",
        _ => string.Empty,
    };

    private static string TagOf(GameChannel channel) => channel switch
    {
        GameChannel.Say => "SAY",
        GameChannel.Shout => "SHOUT",
        GameChannel.Yell => "YELL",
        GameChannel.Party => "PARTY",
        _ => string.Empty,
    };

    private static int Compare(TalkThread left, TalkThread right)
    {
        if (left.Pinned != right.Pinned)
        {
            return left.Pinned ? -1 : 1;
        }

        var time = right.LastAt.CompareTo(left.LastAt);
        if (time != 0)
        {
            return time;
        }

        return string.Compare(left.Title, right.Title, StringComparison.OrdinalIgnoreCase);
    }

    private static int CompareNearby(GamePeerHint left, GamePeerHint right, string nameQuery)
    {
        var leftPrefix = left.Name.StartsWith(nameQuery, StringComparison.OrdinalIgnoreCase);
        var rightPrefix = right.Name.StartsWith(nameQuery, StringComparison.OrdinalIgnoreCase);
        if (leftPrefix != rightPrefix)
        {
            return leftPrefix ? -1 : 1;
        }

        var party = string.CompareOrdinal(left.Reason, "Party") == 0;
        var otherParty = string.CompareOrdinal(right.Reason, "Party") == 0;
        if (party != otherParty)
        {
            return party ? -1 : 1;
        }

        return string.Compare(left.Name, right.Name, StringComparison.OrdinalIgnoreCase);
    }

    private static bool PartyOwns(IReadOnlyList<GamePeer> party, GamePeer peer)
    {
        for (var index = 0; index < party.Count; index++)
        {
            if (party[index].Name.Equals(peer.Name, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static bool AlreadyHinted(List<GamePeerHint> hints, string name, string world)
    {
        for (var index = 0; index < hints.Count; index++)
        {
            if (hints[index].Name.Equals(name, StringComparison.OrdinalIgnoreCase) &&
                hints[index].World.Equals(world, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static DateTimeOffset UnixOrMin(long unix)
    {
        if (unix <= 0)
        {
            return DateTimeOffset.MinValue;
        }

        try
        {
            return DateTimeOffset.FromUnixTimeSeconds(unix);
        }
        catch (ArgumentOutOfRangeException)
        {
            return DateTimeOffset.MinValue;
        }
    }

    private sealed class Room
    {
        public string Id = string.Empty;
        public TalkKind Kind;
        public int ChannelIndex;
        public string Title = string.Empty;
        public string Subtitle = string.Empty;
        public string World = string.Empty;
        public string Note = string.Empty;
        public string Preview = string.Empty;
        public DateTimeOffset LastAt;
        public int Unread;
        public List<TalkLine> Lines { get; } = [];
    }
}
