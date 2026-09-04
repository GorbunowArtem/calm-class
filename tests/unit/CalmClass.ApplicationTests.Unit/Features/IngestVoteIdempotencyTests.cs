using CalmClass.Application.Common.Interfaces;
using CalmClass.Application.Domain.Entities;
using CalmClass.Application.Domain.Enums;
using CalmClass.Application.Features.Polls.Commands.IngestVote;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace CalmClass.ApplicationTests.Unit.Features;

public class IngestVoteIdempotencyTests
{
    private readonly Mock<IPollRepository> _pollRepoMock = new();
    private readonly Mock<IDateTimeProvider> _timeProviderMock = new();
    private readonly DateTime _fixedNow = new(2026, 9, 4, 14, 0, 0, DateTimeKind.Utc);

    private readonly TrackedPoll _activePoll = new()
    {
        ChatId = "-1001",
        PollId = "poll_777",
        MessageId = 202,
        Question = "Опитування",
        Options = new[] { "A", "B" },
        CreatedAtUtc = new DateTime(2026, 9, 4, 10, 0, 0, DateTimeKind.Utc),
        ExpiresAtUtc = new DateTime(2026, 9, 5, 10, 0, 0, DateTimeKind.Utc),
        Status = PollStatus.Open
    };

    public IngestVoteIdempotencyTests()
    {
        _timeProviderMock.Setup(t => t.UtcNow).Returns(_fixedNow);
        _pollRepoMock.Setup(r => r.FindPollByIdAsync("poll_777", It.IsAny<CancellationToken>()))
            .ReturnsAsync(_activePoll);
    }

    [Test]
    public async Task HandleAsync_UnknownVoter_RegistersMemberInRoster()
    {
        _pollRepoMock.Setup(r => r.GetMemberAsync("-1001", 9999, It.IsAny<CancellationToken>()))
            .ReturnsAsync((GroupMember?)null);

        var handler = new IngestVoteCommandHandler(
            _pollRepoMock.Object, _timeProviderMock.Object, NullLogger<IngestVoteCommandHandler>.Instance);

        var command = new IngestVoteCommand
        {
            PollId = "poll_777",
            UserId = 9999,
            DisplayName = "Новий Учасник",
            Username = "novyi",
            SelectedOptionIndices = new[] { 0 }
        };

        var result = await handler.HandleAsync(command);

        await Assert.That(result.Success).IsTrue();
        _pollRepoMock.Verify(r => r.UpsertMemberAsync(
            It.Is<GroupMember>(m => m.UserId == 9999 && m.Role == MemberRole.Member && m.IsActive),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task HandleAsync_UnknownPoll_ReturnsFailureWithoutCrashing()
    {
        _pollRepoMock.Setup(r => r.FindPollByIdAsync("unknown_poll", It.IsAny<CancellationToken>()))
            .ReturnsAsync((TrackedPoll?)null);

        var handler = new IngestVoteCommandHandler(
            _pollRepoMock.Object, _timeProviderMock.Object, NullLogger<IngestVoteCommandHandler>.Instance);

        var command = new IngestVoteCommand
        {
            PollId = "unknown_poll",
            UserId = 123,
            DisplayName = "User",
            SelectedOptionIndices = new[] { 0 }
        };

        var result = await handler.HandleAsync(command);

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Reason).IsEqualTo("Poll not found");
        _pollRepoMock.Verify(r => r.UpsertVoteAsync(It.IsAny<PollVote>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
