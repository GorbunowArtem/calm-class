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
    public async Task<ClosePollResult> HandleAsync(ClosePollCommand command, CancellationToken cancellationToken = default)
    {
        // 1. Check authorization
        var member = await pollRepository.GetMemberAsync(command.ChatId, command.UserId, cancellationToken);
        if (member == null || !member.IsActive || member.Role != MemberRole.Admin)
        {
            logger.LogWarning("Unauthorized /close_poll attempt by user {UserId} in chat {ChatId}", command.UserId, command.ChatId);
            await telegramBotClient.SendMessageAsync(
                command.ChatId,
                UkrainianPollMessages.UnauthorizedAdminOnly,
                cancellationToken: cancellationToken);
            return new ClosePollResult { Success = false, ErrorMessage = UkrainianPollMessages.UnauthorizedAdminOnly };
        }

        // 2. Find active poll
        var poll = await pollRepository.GetActivePollAsync(command.ChatId, cancellationToken);
        if (poll == null)
        {
            logger.LogInformation("No active poll found to close in chat {ChatId}", command.ChatId);
            await telegramBotClient.SendMessageAsync(
                command.ChatId,
                UkrainianPollMessages.NoActivePollFound,
                cancellationToken: cancellationToken);
            return new ClosePollResult { Success = false, ErrorMessage = UkrainianPollMessages.NoActivePollFound };
        }

        // 3. Finalize and publish results
        await pollMonitorService.ClosePollInternalAsync(poll, cancellationToken);
        logger.LogInformation("Successfully executed early /close_poll for poll {PollId} in chat {ChatId}", poll.PollId, command.ChatId);

        return new ClosePollResult { Success = true };
    }
}
