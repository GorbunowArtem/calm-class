using CalmClass.Application.Common.Interfaces;
using CalmClass.Application.Domain.Enums;
using CalmClass.Application.Features.Polls.Localization;
using CalmClass.Application.Features.Polls.Services;
using Microsoft.Extensions.Logging;

namespace CalmClass.Application.Features.Polls.Commands.ClosePoll;

public record ClosePollCommand
{
    public required string ChatId { get; init; }
    public required long UserId { get; init; }
}

public record ClosePollResult
{
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
}

public class ClosePollCommandHandler
{
    private readonly IPollRepository _pollRepository;
    private readonly ITelegramBotClient _telegramBotClient;
    private readonly PollMonitorService _pollMonitorService;
    private readonly ILogger<ClosePollCommandHandler> _logger;

    public ClosePollCommandHandler(
        IPollRepository pollRepository,
        ITelegramBotClient telegramBotClient,
        PollMonitorService pollMonitorService,
        ILogger<ClosePollCommandHandler> logger)
    {
        _pollRepository = pollRepository;
        _telegramBotClient = telegramBotClient;
        _pollMonitorService = pollMonitorService;
        _logger = logger;
    }

    public async Task<ClosePollResult> HandleAsync(ClosePollCommand command, CancellationToken cancellationToken = default)
    {
        // 1. Check authorization
        var member = await _pollRepository.GetMemberAsync(command.ChatId, command.UserId, cancellationToken);
        if (member == null || !member.IsActive || member.Role != MemberRole.Admin)
        {
            _logger.LogWarning("Unauthorized /close_poll attempt by user {UserId} in chat {ChatId}", command.UserId, command.ChatId);
            await _telegramBotClient.SendMessageAsync(
                command.ChatId,
                UkrainianPollMessages.UnauthorizedAdminOnly,
                cancellationToken: cancellationToken);
            return new ClosePollResult { Success = false, ErrorMessage = UkrainianPollMessages.UnauthorizedAdminOnly };
        }

        // 2. Find active poll
        var poll = await _pollRepository.GetActivePollAsync(command.ChatId, cancellationToken);
        if (poll == null)
        {
            _logger.LogInformation("No active poll found to close in chat {ChatId}", command.ChatId);
            await _telegramBotClient.SendMessageAsync(
                command.ChatId,
                UkrainianPollMessages.NoActivePollFound,
                cancellationToken: cancellationToken);
            return new ClosePollResult { Success = false, ErrorMessage = UkrainianPollMessages.NoActivePollFound };
        }

        // 3. Finalize and publish results
        await _pollMonitorService.ClosePollInternalAsync(poll, cancellationToken);
        _logger.LogInformation("Successfully executed early /close_poll for poll {PollId} in chat {ChatId}", poll.PollId, command.ChatId);

        return new ClosePollResult { Success = true };
    }
}
