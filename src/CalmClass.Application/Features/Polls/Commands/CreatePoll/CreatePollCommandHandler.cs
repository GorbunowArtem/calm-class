using System.Text.RegularExpressions;
using CalmClass.Application.Common.Interfaces;
using CalmClass.Application.Common.Options;
using CalmClass.Application.Domain.Entities;
using CalmClass.Application.Domain.Enums;
using CalmClass.Application.Features.Polls.Localization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CalmClass.Application.Features.Polls.Commands.CreatePoll;

public record CreatePollCommand
{
    public required string ChatId { get; init; }
    public required long UserId { get; init; }
    public string? RawArgs { get; init; }
    public string? Question { get; init; }
    public IReadOnlyList<string>? Options { get; init; }
    public int? DurationHours { get; init; }
}

public record CreatePollResult
{
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
    public TrackedPoll? Poll { get; init; }
}

public class CreatePollCommandHandler
{
    private readonly IPollRepository _pollRepository;
    private readonly ITelegramBotClient _telegramBotClient;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly PollOptions _pollOptions;
    private readonly ILogger<CreatePollCommandHandler> _logger;

    public CreatePollCommandHandler(
        IPollRepository pollRepository,
        ITelegramBotClient telegramBotClient,
        IDateTimeProvider dateTimeProvider,
        IOptions<CalmClassOptions> options,
        ILogger<CreatePollCommandHandler> logger)
    {
        _pollRepository = pollRepository;
        _telegramBotClient = telegramBotClient;
        _dateTimeProvider = dateTimeProvider;
        _pollOptions = options.Value.Poll;
        _logger = logger;
    }

    public async Task<CreatePollResult> HandleAsync(CreatePollCommand command, CancellationToken cancellationToken = default)
    {
        // 1. Authorization check: must be active admin
        var member = await _pollRepository.GetMemberAsync(command.ChatId, command.UserId, cancellationToken);
        if (member == null || !member.IsActive || member.Role != MemberRole.Admin)
        {
            _logger.LogWarning("Unauthorized /create_poll attempt by user {UserId} in chat {ChatId}", command.UserId, command.ChatId);
            await _telegramBotClient.SendMessageAsync(
                command.ChatId,
                UkrainianPollMessages.UnauthorizedAdminOnly,
                cancellationToken: cancellationToken);
            return new CreatePollResult { Success = false, ErrorMessage = UkrainianPollMessages.UnauthorizedAdminOnly };
        }

        // 2. Concurrency check: strictly single active poll per chat
        var activePoll = await _pollRepository.GetActivePollAsync(command.ChatId, cancellationToken);
        if (activePoll != null)
        {
            _logger.LogWarning("Rejecting /create_poll: chat {ChatId} already has active poll {PollId}", command.ChatId, activePoll.PollId);
            await _telegramBotClient.SendMessageAsync(
                command.ChatId,
                UkrainianPollMessages.ActivePollAlreadyExists,
                cancellationToken: cancellationToken);
            return new CreatePollResult { Success = false, ErrorMessage = UkrainianPollMessages.ActivePollAlreadyExists };
        }

        // 3. Parse parameters from raw args if not provided
        var question = command.Question;
        var optionsList = command.Options?.ToList();
        var duration = command.DurationHours ?? _pollOptions.DefaultDurationHours;

        if (!string.IsNullOrWhiteSpace(command.RawArgs) && (string.IsNullOrEmpty(question) || optionsList == null))
        {
            var parsed = ParseRawArguments(command.RawArgs, _pollOptions.DefaultDurationHours);
            if (parsed == null)
            {
                await _telegramBotClient.SendMessageAsync(
                    command.ChatId,
                    $"{UkrainianPollMessages.CreatePollUsage}",
                    cancellationToken: cancellationToken);
                return new CreatePollResult { Success = false, ErrorMessage = UkrainianPollMessages.CreatePollUsage };
            }

            question = parsed.Value.Question;
            optionsList = parsed.Value.Options;
            duration = command.DurationHours ?? parsed.Value.DurationHours;
        }

        // 4. Validate question & options
        if (string.IsNullOrWhiteSpace(question) || optionsList == null ||
            optionsList.Count < _pollOptions.MinOptionCount || optionsList.Count > _pollOptions.MaxOptionCount)
        {
            await _telegramBotClient.SendMessageAsync(
                command.ChatId,
                UkrainianPollMessages.InvalidOptionsCount,
                cancellationToken: cancellationToken);
            return new CreatePollResult { Success = false, ErrorMessage = UkrainianPollMessages.InvalidOptionsCount };
        }

        // 5. Validate duration
        if (duration < _pollOptions.MinDurationHours || duration > _pollOptions.MaxDurationHours)
        {
            await _telegramBotClient.SendMessageAsync(
                command.ChatId,
                UkrainianPollMessages.InvalidDuration,
                cancellationToken: cancellationToken);
            return new CreatePollResult { Success = false, ErrorMessage = UkrainianPollMessages.InvalidDuration };
        }

        // 6. Publish non-anonymous poll via Telegram Bot API
        var telegramResult = await _telegramBotClient.SendPollAsync(
            command.ChatId,
            question,
            optionsList,
            isAnonymous: false,
            allowsMultipleAnswers: false,
            cancellationToken: cancellationToken);

        var now = _dateTimeProvider.UtcNow;
        var trackedPoll = new TrackedPoll
        {
            ChatId = command.ChatId,
            PollId = telegramResult.PollId,
            MessageId = telegramResult.MessageId,
            Question = question,
            Options = optionsList,
            AllowsMultipleAnswers = false,
            CreatedAtUtc = now,
            ExpiresAtUtc = now.AddHours(duration),
            Status = PollStatus.Open
        };

        // 7. Persist tracked poll
        await _pollRepository.CreatePollAsync(trackedPoll, cancellationToken);
        _logger.LogInformation("Created and tracked new poll {PollId} in chat {ChatId}", trackedPoll.PollId, command.ChatId);

        return new CreatePollResult { Success = true, Poll = trackedPoll };
    }

    public static (string Question, List<string> Options, int DurationHours)? ParseRawArguments(string raw, int defaultDuration)
    {
        // Extracts all quoted tokens "..." or '...' or unquoted words
        var matches = Regex.Matches(raw, @"[""']([^""']+)[""']|(\S+)");
        var tokens = new List<string>();
        foreach (Match match in matches)
        {
            if (match.Groups[1].Success)
            {
                tokens.Add(match.Groups[1].Value.Trim());
            }
            else if (match.Groups[2].Success)
            {
                tokens.Add(match.Groups[2].Value.Trim());
            }
        }

        if (tokens.Count < 3) // At minimum question + 2 options
        {
            return null;
        }

        var duration = defaultDuration;
        // Check if last token is numeric duration
        if (tokens.Count >= 4 && int.TryParse(tokens[^1], out var parsedDuration))
        {
            duration = parsedDuration;
            tokens.RemoveAt(tokens.Count - 1);
        }

        var question = tokens[0];
        var options = tokens.Skip(1).ToList();

        return (question, options, duration);
    }
}
