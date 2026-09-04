using System.Text.Json.Serialization;
using CalmClass.Application.Domain.Entities;
using CalmClass.Application.Domain.Enums;

namespace CalmClass.Infrastructure.Persistence.Documents;

public record GroupMemberDocument
{
    public const string DocumentType = "GroupMember";

    [JsonPropertyName("id")]
    public required string Id { get; init; }

    [JsonPropertyName("chatId")]
    public required string ChatId { get; init; }

    [JsonPropertyName("type")]
    public string Type { get; init; } = DocumentType;

    [JsonPropertyName("userId")]
    public required long UserId { get; init; }

    [JsonPropertyName("displayName")]
    public required string DisplayName { get; init; }

    [JsonPropertyName("username")]
    public string? Username { get; init; }

    [JsonPropertyName("role")]
    public required string Role { get; init; }

    [JsonPropertyName("isActive")]
    public bool IsActive { get; init; } = true;

    [JsonPropertyName("joinedAtUtc")]
    public required DateTime JoinedAtUtc { get; init; }

    public static GroupMemberDocument FromEntity(GroupMember entity) => new()
    {
        Id = entity.Id,
        ChatId = entity.ChatId,
        Type = DocumentType,
        UserId = entity.UserId,
        DisplayName = entity.DisplayName,
        Username = entity.Username,
        Role = entity.Role.ToString(),
        IsActive = entity.IsActive,
        JoinedAtUtc = entity.JoinedAtUtc
    };

    public GroupMember ToEntity() => new()
    {
        ChatId = ChatId,
        UserId = UserId,
        DisplayName = DisplayName,
        Username = Username,
        Role = Enum.TryParse<MemberRole>(Role, out var r) ? r : MemberRole.Member,
        IsActive = IsActive,
        JoinedAtUtc = JoinedAtUtc
    };
}
