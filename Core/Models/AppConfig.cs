using Newtonsoft.Json;

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
    public ChatCaptureSettings ChatCapture { get; set; } = new();
    public VideoSettings Video { get; set; } = new();

    public static AppConfig GetDefault()
    {
        return new AppConfig
        {
            App = new AppSettings
            {
                Version = "3.0.0",
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
            },
            ChatCapture = new ChatCaptureSettings(),
            Video = new VideoSettings()
        };
    }
}

public class AppSettings
{
    public string Version { get; set; } = "3.0.0";
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

/// <summary>
/// Configuration for the ChatCapture (passive chat archiving) subsystem.
/// </summary>
public class ChatCaptureSettings
{
    /// <summary>Whether the ChatCapture subsystem is enabled.</summary>
    public bool Enabled { get; set; } = false;

    /// <summary>Local WebSocket port for browser extension communication.</summary>
    public int WebSocketPort { get; set; } = 49281;

    /// <summary>Polling interval for UIA-based chat capture when events are unavailable.</summary>
    public TimeSpan UiaPollInterval { get; set; } = TimeSpan.FromSeconds(2);

    /// <summary>Whether to start the WebSocket bridge automatically on app launch.</summary>
    public bool AutoStartOnLaunch { get; set; } = false;

    /// <summary>
    /// Directory for transcript output. Empty string means use the project's save path.
    /// </summary>
    public string TranscriptOutputDir { get; set; } = string.Empty;

    /// <summary>Hostnames to monitor for chat activity via the browser extension.</summary>
    [JsonProperty(ObjectCreationHandling = ObjectCreationHandling.Replace)]
    public List<string> MonitoredHostnames { get; set; } = new() { "claude.ai", "chatgpt.com" };

    /// <summary>
    /// Auth token for WebSocket bridge security. Auto-generated on first launch.
    /// Never store captured content in this field.
    /// </summary>
    public string AuthToken { get; set; } = string.Empty;
}

/// <summary>
/// Configuration for real-time video capture and live OCR.
/// </summary>
public class VideoSettings
{
    /// <summary>Frames per second for OCR processing (not video recording FPS).</summary>
    public int OcrFrameRate { get; set; } = 1;

    /// <summary>Frames per second for video file recording.</summary>
    public int RecordingFps { get; set; } = 15;

    /// <summary>Video codec. Currently only H264 is supported (via Media Foundation).</summary>
    public string VideoCodec { get; set; } = "H264";

    /// <summary>Video bitrate in bits per second. Default: 4 Mbps.</summary>
    public int VideoBitrate { get; set; } = 4_000_000;

    /// <summary>Whether to skip OCR on frames that haven't visually changed.</summary>
    public bool EnableFrameDiffing { get; set; } = true;

    /// <summary>
    /// Minimum fraction of pixels that must change to trigger OCR.
    /// Range: 0.0 (always process) to 1.0 (never process). Default: 2%.
    /// </summary>
    public double FrameDiffThreshold { get; set; } = 0.02;

    /// <summary>
    /// Output directory for video recordings. Empty means use the project's save path.
    /// </summary>
    public string OutputDirectory { get; set; } = string.Empty;
}
