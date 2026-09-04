using CalmClass.Application.Common.Interfaces;
using CalmClass.Application.Common.Options;
using CalmClass.Application.Domain.Entities;
using CalmClass.Application.Domain.Enums;
using CalmClass.Application.Features.Polls.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace CalmClass.ApplicationTests.Unit.Features;

public class PollClosureTallyTests
{
    private readonly Mock<IPollRepository> _pollRepoMock = new();
    private readonly Mock<ITelegramBotClient> _botClientMock = new();
    private readonly Mock<IDateTimeProvider> _timeProviderMock = new();
    private readonly IOptions<CalmClassOptions> _options = Options.Create(new CalmClassOptions());
    private readonly DateTime _fixedNow = new(2026, 9, 5, 12, 0, 0, DateTimeKind.Utc);

    public PollClosureTallyTests()
    {
        _timeProviderMock.Setup(t => t.UtcNow).Returns(_fixedNow);
    }

    [Test]
    public async Task ClosePollInternalAsync_WithSingleWinner_PostsCorrectTallyAndWinner()
    {
        var poll = new TrackedPoll
        {
            ChatId = "-1001",
            PollId = "poll_winner_1",
            MessageId = 40,
            Question = "Яку книгу обрати?",
            Options = new[] { "Кобзар", "Тіні забутих предків", "Енеїда" },
            CreatedAtUtc = _fixedNow.AddDays(-1),
            ExpiresAtUtc = _fixedNow,
            Status = PollStatus.Open
        };

        _pollRepoMock.Setup(r => r.GetActiveMembersAsync("-1001", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new GroupMember { ChatId = "-1001", UserId = 1, DisplayName = "M1", Role = MemberRole.Member, IsActive = true, JoinedAtUtc = _fixedNow },
                new GroupMember { ChatId = "-1001", UserId = 2, DisplayName = "M2", Role = MemberRole.Member, IsActive = true, JoinedAtUtc = _fixedNow },
                new GroupMember { ChatId = "-1001", UserId = 3, DisplayName = "M3", Role = MemberRole.Member, IsActive = true, JoinedAtUtc = _fixedNow },
                new GroupMember { ChatId = "-1001", UserId = 4, DisplayName = "M4", Role = MemberRole.Member, IsActive = true, JoinedAtUtc = _fixedNow }
            });

        // 3 voters: 2 for Кобзар (idx 0), 1 for Енеїда (idx 2)
        _pollRepoMock.Setup(r => r.GetVotesForPollAsync("-1001", "poll_winner_1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new PollVote { ChatId = "-1001", PollId = "poll_winner_1", UserId = 1, DisplayName = "M1", SelectedOptionIndices = new[] { 0 }, VotedAtUtc = _fixedNow, IsRevoked = false },
                new PollVote { ChatId = "-1001", PollId = "poll_winner_1", UserId = 2, DisplayName = "M2", SelectedOptionIndices = new[] { 0 }, VotedAtUtc = _fixedNow, IsRevoked = false },
                new PollVote { ChatId = "-1001", PollId = "poll_winner_1", UserId = 3, DisplayName = "M3", SelectedOptionIndices = new[] { 2 }, VotedAtUtc = _fixedNow, IsRevoked = false }
            });

        var service = new PollMonitorService(
            _pollRepoMock.Object, _botClientMock.Object, _timeProviderMock.Object, _options, NullLogger<PollMonitorService>.Instance);

        await service.ClosePollInternalAsync(poll);

        // Telegram stopPoll called
        _botClientMock.Verify(b => b.StopPollAsync("-1001", 40, It.IsAny<CancellationToken>()), Times.Once);

        // Summary message posted
        _botClientMock.Verify(b => b.SendMessageAsync(
            "-1001",
            It.Is<string>(msg =>
                msg.Contains("Підсумки голосування") &&
                msg.Contains("Всього учасників: 4") &&
                msg.Contains("Проголосувало: 3") &&
                msg.Contains("Кобзар: 2") &&
                msg.Contains("Переможець: *Кобзар*")),
            "MarkdownV2",
            false,
            It.IsAny<CancellationToken>()), Times.Once);

        // Poll marked as Closed
        _pollRepoMock.Verify(r => r.UpdatePollAsync(
            It.Is<TrackedPoll>(p => p.Status == PollStatus.Closed && p.ClosedAtUtc == _fixedNow),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task ClosePollInternalAsync_WithTie_DisplaysMultipleWinners()
    {
        var poll = new TrackedPoll
        {
            ChatId = "-1001",
            PollId = "poll_tie_1",
            MessageId = 41,
            Question = "Куди йдемо?",
            Options = new[] { "Парк", "Кіно" },
            CreatedAtUtc = _fixedNow.AddDays(-1),
            ExpiresAtUtc = _fixedNow,
            Status = PollStatus.Open
        };

        _pollRepoMock.Setup(r => r.GetActiveMembersAsync("-1001", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new GroupMember { ChatId = "-1001", UserId = 1, DisplayName = "M1", Role = MemberRole.Member, IsActive = true, JoinedAtUtc = _fixedNow },
                new GroupMember { ChatId = "-1001", UserId = 2, DisplayName = "M2", Role = MemberRole.Member, IsActive = true, JoinedAtUtc = _fixedNow }
            });

        _pollRepoMock.Setup(r => r.GetVotesForPollAsync("-1001", "poll_tie_1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new PollVote { ChatId = "-1001", PollId = "poll_tie_1", UserId = 1, DisplayName = "M1", SelectedOptionIndices = new[] { 0 }, VotedAtUtc = _fixedNow, IsRevoked = false },
                new PollVote { ChatId = "-1001", PollId = "poll_tie_1", UserId = 2, DisplayName = "M2", SelectedOptionIndices = new[] { 1 }, VotedAtUtc = _fixedNow, IsRevoked = false }
            });

        var service = new PollMonitorService(
            _pollRepoMock.Object, _botClientMock.Object, _timeProviderMock.Object, _options, NullLogger<PollMonitorService>.Instance);

        await service.ClosePollInternalAsync(poll);

        _botClientMock.Verify(b => b.SendMessageAsync(
            "-1001",
            It.Is<string>(msg => msg.Contains("Однаковий результат: *Парк, Кіно*")),
            "MarkdownV2",
            false,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task ClosePollInternalAsync_ZeroVoters_DisplaysNoResultsMessage()
    {
        var poll = new TrackedPoll
        {
            ChatId = "-1001",
            PollId = "poll_zero_1",
            MessageId = 42,
            Question = "Питання",
            Options = new[] { "A", "B" },
            CreatedAtUtc = _fixedNow.AddDays(-1),
            ExpiresAtUtc = _fixedNow,
            Status = PollStatus.Open
        };

        _pollRepoMock.Setup(r => r.GetActiveMembersAsync("-1001", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new GroupMember { ChatId = "-1001", UserId = 1, DisplayName = "M1", Role = MemberRole.Member, IsActive = true, JoinedAtUtc = _fixedNow }
            });

        _pollRepoMock.Setup(r => r.GetVotesForPollAsync("-1001", "poll_zero_1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<PollVote>());

        var service = new PollMonitorService(
            _pollRepoMock.Object, _botClientMock.Object, _timeProviderMock.Object, _options, NullLogger<PollMonitorService>.Instance);

        await service.ClosePollInternalAsync(poll);

        _botClientMock.Verify(b => b.SendMessageAsync(
            "-1001",
            It.Is<string>(msg => msg.Contains("Результатів немає")),
            "MarkdownV2",
            false,
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
