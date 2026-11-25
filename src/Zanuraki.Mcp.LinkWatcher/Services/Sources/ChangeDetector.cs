namespace Zanuraki.Mcp.LinkWatcher.Services.Sources;

public static class ChangeDetector
{
    private const string StatusKey = "status";
    private const string CommentCountKey = "commentCount";
    private const string LastCommentIdKey = "lastCommentId";
    private const string Ellipsis = "…";

    /// <summary>
    /// Returns null for a first observation (<paramref name="previous"/> is null) and when none of the
    /// change-relevant fingerprint keys differ. Informational keys such as <c>updatedAt</c> never count as a change.
    /// </summary>
    public static LinkDelta? Compare(
        IReadOnlyDictionary<string, string>? previous,
        LinkSnapshot current,
        int maxCommentLength = 1000)
    {
        if (previous is null)
        {
            return null;
        }

        var fingerprint = current.Fingerprint;
        var statusChanged = previous.GetValueOrDefault(StatusKey) != fingerprint.GetValueOrDefault(StatusKey);
        var commentCountChanged = previous.GetValueOrDefault(CommentCountKey) != fingerprint.GetValueOrDefault(CommentCountKey);
        var lastCommentIdChanged = previous.GetValueOrDefault(LastCommentIdKey) != fingerprint.GetValueOrDefault(LastCommentIdKey);

        if (!statusChanged && !commentCountChanged && !lastCommentIdChanged)
        {
            return null;
        }

        var newComments = CommentsAfter(current.Comments, previous.GetValueOrDefault(LastCommentIdKey))
            .Select(comment => Truncate(comment, maxCommentLength))
            .ToList();

        return new LinkDelta(
            PreviousStatus: previous.GetValueOrDefault(StatusKey),
            CurrentStatus: current.Status,
            StatusChanged: statusChanged,
            NewComments: newComments);
    }

    private static IEnumerable<SourceComment> CommentsAfter(IReadOnlyList<SourceComment> comments, string? lastKnownId)
    {
        var index = lastKnownId is null ? -1 : FindIndex(comments, lastKnownId);
        return index < 0 ? comments : comments.Skip(index + 1);
    }

    private static int FindIndex(IReadOnlyList<SourceComment> comments, string id)
    {
        for (var i = 0; i < comments.Count; i++)
        {
            if (comments[i].Id == id)
            {
                return i;
            }
        }

        return -1;
    }

    private static SourceComment Truncate(SourceComment comment, int maxLength) =>
        comment.Body.Length <= maxLength
            ? comment
            : comment with { Body = string.Concat(comment.Body.AsSpan(0, maxLength), Ellipsis) };
}
