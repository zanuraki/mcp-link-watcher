namespace Zanuraki.Mcp.LinkWatcher.Test.Services;

using Microsoft.Extensions.Options;
using Shouldly;
using Zanuraki.Mcp.LinkWatcher.Configuration;
using Zanuraki.Mcp.LinkWatcher.Models;
using Zanuraki.Mcp.LinkWatcher.Services;

public abstract class TheLinkStoreClass
{
    private const string IssueUrl = "https://github.com/anthropics/claude-code/issues/15542";

    private readonly string _root = Path.Combine(Path.GetTempPath(), "link-store-tests", Guid.NewGuid().ToString("N"));

    protected string DataPath => Path.Combine(_root, "links");

    protected string StatePath => Path.Combine(_root, "state");

    [After(HookType.Test)]
    public void DeleteTempFolder()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    protected LinkStore CreateStore(string? dataPath = null) =>
        new(
            Options.Create(new LinkWatcherOptions { DataPath = dataPath ?? DataPath, StatePath = StatePath }),
            new FrontmatterService());

    protected static WatchedLink CreateLink(string url = IssueUrl, DateOnly? added = null) => new()
    {
        Id = LinkIdFactory.Create(new Uri(url)),
        Url = new Uri(url),
        Title = "Enable Claude Code to access chat history in Claude App",
        Kind = LinkKind.Feature,
        Added = added ?? new DateOnly(2026, 11, 25),
        Related = ["github-anthropics-claude-code-14228"],
        Why = "Users lose their chat history when switching devices.",
        ResolvedWhen = "Claude Code can read chat history from the Claude App.",
        Updates = [new LinkUpdate(added ?? new DateOnly(2026, 11, 25), "added; upstream OPEN.")],
    };

    protected static void ShouldMatch(WatchedLink? actual, WatchedLink expected)
    {
        actual.ShouldNotBeNull();
        actual.Id.ShouldBe(expected.Id);
        actual.Url.ShouldBe(expected.Url);
        actual.Title.ShouldBe(expected.Title);
        actual.Kind.ShouldBe(expected.Kind);
        actual.Added.ShouldBe(expected.Added);
        actual.Related.ShouldBe(expected.Related);
        actual.Why.ShouldBe(expected.Why);
        actual.ResolvedWhen.ShouldBe(expected.ResolvedWhen);
        actual.Workaround.ShouldBe(expected.Workaround);
        actual.Updates.ShouldBe(expected.Updates);
    }

    public class TheGetAllMethod : TheLinkStoreClass
    {
        [Test]
        public void ShouldReturnEmptyWhenDataFolderIsMissing()
        {
            CreateStore().GetAll().ShouldBeEmpty();
        }

        [Test]
        public void ShouldSortByAddedThenId()
        {
            var store = CreateStore();
            store.Save(CreateLink("https://github.com/o/r/issues/2", new DateOnly(2026, 3, 1)));
            store.Save(CreateLink("https://github.com/o/r/issues/1", new DateOnly(2026, 3, 1)));
            store.Save(CreateLink("https://github.com/o/r/issues/3", new DateOnly(2026, 1, 1)));

            store.GetAll().Select(link => link.Id).ShouldBe(["github-o-r-3", "github-o-r-1", "github-o-r-2"]);
        }

        [Test]
        public void ShouldIgnoreNonMarkdownFiles()
        {
            var store = CreateStore();
            store.Save(CreateLink());
            File.WriteAllText(Path.Combine(DataPath, "notes.txt"), "not a link");

            store.GetAll().Select(link => link.Id).ShouldBe(["github-anthropics-claude-code-15542"]);
        }
    }

    public class TheGetMethod : TheLinkStoreClass
    {
        [Test]
        public void ShouldReturnNullWhenMissing()
        {
            CreateStore().Get("missing").ShouldBeNull();
        }

        [Test]
        public void ShouldRoundTripLinkWithMultiLineSections()
        {
            var store = CreateStore();
            var link = CreateLink() with
            {
                Why = "First line.\n\nSecond paragraph with a detail.",
                ResolvedWhen = "- not a bullet\nlast line",
                Workaround = "Export manually.\nRe-import later.",
                Updates = [new LinkUpdate(new DateOnly(2026, 11, 26), "upstream moved to OPEN.\nsecond line")],
            };

            store.Save(link);

            ShouldMatch(store.Get(link.Id), link);
        }

        [Test]
        public void ShouldOmitWorkaroundSectionWhenNull()
        {
            var store = CreateStore();
            store.Save(CreateLink());

            File.ReadAllText(Path.Combine(DataPath, "github-anthropics-claude-code-15542.md"))
                .ShouldNotContain("## Workaround");
        }
    }

    public class TheSaveMethod : TheLinkStoreClass
    {
        [Test]
        public void ShouldWriteMarkdownInSpecFormat()
        {
            CreateStore().Save(CreateLink());

            File.ReadAllText(Path.Combine(DataPath, "github-anthropics-claude-code-15542.md")).ShouldBe(
                """
                ---
                url: https://github.com/anthropics/claude-code/issues/15542
                title: Enable Claude Code to access chat history in Claude App
                kind: feature
                added: 2026-11-25
                related:
                - github-anthropics-claude-code-14228
                ---
                ## Why
                Users lose their chat history when switching devices.

                ## Resolved when
                Claude Code can read chat history from the Claude App.

                ## Updates
                - 2026-11-25: added; upstream OPEN.

                """.ReplaceLineEndings("\n"));
        }

        [Test]
        public void ShouldExpandTildeInDataPath()
        {
            var relative = Path.Combine("mcp-link-watcher-tests", Guid.NewGuid().ToString("N"));
            var expanded = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), relative);
            try
            {
                var store = CreateStore("~/" + relative.Replace('\\', '/'));

                store.Save(CreateLink());

                File.Exists(Path.Combine(expanded, "github-anthropics-claude-code-15542.md")).ShouldBeTrue();
            }
            finally
            {
                if (Directory.Exists(expanded))
                {
                    Directory.Delete(expanded, recursive: true);
                }
            }
        }
    }

    public class TheFindByUrlMethod : TheLinkStoreClass
    {
        [Test]
        public void ShouldReturnLinkForUrl()
        {
            var store = CreateStore();
            var link = CreateLink();
            store.Save(link);

            ShouldMatch(store.FindByUrl(new Uri(IssueUrl)), link);
        }

        [Test]
        public void ShouldReturnNullForUnknownUrl()
        {
            CreateStore().FindByUrl(new Uri("https://github.com/o/r/issues/1")).ShouldBeNull();
        }
    }

    public class TheAppendUpdateMethod : TheLinkStoreClass
    {
        [Test]
        public void ShouldAppendUpdateAndKeepOtherFields()
        {
            var store = CreateStore();
            var link = CreateLink();
            store.Save(link);

            store.AppendUpdate(link.Id, new LinkUpdate(new DateOnly(2026, 11, 27), "closed upstream."));

            var updated = store.Get(link.Id);
            updated.ShouldNotBeNull();
            updated.Updates.ShouldBe([link.Updates[0], new LinkUpdate(new DateOnly(2026, 11, 27), "closed upstream.")]);
            updated.Why.ShouldBe(link.Why);
            updated.Related.ShouldBe(link.Related);
        }

        [Test]
        public void ShouldThrowWhenLinkIsMissing()
        {
            Should.Throw<KeyNotFoundException>(() =>
                CreateStore().AppendUpdate("missing", new LinkUpdate(new DateOnly(2026, 11, 27), "x")));
        }
    }

    public class TheDeleteMethod : TheLinkStoreClass
    {
        [Test]
        public void ShouldRemoveMarkdownAndStateFiles()
        {
            var store = CreateStore();
            var link = CreateLink();
            store.Save(link);
            store.SaveState(new LinkState { Id = link.Id });

            store.Delete(link.Id).ShouldBeTrue();

            store.Get(link.Id).ShouldBeNull();
            store.GetState(link.Id).ShouldBeNull();
        }

        [Test]
        public void ShouldReturnFalseWhenMarkdownIsMissing()
        {
            CreateStore().Delete("missing").ShouldBeFalse();
        }
    }

    public class TheGetStateMethod : TheLinkStoreClass
    {
        [Test]
        public void ShouldReturnNullWhenMissing()
        {
            CreateStore().GetState("missing").ShouldBeNull();
        }

        [Test]
        public void ShouldRoundTripStateThroughSaveState()
        {
            var store = CreateStore();
            var state = new LinkState
            {
                Id = "github-o-r-1",
                Fingerprint = new Dictionary<string, string> { ["title"] = "abc" },
                LastSummary = "Still open.",
                LastStatus = "OPEN",
                LastChecked = new DateTimeOffset(2026, 11, 25, 10, 0, 0, TimeSpan.Zero),
                LastFetchedVia = "api",
                PendingDelta = "{\"changed\":true}",
            };

            store.SaveState(state);

            var loaded = store.GetState("github-o-r-1");
            loaded.ShouldNotBeNull();
            loaded.Fingerprint.ShouldBe(state.Fingerprint);
            loaded.LastSummary.ShouldBe(state.LastSummary);
            loaded.LastChecked.ShouldBe(state.LastChecked);
            loaded.LastFetchedVia.ShouldBe(state.LastFetchedVia);
            loaded.PendingDelta.ShouldBe(state.PendingDelta);
        }
    }

    public class ThePruneOrphanedStatesMethod : TheLinkStoreClass
    {
        [Test]
        public void ShouldDeleteStatesWithoutMarkdownAndKeepOthers()
        {
            var store = CreateStore();
            var link = CreateLink();
            store.Save(link);
            store.SaveState(new LinkState { Id = link.Id });
            store.SaveState(new LinkState { Id = "orphan" });

            store.PruneOrphanedStates().ShouldBe(1);

            store.GetState(link.Id).ShouldNotBeNull();
            store.GetState("orphan").ShouldBeNull();
        }

        [Test]
        public void ShouldReturnZeroWhenStateFolderIsMissing()
        {
            CreateStore().PruneOrphanedStates().ShouldBe(0);
        }
    }
}
