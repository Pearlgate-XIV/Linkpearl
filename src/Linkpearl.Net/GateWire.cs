using System.Text.Json.Serialization;

namespace Linkpearl.Net;

internal sealed record ChallengeRequestDto(string Name, string World);

internal sealed record ChallengeReplyDto(string ChallengeId, string Code, string Instructions);

internal sealed record VerifyRequestDto(string ChallengeId);

internal sealed record GateUserDto(
    string? Id,
    string? Name,
    string? World,
    string? DisplayName,
    string? Handle,
    string? Bio,
    int Followers,
    int Following);

internal sealed record VerifyReplyDto(bool Ok, string? Reason, string? Token, GateUserDto? User);

internal sealed record ConversationDto(
    string Id,
    bool IsGroup,
    string? Title,
    string? OtherDisplayName,
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
    bool IsMutual);

internal sealed record ContactListDto(ContactDto[]? Contacts, string MyNumber);

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

internal sealed record HealthDto(bool Ok, string? Service);

[JsonSerializable(typeof(ChallengeRequestDto))]
[JsonSerializable(typeof(ChallengeReplyDto))]
[JsonSerializable(typeof(VerifyRequestDto))]
[JsonSerializable(typeof(VerifyReplyDto))]
[JsonSerializable(typeof(GateUserDto))]
[JsonSerializable(typeof(ConversationDto))]
[JsonSerializable(typeof(ConversationPageDto))]
[JsonSerializable(typeof(ContactDto))]
[JsonSerializable(typeof(ContactListDto))]
[JsonSerializable(typeof(UserSearchDto))]
[JsonSerializable(typeof(StoryRingDto))]
[JsonSerializable(typeof(StoryTrayDto))]
[JsonSerializable(typeof(AnnouncementTranslationDto))]
[JsonSerializable(typeof(AnnouncementDto))]
[JsonSerializable(typeof(AnnouncementPageDto))]
[JsonSerializable(typeof(HealthDto))]
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
internal sealed partial class GateJson : JsonSerializerContext
{
}
