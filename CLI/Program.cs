using System.CommandLine;
using AutoCaptureOCR.CLI.Commands;
using AutoCaptureOCR.CLI.Output;
using Spectre.Console;

// Main root command
var rootCommand = new RootCommand("AutoCapture-OCR CLI - Screen capture and OCR automation tool");

// Add version option
rootCommand.Description = @"
AutoCapture-OCR v2.0 CLI

A powerful command-line tool for screen capture, OCR processing, and project management.

Examples:
  autocapture capture fullscreen --project ""My Project"" --ocr
  autocapture capture region --x 100 --y 100 --width 800 --height 600
  autocapture ocr run image.png --format json
  autocapture ocr batch ./screenshots --export results.md --format markdown
  autocapture project list
  autocapture export captures ""My Project"" --output data.json --format json

For more information: https://github.com/yourusername/AutoCapture-OCR
";

// Add all command groups
rootCommand.AddCommand(CaptureCommands.CreateCaptureCommand());
rootCommand.AddCommand(OcrCommands.CreateOcrCommand());
rootCommand.AddCommand(ProjectCommands.CreateProjectCommand());
rootCommand.AddCommand(ExportCommands.CreateExportCommand());

// Add config command
var configCommand = new Command("config", "Manage configuration");

var configShowCommand = new Command("show", "Show current configuration");
configShowCommand.SetHandler(() =>
{
    try
    {
        var context = new AutoCaptureOCR.CLI.Services.CLIContext();

        ConsoleFormatter.WriteHeader("Configuration");
        ConsoleFormatter.WriteKeyValue("App Data Path", context.AppDataPath);
        ConsoleFormatter.WriteKeyValue("Config File", Path.Combine(context.AppDataPath, "config.yaml"));

        AnsiConsole.WriteLine();
        ConsoleFormatter.Info("Current Settings:");
        ConsoleFormatter.WriteYaml(context.Config);
    }
    catch (Exception ex)
    {
        ConsoleFormatter.Error($"Error: {ex.Message}");
        Environment.Exit(1);
    }
});

var configPathCommand = new Command("path", "Show config file path");
configPathCommand.SetHandler(() =>
{
    var appDataPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "AutoCaptureOCR"
    );
    var configPath = Path.Combine(appDataPath, "config.yaml");

    ConsoleFormatter.Info($"Config file: {configPath}");

    if (File.Exists(configPath))
    {
        ConsoleFormatter.Success("File exists");
    }
    else
    {
        ConsoleFormatter.Warning("File does not exist (will be created on first run)");
    }
});

configCommand.AddCommand(configShowCommand);
configCommand.AddCommand(configPathCommand);
rootCommand.AddCommand(configCommand);

// Add version command
var versionCommand = new Command("version", "Show version information");
versionCommand.SetHandler(() =>
{
    ConsoleFormatter.WriteHeader("AutoCapture-OCR CLI");
    ConsoleFormatter.WriteKeyValue("Version", "2.0.0");
    ConsoleFormatter.WriteKeyValue("Framework", ".NET 9.0");
    ConsoleFormatter.WriteKeyValue("Platform", "Windows 10.0.22621");
    AnsiConsole.WriteLine();
    ConsoleFormatter.Info("For more information, visit: https://github.com/yourusername/AutoCapture-OCR");
});
rootCommand.AddCommand(versionCommand);

// Handle errors gracefully
try
{
    return await rootCommand.InvokeAsync(args);
}
catch (Exception ex)
{
    ConsoleFormatter.Error($"Unhandled error: {ex.Message}");
    if (args.Contains("--verbose") || args.Contains("-v"))
    {
        AnsiConsole.WriteException(ex);
    }
    return 1;
}
