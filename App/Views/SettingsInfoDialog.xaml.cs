using System.Diagnostics;
using System.IO;
using System.Windows;

namespace AutoCaptureOCR.App.Views;

public partial class SettingsInfoDialog : Window
{
    private readonly string configPath;

    public SettingsInfoDialog()
    {
        InitializeComponent();

        var appDataPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "AutoCaptureOCR"
        );
        configPath = Path.Combine(appDataPath, "config.yaml");

        ConfigPathTextBox.Text = configPath;
    }

    private void CopyPath_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Clipboard.SetText(configPath);
            MessageBox.Show("Path copied to clipboard!", "Success",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to copy: {ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void OpenFolder_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var folder = Path.GetDirectoryName(configPath);
            if (Directory.Exists(folder))
            {
                Process.Start("explorer.exe", folder);
            }
            else
            {
                MessageBox.Show("Configuration folder does not exist yet.\nIt will be created when you first use the app.",
                    "Info", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to open folder: {ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
