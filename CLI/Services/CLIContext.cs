using AutoCaptureOCR.Core.Capture;
using AutoCaptureOCR.Core.Configuration;
using AutoCaptureOCR.Core.Interfaces;
using AutoCaptureOCR.Core.Models;
using AutoCaptureOCR.Core.OCR;
using AutoCaptureOCR.Core.Services;

namespace AutoCaptureOCR.CLI.Services;

/// <summary>
/// Provides shared services and configuration for CLI commands
/// </summary>
public class CLIContext
{
    public string AppDataPath { get; }
    public AppConfig Config { get; }
    public ConfigManager ConfigManager { get; }
    public CaptureManager CaptureManager { get; }
    public IOCREngine OCREngine { get; }
    public ProjectService ProjectService { get; }
    public MetadataService MetadataService { get; }
    public AnalyticsService AnalyticsService { get; }

    public CLIContext()
    {
        // Initialize paths
        AppDataPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "AutoCaptureOCR"
        );
        Directory.CreateDirectory(AppDataPath);

        // Load configuration
        ConfigManager = new ConfigManager();
        Config = ConfigManager.LoadConfig();

        // Initialize services
        CaptureManager = new CaptureManager();
        OCREngine = new WindowsOCREngine();
        ProjectService = new ProjectService(AppDataPath);
        MetadataService = new MetadataService(AppDataPath);
        AnalyticsService = new AnalyticsService();
    }

    /// <summary>
    /// Get current or specified project
    /// </summary>
    public Project? GetProject(string? projectName = null)
    {
        if (string.IsNullOrEmpty(projectName))
        {
            // Get the most recently modified project
            var projects = ProjectService.GetAllProjects();
            return projects.FirstOrDefault();
        }

        // Try to find by name or ID
        var allProjects = ProjectService.GetAllProjects();

        // Try exact name match first
        var project = allProjects.FirstOrDefault(p =>
            p.Name.Equals(projectName, StringComparison.OrdinalIgnoreCase));

        if (project != null)
            return project;

        // Try GUID match
        if (Guid.TryParse(projectName, out var projectId))
        {
            project = allProjects.FirstOrDefault(p => p.Id == projectId);
        }

        return project;
    }

    /// <summary>
    /// Get current or specified session
    /// </summary>
    public CaptureSession? GetSession(Project project, string? sessionName = null)
    {
        if (string.IsNullOrEmpty(sessionName))
        {
            // Get the most recently created session
            return project.Sessions.OrderByDescending(s => s.Created).FirstOrDefault();
        }

        // Try to find by name or ID
        var session = project.Sessions.FirstOrDefault(s =>
            s.Name.Equals(sessionName, StringComparison.OrdinalIgnoreCase));

        if (session != null)
            return session;

        // Try GUID match
        if (Guid.TryParse(sessionName, out var sessionId))
        {
            session = project.Sessions.FirstOrDefault(s => s.Id == sessionId);
        }

        return session;
    }

    /// <summary>
    /// Save image to project directory
    /// </summary>
    public string SaveImage(Project project, System.Drawing.Bitmap bitmap, string? customFileName = null)
    {
        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var fileName = customFileName ?? $"capture_{timestamp}.png";

        var projectDir = Path.Combine(AppDataPath, "projects", project.Id.ToString(), "images");
        Directory.CreateDirectory(projectDir);

        var filePath = Path.Combine(projectDir, fileName);
        bitmap.Save(filePath, System.Drawing.Imaging.ImageFormat.Png);

        return filePath;
    }
}
