using CalmClass.Application.Common.Interfaces;
using CalmClass.Application.Domain.Entities;
using CalmClass.Application.Domain.Enums;
using CalmClass.Application.Features.Polls.Commands.IngestVote;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace CalmClass.ApplicationTests.Unit.Features;

public class IngestVoteTests
{
    private readonly Mock<IPollRepository> _pollRepoMock = new();
    private readonly Mock<IDateTimeProvider> _timeProviderMock = new();
    private readonly DateTime _fixedNow = new(2026, 9, 4, 14, 0, 0, DateTimeKind.Utc);

    private readonly TrackedPoll _activePoll = new()
    {
        ChatId = "-1001",
        PollId = "poll_456",
        MessageId = 101,
        Question = "Тестове питання",
        Options = new[] { "Опція 1", "Опція 2", "Опція 3" },
        CreatedAtUtc = new DateTime(2026, 9, 4, 10, 0, 0, DateTimeKind.Utc),
        ExpiresAtUtc = new DateTime(2026, 9, 5, 10, 0, 0, DateTimeKind.Utc),
        Status = PollStatus.Open
    };

    public IngestVoteTests()
    {
        _timeProviderMock.Setup(t => t.UtcNow).Returns(_fixedNow);
        _pollRepoMock.Setup(r => r.FindPollByIdAsync("poll_456", It.IsAny<CancellationToken>()))
            .ReturnsAsync(_activePoll);
    }

    [Test]
    public async Task HandleAsync_CastVote_SavesVoteWithSelectedIndices()
    {
        var handler = new IngestVoteCommandHandler(
            _pollRepoMock.Object, _timeProviderMock.Object, NullLogger<IngestVoteCommandHandler>.Instance);

        var command = new IngestVoteCommand
        {
            PollId = "poll_456",
            UserId = 12345,
            DisplayName = "Оксана Петренко",
            Username = "oksana_p",
            SelectedOptionIndices = new[] { 1 }
        };

        var result = await handler.HandleAsync(command);

        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.Vote).IsNotNull();
        await Assert.That(result.Vote!.UserId).IsEqualTo(12345);
        await Assert.That(result.Vote.SelectedOptionIndices).Contains(1);
        await Assert.That(result.Vote.IsRevoked).IsFalse();

        _pollRepoMock.Verify(r => r.UpsertVoteAsync(
            It.Is<PollVote>(v => v.UserId == 12345 && !v.IsRevoked && v.SelectedOptionIndices.Contains(1)),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task HandleAsync_RevokeVote_SetsIsRevokedToTrue()
    {
        var handler = new IngestVoteCommandHandler(
            _pollRepoMock.Object, _timeProviderMock.Object, NullLogger<IngestVoteCommandHandler>.Instance);

        var command = new IngestVoteCommand
        {
            PollId = "poll_456",
            UserId = 12345,
            DisplayName = "Оксана Петренко",
            Username = "oksana_p",
            SelectedOptionIndices = Array.Empty<int>() // Retraction: empty selection
        };

        var result = await handler.HandleAsync(command);

        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.Vote!.IsRevoked).IsTrue();
        await Assert.That(result.Vote.SelectedOptionIndices.Count).IsEqualTo(0);

        _pollRepoMock.Verify(r => r.UpsertVoteAsync(
            It.Is<PollVote>(v => v.UserId == 12345 && v.IsRevoked),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task HandleAsync_WhenPollIsClosed_DropsVote()
    {
        var closedPoll = _activePoll with { Status = PollStatus.Closed };
        _pollRepoMock.Setup(r => r.FindPollByIdAsync("poll_456", It.IsAny<CancellationToken>()))
            .ReturnsAsync(closedPoll);

        var handler = new IngestVoteCommandHandler(
            _pollRepoMock.Object, _timeProviderMock.Object, NullLogger<IngestVoteCommandHandler>.Instance);

        var command = new IngestVoteCommand
        {
            PollId = "poll_456",
            UserId = 12345,
            DisplayName = "Оксана",
            SelectedOptionIndices = new[] { 0 }
        };

        var result = await handler.HandleAsync(command);

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Reason).Contains("Closed");
        _pollRepoMock.Verify(r => r.UpsertVoteAsync(It.IsAny<PollVote>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
