using AutoCaptureOCR.Core.Models;

namespace AutoCaptureOCR.Core.Services;

/// <summary>
/// Calculates analytics data for projects and sessions
/// </summary>
public class AnalyticsService
{
    /// <summary>
    /// Calculate analytics for an entire project
    /// </summary>
    public AnalyticsData CalculateProjectAnalytics(Project project)
    {
        var allCaptures = project.Sessions.SelectMany(s => s.Captures).ToList();
        return CalculateAnalyticsForCaptures(allCaptures);
    }

    /// <summary>
    /// Calculate analytics for a specific session
    /// </summary>
    public AnalyticsData CalculateSessionAnalytics(CaptureSession session)
    {
        return CalculateAnalyticsForCaptures(session.Captures.ToList());
    }

    private AnalyticsData CalculateAnalyticsForCaptures(List<ScreenCapture> captures)
    {
        var analytics = new AnalyticsData
        {
            TotalCaptures = captures.Count
        };

        if (captures.Count == 0)
            return analytics;

        // Status breakdown
        analytics.CompletedCaptures = captures.Count(c => c.Status == CaptureStatus.Completed);
        analytics.FailedCaptures = captures.Count(c => c.Status == CaptureStatus.Failed);
        analytics.ProcessingCaptures = captures.Count(c => c.Status == CaptureStatus.Processing);

        analytics.StatusBreakdown = captures
            .GroupBy(c => c.Status.ToString())
            .ToDictionary(g => g.Key, g => g.Count());

        // Timestamps
        analytics.EarliestCapture = captures.Min(c => c.Timestamp);
        analytics.LatestCapture = captures.Max(c => c.Timestamp);

        // OCR-specific analytics
        var capturesWithOCR = captures.Where(c => c.OCRResult != null).ToList();

        if (capturesWithOCR.Count > 0)
        {
            // Average confidence
            analytics.AverageConfidence = capturesWithOCR.Average(c => c.OCRResult!.Confidence);

            // Confidence distribution
            var highConfidence = capturesWithOCR.Count(c => c.OCRResult!.Confidence >= 0.9);
            var mediumConfidence = capturesWithOCR.Count(c => c.OCRResult!.Confidence >= 0.7 && c.OCRResult.Confidence < 0.9);
            var lowConfidence = capturesWithOCR.Count(c => c.OCRResult!.Confidence < 0.7);

            analytics.ConfidenceBreakdown = new ConfidenceDistribution
            {
                High = highConfidence,
                Medium = mediumConfidence,
                Low = lowConfidence,
                HighPercentage = (highConfidence / (double)capturesWithOCR.Count) * 100,
                MediumPercentage = (mediumConfidence / (double)capturesWithOCR.Count) * 100,
                LowPercentage = (lowConfidence / (double)capturesWithOCR.Count) * 100
            };

            // Total words and lines
            analytics.TotalWords = capturesWithOCR.Sum(c =>
                c.OCRResult!.Text.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length);

            analytics.TotalLines = capturesWithOCR.Sum(c =>
                c.OCRResult!.Lines?.Count ?? 0);

            // Engine usage (if we track this - for now just Windows OCR)
            analytics.EngineUsage["Windows OCR"] = capturesWithOCR.Count;
        }

        // Processing time (calculated from timestamp differences - approximation)
        // In a real implementation, this would track actual processing times
        analytics.AverageProcessingTimeMs = 1500; // Placeholder

        return analytics;
    }

    /// <summary>
    /// Generate a text summary of analytics
    /// </summary>
    public string GenerateSummary(AnalyticsData analytics)
    {
        var summary = new System.Text.StringBuilder();

        summary.AppendLine($"Total Captures: {analytics.TotalCaptures}");
        summary.AppendLine($"Success Rate: {analytics.SuccessRate:F1}%");
        summary.AppendLine($"Completed: {analytics.CompletedCaptures}, Failed: {analytics.FailedCaptures}, Processing: {analytics.ProcessingCaptures}");
        summary.AppendLine();

        if (analytics.TotalCaptures > 0)
        {
            summary.AppendLine($"Average OCR Confidence: {analytics.AverageConfidence:P1}");
            summary.AppendLine($"Confidence Distribution:");
            summary.AppendLine($"  High (≥90%): {analytics.ConfidenceBreakdown.High} ({analytics.ConfidenceBreakdown.HighPercentage:F1}%)");
            summary.AppendLine($"  Medium (70-89%): {analytics.ConfidenceBreakdown.Medium} ({analytics.ConfidenceBreakdown.MediumPercentage:F1}%)");
            summary.AppendLine($"  Low (<70%): {analytics.ConfidenceBreakdown.Low} ({analytics.ConfidenceBreakdown.LowPercentage:F1}%)");
            summary.AppendLine();

            summary.AppendLine($"Total Words Extracted: {analytics.TotalWords:N0}");
            summary.AppendLine($"Total Lines Extracted: {analytics.TotalLines:N0}");
            summary.AppendLine();

            if (analytics.EarliestCapture.HasValue && analytics.LatestCapture.HasValue)
            {
                summary.AppendLine($"Date Range: {analytics.EarliestCapture:yyyy-MM-dd} to {analytics.LatestCapture:yyyy-MM-dd}");
            }
        }

        return summary.ToString();
    }
}
