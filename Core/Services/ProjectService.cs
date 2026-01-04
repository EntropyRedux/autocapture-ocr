using AutoCaptureOCR.Core.Models;
using Newtonsoft.Json;

namespace AutoCaptureOCR.Core.Services;

/// <summary>
/// Manages projects, sessions, and captures
/// </summary>
public class ProjectService
{
    private readonly string projectsPath;

    public event EventHandler<ProjectChangedEventArgs>? ProjectChanged;
    public event EventHandler<CaptureAddedEventArgs>? CaptureAdded;

    public ProjectService(string appDataPath)
    {
        projectsPath = Path.Combine(appDataPath, "projects");
        Directory.CreateDirectory(projectsPath);
    }

    /// <summary>
    /// Create a new project
    /// </summary>
    public Project CreateProject(string name, string description = "")
    {
        var project = new Project
        {
            Name = name,
            Description = description,
            SavePath = Path.Combine(projectsPath, SanitizeFileName(name))
        };

        Directory.CreateDirectory(project.SavePath);
        SaveProject(project);

        ProjectChanged?.Invoke(this, new ProjectChangedEventArgs(project, ProjectChangeType.Created));

        return project;
    }

    /// <summary>
    /// Load project from file
    /// </summary>
    public Project? LoadProject(string projectId)
    {
        var projectFile = Path.Combine(projectsPath, projectId, "project.json");

        if (!File.Exists(projectFile))
            return null;

        try
        {
            var json = File.ReadAllText(projectFile);
            return JsonConvert.DeserializeObject<Project>(json);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Get all projects
    /// </summary>
    public List<Project> GetAllProjects()
    {
        var projects = new List<Project>();

        if (!Directory.Exists(projectsPath))
            return projects;

        foreach (var dir in Directory.GetDirectories(projectsPath))
        {
            var projectFile = Path.Combine(dir, "project.json");
            if (File.Exists(projectFile))
            {
                try
                {
                    var json = File.ReadAllText(projectFile);
                    var project = JsonConvert.DeserializeObject<Project>(json);
                    if (project != null)
                        projects.Add(project);
                }
                catch
                {
                    // Skip corrupted projects
                }
            }
        }

        return projects.OrderByDescending(p => p.Modified).ToList();
    }

    /// <summary>
    /// Save project to disk
    /// </summary>
    public void SaveProject(Project project)
    {
        project.Modified = DateTime.UtcNow;

        var projectDir = Path.Combine(projectsPath, project.Id.ToString());
        Directory.CreateDirectory(projectDir);

        var projectFile = Path.Combine(projectDir, "project.json");
        var json = JsonConvert.SerializeObject(project, Formatting.Indented);
        File.WriteAllText(projectFile, json);

        ProjectChanged?.Invoke(this, new ProjectChangedEventArgs(project, ProjectChangeType.Updated));
    }

    /// <summary>
    /// Delete project
    /// </summary>
    public void DeleteProject(Project project)
    {
        var projectDir = Path.Combine(projectsPath, project.Id.ToString());

        if (Directory.Exists(projectDir))
            Directory.Delete(projectDir, true);

        ProjectChanged?.Invoke(this, new ProjectChangedEventArgs(project, ProjectChangeType.Deleted));
    }

    /// <summary>
    /// Create new session in project
    /// </summary>
    public CaptureSession CreateSession(Project project, string name)
    {
        var session = new CaptureSession
        {
            Name = name
        };

        project.Sessions.Add(session);
        SaveProject(project);

        return session;
    }

    /// <summary>
    /// Add capture to session
    /// </summary>
    public ScreenCapture AddCapture(Project project, CaptureSession session, string filePath)
    {
        var capture = new ScreenCapture
        {
            SequenceNumber = session.Captures.Count + 1,
            FileName = Path.GetFileName(filePath),
            FilePath = filePath,
            Status = CaptureStatus.Captured
        };

        session.Captures.Add(capture);
        SaveProject(project);

        CaptureAdded?.Invoke(this, new CaptureAddedEventArgs(project, session, capture));

        return capture;
    }

    /// <summary>
    /// Update capture with OCR results
    /// </summary>
    public void UpdateCaptureOCR(Project project, ScreenCapture capture, OCRResult ocrResult)
    {
        capture.OCRResult = ocrResult;
        capture.Status = CaptureStatus.Completed;
        SaveProject(project);
    }

    private string SanitizeFileName(string fileName)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return string.Join("_", fileName.Split(invalid));
    }
}

public class ProjectChangedEventArgs : EventArgs
{
    public Project Project { get; }
    public ProjectChangeType ChangeType { get; }

    public ProjectChangedEventArgs(Project project, ProjectChangeType changeType)
    {
        Project = project;
        ChangeType = changeType;
    }
}

public class CaptureAddedEventArgs : EventArgs
{
    public Project Project { get; }
    public CaptureSession Session { get; }
    public ScreenCapture Capture { get; }

    public CaptureAddedEventArgs(Project project, CaptureSession session, ScreenCapture capture)
    {
        Project = project;
        Session = session;
        Capture = capture;
    }
}

public enum ProjectChangeType
{
    Created,
    Updated,
    Deleted
}
