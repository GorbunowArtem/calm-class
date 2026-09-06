namespace CalmClass.Application.Features.Polls.Commands.ClosePoll;

public record ClosePollResult
{
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
}
