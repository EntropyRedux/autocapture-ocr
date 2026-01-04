using System.Drawing;

namespace AutoCaptureOCR.Core.Models;

/// <summary>
/// Result of a screen capture operation
/// </summary>
public class CaptureResult
{
    public Bitmap? Image { get; set; }
    public DateTime Timestamp { get; set; }
    public Rectangle Region { get; set; }
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }

    // Convenience properties for region coordinates
    public int X => Region.X;
    public int Y => Region.Y;
    public int Width => Region.Width;
    public int Height => Region.Height;
}
