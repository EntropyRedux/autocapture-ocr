using System;
using System.Drawing;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace AutoCaptureOCR.App.Views;

public partial class RegionCaptureOverlay : Window
{
    private System.Windows.Point startPoint;
    private bool isSelecting = false;

    public Rectangle? SelectedRegion { get; private set; }

    public RegionCaptureOverlay()
    {
        InitializeComponent();

        // Use Windows Forms Screen API for accurate multi-monitor bounds
        // SystemParameters can be incorrect with DPI scaling on multi-monitor setups
        var bounds = System.Windows.Forms.Screen.AllScreens
            .Aggregate(System.Drawing.Rectangle.Empty, (current, screen) =>
                System.Drawing.Rectangle.Union(current, screen.Bounds));

        // Get DPI scale to convert physical pixels to WPF logical pixels
        var source = PresentationSource.FromVisual(this);
        double dpiScaleX = 1.0;
        double dpiScaleY = 1.0;

        // Note: source will be null here because window isn't shown yet
        // So we'll use the system DPI settings
        using (var graphics = System.Drawing.Graphics.FromHwnd(IntPtr.Zero))
        {
            dpiScaleX = graphics.DpiX / 96.0;
            dpiScaleY = graphics.DpiY / 96.0;
        }

        // Set window to cover all screens (convert physical to logical pixels)
        Left = bounds.Left / dpiScaleX;
        Top = bounds.Top / dpiScaleY;
        Width = bounds.Width / dpiScaleX;
        Height = bounds.Height / dpiScaleY;
    }

    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        startPoint = e.GetPosition(SelectionCanvas);
        isSelecting = true;

        SelectionRectangle.Visibility = Visibility.Visible;
        DimensionLabel.Visibility = Visibility.Visible;

        Canvas.SetLeft(SelectionRectangle, startPoint.X);
        Canvas.SetTop(SelectionRectangle, startPoint.Y);
        SelectionRectangle.Width = 0;
        SelectionRectangle.Height = 0;
    }

    private void Window_MouseMove(object sender, MouseEventArgs e)
    {
        if (!isSelecting) return;

        var currentPoint = e.GetPosition(SelectionCanvas);

        var x = Math.Min(startPoint.X, currentPoint.X);
        var y = Math.Min(startPoint.Y, currentPoint.Y);
        var width = Math.Abs(currentPoint.X - startPoint.X);
        var height = Math.Abs(currentPoint.Y - startPoint.Y);

        Canvas.SetLeft(SelectionRectangle, x);
        Canvas.SetTop(SelectionRectangle, y);
        SelectionRectangle.Width = width;
        SelectionRectangle.Height = height;

        // Update dimension label
        DimensionText.Text = $"{(int)width} × {(int)height}";
        Canvas.SetLeft(DimensionLabel, x + width + 10);
        Canvas.SetTop(DimensionLabel, y);
    }

    private void Window_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!isSelecting) return;

        CompleteSelection();
    }

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            DialogResult = false;
            Close();
        }
        else if (e.Key == Key.Enter && isSelecting)
        {
            CompleteSelection();
        }
    }

    private void CompleteSelection()
    {
        if (SelectionRectangle.Width < 10 || SelectionRectangle.Height < 10)
        {
            MessageBox.Show("Selection too small. Minimum 10x10 pixels.", "Invalid Selection",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var x = (int)Canvas.GetLeft(SelectionRectangle);
        var y = (int)Canvas.GetTop(SelectionRectangle);
        var width = (int)SelectionRectangle.Width;
        var height = (int)SelectionRectangle.Height;

        // CRITICAL FIX for multi-monitor:
        // The window's Left/Top give us the actual window position
        // Canvas coordinates are relative to the window
        // So we need: WindowLeft + CanvasX to get absolute screen coordinates
        var screenX = (int)(this.Left + x);
        var screenY = (int)(this.Top + y);

        // Get DPI scaling information
        var source = PresentationSource.FromVisual(this);
        double dpiScaleX = 1.0;
        double dpiScaleY = 1.0;

        if (source != null)
        {
            dpiScaleX = source.CompositionTarget.TransformToDevice.M11;
            dpiScaleY = source.CompositionTarget.TransformToDevice.M22;
        }

        // Convert WPF logical pixels to physical screen pixels
        var physicalScreenX = (int)((this.Left + x) * dpiScaleX);
        var physicalScreenY = (int)((this.Top + y) * dpiScaleY);
        var physicalWidth = (int)(width * dpiScaleX);
        var physicalHeight = (int)(height * dpiScaleY);

        SelectedRegion = new Rectangle(physicalScreenX, physicalScreenY, physicalWidth, physicalHeight);
        DialogResult = true;
        Close();
    }
}
