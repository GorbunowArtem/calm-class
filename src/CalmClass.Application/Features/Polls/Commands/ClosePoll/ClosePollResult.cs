namespace CalmClass.Application.Features.Polls.Commands.ClosePoll;

public record ClosePollResult
{
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }

    public static ClosePollResult Succeeded() => new()
    {
        Success = true
    };

    public static ClosePollResult Failed(string errorMessage) => new()
    {
        Success = false,
        ErrorMessage = errorMessage
    };
}
