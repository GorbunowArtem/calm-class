using System.Text.Json.Serialization;
using CalmClass.Application.Domain.Entities;

namespace CalmClass.Infrastructure.Persistence.Documents;

public record PollVoteDocument
{
    public const string DocumentType = "PollVote";

    [JsonPropertyName("id")]
    public required string Id { get; init; }

    [JsonPropertyName("chatId")]
    public required string ChatId { get; init; }

    [JsonPropertyName("type")]
    public string Type { get; init; } = DocumentType;

    [JsonPropertyName("pollId")]
    public required string PollId { get; init; }

    [JsonPropertyName("userId")]
    public required long UserId { get; init; }

    [JsonPropertyName("displayName")]
    public required string DisplayName { get; init; }

    [JsonPropertyName("username")]
    public string? Username { get; init; }

    [JsonPropertyName("selectedOptionIndices")]
    public required IReadOnlyList<int> SelectedOptionIndices { get; init; }

    [JsonPropertyName("votedAtUtc")]
    public required DateTime VotedAtUtc { get; init; }

    [JsonPropertyName("isRevoked")]
    public bool IsRevoked { get; init; }

    public static PollVoteDocument FromEntity(PollVote entity) => new()
    {
        Id = entity.Id,
        ChatId = entity.ChatId,
        Type = DocumentType,
        PollId = entity.PollId,
        UserId = entity.UserId,
        DisplayName = entity.DisplayName,
        Username = entity.Username,
        SelectedOptionIndices = entity.SelectedOptionIndices,
        VotedAtUtc = entity.VotedAtUtc,
        IsRevoked = entity.IsRevoked
    };

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
