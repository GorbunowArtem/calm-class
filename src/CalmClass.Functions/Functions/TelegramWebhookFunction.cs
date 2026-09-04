namespace CalmClass.Functions.Functions;

using System.Net;
using System.Text.Json;
using CalmClass.Application.Common.Interfaces;
using CalmClass.Application.Features.Polls.Commands.CancelPoll;
using CalmClass.Application.Features.Polls.Commands.ClosePoll;
using CalmClass.Application.Features.Polls.Commands.CreatePoll;
using CalmClass.Application.Features.Polls.Commands.IngestVote;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

public class TelegramWebhookFunction(
    CreatePollCommandHandler createPollHandler,
    ClosePollCommandHandler closePollHandler,
    CancelPollCommandHandler cancelPollHandler,
    IngestVoteCommandHandler ingestVoteHandler,
    ITelegramBotClient telegramBotClient,
    ILogger<TelegramWebhookFunction> logger)
{
    [Function("TelegramWebhook")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "telegram/webhook")] HttpRequestData req,
        CancellationToken cancellationToken)
    {
        logger.LogInformation("Received Telegram webhook event");
        string? currentChatId = null;

        try
        {
            var body = await req.ReadAsStringAsync();
            if (string.IsNullOrWhiteSpace(body))
            {
                var emptyResponse = req.CreateResponse(HttpStatusCode.OK);
                return emptyResponse;
            }

            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;

            if (root.TryGetProperty("message", out var message))
            {
                if (message.TryGetProperty("text", out var textProp) &&
                    message.TryGetProperty("chat", out var chat) &&
                    message.TryGetProperty("from", out var from))
                {
                    var text = textProp.GetString() ?? string.Empty;
                    var chatId = chat.GetProperty("id").GetInt64().ToString();
                    var userId = from.GetProperty("id").GetInt64();
                    currentChatId = chatId;

                    if (text.StartsWith("/start", StringComparison.OrdinalIgnoreCase) || text.StartsWith("/help", StringComparison.OrdinalIgnoreCase))
                    {
                        await telegramBotClient.SendMessageAsync(
                            chatId,
                            "👋 *Вітаємо у CalmClass\\!*\n\n" +
                            "Бот для прозорих та автоматизованих опитувань у шкільних чатах\\.\n\n" +
                            "📌 *Доступні команди:*\n" +
                            "• `/create_poll \"Тема\" \"Варіант 1\" \"Варіант 2\" [години]` — створити нове опитування \\(лише для адмінів\\)\n" +
                            "• `/close_poll` — достроково закрити опитування та отримати результати\n" +
                            "• `/cancel_poll` — скасувати активне опитування",
                            cancellationToken: cancellationToken);
                    }
                    else if (text.StartsWith("/create_poll", StringComparison.OrdinalIgnoreCase))
                    {
                        var spaceIdx = text.IndexOf(' ');
                        var rawArgs = spaceIdx >= 0 ? text[(spaceIdx + 1)..].Trim() : string.Empty;

                        await createPollHandler.HandleAsync(new CreatePollCommand
                        {
                            ChatId = chatId,
                            UserId = userId,
                            RawArgs = rawArgs
                        }, cancellationToken);
                    }
                    else if (text.StartsWith("/close_poll", StringComparison.OrdinalIgnoreCase))
                    {
                        await closePollHandler.HandleAsync(new ClosePollCommand
                        {
                            ChatId = chatId,
                            UserId = userId
                        }, cancellationToken);
                    }
                    else if (text.StartsWith("/cancel_poll", StringComparison.OrdinalIgnoreCase))
                    {
                        await cancelPollHandler.HandleAsync(new CancelPollCommand
                        {
                            ChatId = chatId,
                            UserId = userId
                        }, cancellationToken);
                    }
                }
            }
            else if (root.TryGetProperty("poll_answer", out var pollAnswer))
            {
                if (pollAnswer.TryGetProperty("poll_id", out var pollIdProp) &&
                    pollAnswer.TryGetProperty("user", out var user))
                {
                    var pollId = pollIdProp.GetString()!;
                    var userId = user.GetProperty("id").GetInt64();
                    var firstName = user.TryGetProperty("first_name", out var fn) ? fn.GetString() ?? "" : "";
                    var lastName = user.TryGetProperty("last_name", out var ln) ? ln.GetString() ?? "" : "";
                    var displayName = string.IsNullOrWhiteSpace(lastName) ? firstName : $"{firstName} {lastName}".Trim();
                    var username = user.TryGetProperty("username", out var un) ? un.GetString() : null;

                    var optionIndices = new List<int>();
                    if (pollAnswer.TryGetProperty("option_ids", out var optionIdsProp) &&
                        optionIdsProp.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var elem in optionIdsProp.EnumerateArray())
                        {
                            if (elem.TryGetInt32(out var idx))
                            {
                                optionIndices.Add(idx);
                            }
                        }
                    }

                    await ingestVoteHandler.HandleAsync(new IngestVoteCommand
                    {
                        PollId = pollId,
                        UserId = userId,
                        DisplayName = displayName,
                        Username = username,
                        SelectedOptionIndices = optionIndices
                    }, cancellationToken);
                }
            }

            var okResponse = req.CreateResponse(HttpStatusCode.OK);
            await okResponse.WriteStringAsync("OK");
            return okResponse;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error handling Telegram webhook event");
            if (!string.IsNullOrEmpty(currentChatId))
            {
                try
                {
                    await telegramBotClient.SendMessageAsync(
                        currentChatId,
                        "⚠️ Помилка з'єднання з базою даних Cosmos DB. Перевірте підключення до бази даних.",
                        cancellationToken: cancellationToken);
                }
                catch (Exception sendEx)
                {
                    logger.LogWarning(sendEx, "Could not send error notification to chat {ChatId}", currentChatId);
                }
            }

            var okResponse = req.CreateResponse(HttpStatusCode.OK);
            return okResponse;
        }
    }
}
