namespace CalmClass.Application.Features.Polls.Commands.IngestVote;

using CalmClass.Application.Common.Interfaces;
using CalmClass.Application.Domain.Entities;
using CalmClass.Application.Domain.Enums;
using Microsoft.Extensions.Logging;


public class IngestVoteCommandHandler(
    IPollRepository pollRepository,
    IDateTimeProvider dateTimeProvider,
    ILogger<IngestVoteCommandHandler> logger)
{
    public async Task<IngestVoteResult> HandleAsync(IngestVoteCommand command, CancellationToken cancellationToken = default)
    {
        var poll = await pollRepository.FindPollByIdAsync(command.PollId, cancellationToken);
        if (poll == null)
        {
            logger.LogWarning("IngestVote dropped: Poll {PollId} not found", command.PollId);
            return new IngestVoteResult { Success = false, Reason = "Poll not found" };
        }

        if (!poll.IsActive)
        {
            logger.LogInformation("IngestVote dropped: Poll {PollId} is already {Status}", command.PollId, poll.Status);
            return new IngestVoteResult { Success = false, Reason = $"Poll is {poll.Status}" };
        }

        var isRevoked = command.SelectedOptionIndices == null || command.SelectedOptionIndices.Count == 0;
        IReadOnlyList<int> validIndices = isRevoked
            ? Array.Empty<int>()
            : command.SelectedOptionIndices!.Where(i => i >= 0 && i < poll.Options.Count).Distinct().ToList();

        var vote = new PollVote
        {
            ChatId = poll.ChatId,
            PollId = poll.PollId,
            UserId = command.UserId,
            DisplayName = command.DisplayName,
            Username = command.Username,
            SelectedOptionIndices = validIndices,
            VotedAtUtc = dateTimeProvider.UtcNow,
            IsRevoked = isRevoked
        };

        // 1. Upsert vote record idempotently
        await pollRepository.UpsertVoteAsync(vote, cancellationToken);

        // 2. Ensure member is registered in classroom roster
        var member = await pollRepository.GetMemberAsync(poll.ChatId, command.UserId, cancellationToken);
        if (member == null)
        {
            var newMember = new GroupMember
            {
                ChatId = poll.ChatId,
                UserId = command.UserId,
                DisplayName = command.DisplayName,
                Username = command.Username,
                Role = MemberRole.Member,
                IsActive = true,
                JoinedAtUtc = dateTimeProvider.UtcNow
            };
            await pollRepository.UpsertMemberAsync(newMember, cancellationToken);
        }

        logger.LogInformation("Successfully ingested vote for user {UserId} on poll {PollId}. Revoked: {IsRevoked}", command.UserId, command.PollId, isRevoked);

        return new IngestVoteResult { Success = true, Vote = vote };
    }
}
