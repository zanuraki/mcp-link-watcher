namespace Zanuraki.Mcp.LinkWatcher.Test.Services.Sources;

internal static class Fixture
{
    public static string Read(string relativePath) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", relativePath));
}
