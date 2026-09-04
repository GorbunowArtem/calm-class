using CalmClass.Application.Common.Interfaces;
using CalmClass.Application.Domain.Enums;
using CalmClass.Application.Features.Polls.Localization;
using Microsoft.Extensions.Logging;

namespace CalmClass.Application.Features.Polls.Commands.CancelPoll;

public record CancelPollCommand
{
    public required string ChatId { get; init; }
    public required long UserId { get; init; }
}

public record CancelPollResult
{
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
}

public class CancelPollCommandHandler(
    IPollRepository pollRepository,
    ITelegramBotClient telegramBotClient,
    IDateTimeProvider dateTimeProvider,
    ILogger<CancelPollCommandHandler> logger)
{
    public async Task<CancelPollResult> HandleAsync(CancelPollCommand command, CancellationToken cancellationToken = default)
    {
        // 1. Check authorization
        var member = await pollRepository.GetMemberAsync(command.ChatId, command.UserId, cancellationToken);
        if (member == null || !member.IsActive || member.Role != MemberRole.Admin)
        {
            logger.LogWarning("Unauthorized /cancel_poll attempt by user {UserId} in chat {ChatId}", command.UserId, command.ChatId);
            await telegramBotClient.SendMessageAsync(
                command.ChatId,
                UkrainianPollMessages.UnauthorizedAdminOnly,
                cancellationToken: cancellationToken);
            return new CancelPollResult { Success = false, ErrorMessage = UkrainianPollMessages.UnauthorizedAdminOnly };
        }

        // 2. Find active poll
        var poll = await pollRepository.GetActivePollAsync(command.ChatId, cancellationToken);
        if (poll == null)
        {
            logger.LogInformation("No active poll found to cancel in chat {ChatId}", command.ChatId);
            await telegramBotClient.SendMessageAsync(
                command.ChatId,
                UkrainianPollMessages.NoActivePollFound,
                cancellationToken: cancellationToken);
            return new CancelPollResult { Success = false, ErrorMessage = UkrainianPollMessages.NoActivePollFound };
        }

        // 3. Stop voting in Telegram
        await telegramBotClient.StopPollAsync(poll.ChatId, poll.MessageId, cancellationToken);

        // 4. Send cancellation message
        await telegramBotClient.SendMessageAsync(
            poll.ChatId,
            UkrainianPollMessages.PollCancelled,
            parseMode: "MarkdownV2",
            disableNotification: false,
            cancellationToken: cancellationToken);

        // 5. Update poll status to Cancelled
        var cancelledPoll = poll with
        {
            ClosedAtUtc = dateTimeProvider.UtcNow,
            Status = PollStatus.Cancelled
        };

        await pollRepository.UpdatePollAsync(cancelledPoll, cancellationToken);
        logger.LogInformation("Cancelled poll {PollId} in chat {ChatId}", poll.PollId, command.ChatId);

        return new CancelPollResult { Success = true };
    }
}
