namespace CalmClass.Application.Common.Interfaces;


public interface ITelegramBotClient
{
    Task<TelegramPollResult> SendPollAsync(
        string chatId,
        string question,
        IReadOnlyList<string> options,
        bool isAnonymous = false,
        bool allowsMultipleAnswers = false,
        CancellationToken cancellationToken = default);

    Task StopPollAsync(
        string chatId,
        int messageId,
        CancellationToken cancellationToken = default);

    Task<int> SendMessageAsync(
        string chatId,
        string text,
        string parseMode = "MarkdownV2",
        bool disableNotification = false,
        CancellationToken cancellationToken = default);
}
