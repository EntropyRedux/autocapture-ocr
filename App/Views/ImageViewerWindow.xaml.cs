using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using AutoCaptureOCR.Core.Models;
using AutoCaptureOCR.Core.Services;

namespace AutoCaptureOCR.App.Views;

public partial class ImageViewerWindow : Window
{
    private readonly List<ScreenCapture> captures;
    private readonly MetadataService? metadataService;
    private int currentIndex;

    public ImageViewerWindow(List<ScreenCapture> captures, int initialIndex = 0, MetadataService? metadataService = null)
    {
        InitializeComponent();

        this.captures = captures ?? throw new ArgumentNullException(nameof(captures));
        this.metadataService = metadataService;
        this.currentIndex = initialIndex;

        if (captures.Count == 0)
        {
            throw new ArgumentException("Captures list cannot be empty", nameof(captures));
        }

        LoadCapture();
    }

    private void LoadCapture()
    {
        var capture = captures[currentIndex];

        // Update header
        FileNameTextBlock.Text = Path.GetFileName(capture.FilePath);
        MetadataTextBlock.Text = $"Captured: {capture.Timestamp:yyyy-MM-dd HH:mm:ss} • Sequence: {capture.SequenceNumber} • Status: {capture.Status}";

        // Load image
        if (File.Exists(capture.FilePath))
        {
            try
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.UriSource = new Uri(capture.FilePath, UriKind.Absolute);
                bitmap.EndInit();
                bitmap.Freeze();
                CaptureImage.Source = bitmap;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load image: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        else
        {
            CaptureImage.Source = null;
            MessageBox.Show($"Image file not found: {capture.FilePath}", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        // Load OCR text
        if (capture.OCRResult != null)
        {
            OcrTextBox.Text = capture.OCRResult.Text;

            var wordCount = capture.OCRResult.Text.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
            var lineCount = capture.OCRResult.Lines?.Count ?? 0;
            OcrStatsTextBlock.Text = $"Words: {wordCount} • Lines: {lineCount} • Confidence: {capture.OCRResult.Confidence:P1}";
        }
        else
        {
            OcrTextBox.Text = capture.Status == CaptureStatus.Processing
                ? "OCR processing in progress..."
                : "No OCR data available";
            OcrStatsTextBlock.Text = "";
        }

        // Update navigation buttons
        PreviousButton.IsEnabled = currentIndex > 0;
        NextButton.IsEnabled = currentIndex < captures.Count - 1;
    }

    private void PreviousButton_Click(object sender, RoutedEventArgs e)
    {
        if (currentIndex > 0)
        {
            currentIndex--;
            LoadCapture();
        }
    }

    private void NextButton_Click(object sender, RoutedEventArgs e)
    {
        if (currentIndex < captures.Count - 1)
        {
            currentIndex++;
            LoadCapture();
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void EditMetadataButton_Click(object sender, RoutedEventArgs e)
    {
        if (metadataService == null)
        {
            MessageBox.Show("Metadata service is not available", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var capture = captures[currentIndex];
        var dialog = new MetadataEditorDialog(metadataService, capture);
        dialog.Owner = this;

        if (dialog.ShowDialog() == true && dialog.Result != null)
        {
            capture.TemplateMetadata = dialog.Result;
            MessageBox.Show("Metadata saved successfully", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private void CopyButton_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(OcrTextBox.Text))
        {
            try
            {
                Clipboard.SetText(OcrTextBox.Text);
                OcrStatsTextBlock.Text += " • Copied!";

                // Reset message after 2 seconds
                Task.Delay(2000).ContinueWith(_ =>
                {
                    Dispatcher.Invoke(() =>
                    {
                        var capture = captures[currentIndex];
                        if (capture.OCRResult != null)
                        {
                            var wordCount = capture.OCRResult.Text.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
                            var lineCount = capture.OCRResult.Lines?.Count ?? 0;
                            OcrStatsTextBlock.Text = $"Words: {wordCount} • Lines: {lineCount} • Confidence: {capture.OCRResult.Confidence:P1}";
                        }
                    });
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to copy to clipboard: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Escape:
                Close();
                break;
            case Key.Left:
                if (PreviousButton.IsEnabled)
                    PreviousButton_Click(this, new RoutedEventArgs());
                break;
            case Key.Right:
                if (NextButton.IsEnabled)
                    NextButton_Click(this, new RoutedEventArgs());
                break;
        }
    }
}
