using System.CommandLine;
using System.Drawing;
using AutoCaptureOCR.CLI.Output;
using AutoCaptureOCR.CLI.Services;
using AutoCaptureOCR.Core.Models;
using Newtonsoft.Json;
using Spectre.Console;

namespace AutoCaptureOCR.CLI.Commands;

public static class OcrCommands
{
    public static Command CreateOcrCommand()
    {
        var ocrCommand = new Command("ocr", "Run OCR on images");

        // autocapture ocr <image-path>
        var singleCommand = new Command("run", "Run OCR on a single image");
        var imagePathArg = new Argument<string>("image-path", "Path to image file");
        var formatOption = new Option<string>("--format", () => "text", "Output format: text, json, yaml");
        var languageOption = new Option<string?>("--language", "OCR language (default: system language)");

        singleCommand.AddArgument(imagePathArg);
        singleCommand.AddOption(formatOption);
        singleCommand.AddOption(languageOption);

        singleCommand.SetHandler(async (string imagePath, string format, string? language) =>
        {
            await RunSingleOcr(imagePath, format, language);
        }, imagePathArg, formatOption, languageOption);

        // autocapture ocr batch <folder-path>
        var batchCommand = new Command("batch", "Run OCR on multiple images in a folder");
        var folderPathArg = new Argument<string>("folder-path", "Path to folder containing images");
        var patternOption = new Option<string>("--pattern", () => "*.png", "File pattern (e.g., *.png, *.jpg)");
        var exportOption = new Option<string?>("--export", "Export results to file");
        var exportFormatOption = new Option<string>("--format", () => "json", "Export format: json, yaml, markdown, csv, text");
        var recursiveOption = new Option<bool>("--recursive", () => false, "Search subfolders");

        batchCommand.AddArgument(folderPathArg);
        batchCommand.AddOption(patternOption);
        batchCommand.AddOption(exportOption);
        batchCommand.AddOption(exportFormatOption);
        batchCommand.AddOption(recursiveOption);

        batchCommand.SetHandler(async (string folderPath, string pattern, string? export, string format, bool recursive) =>
        {
            await RunBatchOcr(folderPath, pattern, export, format, recursive);
        }, folderPathArg, patternOption, exportOption, exportFormatOption, recursiveOption);

        ocrCommand.AddCommand(singleCommand);
        ocrCommand.AddCommand(batchCommand);

        return ocrCommand;
    }

    private static async Task RunSingleOcr(string imagePath, string format, string? language)
    {
        try
        {
            if (!File.Exists(imagePath))
            {
                ConsoleFormatter.Error($"File not found: {imagePath}");
                Environment.Exit(1);
                return;
            }

            var context = new CLIContext();

            ConsoleFormatter.Info($"Processing: {Path.GetFileName(imagePath)}");

            using var bitmap = new Bitmap(imagePath);
            var result = await context.OCREngine.ProcessAsync(bitmap);

            if (string.IsNullOrWhiteSpace(result.Text))
            {
                ConsoleFormatter.Warning("No text detected in image");
                return;
            }

            // Output based on format
            switch (format.ToLower())
            {
                case "json":
                    ConsoleFormatter.WriteJson(new
                    {
                        file = Path.GetFileName(imagePath),
                        text = result.Text,
                        confidence = result.Confidence,
                        engine = result.EngineName,
                        processingTime = result.ProcessingTime.TotalMilliseconds
                    });
                    break;

                case "yaml":
                    ConsoleFormatter.WriteYaml(new
                    {
                        file = Path.GetFileName(imagePath),
                        text = result.Text,
                        confidence = result.Confidence,
                        engine = result.EngineName,
                        processingTime = result.ProcessingTime.TotalMilliseconds
                    });
                    break;

                case "text":
                default:
                    AnsiConsole.WriteLine(result.Text);
                    break;
            }
        }
        catch (Exception ex)
        {
            ConsoleFormatter.Error($"Error: {ex.Message}");
            Environment.Exit(1);
        }
    }

    private static async Task RunBatchOcr(string folderPath, string pattern, string? exportPath, string format, bool recursive)
    {
        try
        {
            if (!Directory.Exists(folderPath))
            {
                ConsoleFormatter.Error($"Folder not found: {folderPath}");
                Environment.Exit(1);
                return;
            }

            var context = new CLIContext();

            // Find all matching files
            var searchOption = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
            var files = Directory.GetFiles(folderPath, pattern, searchOption);

            if (files.Length == 0)
            {
                ConsoleFormatter.Warning($"No files found matching pattern: {pattern}");
                return;
            }

            ConsoleFormatter.Info($"Found {files.Length} file(s) to process");

            var results = new List<BatchOcrResult>();

            // Process with progress bar
            await AnsiConsole.Progress()
                .StartAsync(async ctx =>
                {
                    var task = ctx.AddTask("[green]Processing images[/]", maxValue: files.Length);

                    foreach (var file in files)
                    {
                        try
                        {
                            using var bitmap = new Bitmap(file);
                            var ocrResult = await context.OCREngine.ProcessAsync(bitmap);

                            results.Add(new BatchOcrResult
                            {
                                FileName = Path.GetFileName(file),
                                FilePath = file,
                                Text = ocrResult.Text ?? "",
                                Confidence = ocrResult.Confidence,
                                ProcessingTime = ocrResult.ProcessingTime,
                                Success = !string.IsNullOrWhiteSpace(ocrResult.Text)
                            });

                            task.Increment(1);
                        }
                        catch (Exception ex)
                        {
                            results.Add(new BatchOcrResult
                            {
                                FileName = Path.GetFileName(file),
                                FilePath = file,
                                Text = "",
                                Confidence = 0,
                                ProcessingTime = TimeSpan.Zero,
                                Success = false,
                                ErrorMessage = ex.Message
                            });
                            task.Increment(1);
                        }
                    }
                });

            // Display summary
            var successful = results.Count(r => r.Success);
            ConsoleFormatter.Success($"Processed {results.Count} images ({successful} successful, {results.Count - successful} failed)");

            // Export results if requested
            if (!string.IsNullOrEmpty(exportPath))
            {
                await ExportBatchResults(results, exportPath, format);
                ConsoleFormatter.Success($"Results exported to: {exportPath}");
            }
            else
            {
                // Display results in console
                DisplayBatchResults(results, format);
            }
        }
        catch (Exception ex)
        {
            ConsoleFormatter.Error($"Error: {ex.Message}");
            Environment.Exit(1);
        }
    }

    private static async Task ExportBatchResults(List<BatchOcrResult> results, string exportPath, string format)
    {
        switch (format.ToLower())
        {
            case "json":
                var json = JsonConvert.SerializeObject(results, Formatting.Indented);
                await File.WriteAllTextAsync(exportPath, json);
                break;

            case "yaml":
                var serializer = new YamlDotNet.Serialization.SerializerBuilder()
                    .WithNamingConvention(YamlDotNet.Serialization.NamingConventions.CamelCaseNamingConvention.Instance)
                    .Build();
                var yaml = serializer.Serialize(results);
                await File.WriteAllTextAsync(exportPath, yaml);
                break;

            case "markdown":
                var markdown = GenerateMarkdownReport(results);
                await File.WriteAllTextAsync(exportPath, markdown);
                break;

            case "csv":
                var csv = GenerateCsvReport(results);
                await File.WriteAllTextAsync(exportPath, csv);
                break;

            case "text":
                var text = string.Join("\n\n" + new string('=', 80) + "\n\n",
                    results.Select(r => $"File: {r.FileName}\nText:\n{r.Text}"));
                await File.WriteAllTextAsync(exportPath, text);
                break;

            default:
                throw new ArgumentException($"Unknown format: {format}");
        }
    }

    private static void DisplayBatchResults(List<BatchOcrResult> results, string format)
    {
        var table = new Table();
        table.AddColumn("File");
        table.AddColumn("Status");
        table.AddColumn("Confidence");
        table.AddColumn("Text Preview");

        foreach (var result in results)
        {
            var status = result.Success ? "[green]✓[/]" : "[red]✗[/]";
            var confidence = result.Success ? $"{result.Confidence:P0}" : "-";
            var preview = result.Success
                ? (result.Text.Length > 50 ? result.Text.Substring(0, 50) + "..." : result.Text)
                : (result.ErrorMessage ?? "Failed");

            table.AddRow(
                Markup.Escape(result.FileName),
                status,
                confidence,
                Markup.Escape(preview)
            );
        }

        AnsiConsole.Write(table);
    }

    private static string GenerateMarkdownReport(List<BatchOcrResult> results)
    {
        var md = new System.Text.StringBuilder();
        md.AppendLine("# OCR Batch Results");
        md.AppendLine();
        md.AppendLine($"**Processed:** {results.Count} images");
        md.AppendLine($"**Successful:** {results.Count(r => r.Success)}");
        md.AppendLine($"**Failed:** {results.Count(r => !r.Success)}");
        md.AppendLine($"**Date:** {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        md.AppendLine();
        md.AppendLine("---");
        md.AppendLine();

        foreach (var result in results)
        {
            md.AppendLine($"## {result.FileName}");
            md.AppendLine();
            if (result.Success)
            {
                md.AppendLine($"**Confidence:** {result.Confidence:P0}");
                md.AppendLine();
                md.AppendLine("```");
                md.AppendLine(result.Text);
                md.AppendLine("```");
            }
            else
            {
                md.AppendLine($"**Status:** Failed");
                md.AppendLine($"**Error:** {result.ErrorMessage}");
            }
            md.AppendLine();
        }

        return md.ToString();
    }

    private static string GenerateCsvReport(List<BatchOcrResult> results)
    {
        var csv = new System.Text.StringBuilder();
        csv.AppendLine("FileName,Success,Confidence,Text,Error");

        foreach (var result in results)
        {
            var text = result.Text.Replace("\"", "\"\"").Replace("\n", " ").Replace("\r", "");
            var error = result.ErrorMessage?.Replace("\"", "\"\"") ?? "";
            csv.AppendLine($"\"{result.FileName}\",{result.Success},{result.Confidence},\"{text}\",\"{error}\"");
        }

        return csv.ToString();
    }
}

public class BatchOcrResult
{
    public string FileName { get; set; } = "";
    public string FilePath { get; set; } = "";
    public string Text { get; set; } = "";
    public double Confidence { get; set; }
    public TimeSpan ProcessingTime { get; set; }
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
}
