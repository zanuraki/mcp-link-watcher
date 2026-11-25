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

To be filled in.

## TUnit patterns

Test naming: outer class `The{Class}Class` (abstract), nested class per member
`The{Member}Method` / `The{Member}Property` inheriting the outer class, test method `Should{description}`.
One file per outer class, named after it. Assertions use Shouldly (`result.ShouldBe(...)`), not TUnit's `Assert.That`.
