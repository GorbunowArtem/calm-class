namespace CalmClass.Application.Features.Polls.Commands.CancelPoll;

public record CancelPollResult
{
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
}
