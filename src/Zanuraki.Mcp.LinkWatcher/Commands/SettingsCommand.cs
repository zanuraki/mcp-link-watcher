namespace Zanuraki.Mcp.LinkWatcher.Commands;

using System.CommandLine;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Options;
using Zanuraki.Mcp.LinkWatcher.Configuration;

public sealed class SettingsCommand(IOptions<LinkWatcherOptions> options)
{
    public static Command Create(IOptions<LinkWatcherOptions> options)
    {
        var command = new SettingsCommand(options);

        var settingsCommand = new Command("settings", "Manage link watcher configuration")
        {
            CreateListCommand(command),
            CreateSetCommand(),
        };

        return settingsCommand;
    }

    private static Command CreateListCommand(SettingsCommand command)
    {
        var listCommand = new Command("list", "Print all resolved configuration values");

        listCommand.SetAction((_, _) =>
        {
            command.List();

            return Task.CompletedTask;
        });

        return listCommand;
    }

    private static Command CreateSetCommand()
    {
        var setCommand = new Command("set", "Set a configuration value")
        {
            CreateSetPathCommand("data-path", "DataPath", "Set the directory that holds the watched link files"),
            CreateSetPathCommand("state-path", "StatePath", "Set the directory that holds watcher state"),
        };

        return setCommand;
    }

    private static Command CreateSetPathCommand(string name, string key, string description)
    {
        var pathArg = new Argument<string>("path")
        {
            Description = "Absolute path to the directory",
        };

        var setPathCommand = new Command(name, description);
        setPathCommand.Add(pathArg);

        setPathCommand.SetAction((result, _) =>
        {
            var path = result.GetValue(pathArg)!;
            SetPath(key, path);

            return Task.CompletedTask;
        });

        return setPathCommand;
    }

    private void List()
    {
        Console.WriteLine($"{"DataPath",-10}: {options.Value.DataPath}");
        Console.WriteLine($"{"StatePath",-10}: {options.Value.StatePath}");
        Console.WriteLine($"{"AppFolder",-10}: {Constants.AppFolderPath}");
        Console.WriteLine($"{"LogFile",-10}: {Constants.LogFilePath}");
        Console.WriteLine($"{"Settings",-10}: {Constants.AppSettingsFilePath}");
    }

    private static void SetPath(string key, string path)
    {
        var json = ReadOrCreateUserConfig();
        var section = json[LinkWatcherOptions.SectionName]?.AsObject() ?? new JsonObject();
        section[key] = path;
        json[LinkWatcherOptions.SectionName] = section;
        WriteUserConfig(json);
        Console.WriteLine($"{key} set to: {path}");
    }

    private static JsonObject ReadOrCreateUserConfig()
    {
        var settingsPath = Constants.AppSettingsFilePath;

        if (!File.Exists(settingsPath))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(settingsPath)!);

            return new JsonObject();
        }

        var text = File.ReadAllText(settingsPath);

        return JsonNode.Parse(text)?.AsObject() ?? new JsonObject();
    }

    private static void WriteUserConfig(JsonObject json)
    {
        var text = json.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(Constants.AppSettingsFilePath, text);
    }
}
