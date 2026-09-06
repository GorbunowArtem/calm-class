namespace CalmClass.Application.Features.Polls.Commands.CancelPoll;

using CalmClass.Application.Common.Interfaces;
using CalmClass.Application.Domain.Entities;
using CalmClass.Application.Domain.Enums;
using CalmClass.Application.Features.Polls.Localization;
using Microsoft.Extensions.Logging;

public class CancelPollCommandHandler(
    IPollRepository pollRepository,
    ITelegramBotClient telegramBotClient,
    IDateTimeProvider dateTimeProvider,
    ILogger<CancelPollCommandHandler> logger)
{
    private const string CommandName = "/cancel_poll";

    public async Task<CancelPollResult> HandleAsync(CancelPollCommand command, CancellationToken cancellationToken = default)
    {
        if (!await IsAuthorizedAdminAsync(command.ChatId, command.UserId, cancellationToken))
        {
            logger.LogWarning("Unauthorized {Command} attempt by user {UserId} in chat {ChatId}", CommandName, command.UserId, command.ChatId);
            return await FailAsync(command.ChatId, UkrainianPollMessages.UnauthorizedAdminOnly, cancellationToken);
        }

        var activePoll = await pollRepository.GetActivePollAsync(command.ChatId, cancellationToken);
        if (activePoll == null)
        {
            logger.LogInformation("No active poll found to cancel in chat {ChatId}", command.ChatId);
            return await FailAsync(command.ChatId, UkrainianPollMessages.NoActivePollFound, cancellationToken);
        }

        return await CancelAndNotifyAsync(activePoll, cancellationToken);
    }

    private async Task<bool> IsAuthorizedAdminAsync(string chatId, long userId, CancellationToken cancellationToken)
    {
        var member = await pollRepository.GetMemberAsync(chatId, userId, cancellationToken);
        return member is { IsActive: true, Role: MemberRole.Admin };
    }

    private async Task<CancelPollResult> FailAsync(string chatId, string errorMessage, CancellationToken cancellationToken)
    {
        await telegramBotClient.SendMessageAsync(
            chatId,
            errorMessage,
            parseMode: "MarkdownV2",
            disableNotification: false,
            cancellationToken: cancellationToken);

        return CancelPollResult.Failed(errorMessage);
    }

    private async Task<CancelPollResult> CancelAndNotifyAsync(TrackedPoll poll, CancellationToken cancellationToken)
    {
        await telegramBotClient.StopPollAsync(poll.ChatId, poll.MessageId, cancellationToken);

        await telegramBotClient.SendMessageAsync(
            poll.ChatId,
            UkrainianPollMessages.PollCancelled,
            parseMode: "MarkdownV2",
            disableNotification: false,
            cancellationToken: cancellationToken);

        var cancelledPoll = poll with
        {
            ClosedAtUtc = dateTimeProvider.UtcNow,
            Status = PollStatus.Cancelled
        };

        await pollRepository.UpdatePollAsync(cancelledPoll, cancellationToken);
        logger.LogInformation("Cancelled poll {PollId} in chat {ChatId}", poll.PollId, poll.ChatId);

        return CancelPollResult.Succeeded();
    }
}
