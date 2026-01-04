namespace AutoCaptureOCR.Core.Models;

/// <summary>
/// Analytics data for a project or session
/// </summary>
public class AnalyticsData
{
    public int TotalCaptures { get; set; }
    public int CompletedCaptures { get; set; }
    public int FailedCaptures { get; set; }
    public int ProcessingCaptures { get; set; }

    public double AverageConfidence { get; set; }
    public double AverageProcessingTimeMs { get; set; }

    public ConfidenceDistribution ConfidenceBreakdown { get; set; } = new();
    public Dictionary<string, int> EngineUsage { get; set; } = new();
    public Dictionary<string, int> StatusBreakdown { get; set; } = new();

    public DateTime? EarliestCapture { get; set; }
    public DateTime? LatestCapture { get; set; }

    public int TotalWords { get; set; }
    public int TotalLines { get; set; }

    public double SuccessRate => TotalCaptures > 0
        ? (CompletedCaptures / (double)TotalCaptures) * 100
        : 0;
}

/// <summary>
/// Confidence level distribution
/// </summary>
public class ConfidenceDistribution
{
    public int High { get; set; }       // >= 0.9
    public int Medium { get; set; }     // 0.7 - 0.89
    public int Low { get; set; }        // < 0.7

    public double HighPercentage { get; set; }
    public double MediumPercentage { get; set; }
    public double LowPercentage { get; set; }
}
