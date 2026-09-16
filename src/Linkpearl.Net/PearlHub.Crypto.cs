using System.Security.Cryptography;
using System.Text.Json;
using Linkpearl.Diagnostics;

namespace Linkpearl.Net;

public sealed partial class PearlHub
{
    private static readonly JsonSerializerOptions SealJson = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = false,
    };

    private readonly Func<bool> allowE2E;
    private readonly string sealPath;
    private ChatSeal.Identity? identity;
    private bool gateE2E = true;
    private readonly Dictionary<string, RoomSeal> roomKeys = new(StringComparer.Ordinal);

    private bool E2EEnabled => allowE2E() && gateE2E;

    private void LoadIdentity()
    {
        try
        {
            if (File.Exists(sealPath))
            {
                var saved = JsonSerializer.Deserialize<SealFile>(File.ReadAllText(sealPath), SealJson);
                if (saved is { PublicKey.Length: > 0, PrivateD.Length: > 0 } &&
                    ChatSeal.TryParsePublic(saved.PublicKey, out _, out _))
                {
                    identity = new ChatSeal.Identity(saved.PublicKey, saved.PrivateD);
                    return;
                }
            }
        }
        catch (Exception failure) when (failure is IOException or UnauthorizedAccessException or JsonException)
        {
            log.Write(LogSeverity.Warning, failure, "Could not read the local chat seal");
        }

        identity = ChatSeal.CreateIdentity();
        PersistIdentity();
    }

    private void PersistIdentity()
    {
        if (identity is not { } mine)
        {
            return;
        }

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(sealPath) ?? ".");
            File.WriteAllText(sealPath, JsonSerializer.Serialize(new SealFile(mine.PublicKey, mine.PrivateD), SealJson));
        }
        catch (Exception failure) when (failure is IOException or UnauthorizedAccessException)
        {
            log.Write(LogSeverity.Warning, failure, "Could not save the local chat seal");
        }
    }

    private async Task PublishIdentityAsync(CancellationToken token)
    {
        if (identity is not { } mine)
        {
            return;
        }

        var body = GateClient.JsonBody(new PutMyKeysDto(mine.PublicKey), GateJson.Default.PutMyKeysDto);
        var (reply, status) = await client.PutAsync("/keys/me", body, GateJson.Default.MyKeysDto, token)
            .ConfigureAwait(false);
        if (status is < 200 or >= 300)
        {
            log.Write(LogSeverity.Warning, "Pearlgate key publish returned HTTP " + status);
            return;
        }

        if (reply?.PublicKey is { Length: > 0 } &&
            !string.Equals(reply.PublicKey, mine.PublicKey, StringComparison.Ordinal))
        {
            log.Write(LogSeverity.Warning, "Pearlgate stored a different public key than this handset.");
        }
    }

    private async Task ReadFlagsAsync(CancellationToken token)
    {
        var (flags, status) = await client.GetAsync("/flags", GateJson.Default.FeatureFlagsDto, token)
            .ConfigureAwait(false);
        if (status is < 200 or >= 300 || flags?.Apps is null)
        {
            gateE2E = true;
            return;
        }

        gateE2E = !flags.Apps.TryGetValue("e2e", out var flagged) || flagged;
    }

    private async Task<SendChatDto> SealOutgoingAsync(string chatId, string plaintext, CancellationToken token)
    {
        if (!E2EEnabled)
        {
            return new SendChatDto(plaintext);
        }

        var room = await TryReadyRoomAsync(chatId, token).ConfigureAwait(false);
        if (room is null)
        {
            return new SendChatDto(plaintext);
        }

        var (body, commitment) = ChatSeal.SealBody(room.Value.RoomKey, plaintext);
        return new SendChatDto(body, EncVersion: room.Value.Generation, CommitmentTag: commitment);
    }

    private PearlChatLine? MapChatLine(ChatMessageDto item, string meId)
    {
        var raw = item.Body ?? item.Text ?? item.Content ?? string.Empty;
        var encVersion = item.EncVersion;
        var sealedLine = encVersion > 0 || ChatSeal.IsSealedBlob(raw);
        string body;
        var opened = true;
        if (encVersion <= 0 && !ChatSeal.IsSealedBlob(raw))
        {
            if (raw.Length == 0)
            {
                return null;
            }

            body = raw;
        }
        else
        {
            var chatId = item.ConversationId ?? watchedChat;
            if (TryOpen(chatId, encVersion, raw, out var openedBody))
            {
                body = openedBody;
            }
            else
            {
                body = ChatSeal.HonestBody(raw, encVersion > 0 ? encVersion : 1);
                opened = false;
            }
        }

        var mine = item.Mine ||
                   (meId.Length > 0 && string.Equals(item.SenderId, meId, StringComparison.Ordinal));
        var author = item.AuthorDisplayName ?? item.SenderDisplayName ??
                     (mine ? "You" : "Them");
        var when = item.CreatedAtUnix > 0
            ? DateTimeOffset.FromUnixTimeSeconds(item.CreatedAtUnix).ToLocalTime()
                .ToString("HH:mm", System.Globalization.CultureInfo.InvariantCulture)
            : "now";
        return new PearlChatLine(mine, body, when, author, item.Id ?? string.Empty, sealedLine, opened);
    }

    private string OpenPreview(string preview, int encVersion, string chatId)
    {
        if (encVersion <= 0 && !ChatSeal.IsSealedBlob(preview))
        {
            return preview;
        }

        return TryOpen(chatId, encVersion, preview, out var opened)
            ? opened
            : ChatSeal.HonestBody(preview, encVersion > 0 ? encVersion : 1);
    }

    private bool TryOpen(string chatId, int encVersion, string body, out string plaintext)
    {
        plaintext = string.Empty;
        if (chatId.Length == 0)
        {
            return false;
        }

        RoomSeal? cached;
        lock (gate)
        {
            cached = roomKeys.TryGetValue(chatId, out var row) ? row : null;
        }

        if (cached is not { } room)
        {
            return false;
        }

        if (encVersion > 0 && room.Generation != encVersion)
        {
            return false;
        }

        return ChatSeal.TryOpenBody(room.RoomKey, body, out plaintext);
    }

    private async Task<RoomSeal?> TryReadyRoomAsync(string chatId, CancellationToken token)
    {
        var keys = await FetchRoomKeysAsync(chatId, token).ConfigureAwait(false);
        if (keys is null)
        {
            return null;
        }

        var members = keys.MemberKeys ?? [];
        var missing = keys.MembersWithoutKeys ?? [];
        var meId = Current.MeId;
        for (var index = 0; index < missing.Length; index++)
        {
            if (!string.Equals(missing[index], meId, StringComparison.Ordinal))
            {
                log.Write(LogSeverity.Information, "Pearlgate chat " + chatId + " stays plaintext; members are missing keys.");
                return null;
            }
        }

        foreach (var member in members)
        {
            if (!ChatSeal.TryParsePublic(member.PublicKey, out _, out _))
            {
                log.Write(LogSeverity.Information, "Pearlgate chat " + chatId + " stays plaintext; a member key is not lp1.");
                return null;
            }
        }

        if (identity is not { } mine)
        {
            return null;
        }

        if (keys.CurrentGeneration > 0 && (keys.MyWraps ?? []).Length > 0 &&
            TryCacheWraps(chatId, keys.CurrentGeneration, keys.MyWraps!, mine) is { } opened)
        {
            return opened;
        }

        if (!keys.NeedsNewGeneration && keys.CurrentGeneration > 0)
        {
            return null;
        }

        return await CreateRoomGenerationAsync(chatId, keys, members, mine, token).ConfigureAwait(false);
    }

    private RoomSeal? TryCacheWraps(string chatId, int generation, KeyWrapDto[] wraps, ChatSeal.Identity mine)
    {
        foreach (var wrap in wraps)
        {
            if (wrap.Generation != generation)
            {
                continue;
            }

            if (!ChatSeal.TryUnwrapRoomKey(wrap.WrappedKey, mine, out var roomKey))
            {
                continue;
            }

            var seal = new RoomSeal(generation, roomKey);
            lock (gate)
            {
                roomKeys[chatId] = seal;
            }

            return seal;
        }

        return null;
    }

    private async Task<RoomSeal?> CreateRoomGenerationAsync(
        string chatId, ConversationKeysDto keys, UserPublicKeyDto[] members, ChatSeal.Identity mine,
        CancellationToken token)
    {
        var generation = keys.CurrentGeneration <= 0 ? 1 : keys.CurrentGeneration + 1;
        var roomKey = RandomNumberGenerator.GetBytes(32);
        var wraps = new List<NewWrapDto>(members.Length + 1);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < members.Length; index++)
        {
            var member = members[index];
            if (!seen.Add(member.UserId))
            {
                continue;
            }

            wraps.Add(new NewWrapDto(member.UserId, member.KeyVersion, ChatSeal.WrapRoomKey(member.PublicKey, roomKey)));
        }

        var meId = Current.MeId;
        if (meId.Length > 0 && seen.Add(meId))
        {
            wraps.Add(new NewWrapDto(meId, 1, ChatSeal.WrapRoomKey(mine.PublicKey, roomKey)));
        }

        if (wraps.Count == 0)
        {
            return null;
        }

        var body = GateClient.JsonBody(new CreateGenerationDto(generation, wraps.ToArray()),
            GateJson.Default.CreateGenerationDto);
        var (reply, status) = await client
            .PostAsync("/chats/" + Uri.EscapeDataString(chatId) + "/keys", body, GateJson.Default.ConversationKeysDto,
                token)
            .ConfigureAwait(false);
        if (status is < 200 or >= 300 || reply is null)
        {
            log.Write(LogSeverity.Warning, "Pearlgate room key create returned HTTP " + status);
            return null;
        }

        var seal = new RoomSeal(generation, roomKey);
        lock (gate)
        {
            roomKeys[chatId] = seal;
        }

        return seal;
    }

    private async Task<ConversationKeysDto?> FetchRoomKeysAsync(string chatId, CancellationToken token)
    {
        var (keys, status) = await client
            .GetAsync("/chats/" + Uri.EscapeDataString(chatId) + "/keys", GateJson.Default.ConversationKeysDto, token)
            .ConfigureAwait(false);
        if (status == 404)
        {
            return null;
        }

        if (status is < 200 or >= 300)
        {
            log.Write(LogSeverity.Warning, "Pearlgate room keys returned HTTP " + status);
            return null;
        }

        return keys;
    }

    private sealed record SealFile(string PublicKey, string PrivateD);

    private readonly record struct RoomSeal(int Generation, byte[] RoomKey);
}
