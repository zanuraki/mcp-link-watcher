# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project purpose

MCP server (`Zanuraki.Mcp.LinkWatcher`) that manages a folder of Markdown files describing watched
URLs (bug reports, support issues, feature requests) and reports what changed at those URLs.

The server runs via stdio transport and is distributed as a .NET global tool (`link-watcher`).

## Tech stack

- .NET 10 (`global.json` pins SDK `10.0.100`, `latestFeature` roll-forward)
- `ModelContextProtocol` 2.x — MCP server + stdio transport
- `System.CommandLine` v3 preview — CLI subcommand parsing
- `Microsoft.Extensions.Hosting` — DI, configuration, logging
- `Microsoft.Extensions.Http` — `IHttpClientFactory`
- `Serilog` — file logging (daily rolling, under `%LOCALAPPDATA%\Zanuraki\Mcp\LinkWatcher\logs\`)
- `YamlDotNet` — YAML frontmatter parsing/serialization
- TUnit — test framework; `Shouldly` for assertions; `NSubstitute` for mocks

## Commands

- **Build**: `dotnet build` (from the repo root)
- **Test**: `dotnet test` (from the repo root)
- **Pack**: `dotnet pack src/Zanuraki.Mcp.LinkWatcher -o ./nupkg`
- **Install globally**: `dotnet tool install --global Zanuraki.Mcp.LinkWatcher --add-source ./nupkg`
- **Update globally**: `dotnet tool update --global Zanuraki.Mcp.LinkWatcher --add-source ./nupkg`

## CLI subcommand tree

```
link-watcher
├── mcp                            Start the MCP stdio server
└── settings
    ├── list                       Print all resolved configuration values
    └── set
        ├── data-path <path>       Set LinkWatcher:DataPath
        └── state-path <path>      Set LinkWatcher:StatePath
```

## Configuration

Two config sources are layered (last wins):

1. **Bundled** `appsettings.json` — empty defaults, never edited by users
2. **User config** `%LOCALAPPDATA%\Zanuraki\Mcp\LinkWatcher\appsettings.json` — machine-specific,
   survives `dotnet tool update`, created on first `settings set` call

Config section is `LinkWatcher`. Relevant class: `LinkWatcherOptions` in
`src/Zanuraki.Mcp.LinkWatcher/Configuration/LinkWatcherOptions.cs`.

| Key                     | Type     | Default                                          |
| ----------------------- | -------- | ------------------------------------------------ |
| `LinkWatcher:DataPath`  | `string` | `%LOCALAPPDATA%\Zanuraki\Mcp\LinkWatcher\links`  |
| `LinkWatcher:StatePath` | `string` | `%LOCALAPPDATA%\Zanuraki\Mcp\LinkWatcher\state`  |

## Architecture

Startup: `Program` → `HostFactory` (configuration, Serilog, DI from `ServiceCollectionExtensions.ConfigureServices`)
→ `CommandLineRoot` subcommands. `mcp` runs the stdio MCP server; `settings` edits the user config.

- **Tools** (`Tools/LinkTools.cs`): `[McpServerToolType]` class exposing the seven MCP tools
  (`add_link`, `list_links`, `get_link`, `check_links`, `record_update`, `update_link`, `remove_link`).
  Result types are records in `Models/`. Tool errors throw `McpException` so the message reaches the client;
  the SDK masks the message of any other exception type.
- **Services**: `LinkStore` reads and writes the Markdown and state files; `FrontmatterService` parses
  YAML frontmatter; `LinkIdFactory` derives the file-name id from a URL.
- **Sources** (`Services/Sources/`): `ILinkSource` implementations for trackers with an API (`GitHubIssueSource`,
  `BugzillaSource`). `SourceResolver` picks the source for a URL; a URL with no source is checked by the
  calling agent. `ChangeDetector` compares a stored fingerprint with a fresh snapshot.
- **Data layout**: `<DataPath>/<id>.md` (frontmatter, then Why, Resolved when, Workaround and Updates sections)
  and `<StatePath>/<id>.json` (`LinkState`: fingerprint, last status and summary, last checked, last fetched via,
  pending delta). `list_links` and `check_links` delete state files that have no Markdown file.
- **Pending-delta invariant**: `LinkState.Fingerprint` is the last acknowledged state. `check_links` stores a
  detected change in `LinkState.PendingDelta` and does not advance the fingerprint. Only `record_update` acknowledges
  it: it copies the pending fingerprint and status, then clears the pending delta. Until then the change is
  reported on every check.

| Tool            | Read-only | Purpose                                                                           |
| --------------- | --------- | --------------------------------------------------------------------------------- |
| `add_link`      | no        | Start watching a URL; API-checked links get a baseline                            |
| `list_links`    | yes       | List links with checked-by mode and last status; optional kind filter             |
| `get_link`      | yes       | Full link, its updates and check state, including `hasPendingChange`              |
| `check_links`   | no        | Check API links (4 at a time); list agent-checked links that need a look          |
| `record_update` | no        | Acknowledge a finding; optionally append an update and store the snapshot         |
| `update_link`   | no        | Change fields of a link; omitted fields are kept                                  |
| `remove_link`   | no        | Permanently delete a link and its state (only after the user confirms)            |

## TUnit patterns

Test naming: outer class `The{Class}Class` (abstract), nested class per member
`The{Member}Method` / `The{Member}Property` inheriting the outer class, test method `Should{description}`.
One file per outer class, named after it. Assertions use Shouldly (`result.ShouldBe(...)`), not TUnit's `Assert.That`.
