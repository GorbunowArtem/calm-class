using CalmClass.Application.Common.Interfaces;
using CalmClass.Application.Common.Options;
using CalmClass.Application.Domain.Entities;
using CalmClass.Application.Domain.Enums;
using CalmClass.Application.Common.Helpers;
using CalmClass.Application.Features.Polls.Localization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CalmClass.Application.Features.Polls.Services;

public class PollMonitorService(
    IPollRepository pollRepository,
    ITelegramBotClient telegramBotClient,
    IDateTimeProvider dateTimeProvider,
    IOptions<CalmClassOptions> options,
    ILogger<PollMonitorService> logger)
{
    public async Task<int> ProcessRemindersAsync(CancellationToken cancellationToken = default)
    {
        var pollOptions = options.Value.Poll;
        var quietHoursOptions = options.Value.QuietHours;
        var activePolls = await pollRepository.GetActivePollsAcrossAllChatsAsync(cancellationToken);
        var remindedCount = 0;
        var now = dateTimeProvider.UtcNow;

        foreach (var poll in activePolls)
        {
            if (poll.Status != PollStatus.Open || poll.RemindedAtUtc != null)
            {
                continue;
            }

            var timeUntilExpiry = poll.ExpiresAtUtc - now;
            var isWithinWindow = timeUntilExpiry.TotalHours <= pollOptions.ReminderHoursBeforeExpiry && timeUntilExpiry > TimeSpan.Zero;

            if (!isWithinWindow)
            {
                continue;
            }

            var members = await pollRepository.GetActiveMembersAsync(poll.ChatId, cancellationToken);
            var votes = await pollRepository.GetVotesForPollAsync(poll.ChatId, poll.PollId, cancellationToken);

            var activeVoterIds = votes.Where(v => !v.IsRevoked).Select(v => v.UserId).ToHashSet();
            var unresponsiveMembers = members.Where(m => !activeVoterIds.Contains(m.UserId)).ToList();

            if (unresponsiveMembers.Count == 0)
            {
                logger.LogInformation("All members voted in poll {PollId}, skipping reminder", poll.PollId);
                continue;
            }

            var isQuiet = dateTimeProvider.IsQuietHours(
                quietHoursOptions.StartHour,
                quietHoursOptions.EndHour,
                quietHoursOptions.TimeZoneId);

            var formattedMentions = unresponsiveMembers.Select(m =>
                MarkdownV2Helper.FormatMention(m.UserId, m.DisplayName, m.Username)).ToList();

            var messageText = UkrainianPollMessages.FormatReminder(
                MarkdownV2Helper.Escape(poll.Question),
                formattedMentions);

            await telegramBotClient.SendMessageAsync(
                poll.ChatId,
                messageText,
                parseMode: "MarkdownV2",
                disableNotification: isQuiet,
                cancellationToken: cancellationToken);

            var updatedPoll = poll with
            {
                RemindedAtUtc = now,
                Status = PollStatus.Reminded
            };

            await pollRepository.UpdatePollAsync(updatedPoll, cancellationToken);
            logger.LogInformation("Sent reminder for poll {PollId} in chat {ChatId} to {Count} members (Silent: {IsQuiet})",
                poll.PollId, poll.ChatId, unresponsiveMembers.Count, isQuiet);

            remindedCount++;
        }

        return remindedCount;
    }

    public async Task<int> ProcessClosuresAsync(CancellationToken cancellationToken = default)
    {
        var activePolls = await pollRepository.GetActivePollsAcrossAllChatsAsync(cancellationToken);
        var closedCount = 0;
        var now = dateTimeProvider.UtcNow;

        foreach (var poll in activePolls)
        {
            if (!poll.IsActive || poll.ExpiresAtUtc > now)
            {
                continue;
            }

            await ClosePollInternalAsync(poll, cancellationToken);
            closedCount++;
        }

        return closedCount;
    }

    public async Task ClosePollInternalAsync(TrackedPoll poll, CancellationToken cancellationToken = default)
    {
        var now = dateTimeProvider.UtcNow;

        // 1. Stop poll in Telegram
        await telegramBotClient.StopPollAsync(poll.ChatId, poll.MessageId, cancellationToken);

        // 2. Fetch votes and eligible members
        var members = await pollRepository.GetActiveMembersAsync(poll.ChatId, cancellationToken);
        var votes = await pollRepository.GetVotesForPollAsync(poll.ChatId, poll.PollId, cancellationToken);

        var activeVotes = votes.Where(v => !v.IsRevoked).ToList();
        var totalVoters = activeVotes.Count;
        var totalEligible = members.Count;

        // 3. Tally votes per option
        var optionTallies = new int[poll.Options.Count];
        foreach (var vote in activeVotes)
        {
            foreach (var idx in vote.SelectedOptionIndices)
            {
                if (idx >= 0 && idx < poll.Options.Count)
                {
                    optionTallies[idx]++;
                }
            }
        }

        var resultsList = new List<(string OptionName, int VoteCount, double Percentage)>();
        var maxVotes = 0;

        for (var i = 0; i < poll.Options.Count; i++)
        {
            var count = optionTallies[i];
            var pct = totalVoters > 0 ? Math.Round((double)count / totalVoters * 100.0, 1) : 0.0;
            resultsList.Add((MarkdownV2Helper.Escape(poll.Options[i]), count, pct));
            if (count > maxVotes)
            {
                maxVotes = count;
            }
        }

        var winners = new List<string>();
        if (maxVotes > 0)
        {
            for (var i = 0; i < poll.Options.Count; i++)
            {
                if (optionTallies[i] == maxVotes)
                {
                    winners.Add(MarkdownV2Helper.Escape(poll.Options[i]));
                }
            }
        }

        // 4. Send aggregated summary
        var summaryText = UkrainianPollMessages.FormatSummaryReport(
            MarkdownV2Helper.Escape(poll.Question),
            totalEligible,
            totalVoters,
            resultsList,
            winners);

        await telegramBotClient.SendMessageAsync(
            poll.ChatId,
            summaryText,
            parseMode: "MarkdownV2",
            disableNotification: false,
            cancellationToken: cancellationToken);

        // 5. Update poll status
        var closedPoll = poll with
        {
            ClosedAtUtc = now,
            Status = PollStatus.Closed
        };

        await pollRepository.UpdatePollAsync(closedPoll, cancellationToken);
        logger.LogInformation("Closed poll {PollId} in chat {ChatId}. Turnout: {Voters}/{Eligible}",
            poll.PollId, poll.ChatId, totalVoters, totalEligible);
    }
}
