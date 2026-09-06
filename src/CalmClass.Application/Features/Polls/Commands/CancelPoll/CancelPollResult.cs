namespace CalmClass.Application.Features.Polls.Commands.CancelPoll;

public record CancelPollResult
{
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }

    public static CancelPollResult Succeeded() => new()
    {
        Success = true
    };

    public static CancelPollResult Failed(string errorMessage) => new()
    {
        Success = false,
        ErrorMessage = errorMessage
    };
}
