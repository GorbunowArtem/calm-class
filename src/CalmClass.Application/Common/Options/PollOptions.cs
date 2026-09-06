namespace CalmClass.Application.Common.Options;

public record PollOptions
{
    public const string SectionName = "Poll";

    public int DefaultDurationHours { get; init; } = 24;
    public int ReminderHoursBeforeExpiry { get; init; } = 6;
    public int MinOptionCount { get; init; } = 2;
    public int MaxOptionCount { get; init; } = 10;
    public int MinDurationHours { get; init; } = 1;
    public int MaxDurationHours { get; init; } = 168;
}
