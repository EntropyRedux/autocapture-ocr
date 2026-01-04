using AutoCaptureOCR.Core.Models;
using System.Drawing;

namespace AutoCaptureOCR.Core.Interfaces;

/// <summary>
/// Interface for OCR engine implementations
/// </summary>
public interface IOCREngine
{
    string Name { get; }
    Task<OCRResult> ProcessAsync(Bitmap image);
    Task<bool> IsAvailableAsync();
}
