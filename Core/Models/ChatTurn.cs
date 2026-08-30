namespace AutoCaptureOCR.Core.Models;

/// <summary>
/// Represents a single conversational turn extracted from a chat interface.
/// Immutable record type for thread-safe sharing across pipeline stages.
/// </summary>
public sealed record ChatTurn
{
    /// <summary>
    /// The role of the message author. Typically "user", "assistant", or "system".
    /// </summary>
    public required string Role { get; init; }

    /// <summary>
    /// The message content in Markdown format.
    /// Code blocks, formatting, and structure are preserved from the source DOM/UIA.
    /// </summary>
    public required string Content { get; init; }

    /// <summary>
    /// Stable identifier from the source application (e.g. data-message-id attribute).
    /// Used for deduplication when available. Null if the source doesn't expose message IDs.
    /// </summary>
    public string? MessageId { get; init; }

    /// <summary>
    /// Zero-based index of this turn within the conversation, as ordered in the source UI.
    /// </summary>
    public int TurnIndex { get; init; }

    /// <summary>
    /// Timestamp when this turn was captured (not when the original message was sent).
    /// </summary>
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// True if the AI assistant is still generating this turn (streaming response).
    /// The dedup engine should treat streaming turns as mutable and update rather than append.
    /// </summary>
    public bool IsStreaming { get; init; }
}
