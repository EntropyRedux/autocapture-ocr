using System.Collections.ObjectModel;

namespace AutoCaptureOCR.Core.Models;

/// <summary>
/// Session grouping related captures within a project.
/// Sessions are typed: Snapshot (v2 default), LiveChat, or VideoRecording.
/// </summary>
public class CaptureSession
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public DateTime Created { get; set; } = DateTime.UtcNow;
    public ObservableCollection<ScreenCapture> Captures { get; set; } = new();
    public string Notes { get; set; } = string.Empty;

    /// <summary>Discriminator for the kind of session (v3).</summary>
    public SessionType Type { get; set; } = SessionType.Snapshot;

    /// <summary>Name of the capture engine used (e.g. "DOM Extension", "UIA", "WGC").</summary>
    public string? SourceEngine { get; set; }

    /// <summary>Title of the target window being captured (for UIA/WGC sessions).</summary>
    public string? TargetWindowTitle { get; set; }

    /// <summary>Path to the Markdown transcript file (for LiveChat sessions).</summary>
    public string? TranscriptFilePath { get; set; }

    /// <summary>Path to the MP4 video file (for VideoRecording sessions).</summary>
    public string? VideoFilePath { get; set; }
}

/// <summary>
/// Type discriminator for capture sessions.
/// </summary>
public enum SessionType
{
    /// <summary>Traditional manual screenshot session (v2 behavior).</summary>
    Snapshot,

    /// <summary>Passive AI chat conversation archiving session.</summary>
    LiveChat,

    /// <summary>Continuous video recording with real-time OCR.</summary>
    VideoRecording
}

