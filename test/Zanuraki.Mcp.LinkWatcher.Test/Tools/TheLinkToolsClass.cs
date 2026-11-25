namespace Zanuraki.Mcp.LinkWatcher.Test.Tools;

using System.Globalization;
using System.IO.Pipelines;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using NSubstitute;
using Shouldly;
using Zanuraki.Mcp.LinkWatcher.Configuration;
using Zanuraki.Mcp.LinkWatcher.Models;
using Zanuraki.Mcp.LinkWatcher.Services;
using Zanuraki.Mcp.LinkWatcher.Services.Sources;
using Zanuraki.Mcp.LinkWatcher.Tools;

public abstract class TheLinkToolsClass
{
    private const string GitHubUrl = "https://github.com/acme/widgets/issues/43";
    private const string OtherGitHubUrl = "https://github.com/acme/widgets/issues/44";
    private const string AgentUrl = "https://forum.example.org/viewtopic.php?t=7";
    private const string GitHubId = "github-acme-widgets-43";
    private const string AgentId = "forum-example-org-viewtopic-php-t-7";

    private static readonly DateTimeOffset Now = new(2026, 11, 25, 10, 0, 0, TimeSpan.Zero);

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly string _root = Path.Combine(Path.GetTempPath(), "link-tools-tests", Guid.NewGuid().ToString("N"));

    protected TheLinkToolsClass()
    {
        Source.CanHandle(Arg.Is<Uri>(uri => uri.Host == "github.com")).Returns(true);
        Store = new LinkStore(
            Options.Create(new LinkWatcherOptions { DataPath = Path.Combine(_root, "links"), StatePath = Path.Combine(_root, "state") }),
            new FrontmatterService());
        Resolver = new SourceResolver([Source]);
    }

    protected string DataPath => Path.Combine(_root, "links");

    protected ILinkSource Source { get; } = Substitute.For<ILinkSource>();

    protected TimeProvider Time { get; } = new FixedTimeProvider(Now);

    protected LinkStore Store { get; }

    protected SourceResolver Resolver { get; }

    [After(HookType.Test)]
    public void DeleteTempFolder()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    protected static WatchedLink CreateLink(string url, LinkKind kind = LinkKind.Bug, string title = "Widgets crash on resize") => new()
    {
        Id = LinkIdFactory.Create(new Uri(url)),
        Url = new Uri(url),
        Title = title,
        Kind = kind,
        Added = new DateOnly(2026, 11, 20),
        Why = "Blocks our release.",
        ResolvedWhen = "A fix ships in a stable release.",
        Updates = [new LinkUpdate(new DateOnly(2026, 11, 20), "added; upstream OPEN.")],
    };

    protected static LinkSnapshot Snapshot(string status = "OPEN", params SourceComment[] comments) => new(
        "Widgets crash on resize",
        status,
        new Dictionary<string, string>
        {
            ["status"] = status,
            ["commentCount"] = comments.Length.ToString(CultureInfo.InvariantCulture),
            ["lastCommentId"] = comments.LastOrDefault()?.Id ?? string.Empty,
            ["updatedAt"] = "2026-11-25T09:00:00Z",
        },
        comments);

    protected static SourceComment Comment(string id, string body = "Still broken on 1.2") => new(id, "alice", Now, body);

    protected static LinkState Baseline(string id, string status = "OPEN") => new()
    {
        Id = id,
        Fingerprint = Snapshot(status).Fingerprint.ToDictionary(),
        LastStatus = status,
        LastChecked = Now,
        LastFetchedVia = "api",
    };

    protected void ReturnSnapshot(string url, LinkSnapshot snapshot) =>
        Source.FetchAsync(Arg.Is<Uri>(uri => uri.AbsoluteUri == url), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(snapshot));

    protected void FailFetch(string url) =>
        Source.FetchAsync(Arg.Is<Uri>(uri => uri.AbsoluteUri == url), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<LinkSnapshot>(new HttpRequestException("tracker unavailable")));

    protected async Task<McpTestFixture> CreateFixtureAsync()
    {
        var clientToServer = new Pipe();
        var serverToClient = new Pipe();

        var builder = Host.CreateApplicationBuilder();
        builder.Logging.ClearProviders();
        builder.Services.AddSingleton(Store);
        builder.Services.AddSingleton(Resolver);
        builder.Services.AddSingleton<TimeProvider>(Time);
        builder.Services
            .AddMcpServer()
            .WithStreamServerTransport(clientToServer.Reader.AsStream(), serverToClient.Writer.AsStream())
            .WithToolsFromAssembly(typeof(LinkTools).Assembly);

        var host = builder.Build();
        await host.StartAsync();

        var client = await McpClient.CreateAsync(
            new StreamClientTransport(
                clientToServer.Writer.AsStream(),
                serverToClient.Reader.AsStream(),
                NullLoggerFactory.Instance),
            clientOptions: null,
            NullLoggerFactory.Instance,
            CancellationToken.None);

        return new McpTestFixture(host, client);
    }

    protected static ValueTask<CallToolResult> CallAsync(McpClient client, string toolName, Dictionary<string, object?>? arguments = null) =>
        client.CallToolAsync(toolName, arguments, progress: null, options: null, CancellationToken.None);

    protected static T? Parse<T>(CallToolResult result) => JsonSerializer.Deserialize<T>(Text(result), JsonOptions);

    protected static string Text(CallToolResult result) => ((TextContentBlock)result.Content[0]).Text;

    protected static void ShouldBeError(CallToolResult result, string messageFragment)
    {
        result.IsError.ShouldBe(true);
        Text(result).ShouldContain(messageFragment);
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;

        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
    }

    protected sealed class McpTestFixture(IHost host, McpClient client) : IAsyncDisposable
    {
        public McpClient Client { get; } = client;

        public async ValueTask DisposeAsync()
        {
            await Client.DisposeAsync();
            await host.StopAsync();
            host.Dispose();
        }
    }

    public class TheToolsListing : TheLinkToolsClass
    {
        [Test]
        public async Task ShouldExposeExactlySevenSnakeCaseTools()
        {
            await using var fixture = await CreateFixtureAsync();

            var tools = await fixture.Client.ListToolsAsync();

            tools.Select(tool => tool.Name).Order().ShouldBe(
                ["add_link", "check_links", "get_link", "list_links", "record_update", "remove_link", "update_link"]);
        }
    }

    public class TheAddLinkMethod : TheLinkToolsClass
    {
        [Test]
        public async Task ShouldSaveApiLinkWithBaselineAndTrackerTitle()
        {
            ReturnSnapshot(GitHubUrl, Snapshot("OPEN"));
            await using var fixture = await CreateFixtureAsync();

            var result = await CallAsync(fixture.Client, "add_link", new()
            {
                ["url"] = GitHubUrl,
                ["kind"] = "bug",
                ["why"] = "Blocks our release.",
                ["resolvedWhen"] = "A fix ships in a stable release.",
            });

            var added = Parse<AddLinkResult>(result);
            added.ShouldNotBeNull();
            added.Note.ShouldBeNull();
            added.Link.Id.ShouldBe(GitHubId);
            added.Link.Title.ShouldBe("Widgets crash on resize");
            added.Link.CheckedBy.ShouldBe("api");
            added.Link.Updates.Single().Text.ShouldBe("added; upstream OPEN.");

            var state = Store.GetState(GitHubId);
            state.ShouldNotBeNull();
            state.Fingerprint!["status"].ShouldBe("OPEN");
            state.LastStatus.ShouldBe("OPEN");
            state.LastFetchedVia.ShouldBe("api");
            state.LastChecked.ShouldBe(Now);
        }

        [Test]
        public async Task ShouldAddAgentLinkWithGivenTitleAndNoBaseline()
        {
            await using var fixture = await CreateFixtureAsync();

            var result = await CallAsync(fixture.Client, "add_link", new()
            {
                ["url"] = AgentUrl,
                ["kind"] = "support",
                ["why"] = "Customer is waiting.",
                ["resolvedWhen"] = "The thread is answered.",
                ["title"] = "Forum thread",
                ["related"] = new[] { GitHubId },
            });

            var added = Parse<AddLinkResult>(result);
            added.ShouldNotBeNull();
            added.Link.CheckedBy.ShouldBe("agent");
            added.Link.Title.ShouldBe("Forum thread");
            added.Link.Kind.ShouldBe("support");
            added.Link.Related.ShouldBe([GitHubId]);
            added.Link.Updates.Single().Text.ShouldBe("added.");
            Store.GetState(AgentId).ShouldBeNull();
        }

        [Test]
        public async Task ShouldRejectAgentLinkWithoutTitle()
        {
            await using var fixture = await CreateFixtureAsync();

            var result = await CallAsync(fixture.Client, "add_link", new()
            {
                ["url"] = AgentUrl,
                ["kind"] = "bug",
                ["why"] = "Why",
                ["resolvedWhen"] = "When",
            });

            ShouldBeError(result, "title is required");
            Store.Get(AgentId).ShouldBeNull();
        }

        [Test]
        public async Task ShouldRejectDuplicateUrlAndNameExistingId()
        {
            await using var fixture = await CreateFixtureAsync();
            var arguments = new Dictionary<string, object?>
            {
                ["url"] = AgentUrl,
                ["kind"] = "bug",
                ["why"] = "Why",
                ["resolvedWhen"] = "When",
                ["title"] = "Forum thread",
            };
            await CallAsync(fixture.Client, "add_link", arguments);

            var result = await CallAsync(fixture.Client, "add_link", arguments);

            ShouldBeError(result, $"already watched as '{AgentId}'");
        }

        [Test]
        public async Task ShouldRejectUnknownKind()
        {
            await using var fixture = await CreateFixtureAsync();

            var result = await CallAsync(fixture.Client, "add_link", new()
            {
                ["url"] = AgentUrl,
                ["kind"] = "security",
                ["why"] = "Why",
                ["resolvedWhen"] = "When",
                ["title"] = "Forum thread",
            });

            ShouldBeError(result, "Valid values: bug, support, feature");
        }

        [Test]
        public async Task ShouldRejectNonHttpUrl()
        {
            await using var fixture = await CreateFixtureAsync();

            var result = await CallAsync(fixture.Client, "add_link", new()
            {
                ["url"] = "ftp://example.org/file",
                ["kind"] = "bug",
                ["why"] = "Why",
                ["resolvedWhen"] = "When",
                ["title"] = "File",
            });

            ShouldBeError(result, "not an absolute http or https URL");
        }

        [Test]
        public async Task ShouldAddWithGivenTitleAndNoteWhenTrackerFetchFails()
        {
            FailFetch(GitHubUrl);
            await using var fixture = await CreateFixtureAsync();

            var result = await CallAsync(fixture.Client, "add_link", new()
            {
                ["url"] = GitHubUrl,
                ["kind"] = "bug",
                ["why"] = "Why",
                ["resolvedWhen"] = "When",
                ["title"] = "Given title",
            });

            var added = Parse<AddLinkResult>(result);
            added.ShouldNotBeNull();
            added.Link.Title.ShouldBe("Given title");
            added.Link.Updates.Single().Text.ShouldBe("added.");
            added.Note!.ShouldContain("baseline will be set on the next check");
            Store.GetState(GitHubId).ShouldBeNull();
        }

        [Test]
        public async Task ShouldRejectWhenTrackerFetchFailsAndNoTitleGiven()
        {
            FailFetch(GitHubUrl);
            await using var fixture = await CreateFixtureAsync();

            var result = await CallAsync(fixture.Client, "add_link", new()
            {
                ["url"] = GitHubUrl,
                ["kind"] = "bug",
                ["why"] = "Why",
                ["resolvedWhen"] = "When",
            });

            ShouldBeError(result, "title is required");
            Store.Get(GitHubId).ShouldBeNull();
        }
    }

    public class TheListLinksMethod : TheLinkToolsClass
    {
        [Test]
        public async Task ShouldListLinksWithCheckedByAndFilterByKind()
        {
            Store.Save(CreateLink(GitHubUrl, LinkKind.Bug));
            Store.Save(CreateLink(AgentUrl, LinkKind.Support, "Forum thread"));
            await using var fixture = await CreateFixtureAsync();

            var all = Parse<List<LinkSummary>>(await CallAsync(fixture.Client, "list_links"));
            var support = Parse<List<LinkSummary>>(await CallAsync(fixture.Client, "list_links", new() { ["kind"] = "support" }));

            all!.Select(link => (link.Id, link.CheckedBy)).ShouldBe([(AgentId, "agent"), (GitHubId, "api")]);
            support!.Select(link => link.Id).ShouldBe([AgentId]);
            support![0].Kind.ShouldBe("support");
        }

        [Test]
        public async Task ShouldPruneStatesWithoutLinks()
        {
            Store.SaveState(new LinkState { Id = "orphan" });
            await using var fixture = await CreateFixtureAsync();

            await CallAsync(fixture.Client, "list_links");

            Store.GetState("orphan").ShouldBeNull();
        }
    }

    public class TheGetLinkMethod : TheLinkToolsClass
    {
        [Test]
        public async Task ShouldReturnFullLinkAndStateWithPendingFlag()
        {
            Store.Save(CreateLink(GitHubUrl) with
            {
                Related = ["forum-example-org-viewtopic-php-t-1"],
                Workaround = "Disable hardware scaling.",
            });
            var state = Baseline(GitHubId);
            state.PendingDelta = """{"previousStatus":"OPEN","currentStatus":"CLOSED","newComments":[],"newFingerprint":{}}""";
            Store.SaveState(state);
            await using var fixture = await CreateFixtureAsync();

            var link = Parse<LinkDetails>(await CallAsync(fixture.Client, "get_link", new() { ["id"] = GitHubId }));

            link.ShouldNotBeNull();
            link.Workaround.ShouldBe("Disable hardware scaling.");
            link.Related.ShouldBe(["forum-example-org-viewtopic-php-t-1"]);
            link.ResolvedWhen.ShouldBe("A fix ships in a stable release.");
            link.Updates.Count.ShouldBe(1);
            link.State.ShouldNotBeNull();
            link.State.LastStatus.ShouldBe("OPEN");
            link.State.HasPendingChange.ShouldBeTrue();
        }

        [Test]
        public async Task ShouldRejectUnknownId()
        {
            await using var fixture = await CreateFixtureAsync();

            ShouldBeError(await CallAsync(fixture.Client, "get_link", new() { ["id"] = "missing" }), "No watched link with id 'missing'");
        }

        [Test]
        public async Task ShouldRejectIdOutsideSlugFormatWithoutTouchingTheFolder()
        {
            var outside = Path.Combine(Path.GetDirectoryName(DataPath)!, "outside.md");
            Directory.CreateDirectory(Path.GetDirectoryName(DataPath)!);
            File.WriteAllText(outside, "outside");
            await using var fixture = await CreateFixtureAsync();

            ShouldBeError(await CallAsync(fixture.Client, "get_link", new() { ["id"] = "../outside" }), "No watched link with id '../outside'");
            File.Exists(outside).ShouldBeTrue();
        }
    }

    public class TheCheckLinksMethod : TheLinkToolsClass
    {
        [Test]
        public async Task ShouldBaselineApiLinkWithoutReportingChange()
        {
            Store.Save(CreateLink(GitHubUrl));
            ReturnSnapshot(GitHubUrl, Snapshot("OPEN"));
            await using var fixture = await CreateFixtureAsync();

            var result = Parse<CheckLinksResult>(await CallAsync(fixture.Client, "check_links"));

            result.ShouldNotBeNull();
            result.BaselinedCount.ShouldBe(1);
            result.UnchangedCount.ShouldBe(0);
            result.Changed.ShouldBeEmpty();
            Store.GetState(GitHubId)!.Fingerprint!["status"].ShouldBe("OPEN");
        }

        [Test]
        public async Task ShouldCountUnchangedApiLink()
        {
            Store.Save(CreateLink(GitHubUrl));
            Store.SaveState(Baseline(GitHubId));
            ReturnSnapshot(GitHubUrl, Snapshot("OPEN"));
            await using var fixture = await CreateFixtureAsync();

            var result = Parse<CheckLinksResult>(await CallAsync(fixture.Client, "check_links"));

            result!.UnchangedCount.ShouldBe(1);
            result.Changed.ShouldBeEmpty();
        }

        [Test]
        public async Task ShouldReportChangeAndStorePendingDeltaWithoutAdvancingFingerprint()
        {
            Store.Save(CreateLink(GitHubUrl));
            Store.SaveState(Baseline(GitHubId));
            ReturnSnapshot(GitHubUrl, Snapshot("CLOSED", Comment("c1")));
            await using var fixture = await CreateFixtureAsync();

            var result = Parse<CheckLinksResult>(await CallAsync(fixture.Client, "check_links"));

            result!.Changed.Count.ShouldBe(1);
            var changed = result.Changed[0];
            changed.Id.ShouldBe(GitHubId);
            changed.PreviousStatus.ShouldBe("OPEN");
            changed.CurrentStatus.ShouldBe("CLOSED");
            changed.ResolvedWhen.ShouldBe("A fix ships in a stable release.");
            changed.NewComments.Single().Author.ShouldBe("alice");

            var state = Store.GetState(GitHubId);
            state!.PendingDelta.ShouldNotBeNull();
            state.Fingerprint!["status"].ShouldBe("OPEN");
        }

        [Test]
        public async Task ShouldReportPendingChangeAgainWhenTrackerReverts()
        {
            Store.Save(CreateLink(GitHubUrl));
            Store.SaveState(Baseline(GitHubId));
            ReturnSnapshot(GitHubUrl, Snapshot("CLOSED", Comment("c1")));
            await using var fixture = await CreateFixtureAsync();
            await CallAsync(fixture.Client, "check_links");
            ReturnSnapshot(GitHubUrl, Snapshot("OPEN"));

            var result = Parse<CheckLinksResult>(await CallAsync(fixture.Client, "check_links"));

            result!.Changed.Single().CurrentStatus.ShouldBe("CLOSED");
            result.UnchangedCount.ShouldBe(0);
        }

        [Test]
        public async Task ShouldIsolateFetchErrorFromOtherLinks()
        {
            Store.Save(CreateLink(GitHubUrl));
            Store.Save(CreateLink(OtherGitHubUrl));
            Store.SaveState(Baseline(GitHubId));
            Store.SaveState(Baseline("github-acme-widgets-44"));
            FailFetch(GitHubUrl);
            ReturnSnapshot(OtherGitHubUrl, Snapshot("OPEN"));
            await using var fixture = await CreateFixtureAsync();

            var result = Parse<CheckLinksResult>(await CallAsync(fixture.Client, "check_links"));

            result!.UnchangedCount.ShouldBe(1);
            result.Errors.Single().Id.ShouldBe(GitHubId);
            result.Errors.Single().Url.ShouldBe(GitHubUrl);
        }

        [Test]
        public async Task ShouldListAgentLinksAsNeedsAgentCheckWithLastState()
        {
            Store.Save(CreateLink(AgentUrl, LinkKind.Support, "Forum thread"));
            Store.SaveState(new LinkState
            {
                Id = AgentId,
                LastSummary = "Open, no replies.",
                LastFetchedVia = "fetch",
                LastChecked = Now,
            });
            await using var fixture = await CreateFixtureAsync();

            var result = Parse<CheckLinksResult>(await CallAsync(fixture.Client, "check_links"));

            var item = result!.NeedsAgentCheck.Single();
            item.Id.ShouldBe(AgentId);
            item.Url.ShouldBe(AgentUrl);
            item.Title.ShouldBe("Forum thread");
            item.LastSummary.ShouldBe("Open, no replies.");
            item.LastFetchedVia.ShouldBe("fetch");
            result.Note.ShouldContain("untrusted third-party text");
        }

        [Test]
        public async Task ShouldReportUnknownIdAsError()
        {
            await using var fixture = await CreateFixtureAsync();

            var result = Parse<CheckLinksResult>(await CallAsync(fixture.Client, "check_links", new() { ["ids"] = new[] { "missing" } }));

            result!.Errors.Single().Id.ShouldBe("missing");
            result.Errors.Single().Url.ShouldBeNull();
        }

        [Test]
        public async Task ShouldReportIdOutsideSlugFormatAsErrorWithoutFetching()
        {
            await using var fixture = await CreateFixtureAsync();

            var result = Parse<CheckLinksResult>(await CallAsync(fixture.Client, "check_links", new() { ["ids"] = new[] { "../x" } }));

            result!.Errors.Single().Id.ShouldBe("../x");
            _ = Source.DidNotReceiveWithAnyArgs().FetchAsync(default!, default);
        }
    }

    public class TheRecordUpdateMethod : TheLinkToolsClass
    {
        [Test]
        public async Task ShouldAcknowledgePendingChangeAndAppendRelevantUpdate()
        {
            Store.Save(CreateLink(GitHubUrl));
            Store.SaveState(Baseline(GitHubId));
            ReturnSnapshot(GitHubUrl, Snapshot("CLOSED", Comment("c1")));
            await using var fixture = await CreateFixtureAsync();
            await CallAsync(fixture.Client, "check_links");

            var result = Parse<LinkDetails>(await CallAsync(fixture.Client, "record_update", new()
            {
                ["id"] = GitHubId,
                ["summary"] = "Closed upstream as fixed.",
                ["relevant"] = true,
            }));

            result!.State!.HasPendingChange.ShouldBeFalse();
            result.State.LastStatus.ShouldBe("CLOSED");
            result.Updates.Last().ShouldBe(new LinkUpdate(new DateOnly(2026, 11, 25), "Closed upstream as fixed."));
            Store.GetState(GitHubId)!.Fingerprint!["status"].ShouldBe("CLOSED");

            var recheck = Parse<CheckLinksResult>(await CallAsync(fixture.Client, "check_links"));
            recheck!.Changed.ShouldBeEmpty();
            recheck.UnchangedCount.ShouldBe(1);
        }

        [Test]
        public async Task ShouldNotAppendUpdateWhenNotRelevant()
        {
            Store.Save(CreateLink(GitHubUrl));
            Store.SaveState(Baseline(GitHubId));
            await using var fixture = await CreateFixtureAsync();

            var result = Parse<LinkDetails>(await CallAsync(fixture.Client, "record_update", new()
            {
                ["id"] = GitHubId,
                ["summary"] = "Only a label change.",
                ["relevant"] = false,
            }));

            result!.Updates.Count.ShouldBe(1);
        }

        [Test]
        public async Task ShouldStoreSnapshotAndFetchedVia()
        {
            Store.Save(CreateLink(AgentUrl, LinkKind.Support, "Forum thread"));
            await using var fixture = await CreateFixtureAsync();

            var result = Parse<LinkDetails>(await CallAsync(fixture.Client, "record_update", new()
            {
                ["id"] = AgentId,
                ["summary"] = "Answered by a maintainer.",
                ["relevant"] = true,
                ["snapshot"] = "Thread has one reply from a maintainer.",
                ["fetchedVia"] = "browser",
            }));

            result!.State!.LastSummary.ShouldBe("Thread has one reply from a maintainer.");
            result.State.LastFetchedVia.ShouldBe("browser");
            result.State.LastChecked.ShouldBe(Now);
            result.Updates.Last().Text.ShouldBe("Answered by a maintainer.");
        }

        [Test]
        public async Task ShouldRejectUnknownFetchedVia()
        {
            Store.Save(CreateLink(AgentUrl, LinkKind.Support, "Forum thread"));
            await using var fixture = await CreateFixtureAsync();

            var result = await CallAsync(fixture.Client, "record_update", new()
            {
                ["id"] = AgentId,
                ["summary"] = "x",
                ["relevant"] = false,
                ["fetchedVia"] = "carrier-pigeon",
            });

            ShouldBeError(result, "Unknown fetchedVia 'carrier-pigeon'");
        }

        [Test]
        public async Task ShouldRejectUnknownId()
        {
            await using var fixture = await CreateFixtureAsync();

            ShouldBeError(
                await CallAsync(fixture.Client, "record_update", new() { ["id"] = "missing", ["summary"] = "x", ["relevant"] = false }),
                "No watched link with id 'missing'");
        }
    }

    public class TheUpdateLinkMethod : TheLinkToolsClass
    {
        [Test]
        public async Task ShouldChangeGivenFieldsAndKeepTheRest()
        {
            Store.Save(CreateLink(GitHubUrl) with { Related = ["a"] });
            await using var fixture = await CreateFixtureAsync();

            var result = Parse<LinkDetails>(await CallAsync(fixture.Client, "update_link", new()
            {
                ["id"] = GitHubId,
                ["title"] = "Widgets crash on resize (HiDPI)",
                ["kind"] = "support",
                ["resolvedWhen"] = "Support answers the ticket.",
            }));

            result!.Title.ShouldBe("Widgets crash on resize (HiDPI)");
            result.Kind.ShouldBe("support");
            result.ResolvedWhen.ShouldBe("Support answers the ticket.");
            result.Why.ShouldBe("Blocks our release.");
            result.Related.ShouldBe(["a"]);

            var stored = Store.Get(GitHubId);
            stored!.Kind.ShouldBe(LinkKind.Support);
            stored.Title.ShouldBe("Widgets crash on resize (HiDPI)");
        }

        [Test]
        public async Task ShouldReplaceRelatedList()
        {
            Store.Save(CreateLink(GitHubUrl) with { Related = ["a"] });
            await using var fixture = await CreateFixtureAsync();

            var result = Parse<LinkDetails>(await CallAsync(fixture.Client, "update_link", new()
            {
                ["id"] = GitHubId,
                ["related"] = new[] { "b", "c" },
            }));

            result!.Related.ShouldBe(["b", "c"]);
        }

        [Test]
        public async Task ShouldRejectUnknownId()
        {
            await using var fixture = await CreateFixtureAsync();

            ShouldBeError(
                await CallAsync(fixture.Client, "update_link", new() { ["id"] = "missing", ["title"] = "x" }),
                "No watched link with id 'missing'");
        }
    }

    public class TheRemoveLinkMethod : TheLinkToolsClass
    {
        [Test]
        public async Task ShouldDeleteMarkdownAndStateFiles()
        {
            Store.Save(CreateLink(GitHubUrl));
            Store.SaveState(Baseline(GitHubId));
            await using var fixture = await CreateFixtureAsync();

            var removed = Parse<RemovedLink>(await CallAsync(fixture.Client, "remove_link", new() { ["id"] = GitHubId }));

            removed!.Id.ShouldBe(GitHubId);
            Store.Get(GitHubId).ShouldBeNull();
            Store.GetState(GitHubId).ShouldBeNull();
        }

        [Test]
        public async Task ShouldRejectUnknownId()
        {
            await using var fixture = await CreateFixtureAsync();

            ShouldBeError(await CallAsync(fixture.Client, "remove_link", new() { ["id"] = "missing" }), "No watched link with id 'missing'");
        }

        [Test]
        public async Task ShouldRejectIdOutsideSlugFormatWithoutDeletingOutsideTheFolder()
        {
            var outside = Path.Combine(Path.GetDirectoryName(DataPath)!, "outside.md");
            Directory.CreateDirectory(Path.GetDirectoryName(DataPath)!);
            File.WriteAllText(outside, "outside");
            await using var fixture = await CreateFixtureAsync();

            ShouldBeError(await CallAsync(fixture.Client, "remove_link", new() { ["id"] = "../outside" }), "No watched link with id '../outside'");
            File.Exists(outside).ShouldBeTrue();
        }
    }
}
