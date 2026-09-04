namespace CalmClass.Infrastructure.Persistence;

using System.Collections.Concurrent;
using CalmClass.Application.Common.Interfaces;
using CalmClass.Application.Domain.Entities;
using CalmClass.Application.Domain.Enums;
using Microsoft.Extensions.Logging;

public class InMemoryPollRepository(ILogger<InMemoryPollRepository>? logger = null) : IPollRepository
{
    private static readonly ConcurrentDictionary<string, TrackedPoll> Polls = new();
    private static readonly ConcurrentDictionary<string, PollVote> Votes = new();
    private static readonly ConcurrentDictionary<string, GroupMember> Members = new();

    public Task<TrackedPoll?> GetActivePollAsync(string chatId, CancellationToken cancellationToken = default)
    {
        var active = Polls.Values
            .FirstOrDefault(p => p.ChatId == chatId && (p.Status == PollStatus.Open || p.Status == PollStatus.Reminded));
        return Task.FromResult(active);
    }

    public Task<TrackedPoll?> GetPollByIdAsync(string chatId, string pollId, CancellationToken cancellationToken = default)
    {
        Polls.TryGetValue($"poll_{pollId}", out var poll);
        return Task.FromResult(poll != null && poll.ChatId == chatId ? poll : null);
    }

    public Task<TrackedPoll?> FindPollByIdAsync(string pollId, CancellationToken cancellationToken = default)
    {
        Polls.TryGetValue($"poll_{pollId}", out var poll);
        return Task.FromResult(poll);
    }

    public Task<IReadOnlyList<TrackedPoll>> GetActivePollsAcrossAllChatsAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<TrackedPoll> list = Polls.Values
            .Where(p => p.Status == PollStatus.Open || p.Status == PollStatus.Reminded)
            .ToList();
        return Task.FromResult(list);
    }

    public Task CreatePollAsync(TrackedPoll poll, CancellationToken cancellationToken = default)
    {
        Polls[$"poll_{poll.PollId}"] = poll;
        logger?.LogInformation("InMemory: Created poll {PollId} in chat {ChatId}", poll.PollId, poll.ChatId);
        return Task.CompletedTask;
    }

    public Task UpdatePollAsync(TrackedPoll poll, CancellationToken cancellationToken = default)
    {
        Polls[$"poll_{poll.PollId}"] = poll;
        logger?.LogInformation("InMemory: Updated poll {PollId} status to {Status}", poll.PollId, poll.Status);
        return Task.CompletedTask;
    }

    public Task UpsertVoteAsync(PollVote vote, CancellationToken cancellationToken = default)
    {
        Votes[$"vote_{vote.ChatId}_{vote.PollId}_{vote.UserId}"] = vote;
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<PollVote>> GetVotesForPollAsync(string chatId, string pollId, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<PollVote> list = Votes.Values
            .Where(v => v.ChatId == chatId && v.PollId == pollId)
            .ToList();
        return Task.FromResult(list);
    }

    public Task<IReadOnlyList<GroupMember>> GetActiveMembersAsync(string chatId, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<GroupMember> list = Members.Values
            .Where(m => m.ChatId == chatId && m.IsActive)
            .ToList();
        return Task.FromResult(list);
    }

    public Task<GroupMember?> GetMemberAsync(string chatId, long userId, CancellationToken cancellationToken = default)
    {
        var key = $"member_{chatId}_{userId}";
        if (Members.TryGetValue(key, out var member))
        {
            return Task.FromResult<GroupMember?>(member);
        }

        // Auto-seed first user in chat as Admin for frictionless local development
        var hasAdmin = Members.Values.Any(m => m.ChatId == chatId && m.Role == MemberRole.Admin && m.IsActive);
        if (!hasAdmin)
        {
            var admin = new GroupMember
            {
                ChatId = chatId,
                UserId = userId,
                DisplayName = "Admin",
                Role = MemberRole.Admin,
                IsActive = true,
                JoinedAtUtc = DateTime.UtcNow
            };
            Members[key] = admin;
            logger?.LogInformation("InMemory: Auto-registered user {UserId} as Admin for chat {ChatId}", userId, chatId);
            return Task.FromResult<GroupMember?>(admin);
        }

        return Task.FromResult<GroupMember?>(null);
    }

    public Task UpsertMemberAsync(GroupMember member, CancellationToken cancellationToken = default)
    {
        var key = $"member_{member.ChatId}_{member.UserId}";
        Members[key] = member;
        return Task.CompletedTask;
    }
}
