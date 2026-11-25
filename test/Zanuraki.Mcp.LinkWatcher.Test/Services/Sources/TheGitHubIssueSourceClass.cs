namespace Zanuraki.Mcp.LinkWatcher.Test.Services.Sources;

using System.Net;
using NSubstitute;
using Shouldly;
using Zanuraki.Mcp.LinkWatcher.Services.Sources;

public abstract class TheGitHubIssueSourceClass
{
    protected const string IssueUrl = "https://github.com/acme/widgets/issues/43";
    protected const string ApiIssueUrl = "https://api.github.com/repos/acme/widgets/issues/43";
    protected const string ApiCommentsUrl = ApiIssueUrl + "/comments?per_page=100";

    protected static GitHubIssueSource CreateSource(StubHttpMessageHandler handler, string? token = null)
    {
        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient(Arg.Any<string>()).Returns(_ => new HttpClient(handler, disposeHandler: false));
        return new GitHubIssueSource(factory, name => name == "GITHUB_TOKEN" ? token : null);
    }

    protected static string PageUrl(int page) => $"{ApiCommentsUrl}&page={page}";

    protected static string NextLink(string url) => $"<{url}>; rel=\"next\", <{url}>; rel=\"last\"";

    public class TheNameProperty : TheGitHubIssueSourceClass
    {
        [Test]
        public void ShouldBeGithub()
        {
            CreateSource(new StubHttpMessageHandler()).Name.ShouldBe("github");
        }
    }

    public class TheCanHandleMethod : TheGitHubIssueSourceClass
    {
        [Test]
        [Arguments("https://github.com/acme/widgets/issues/43")]
        [Arguments("https://github.com/acme/widgets/pull/7")]
        [Arguments("https://github.com/acme/widgets/issues/43/")]
        public void ShouldAcceptIssueAndPullRequestUrls(string url)
        {
            CreateSource(new StubHttpMessageHandler()).CanHandle(new Uri(url)).ShouldBeTrue();
        }

        [Test]
        [Arguments("https://github.com/acme/widgets/issues")]
        [Arguments("https://github.com/acme/widgets")]
        [Arguments("https://github.com/acme/widgets/issues/abc")]
        [Arguments("https://gitlab.com/acme/widgets/issues/43")]
        [Arguments("https://bugs.example.org/show_bug.cgi?id=43")]
        public void ShouldRejectOtherUrls(string url)
        {
            CreateSource(new StubHttpMessageHandler()).CanHandle(new Uri(url)).ShouldBeFalse();
        }
    }

    public class TheFetchAsyncMethod : TheGitHubIssueSourceClass
    {
        [Test]
        public async Task ShouldReturnTitleAndFormattedClosedStatus()
        {
            var handler = new StubHttpMessageHandler();
            handler.RouteFixture("https://api.github.com/repos/acme/widgets/issues/42", "github/issue-closed.json");
            handler.RouteFixture("https://api.github.com/repos/acme/widgets/issues/42/comments?per_page=100", "github/comments-page-1.json");

            var snapshot = await CreateSource(handler).FetchAsync(new Uri("https://github.com/acme/widgets/issues/42"), CancellationToken.None);

            snapshot.Title.ShouldBe("Widgets crash on resize");
            snapshot.Status.ShouldBe("CLOSED (completed)");
        }

        [Test]
        public async Task ShouldOmitStateReasonWhenNull()
        {
            var handler = new StubHttpMessageHandler();
            handler.RouteFixture(ApiIssueUrl, "github/issue-open.json");
            handler.RouteFixture(ApiCommentsUrl, "github/comments-empty.json");

            var snapshot = await CreateSource(handler).FetchAsync(new Uri(IssueUrl), CancellationToken.None);

            snapshot.Status.ShouldBe("OPEN");
        }

        [Test]
        public async Task ShouldDropBotComments()
        {
            var handler = new StubHttpMessageHandler();
            handler.RouteFixture(ApiIssueUrl, "github/issue-open.json");
            handler.RouteFixture(ApiCommentsUrl, "github/comments-page-1.json");

            var snapshot = await CreateSource(handler).FetchAsync(new Uri(IssueUrl), CancellationToken.None);

            snapshot.Comments.Count.ShouldBe(1);
            snapshot.Comments[0].Id.ShouldBe("1001");
            snapshot.Comments[0].Author.ShouldBe("alice");
            snapshot.Comments[0].Body.ShouldBe("Reproduced on 2.3.");
        }

        [Test]
        public async Task ShouldFollowNextLinkAcrossPages()
        {
            var handler = new StubHttpMessageHandler();
            handler.RouteFixture(ApiIssueUrl, "github/issue-open.json");
            handler.RouteFixture(ApiCommentsUrl, "github/comments-page-1.json", NextLink(PageUrl(2)));
            handler.RouteFixture(PageUrl(2), "github/comments-page-2.json");

            var snapshot = await CreateSource(handler).FetchAsync(new Uri(IssueUrl), CancellationToken.None);

            snapshot.Comments.Select(comment => comment.Id).ShouldBe(["1001", "1003"]);
            handler.Requests.Count.ShouldBe(3);
        }

        [Test]
        public async Task ShouldStopAfterTenCommentPages()
        {
            var handler = new StubHttpMessageHandler();
            handler.RouteFixture(ApiIssueUrl, "github/issue-open.json");
            for (var page = 1; page <= 11; page++)
            {
                var pageUrl = page == 1 ? ApiCommentsUrl : PageUrl(page);
                handler.RouteFixture(pageUrl, "github/comments-page-2.json", NextLink(PageUrl(page + 1)));
            }

            await CreateSource(handler).FetchAsync(new Uri(IssueUrl), CancellationToken.None);

            handler.Requests.Count.ShouldBe(11);
        }

        [Test]
        public async Task ShouldSendGitHubHeadersWithoutAuthorizationWhenTokenIsUnset()
        {
            var handler = new StubHttpMessageHandler();
            handler.RouteFixture(ApiIssueUrl, "github/issue-open.json");
            handler.RouteFixture(ApiCommentsUrl, "github/comments-empty.json");

            await CreateSource(handler).FetchAsync(new Uri(IssueUrl), CancellationToken.None);

            foreach (var request in handler.Requests)
            {
                request.Headers.UserAgent.ToString().ShouldBe("mcp-link-watcher");
                request.Headers.Accept.ToString().ShouldBe("application/vnd.github+json");
                request.Headers.GetValues("X-GitHub-Api-Version").ShouldBe(["2022-11-28"]);
                request.Headers.Authorization.ShouldBeNull();
            }
        }

        [Test]
        public async Task ShouldSendBearerTokenWhenGitHubTokenIsSet()
        {
            var handler = new StubHttpMessageHandler();
            handler.RouteFixture(ApiIssueUrl, "github/issue-open.json");
            handler.RouteFixture(ApiCommentsUrl, "github/comments-empty.json");

            await CreateSource(handler, token: "test-token").FetchAsync(new Uri(IssueUrl), CancellationToken.None);

            foreach (var request in handler.Requests)
            {
                request.Headers.Authorization!.Scheme.ShouldBe("Bearer");
                request.Headers.Authorization!.Parameter.ShouldBe("test-token");
            }
        }

        [Test]
        public async Task ShouldIgnoreEmptyGitHubToken()
        {
            var handler = new StubHttpMessageHandler();
            handler.RouteFixture(ApiIssueUrl, "github/issue-open.json");
            handler.RouteFixture(ApiCommentsUrl, "github/comments-empty.json");

            await CreateSource(handler, token: string.Empty).FetchAsync(new Uri(IssueUrl), CancellationToken.None);

            handler.Requests.ShouldAllBe(request => request.Headers.Authorization == null);
        }

        [Test]
        public async Task ShouldFingerprintStatusCommentCountLastCommentIdAndUpdatedAt()
        {
            var handler = new StubHttpMessageHandler();
            handler.RouteFixture(ApiIssueUrl, "github/issue-open.json");
            handler.RouteFixture(ApiCommentsUrl, "github/comments-page-1.json", NextLink(PageUrl(2)));
            handler.RouteFixture(PageUrl(2), "github/comments-page-2.json");

            var snapshot = await CreateSource(handler).FetchAsync(new Uri(IssueUrl), CancellationToken.None);

            snapshot.Fingerprint["status"].ShouldBe("OPEN");
            snapshot.Fingerprint["commentCount"].ShouldBe("2");
            snapshot.Fingerprint["lastCommentId"].ShouldBe("1003");
            snapshot.Fingerprint["updatedAt"].ShouldBe("2025-02-01T08:00:00Z");
        }

        [Test]
        public async Task ShouldUseEmptyLastCommentIdWhenThereAreNoComments()
        {
            var handler = new StubHttpMessageHandler();
            handler.RouteFixture(ApiIssueUrl, "github/issue-open.json");
            handler.RouteFixture(ApiCommentsUrl, "github/comments-empty.json");

            var snapshot = await CreateSource(handler).FetchAsync(new Uri(IssueUrl), CancellationToken.None);

            snapshot.Fingerprint["commentCount"].ShouldBe("0");
            snapshot.Fingerprint["lastCommentId"].ShouldBe(string.Empty);
            snapshot.Comments.ShouldBeEmpty();
        }

        [Test]
        public async Task ShouldThrowHttpRequestExceptionWhenIssueIsNotFound()
        {
            var handler = new StubHttpMessageHandler();
            handler.RouteStatus(ApiIssueUrl, HttpStatusCode.NotFound);

            await Should.ThrowAsync<HttpRequestException>(() =>
                CreateSource(handler).FetchAsync(new Uri(IssueUrl), CancellationToken.None));
        }
    }
}
