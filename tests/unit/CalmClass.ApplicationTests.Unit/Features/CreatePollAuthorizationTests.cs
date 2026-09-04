namespace CalmClass.ApplicationTests.Unit.Features;

using CalmClass.Application.Common.Interfaces;
using CalmClass.Application.Common.Options;
using CalmClass.Application.Domain.Entities;
using CalmClass.Application.Domain.Enums;
using CalmClass.Application.Features.Polls.Commands.CreatePoll;
using CalmClass.Application.Features.Polls.Localization;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

public class CreatePollAuthorizationTests
{
    private readonly Mock<IPollRepository> _pollRepoMock = new();
    private readonly Mock<ITelegramBotClient> _botClientMock = new();
    private readonly Mock<IDateTimeProvider> _timeProviderMock = new();
    private readonly IOptions<CalmClassOptions> _options = Options.Create(new CalmClassOptions());
    private readonly DateTime _fixedNow = new(2026, 9, 4, 12, 0, 0, DateTimeKind.Utc);

    public CreatePollAuthorizationTests()
    {
        _timeProviderMock.Setup(t => t.UtcNow).Returns(_fixedNow);
    }

    [Test]
    public async Task HandleAsync_WhenUserIsRegularMember_RejectsUnauthorized()
    {
        _pollRepoMock.Setup(r => r.GetMemberAsync("-1001", 200, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GroupMember
            {
                ChatId = "-1001",
                UserId = 200,
                DisplayName = "Parent",
                Role = MemberRole.Member, // Not Admin
                IsActive = true,
                JoinedAtUtc = _fixedNow
            });

        var handler = new CreatePollCommandHandler(
            _pollRepoMock.Object, _botClientMock.Object, _timeProviderMock.Object, _options, NullLogger<CreatePollCommandHandler>.Instance);

        var command = new CreatePollCommand
        {
            ChatId = "-1001",
            UserId = 200,
            Question = "Питання",
            Options = new[] { "A", "B" }
        };

        var result = await handler.HandleAsync(command);

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.ErrorMessage).IsEqualTo(UkrainianPollMessages.UnauthorizedAdminOnly);
        _botClientMock.Verify(b => b.SendMessageAsync("-1001", UkrainianPollMessages.UnauthorizedAdminOnly, "MarkdownV2", false, It.IsAny<CancellationToken>()), Times.Once);
        _pollRepoMock.Verify(r => r.CreatePollAsync(It.IsAny<TrackedPoll>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task HandleAsync_WhenAdminIsInactive_RejectsUnauthorized()
    {
        _pollRepoMock.Setup(r => r.GetMemberAsync("-1001", 300, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GroupMember
            {
                ChatId = "-1001",
                UserId = 300,
                DisplayName = "Inactive Admin",
                Role = MemberRole.Admin,
                IsActive = false, // Inactive
                JoinedAtUtc = _fixedNow
            });

        var handler = new CreatePollCommandHandler(
            _pollRepoMock.Object, _botClientMock.Object, _timeProviderMock.Object, _options, NullLogger<CreatePollCommandHandler>.Instance);

        var command = new CreatePollCommand
        {
            ChatId = "-1001",
            UserId = 300,
            Question = "Питання",
            Options = new[] { "A", "B" }
        };

        var result = await handler.HandleAsync(command);

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.ErrorMessage).IsEqualTo(UkrainianPollMessages.UnauthorizedAdminOnly);
    }

    [Test]
    public async Task HandleAsync_WhenActivePollAlreadyExists_RejectsWithConflictMessage()
    {
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

        _pollRepoMock.Setup(r => r.GetActivePollAsync("-1001", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TrackedPoll
            {
                ChatId = "-1001",
                PollId = "existing_poll_999",
                MessageId = 10,
                Question = "Активне опитування",
                Options = new[] { "Так", "Ні" },
                CreatedAtUtc = _fixedNow.AddHours(-1),
                ExpiresAtUtc = _fixedNow.AddHours(23),
                Status = PollStatus.Open
            });

        var handler = new CreatePollCommandHandler(
            _pollRepoMock.Object, _botClientMock.Object, _timeProviderMock.Object, _options, NullLogger<CreatePollCommandHandler>.Instance);

        var command = new CreatePollCommand
        {
            ChatId = "-1001",
            UserId = 100,
            Question = "Нове опитування",
            Options = new[] { "1", "2" }
        };

        var result = await handler.HandleAsync(command);

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.ErrorMessage).IsEqualTo(UkrainianPollMessages.ActivePollAlreadyExists);
        _botClientMock.Verify(b => b.SendMessageAsync("-1001", UkrainianPollMessages.ActivePollAlreadyExists, "MarkdownV2", false, It.IsAny<CancellationToken>()), Times.Once);
        _botClientMock.Verify(b => b.SendPollAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IReadOnlyList<string>>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
