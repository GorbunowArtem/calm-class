namespace CalmClass.Application.Features.Polls.Commands.CreatePoll;

using CalmClass.Application.Domain.Entities;

public record CreatePollResult
{
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
    public TrackedPoll? Poll { get; init; }

    public static CreatePollResult Succeeded(TrackedPoll poll) => new()
    {
        Success = true,
        Poll = poll
    };

    public static CreatePollResult Failed(string errorMessage) => new()
    {
        Success = false,
        ErrorMessage = errorMessage
    };
}
