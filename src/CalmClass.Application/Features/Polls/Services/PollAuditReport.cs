namespace CalmClass.Application.Features.Polls.Services;

using CalmClass.Application.Domain.Enums;

public record PollAuditReport
{
    public required string PollId { get; init; }
    public required string Question { get; init; }
    public required PollStatus Status { get; init; }
    public required IReadOnlyList<VoterAuditRecord> Voters { get; init; }
}
