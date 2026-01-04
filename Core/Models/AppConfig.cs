namespace AutoCaptureOCR.Core.Models;

/// <summary>
/// Application configuration loaded from YAML
/// </summary>
public class AppConfig
{
    public AppSettings App { get; set; } = new();
    public CaptureSettings Capture { get; set; } = new();
    public OCRSettings OCR { get; set; } = new();
    public NamingSettings Naming { get; set; } = new();
    public ExportSettings Export { get; set; } = new();
    public UISettings UI { get; set; } = new();

    public static AppConfig GetDefault()
    {
        return new AppConfig
        {
            App = new AppSettings
            {
                Version = "2.0.0",
                StartupMode = "minimal"
            },
            Capture = new CaptureSettings
            {
                DefaultMode = "region",
                SaveDirectory = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
                    "Captures"
                ),
                AutoHideUI = true,
                HideDelayMs = 500,
                DefaultImageFormat = "PNG",
                JpegQuality = 95
            },
            OCR = new OCRSettings
            {
                DefaultEngine = "windows",
                AutoProcess = true,
                Languages = new List<string> { "en-US" },
                ConfidenceThreshold = 0.7,
                QueueDelay = 500
            },
            Naming = new NamingSettings
            {
                DefaultPattern = "capture_{session}_{timestamp}",
                TimestampFormat = "yyyyMMdd_HHmmss"
            },
            Export = new ExportSettings
            {
                OCRFormat = "json",
                SaveWithScreenshot = true
            },
            UI = new UISettings
            {
                ShowNotifications = true,
                NotificationDuration = 3000,
                Theme = "Dark"
            }
        };
    }
}

public class AppSettings
{
    public string Version { get; set; } = "2.0.0";
    public string StartupMode { get; set; } = "minimal";
    public string DefaultProjectPath { get; set; } = string.Empty;
}

public class CaptureSettings
{
    public string DefaultMode { get; set; } = "region";
    public string SaveDirectory { get; set; } = string.Empty;
    public bool AutoHideUI { get; set; } = true;
    public int HideDelayMs { get; set; } = 500;
    public string DefaultImageFormat { get; set; } = "PNG";
    public int JpegQuality { get; set; } = 95;
    public bool OrganizeByDate { get; set; } = false;
}

public class OCRSettings
{
    public string DefaultEngine { get; set; } = "windows";
    public bool AutoProcess { get; set; } = true;
    public List<string> Languages { get; set; } = new();
    public double ConfidenceThreshold { get; set; } = 0.7;
    public int QueueDelay { get; set; } = 500;
    public string DisplayMode { get; set; } = "continuous"; // "continuous", "lines", "structured", "json"
}

public class NamingSettings
{
    public string DefaultPattern { get; set; } = "capture_{session}_{timestamp}";
    public string TimestampFormat { get; set; } = "yyyyMMdd_HHmmss";
    public bool UseSmartFilenames { get; set; } = true;
    public int SmartFilenameMaxLength { get; set; } = 50;
    public string FallbackPattern { get; set; } = "capture_{timestamp}";
}

public class ExportSettings
{
    public string OCRFormat { get; set; } = "json";
    public bool SaveWithScreenshot { get; set; } = true;
}

public class UISettings
{
    public bool ShowNotifications { get; set; } = true;
    public int NotificationDuration { get; set; } = 3000;
    public string Theme { get; set; } = "Dark";
}
