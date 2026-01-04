using System.CommandLine;
using System.Drawing;
using AutoCaptureOCR.CLI.Output;
using AutoCaptureOCR.CLI.Services;
using AutoCaptureOCR.Core.Models;
using Spectre.Console;

namespace AutoCaptureOCR.CLI.Commands;

public static class CaptureCommands
{
    public static Command CreateCaptureCommand()
    {
        var captureCommand = new Command("capture", "Capture screenshots");

        // autocapture capture fullscreen
        var fullscreenCommand = new Command("fullscreen", "Capture entire screen");
        var projectOption = new Option<string?>("--project", "Project name or ID");
        var sessionOption = new Option<string?>("--session", "Session name or ID");
        var outputOption = new Option<string?>("--output", "Output file path (overrides project save)");
        var runOcrOption = new Option<bool>("--ocr", () => true, "Run OCR on capture");

        fullscreenCommand.AddOption(projectOption);
        fullscreenCommand.AddOption(sessionOption);
        fullscreenCommand.AddOption(outputOption);
        fullscreenCommand.AddOption(runOcrOption);

        fullscreenCommand.SetHandler(async (string? project, string? session, string? output, bool runOcr) =>
        {
            await CaptureFullscreen(project, session, output, runOcr);
        }, projectOption, sessionOption, outputOption, runOcrOption);

        // autocapture capture region
        var regionCommand = new Command("region", "Capture specific screen region");
        var xOption = new Option<int>("--x", "X coordinate") { IsRequired = true };
        var yOption = new Option<int>("--y", "Y coordinate") { IsRequired = true };
        var widthOption = new Option<int>("--width", "Width in pixels") { IsRequired = true };
        var heightOption = new Option<int>("--height", "Height in pixels") { IsRequired = true };

        regionCommand.AddOption(xOption);
        regionCommand.AddOption(yOption);
        regionCommand.AddOption(widthOption);
        regionCommand.AddOption(heightOption);
        regionCommand.AddOption(projectOption);
        regionCommand.AddOption(sessionOption);
        regionCommand.AddOption(outputOption);
        regionCommand.AddOption(runOcrOption);

        regionCommand.SetHandler(async (int x, int y, int width, int height, string? project, string? session, string? output, bool runOcr) =>
        {
            await CaptureRegion(x, y, width, height, project, session, output, runOcr);
        }, xOption, yOption, widthOption, heightOption, projectOption, sessionOption, outputOption, runOcrOption);

        captureCommand.AddCommand(fullscreenCommand);
        captureCommand.AddCommand(regionCommand);

        return captureCommand;
    }

    private static async Task CaptureFullscreen(string? projectName, string? sessionName, string? outputPath, bool runOcr)
    {
        try
        {
            var context = new CLIContext();

            ConsoleFormatter.Info("Capturing fullscreen...");
            await Task.Delay(500); // Small delay for UI to settle

            var result = await context.CaptureManager.CaptureFullscreenAsync();

            if (!result.Success || result.Image == null)
            {
                ConsoleFormatter.Error($"Capture failed: {result.ErrorMessage}");
                Environment.Exit(1);
                return;
            }

            // Save image
            string filePath;
            Project? project = null;
            CaptureSession? session = null;

            if (!string.IsNullOrEmpty(outputPath))
            {
                // Direct file save
                result.Image.Save(outputPath, System.Drawing.Imaging.ImageFormat.Png);
                filePath = outputPath;
                ConsoleFormatter.Success($"Saved to: {filePath}");
            }
            else
            {
                // Save to project
                project = context.GetProject(projectName);
                if (project == null)
                {
                    project = context.ProjectService.CreateProject(
                        projectName ?? "CLI Captures",
                        "Created via CLI"
                    );
                }

                session = context.GetSession(project, sessionName);
                if (session == null)
                {
                    session = context.ProjectService.CreateSession(project, sessionName ?? "CLI Session");
                }

                filePath = context.SaveImage(project, result.Image);
                var capture = context.ProjectService.AddCapture(project, session, filePath);

                ConsoleFormatter.Success($"Captured to project '{project.Name}' session '{session.Name}'");
                ConsoleFormatter.Info($"File: {filePath}");

                // Run OCR if requested
                if (runOcr)
                {
                    await RunOCR(context, result.Image, project, capture);
                }
            }
        }
        catch (Exception ex)
        {
            ConsoleFormatter.Error($"Error: {ex.Message}");
            Environment.Exit(1);
        }
    }

    private static async Task CaptureRegion(int x, int y, int width, int height, string? projectName, string? sessionName, string? outputPath, bool runOcr)
    {
        try
        {
            var context = new CLIContext();

            ConsoleFormatter.Info($"Capturing region at ({x}, {y}) size {width}x{height}...");
            await Task.Delay(500);

            var region = new Rectangle(x, y, width, height);
            var result = await context.CaptureManager.CaptureRegionInteractiveAsync(async () =>
            {
                return await Task.FromResult<Rectangle?>(region);
            });

            if (result == null || !result.Success || result.Image == null)
            {
                ConsoleFormatter.Error($"Capture failed: {result?.ErrorMessage ?? "Unknown error"}");
                Environment.Exit(1);
                return;
            }

            // Save image
            string filePath;
            Project? project = null;
            CaptureSession? session = null;

            if (!string.IsNullOrEmpty(outputPath))
            {
                // Direct file save
                result.Image.Save(outputPath, System.Drawing.Imaging.ImageFormat.Png);
                filePath = outputPath;
                ConsoleFormatter.Success($"Saved to: {filePath}");
            }
            else
            {
                // Save to project
                project = context.GetProject(projectName);
                if (project == null)
                {
                    project = context.ProjectService.CreateProject(
                        projectName ?? "CLI Captures",
                        "Created via CLI"
                    );
                }

                session = context.GetSession(project, sessionName);
                if (session == null)
                {
                    session = context.ProjectService.CreateSession(project, sessionName ?? "CLI Session");
                }

                filePath = context.SaveImage(project, result.Image);
                var capture = context.ProjectService.AddCapture(project, session, filePath);

                ConsoleFormatter.Success($"Captured to project '{project.Name}' session '{session.Name}'");
                ConsoleFormatter.Info($"File: {filePath}");

                // Run OCR if requested
                if (runOcr)
                {
                    await RunOCR(context, result.Image, project, capture);
                }
            }
        }
        catch (Exception ex)
        {
            ConsoleFormatter.Error($"Error: {ex.Message}");
            Environment.Exit(1);
        }
    }

    private static async Task RunOCR(CLIContext context, Bitmap image, Project project, ScreenCapture capture)
    {
        try
        {
            ConsoleFormatter.Info("Running OCR...");
            var ocrResult = await context.OCREngine.ProcessAsync(image);

            if (!string.IsNullOrWhiteSpace(ocrResult.Text))
            {
                context.ProjectService.UpdateCaptureOCR(project, capture, ocrResult);
                ConsoleFormatter.Success($"OCR completed. Confidence: {ocrResult.Confidence:P0}");
                AnsiConsole.WriteLine();
                AnsiConsole.MarkupLine("[grey]Text:[/]");
                AnsiConsole.WriteLine(ocrResult.Text);
            }
            else
            {
                ConsoleFormatter.Warning("No text detected in image");
            }
        }
        catch (Exception ex)
        {
            ConsoleFormatter.Warning($"OCR failed: {ex.Message}");
        }
    }
}
