namespace CalmClass.Application.Features.Polls.Commands.ClosePoll;

public record ClosePollCommand
{
    public required string ChatId { get; init; }
    public required long UserId { get; init; }
}
