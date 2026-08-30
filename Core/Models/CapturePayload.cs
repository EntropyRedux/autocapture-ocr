using System.Drawing;

namespace AutoCaptureOCR.Core.Models;

/// <summary>
/// Unified capture payload that can carry a bitmap frame, structured chat turns,
/// raw text, or any combination. Acts as the common currency between all capture
/// sources and processing pipelines.
/// </summary>
public sealed class CapturePayload
{
    /// <summary>Unique identifier for this payload.</summary>
    public Guid Id { get; init; } = Guid.NewGuid();

    /// <summary>Wall-clock timestamp when this payload was created.</summary>
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;

    /// <summary>Which type of capture source produced this payload.</summary>
    public CaptureSourceType SourceType { get; init; }

    /// <summary>Name of the engine that produced this payload (e.g. "WGC", "DOM Extension").</summary>
    public string SourceEngine { get; init; } = string.Empty;

    // ─── Pixel Data (snapshot / video frame / OCR fallback) ───────────────

    /// <summary>Captured bitmap frame. Null for pure text-stream payloads.</summary>
    public Bitmap? Frame { get; init; }

    /// <summary>Screen region that was captured. Null if not applicable.</summary>
    public Rectangle? Region { get; init; }

    // ─── Structured Text Data (DOM / UIA extraction) ─────────────────────

    /// <summary>
    /// Ordered list of chat turns extracted from a conversation interface.
    /// Null for pixel-only payloads (snapshots, video frames).
    /// </summary>
    public IReadOnlyList<ChatTurn>? Turns { get; init; }

    /// <summary>
    /// Raw text content. Used when structured turns are unavailable
    /// (e.g. OCR text, plain UIA text dump).
    /// </summary>
    public string? RawText { get; init; }

    // ─── Video-specific ──────────────────────────────────────────────────

    /// <summary>Offset from the start of the video recording. Null for non-video payloads.</summary>
    public TimeSpan? VideoTimestamp { get; init; }

    /// <summary>Sequential frame number within the recording. Null for non-video payloads.</summary>
    public long? FrameNumber { get; init; }

    // ─── Computed Properties ─────────────────────────────────────────────

    /// <summary>True if this payload contains a bitmap frame.</summary>
    public bool HasImage => Frame is not null;

    /// <summary>True if this payload contains structured turns or raw text.</summary>
    public bool HasText => (Turns is not null && Turns.Count > 0) || !string.IsNullOrEmpty(RawText);
}
