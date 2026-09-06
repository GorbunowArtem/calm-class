namespace CalmClass.Application.Domain.Entities;

public record PollVote
{
    public string Id => $"vote_{PollId}_{UserId}";
    public required string ChatId { get; init; }
    public required string PollId { get; init; }
    public required long UserId { get; init; }
    public required string DisplayName { get; init; }
    public string? Username { get; init; }
    public required IReadOnlyList<int> SelectedOptionIndices { get; init; }
    public required DateTime VotedAtUtc { get; init; }
    public bool IsRevoked { get; init; }
}
