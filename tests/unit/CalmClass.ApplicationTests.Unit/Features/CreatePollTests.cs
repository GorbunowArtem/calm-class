using CalmClass.Application.Common.Interfaces;
using CalmClass.Application.Common.Options;
using CalmClass.Application.Domain.Entities;
using CalmClass.Application.Domain.Enums;
using CalmClass.Application.Features.Polls.Commands.CreatePoll;
using CalmClass.Application.Features.Polls.Localization;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace CalmClass.ApplicationTests.Unit.Features;

public class CreatePollTests
{
    private readonly Mock<IPollRepository> _pollRepoMock = new();
    private readonly Mock<ITelegramBotClient> _botClientMock = new();
    private readonly Mock<IDateTimeProvider> _timeProviderMock = new();
    private readonly IOptions<CalmClassOptions> _options = Options.Create(new CalmClassOptions());
    private readonly DateTime _fixedNow = new(2026, 9, 4, 12, 0, 0, DateTimeKind.Utc);

    public CreatePollTests()
    {
        _timeProviderMock.Setup(t => t.UtcNow).Returns(_fixedNow);

        // Default: active admin user
        _pollRepoMock.Setup(r => r.GetMemberAsync("-1001", 100, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GroupMember
            {
                ChatId = "-1001",
                UserId = 100,
                DisplayName = "Admin User",
                Role = MemberRole.Admin,
                IsActive = true,
                JoinedAtUtc = _fixedNow.AddDays(-10)
            });

        // Default: no active poll
        _pollRepoMock.Setup(r => r.GetActivePollAsync("-1001", It.IsAny<CancellationToken>()))
            .ReturnsAsync((TrackedPoll?)null);

        // Default: bot send poll succeeds
        _botClientMock.Setup(b => b.SendPollAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IReadOnlyList<string>>(),
                It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TelegramPollResult { PollId = "poll_test_123", MessageId = 55 });
    }

    [Test]
    public async Task HandleAsync_ValidCommand_PublishesNonAnonymousPollAndSaves()
    {
        var handler = new CreatePollCommandHandler(
            _pollRepoMock.Object, _botClientMock.Object, _timeProviderMock.Object, _options, NullLogger<CreatePollCommandHandler>.Instance);

        var command = new CreatePollCommand
        {
            ChatId = "-1001",
            UserId = 100,
            Question = "Куди підемо на екскурсію?",
            Options = new[] { "Зоопарк", "Музей", "Театр" },
            DurationHours = 48
        };

        var result = await handler.HandleAsync(command);

        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.Poll).IsNotNull();
        await Assert.That(result.Poll!.PollId).IsEqualTo("poll_test_123");
        await Assert.That(result.Poll.MessageId).IsEqualTo(55);
        await Assert.That(result.Poll.Status).IsEqualTo(PollStatus.Open);
        await Assert.That(result.Poll.ExpiresAtUtc).IsEqualTo(_fixedNow.AddHours(48));

        // Telegram called with isAnonymous: false
        _botClientMock.Verify(b => b.SendPollAsync("-1001", "Куди підемо на екскурсію?", command.Options, false, false, It.IsAny<CancellationToken>()), Times.Once);
        _pollRepoMock.Verify(r => r.CreatePollAsync(It.Is<TrackedPoll>(p => p.PollId == "poll_test_123"), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task HandleAsync_RawArgumentsParsing_CorrectlyExtractsQuestionAndOptionsAndDuration()
    {
        var handler = new CreatePollCommandHandler(
            _pollRepoMock.Object, _botClientMock.Object, _timeProviderMock.Object, _options, NullLogger<CreatePollCommandHandler>.Instance);

        var command = new CreatePollCommand
        {
            ChatId = "-1001",
            UserId = 100,
            RawArgs = "\"Екскурсія восени\" \"Зоопарк\" \"Планетарій\" 72"
        };

        var result = await handler.HandleAsync(command);

        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.Poll!.Question).IsEqualTo("Екскурсія восени");
        await Assert.That(result.Poll.Options.Count).IsEqualTo(2);
        await Assert.That(result.Poll.ExpiresAtUtc).IsEqualTo(_fixedNow.AddHours(72));
    }

    [Test]
    public async Task HandleAsync_LessThanTwoOptions_RejectsWithInvalidOptionsMessage()
    {
        var handler = new CreatePollCommandHandler(
            _pollRepoMock.Object, _botClientMock.Object, _timeProviderMock.Object, _options, NullLogger<CreatePollCommandHandler>.Instance);

        var command = new CreatePollCommand
        {
            ChatId = "-1001",
            UserId = 100,
            Question = "Одне питання",
            Options = new[] { "Один варіант" }
        };

        var result = await handler.HandleAsync(command);

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.ErrorMessage).IsEqualTo(UkrainianPollMessages.InvalidOptionsCount);
        _botClientMock.Verify(b => b.SendMessageAsync("-1001", UkrainianPollMessages.InvalidOptionsCount, "MarkdownV2", false, It.IsAny<CancellationToken>()), Times.Once);
        _botClientMock.Verify(b => b.SendPollAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IReadOnlyList<string>>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task HandleAsync_DurationOutOfRange_RejectsWithInvalidDurationMessage()
    {
        var handler = new CreatePollCommandHandler(
            _pollRepoMock.Object, _botClientMock.Object, _timeProviderMock.Object, _options, NullLogger<CreatePollCommandHandler>.Instance);

        var command = new CreatePollCommand
        {
            ChatId = "-1001",
            UserId = 100,
            Question = "Питання",
            Options = new[] { "Варіант 1", "Варіант 2" },
            DurationHours = 200 // Max is 168
        };

        var result = await handler.HandleAsync(command);

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.ErrorMessage).IsEqualTo(UkrainianPollMessages.InvalidDuration);
        _botClientMock.Verify(b => b.SendMessageAsync("-1001", UkrainianPollMessages.InvalidDuration, "MarkdownV2", false, It.IsAny<CancellationToken>()), Times.Once);
    }
}
