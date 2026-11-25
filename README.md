# Link Watcher MCP server

A .NET tool that runs an MCP server for watching URLs from bug reports, support issues and feature
requests, and reporting what changed at those URLs.

Status: work in progress.

Links on GitHub (issues and pull requests) and Bugzilla are checked by the server. Every other link is checked
by the AI agent, which reports back through the MCP tools.

## Install

```bash
dotnet pack src/Zanuraki.Mcp.LinkWatcher -o ./nupkg
dotnet tool install --global Zanuraki.Mcp.LinkWatcher --add-source ./nupkg
```

## Configure

```bash
link-watcher settings set data-path <dir>
```

Optional: set `GITHUB_TOKEN` in the environment for higher GitHub API rate limits.

## Register with Claude Code

```bash
claude mcp add --scope user link-watcher -- link-watcher mcp
```

## Build and test

```bash
dotnet build
dotnet test
```
