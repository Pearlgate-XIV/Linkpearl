using System.Text.Json.Serialization;

namespace Linkpearl.Net;

internal sealed record ChallengeRequestDto(string Name, string World);

internal sealed record ChallengeReplyDto(string ChallengeId, string Code, string Instructions);

internal sealed record VerifyRequestDto(string ChallengeId);

internal sealed record XivAuthStartRequestDto(string Name, string World);

internal sealed record XivAuthStartReplyDto(
    bool Ok,
    string? Reason,
    string? FlowId,
    string? UserCode,
    string? VerificationUri,
    string? VerificationUriComplete,
    int IntervalSeconds,
    int ExpiresInSeconds);

internal sealed record XivAuthPollRequestDto(string FlowId);

internal sealed record GateUserDto(
    string? Id,
    string? Name,
    string? World,
    string? DisplayName,
    string? Handle,
    string? Bio,
    string? AvatarUrl,
    int Followers,
    int Following,
    int? FounderSeat,
    int? Seat,
    bool? Patron,
    bool? IsPatron,
    bool? Patreon,
    bool? PatreonActive,
    string? PatreonUrl,
    string? PhoneNumber,
    string? TimeZoneId = null,
    AccountNoticeDto[]? StaffNotices = null,
    bool? Banned = null,
    bool? Muted = null,
    long MuteUntilUnix = 0,
    string? BanReason = null);

internal sealed record VerifyReplyDto(bool Ok, string? Reason, string? Token, GateUserDto? User);

internal sealed record ConversationDto(
    string Id,
    bool IsGroup,
    string? Title,
    string? OtherDisplayName,
    string? OtherUserId,
    string? LastMessagePreview,
    long LastMessageAtUnix,
    int UnreadCount);

internal sealed record ConversationPageDto(ConversationDto[]? Items, string? NextCursor);

internal sealed record ContactDto(
    string UserId,
    string? DisplayName,
    string? Handle,
    string? PhoneNumber,
    string? Alias,
    string? AvatarUrl,
    bool IsMutual,
    string? Race,
    string? World,
    string? TimeZoneId = null);

internal sealed record ContactListDto(
    ContactDto[]? Contacts,
    string? MyNumber,
    string? PhoneNumber,
    string? Number);

internal sealed record AddContactBodyDto(string? Number, string? Alias, string? UserId);

internal sealed record UserSearchDto(GateUserDto[]? Users);

internal sealed record StoryRingDto(
    string? AuthorId,
    string? AuthorDisplayName,
    bool HasUnseen,
    int Count);

internal sealed record StoryTrayDto(StoryRingDto[]? Rings);

internal sealed record AnnouncementTranslationDto(string Lang, string Title, string Body);

internal sealed record AnnouncementDto(
    string? Id,
    string? Title,
    string? Body,
    AnnouncementTranslationDto[]? Translations,
    long CreatedAtUnix);

internal sealed record AnnouncementPageDto(AnnouncementDto[]? Items, string? NextCursor);

internal sealed record SendChatDto(string Body);

internal sealed record ChatMessageDto(
    string? Id,
    string? Body,
    string? Text,
    string? Content,
    bool Mine,
    string? AuthorDisplayName,
    string? SenderDisplayName = null,
    string? SenderId = null,
    long CreatedAtUnix = 0);

internal sealed record ChatMessagePageDto(ChatMessageDto[]? Items, ChatMessageDto[]? Messages);

internal sealed record HealthDto(bool Ok, string? Service);

internal sealed record PatronDto(bool? Linked, bool? Patron, bool? IsPatron, string? Url, string? LinkUrl);

internal sealed record MediaDto(string? Id, string? Url, int Width, int Height);

internal sealed record PostDto(
    string? Id,
    string? AuthorId,
    string? AuthorDisplayName,
    string? AuthorHandle,
    string? AuthorAvatarUrl,
    string? Body,
    long CreatedAtUnix,
    bool Mine,
    bool Liked,
    int Likes,
    int Comments,
    int Reposts,
    bool Reposted,
    string? QuoteOf,
    string? QuoteAuthor,
    string? QuoteBody,
    MediaDto[]? Media);

internal sealed record PostPageDto(PostDto[]? Items, string? NextCursor);

internal sealed record PostBodyDto(string? Body, string Audience, string[]? MediaIds, string? QuoteOf);

internal sealed record VybePlusPostDto(
    string? Id,
    string? OwnerId,
    string? OwnerDisplayName,
    string? OwnerHandle,
    string? OwnerAvatarUrl,
    string? MediaId,
    string? MediaUrl,
    string? Caption,
    string[]? Tags,
    bool Unlocked,
    int MediaWidth,
    int MediaHeight,
    long CreatedAtUnix,
    int[]? ReactionCounts,
    int TotalReactions,
    int MyReaction,
    int CommentCount,
    string[]? MediaUrls = null,
    int Audience = 0,
    int Lane = 0);

internal sealed record VybePlusFeedPageDto(VybePlusPostDto[]? Items, string? NextCursor = null);

internal sealed record VybePlusUserPostsPageDto(VybePlusPostDto[]? Items, int TotalCount = 0, string? NextCursor = null);

internal sealed record CreateVybePlusPostBodyDto(
    string? MediaKey,
    int Width,
    int Height,
    string Caption,
    string[] Tags,
    string[]? MediaKeys = null,
    int Audience = 1,
    int Lane = 0);

internal sealed record CommentDto(
    string? Id,
    string? AuthorDisplayName,
    string? Body,
    long CreatedAtUnix,
    bool Mine);

internal sealed record CommentPageDto(CommentDto[]? Items, PostDto? Post);

internal sealed record CommentBodyDto(string Body);

internal sealed record NoteDto(
    string? Id,
    string? Kind,
    string? ActorId,
    string? ActorName,
    string? Line,
    string? PostId,
    long CreatedAtUnix);

internal sealed record NotePageDto(NoteDto[]? Items);

internal sealed record MediaReplyDto(string? Id, string? Url, int Width, int Height);

internal sealed record AvatarBodyDto(string MediaId);

internal sealed record StoryBodyDto(string? Body, string? MediaId);

internal sealed record RetainerDto(
    int Slot,
    string? Name,
    long Gil,
    int ItemsOnSale,
    long VentureUntilUnix);

internal sealed record RetainerPageDto(RetainerDto[]? Items);

internal sealed record MarketWatchDto(
    int ItemId,
    string? Label,
    string? World,
    long NqGil,
    long HqGil,
    int Listed,
    long UpdatedAtUnix);

internal sealed record MarketPageDto(MarketWatchDto[]? Items);

internal sealed record MarketWatchBodyDto(int ItemId, string? World, string? Label);

internal sealed record BanSnapshotDto(
    bool Banned,
    string? BanReason,
    long BanUntilUnix,
    bool Muted,
    long MuteUntilUnix,
    int WarnCount);

internal sealed record AccountNoticeDto(
    string? Id,
    string? Kind,
    string? Title,
    string? Body,
    long CreatedAtUnix,
    bool Read);

internal sealed record AccountNoticePageDto(AccountNoticeDto[]? Items);

internal sealed record ReportBodyDto(
    string TargetType,
    string TargetId,
    string? Reason,
    string? Detail = null,
    RevealedChatLineDto[]? RevealedMessages = null);

internal sealed record RevealedChatLineDto(string MessageId, string PlainText, string? FrankingKey = null);

internal sealed record GifRowDto(
    string? Id,
    string? Title,
    string? PreviewUrl,
    string? SendUrl,
    string? Url,
    string? Preview,
    string? Send);

internal sealed record GifPageDto(GifRowDto[]? Items, int Total);

[JsonSerializable(typeof(ChallengeRequestDto))]
[JsonSerializable(typeof(ChallengeReplyDto))]
[JsonSerializable(typeof(VerifyRequestDto))]
[JsonSerializable(typeof(XivAuthStartRequestDto))]
[JsonSerializable(typeof(XivAuthStartReplyDto))]
[JsonSerializable(typeof(XivAuthPollRequestDto))]
[JsonSerializable(typeof(VerifyReplyDto))]
[JsonSerializable(typeof(GateUserDto))]
[JsonSerializable(typeof(ConversationDto))]
[JsonSerializable(typeof(ConversationPageDto))]
[JsonSerializable(typeof(ContactDto))]
[JsonSerializable(typeof(ContactListDto))]
[JsonSerializable(typeof(AddContactBodyDto))]
[JsonSerializable(typeof(UserSearchDto))]
[JsonSerializable(typeof(StoryRingDto))]
[JsonSerializable(typeof(StoryTrayDto))]
[JsonSerializable(typeof(AnnouncementTranslationDto))]
[JsonSerializable(typeof(AnnouncementDto))]
[JsonSerializable(typeof(AnnouncementPageDto))]
[JsonSerializable(typeof(SendChatDto))]
[JsonSerializable(typeof(ChatMessageDto))]
[JsonSerializable(typeof(ChatMessagePageDto))]
[JsonSerializable(typeof(HealthDto))]
[JsonSerializable(typeof(PatronDto))]
[JsonSerializable(typeof(MediaDto))]
[JsonSerializable(typeof(PostDto))]
[JsonSerializable(typeof(PostPageDto))]
[JsonSerializable(typeof(PostBodyDto))]
[JsonSerializable(typeof(VybePlusPostDto))]
[JsonSerializable(typeof(VybePlusFeedPageDto))]
[JsonSerializable(typeof(VybePlusUserPostsPageDto))]
[JsonSerializable(typeof(CreateVybePlusPostBodyDto))]
[JsonSerializable(typeof(CommentDto))]
[JsonSerializable(typeof(CommentPageDto))]
[JsonSerializable(typeof(CommentBodyDto))]
[JsonSerializable(typeof(NoteDto))]
[JsonSerializable(typeof(NotePageDto))]
[JsonSerializable(typeof(MediaReplyDto))]
[JsonSerializable(typeof(AvatarBodyDto))]
[JsonSerializable(typeof(StoryBodyDto))]
[JsonSerializable(typeof(RetainerDto))]
[JsonSerializable(typeof(RetainerPageDto))]
[JsonSerializable(typeof(MarketWatchDto))]
[JsonSerializable(typeof(MarketPageDto))]
[JsonSerializable(typeof(MarketWatchBodyDto))]
[JsonSerializable(typeof(GifRowDto))]
[JsonSerializable(typeof(GifPageDto))]
[JsonSerializable(typeof(BanSnapshotDto))]
[JsonSerializable(typeof(AccountNoticeDto))]
[JsonSerializable(typeof(AccountNoticePageDto))]
[JsonSerializable(typeof(ReportBodyDto))]
[JsonSerializable(typeof(RevealedChatLineDto))]
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
internal sealed partial class GateJson : JsonSerializerContext
{
}
