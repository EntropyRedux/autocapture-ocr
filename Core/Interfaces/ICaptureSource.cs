using System.Drawing;
using System.Runtime.CompilerServices;
using AutoCaptureOCR.Core.Models;

namespace AutoCaptureOCR.Core.Interfaces;

/// <summary>
/// Unified abstraction for all capture sources.
/// Snapshot sources produce discrete items on demand;
/// stream sources produce continuous flows via async enumerable.
/// </summary>
public interface ICaptureSource : IAsyncDisposable
{
    /// <summary>
    /// Human-readable engine name (e.g. "Windows OCR", "DOM Extension", "UI Automation").
    /// </summary>
    string SourceName { get; }

    /// <summary>
    /// Discriminator for the type of capture this source produces.
    /// </summary>
    CaptureSourceType SourceType { get; }

    /// <summary>
    /// Whether this source is currently running (started and not stopped).
    /// </summary>
    bool IsRunning { get; }

    /// <summary>
    /// Checks whether this capture source is available on the current system
    /// (e.g. WGC requires Windows 10 1903+, UIA requires the target window to exist).
    /// </summary>
    Task<bool> IsAvailableAsync(CancellationToken ct = default);

    /// <summary>
    /// Initializes and starts the capture source with the given options.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown if already running.</exception>
    Task StartAsync(CaptureSourceOptions options, CancellationToken ct = default);

    /// <summary>
    /// Gracefully stops the capture source and releases resources.
    /// Safe to call if not running (no-op).
    /// </summary>
    Task StopAsync(CancellationToken ct = default);

    /// <summary>
    /// For snapshot sources: captures a single frame/text extraction on demand.
    /// For stream sources: captures a single snapshot of the current state.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown if the source does not support on-demand capture.
    /// </exception>
    Task<CapturePayload> CaptureOnceAsync(CancellationToken ct = default);

    /// <summary>
    /// For stream sources: returns an async enumerable of payloads as they arrive.
    /// The enumerable completes when <see cref="StopAsync"/> is called or the
    /// <paramref name="ct"/> is cancelled.
    /// For snapshot-only sources: yields nothing (empty enumerable).
    /// </summary>
    IAsyncEnumerable<CapturePayload> GetStreamAsync(CancellationToken ct = default);
}
