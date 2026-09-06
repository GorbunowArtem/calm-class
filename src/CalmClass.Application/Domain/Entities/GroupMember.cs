namespace CalmClass.Application.Domain.Entities;

using CalmClass.Application.Domain.Enums;

public record GroupMember
{
    public string Id => $"member_{ChatId}_{UserId}";
    public required string ChatId { get; init; }
    public required long UserId { get; init; }
    public required string DisplayName { get; init; }
    public string? Username { get; init; }
    public MemberRole Role { get; init; } = MemberRole.Member;
    public bool IsActive { get; init; } = true;
    public required DateTime JoinedAtUtc { get; init; }
}
