namespace Zanuraki.Mcp.LinkWatcher.Test.Services.Sources;

using Shouldly;
using Zanuraki.Mcp.LinkWatcher.Services.Sources;

public abstract class TheChangeDetectorClass
{
    protected static readonly SourceComment First = new("c1", "alice", DateTimeOffset.Parse("2025-01-01T10:00:00Z"), "First");
    protected static readonly SourceComment Second = new("c2", "bob", DateTimeOffset.Parse("2025-01-02T10:00:00Z"), "Second");
    protected static readonly SourceComment Third = new("c3", "carol", DateTimeOffset.Parse("2025-01-03T10:00:00Z"), "Third");

    protected static Dictionary<string, string> Fingerprint(
        string status = "OPEN",
        int commentCount = 3,
        string lastCommentId = "c3",
        string updatedAt = "2025-01-03T10:00:00Z") => new()
    {
        ["status"] = status,
        ["commentCount"] = commentCount.ToString(),
        ["lastCommentId"] = lastCommentId,
        ["updatedAt"] = updatedAt,
    };

    protected static LinkSnapshot Snapshot(
        string status = "OPEN",
        IReadOnlyList<SourceComment>? comments = null,
        Dictionary<string, string>? fingerprint = null) => new(
        Title: "Title",
        Status: status,
        Fingerprint: fingerprint ?? Fingerprint(status: status, commentCount: comments?.Count ?? 3, lastCommentId: comments?.LastOrDefault()?.Id ?? "c3"),
        Comments: comments ?? [First, Second, Third]);

    public class TheCompareMethod : TheChangeDetectorClass
    {
        [Test]
        public void ShouldReturnNullWhenThereIsNoPreviousFingerprint()
        {
            ChangeDetector.Compare(null, Snapshot()).ShouldBeNull();
        }

        [Test]
        public void ShouldReturnNullWhenNothingChanged()
        {
            ChangeDetector.Compare(Fingerprint(), Snapshot()).ShouldBeNull();
        }

        [Test]
        public void ShouldReturnNullWhenOnlyInformationalKeyChanged()
        {
            var previous = Fingerprint(updatedAt: "2024-12-01T00:00:00Z");

            ChangeDetector.Compare(previous, Snapshot()).ShouldBeNull();
        }

        [Test]
        public void ShouldReportStatusChangeWithoutNewComments()
        {
            var delta = ChangeDetector.Compare(Fingerprint(status: "OPEN"), Snapshot(status: "CLOSED (completed)"));

            delta.ShouldNotBeNull();
            delta.StatusChanged.ShouldBeTrue();
            delta.PreviousStatus.ShouldBe("OPEN");
            delta.CurrentStatus.ShouldBe("CLOSED (completed)");
            delta.NewComments.ShouldBeEmpty();
        }

        [Test]
        public void ShouldReportCommentsAfterLastKnownId()
        {
            var previous = Fingerprint(commentCount: 1, lastCommentId: "c1");

            var delta = ChangeDetector.Compare(previous, Snapshot());

            delta.ShouldNotBeNull();
            delta.StatusChanged.ShouldBeFalse();
            delta.PreviousStatus.ShouldBe("OPEN");
            delta.NewComments.ShouldBe([Second, Third]);
        }

        [Test]
        public void ShouldReportAllCommentsWhenLastKnownIdIsNotFound()
        {
            var previous = Fingerprint(commentCount: 1, lastCommentId: "gone");

            var delta = ChangeDetector.Compare(previous, Snapshot());

            delta.ShouldNotBeNull();
            delta.NewComments.ShouldBe([First, Second, Third]);
        }

        [Test]
        public void ShouldReportAllCommentsWhenPreviousHadNoComments()
        {
            var previous = Fingerprint(commentCount: 0, lastCommentId: string.Empty);

            var delta = ChangeDetector.Compare(previous, Snapshot());

            delta.ShouldNotBeNull();
            delta.NewComments.ShouldBe([First, Second, Third]);
        }

        [Test]
        public void ShouldTruncateBodiesLongerThanLimitAndAppendEllipsis()
        {
            var longComment = new SourceComment("c4", "dave", DateTimeOffset.Parse("2025-01-04T10:00:00Z"), "abcdefghij");
            var current = Snapshot(comments: [First, longComment]);

            var delta = ChangeDetector.Compare(Fingerprint(commentCount: 1, lastCommentId: "c1"), current, maxCommentLength: 5);

            delta.ShouldNotBeNull();
            delta.NewComments.Single().Body.ShouldBe("abcde…");
        }

        [Test]
        public void ShouldKeepBodiesExactlyAtLimitUnchanged()
        {
            var comment = new SourceComment("c4", "dave", DateTimeOffset.Parse("2025-01-04T10:00:00Z"), "abcde");
            var current = Snapshot(comments: [First, comment]);

            var delta = ChangeDetector.Compare(Fingerprint(commentCount: 1, lastCommentId: "c1"), current, maxCommentLength: 5);

            delta.ShouldNotBeNull();
            delta.NewComments.Single().Body.ShouldBe("abcde");
        }

        [Test]
        public void ShouldTruncateAtThousandCharactersByDefault()
        {
            var body = new string('x', 1500);
            var comment = new SourceComment("c4", "dave", DateTimeOffset.Parse("2025-01-04T10:00:00Z"), body);
            var current = Snapshot(comments: [First, comment]);

            var delta = ChangeDetector.Compare(Fingerprint(commentCount: 1, lastCommentId: "c1"), current);

            delta.ShouldNotBeNull();
            delta.NewComments.Single().Body.ShouldBe(new string('x', 1000) + "…");
        }
    }
}
