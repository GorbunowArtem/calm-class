namespace CalmClass.Infrastructure.Persistence.Documents;

using System.Text.Json.Serialization;
using CalmClass.Application.Domain.Entities;
using CalmClass.Application.Domain.Enums;

public record GroupMemberDocument(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("chatId")] string ChatId,
    [property: JsonPropertyName("userId")] long UserId,
    [property: JsonPropertyName("displayName")] string DisplayName,
    [property: JsonPropertyName("role")] string Role,
    [property: JsonPropertyName("joinedAtUtc")] DateTime JoinedAtUtc,
    [property: JsonPropertyName("username")] string? Username = null,
    [property: JsonPropertyName("isActive")] bool IsActive = true,
    [property: JsonPropertyName("type")] string Type = GroupMemberDocument.DocumentType)
{
    public const string DocumentType = "GroupMember";

    public static GroupMemberDocument FromEntity(GroupMember entity) => new(
        Id: entity.Id,
        ChatId: entity.ChatId,
        UserId: entity.UserId,
        DisplayName: entity.DisplayName,
        Role: entity.Role.ToString(),
        JoinedAtUtc: entity.JoinedAtUtc,
        Username: entity.Username,
        IsActive: entity.IsActive
    );

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
