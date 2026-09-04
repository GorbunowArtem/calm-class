using CalmClass.Application.Domain.Entities;
using CalmClass.Application.Domain.Enums;
using CalmClass.Infrastructure.Persistence.Documents;

namespace CalmClass.InfrastructureTests.Unit.Persistence;

public class CosmosMappingTests
{
    [Test]
    public async Task TrackedPoll_DocumentMapping_RoundtripSucceeds()
    {
        var now = DateTime.UtcNow;
        var entity = new TrackedPoll
        {
            ChatId = "-100123",
            PollId = "555",
            MessageId = 44,
            Question = "Питання?",
            Options = new[] { "Опція 1", "Опція 2" },
            AllowsMultipleAnswers = false,
            CreatedAtUtc = now,
            ExpiresAtUtc = now.AddHours(24),
            RemindedAtUtc = now.AddHours(18),
            ClosedAtUtc = null,
            Status = PollStatus.Reminded,
            ETag = "\"etag-123\""
        };

        var doc = TrackedPollDocument.FromEntity(entity);

        await Assert.That(doc.Id).IsEqualTo("poll_555");
        await Assert.That(doc.ChatId).IsEqualTo("-100123");
        await Assert.That(doc.Type).IsEqualTo("TrackedPoll");
        await Assert.That(doc.Status).IsEqualTo("Reminded");

        var back = doc.ToEntity();

        await Assert.That(back.PollId).IsEqualTo(entity.PollId);
        await Assert.That(back.ChatId).IsEqualTo(entity.ChatId);
        await Assert.That(back.Question).IsEqualTo(entity.Question);
        await Assert.That(back.Options.Count).IsEqualTo(2);
        await Assert.That(back.Status).IsEqualTo(PollStatus.Reminded);
        await Assert.That(back.ETag).IsEqualTo(entity.ETag);
    }

    [Test]
    public async Task PollVote_DocumentMapping_RoundtripSucceeds()
    {
        var now = DateTime.UtcNow;
        var entity = new PollVote
        {
            ChatId = "-100123",
            PollId = "555",
            UserId = 99,
            DisplayName = "Тарас",
            Username = "taras_sh",
            SelectedOptionIndices = new[] { 0, 1 },
            VotedAtUtc = now,
            IsRevoked = false
        };

        var doc = PollVoteDocument.FromEntity(entity);

        await Assert.That(doc.Id).IsEqualTo("vote_555_99");
        await Assert.That(doc.Type).IsEqualTo("PollVote");

        var back = doc.ToEntity();

        await Assert.That(back.UserId).IsEqualTo(99);
        await Assert.That(back.DisplayName).IsEqualTo("Тарас");
        await Assert.That(back.SelectedOptionIndices).Contains(0);
        await Assert.That(back.SelectedOptionIndices).Contains(1);
        await Assert.That(back.IsRevoked).IsFalse();
    }

    [Test]
    public async Task GroupMember_DocumentMapping_RoundtripSucceeds()
    {
        var now = DateTime.UtcNow;
        var entity = new GroupMember
        {
            ChatId = "-100123",
            UserId = 77,
            DisplayName = "Олена",
            Username = "olena",
            Role = MemberRole.Admin,
            IsActive = true,
            JoinedAtUtc = now
        };

        var doc = GroupMemberDocument.FromEntity(entity);

        await Assert.That(doc.Id).IsEqualTo("member_-100123_77");
        await Assert.That(doc.Type).IsEqualTo("GroupMember");
        await Assert.That(doc.Role).IsEqualTo("Admin");

        var back = doc.ToEntity();

        await Assert.That(back.UserId).IsEqualTo(77);
        await Assert.That(back.Role).IsEqualTo(MemberRole.Admin);
        await Assert.That(back.IsActive).IsTrue();
    }
}
