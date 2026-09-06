namespace CalmClass.Application.Features.Polls.Commands.CreatePoll;

using CalmClass.Application.Domain.Entities;

public record CreatePollResult
{
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
    public TrackedPoll? Poll { get; init; }
}
