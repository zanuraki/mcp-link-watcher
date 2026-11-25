namespace Zanuraki.Mcp.LinkWatcher.Configuration;

using System.Diagnostics.CodeAnalysis;

[SuppressMessage("ReSharper", "AutoPropertyCanBeMadeGetOnly.Global", Justification = "Used by IOptions binding")]
[SuppressMessage("ReSharper", "PropertyCanBeMadeInitOnly.Global", Justification = "Used by IOptions binding")]
public sealed class LinkWatcherOptions
{
    public const string SectionName = "LinkWatcher";

    public string DataPath { get; set; } = Path.Combine(Constants.AppFolderPath, "links");

    public string StatePath { get; set; } = Path.Combine(Constants.AppFolderPath, "state");
}
