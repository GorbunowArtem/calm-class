namespace CalmClass.Application.Features.Polls.Commands.CancelPoll;

public record CancelPollCommand
{
    public required string ChatId { get; init; }
    public required long UserId { get; init; }
}
