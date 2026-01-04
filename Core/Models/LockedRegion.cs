namespace AutoCaptureOCR.Core.Models;

/// <summary>
/// Represents a locked screen region for continuous capture mode
/// </summary>
public class LockedRegion
{
    public int X { get; set; }
    public int Y { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public DateTime LockedAt { get; set; }
    public int CaptureCount { get; set; }
    public string? PreviewImagePath { get; set; }

    public string GetCoordinatesString()
    {
        return $"X:{X}, Y:{Y}, W:{Width}, H:{Height}";
    }

    public bool IsValid()
    {
        return Width > 0 && Height > 0;
    }
}
