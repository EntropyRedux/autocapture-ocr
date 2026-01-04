namespace AutoCaptureOCR.Core.Models;

/// <summary>
/// Export configuration options
/// </summary>
public class ExportOptions
{
    public bool IncludeOCRResults { get; set; } = true;
    public bool IncludeMetadata { get; set; } = true;
    public bool IncludeBoundingBoxes { get; set; } = true;
    public bool IncludeThumbnails { get; set; } = false;
    public bool CompressOutput { get; set; } = false;
    public ExportFormat Format { get; set; } = ExportFormat.JSON;
    public OcrTextFormat OcrTextFormat { get; set; } = OcrTextFormat.Continuous;
}

/// <summary>
/// Supported export formats
/// </summary>
public enum ExportFormat
{
    JSON,
    CSV
}

/// <summary>
/// OCR text formatting options for CSV export
/// </summary>
public enum OcrTextFormat
{
    /// <summary>All text in one continuous line (default)</summary>
    Continuous,
    /// <summary>Each line separated by newlines</summary>
    Lines,
    /// <summary>Structured format with line numbers</summary>
    Structured,
    /// <summary>JSON format with full OCR data</summary>
    Json
}

/// <summary>
/// Export result information
/// </summary>
public class ExportResult
{
    public bool Success { get; set; }
    public string? FilePath { get; set; }
    public string? ErrorMessage { get; set; }
    public int CapturesExported { get; set; }
    public long FileSizeBytes { get; set; }
    public TimeSpan Duration { get; set; }
    public List<string> Warnings { get; set; } = new();
}
