namespace Zanuraki.Mcp.LinkWatcher.Test.Configuration;

using Shouldly;
using Zanuraki.Mcp.LinkWatcher.Configuration;

public abstract class TheLinkWatcherOptionsClass
{
    public class TheDataPathProperty : TheLinkWatcherOptionsClass
    {
        [Test]
        public void ShouldDefaultToLinksFolderInAppFolder()
        {
            var options = new LinkWatcherOptions();

            options.DataPath.ShouldBe(Path.Combine(Constants.AppFolderPath, "links"));
        }
    }

    public class TheStatePathProperty : TheLinkWatcherOptionsClass
    {
        [Test]
        public void ShouldDefaultToStateFolderInAppFolder()
        {
            var options = new LinkWatcherOptions();

            options.StatePath.ShouldBe(Path.Combine(Constants.AppFolderPath, "state"));
        }
    }
}
