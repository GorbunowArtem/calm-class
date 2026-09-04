using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using CalmClass.Application.Common.Interfaces;
using CalmClass.Application.Common.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;

namespace CalmClass.Infrastructure.Telegram;

public class TelegramBotClient : ITelegramBotClient
{
    private readonly HttpClient _httpClient;
    private readonly TelegramOptions _options;
    private readonly ResiliencePipeline<HttpResponseMessage> _pipeline;
    private readonly ILogger<TelegramBotClient> _logger;

    public TelegramBotClient(
        HttpClient httpClient,
        IOptions<CalmClassOptions> options,
        ILogger<TelegramBotClient> logger)
    {
        _httpClient = httpClient;
        _options = options.Value.Telegram;
        _logger = logger;
        _pipeline = TelegramResiliencePipeline.CreatePipeline(logger);
    }

    private string BuildUrl(string method) =>
        $"{_options.BaseUrl.TrimEnd('/')}/bot{_options.BotToken}/{method}";

    public async Task<TelegramPollResult> SendPollAsync(
        string chatId,
        string question,
        IReadOnlyList<string> options,
        bool isAnonymous = false,
        bool allowsMultipleAnswers = false,
        CancellationToken cancellationToken = default)
    {
        var url = BuildUrl("sendPoll");
        // In Telegram Bot API, options are array of objects with "text" property
        var formattedOptions = options.Select(opt => new { text = opt }).ToArray();

        var payload = new
        {
            chat_id = chatId,
            question,
            options = formattedOptions,
            is_anonymous = isAnonymous,
            allows_multiple_answers = allowsMultipleAnswers
        };

        var response = await ExecuteWithResilienceAsync(
            () => _httpClient.PostAsJsonAsync(url, payload, cancellationToken),
            cancellationToken);

        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsStringAsync(cancellationToken);

        using var doc = JsonDocument.Parse(content);
        var result = doc.RootElement.GetProperty("result");
        var messageId = result.GetProperty("message_id").GetInt32();
        var pollId = result.GetProperty("poll").GetProperty("id").GetString()!;

        _logger.LogInformation("Successfully posted poll {PollId} in chat {ChatId}, messageId {MessageId}", pollId, chatId, messageId);

        return new TelegramPollResult
        {
            PollId = pollId,
            MessageId = messageId
        };
    }

    public async Task StopPollAsync(
        string chatId,
        int messageId,
        CancellationToken cancellationToken = default)
    {
        var url = BuildUrl("stopPoll");
        var payload = new
        {
            chat_id = chatId,
            message_id = messageId
        };

        var response = await ExecuteWithResilienceAsync(
            () => _httpClient.PostAsJsonAsync(url, payload, cancellationToken),
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var err = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogWarning("Failed to stop poll message {MessageId} in chat {ChatId}: {Error}", messageId, chatId, err);
        }
    }

    public async Task<int> SendMessageAsync(
        string chatId,
        string text,
        string parseMode = "MarkdownV2",
        bool disableNotification = false,
        CancellationToken cancellationToken = default)
    {
        var url = BuildUrl("sendMessage");
        var payload = new
        {
            chat_id = chatId,
            text,
            parse_mode = parseMode,
            disable_notification = disableNotification
        };

        var response = await ExecuteWithResilienceAsync(
            () => _httpClient.PostAsJsonAsync(url, payload, cancellationToken),
            cancellationToken);

        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsStringAsync(cancellationToken);

        using var doc = JsonDocument.Parse(content);
        var messageId = doc.RootElement.GetProperty("result").GetProperty("message_id").GetInt32();

        return messageId;
    }

    private async Task<HttpResponseMessage> ExecuteWithResilienceAsync(
        Func<Task<HttpResponseMessage>> action,
        CancellationToken cancellationToken)
    {
        return await _pipeline.ExecuteAsync(
            async state => await action(),
            cancellationToken);
    }
}
