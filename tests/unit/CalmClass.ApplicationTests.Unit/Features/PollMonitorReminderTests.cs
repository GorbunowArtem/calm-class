namespace CalmClass.ApplicationTests.Unit.Features;

using CalmClass.Application.Common.Interfaces;
using CalmClass.Application.Common.Options;
using CalmClass.Application.Domain.Entities;
using CalmClass.Application.Domain.Enums;
using CalmClass.Application.Features.Polls.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

public class PollMonitorReminderTests
{
    private readonly Mock<IPollRepository> _pollRepoMock = new();
    private readonly Mock<ITelegramBotClient> _botClientMock = new();
    private readonly Mock<IDateTimeProvider> _timeProviderMock = new();
    private readonly IOptions<CalmClassOptions> _options = Options.Create(new CalmClassOptions());
    private readonly DateTime _fixedNow = new(2026, 9, 4, 18, 0, 0, DateTimeKind.Utc); // 18:00 UTC = 21:00 Kyiv (quiet hour)

    private readonly TrackedPoll _samplePoll = new()
    {
        ChatId = "-1001",
        PollId = "poll_remind_1",
        MessageId = 50,
        Question = "Збори о 19:00",
        Options = new[] { "Буду", "Не буду" },
        CreatedAtUtc = new DateTime(2026, 9, 3, 22, 0, 0, DateTimeKind.Utc),
        ExpiresAtUtc = new DateTime(2026, 9, 4, 22, 0, 0, DateTimeKind.Utc), // Expiry in 4 hours
        Status = PollStatus.Open
    };

    public PollMonitorReminderTests()
    {
        _timeProviderMock.Setup(t => t.UtcNow).Returns(_fixedNow);
    }

    [Test]
    public async Task ProcessRemindersAsync_WhenRemainingTimeMoreThanSixHours_DoesNotSendReminder()
    {
        // Poll expires in 10 hours
        var pollFarFromExpiry = _samplePoll with
        {
            ExpiresAtUtc = _fixedNow.AddHours(10)
        };

        _pollRepoMock.Setup(r => r.GetActivePollsAcrossAllChatsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { pollFarFromExpiry });

        var service = new PollMonitorService(
            _pollRepoMock.Object, _botClientMock.Object, _timeProviderMock.Object, _options, NullLogger<PollMonitorService>.Instance);

        var reminded = await service.ProcessRemindersAsync();

        await Assert.That(reminded).IsEqualTo(0);
        _botClientMock.Verify(b => b.SendMessageAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task ProcessRemindersAsync_WhenWithinWindowAndQuietHours_SendsSilentNotification()
    {
        _pollRepoMock.Setup(r => r.GetActivePollsAcrossAllChatsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { _samplePoll });

        _pollRepoMock.Setup(r => r.GetActiveMembersAsync("-1001", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new GroupMember { ChatId = "-1001", UserId = 1, DisplayName = "User 1", Role = MemberRole.Member, IsActive = true, JoinedAtUtc = _fixedNow },
                new GroupMember { ChatId = "-1001", UserId = 2, DisplayName = "User 2", Role = MemberRole.Member, IsActive = true, JoinedAtUtc = _fixedNow }
            });

        // Only User 1 voted
        _pollRepoMock.Setup(r => r.GetVotesForPollAsync("-1001", "poll_remind_1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new PollVote { ChatId = "-1001", PollId = "poll_remind_1", UserId = 1, DisplayName = "User 1", SelectedOptionIndices = new[] { 0 }, VotedAtUtc = _fixedNow, IsRevoked = false }
            });

        // Set quiet hours = true (e.g. 21:00 Kyiv)
        _timeProviderMock.Setup(t => t.IsQuietHours(20, 8, "Europe/Kyiv")).Returns(true);

        var service = new PollMonitorService(
            _pollRepoMock.Object, _botClientMock.Object, _timeProviderMock.Object, _options, NullLogger<PollMonitorService>.Instance);

        var reminded = await service.ProcessRemindersAsync();

        await Assert.That(reminded).IsEqualTo(1);

        // Verification: disableNotification must be TRUE during quiet hours
        _botClientMock.Verify(b => b.SendMessageAsync(
            "-1001",
            It.Is<string>(s => s.Contains("Нагадування про голосування") && s.Contains("User 2")),
            "MarkdownV2",
            true, // Silent notification
            It.IsAny<CancellationToken>()), Times.Once);

        // Status must be updated to Reminded
        _pollRepoMock.Verify(r => r.UpdatePollAsync(
            It.Is<TrackedPoll>(p => p.Status == PollStatus.Reminded && p.RemindedAtUtc != null),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
