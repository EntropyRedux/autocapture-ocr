using System.Drawing;
using System.IO;
using System.Threading.Channels;
using AutoCaptureOCR.Core.Capture;
using AutoCaptureOCR.Core.Interfaces;
using AutoCaptureOCR.Core.Models;
using AutoCaptureOCR.Core.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace AutoCaptureOCR.Core.Video;

/// <summary>
/// Orchestrates simultaneous video recording (MP4) and real-time frame-differenced OCR (Markdown index).
/// </summary>
public sealed class VideoOcrPipeline : IAsyncDisposable, IDisposable
{
    private readonly ICaptureSource _captureSource;
    private readonly IOCREngine _ocrEngine;
    private readonly ILogger<VideoOcrPipeline> _logger;
    private readonly object _stateLock = new();

    private VideoRecorder? _recorder;
    private TranscriptWriter? _transcriptWriter;
    private TextStitcher? _textStitcher;
    private FrameDiffFilter? _diffFilter;
    private CancellationTokenSource? _pipelineCts;
    private Task? _ocrProcessingTask;
    private bool _isRunning;
    private bool _disposed;

    public bool IsRunning => _isRunning;

    public VideoOcrPipeline(
        ICaptureSource? captureSource = null,
        IOCREngine? ocrEngine = null,
        ILogger<VideoOcrPipeline>? logger = null)
    {
        _captureSource = captureSource ?? new WindowGraphicsCaptureProvider();
        _ocrEngine = ocrEngine ?? new OCR.WindowsOCREngine();
        _logger = logger ?? NullLogger<VideoOcrPipeline>.Instance;
    }

    /// <summary>
    /// Starts the dual video recording + live OCR pipeline.
    /// </summary>
    public async Task StartAsync(
        IntPtr targetHwnd,
        string videoOutputPath,
        string transcriptOutputPath,
        VideoSettings? settings = null,
        CancellationToken ct = default)
    {
        lock (_stateLock)
        {
            if (_isRunning)
            {
                throw new InvalidOperationException("Video OCR pipeline is already running.");
            }

            _isRunning = true;
            _pipelineCts = new CancellationTokenSource();
        }

        var videoSettings = settings ?? new VideoSettings();

        // 1. Initialize Frame Diff Filter & Stitcher
        _diffFilter = new FrameDiffFilter
        {
            DefaultThreshold = videoSettings.FrameDiffThreshold
        };
        _textStitcher = new TextStitcher();

        // 2. Initialize Transcript Writer
        _transcriptWriter = new TranscriptWriter(
            transcriptOutputPath,
            title: $"Video OCR Index - {Path.GetFileName(videoOutputPath)}",
            source: "Video OCR Pipeline");
        await _transcriptWriter.EnsureInitializedAsync(ct).ConfigureAwait(false);

        // 3. Initialize & Start Capture Source
        var captureOptions = new CaptureSourceOptions
        {
            TargetWindowHandle = targetHwnd,
            MaxFrameRate = videoSettings.RecordingFps,
            EnableFrameDiffing = false // Capture all frames for video recording; diffing is done in OCR branch
        };
        await _captureSource.StartAsync(captureOptions, ct).ConfigureAwait(false);

        // Capture initial frame to determine video dimensions
        var initialPayload = await _captureSource.CaptureOnceAsync(ct).ConfigureAwait(false);
        int width = initialPayload.Frame?.Width ?? 1280;
        int height = initialPayload.Frame?.Height ?? 720;
        initialPayload.Frame?.Dispose();

        // 4. Initialize Video Recorder
        _recorder = new VideoRecorder();
        await _recorder.StartRecordingAsync(videoOutputPath, width, height, videoSettings, ct).ConfigureAwait(false);

        // 5. Start Background OCR Processing Loop
        _ocrProcessingTask = Task.Run(() => ProcessStreamAsync(_pipelineCts.Token, videoSettings));

        _logger.LogInformation("VideoOcrPipeline successfully started -> Video: {Video}, Transcript: {Transcript}", videoOutputPath, transcriptOutputPath);
    }

    private async Task ProcessStreamAsync(CancellationToken ct, VideoSettings settings)
    {
        var stream = _captureSource.GetStreamAsync(ct);
        DateTime lastOcrTime = DateTime.MinValue;
        TimeSpan minOcrInterval = TimeSpan.FromMilliseconds(1000.0 / Math.Max(1, settings.OcrFrameRate));

        try
        {
            await foreach (var payload in stream.WithCancellation(ct).ConfigureAwait(false))
            {
                if (payload.Frame == null) continue;

                var timestamp = payload.VideoTimestamp ?? (payload.Timestamp - DateTime.UtcNow);

                // Path 1: Enqueue for video encoding
                _recorder?.EnqueueFrame(payload.Frame, timestamp);

                // Path 2: Live OCR on keyframes
                var now = DateTime.UtcNow;
                if (now - lastOcrTime >= minOcrInterval)
                {
                    bool shouldOcr = true;
                    if (settings.EnableFrameDiffing && _diffFilter != null)
                    {
                        shouldOcr = _diffFilter.ShouldProcess(payload.Frame, settings.FrameDiffThreshold);
                    }

                    if (shouldOcr)
                    {
                        lastOcrTime = now;
                        await ProcessOcrFrameAsync(payload.Frame, payload.Timestamp, payload.VideoTimestamp, ct).ConfigureAwait(false);
                    }
                }

                payload.Frame.Dispose();
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in VideoOcrPipeline stream processing loop.");
        }
    }

    private async Task ProcessOcrFrameAsync(Bitmap frame, DateTime timestamp, TimeSpan? videoOffset, CancellationToken ct)
    {
        try
        {
            var ocrResult = await _ocrEngine.ProcessAsync(frame).ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(ocrResult.Text) && _textStitcher != null && _transcriptWriter != null)
            {
                var newLines = _textStitcher.StitchNewLines(ocrResult.Text);
                if (newLines.Count > 0)
                {
                    string joinedText = string.Join(Environment.NewLine, newLines);
                    await _transcriptWriter.AppendTimestampedTextAsync(timestamp, videoOffset, joinedText, ct).ConfigureAwait(false);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "OCR processing failed for video keyframe.");
        }
    }

    public async Task StopAsync()
    {
        Task? ocrTask = null;
        lock (_stateLock)
        {
            if (!_isRunning) return;
            _isRunning = false;

            _pipelineCts?.Cancel();
            ocrTask = _ocrProcessingTask;
        }

        if (ocrTask != null)
        {
            try
            {
                await ocrTask.ConfigureAwait(false);
            }
            catch { }
        }

        await _captureSource.StopAsync().ConfigureAwait(false);

        if (_recorder != null)
        {
            await _recorder.StopRecordingAsync().ConfigureAwait(false);
        }

        _logger.LogInformation("VideoOcrPipeline stopped.");
    }

    public RecordingStats? GetVideoStats()
    {
        return _recorder?.GetStats();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        try
        {
            StopAsync().GetAwaiter().GetResult();
        }
        catch { }

        _recorder?.Dispose();
        _transcriptWriter?.Dispose();
        _diffFilter?.Dispose();
        _pipelineCts?.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        await StopAsync().ConfigureAwait(false);

        if (_recorder != null) await _recorder.DisposeAsync().ConfigureAwait(false);
        if (_transcriptWriter != null) await _transcriptWriter.DisposeAsync().ConfigureAwait(false);
        _diffFilter?.Dispose();
        _pipelineCts?.Dispose();
    }
}
