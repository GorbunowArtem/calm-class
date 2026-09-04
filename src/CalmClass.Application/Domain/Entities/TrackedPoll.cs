using CalmClass.Application.Domain.Enums;

namespace CalmClass.Application.Domain.Entities;

public record TrackedPoll
{
    public string Id => $"poll_{PollId}";
    public required string ChatId { get; init; }
    public required string PollId { get; init; }
    public required int MessageId { get; init; }
    public required string Question { get; init; }
    public required IReadOnlyList<string> Options { get; init; }
    public bool AllowsMultipleAnswers { get; init; }
    public required DateTime CreatedAtUtc { get; init; }
    public required DateTime ExpiresAtUtc { get; init; }
    public DateTime? RemindedAtUtc { get; init; }
    public DateTime? ClosedAtUtc { get; init; }
    public PollStatus Status { get; init; } = PollStatus.Open;
    public string? ETag { get; init; }

    public bool IsActive => Status is PollStatus.Open or PollStatus.Reminded;

    public bool CanBeReminded(DateTime utcNow, int reminderHoursBeforeExpiry) =>
        Status == PollStatus.Open &&
        RemindedAtUtc == null &&
        utcNow >= ExpiresAtUtc.AddHours(-reminderHoursBeforeExpiry) &&
        utcNow < ExpiresAtUtc;

    public bool IsExpired(DateTime utcNow) =>
        IsActive && utcNow >= ExpiresAtUtc;
}
