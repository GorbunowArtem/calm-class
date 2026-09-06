namespace CalmClass.Application.Features.Polls.Commands.ClosePoll;

using CalmClass.Application.Common.Interfaces;
using CalmClass.Application.Domain.Enums;
using CalmClass.Application.Features.Polls.Localization;
using CalmClass.Application.Features.Polls.Services;
using Microsoft.Extensions.Logging;

public class ClosePollCommandHandler(
    IPollRepository pollRepository,
    ITelegramBotClient telegramBotClient,
    PollMonitorService pollMonitorService,
    ILogger<ClosePollCommandHandler> logger)
{
    private const string CommandName = "/close_poll";

    public async Task<ClosePollResult> HandleAsync(ClosePollCommand command, CancellationToken cancellationToken = default)
    {
        if (!await IsAuthorizedAdminAsync(command.ChatId, command.UserId, cancellationToken))
        {
            logger.LogWarning("Unauthorized {Command} attempt by user {UserId} in chat {ChatId}", CommandName, command.UserId, command.ChatId);
            return await FailAsync(command.ChatId, UkrainianPollMessages.UnauthorizedAdminOnly, cancellationToken);
        }

        var activePoll = await pollRepository.GetActivePollAsync(command.ChatId, cancellationToken);
        if (activePoll == null)
        {
            logger.LogInformation("No active poll found to close in chat {ChatId}", command.ChatId);
            return await FailAsync(command.ChatId, UkrainianPollMessages.NoActivePollFound, cancellationToken);
        }

        await pollMonitorService.ClosePollInternalAsync(activePoll, cancellationToken);
        logger.LogInformation("Successfully executed early {Command} for poll {PollId} in chat {ChatId}", CommandName, activePoll.PollId, command.ChatId);

        return ClosePollResult.Succeeded();
    }

    private async Task<bool> IsAuthorizedAdminAsync(string chatId, long userId, CancellationToken cancellationToken)
    {
        var member = await pollRepository.GetMemberAsync(chatId, userId, cancellationToken);
        return member is { IsActive: true, Role: MemberRole.Admin };
    }

    private async Task<ClosePollResult> FailAsync(string chatId, string errorMessage, CancellationToken cancellationToken)
    {
        await telegramBotClient.SendMessageAsync(
            chatId,
            errorMessage,
            cancellationToken: cancellationToken);

        return ClosePollResult.Failed(errorMessage);
    }
}
