namespace Zanuraki.Mcp.LinkWatcher.Test.Services.Sources;

using System.Net;
using NSubstitute;
using Shouldly;
using Zanuraki.Mcp.LinkWatcher.Services.Sources;

public abstract class TheBugzillaSourceClass
{
    protected const string BugUrl = "https://bugzilla.example.org/bugzilla/show_bug.cgi?id=5";
    protected const string RestBugUrl = "https://bugzilla.example.org/bugzilla/rest/bug/5";
    protected const string BugFields = "?include_fields=id,summary,status,resolution,last_change_time";
    protected const string CommentsUrl = RestBugUrl + "/comment";

    protected static BugzillaSource CreateSource(StubHttpMessageHandler handler)
    {
        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient(Arg.Any<string>()).Returns(_ => new HttpClient(handler, disposeHandler: false));
        return new BugzillaSource(factory);
    }

    public class TheNameProperty : TheBugzillaSourceClass
    {
        [Test]
        public void ShouldBeBugzilla()
        {
            CreateSource(new StubHttpMessageHandler()).Name.ShouldBe("bugzilla");
        }
    }

    public class TheCanHandleMethod : TheBugzillaSourceClass
    {
        [Test]
        [Arguments("https://bugzilla.example.org/bugzilla/show_bug.cgi?id=5")]
        [Arguments("https://bugs.kde.org/show_bug.cgi?id=446961")]
        [Arguments("https://bugs.example.org/show_bug.cgi?product=foo&id=12")]
        public void ShouldAcceptShowBugUrlsWithIntegerId(string url)
        {
            CreateSource(new StubHttpMessageHandler()).CanHandle(new Uri(url)).ShouldBeTrue();
        }

        [Test]
        [Arguments("https://bugzilla.example.org/show_bug.cgi")]
        [Arguments("https://bugzilla.example.org/show_bug.cgi?id=abc")]
        [Arguments("https://bugzilla.example.org/show_bug.cgi?id=")]
        [Arguments("https://example.org/page.cgi?id=5")]
        [Arguments("https://github.com/acme/widgets/issues/43")]
        public void ShouldRejectOtherUrls(string url)
        {
            CreateSource(new StubHttpMessageHandler()).CanHandle(new Uri(url)).ShouldBeFalse();
        }
    }

    public class TheFetchAsyncMethod : TheBugzillaSourceClass
    {
        [Test]
        public async Task ShouldResolveRestBaseFromInstallationSubPath()
        {
            var handler = new StubHttpMessageHandler();
            handler.RouteFixture(RestBugUrl + BugFields, "bugzilla/bug-resolved.json");
            handler.RouteFixture(CommentsUrl, "bugzilla/comments.json");

            var snapshot = await CreateSource(handler).FetchAsync(new Uri(BugUrl), CancellationToken.None);

            snapshot.Title.ShouldBe("Crash on save");
            handler.Requests.Select(request => request.RequestUri!.AbsoluteUri)
                .ShouldBe([RestBugUrl + BugFields, CommentsUrl]);
        }

        [Test]
        public async Task ShouldAppendResolutionToStatusWhenPresent()
        {
            var handler = new StubHttpMessageHandler();
            handler.RouteFixture(RestBugUrl + BugFields, "bugzilla/bug-resolved.json");
            handler.RouteFixture(CommentsUrl, "bugzilla/comments.json");

            var snapshot = await CreateSource(handler).FetchAsync(new Uri(BugUrl), CancellationToken.None);

            snapshot.Status.ShouldBe("RESOLVED FIXED");
        }

        [Test]
        public async Task ShouldUseBareStatusWhenResolutionIsEmpty()
        {
            var handler = new StubHttpMessageHandler();
            handler.RouteFixture("https://bugzilla.example.org/rest/bug/7" + BugFields, "bugzilla/bug-new.json");
            handler.RouteFixture("https://bugzilla.example.org/rest/bug/7/comment", "bugzilla/comments-empty.json");

            var snapshot = await CreateSource(handler).FetchAsync(new Uri("https://bugzilla.example.org/show_bug.cgi?id=7"), CancellationToken.None);

            snapshot.Status.ShouldBe("NEW");
            snapshot.Title.ShouldBe("Typo in settings dialog");
        }

        [Test]
        public async Task ShouldMapCommentsInOrder()
        {
            var handler = new StubHttpMessageHandler();
            handler.RouteFixture(RestBugUrl + BugFields, "bugzilla/bug-resolved.json");
            handler.RouteFixture(CommentsUrl, "bugzilla/comments.json");

            var snapshot = await CreateSource(handler).FetchAsync(new Uri(BugUrl), CancellationToken.None);

            snapshot.Comments.Count.ShouldBe(2);
            snapshot.Comments[0].Id.ShouldBe("501");
            snapshot.Comments[0].Author.ShouldBe("reporter@example.org");
            snapshot.Comments[0].CreatedAt.ShouldBe(DateTimeOffset.Parse("2025-02-27T09:00:00Z"));
            snapshot.Comments[0].Body.ShouldBe("Saving a large document crashes the editor.");
            snapshot.Comments[1].Id.ShouldBe("502");
        }

        [Test]
        public async Task ShouldFingerprintStatusCommentCountLastCommentIdAndLastChangeTime()
        {
            var handler = new StubHttpMessageHandler();
            handler.RouteFixture(RestBugUrl + BugFields, "bugzilla/bug-resolved.json");
            handler.RouteFixture(CommentsUrl, "bugzilla/comments.json");

            var snapshot = await CreateSource(handler).FetchAsync(new Uri(BugUrl), CancellationToken.None);

            snapshot.Fingerprint["status"].ShouldBe("RESOLVED FIXED");
            snapshot.Fingerprint["commentCount"].ShouldBe("2");
            snapshot.Fingerprint["lastCommentId"].ShouldBe("502");
            snapshot.Fingerprint["lastChangeTime"].ShouldBe("2025-03-01T12:00:00Z");
        }

        [Test]
        public async Task ShouldThrowHttpRequestExceptionWhenBugIsNotFound()
        {
            var handler = new StubHttpMessageHandler();
            handler.RouteStatus(RestBugUrl + BugFields, HttpStatusCode.NotFound);

            await Should.ThrowAsync<HttpRequestException>(() =>
                CreateSource(handler).FetchAsync(new Uri(BugUrl), CancellationToken.None));
        }
    }
}
