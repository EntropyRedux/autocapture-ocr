using System.Windows;

namespace AutoCaptureOCR.App.Views;

/// <summary>
/// Visual indicator overlay showing the locked region for continuous capture
/// </summary>
public partial class LockedRegionOverlay : Window
{
    public LockedRegionOverlay(int x, int y, int width, int height)
    {
        InitializeComponent();

        // Position and size the window to match the locked region
        Left = x;
        Top = y;
        Width = width;
        Height = height;
    }

    /// <summary>
    /// Update the overlay position and size
    /// </summary>
    public void UpdateRegion(int x, int y, int width, int height)
    {
        Left = x;
        Top = y;
        Width = width;
        Height = height;
    }
}
