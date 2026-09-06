namespace CalmClass.Application.Features.Polls.Commands.IngestVote;

using CalmClass.Application.Domain.Entities;

public record IngestVoteResult
{
    public bool Success { get; init; }
    public string? Reason { get; init; }
    public PollVote? Vote { get; init; }
}
