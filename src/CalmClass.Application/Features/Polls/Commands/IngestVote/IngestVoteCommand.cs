namespace CalmClass.Application.Features.Polls.Commands.IngestVote;

public record IngestVoteCommand
{
    public required string PollId { get; init; }
    public required long UserId { get; init; }
    public required string DisplayName { get; init; }
    public string? Username { get; init; }
    public required IReadOnlyList<int> SelectedOptionIndices { get; init; }
}
