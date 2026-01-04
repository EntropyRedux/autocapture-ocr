namespace AutoCaptureOCR.Core.Models;

/// <summary>
/// Individual screen capture with OCR results and metadata
/// </summary>
public class ScreenCapture
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public int SequenceNumber { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public CaptureStatus Status { get; set; } = CaptureStatus.Captured;
    public OCRResult? OCRResult { get; set; }
    public Dictionary<string, string> Metadata { get; set; } = new();
    public CaptureMetadata? TemplateMetadata { get; set; }
    public string ThumbnailPath { get; set; } = string.Empty;
}

/// <summary>
/// Capture processing status
/// </summary>
public enum CaptureStatus
{
    Captured,
    Processing,
    Completed,
    Failed
}
