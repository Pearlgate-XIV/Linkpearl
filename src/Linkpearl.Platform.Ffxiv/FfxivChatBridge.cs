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
using Linkpearl.Platform;
using WorldSheet = Lumina.Excel.Sheets.World;

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
    private string pendingTellName = string.Empty;
    private string pendingTellWorld = string.Empty;

    public FfxivChatBridge(IChatGui chat, IClientState clientState, IPartyList party, IObjectTable objects,
        IDataManager data, IGameSession session, IFramework framework, IPluginLog log)
    {
        this.chat = chat;
        this.clientState = clientState;
        this.party = party;
        this.objects = objects;
        this.data = data;
        this.session = session;
        this.framework = framework;
        this.log = log;
        chat.ChatMessage += HandleChatMessage;
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
            _ => string.Empty,
        };
        Queue(command);
    }

    public void SendTell(string characterName, string world, string body)
    {
        var name = characterName.Trim();
        var text = SanitizeBody(body);
        if (name.Length == 0 || text.Length == 0)
        {
            return;
        }

        var home = world.Trim();
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

        pendingTellName = name;
        pendingTellWorld = home;
        var target = home.Length == 0 ? name : name + "@" + home;
        Queue("/tell " + target + " " + text);
    }

    public void Print(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return;
        }

        chat.Print(body, "Linkpearl");
    }

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
        if (world.Length == 0)
        {
            world = WorldOf(message.Message);
        }

        if (TryFirstPlayer(message.Sender, out var payloadName, out var payloadWorld) ||
            TryFirstPlayer(message.Message, out payloadName, out payloadWorld))
        {
            if (!outgoingTell || !IsLocal(payloadName))
            {
                sender = payloadName;
            }

            if (payloadWorld.Length > 0)
            {
                world = payloadWorld;
            }
        }

        PeelWorld(ref sender, world);

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
        var nearby = NearbyPlayers;
        for (var index = 0; index < nearby.Count; index++)
        {
            if (!NamesMatch(nearby[index].Name, name))
            {
                continue;
            }

            liveName = nearby[index].Name;
            liveWorld = nearby[index].World;
            return true;
        }

        return false;
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

    private static bool NamesMatch(string left, string right) =>
        left.Equals(right, StringComparison.OrdinalIgnoreCase);

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

        if (sender.Equals(local, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var at = sender.IndexOf('@');
        return at > 0 && sender.AsSpan(0, at).Equals(local, StringComparison.OrdinalIgnoreCase);
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

    private void Queue(string command)
    {
        if (command.Length == 0)
        {
            return;
        }

        log.Information("Sending chat: {0}", command);
        _ = framework.RunOnTick(() =>
        {
            try
            {
                ExecuteNow(command);
            }
            catch (Exception failure)
            {
                log.Error(failure, "Chat send failed");
                chat.Print("Linkpearl could not send: " + failure.Message, "Linkpearl");
            }
        });
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