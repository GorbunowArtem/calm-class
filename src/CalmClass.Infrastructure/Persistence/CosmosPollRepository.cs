using System.Net;
using CalmClass.Application.Common.Interfaces;
using CalmClass.Application.Common.Options;
using CalmClass.Application.Domain.Entities;
using CalmClass.Infrastructure.Persistence.Documents;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CalmClass.Infrastructure.Persistence;

public class CosmosPollRepository(
    Container container,
    ILogger<CosmosPollRepository>? logger = null) : IPollRepository
{
    public CosmosPollRepository(
        CosmosClient cosmosClient,
        IOptions<CalmClassOptions> options,
        ILogger<CosmosPollRepository> logger)
        : this(cosmosClient.GetContainer(options.Value.CosmosDb.DatabaseName, options.Value.CosmosDb.ContainerName), logger)
    {
    }

    public async Task<TrackedPoll?> GetActivePollAsync(string chatId, CancellationToken cancellationToken = default)
    {
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
    }

    public async Task<TrackedPoll?> GetPollByIdAsync(string chatId, string pollId, CancellationToken cancellationToken = default)
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
    }

    public async Task<TrackedPoll?> FindPollByIdAsync(string pollId, CancellationToken cancellationToken = default)
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
    }

    public async Task<IReadOnlyList<TrackedPoll>> GetActivePollsAcrossAllChatsAsync(CancellationToken cancellationToken = default)
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

        return results;
    }

    public async Task CreatePollAsync(TrackedPoll poll, CancellationToken cancellationToken = default)
    {
        var doc = TrackedPollDocument.FromEntity(poll);
        await container.CreateItemAsync(doc, new PartitionKey(poll.ChatId), cancellationToken: cancellationToken);
    }

    public async Task UpdatePollAsync(TrackedPoll poll, CancellationToken cancellationToken = default)
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
    }

    public async Task UpsertVoteAsync(PollVote vote, CancellationToken cancellationToken = default)
    {
        var doc = PollVoteDocument.FromEntity(vote);
        await container.UpsertItemAsync(doc, new PartitionKey(vote.ChatId), cancellationToken: cancellationToken);
    }

    public async Task<IReadOnlyList<PollVote>> GetVotesForPollAsync(string chatId, string pollId, CancellationToken cancellationToken = default)
    {
        var queryDefinition = new QueryDefinition(
            "SELECT * FROM c WHERE c.chatId = @chatId AND c.type = @type AND c.pollId = @pollId AND c.isRevoked = false")
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

        return results;
    }

    public async Task<IReadOnlyList<GroupMember>> GetActiveMembersAsync(string chatId, CancellationToken cancellationToken = default)
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

        return results;
    }

    public async Task<GroupMember?> GetMemberAsync(string chatId, long userId, CancellationToken cancellationToken = default)
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
    }

    public async Task UpsertMemberAsync(GroupMember member, CancellationToken cancellationToken = default)
    {
        var doc = GroupMemberDocument.FromEntity(member);
        await container.UpsertItemAsync(doc, new PartitionKey(member.ChatId), cancellationToken: cancellationToken);
    }
}
