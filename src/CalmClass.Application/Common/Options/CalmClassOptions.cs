namespace CalmClass.Application.Common.Options;

public record CalmClassOptions
{
    public const string SectionName = "CalmClass";

    public TelegramOptions Telegram { get; init; } = new();
    public CosmosDbOptions CosmosDb { get; init; } = new();
    public QuietHoursOptions QuietHours { get; init; } = new();
    public PollOptions Poll { get; init; } = new();
}

public record TelegramOptions
{
    public const string SectionName = "Telegram";

    public string BotToken { get; init; } = string.Empty;
    public string SecretToken { get; init; } = string.Empty;
    public string BaseUrl { get; init; } = "https://api.telegram.org";
}

public record CosmosDbOptions
{
    public const string SectionName = "CosmosDb";

    public string ConnectionString { get; init; } = string.Empty;
    public string DatabaseName { get; init; } = "CalmClassDb";
    public string ContainerName { get; init; } = "Polls";
}

public record QuietHoursOptions
{
    public const string SectionName = "QuietHours";

    public int StartHour { get; init; } = 20; // 20:00 (8 PM) Kyiv time
    public int EndHour { get; init; } = 8;     // 08:00 (8 AM) Kyiv time
    public string TimeZoneId { get; init; } = "Europe/Kyiv";
}

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
