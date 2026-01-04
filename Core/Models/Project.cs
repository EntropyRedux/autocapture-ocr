using System.Collections.ObjectModel;

namespace AutoCaptureOCR.Core.Models;

/// <summary>
/// Project containing organized capture sessions
/// </summary>
public class Project
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime Created { get; set; } = DateTime.UtcNow;
    public DateTime Modified { get; set; } = DateTime.UtcNow;
    public string ActiveMetadataTemplate { get; set; } = "Basic Capture";
    public ObservableCollection<CaptureSession> Sessions { get; set; } = new();
    public string SavePath { get; set; } = string.Empty;
}
