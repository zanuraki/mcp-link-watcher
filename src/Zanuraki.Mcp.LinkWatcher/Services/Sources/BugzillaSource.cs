namespace Zanuraki.Mcp.LinkWatcher.Services.Sources;

using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using System.Web;

public sealed class BugzillaSource(IHttpClientFactory httpClientFactory) : ILinkSource
{
    private const string ShowBugPage = "show_bug.cgi";
    private const string IncludeFields = "id,summary,status,resolution,last_change_time";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    public string Name => "bugzilla";

    public bool CanHandle(Uri url) => TryGetBugId(url, out _);

    public async Task<LinkSnapshot> FetchAsync(Uri url, CancellationToken cancellationToken)
    {
        if (!TryGetBugId(url, out var id))
        {
            throw new ArgumentException("Not a Bugzilla show_bug.cgi URL with an integer id.", nameof(url));
        }

        var basePath = url.AbsolutePath[..^ShowBugPage.Length];
        var restUrl = $"{url.Scheme}://{url.Authority}{basePath}rest/bug/{id.ToString(CultureInfo.InvariantCulture)}";
        var client = httpClientFactory.CreateClient();

        var bugs = await client.GetFromJsonAsync<BugsResponse>($"{restUrl}?include_fields={IncludeFields}", JsonOptions, cancellationToken);
        var bug = bugs!.Bugs[0];

        var commentsResponse = await client.GetFromJsonAsync<CommentsResponse>($"{restUrl}/comment", JsonOptions, cancellationToken);
        var comments = commentsResponse!.Bugs[id.ToString(CultureInfo.InvariantCulture)].Comments
            .Select(comment => new SourceComment(
                comment.Id.ToString(CultureInfo.InvariantCulture),
                comment.Creator,
                comment.CreationTime,
                comment.Text))
            .ToList();

        var status = string.IsNullOrEmpty(bug.Resolution) ? bug.Status : $"{bug.Status} {bug.Resolution}";

        return new LinkSnapshot(
            Title: bug.Summary,
            Status: status,
            Fingerprint: new Dictionary<string, string>
            {
                ["status"] = status,
                ["commentCount"] = comments.Count.ToString(CultureInfo.InvariantCulture),
                ["lastCommentId"] = comments.LastOrDefault()?.Id ?? string.Empty,
                ["lastChangeTime"] = bug.LastChangeTime,
            },
            Comments: comments);
    }

    private static bool TryGetBugId(Uri url, out int id)
    {
        id = 0;
        if (!url.AbsolutePath.EndsWith(ShowBugPage, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var idText = HttpUtility.ParseQueryString(url.Query)["id"];
        return int.TryParse(idText, NumberStyles.None, CultureInfo.InvariantCulture, out id);
    }

    private sealed record BugsResponse(List<BugzillaBug> Bugs);

    private sealed record BugzillaBug(int Id, string Summary, string Status, string? Resolution, string LastChangeTime);

    private sealed record CommentsResponse(Dictionary<string, BugComments> Bugs);

    private sealed record BugComments(List<BugzillaComment> Comments);

    private sealed record BugzillaComment(int Id, string Creator, DateTimeOffset CreationTime, string Text);
}
