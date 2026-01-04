using System.CommandLine;
using AutoCaptureOCR.CLI.Output;
using AutoCaptureOCR.CLI.Services;
using AutoCaptureOCR.Core.Export;
using AutoCaptureOCR.Core.Interfaces;
using AutoCaptureOCR.Core.Models;

namespace AutoCaptureOCR.CLI.Commands;

public static class ExportCommands
{
    public static Command CreateExportCommand()
    {
        var exportCommand = new Command("export", "Export project data");

        // autocapture export captures
        var capturesCommand = new Command("captures", "Export all captures from a project");
        var projectArg = new Argument<string>("project", "Project name or ID");
        var outputOption = new Option<string>("--output", "Output file path") { IsRequired = true };
        var formatOption = new Option<string>("--format", () => "json", "Export format: json, csv");
        var textFormatOption = new Option<string>("--text-format", () => "continuous",
            "OCR text format for CSV: continuous, lines, structured, json");

        capturesCommand.AddArgument(projectArg);
        capturesCommand.AddOption(outputOption);
        capturesCommand.AddOption(formatOption);
        capturesCommand.AddOption(textFormatOption);

        capturesCommand.SetHandler(async (string project, string output, string format, string textFormat) =>
        {
            await ExportCaptures(project, output, format, textFormat);
        }, projectArg, outputOption, formatOption, textFormatOption);

        // autocapture export analytics
        var analyticsCommand = new Command("analytics", "Export analytics data from a project");
        var analyticsProjectArg = new Argument<string>("project", "Project name or ID");
        var analyticsOutputOption = new Option<string>("--output", "Output file path") { IsRequired = true };

        analyticsCommand.AddArgument(analyticsProjectArg);
        analyticsCommand.AddOption(analyticsOutputOption);

        analyticsCommand.SetHandler(async (string project, string output) =>
        {
            await ExportAnalytics(project, output);
        }, analyticsProjectArg, analyticsOutputOption);

        exportCommand.AddCommand(capturesCommand);
        exportCommand.AddCommand(analyticsCommand);

        return exportCommand;
    }

    private static async Task ExportCaptures(string projectName, string outputPath, string format, string textFormat)
    {
        try
        {
            var context = new CLIContext();
            var project = context.GetProject(projectName);

            if (project == null)
            {
                ConsoleFormatter.Error($"Project not found: {projectName}");
                Environment.Exit(1);
                return;
            }

            var captureCount = project.Sessions.Sum(s => s.Captures.Count);
            if (captureCount == 0)
            {
                ConsoleFormatter.Warning("Project has no captures to export");
                return;
            }

            ConsoleFormatter.Info($"Exporting {captureCount} capture(s) from project '{project.Name}'...");

            // Select exporter based on format
            IExporter exporter = format.ToLower() switch
            {
                "json" => new JsonExporter(),
                "csv" => new CsvExporter(),
                _ => throw new ArgumentException($"Unknown format: {format}. Supported: json, csv")
            };

            // Parse OCR text format
            var ocrTextFormat = textFormat.ToLower() switch
            {
                "continuous" => OcrTextFormat.Continuous,
                "lines" => OcrTextFormat.Lines,
                "structured" => OcrTextFormat.Structured,
                "json" => OcrTextFormat.Json,
                _ => throw new ArgumentException($"Unknown text format: {textFormat}. Supported: continuous, lines, structured, json")
            };

            var options = new ExportOptions
            {
                IncludeMetadata = true,
                IncludeOCRResults = true,
                OcrTextFormat = ocrTextFormat
            };

            await exporter.ExportProjectAsync(project, outputPath, options);

            ConsoleFormatter.Success($"Exported to: {outputPath}");
        }
        catch (Exception ex)
        {
            ConsoleFormatter.Error($"Error: {ex.Message}");
            Environment.Exit(1);
        }
    }

    private static async Task ExportAnalytics(string projectName, string outputPath)
    {
        try
        {
            var context = new CLIContext();
            var project = context.GetProject(projectName);

            if (project == null)
            {
                ConsoleFormatter.Error($"Project not found: {projectName}");
                Environment.Exit(1);
                return;
            }

            ConsoleFormatter.Info($"Generating analytics for project '{project.Name}'...");

            var analytics = context.AnalyticsService.CalculateProjectAnalytics(project);

            // Export as JSON
            var json = Newtonsoft.Json.JsonConvert.SerializeObject(analytics, Newtonsoft.Json.Formatting.Indented);
            await File.WriteAllTextAsync(outputPath, json);

            ConsoleFormatter.Success($"Analytics exported to: {outputPath}");

            // Display summary
            ConsoleFormatter.WriteKeyValue("Total Captures", analytics.TotalCaptures.ToString());
            ConsoleFormatter.WriteKeyValue("Completed", analytics.CompletedCaptures.ToString());
            ConsoleFormatter.WriteKeyValue("Failed", analytics.FailedCaptures.ToString());
            ConsoleFormatter.WriteKeyValue("Average Confidence", $"{analytics.AverageConfidence:P0}");
        }
        catch (Exception ex)
        {
            ConsoleFormatter.Error($"Error: {ex.Message}");
            Environment.Exit(1);
        }
    }
}
