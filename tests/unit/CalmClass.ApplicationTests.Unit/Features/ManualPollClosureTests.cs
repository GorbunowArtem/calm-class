using CalmClass.Application.Common.Interfaces;
using CalmClass.Application.Common.Options;
using CalmClass.Application.Domain.Entities;
using CalmClass.Application.Domain.Enums;
using CalmClass.Application.Features.Polls.Commands.CancelPoll;
using CalmClass.Application.Features.Polls.Commands.ClosePoll;
using CalmClass.Application.Features.Polls.Localization;
using CalmClass.Application.Features.Polls.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace CalmClass.ApplicationTests.Unit.Features;

public class ManualPollClosureTests
{
    private readonly Mock<IPollRepository> _pollRepoMock = new();
    private readonly Mock<ITelegramBotClient> _botClientMock = new();
    private readonly Mock<IDateTimeProvider> _timeProviderMock = new();
    private readonly IOptions<CalmClassOptions> _options = Options.Create(new CalmClassOptions());
    private readonly DateTime _fixedNow = new(2026, 9, 5, 12, 0, 0, DateTimeKind.Utc);

    private readonly TrackedPoll _activePoll = new()
    {
        ChatId = "-1001",
        PollId = "poll_manual_1",
        MessageId = 33,
        Question = "Тестове опитування",
        Options = new[] { "Так", "Ні" },
        CreatedAtUtc = new DateTime(2026, 9, 4, 12, 0, 0, DateTimeKind.Utc),
        ExpiresAtUtc = new DateTime(2026, 9, 6, 12, 0, 0, DateTimeKind.Utc),
        Status = PollStatus.Open
    };

    public ManualPollClosureTests()
    {
        _timeProviderMock.Setup(t => t.UtcNow).Returns(_fixedNow);

        // Admin member
        _pollRepoMock.Setup(r => r.GetMemberAsync("-1001", 100, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GroupMember
            {
                ChatId = "-1001",
                UserId = 100,
                DisplayName = "Admin",
                Role = MemberRole.Admin,
                IsActive = true,
                JoinedAtUtc = _fixedNow
            });

        // Non-admin member
        _pollRepoMock.Setup(r => r.GetMemberAsync("-1001", 200, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GroupMember
            {
                ChatId = "-1001",
                UserId = 200,
                DisplayName = "Member",
                Role = MemberRole.Member,
                IsActive = true,
                JoinedAtUtc = _fixedNow
            });
    }

    [Test]
    public async Task ClosePoll_ByAdmin_ExecutesClosureSuccessfully()
    {
        _pollRepoMock.Setup(r => r.GetActivePollAsync("-1001", It.IsAny<CancellationToken>()))
            .ReturnsAsync(_activePoll);
        _pollRepoMock.Setup(r => r.GetActiveMembersAsync("-1001", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<GroupMember>());
        _pollRepoMock.Setup(r => r.GetVotesForPollAsync("-1001", "poll_manual_1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<PollVote>());

        var monitorService = new PollMonitorService(
            _pollRepoMock.Object, _botClientMock.Object, _timeProviderMock.Object, _options, NullLogger<PollMonitorService>.Instance);

        var handler = new ClosePollCommandHandler(
            _pollRepoMock.Object, _botClientMock.Object, monitorService, NullLogger<ClosePollCommandHandler>.Instance);

        var result = await handler.HandleAsync(new ClosePollCommand { ChatId = "-1001", UserId = 100 });

        await Assert.That(result.Success).IsTrue();
        _botClientMock.Verify(b => b.StopPollAsync("-1001", 33, It.IsAny<CancellationToken>()), Times.Once);
        _pollRepoMock.Verify(r => r.UpdatePollAsync(It.Is<TrackedPoll>(p => p.Status == PollStatus.Closed), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task ClosePoll_ByNonAdmin_RejectsWithUnauthorized()
    {
        var monitorService = new PollMonitorService(
            _pollRepoMock.Object, _botClientMock.Object, _timeProviderMock.Object, _options, NullLogger<PollMonitorService>.Instance);

        var handler = new ClosePollCommandHandler(
            _pollRepoMock.Object, _botClientMock.Object, monitorService, NullLogger<ClosePollCommandHandler>.Instance);

        var result = await handler.HandleAsync(new ClosePollCommand { ChatId = "-1001", UserId = 200 });

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.ErrorMessage).IsEqualTo(UkrainianPollMessages.UnauthorizedAdminOnly);
        _botClientMock.Verify(b => b.SendMessageAsync("-1001", UkrainianPollMessages.UnauthorizedAdminOnly, "MarkdownV2", false, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task CancelPoll_ByAdmin_CancelsPollAndPostsNotice()
    {
        _pollRepoMock.Setup(r => r.GetActivePollAsync("-1001", It.IsAny<CancellationToken>()))
            .ReturnsAsync(_activePoll);

        var handler = new CancelPollCommandHandler(
            _pollRepoMock.Object, _botClientMock.Object, _timeProviderMock.Object, NullLogger<CancelPollCommandHandler>.Instance);

        var result = await handler.HandleAsync(new CancelPollCommand { ChatId = "-1001", UserId = 100 });

        await Assert.That(result.Success).IsTrue();
        _botClientMock.Verify(b => b.StopPollAsync("-1001", 33, It.IsAny<CancellationToken>()), Times.Once);
        _botClientMock.Verify(b => b.SendMessageAsync("-1001", UkrainianPollMessages.PollCancelled, "MarkdownV2", false, It.IsAny<CancellationToken>()), Times.Once);
        _pollRepoMock.Verify(r => r.UpdatePollAsync(It.Is<TrackedPoll>(p => p.Status == PollStatus.Cancelled), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task CancelPoll_WhenNoActivePoll_ReturnsNoPollWarning()
    {
        _pollRepoMock.Setup(r => r.GetActivePollAsync("-1001", It.IsAny<CancellationToken>()))
            .ReturnsAsync((TrackedPoll?)null);

        var handler = new CancelPollCommandHandler(
            _pollRepoMock.Object, _botClientMock.Object, _timeProviderMock.Object, NullLogger<CancelPollCommandHandler>.Instance);

        var result = await handler.HandleAsync(new CancelPollCommand { ChatId = "-1001", UserId = 100 });

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.ErrorMessage).IsEqualTo(UkrainianPollMessages.NoActivePollFound);
        _botClientMock.Verify(b => b.SendMessageAsync("-1001", UkrainianPollMessages.NoActivePollFound, "MarkdownV2", false, It.IsAny<CancellationToken>()), Times.Once);
    }
}
