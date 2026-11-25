namespace Zanuraki.Mcp.LinkWatcher;

public static class Constants
{
    public static readonly string AppFolderPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Zanuraki",
        "Mcp",
        "LinkWatcher");

    public static readonly string AppSettingsFilePath = Path.Combine(AppFolderPath, "appsettings.json");

    public static readonly string LogFilePath = Path.Combine(AppFolderPath, "logs", "app.log");
}
