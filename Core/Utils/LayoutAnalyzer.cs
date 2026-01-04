using AutoCaptureOCR.Core.Models;

namespace AutoCaptureOCR.Core.Utils;

/// <summary>
/// Analyzes OCR layout and calculates spatial relationships
/// </summary>
public static class LayoutAnalyzer
{
    /// <summary>
    /// Calculate spatial relationships between OCR elements
    /// </summary>
    public static LayoutAnalysis AnalyzeLayout(OCRResult ocrResult)
    {
        var analysis = new LayoutAnalysis();

        if (ocrResult.Lines == null || ocrResult.Lines.Count == 0)
        {
            return analysis;
        }

        // Calculate overall bounds
        analysis.CanvasBounds = CalculateCanvasBounds(ocrResult.Lines);

        // Analyze each line
        for (int i = 0; i < ocrResult.Lines.Count; i++)
        {
            var line = ocrResult.Lines[i];
            var lineAnalysis = new LineAnalysis
            {
                LineNumber = line.LineNumber,
                Text = line.Text,
                BoundingBox = line.BoundingBox
            };

            // Find spatial relationships with other lines
            if (i > 0)
            {
                var previousLine = ocrResult.Lines[i - 1];
                lineAnalysis.RelativePosition = CalculateRelativePosition(previousLine.BoundingBox, line.BoundingBox);
                lineAnalysis.VerticalGap = line.BoundingBox.Y - (previousLine.BoundingBox.Y + previousLine.BoundingBox.Height);
            }

            analysis.Lines.Add(lineAnalysis);
        }

        return analysis;
    }

    /// <summary>
    /// Calculate relative position between two bounding boxes
    /// </summary>
    private static string CalculateRelativePosition(BoundingBox reference, BoundingBox target)
    {
        var refCenterY = reference.Y + reference.Height / 2;
        var targetCenterY = target.Y + target.Height / 2;
        var refCenterX = reference.X + reference.Width / 2;
        var targetCenterX = target.X + target.Width / 2;

        var verticalDiff = targetCenterY - refCenterY;
        var horizontalDiff = targetCenterX - refCenterX;

        // Determine primary direction
        if (Math.Abs(verticalDiff) > Math.Abs(horizontalDiff))
        {
            // Primarily vertical relationship
            if (verticalDiff > 0)
            {
                return Math.Abs(horizontalDiff) < reference.Width / 4 ? "below" : "below-offset";
            }
            else
            {
                return Math.Abs(horizontalDiff) < reference.Width / 4 ? "above" : "above-offset";
            }
        }
        else
        {
            // Primarily horizontal relationship
            if (horizontalDiff > 0)
            {
                return Math.Abs(verticalDiff) < reference.Height / 2 ? "right" : "diagonal-right";
            }
            else
            {
                return Math.Abs(verticalDiff) < reference.Height / 2 ? "left" : "diagonal-left";
            }
        }
    }

    /// <summary>
    /// Calculate overall canvas bounds from all lines
    /// </summary>
    private static BoundingBox CalculateCanvasBounds(List<OCRLine> lines)
    {
        if (lines.Count == 0)
        {
            return new BoundingBox();
        }

        var minX = lines.Min(l => l.BoundingBox.X);
        var minY = lines.Min(l => l.BoundingBox.Y);
        var maxX = lines.Max(l => l.BoundingBox.X + l.BoundingBox.Width);
        var maxY = lines.Max(l => l.BoundingBox.Y + l.BoundingBox.Height);

        return new BoundingBox
        {
            X = minX,
            Y = minY,
            Width = maxX - minX,
            Height = maxY - minY
        };
    }
}

/// <summary>
/// Layout analysis result
/// </summary>
public class LayoutAnalysis
{
    public BoundingBox CanvasBounds { get; set; } = new();
    public List<LineAnalysis> Lines { get; set; } = new();
}

/// <summary>
/// Analysis of a single line's layout
/// </summary>
public class LineAnalysis
{
    public int LineNumber { get; set; }
    public string Text { get; set; } = string.Empty;
    public BoundingBox BoundingBox { get; set; } = new();
    public string? RelativePosition { get; set; }
    public int VerticalGap { get; set; }
}
