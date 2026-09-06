namespace CalmClass.ApplicationTests.Unit.Features;

using CalmClass.Application.Common.Interfaces;
using CalmClass.Application.Common.Options;
using CalmClass.Application.Domain.Entities;
using CalmClass.Application.Domain.Enums;
using CalmClass.Application.Features.Polls.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

public class PollReminderExecutionTests
{
    private readonly Mock<IPollRepository> _pollRepoMock = new();
    private readonly Mock<ITelegramBotClient> _botClientMock = new();
    private readonly Mock<IDateTimeProvider> _timeProviderMock = new();
    private readonly IOptions<CalmClassOptions> _options = Options.Create(new CalmClassOptions());
    private readonly DateTime _fixedNow = new(2026, 9, 4, 12, 0, 0, DateTimeKind.Utc); // 12:00 UTC = 15:00 Kyiv (daytime)

    public PollReminderExecutionTests()
    {
        _timeProviderMock.Setup(t => t.UtcNow).Returns(_fixedNow);
    }

    [Test]
    public async Task ProcessRemindersAsync_WhenPollAlreadyReminded_DoesNotSendSecondReminder()
    {
        var remindedPoll = new TrackedPoll
        {
            ChatId = "-1001",
            PollId = "poll_already_reminded",
            MessageId = 51,
            Question = "Питання",
            Options = new[] { "A", "B" },
            CreatedAtUtc = _fixedNow.AddHours(-20),
            ExpiresAtUtc = _fixedNow.AddHours(2),
            RemindedAtUtc = _fixedNow.AddHours(-2),
            Status = PollStatus.Reminded // Already reminded!
        };

        _pollRepoMock.Setup(r => r.GetActivePollsAcrossAllChatsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { remindedPoll });

        var service = new PollMonitorService(
            _pollRepoMock.Object, _botClientMock.Object, _timeProviderMock.Object, _options, NullLogger<PollMonitorService>.Instance);

        var count = await service.ProcessRemindersAsync();

        await Assert.That(count).IsEqualTo(0);
        _botClientMock.Verify(b => b.SendMessageAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task ProcessRemindersAsync_WhenAllMembersVoted_DoesNotSendReminder()
    {
        var openPoll = new TrackedPoll
        {
            ChatId = "-1001",
            PollId = "poll_all_voted",
            MessageId = 52,
            Question = "Питання",
            Options = new[] { "A", "B" },
            CreatedAtUtc = _fixedNow.AddHours(-20),
            ExpiresAtUtc = _fixedNow.AddHours(2), // within 6 hours
            Status = PollStatus.Open
        };

        _pollRepoMock.Setup(r => r.GetActivePollsAcrossAllChatsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { openPoll });

        _pollRepoMock.Setup(r => r.GetActiveMembersAsync("-1001", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new GroupMember { ChatId = "-1001", UserId = 10, DisplayName = "Member 1", Role = MemberRole.Member, IsActive = true, JoinedAtUtc = _fixedNow }
            });

        _pollRepoMock.Setup(r => r.GetVotesForPollAsync("-1001", "poll_all_voted", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new PollVote { ChatId = "-1001", PollId = "poll_all_voted", UserId = 10, DisplayName = "Member 1", SelectedOptionIndices = new[] { 0 }, VotedAtUtc = _fixedNow, IsRevoked = false }
            });

        var service = new PollMonitorService(
            _pollRepoMock.Object, _botClientMock.Object, _timeProviderMock.Object, _options, NullLogger<PollMonitorService>.Instance);

        var count = await service.ProcessRemindersAsync();

        await Assert.That(count).IsEqualTo(0);
        _botClientMock.Verify(b => b.SendMessageAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task ProcessRemindersAsync_DuringDaytime_SendsSoundNotification()
    {
        var openPoll = new TrackedPoll
        {
            ChatId = "-1001",
            PollId = "poll_daytime",
            MessageId = 53,
            Question = "Питання",
            Options = new[] { "A", "B" },
            CreatedAtUtc = _fixedNow.AddHours(-20),
            ExpiresAtUtc = _fixedNow.AddHours(4),
            Status = PollStatus.Open
        };

        _pollRepoMock.Setup(r => r.GetActivePollsAcrossAllChatsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { openPoll });

        _pollRepoMock.Setup(r => r.GetActiveMembersAsync("-1001", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new GroupMember { ChatId = "-1001", UserId = 20, DisplayName = "Taras", Role = MemberRole.Member, IsActive = true, JoinedAtUtc = _fixedNow }
            });

        _pollRepoMock.Setup(r => r.GetVotesForPollAsync("-1001", "poll_daytime", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<PollVote>());

        // Daytime: quiet hours = false
        _timeProviderMock.Setup(t => t.IsQuietHours(20, 8, "Europe/Kyiv")).Returns(false);

        var service = new PollMonitorService(
            _pollRepoMock.Object, _botClientMock.Object, _timeProviderMock.Object, _options, NullLogger<PollMonitorService>.Instance);

        var count = await service.ProcessRemindersAsync();

        await Assert.That(count).IsEqualTo(1);
        // disableNotification MUST be false
        _botClientMock.Verify(b => b.SendMessageAsync(
            "-1001",
            It.IsAny<string>(),
            "MarkdownV2",
            false, // Sound on
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
