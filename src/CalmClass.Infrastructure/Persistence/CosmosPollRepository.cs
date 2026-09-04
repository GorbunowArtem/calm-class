namespace CalmClass.Infrastructure.Persistence;

using System.Net;
using System.Net.Sockets;
using CalmClass.Application.Common.Interfaces;
using CalmClass.Application.Common.Options;
using CalmClass.Application.Domain.Entities;
using CalmClass.Application.Domain.Enums;
using CalmClass.Infrastructure.Persistence.Documents;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

public class CosmosPollRepository(
    Container container,
    ILogger<CosmosPollRepository>? logger = null) : IPollRepository
{
    private readonly InMemoryPollRepository inMemoryFallback = new();

    public CosmosPollRepository(
        CosmosClient cosmosClient,
        IOptions<CalmClassOptions> options,
        ILogger<CosmosPollRepository> logger)
        : this(cosmosClient.GetContainer(options.Value.CosmosDb.DatabaseName, options.Value.CosmosDb.ContainerName), logger)
    {
    }

    private async Task<T> ExecuteWithFallbackAsync<T>(Func<Task<T>> cosmosOp, Func<Task<T>> inMemoryOp)
    {
        try
        {
            return await cosmosOp();
        }
        catch (Exception ex) when (ex is HttpRequestException or SocketException ||
                                  (ex is CosmosException ce && (ce.StatusCode == HttpStatusCode.ServiceUnavailable || ce.StatusCode == 0)))
        {
            logger?.LogWarning("Cosmos DB is unreachable ({Message}). Falling back to in-memory store for local testing.", ex.Message);
            return await inMemoryOp();
        }
    }

    private async Task ExecuteWithFallbackAsync(Func<Task> cosmosOp, Func<Task> inMemoryOp)
    {
        try
        {
            await cosmosOp();
        }
        catch (Exception ex) when (ex is HttpRequestException or SocketException ||
                                  (ex is CosmosException ce && (ce.StatusCode == HttpStatusCode.ServiceUnavailable || ce.StatusCode == 0)))
        {
            logger?.LogWarning("Cosmos DB is unreachable ({Message}). Falling back to in-memory store for local testing.", ex.Message);
            await inMemoryOp();
        }
    }

    public Task<TrackedPoll?> GetActivePollAsync(string chatId, CancellationToken cancellationToken = default) =>
        ExecuteWithFallbackAsync(async () =>
        {
            logger?.LogDebug("Querying active poll for chat {ChatId}", chatId);
            var queryDefinition = new QueryDefinition(
                "SELECT * FROM c WHERE c.chatId = @chatId AND c.type = @type AND (c.status = 'Open' OR c.status = 'Reminded')")
                .WithParameter("@chatId", chatId)
                .WithParameter("@type", TrackedPollDocument.DocumentType);

            var query = container.GetItemQueryIterator<TrackedPollDocument>(
                queryDefinition,
                requestOptions: new QueryRequestOptions { PartitionKey = new PartitionKey(chatId) });

            while (query.HasMoreResults)
            {
                var response = await query.ReadNextAsync(cancellationToken);
                var doc = response.FirstOrDefault();
                if (doc != null)
                {
                    return doc.ToEntity();
                }
            }

            return null;
        }, () => inMemoryFallback.GetActivePollAsync(chatId, cancellationToken));

    public Task<TrackedPoll?> GetPollByIdAsync(string chatId, string pollId, CancellationToken cancellationToken = default) =>
        ExecuteWithFallbackAsync(async () =>
        {
            var docId = $"poll_{pollId}";
            try
            {
                var response = await container.ReadItemAsync<TrackedPollDocument>(
                    docId,
                    new PartitionKey(chatId),
                    cancellationToken: cancellationToken);

                return response.Resource.ToEntity();
            }
            catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                return null;
            }
        }, () => inMemoryFallback.GetPollByIdAsync(chatId, pollId, cancellationToken));

    public Task<TrackedPoll?> FindPollByIdAsync(string pollId, CancellationToken cancellationToken = default) =>
        ExecuteWithFallbackAsync(async () =>
        {
            var queryDefinition = new QueryDefinition(
                "SELECT * FROM c WHERE c.type = @type AND c.pollId = @pollId")
                .WithParameter("@type", TrackedPollDocument.DocumentType)
                .WithParameter("@pollId", pollId);

            var query = container.GetItemQueryIterator<TrackedPollDocument>(queryDefinition);
            while (query.HasMoreResults)
            {
                var response = await query.ReadNextAsync(cancellationToken);
                var doc = response.FirstOrDefault();
                if (doc != null)
                {
                    return doc.ToEntity();
                }
            }

            return null;
        }, () => inMemoryFallback.FindPollByIdAsync(pollId, cancellationToken));

    public Task<IReadOnlyList<TrackedPoll>> GetActivePollsAcrossAllChatsAsync(CancellationToken cancellationToken = default) =>
        ExecuteWithFallbackAsync(async () =>
        {
            var queryDefinition = new QueryDefinition(
                "SELECT * FROM c WHERE c.type = @type AND (c.status = 'Open' OR c.status = 'Reminded')")
                .WithParameter("@type", TrackedPollDocument.DocumentType);

            var query = container.GetItemQueryIterator<TrackedPollDocument>(queryDefinition);
            var results = new List<TrackedPoll>();

            while (query.HasMoreResults)
            {
                var response = await query.ReadNextAsync(cancellationToken);
                results.AddRange(response.Select(d => d.ToEntity()));
            }

            return (IReadOnlyList<TrackedPoll>)results;
        }, () => inMemoryFallback.GetActivePollsAcrossAllChatsAsync(cancellationToken));

    public Task CreatePollAsync(TrackedPoll poll, CancellationToken cancellationToken = default) =>
        ExecuteWithFallbackAsync(async () =>
        {
            var doc = TrackedPollDocument.FromEntity(poll);
            await container.CreateItemAsync(doc, new PartitionKey(poll.ChatId), cancellationToken: cancellationToken);
        }, () => inMemoryFallback.CreatePollAsync(poll, cancellationToken));

    public Task UpdatePollAsync(TrackedPoll poll, CancellationToken cancellationToken = default) =>
        ExecuteWithFallbackAsync(async () =>
        {
            var doc = TrackedPollDocument.FromEntity(poll);
            var requestOptions = !string.IsNullOrEmpty(poll.ETag)
                ? new ItemRequestOptions { IfMatchEtag = poll.ETag }
                : null;

            await container.UpsertItemAsync(
                doc,
                new PartitionKey(poll.ChatId),
                requestOptions: requestOptions,
                cancellationToken: cancellationToken);
        }, () => inMemoryFallback.UpdatePollAsync(poll, cancellationToken));

    public Task UpsertVoteAsync(PollVote vote, CancellationToken cancellationToken = default) =>
        ExecuteWithFallbackAsync(async () =>
        {
            var doc = PollVoteDocument.FromEntity(vote);
            await container.UpsertItemAsync(doc, new PartitionKey(vote.ChatId), cancellationToken: cancellationToken);
        }, () => inMemoryFallback.UpsertVoteAsync(vote, cancellationToken));

    public Task<IReadOnlyList<PollVote>> GetVotesForPollAsync(string chatId, string pollId, CancellationToken cancellationToken = default) =>
        ExecuteWithFallbackAsync(async () =>
        {
            var queryDefinition = new QueryDefinition(
                "SELECT * FROM c WHERE c.chatId = @chatId AND c.type = @type AND c.pollId = @pollId")
                .WithParameter("@chatId", chatId)
                .WithParameter("@type", PollVoteDocument.DocumentType)
                .WithParameter("@pollId", pollId);

            var query = container.GetItemQueryIterator<PollVoteDocument>(
                queryDefinition,
                requestOptions: new QueryRequestOptions { PartitionKey = new PartitionKey(chatId) });

            var results = new List<PollVote>();
            while (query.HasMoreResults)
            {
                var response = await query.ReadNextAsync(cancellationToken);
                results.AddRange(response.Select(d => d.ToEntity()));
            }

            return (IReadOnlyList<PollVote>)results;
        }, () => inMemoryFallback.GetVotesForPollAsync(chatId, pollId, cancellationToken));

    public Task<IReadOnlyList<GroupMember>> GetActiveMembersAsync(string chatId, CancellationToken cancellationToken = default) =>
        ExecuteWithFallbackAsync(async () =>
        {
            var queryDefinition = new QueryDefinition(
                "SELECT * FROM c WHERE c.chatId = @chatId AND c.type = @type AND c.isActive = true")
                .WithParameter("@chatId", chatId)
                .WithParameter("@type", GroupMemberDocument.DocumentType);

            var query = container.GetItemQueryIterator<GroupMemberDocument>(
                queryDefinition,
                requestOptions: new QueryRequestOptions { PartitionKey = new PartitionKey(chatId) });

            var results = new List<GroupMember>();
            while (query.HasMoreResults)
            {
                var response = await query.ReadNextAsync(cancellationToken);
                results.AddRange(response.Select(d => d.ToEntity()));
            }

            return (IReadOnlyList<GroupMember>)results;
        }, () => inMemoryFallback.GetActiveMembersAsync(chatId, cancellationToken));

    public Task<GroupMember?> GetMemberAsync(string chatId, long userId, CancellationToken cancellationToken = default) =>
        ExecuteWithFallbackAsync(async () =>
        {
            var docId = $"member_{chatId}_{userId}";
            try
            {
                var response = await container.ReadItemAsync<GroupMemberDocument>(
                    docId,
                    new PartitionKey(chatId),
                    cancellationToken: cancellationToken);

                return response.Resource.ToEntity();
            }
            catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                return null;
            }
        }, () => inMemoryFallback.GetMemberAsync(chatId, userId, cancellationToken));

    public Task UpsertMemberAsync(GroupMember member, CancellationToken cancellationToken = default) =>
        ExecuteWithFallbackAsync(async () =>
        {
            var doc = GroupMemberDocument.FromEntity(member);
            await container.UpsertItemAsync(doc, new PartitionKey(member.ChatId), cancellationToken: cancellationToken);
        }, () => inMemoryFallback.UpsertMemberAsync(member, cancellationToken));
}
