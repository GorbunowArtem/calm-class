using CalmClass.Application.Common.Interfaces;
using CalmClass.Application.Domain.Entities;
using CalmClass.Application.Domain.Enums;
using CalmClass.Application.Features.Polls.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace CalmClass.ApplicationTests.Unit.Features;

public class PollAuditServiceTests
{
    private readonly Mock<IPollRepository> _pollRepoMock = new();
    private readonly DateTime _fixedNow = new(2026, 9, 5, 12, 0, 0, DateTimeKind.Utc);

    [Test]
    public async Task GetAuditReportAsync_ByAdmin_ReturnsDetailedPerVoterChoices()
    {
        _pollRepoMock.Setup(r => r.GetMemberAsync("-1001", 100, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GroupMember
            {
                ChatId = "-1001",
                UserId = 100,
                DisplayName = "Admin User",
                Role = MemberRole.Admin,
                IsActive = true,
                JoinedAtUtc = _fixedNow
            });

        _pollRepoMock.Setup(r => r.GetPollByIdAsync("-1001", "poll_audit_1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TrackedPoll
            {
                ChatId = "-1001",
                PollId = "poll_audit_1",
                MessageId = 77,
                Question = "Питання аудиту",
                Options = new[] { "Опція А", "Опція Б" },
                CreatedAtUtc = _fixedNow.AddDays(-1),
                ExpiresAtUtc = _fixedNow,
                Status = PollStatus.Closed
            });

        _pollRepoMock.Setup(r => r.GetVotesForPollAsync("-1001", "poll_audit_1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new PollVote
                {
                    ChatId = "-1001",
                    PollId = "poll_audit_1",
                    UserId = 201,
                    DisplayName = "Іван Франко",
                    Username = "ivan_f",
                    SelectedOptionIndices = new[] { 0 },
                    VotedAtUtc = _fixedNow.AddHours(-5),
                    IsRevoked = false
                }
            });

        var service = new PollAuditService(_pollRepoMock.Object, NullLogger<PollAuditService>.Instance);

        var report = await service.GetAuditReportAsync("-1001", "poll_audit_1", 100);

        await Assert.That(report).IsNotNull();
        await Assert.That(report!.PollId).IsEqualTo("poll_audit_1");
        await Assert.That(report.Voters.Count).IsEqualTo(1);
        await Assert.That(report.Voters[0].DisplayName).IsEqualTo("Іван Франко");
        await Assert.That(report.Voters[0].SelectedOptions).Contains("Опція А");
    }

    [Test]
    public async Task GetAuditReportAsync_ByNonAdmin_ThrowsUnauthorizedException()
    {
        _pollRepoMock.Setup(r => r.GetMemberAsync("-1001", 200, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GroupMember
            {
                ChatId = "-1001",
                UserId = 200,
                DisplayName = "Regular Parent",
                Role = MemberRole.Member,
                IsActive = true,
                JoinedAtUtc = _fixedNow
            });

        var service = new PollAuditService(_pollRepoMock.Object, NullLogger<PollAuditService>.Instance);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(async () =>
        {
            await service.GetAuditReportAsync("-1001", "poll_audit_1", 200);
        });
    }
}
