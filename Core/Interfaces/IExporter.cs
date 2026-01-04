using AutoCaptureOCR.Core.Models;

namespace AutoCaptureOCR.Core.Interfaces;

/// <summary>
/// Interface for export implementations
/// </summary>
public interface IExporter
{
    /// <summary>
    /// Export a project to a file
    /// </summary>
    Task<ExportResult> ExportProjectAsync(Project project, string outputPath, ExportOptions options);

    /// <summary>
    /// Export a specific session to a file
    /// </summary>
    Task<ExportResult> ExportSessionAsync(CaptureSession session, string outputPath, ExportOptions options);

    /// <summary>
    /// Gets the file extension for this exporter
    /// </summary>
    string FileExtension { get; }
}
