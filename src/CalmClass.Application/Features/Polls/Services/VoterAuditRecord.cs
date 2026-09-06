namespace CalmClass.Application.Features.Polls.Services;

public record VoterAuditRecord
{
    public required long UserId { get; init; }
    public required string DisplayName { get; init; }
    public string? Username { get; init; }
    public required IReadOnlyList<string> SelectedOptions { get; init; }
    public required DateTime VotedAtUtc { get; init; }
}
