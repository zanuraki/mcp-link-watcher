namespace Zanuraki.Mcp.LinkWatcher.Test.Services.Sources;

using NSubstitute;
using Shouldly;
using Zanuraki.Mcp.LinkWatcher.Services.Sources;

public abstract class TheSourceResolverClass
{
    protected static readonly GitHubIssueSource GitHub = new(Substitute.For<IHttpClientFactory>());
    protected static readonly BugzillaSource Bugzilla = new(Substitute.For<IHttpClientFactory>());

    public class TheResolveMethod : TheSourceResolverClass
    {
        [Test]
        public void ShouldReturnGitHubSourceForIssueUrl()
        {
            var resolver = new SourceResolver([GitHub, Bugzilla]);

            resolver.Resolve(new Uri("https://github.com/acme/widgets/issues/43")).ShouldBeSameAs(GitHub);
        }

        [Test]
        public void ShouldReturnBugzillaSourceForShowBugUrl()
        {
            var resolver = new SourceResolver([GitHub, Bugzilla]);

            resolver.Resolve(new Uri("https://bugzilla.example.org/bugzilla/show_bug.cgi?id=5")).ShouldBeSameAs(Bugzilla);
        }

        [Test]
        public void ShouldReturnNullForGenericUrl()
        {
            var resolver = new SourceResolver([GitHub, Bugzilla]);

            resolver.Resolve(new Uri("https://example.org/articles/42")).ShouldBeNull();
        }

        [Test]
        public void ShouldReturnNullWhenNoSourcesAreRegistered()
        {
            var resolver = new SourceResolver([]);

            resolver.Resolve(new Uri("https://github.com/acme/widgets/issues/43")).ShouldBeNull();
        }
    }
}
