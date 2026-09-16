using Dalamud.Game.Chat;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.System.String;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Info;
using FFXIVClientStructs.FFXIV.Client.UI.Shell;
using Linkpearl.Chat;
using WorldSheet = Lumina.Excel.Sheets.World;
using TerritorySheet = Lumina.Excel.Sheets.TerritoryType;
using EmoteSheet = Lumina.Excel.Sheets.Emote;

namespace Linkpearl.Platform.Ffxiv;

public sealed class FfxivChatBridge : IChatBridge, IDisposable
{
    private const int LinkshellSlots = 8;
    private const int MaxCommandBytes = 500;

    private readonly IChatGui chat;
    private readonly IClientState clientState;
    private readonly IPartyList party;
    private readonly IObjectTable objects;
    private readonly IDataManager data;
    private readonly IGameSession session;
    private readonly IFramework framework;
    private readonly IPluginLog log;
    private readonly ITargetManager targets;
    private readonly object friendGate = new();
    private readonly object commandGate = new();
    private readonly Queue<OutgoingLine> outgoing = new();
    private readonly HashSet<string> offeredFriends = new(StringComparer.OrdinalIgnoreCase);
    private GameFriend[] friends = [];
    private GameFriend[] pendingFriends = [];
    private bool askedFriendList;
    private string pendingAddTarget = string.Empty;
    private int pendingAddWait;
    private string pendingTellName = string.Empty;
    private string pendingTellWorld = string.Empty;
    private readonly Dictionary<string, string> emoteCommands = new(StringComparer.OrdinalIgnoreCase);
    private bool emotesLoaded;

    public FfxivChatBridge(IChatGui chat, IClientState clientState, IPartyList party, IObjectTable objects,
        IDataManager data, IGameSession session, IFramework framework, IPluginLog log, ITargetManager targets)
    {
        this.chat = chat;
        this.clientState = clientState;
        this.party = party;
        this.objects = objects;
        this.data = data;
        this.session = session;
        this.framework = framework;
        this.log = log;
        this.targets = targets;
        chat.ChatMessage += HandleChatMessage;
        framework.Update += HandleUpdate;
    }

    public event Action<GameChatLine>? LineReceived;

    public bool CanSend
    {
        get
        {
            if (!clientState.IsLoggedIn || session.IsInCutscene)
            {
                return false;
            }

            unsafe
            {
                var shell = RaptureShellModule.Instance();
                return shell is not null && !shell->IsTextCommandUnavailable;
            }
        }
    }

    public bool InParty => party.Length > 0;

    public bool InAlliance => party.IsAlliance;

    public IReadOnlyList<GamePeer> PartyMembers
    {
        get
        {
            var local = session.Character.Name;
            var list = new List<GamePeer>(party.Length);
            for (var index = 0; index < party.Length; index++)
            {
                var member = party[index];
                if (member is null)
                {
                    continue;
                }

                var name = MemberName(member);
                if (name.Length == 0 || string.Equals(name, local, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                list.Add(new GamePeer(name, WorldName(member.World.RowId)));
            }

            return list;
        }
    }

    public IReadOnlyList<GamePeer> NearbyPlayers
    {
        get
        {
            var local = session.Character.Name;
            var list = new List<GamePeer>();
            foreach (var obj in objects)
            {
                if (obj is not IPlayerCharacter player)
                {
                    continue;
                }

                RememberPeer(list, player.Name.TextValue.Trim(), WorldName(player.HomeWorld.RowId), local);
            }

            for (var index = 0; index < party.Length; index++)
            {
                var member = party[index];
                if (member is null)
                {
                    continue;
                }

                var name = MemberName(member);
                if (Named(list, name))
                {
                    continue;
                }

                RememberPeer(list, name, WorldName(member.World.RowId), local);
            }

            return list;
        }
    }

    public IReadOnlyList<GameFriend> Friends
    {
        get
        {
            lock (friendGate)
            {
                return friends;
            }
        }
    }

    public bool ShouldOfferFriend(string characterName, string world)
    {
        if (!TryFormatPlayer(characterName, world, out var target))
        {
            return false;
        }

        lock (friendGate)
        {
            return !NamedFriend(friends, characterName, world) && !offeredFriends.Contains(target);
        }
    }

    public void RequestFriend(string characterName, string world)
    {
        if (!TryFormatPlayer(characterName, world, out var target))
        {
            return;
        }

        SplitFriendTarget(target, out var name, out var home);
        lock (friendGate)
        {
            if (NamedFriend(friends, name, home))
            {
                return;
            }

            offeredFriends.Add(target);
        }

        QueueFriend("add", name, home);
        chat.Print("Adding " + name + " as a friend.", "Linkpearl");
    }

    public void Send(GameChannel channel, int channelIndex, string body)
    {
        var text = SanitizeBody(body);
        if (text.Length == 0)
        {
            return;
        }

        var command = channel switch
        {
            GameChannel.Party => "/p " + text,
            GameChannel.Alliance => "/a " + text,
            GameChannel.Linkshell when channelIndex is >= 1 and <= 8 => "/l" + channelIndex + " " + text,
            GameChannel.CrossWorldLinkshell when channelIndex is >= 1 and <= 8 => "/cwl" + channelIndex + " " + text,
            GameChannel.FreeCompany => "/fc " + text,
            GameChannel.Novice => "/n " + text,
            GameChannel.Say => "/s " + text,
            GameChannel.Yell => "/y " + text,
            GameChannel.Shout => "/sh " + text,
            _ => string.Empty,
        };
        Queue(command);
    }

    public void SendTell(string characterName, string world, string body)
    {
        var name = StripTellToken(characterName);
        var text = SanitizeBody(body);
        if (name.Length == 0 || text.Length == 0)
        {
            return;
        }

        var home = StripTellToken(world);
        TakeHome(ref name, ref home);
        PeelWorld(ref name, home);
        if (name.Length == 0)
        {
            return;
        }

        if (TryResolvePlayer(name, out var liveName, out var liveWorld))
        {
            name = liveName;
            if (liveWorld.Length > 0)
            {
                home = liveWorld;
            }
        }
        else
        {
            name = CanonicalName(name);
        }

        home = StripTellToken(home);
        if (home.Length == 0)
        {
            chat.Print("Linkpearl needs their world to send a tell.", "Linkpearl");
            return;
        }

        pendingTellName = name;
        pendingTellWorld = home;
        Queue("/tell " + name + "@" + home + " " + text);
    }

    public void InviteToParty(string characterName, string world)
    {
        if (!TryFormatPlayer(characterName, world, out var target))
        {
            return;
        }

        SplitFriendTarget(target, out var name, out var home);
        QueueFriend("invite", name, home);
    }

    public void TargetPlayer(string characterName, string world)
    {
        var name = characterName.Trim();
        var home = world.Trim();
        if (name.Length == 0)
        {
            return;
        }

        PeelWorld(ref name, home);
        if (name.Length == 0)
        {
            return;
        }

        QueueFriend("target", name, home);
    }

    public bool TrySendEmote(string body)
    {
        EnsureEmotes();
        if (!GameEmoteDraft.TryCommand(body, emoteCommands, out var command))
        {
            return false;
        }

        Queue(command);
        return true;
    }

    private bool TryFormatPlayer(string characterName, string world, out string target)
    {
        target = string.Empty;
        var name = characterName.Trim();
        var home = world.Trim();
        if (name.Length == 0)
        {
            return false;
        }

        PeelWorld(ref name, home);
        if (name.Length == 0)
        {
            return false;
        }

        if (TryResolvePlayer(name, out var liveName, out var liveWorld))
        {
            name = liveName;
            if (liveWorld.Length > 0)
            {
                home = liveWorld;
            }
        }
        else
        {
            name = CanonicalName(name);
        }

        if (name.IndexOf(' ') < 0)
        {
            return false;
        }

        target = home.Length == 0 ? name : name + "@" + home;
        return true;
    }

    public void Print(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return;
        }

        chat.Print(body, "Linkpearl");
    }

    public void OpenGameMenu(GameMenu menu)
    {
        if (!clientState.IsLoggedIn)
        {
            return;
        }

        var command = CommandOf(menu);
        if (command.Length == 0)
        {
            return;
        }

        Queue(command);
    }

    private static string CommandOf(GameMenu menu) => menu switch
    {
        GameMenu.Character => "/character",
        GameMenu.Inventory => "/inventory",
        GameMenu.ArmouryChest => "/armourychest",
        GameMenu.Saddlebag => "/saddlebag",
        GameMenu.Currency => "/currency",
        GameMenu.Achievements => "/achievements",
        GameMenu.GoldSaucer => "/goldsaucer",
        GameMenu.MountGuide => "/mountguide",
        GameMenu.MinionGuide => "/minionguide",
        GameMenu.Companion => "/companion",
        GameMenu.PvpProfile => "/pvpprofile",
        GameMenu.BlueSpellbook => "/bluespellbook",
        GameMenu.Fashion => "/fashion",
        GameMenu.Facewear => "/facewear",
        GameMenu.AdventurerPlate => "/adventurerplate",
        GameMenu.Portraits => "/portraitlist",
        GameMenu.Actions => "/actions",
        GameMenu.FriendList => "/friendlist",
        GameMenu.Blacklist => "/blacklist",
        GameMenu.Linkshell => "/linkshell",
        GameMenu.CrossWorldLinkshell => "/cwls",
        GameMenu.FreeCompany => "/freecompany",
        GameMenu.Emotes => "/emotelist",
        GameMenu.PlayerSearch => "/search",
        GameMenu.PartyFinder => "/partyfinder",
        GameMenu.DutyFinder => "/dutyfinder",
        GameMenu.RaidFinder => "/raidfinder",
        GameMenu.Journal => "/journal",
        GameMenu.ChallengeLog => "/challengelog",
        GameMenu.NoviceNetwork => "/novicenetwork",
        _ => string.Empty,
    };

    public IReadOnlyList<string> LinkshellNames(bool crossWorld)
    {
        var names = new string[LinkshellSlots];
        Array.Fill(names, string.Empty);
        if (crossWorld)
        {
            FillCrossWorldNames(names);
        }
        else
        {
            FillLinkshellNames(names);
        }

        return names;
    }

    public void Dispose()
    {
        chat.ChatMessage -= HandleChatMessage;
        framework.Update -= HandleUpdate;
    }

    private void HandleUpdate(IFramework running)
    {
        EnsureEmotes();
        if (!clientState.IsLoggedIn)
        {
            lock (friendGate)
            {
                friends = [];
                pendingFriends = [];
                offeredFriends.Clear();
                askedFriendList = false;
                pendingAddTarget = string.Empty;
                pendingAddWait = 0;
            }

            lock (commandGate)
            {
                outgoing.Clear();
            }

            return;
        }

        RefreshFriends();
        FinishFriendAdd();
        PumpCommand();
    }

    private void FinishFriendAdd()
    {
        string target;
        lock (friendGate)
        {
            if (pendingAddWait <= 0 || pendingAddTarget.Length == 0)
            {
                return;
            }

            pendingAddWait--;
            if (pendingAddWait > 0)
            {
                return;
            }

            target = pendingAddTarget;
            pendingAddTarget = string.Empty;
            SplitFriendTarget(target, out var name, out var home);
            if (NamedFriend(friends, name, home))
            {
                return;
            }
        }

        SplitFriendTarget(target, out var player, out var world);
        QueueFriend("add", player, world);
    }

    private unsafe void RefreshFriends()
    {
        var proxy = InfoProxyFriendList.Instance();
        if (proxy is null)
        {
            return;
        }

        if (proxy->EntryCount == 0 && !askedFriendList)
        {
            askedFriendList = true;
            proxy->RequestData();
        }

        var local = session.Character.Name;
        var next = new List<GameFriend>((int)Math.Min(proxy->EntryCount, 200u));
        var pending = new List<GameFriend>();
        var count = proxy->GetEntryCount();
        if (count == 0)
        {
            return;
        }

        for (uint index = 0; index < count; index++)
        {
            var entry = proxy->GetEntry(index);
            if (entry is null || entry->ContentId == 0)
            {
                continue;
            }

            var name = entry->NameString.Trim();
            if (name.Length == 0 || string.Equals(name, local, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var home = WorldName(entry->HomeWorld);
            var current = WorldName(entry->CurrentWorld);
            var online = FriendOnline(entry->State);
            var row = new GameFriend(name, home, FriendPlace(entry, online, home, current), online);
            var waiting = (entry->ExtraFlags & 0x20) != 0 ||
                (entry->State & InfoProxyCommonList.CharacterData.OnlineStatus.WaitingForFriendListApproval) != 0;
            if (waiting)
            {
                pending.Add(row);
                continue;
            }

            next.Add(row);
        }

        lock (friendGate)
        {
            friends = next.ToArray();
            pendingFriends = pending.ToArray();
            ForgetOfferedFriends();
        }
    }

    private void ForgetOfferedFriends()
    {
        if (offeredFriends.Count == 0)
        {
            return;
        }

        offeredFriends.RemoveWhere(target =>
        {
            SplitFriendTarget(target, out var name, out var world);
            return NamedFriend(friends, name, world);
        });
    }

    private static bool NamedFriend(GameFriend[] roster, string name, string world)
    {
        var trimmedName = name.Trim();
        var trimmedWorld = world.Trim();
        if (trimmedName.Length == 0)
        {
            return false;
        }

        PeelWorld(ref trimmedName, trimmedWorld);
        for (var index = 0; index < roster.Length; index++)
        {
            if (!roster[index].Name.Equals(trimmedName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (trimmedWorld.Length == 0 || roster[index].World.Length == 0 ||
                roster[index].World.Equals(trimmedWorld, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static void SplitFriendTarget(string target, out string name, out string world)
    {
        var at = target.LastIndexOf('@');
        if (at < 0)
        {
            name = target;
            world = string.Empty;
            return;
        }

        name = target[..at];
        world = target[(at + 1)..];
    }

    private unsafe string FriendPlace(InfoProxyCommonList.CharacterData* entry, bool online, string home,
        string current)
    {
        var zone = PlaceName(entry->Location);
        if (zone.Length > 0)
        {
            return zone;
        }

        if (!online)
        {
            return home.Length > 0 ? home : "Offline";
        }

        if ((entry->State & InfoProxyCommonList.CharacterData.OnlineStatus.AnotherWorld) != 0 &&
            current.Length > 0)
        {
            return current;
        }

        if (current.Length > 0)
        {
            return current;
        }

        return home.Length > 0 ? home : "Online";
    }

    private string PlaceName(uint rowId)
    {
        if (rowId != 0 && data.GetExcelSheet<TerritorySheet>().TryGetRow(rowId, out var territory))
        {
            var place = territory.PlaceName.ValueNullable;
            if (place is { } named)
            {
                return named.Name.ExtractText();
            }
        }

        return string.Empty;
    }

    private static bool FriendOnline(InfoProxyCommonList.CharacterData.OnlineStatus state)
    {
        if (state == 0)
        {
            return false;
        }

        const InfoProxyCommonList.CharacterData.OnlineStatus away =
            InfoProxyCommonList.CharacterData.OnlineStatus.Disconnected |
            InfoProxyCommonList.CharacterData.OnlineStatus.NotFound |
            InfoProxyCommonList.CharacterData.OnlineStatus.OfflineExd;
        return (state & away) == 0;
    }

    private void HandleChatMessage(IHandleableChatMessage message)
    {
        if (!TryMap(message.LogKind, out var channel, out var index, out var outgoingTell))
        {
            return;
        }

        var sender = message.Sender.TextValue.Trim();
        var body = message.Message.TextValue.Trim();
        if (body.Length == 0)
        {
            return;
        }

        var world = WorldOf(message.Sender);
        if (TryFirstPlayer(message.Sender, out var payloadName, out var payloadWorld))
        {
            if (!outgoingTell || !IsLocal(payloadName))
            {
                sender = PreferFullName(sender, payloadName);
            }

            if (payloadWorld.Length > 0)
            {
                world = payloadWorld;
            }
        }

        PeelWorld(ref sender, world);
        sender = PlayerNames.Clean(sender);

        if (outgoingTell)
        {
            ResolveTellPeer(message, ref sender, ref world);
            if ((sender.Length == 0 || IsLocal(sender)) && pendingTellName.Length > 0)
            {
                sender = pendingTellName;
                if (pendingTellWorld.Length > 0)
                {
                    world = pendingTellWorld;
                }
            }

            pendingTellName = string.Empty;
            pendingTellWorld = string.Empty;
        }

        var mine = outgoingTell || IsLocal(sender);
        LineReceived?.Invoke(new GameChatLine(channel, index, sender, world, body, DateTimeOffset.Now, mine));
    }

    private void ResolveTellPeer(IHandleableChatMessage message, ref string sender, ref string world)
    {
        if (!IsLocal(sender))
        {
            return;
        }

        if (TryPeer(message.Sender, sender, out var name, out var home) ||
            TryPeer(message.Message, sender, out name, out home))
        {
            sender = name;
            if (home.Length > 0)
            {
                world = home;
            }
        }
    }

    private bool TryPeer(SeString seString, string localName, out string name, out string world)
    {
        name = string.Empty;
        world = string.Empty;
        for (var index = 0; index < seString.Payloads.Count; index++)
        {
            if (seString.Payloads[index] is not PlayerPayload player)
            {
                continue;
            }

            var candidate = player.PlayerName.Trim();
            if (candidate.Length == 0 || candidate.Equals(localName, StringComparison.OrdinalIgnoreCase) ||
                IsLocal(candidate))
            {
                continue;
            }

            name = candidate;
            var home = player.World.ValueNullable;
            if (home is { } named)
            {
                world = named.Name.ExtractText();
            }

            return true;
        }

        return false;
    }

    private static bool TryFirstPlayer(SeString seString, out string name, out string world)
    {
        name = string.Empty;
        world = string.Empty;
        for (var index = 0; index < seString.Payloads.Count; index++)
        {
            if (seString.Payloads[index] is not PlayerPayload player)
            {
                continue;
            }

            var candidate = player.PlayerName.Trim();
            if (candidate.Length == 0)
            {
                continue;
            }

            name = candidate;
            var home = player.World.ValueNullable;
            if (home is { } named)
            {
                world = named.Name.ExtractText();
            }

            return true;
        }

        return false;
    }

    private static string StripTellToken(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        return value.Trim().Trim('"').Trim();
    }

    private static void TakeHome(ref string name, ref string world)
    {
        var at = name.LastIndexOf('@');
        if (at <= 0)
        {
            return;
        }

        var home = StripTellToken(name[(at + 1)..]);
        name = StripTellToken(name[..at]);
        if (world.Length == 0)
        {
            world = home;
        }
    }

    private static void PeelWorld(ref string name, string world)
    {
        if (world.Length == 0 || name.Length <= world.Length)
        {
            return;
        }

        if (name.EndsWith(world, StringComparison.OrdinalIgnoreCase))
        {
            name = name[..^world.Length].Trim();
        }
    }

    private bool TryResolvePlayer(string name, out string liveName, out string liveWorld)
    {
        liveName = string.Empty;
        liveWorld = string.Empty;
        if (TryMatchPlayer(NearbyPlayers, name, requireExact: true, out liveName, out liveWorld) ||
            TryMatchFriend(name, requireExact: true, out liveName, out liveWorld) ||
            TryMatchPlayer(NearbyPlayers, name, requireExact: false, out liveName, out liveWorld) ||
            TryMatchFriend(name, requireExact: false, out liveName, out liveWorld))
        {
            return true;
        }

        return false;
    }

    private static bool TryMatchPlayer(IReadOnlyList<GamePeer> nearby, string name, bool requireExact,
        out string liveName, out string liveWorld)
    {
        liveName = string.Empty;
        liveWorld = string.Empty;
        var hits = 0;
        for (var index = 0; index < nearby.Count; index++)
        {
            if (requireExact
                    ? !nearby[index].Name.Equals(name, StringComparison.OrdinalIgnoreCase)
                    : !NamesMatch(nearby[index].Name, name))
            {
                continue;
            }

            hits++;
            liveName = nearby[index].Name;
            liveWorld = nearby[index].World;
            if (requireExact)
            {
                return true;
            }
        }

        return hits == 1;
    }

    private bool TryMatchFriend(string name, bool requireExact, out string liveName, out string liveWorld)
    {
        liveName = string.Empty;
        liveWorld = string.Empty;
        GameFriend[] roster;
        lock (friendGate)
        {
            roster = friends;
        }

        var hits = 0;
        for (var index = 0; index < roster.Length; index++)
        {
            if (requireExact
                    ? !roster[index].Name.Equals(name, StringComparison.OrdinalIgnoreCase)
                    : !NamesMatch(roster[index].Name, name))
            {
                continue;
            }

            hits++;
            liveName = roster[index].Name;
            liveWorld = roster[index].World;
            if (requireExact)
            {
                return true;
            }
        }

        return hits == 1;
    }

    private static string PreferFullName(string shown, string payload)
    {
        if (payload.Contains(' ') && !shown.Contains(' '))
        {
            return payload;
        }

        if (shown.Contains(' '))
        {
            return shown;
        }

        return payload.Length > shown.Length ? payload : shown;
    }

    private static void RememberPeer(List<GamePeer> list, string name, string world, string local)
    {
        if (name.Length == 0 || NamesMatch(name, local))
        {
            return;
        }

        for (var index = 0; index < list.Count; index++)
        {
            if (NamesMatch(list[index].Name, name) &&
                list[index].World.Equals(world, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }
        }

        list.Add(new GamePeer(name, world));
    }

    private static bool Named(List<GamePeer> list, string name)
    {
        for (var index = 0; index < list.Count; index++)
        {
            if (NamesMatch(list[index].Name, name))
            {
                return true;
            }
        }

        return false;
    }

    private static bool NamesMatch(string left, string right)
    {
        if (left.Equals(right, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return FirstNameEquals(left, right) || FirstNameEquals(right, left);
    }

    private static bool FirstNameEquals(string full, string token)
    {
        if (token.Contains(' '))
        {
            return false;
        }

        var space = full.IndexOf(' ');
        return space > 0 && full.AsSpan(0, space).Equals(token, StringComparison.OrdinalIgnoreCase);
    }

    private static string CanonicalName(string name)
    {
        var foundUpper = false;
        for (var index = 0; index < name.Length; index++)
        {
            if (char.IsUpper(name[index]))
            {
                foundUpper = true;
                break;
            }
        }

        if (foundUpper)
        {
            return name;
        }

        var chars = name.ToCharArray();
        var cap = true;
        for (var index = 0; index < chars.Length; index++)
        {
            if (chars[index] == ' ')
            {
                cap = true;
                continue;
            }

            chars[index] = cap ? char.ToUpperInvariant(chars[index]) : char.ToLowerInvariant(chars[index]);
            cap = false;
        }

        return new string(chars);
    }

    private bool IsLocal(string sender)
    {
        var local = session.Character.Name;
        if (local.Length == 0 || sender.Length == 0)
        {
            return false;
        }

        return PlayerNames.Same(sender, local);
    }

    private string WorldName(uint rowId)
    {
        if (rowId != 0 && data.GetExcelSheet<WorldSheet>().TryGetRow(rowId, out var world))
        {
            return world.Name.ExtractText();
        }

        return string.Empty;
    }

    private static string WorldOf(SeString seString)
    {
        for (var index = 0; index < seString.Payloads.Count; index++)
        {
            if (seString.Payloads[index] is PlayerPayload player)
            {
                var world = player.World.ValueNullable;
                if (world is { } named)
                {
                    var label = named.Name.ExtractText();
                    if (label.Length > 0)
                    {
                        return label;
                    }
                }
            }
        }

        return string.Empty;
    }

    private static string MemberName(Dalamud.Game.ClientState.Party.IPartyMember member)
    {
        var name = member.Name;
        return name.TextValue.Trim();
    }

    private static bool TryMap(XivChatType type, out GameChannel channel, out int index, out bool outgoingTell)
    {
        index = 0;
        outgoingTell = false;
        switch (type)
        {
            case XivChatType.TellOutgoing:
            case XivChatType.GmTell:
                channel = GameChannel.Tell;
                outgoingTell = type == XivChatType.TellOutgoing;
                return true;
            case XivChatType.TellIncoming:
                channel = GameChannel.Tell;
                return true;
            case XivChatType.StandardEmote:
            case XivChatType.CustomEmote:
                channel = GameChannel.Emote;
                return true;
            case XivChatType.Say:
                channel = GameChannel.Say;
                return true;
            case XivChatType.Shout:
                channel = GameChannel.Shout;
                return true;
            case XivChatType.Yell:
                channel = GameChannel.Yell;
                return true;
            case XivChatType.Party:
            case XivChatType.CrossParty:
            case XivChatType.GmParty:
                channel = GameChannel.Party;
                return true;
            case XivChatType.Alliance:
                channel = GameChannel.Alliance;
                return true;
            case XivChatType.FreeCompany:
            case XivChatType.GmFreeCompany:
                channel = GameChannel.FreeCompany;
                return true;
            case XivChatType.NoviceNetwork:
            case XivChatType.GmNoviceNetwork:
                channel = GameChannel.Novice;
                return true;
            case XivChatType.Ls1:
                channel = GameChannel.Linkshell;
                index = 1;
                return true;
            case XivChatType.Ls2:
                channel = GameChannel.Linkshell;
                index = 2;
                return true;
            case XivChatType.Ls3:
                channel = GameChannel.Linkshell;
                index = 3;
                return true;
            case XivChatType.Ls4:
                channel = GameChannel.Linkshell;
                index = 4;
                return true;
            case XivChatType.Ls5:
                channel = GameChannel.Linkshell;
                index = 5;
                return true;
            case XivChatType.Ls6:
                channel = GameChannel.Linkshell;
                index = 6;
                return true;
            case XivChatType.Ls7:
                channel = GameChannel.Linkshell;
                index = 7;
                return true;
            case XivChatType.Ls8:
                channel = GameChannel.Linkshell;
                index = 8;
                return true;
            case XivChatType.CrossLinkShell1:
                channel = GameChannel.CrossWorldLinkshell;
                index = 1;
                return true;
            case XivChatType.CrossLinkShell2:
                channel = GameChannel.CrossWorldLinkshell;
                index = 2;
                return true;
            case XivChatType.CrossLinkShell3:
                channel = GameChannel.CrossWorldLinkshell;
                index = 3;
                return true;
            case XivChatType.CrossLinkShell4:
                channel = GameChannel.CrossWorldLinkshell;
                index = 4;
                return true;
            case XivChatType.CrossLinkShell5:
                channel = GameChannel.CrossWorldLinkshell;
                index = 5;
                return true;
            case XivChatType.CrossLinkShell6:
                channel = GameChannel.CrossWorldLinkshell;
                index = 6;
                return true;
            case XivChatType.CrossLinkShell7:
                channel = GameChannel.CrossWorldLinkshell;
                index = 7;
                return true;
            case XivChatType.CrossLinkShell8:
                channel = GameChannel.CrossWorldLinkshell;
                index = 8;
                return true;
            default:
                channel = GameChannel.Unknown;
                return false;
        }
    }

    private static string SanitizeBody(string body)
    {
        var trimmed = body.Replace('\r', ' ').Replace('\n', ' ').Trim();
        if (trimmed.Length == 0)
        {
            return string.Empty;
        }

        if (trimmed[0] == '/')
        {
            trimmed = trimmed.TrimStart('/');
        }

        return trimmed;
    }

    private void EnsureEmotes()
    {
        if (emotesLoaded)
        {
            return;
        }

        emotesLoaded = true;
        foreach (var emote in data.GetExcelSheet<EmoteSheet>())
        {
            if (emote.TextCommand.ValueNullable is not { } text)
            {
                continue;
            }

            var canon = CanonEmote(text.Command.ExtractText());
            if (canon.Length == 0)
            {
                canon = CanonEmote(text.ShortCommand.ExtractText());
            }

            if (canon.Length == 0)
            {
                continue;
            }

            RememberEmote(text.Command.ExtractText(), canon);
            RememberEmote(text.ShortCommand.ExtractText(), canon);
            RememberEmote(text.Alias.ExtractText(), canon);
            RememberEmote(text.ShortAlias.ExtractText(), canon);
        }
    }

    private void RememberEmote(string raw, string canon)
    {
        var key = NormalizeEmote(raw);
        if (key.Length == 0)
        {
            return;
        }

        emoteCommands.TryAdd(key, canon);
    }

    private static string NormalizeEmote(string raw)
    {
        var trimmed = (raw ?? string.Empty).Trim();
        if (trimmed.StartsWith('/'))
        {
            trimmed = trimmed[1..];
        }

        var space = trimmed.IndexOf(' ');
        if (space >= 0)
        {
            trimmed = trimmed[..space];
        }

        return trimmed.ToLowerInvariant();
    }

    private static string CanonEmote(string raw)
    {
        var key = NormalizeEmote(raw);
        return key.Length == 0 ? string.Empty : "/" + key;
    }

    private void Queue(string command)
    {
        if (command.Length == 0)
        {
            return;
        }

        log.Information("Sending chat: {0}", command);
        lock (commandGate)
        {
            outgoing.Enqueue(new OutgoingLine(command, string.Empty, string.Empty, string.Empty));
        }
    }

    private void QueueFriend(string verb, string name, string world)
    {
        if (verb.Length == 0 || name.Length == 0)
        {
            return;
        }

        log.Information("Friend list {0}: {1}@{2}", verb, name, world);
        lock (commandGate)
        {
            outgoing.Enqueue(new OutgoingLine(string.Empty, verb, name, world));
        }
    }

    private void PumpCommand()
    {
        OutgoingLine line;
        lock (commandGate)
        {
            if (outgoing.Count == 0)
            {
                return;
            }

            line = outgoing.Dequeue();
        }

        try
        {
            if (line.FriendVerb.Length > 0)
            {
                ExecuteFriend(line.FriendVerb, line.FriendName, line.FriendWorld);
            }
            else
            {
                ExecuteNow(line.Command);
            }
        }
        catch (Exception failure)
        {
            log.Error(failure, "Chat send failed");
            chat.Print("Linkpearl could not send: " + failure.Message, "Linkpearl");
        }
    }

    private readonly record struct OutgoingLine(string Command, string FriendVerb, string FriendName, string FriendWorld);

    private unsafe void ExecuteFriend(string verb, string name, string world)
    {
        if (TryResolvePlayer(name, out var liveName, out var liveWorld))
        {
            name = liveName;
            if (liveWorld.Length > 0)
            {
                world = liveWorld;
            }
        }

        if (verb.Equals("target", StringComparison.OrdinalIgnoreCase))
        {
            if (!TryAimAtPlayer(name, world))
            {
                chat.Print("Linkpearl could not target " + name + " right now.", "Linkpearl");
            }

            return;
        }

        if (name.IndexOf(' ') < 0)
        {
            chat.Print("Linkpearl needs a first and last name to do that.", "Linkpearl");
            return;
        }

        var invite = verb.Equals("invite", StringComparison.OrdinalIgnoreCase);
        if (TryAimAtPlayer(name, world))
        {
            ExecuteNow(invite ? "/invite <t>" : "/friendlist add <t>");
            return;
        }

        var prefix = invite ? "/invite " : "/friendlist add ";
        var worldId = ResolveWorldId(name, world);
        var built = new SeStringBuilder();
        built.AddText(prefix);
        if (worldId != 0)
        {
            built.Add(new PlayerPayload(name, worldId));
        }
        else
        {
            built.AddText("\"" + name + "\"");
        }

        ExecutePayload(built.Build().Encode());
    }

    private uint ResolveWorldId(string name, string world)
    {
        var id = WorldRowId(world);
        if (id != 0)
        {
            return id;
        }

        foreach (var obj in objects)
        {
            if (obj is not IPlayerCharacter player)
            {
                continue;
            }

            if (!player.Name.TextValue.Trim().Equals(name, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            return player.HomeWorld.RowId;
        }

        return 0;
    }

    private bool TryAimAtPlayer(string name, string world)
    {
        foreach (var obj in objects)
        {
            if (obj is not IPlayerCharacter player)
            {
                continue;
            }

            if (!player.Name.TextValue.Trim().Equals(name, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var home = WorldName(player.HomeWorld.RowId);
            if (world.Length > 0 && home.Length > 0 &&
                !home.Equals(world, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            targets.Target = player;
            return true;
        }

        return false;
    }

    private unsafe void ExecutePayload(byte[] encoded)
    {
        if (encoded.Length == 0 || encoded.Length > MaxCommandBytes)
        {
            chat.Print("Linkpearl could not send: the message is empty or too long.", "Linkpearl");
            return;
        }

        var ui = UIModule.Instance();
        if (ui is null)
        {
            chat.Print("Linkpearl could not send: the game UI is not ready.", "Linkpearl");
            return;
        }

        var text = Utf8String.FromSequence(encoded);
        if (text is null)
        {
            chat.Print("Linkpearl could not send: the game UI is not ready.", "Linkpearl");
            return;
        }

        try
        {
            ui->ProcessChatBoxEntry(text);
        }
        finally
        {
            text->Dtor(true);
        }
    }

    private unsafe void ExecuteNow(string command)
    {
        if (command.Length == 0)
        {
            return;
        }

        var ui = UIModule.Instance();
        if (ui is null)
        {
            chat.Print("Linkpearl could not send: the game UI is not ready.", "Linkpearl");
            return;
        }

        var text = Utf8String.FromString(command);
        if (text is null || text->Length == 0 || text->Length > MaxCommandBytes)
        {
            if (text is not null)
            {
                text->Dtor(true);
            }

            chat.Print("Linkpearl could not send: the message is empty or too long.", "Linkpearl");
            return;
        }

        try
        {
            ui->ProcessChatBoxEntry(text);
        }
        finally
        {
            text->Dtor(true);
        }
    }

    private uint WorldRowId(string name)
    {
        if (name.Length == 0)
        {
            return 0;
        }

        var sheet = data.GetExcelSheet<WorldSheet>();
        foreach (var world in sheet)
        {
            if (world.Name.ExtractText().Equals(name, StringComparison.OrdinalIgnoreCase))
            {
                return world.RowId;
            }
        }

        return 0;
    }

    private static unsafe void FillLinkshellNames(string[] names)
    {
        var proxy = InfoProxyLinkshell.Instance();
        if (proxy is null)
        {
            return;
        }

        for (uint slot = 0; slot < names.Length; slot++)
        {
            var entry = proxy->GetLinkshellInfo(slot);
            if (entry is null || entry->Id == 0)
            {
                continue;
            }

            names[slot] = PointerText(proxy->GetLinkshellName(entry->Id));
        }
    }

    private static unsafe void FillCrossWorldNames(string[] names)
    {
        var proxy = InfoProxyCrossWorldLinkshell.Instance();
        if (proxy is null)
        {
            return;
        }

        for (uint slot = 0; slot < names.Length; slot++)
        {
            var name = proxy->GetCrossworldLinkshellName(slot);
            if (name is null || name->IsEmpty)
            {
                continue;
            }

            names[slot] = name->ToString();
        }
    }

    private static string PointerText(InteropGenerator.Runtime.CStringPointer pointer)
    {
        if (!pointer.HasValue)
        {
            return string.Empty;
        }

        return pointer.ToString();
    }
}