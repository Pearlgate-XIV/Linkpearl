namespace Linkpearl.Net;

public sealed partial class PearlHub
{
    private void OnRealtime(GateRealtimeDto frame)
    {
        var kind = frame.Type ?? string.Empty;
        if (kind.Length == 0)
        {
            if (!HasChatPayload(frame))
            {
                return;
            }
        }
        else if (string.Equals(kind, "pong", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(kind, "ping", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(kind, "chat.ping", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }
        else if (string.Equals(kind, "vybe-plus.ping", StringComparison.OrdinalIgnoreCase))
        {
            feedQueued = true;
            return;
        }

        if (LooksLikeChat(kind) || HasChatPayload(frame))
        {
            ApplyChatPush(frame);
        }
    }

    private void OnRealtimeUnauthorized()
    {
        if (Current.SignedIn)
        {
            DropSession("Session expired. Sign in again.");
        }
    }

    private void ApplyChatPush(GateRealtimeDto frame)
    {
        var conversation = frame.Conversation;
        var message = frame.Message;
        var chatId = conversation?.Id
                     ?? message?.ConversationId
                     ?? frame.ConversationId
                     ?? frame.ChatId
                     ?? string.Empty;
        if (chatId.Length == 0)
        {
            QueueRefresh();
            return;
        }

        if (conversation is not null)
        {
            var mapped = MapChats([conversation]);
            if (mapped.Length > 0)
            {
                UpsertChat(mapped[0], mapped[0].OtherUserId);
            }
        }

        var line = message is not null
            ? MapChatLine(message, Current.MeId)
            : MapRealtimeLine(frame);
        if (line is { } incoming)
        {
            RememberLine(chatId, incoming);
            TouchChatPreview(chatId, incoming, frame);
        }
        else if (frame.LastMessagePreview is { Length: > 0 } preview)
        {
            TouchChatPreview(chatId, new PearlChatLine(false, preview, "now", string.Empty), frame);
        }

        if (string.Equals(watchedChat, chatId, StringComparison.Ordinal))
        {
            chatFetchQueued = true;
        }
    }

    private void TouchChatPreview(string chatId, PearlChatLine line, GateRealtimeDto frame)
    {
        var chats = Current.Chats;
        var next = new PearlChat[chats.Length == 0 ? 1 : chats.Length];
        var found = false;
        var unreadBump = line.Mine ? 0 : 1;
        for (var index = 0; index < chats.Length; index++)
        {
            var row = chats[index];
            if (!string.Equals(row.Id, chatId, StringComparison.Ordinal))
            {
                next[index] = row;
                continue;
            }

            found = true;
            var unread = frame.UnreadCount > 0
                ? frame.UnreadCount
                : Math.Max(0, row.UnreadCount) + unreadBump;
            var when = frame.LastMessageAtUnix > 0
                ? frame.LastMessageAtUnix
                : DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            next[index] = row with
            {
                Preview = line.Body.Length > 0 ? line.Body : row.Preview,
                UnreadCount = unread,
                LastMessageAtUnix = when,
            };
        }

        if (!found)
        {
            QueueRefresh();
            return;
        }

        Replace(Current with
        {
            Chats = next,
            UnreadTotal = CountUnread(next),
            Generation = NextGeneration(),
        });
    }

    private static bool LooksLikeChat(string type)
    {
        return type.Contains("chat", StringComparison.OrdinalIgnoreCase) ||
               type.Contains("message", StringComparison.OrdinalIgnoreCase) ||
               type.Contains("conversation", StringComparison.OrdinalIgnoreCase);
    }

    private static bool HasChatPayload(GateRealtimeDto frame) =>
        frame.Message is not null ||
        frame.Conversation is not null ||
        (frame.ConversationId ?? frame.ChatId ?? string.Empty).Length > 0;

    private PearlChatLine? MapRealtimeLine(GateRealtimeDto frame)
    {
        var body = frame.Body ?? frame.Text ?? frame.Content ?? string.Empty;
        if (body.Length == 0)
        {
            return null;
        }

        return MapChatLine(new ChatMessageDto(
            frame.Id,
            frame.Body,
            frame.Text,
            frame.Content,
            frame.Mine,
            frame.AuthorDisplayName,
            frame.SenderDisplayName,
            frame.SenderId,
            frame.CreatedAtUnix,
            frame.ConversationId ?? frame.ChatId,
            frame.EncVersion,
            frame.CommitmentTag), Current.MeId);
    }
}
