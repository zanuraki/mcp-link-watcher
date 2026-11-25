namespace Zanuraki.Mcp.LinkWatcher.Test;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Zanuraki.Mcp.LinkWatcher.Services;
using Zanuraki.Mcp.LinkWatcher.Services.Sources;
using Zanuraki.Mcp.LinkWatcher.Tools;

public abstract class TheServiceCollectionExtensionsClass
{
    public class TheConfigureServicesMethod : TheServiceCollectionExtensionsClass
    {
        [Test]
        public void ShouldResolveLinkToolsAndSourceResolverWithBothTrackerSources()
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.ConfigureServices(new ConfigurationBuilder().Build());

            using var provider = services.BuildServiceProvider(new ServiceProviderOptions
            {
                ValidateOnBuild = true,
                ValidateScopes = true,
            });

            provider.GetRequiredService<LinkTools>().ShouldNotBeNull();
            var resolver = provider.GetRequiredService<SourceResolver>();
            resolver.Resolve(new Uri("https://github.com/acme/widgets/issues/43")).ShouldBeOfType<GitHubIssueSource>();
            resolver.Resolve(new Uri("https://bugs.example.org/show_bug.cgi?id=5")).ShouldBeOfType<BugzillaSource>();
        }
    }
}
