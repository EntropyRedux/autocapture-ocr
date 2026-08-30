using System.Windows;
using AutoCaptureOCR.Core.Configuration;
using AutoCaptureOCR.Core.Models;

namespace AutoCaptureOCR.App.Views;

/// <summary>
/// Settings panel for ChatCapture configuration.
/// Placeholder for Phase 8 — wired to ConfigManager's existing API.
/// </summary>
public partial class ChatCaptureSettingsPanel : Window
{
    private readonly ConfigManager _configManager;
    private readonly AppConfig _config;

    public ChatCaptureSettingsPanel(ConfigManager? configManager = null)
    {
        InitializeComponent();
        _configManager = configManager ?? new ConfigManager();
        _config = _configManager.LoadConfig();
        LoadSettings();
    }

    private void LoadSettings()
    {
        var settings = _config.ChatCapture;

        EnabledCheckBox.IsChecked = settings.Enabled;
        PortTextBox.Text = settings.WebSocketPort.ToString();
        AutoStartCheckBox.IsChecked = settings.AutoStartOnLaunch;
        HostnamesTextBox.Text = string.Join(", ", settings.MonitoredHostnames);
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        var settings = _config.ChatCapture;

        settings.Enabled = EnabledCheckBox.IsChecked == true;
        if (int.TryParse(PortTextBox.Text.Trim(), out int port) && port > 1024 && port < 65535)
        {
            settings.WebSocketPort = port;
        }
        settings.AutoStartOnLaunch = AutoStartCheckBox.IsChecked == true;

        var hostnames = HostnamesTextBox.Text
            .Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(h => h.Trim())
            .Where(h => !string.IsNullOrEmpty(h))
            .ToList();

        if (hostnames.Count > 0)
        {
            settings.MonitoredHostnames = hostnames;
        }

        _configManager.SaveConfig(_config);

        DialogResult = true;
        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
