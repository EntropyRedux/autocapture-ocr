namespace AutoCaptureOCR.Core.Models;

/// <summary>
/// OCR processing result with text and confidence data
/// </summary>
public class OCRResult
{
    public string Text { get; set; } = string.Empty;
    public double Confidence { get; set; }
    public string EngineName { get; set; } = string.Empty;
    public TimeSpan ProcessingTime { get; set; }
    public List<OCRLine> Lines { get; set; } = new();
    public bool FallbackUsed { get; set; }
}

/// <summary>
/// Individual line detected by OCR with bounding box
/// </summary>
public class OCRLine
{
    public string Text { get; set; } = string.Empty;
    public double Confidence { get; set; }
    public int LineNumber { get; set; }
    public BoundingBox BoundingBox { get; set; } = new();
    public List<OCRWord> Words { get; set; } = new();
}

/// <summary>
/// Individual word detected by OCR with bounding box
/// </summary>
public class OCRWord
{
    public string Text { get; set; } = string.Empty;
    public BoundingBox BoundingBox { get; set; } = new();
}

/// <summary>
/// Bounding box coordinates for OCR text regions
/// </summary>
public class BoundingBox
{
    public int X { get; set; }
    public int Y { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
}
