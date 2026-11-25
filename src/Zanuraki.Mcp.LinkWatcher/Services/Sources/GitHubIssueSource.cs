namespace Zanuraki.Mcp.LinkWatcher.Services.Sources;

using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;

public sealed partial class GitHubIssueSource(
    IHttpClientFactory httpClientFactory,
    Func<string, string?>? getEnvironmentVariable = null) : ILinkSource
{
    private const string ApiBase = "https://api.github.com/repos";
    private const string TokenVariable = "GITHUB_TOKEN";
    private const int MaxCommentPages = 10;
    private const string BotUserType = "Bot";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    private readonly Func<string, string?> _getEnvironmentVariable = getEnvironmentVariable ?? Environment.GetEnvironmentVariable;

    public string Name => "github";

    public bool CanHandle(Uri url) => MatchIssueUrl(url) is not null;

    public async Task<LinkSnapshot> FetchAsync(Uri url, CancellationToken cancellationToken)
    {
        var match = MatchIssueUrl(url) ?? throw new ArgumentException("Not a GitHub issue or pull request URL.", nameof(url));
        var issueUrl = $"{ApiBase}/{match.Groups["owner"].Value}/{match.Groups["repo"].Value}/issues/{match.Groups["number"].Value}";
        var token = _getEnvironmentVariable(TokenVariable);
        var client = httpClientFactory.CreateClient();

        using var issueRequest = CreateRequest(issueUrl, token);
        using var issueResponse = await client.SendAsync(issueRequest, cancellationToken);
        issueResponse.EnsureSuccessStatusCode();
        var issue = await issueResponse.Content.ReadFromJsonAsync<IssueResponse>(JsonOptions, cancellationToken)
            ?? throw new HttpRequestException("GitHub returned an empty issue body.");

        var comments = await FetchCommentsAsync(client, $"{issueUrl}/comments?per_page=100", token, cancellationToken);
        var status = issue.StateReason is null
            ? issue.State.ToUpperInvariant()
            : $"{issue.State.ToUpperInvariant()} ({issue.StateReason})";

        return new LinkSnapshot(
            Title: issue.Title,
            Status: status,
            Fingerprint: new Dictionary<string, string>
            {
                ["status"] = status,
                ["commentCount"] = comments.Count.ToString(CultureInfo.InvariantCulture),
                ["lastCommentId"] = comments.LastOrDefault()?.Id ?? string.Empty,
                ["updatedAt"] = issue.UpdatedAt,
            },
            Comments: comments);
    }

    [GeneratedRegex(@"^/(?<owner>[^/]+)/(?<repo>[^/]+)/(?:issues|pull)/(?<number>\d+)/?$")]
    private static partial Regex IssuePath();

    [GeneratedRegex("<([^>]+)>;\\s*rel=\"next\"")]
    private static partial Regex NextLink();

    private static Match? MatchIssueUrl(Uri url)
    {
        if (url.Scheme != Uri.UriSchemeHttps || !url.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var match = IssuePath().Match(url.AbsolutePath);
        return match.Success ? match : null;
    }

    private static async Task<List<SourceComment>> FetchCommentsAsync(
        HttpClient client,
        string firstPageUrl,
        string? token,
        CancellationToken cancellationToken)
    {
        var comments = new List<SourceComment>();
        string? pageUrl = firstPageUrl;

        for (var page = 0; page < MaxCommentPages && pageUrl is not null; page++)
        {
            using var request = CreateRequest(pageUrl, token);
            using var response = await client.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();

            var items = await response.Content.ReadFromJsonAsync<List<CommentResponse>>(JsonOptions, cancellationToken)
                ?? [];
            comments.AddRange(items
                .Where(item => item.User.Type != BotUserType)
                .Select(item => new SourceComment(
                    item.Id.ToString(CultureInfo.InvariantCulture),
                    item.User.Login,
                    item.CreatedAt,
                    item.Body ?? string.Empty)));

            pageUrl = NextPageUrl(response);
        }

        return comments;
    }

    private static string? NextPageUrl(HttpResponseMessage response)
    {
        if (!response.Headers.TryGetValues("Link", out var linkHeaders))
        {
            return null;
        }

        foreach (var linkHeader in linkHeaders)
        {
            var match = NextLink().Match(linkHeader);
            if (match.Success)
            {
                return match.Groups[1].Value;
            }
        }

        return null;
    }

    private static HttpRequestMessage CreateRequest(string requestUrl, string? token)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, requestUrl);
        request.Headers.UserAgent.ParseAdd("mcp-link-watcher");
        request.Headers.Accept.ParseAdd("application/vnd.github+json");
        request.Headers.Add("X-GitHub-Api-Version", "2022-11-28");

        if (!string.IsNullOrEmpty(token))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return request;
    }

    private sealed record IssueResponse(string Title, string State, string? StateReason, string UpdatedAt);

    private sealed record CommentResponse(long Id, string? Body, DateTimeOffset CreatedAt, GitHubUser User);

    private sealed record GitHubUser(string Login, string Type);
}
