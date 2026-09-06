namespace CalmClass.Application.Features.Polls.Commands.CreatePoll;

using System.Text.RegularExpressions;
using CalmClass.Application.Common.Interfaces;
using CalmClass.Application.Common.Options;
using CalmClass.Application.Domain.Entities;
using CalmClass.Application.Domain.Enums;
using CalmClass.Application.Features.Polls.Localization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;


public class CreatePollCommandHandler(
    IPollRepository pollRepository,
    ITelegramBotClient telegramBotClient,
    IDateTimeProvider dateTimeProvider,
    IOptions<CalmClassOptions> options,
    ILogger<CreatePollCommandHandler> logger)
{
    public async Task<CreatePollResult> HandleAsync(CreatePollCommand command, CancellationToken cancellationToken = default)
    {
        var pollOptions = options.Value.Poll;

        // 1. Authorization check: must be active admin
        var member = await pollRepository.GetMemberAsync(command.ChatId, command.UserId, cancellationToken);
        if (member == null || !member.IsActive || member.Role != MemberRole.Admin)
        {
            logger.LogWarning("Unauthorized /create_poll attempt by user {UserId} in chat {ChatId}", command.UserId, command.ChatId);
            await telegramBotClient.SendMessageAsync(
                command.ChatId,
                UkrainianPollMessages.UnauthorizedAdminOnly,
                cancellationToken: cancellationToken);
            return new CreatePollResult { Success = false, ErrorMessage = UkrainianPollMessages.UnauthorizedAdminOnly };
        }

        // 2. Concurrency check: strictly single active poll per chat
        var activePoll = await pollRepository.GetActivePollAsync(command.ChatId, cancellationToken);
        if (activePoll != null)
        {
            logger.LogWarning("Rejecting /create_poll: chat {ChatId} already has active poll {PollId}", command.ChatId, activePoll.PollId);
            await telegramBotClient.SendMessageAsync(
                command.ChatId,
                UkrainianPollMessages.ActivePollAlreadyExists,
                cancellationToken: cancellationToken);
            return new CreatePollResult { Success = false, ErrorMessage = UkrainianPollMessages.ActivePollAlreadyExists };
        }

        // 3. Parse parameters from raw args if not provided
        var question = command.Question;
        var optionsList = command.Options?.ToList();
        var duration = command.DurationHours ?? pollOptions.DefaultDurationHours;

        if (!string.IsNullOrWhiteSpace(command.RawArgs) && (string.IsNullOrEmpty(question) || optionsList == null))
        {
            var parsed = ParseRawArguments(command.RawArgs, pollOptions.DefaultDurationHours);
            if (parsed == null)
            {
                await telegramBotClient.SendMessageAsync(
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
            optionsList.Count < pollOptions.MinOptionCount || optionsList.Count > pollOptions.MaxOptionCount)
        {
            await telegramBotClient.SendMessageAsync(
                command.ChatId,
                UkrainianPollMessages.InvalidOptionsCount,
                cancellationToken: cancellationToken);
            return new CreatePollResult { Success = false, ErrorMessage = UkrainianPollMessages.InvalidOptionsCount };
        }

        // 5. Validate duration
        if (duration < pollOptions.MinDurationHours || duration > pollOptions.MaxDurationHours)
        {
            await telegramBotClient.SendMessageAsync(
                command.ChatId,
                UkrainianPollMessages.InvalidDuration,
                cancellationToken: cancellationToken);
            return new CreatePollResult { Success = false, ErrorMessage = UkrainianPollMessages.InvalidDuration };
        }

        // 6. Publish non-anonymous poll via Telegram Bot API
        var telegramResult = await telegramBotClient.SendPollAsync(
            command.ChatId,
            question,
            optionsList,
            isAnonymous: false,
            allowsMultipleAnswers: false,
            cancellationToken: cancellationToken);

        var now = dateTimeProvider.UtcNow;
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
        await pollRepository.CreatePollAsync(trackedPoll, cancellationToken);
        logger.LogInformation("Created and tracked new poll {PollId} in chat {ChatId}", trackedPoll.PollId, command.ChatId);

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
