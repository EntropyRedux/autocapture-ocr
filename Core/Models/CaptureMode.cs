namespace AutoCaptureOCR.Core.Models;

/// <summary>
/// Defines how captures should be processed
/// </summary>
public enum CaptureMode
{
    /// <summary>
    /// Capture images and automatically queue for OCR processing
    /// </summary>
    CaptureAndOcr,

    /// <summary>
    /// Capture images only without OCR processing
    /// </summary>
    CaptureOnly,

    /// <summary>
    /// Process existing images with OCR (no new captures)
    /// </summary>
    OcrOnly
}
