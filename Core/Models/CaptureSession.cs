using System.Collections.ObjectModel;

namespace AutoCaptureOCR.Core.Models;

/// <summary>
/// Session grouping related captures within a project
/// </summary>
public class CaptureSession
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public DateTime Created { get; set; } = DateTime.UtcNow;
    public ObservableCollection<ScreenCapture> Captures { get; set; } = new();
    public string Notes { get; set; } = string.Empty;
}
