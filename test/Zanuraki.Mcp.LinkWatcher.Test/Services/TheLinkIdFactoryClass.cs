namespace Zanuraki.Mcp.LinkWatcher.Test.Services;

using Shouldly;
using Zanuraki.Mcp.LinkWatcher.Services;

public abstract class TheLinkIdFactoryClass
{
    public class TheCreateMethod : TheLinkIdFactoryClass
    {
        [Test]
        public void ShouldDeriveIdFromGitHubIssue()
        {
            LinkIdFactory.Create(new Uri("https://github.com/anthropics/claude-code/issues/14228"))
                .ShouldBe("github-anthropics-claude-code-14228");
        }

        [Test]
        public void ShouldDeriveIdFromGitHubPullRequest()
        {
            LinkIdFactory.Create(new Uri("https://github.com/anthropics/claude-code/pull/15542"))
                .ShouldBe("github-anthropics-claude-code-15542");
        }

        [Test]
        public void ShouldDeriveIdFromGitHubDiscussion()
        {
            LinkIdFactory.Create(new Uri("https://github.com/anthropics/claude-code/discussions/77"))
                .ShouldBe("github-anthropics-claude-code-discussion-77");
        }

        [Test]
        public void ShouldDeriveIdFromBugzillaShowBug()
        {
            LinkIdFactory.Create(new Uri("https://bugs.kde.org/show_bug.cgi?id=446961"))
                .ShouldBe("bugs-kde-org-446961");
        }

        [Test]
        public void ShouldFallBackToHostAndPathForGenericUrl()
        {
            LinkIdFactory.Create(new Uri("https://support.example.com/tickets/Abc_123/view"))
                .ShouldBe("support-example-com-tickets-abc-123-view");
        }

        [Test]
        public void ShouldStripLeadingWwwFromGenericHost()
        {
            LinkIdFactory.Create(new Uri("https://www.example.com/a"))
                .ShouldBe("example-com-a");
        }

        [Test]
        public void ShouldTruncateToEightyCharactersWithoutTrailingDash()
        {
            var id = LinkIdFactory.Create(new Uri("https://example.com/" + new string('a', 70) + "-bbbbbbbbbbbb"));

            id.Length.ShouldBeLessThanOrEqualTo(80);
            id.ShouldNotEndWith("-");
            id.ShouldBe("example-com-" + new string('a', 68));
        }
    }
}
