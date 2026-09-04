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

public class CancelPollCommandHandler
{
    private readonly IPollRepository _pollRepository;
    private readonly ITelegramBotClient _telegramBotClient;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly ILogger<CancelPollCommandHandler> _logger;

    public CancelPollCommandHandler(
        IPollRepository pollRepository,
        ITelegramBotClient telegramBotClient,
        IDateTimeProvider dateTimeProvider,
        ILogger<CancelPollCommandHandler> logger)
    {
        _pollRepository = pollRepository;
        _telegramBotClient = telegramBotClient;
        _dateTimeProvider = dateTimeProvider;
        _logger = logger;
    }

    public async Task<CancelPollResult> HandleAsync(CancelPollCommand command, CancellationToken cancellationToken = default)
    {
        // 1. Check authorization
        var member = await _pollRepository.GetMemberAsync(command.ChatId, command.UserId, cancellationToken);
        if (member == null || !member.IsActive || member.Role != MemberRole.Admin)
        {
            _logger.LogWarning("Unauthorized /cancel_poll attempt by user {UserId} in chat {ChatId}", command.UserId, command.ChatId);
            await _telegramBotClient.SendMessageAsync(
                command.ChatId,
                UkrainianPollMessages.UnauthorizedAdminOnly,
                cancellationToken: cancellationToken);
            return new CancelPollResult { Success = false, ErrorMessage = UkrainianPollMessages.UnauthorizedAdminOnly };
        }

        // 2. Find active poll
        var poll = await _pollRepository.GetActivePollAsync(command.ChatId, cancellationToken);
        if (poll == null)
        {
            _logger.LogInformation("No active poll found to cancel in chat {ChatId}", command.ChatId);
            await _telegramBotClient.SendMessageAsync(
                command.ChatId,
                UkrainianPollMessages.NoActivePollFound,
                cancellationToken: cancellationToken);
            return new CancelPollResult { Success = false, ErrorMessage = UkrainianPollMessages.NoActivePollFound };
        }

        // 3. Stop voting in Telegram
        await _telegramBotClient.StopPollAsync(poll.ChatId, poll.MessageId, cancellationToken);

        // 4. Send cancellation message
        await _telegramBotClient.SendMessageAsync(
            poll.ChatId,
            UkrainianPollMessages.PollCancelled,
            parseMode: "MarkdownV2",
            disableNotification: false,
            cancellationToken: cancellationToken);

        // 5. Update poll status to Cancelled
        var cancelledPoll = poll with
        {
            ClosedAtUtc = _dateTimeProvider.UtcNow,
            Status = PollStatus.Cancelled
        };

        await _pollRepository.UpdatePollAsync(cancelledPoll, cancellationToken);
        _logger.LogInformation("Cancelled poll {PollId} in chat {ChatId}", poll.PollId, command.ChatId);

        return new CancelPollResult { Success = true };
    }
}
