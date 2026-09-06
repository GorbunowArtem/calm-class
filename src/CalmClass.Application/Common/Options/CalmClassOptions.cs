namespace CalmClass.Application.Common.Options;

public record CalmClassOptions
{
    public const string SectionName = "CalmClass";

    public TelegramOptions Telegram { get; init; } = new();
    public CosmosDbOptions CosmosDb { get; init; } = new();
    public QuietHoursOptions QuietHours { get; init; } = new();
    public PollOptions Poll { get; init; } = new();
}
