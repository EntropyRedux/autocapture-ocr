using System.IO;
using System.Windows;
using System.Windows.Input;
using AutoCaptureOCR.Core.Export;
using AutoCaptureOCR.Core.Interfaces;
using AutoCaptureOCR.Core.Models;
using Microsoft.Win32;

namespace AutoCaptureOCR.App.Views;

public partial class ExportDialog : Window
{
    private readonly Project? project;
    private readonly CaptureSession? session;
    private readonly bool isProjectExport;

    public ExportDialog(Project project)
    {
        InitializeComponent();
        this.project = project ?? throw new ArgumentNullException(nameof(project));
        this.isProjectExport = true;

        var totalCaptures = project.Sessions.Sum(s => s.Captures.Count);
        ExportInfoTextBlock.Text = $"Project: {project.Name} • {project.Sessions.Count} sessions • {totalCaptures} captures";

        UpdatePreview();
        AttachEventHandlers();
    }

    public ExportDialog(CaptureSession session, string projectName)
    {
        InitializeComponent();
        this.session = session ?? throw new ArgumentNullException(nameof(session));
        this.isProjectExport = false;

        ExportInfoTextBlock.Text = $"Session: {session.Name} (Project: {projectName}) • {session.Captures.Count} captures";

        UpdatePreview();
        AttachEventHandlers();
    }

    private void AttachEventHandlers()
    {
        JsonRadioButton.Checked += (s, e) => UpdatePreview();
        CsvRadioButton.Checked += (s, e) => UpdatePreview();
        IncludeOCRCheckBox.Checked += (s, e) => UpdatePreview();
        IncludeOCRCheckBox.Unchecked += (s, e) => UpdatePreview();
        IncludeBoundingBoxesCheckBox.Checked += (s, e) => UpdatePreview();
        IncludeBoundingBoxesCheckBox.Unchecked += (s, e) => UpdatePreview();
        IncludeMetadataCheckBox.Checked += (s, e) => UpdatePreview();
        IncludeMetadataCheckBox.Unchecked += (s, e) => UpdatePreview();
        IncludeThumbnailsCheckBox.Checked += (s, e) => UpdatePreview();
        IncludeThumbnailsCheckBox.Unchecked += (s, e) => UpdatePreview();
        CompressOutputCheckBox.Checked += (s, e) => UpdatePreview();
        CompressOutputCheckBox.Unchecked += (s, e) => UpdatePreview();
        OcrTextFormatComboBox.SelectionChanged += (s, e) => UpdatePreview();
    }

    private void FormatRadioButton_Checked(object sender, RoutedEventArgs e)
    {
        if (OcrTextFormatPanel == null) return;

        // Show OCR text format options only when CSV is selected
        OcrTextFormatPanel.Visibility = CsvRadioButton.IsChecked == true
            ? Visibility.Visible
            : Visibility.Collapsed;

        UpdatePreview();
    }

    private void UpdatePreview()
    {
        var options = GetExportOptions();
        var format = options.Format == ExportFormat.JSON ? "JSON" : "CSV";
        var compression = options.CompressOutput ? " (compressed)" : "";

        var features = new List<string>();
        if (options.IncludeOCRResults)
        {
            var ocrText = "OCR results";
            if (options.Format == ExportFormat.CSV)
            {
                var formatName = options.OcrTextFormat switch
                {
                    OcrTextFormat.Continuous => "continuous",
                    OcrTextFormat.Lines => "lines",
                    OcrTextFormat.Structured => "structured",
                    OcrTextFormat.Json => "json",
                    _ => "continuous"
                };
                ocrText += $" ({formatName})";
            }
            features.Add(ocrText);
        }
        if (options.IncludeBoundingBoxes && options.Format == ExportFormat.JSON) features.Add("bounding boxes");
        if (options.IncludeMetadata) features.Add("metadata");
        if (options.IncludeThumbnails && options.Format == ExportFormat.JSON) features.Add("thumbnails");

        var captureCount = isProjectExport
            ? project!.Sessions.Sum(s => s.Captures.Count)
            : session!.Captures.Count;

        PreviewTextBlock.Text = $"Format: {format}{compression}\n" +
                                $"Captures: {captureCount}\n" +
                                $"Includes: {string.Join(", ", features)}";
    }

    private ExportOptions GetExportOptions()
    {
        // Parse OCR text format from combo box selection
        var ocrTextFormat = OcrTextFormatComboBox.SelectedIndex switch
        {
            0 => OcrTextFormat.Continuous,
            1 => OcrTextFormat.Lines,
            2 => OcrTextFormat.Structured,
            3 => OcrTextFormat.Json,
            _ => OcrTextFormat.Continuous
        };

        return new ExportOptions
        {
            Format = JsonRadioButton.IsChecked == true ? ExportFormat.JSON : ExportFormat.CSV,
            IncludeOCRResults = IncludeOCRCheckBox.IsChecked == true,
            IncludeBoundingBoxes = IncludeBoundingBoxesCheckBox.IsChecked == true,
            IncludeMetadata = IncludeMetadataCheckBox.IsChecked == true,
            IncludeThumbnails = IncludeThumbnailsCheckBox.IsChecked == true,
            CompressOutput = CompressOutputCheckBox.IsChecked == true,
            OcrTextFormat = ocrTextFormat
        };
    }

    private async void ExportButton_Click(object sender, RoutedEventArgs e)
    {
        var options = GetExportOptions();
        IExporter exporter = options.Format == ExportFormat.JSON
            ? new JsonExporter()
            : new CsvExporter();

        // Show save file dialog
        var saveDialog = new SaveFileDialog
        {
            Filter = options.Format == ExportFormat.JSON
                ? "JSON Files (*.json)|*.json|Compressed JSON (*.json.gz)|*.json.gz"
                : "CSV Files (*.csv)|*.csv",
            FileName = isProjectExport
                ? $"{project!.Name}_{DateTime.Now:yyyyMMdd_HHmmss}"
                : $"{session!.Name}_{DateTime.Now:yyyyMMdd_HHmmss}",
            DefaultExt = exporter.FileExtension
        };

        if (saveDialog.ShowDialog() != true)
            return;

        // Disable UI during export
        ExportButton.IsEnabled = false;
        CancelButton.IsEnabled = false;
        StatusTextBlock.Text = "Exporting...";

        try
        {
            ExportResult result;
            if (isProjectExport)
            {
                result = await exporter.ExportProjectAsync(project!, saveDialog.FileName, options);
            }
            else
            {
                result = await exporter.ExportSessionAsync(session!, saveDialog.FileName, options);
            }

            if (result.Success)
            {
                var sizeKb = result.FileSizeBytes / 1024.0;
                var message = $"Export completed successfully!\n\n" +
                              $"File: {Path.GetFileName(result.FilePath)}\n" +
                              $"Captures: {result.CapturesExported}\n" +
                              $"Size: {sizeKb:F2} KB\n" +
                              $"Duration: {result.Duration.TotalSeconds:F2}s";

                if (result.Warnings.Count > 0)
                {
                    message += $"\n\nWarnings:\n{string.Join("\n", result.Warnings)}";
                }

                MessageBox.Show(message, "Export Successful", MessageBoxButton.OK, MessageBoxImage.Information);
                DialogResult = true;
                Close();
            }
            else
            {
                MessageBox.Show($"Export failed:\n\n{result.ErrorMessage}",
                    "Export Error", MessageBoxButton.OK, MessageBoxImage.Error);
                StatusTextBlock.Text = "Export failed";
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Export error:\n\n{ex.Message}",
                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            StatusTextBlock.Text = "Export failed";
        }
        finally
        {
            ExportButton.IsEnabled = true;
            CancelButton.IsEnabled = true;
        }
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            DialogResult = false;
            Close();
        }
    }
}
