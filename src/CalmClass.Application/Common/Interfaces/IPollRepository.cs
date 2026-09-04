using CalmClass.Application.Domain.Entities;

namespace CalmClass.Application.Common.Interfaces;

public interface IPollRepository
{
    Task<TrackedPoll?> GetActivePollAsync(string chatId, CancellationToken cancellationToken = default);
    Task<TrackedPoll?> GetPollByIdAsync(string chatId, string pollId, CancellationToken cancellationToken = default);
    Task<TrackedPoll?> FindPollByIdAsync(string pollId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TrackedPoll>> GetActivePollsAcrossAllChatsAsync(CancellationToken cancellationToken = default);
    Task CreatePollAsync(TrackedPoll poll, CancellationToken cancellationToken = default);
    Task UpdatePollAsync(TrackedPoll poll, CancellationToken cancellationToken = default);

    Task UpsertVoteAsync(PollVote vote, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PollVote>> GetVotesForPollAsync(string chatId, string pollId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<GroupMember>> GetActiveMembersAsync(string chatId, CancellationToken cancellationToken = default);
    Task<GroupMember?> GetMemberAsync(string chatId, long userId, CancellationToken cancellationToken = default);
    Task UpsertMemberAsync(GroupMember member, CancellationToken cancellationToken = default);
}
