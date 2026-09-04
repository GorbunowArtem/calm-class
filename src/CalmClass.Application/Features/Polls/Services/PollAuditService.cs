namespace CalmClass.Application.Features.Polls.Services;

using CalmClass.Application.Common.Interfaces;
using CalmClass.Application.Domain.Enums;
using Microsoft.Extensions.Logging;

public record VoterAuditRecord
{
    public required long UserId { get; init; }
    public required string DisplayName { get; init; }
    public string? Username { get; init; }
    public required IReadOnlyList<string> SelectedOptions { get; init; }
    public required DateTime VotedAtUtc { get; init; }
}

public record PollAuditReport
{
    public required string PollId { get; init; }
    public required string Question { get; init; }
    public required PollStatus Status { get; init; }
    public required IReadOnlyList<VoterAuditRecord> Voters { get; init; }
}

public class PollAuditService(
    IPollRepository pollRepository,
    ILogger<PollAuditService> logger)
{
    public async Task<PollAuditReport?> GetAuditReportAsync(
        string chatId,
        string pollId,
        long requestingUserId,
        CancellationToken cancellationToken = default)
    {
        // 1. Verify that requesting user is an active admin
        var member = await pollRepository.GetMemberAsync(chatId, requestingUserId, cancellationToken);
        if (member == null || !member.IsActive || member.Role != MemberRole.Admin)
        {
            logger.LogWarning("Unauthorized audit access attempt by user {UserId} in chat {ChatId}", requestingUserId, chatId);
            throw new UnauthorizedAccessException("Only active committee admins can access the audit report.");
        }

        // 2. Fetch poll
        var poll = await pollRepository.GetPollByIdAsync(chatId, pollId, cancellationToken);
        if (poll == null)
        {
            logger.LogInformation("Poll {PollId} not found in chat {ChatId} for audit report", pollId, chatId);
            return null;
        }

        // 3. Fetch active votes
        var votes = await pollRepository.GetVotesForPollAsync(chatId, pollId, cancellationToken);
        var activeVotes = votes.Where(v => !v.IsRevoked).ToList();

        var voterRecords = activeVotes.Select(v =>
        {
            var selectedNames = v.SelectedOptionIndices
                .Where(idx => idx >= 0 && idx < poll.Options.Count)
                .Select(idx => poll.Options[idx])
                .ToList();

            return new VoterAuditRecord
            {
                UserId = v.UserId,
                DisplayName = v.DisplayName,
                Username = v.Username,
                SelectedOptions = selectedNames,
                VotedAtUtc = v.VotedAtUtc
            };
        }).ToList();

        return new PollAuditReport
        {
            PollId = poll.PollId,
            Question = poll.Question,
            Status = poll.Status,
            Voters = voterRecords
        };
    }
}
