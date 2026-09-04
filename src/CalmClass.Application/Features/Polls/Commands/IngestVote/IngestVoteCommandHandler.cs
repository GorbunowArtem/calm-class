using CalmClass.Application.Common.Interfaces;
using CalmClass.Application.Domain.Entities;
using CalmClass.Application.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace CalmClass.Application.Features.Polls.Commands.IngestVote;

public record IngestVoteCommand
{
    public required string PollId { get; init; }
    public required long UserId { get; init; }
    public required string DisplayName { get; init; }
    public string? Username { get; init; }
    public required IReadOnlyList<int> SelectedOptionIndices { get; init; }
}

public record IngestVoteResult
{
    public bool Success { get; init; }
    public string? Reason { get; init; }
    public PollVote? Vote { get; init; }
}

public class IngestVoteCommandHandler
{
    private readonly IPollRepository _pollRepository;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly ILogger<IngestVoteCommandHandler> _logger;

    public IngestVoteCommandHandler(
        IPollRepository pollRepository,
        IDateTimeProvider dateTimeProvider,
        ILogger<IngestVoteCommandHandler> logger)
    {
        _pollRepository = pollRepository;
        _dateTimeProvider = dateTimeProvider;
        _logger = logger;
    }

    public async Task<IngestVoteResult> HandleAsync(IngestVoteCommand command, CancellationToken cancellationToken = default)
    {
        var poll = await _pollRepository.FindPollByIdAsync(command.PollId, cancellationToken);
        if (poll == null)
        {
            _logger.LogWarning("IngestVote dropped: Poll {PollId} not found", command.PollId);
            return new IngestVoteResult { Success = false, Reason = "Poll not found" };
        }

        if (!poll.IsActive)
        {
            _logger.LogInformation("IngestVote dropped: Poll {PollId} is already {Status}", command.PollId, poll.Status);
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
            VotedAtUtc = _dateTimeProvider.UtcNow,
            IsRevoked = isRevoked
        };

        // 1. Upsert vote record idempotently
        await _pollRepository.UpsertVoteAsync(vote, cancellationToken);

        // 2. Ensure member is registered in classroom roster
        var member = await _pollRepository.GetMemberAsync(poll.ChatId, command.UserId, cancellationToken);
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
                JoinedAtUtc = _dateTimeProvider.UtcNow
            };
            await _pollRepository.UpsertMemberAsync(newMember, cancellationToken);
        }

        _logger.LogInformation("Successfully ingested vote for user {UserId} on poll {PollId}. Revoked: {IsRevoked}", command.UserId, command.PollId, isRevoked);

        return new IngestVoteResult { Success = true, Vote = vote };
    }
}
