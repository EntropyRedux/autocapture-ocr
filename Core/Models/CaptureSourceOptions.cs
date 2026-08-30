using System.Drawing;

namespace AutoCaptureOCR.Core.Models;

/// <summary>
/// Configuration options passed to an <see cref="Interfaces.ICaptureSource"/>
/// when starting a capture session.
/// </summary>
public sealed class CaptureSourceOptions
{
    /// <summary>
    /// Handle to the target window for WGC or UIA capture.
    /// <see cref="IntPtr.Zero"/> for extension-based (DOM) sources.
    /// </summary>
    public IntPtr TargetWindowHandle { get; init; }

    /// <summary>
    /// Hostname of the target web app (e.g. "claude.ai", "chatgpt.com").
    /// Used by the DOM capture engine to select the correct CSS selectors.
    /// Null for non-web sources.
    /// </summary>
    public string? TargetHostname { get; init; }

    /// <summary>
    /// Polling interval for sources that require polling (UIA fallback, health checks).
    /// Ignored by event-driven sources. Minimum recommended: 1 second.
    /// </summary>
    public TimeSpan PollInterval { get; init; } = TimeSpan.FromSeconds(2);

    /// <summary>
    /// Maximum frames per second to capture for video/OCR streams.
    /// Higher values increase CPU usage. Default: 1 FPS for OCR, up to 30 for video recording.
    /// </summary>
    public int MaxFrameRate { get; init; } = 1;

    /// <summary>
    /// Whether to enable frame-difference filtering to skip unchanged frames.
    /// Dramatically reduces OCR processing load. Recommended: true.
    /// </summary>
    public bool EnableFrameDiffing { get; init; } = true;

    /// <summary>
    /// Minimum fraction of pixels that must change between consecutive frames
    /// to trigger OCR processing. Range: 0.0 (always process) to 1.0 (never process).
    /// Default: 0.02 (2% of pixels changed).
    /// </summary>
    public double FrameDiffThreshold { get; init; } = 0.02;

    /// <summary>
    /// Optional sub-region of the target window to capture.
    /// Null means capture the full window client area.
    /// </summary>
    public Rectangle? CaptureRegion { get; init; }
}
