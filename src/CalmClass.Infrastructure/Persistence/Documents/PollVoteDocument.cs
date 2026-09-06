namespace CalmClass.Infrastructure.Persistence.Documents;

using System.Text.Json.Serialization;
using CalmClass.Application.Domain.Entities;

public record PollVoteDocument(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("chatId")] string ChatId,
    [property: JsonPropertyName("pollId")] string PollId,
    [property: JsonPropertyName("userId")] long UserId,
    [property: JsonPropertyName("displayName")] string DisplayName,
    [property: JsonPropertyName("selectedOptionIndices")] IReadOnlyList<int> SelectedOptionIndices,
    [property: JsonPropertyName("votedAtUtc")] DateTime VotedAtUtc,
    [property: JsonPropertyName("username")] string? Username = null,
    [property: JsonPropertyName("isRevoked")] bool IsRevoked = false,
    [property: JsonPropertyName("type")] string Type = PollVoteDocument.DocumentType)
{
    public const string DocumentType = "PollVote";

    public static PollVoteDocument FromEntity(PollVote entity) => new(
        Id: entity.Id,
        ChatId: entity.ChatId,
        PollId: entity.PollId,
        UserId: entity.UserId,
        DisplayName: entity.DisplayName,
        SelectedOptionIndices: entity.SelectedOptionIndices,
        VotedAtUtc: entity.VotedAtUtc,
        Username: entity.Username,
        IsRevoked: entity.IsRevoked
    );

    public PollVote ToEntity() => new()
    {
        ChatId = ChatId,
        PollId = PollId,
        UserId = UserId,
        DisplayName = DisplayName,
        Username = Username,
        SelectedOptionIndices = SelectedOptionIndices,
        VotedAtUtc = VotedAtUtc,
        IsRevoked = IsRevoked
    };
}
