using System.Text.Json.Serialization;
using CalmClass.Application.Domain.Entities;
using CalmClass.Application.Domain.Enums;

namespace CalmClass.Infrastructure.Persistence.Documents;

public record TrackedPollDocument
{
    public const string DocumentType = "TrackedPoll";

    [JsonPropertyName("id")]
    public required string Id { get; init; }

    [JsonPropertyName("chatId")]
    public required string ChatId { get; init; }

    [JsonPropertyName("type")]
    public string Type { get; init; } = DocumentType;

    [JsonPropertyName("pollId")]
    public required string PollId { get; init; }

    [JsonPropertyName("messageId")]
    public required int MessageId { get; init; }

    [JsonPropertyName("question")]
    public required string Question { get; init; }

    [JsonPropertyName("options")]
    public required IReadOnlyList<string> Options { get; init; }

    [JsonPropertyName("allowsMultipleAnswers")]
    public bool AllowsMultipleAnswers { get; init; }

    [JsonPropertyName("createdAtUtc")]
    public required DateTime CreatedAtUtc { get; init; }

    [JsonPropertyName("expiresAtUtc")]
    public required DateTime ExpiresAtUtc { get; init; }

    [JsonPropertyName("remindedAtUtc")]
    public DateTime? RemindedAtUtc { get; init; }

    [JsonPropertyName("closedAtUtc")]
    public DateTime? ClosedAtUtc { get; init; }

    [JsonPropertyName("status")]
    public required string Status { get; init; }

    [JsonPropertyName("_etag")]
    public string? ETag { get; init; }

    public static TrackedPollDocument FromEntity(TrackedPoll entity) => new()
    {
        Id = entity.Id,
        ChatId = entity.ChatId,
        Type = DocumentType,
        PollId = entity.PollId,
        MessageId = entity.MessageId,
        Question = entity.Question,
        Options = entity.Options,
        AllowsMultipleAnswers = entity.AllowsMultipleAnswers,
        CreatedAtUtc = entity.CreatedAtUtc,
        ExpiresAtUtc = entity.ExpiresAtUtc,
        RemindedAtUtc = entity.RemindedAtUtc,
        ClosedAtUtc = entity.ClosedAtUtc,
        Status = entity.Status.ToString(),
        ETag = entity.ETag
    };

    public TrackedPoll ToEntity() => new()
    {
        ChatId = ChatId,
        PollId = PollId,
        MessageId = MessageId,
        Question = Question,
        Options = Options,
        AllowsMultipleAnswers = AllowsMultipleAnswers,
        CreatedAtUtc = CreatedAtUtc,
        ExpiresAtUtc = ExpiresAtUtc,
        RemindedAtUtc = RemindedAtUtc,
        ClosedAtUtc = ClosedAtUtc,
        Status = Enum.TryParse<PollStatus>(Status, out var s) ? s : PollStatus.Open,
        ETag = ETag
    };
}
