namespace CalmClass.Infrastructure.Persistence.Documents;

using System.Text.Json.Serialization;
using CalmClass.Application.Domain.Entities;
using CalmClass.Application.Domain.Enums;

public record TrackedPollDocument(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("chatId")] string ChatId,
    [property: JsonPropertyName("pollId")] string PollId,
    [property: JsonPropertyName("messageId")] int MessageId,
    [property: JsonPropertyName("question")] string Question,
    [property: JsonPropertyName("options")] IReadOnlyList<string> Options,
    [property: JsonPropertyName("allowsMultipleAnswers")] bool AllowsMultipleAnswers,
    [property: JsonPropertyName("createdAtUtc")] DateTime CreatedAtUtc,
    [property: JsonPropertyName("expiresAtUtc")] DateTime ExpiresAtUtc,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("remindedAtUtc")] DateTime? RemindedAtUtc = null,
    [property: JsonPropertyName("closedAtUtc")] DateTime? ClosedAtUtc = null,
    [property: JsonPropertyName("_etag")] string? ETag = null,
    [property: JsonPropertyName("type")] string Type = TrackedPollDocument.DocumentType)
{
    public const string DocumentType = "TrackedPoll";

    public static TrackedPollDocument FromEntity(TrackedPoll entity) => new(
        Id: entity.Id,
        ChatId: entity.ChatId,
        PollId: entity.PollId,
        MessageId: entity.MessageId,
        Question: entity.Question,
        Options: entity.Options,
        AllowsMultipleAnswers: entity.AllowsMultipleAnswers,
        CreatedAtUtc: entity.CreatedAtUtc,
        ExpiresAtUtc: entity.ExpiresAtUtc,
        Status: entity.Status.ToString(),
        RemindedAtUtc: entity.RemindedAtUtc,
        ClosedAtUtc: entity.ClosedAtUtc,
        ETag: entity.ETag
    );

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
