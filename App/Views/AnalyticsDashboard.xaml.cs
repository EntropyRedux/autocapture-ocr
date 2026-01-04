using System.Windows;
using System.Windows.Input;
using AutoCaptureOCR.Core.Models;
using AutoCaptureOCR.Core.Services;

namespace AutoCaptureOCR.App.Views;

public partial class AnalyticsDashboard : Window
{
    public AnalyticsDashboard(Project project, AnalyticsService analyticsService)
    {
        InitializeComponent();

        TitleTextBlock.Text = $"Analytics: {project.Name}";
        SubtitleTextBlock.Text = $"Project Analytics • {project.Sessions.Count} sessions";

        var analytics = analyticsService.CalculateProjectAnalytics(project);
        DisplayAnalytics(analytics);
    }

    public AnalyticsDashboard(CaptureSession session, string projectName, AnalyticsService analyticsService)
    {
        InitializeComponent();

        TitleTextBlock.Text = $"Analytics: {session.Name}";
        SubtitleTextBlock.Text = $"Session Analytics • Project: {projectName}";

        var analytics = analyticsService.CalculateSessionAnalytics(session);
        DisplayAnalytics(analytics);
    }

    private void DisplayAnalytics(AnalyticsData analytics)
    {
        // Overview Cards
        TotalCapturesText.Text = analytics.TotalCaptures.ToString("N0");
        SuccessRateText.Text = $"{analytics.SuccessRate:F1}%";
        AvgConfidenceText.Text = $"{analytics.AverageConfidence:P0}";
        TotalWordsText.Text = analytics.TotalWords.ToString("N0");

        // Status Breakdown
        CompletedText.Text = $"{analytics.CompletedCaptures} ({(analytics.TotalCaptures > 0 ? (analytics.CompletedCaptures / (double)analytics.TotalCaptures * 100) : 0):F0}%)";
        ProcessingText.Text = $"{analytics.ProcessingCaptures} ({(analytics.TotalCaptures > 0 ? (analytics.ProcessingCaptures / (double)analytics.TotalCaptures * 100) : 0):F0}%)";
        FailedText.Text = $"{analytics.FailedCaptures} ({(analytics.TotalCaptures > 0 ? (analytics.FailedCaptures / (double)analytics.TotalCaptures * 100) : 0):F0}%)";

        // Confidence Distribution
        HighConfidenceText.Text = $"{analytics.ConfidenceBreakdown.High} ({analytics.ConfidenceBreakdown.HighPercentage:F0}%)";
        MediumConfidenceText.Text = $"{analytics.ConfidenceBreakdown.Medium} ({analytics.ConfidenceBreakdown.MediumPercentage:F0}%)";
        LowConfidenceText.Text = $"{analytics.ConfidenceBreakdown.Low} ({analytics.ConfidenceBreakdown.LowPercentage:F0}%)";

        // Confidence Bars
        var total = analytics.ConfidenceBreakdown.High + analytics.ConfidenceBreakdown.Medium + analytics.ConfidenceBreakdown.Low;
        if (total > 0)
        {
            HighConfidenceBar.Width = (analytics.ConfidenceBreakdown.High / (double)total) * 500;
            MediumConfidenceBar.Width = (analytics.ConfidenceBreakdown.Medium / (double)total) * 500;
            LowConfidenceBar.Width = (analytics.ConfidenceBreakdown.Low / (double)total) * 500;
        }

        // Additional Stats
        TotalLinesText.Text = analytics.TotalLines.ToString("N0");

        if (analytics.EarliestCapture.HasValue && analytics.LatestCapture.HasValue)
        {
            DateRangeText.Text = $"{analytics.EarliestCapture:MMM d, yyyy} - {analytics.LatestCapture:MMM d, yyyy}";
        }
        else
        {
            DateRangeText.Text = "N/A";
        }

        if (analytics.EngineUsage.Count > 0)
        {
            var engines = string.Join(", ", analytics.EngineUsage.Select(e => $"{e.Key} ({e.Value})"));
            EngineText.Text = engines;
        }
        else
        {
            EngineText.Text = "N/A";
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Close();
        }
    }
}
