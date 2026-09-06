namespace CalmClass.Application.Features.Polls.Commands.CreatePoll;

public record CreatePollCommand
{
    public required string ChatId { get; init; }
    public required long UserId { get; init; }
    public string? RawArgs { get; init; }
    public string? Question { get; init; }
    public IReadOnlyList<string>? Options { get; init; }
    public int? DurationHours { get; init; }
}
