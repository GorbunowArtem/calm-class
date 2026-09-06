namespace CalmClass.Application.Common.Options;

public record TelegramOptions
{
    public const string SectionName = "Telegram";

    public string BotToken { get; init; } = string.Empty;
    public string SecretToken { get; init; } = string.Empty;
    public string BaseUrl { get; init; } = "https://api.telegram.org";
}
