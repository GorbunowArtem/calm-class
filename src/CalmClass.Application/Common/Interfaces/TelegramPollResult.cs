namespace CalmClass.Application.Common.Interfaces;

public record TelegramPollResult
{
    public required string PollId { get; init; }
    public required int MessageId { get; init; }
}
