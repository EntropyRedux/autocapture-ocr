namespace AutoCaptureOCR.Core.Models;

/// <summary>
/// Discriminator for the kind of data a capture source produces.
/// Used to route payloads to the correct processing pipeline.
/// </summary>
public enum CaptureSourceType
{
    /// <summary>
    /// User-triggered screenshot (region or fullscreen).
    /// Produces a single <see cref="CapturePayload"/> per invocation.
    /// </summary>
    Snapshot,

    /// <summary>
    /// Continuous structured-text stream from DOM or UIA extraction (ChatCapture).
    /// Produces <see cref="CapturePayload"/> items with <see cref="CapturePayload.Turns"/>.
    /// </summary>
    LiveTextStream,

    /// <summary>
    /// Continuous video frame stream with optional real-time OCR.
    /// Produces <see cref="CapturePayload"/> items with <see cref="CapturePayload.Frame"/>.
    /// </summary>
    VideoStream
}
