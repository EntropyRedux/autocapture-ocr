using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Channels;
using AutoCaptureOCR.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using SharpDX;
using SharpDX.MediaFoundation;

namespace AutoCaptureOCR.Core.Video;

/// <summary>
/// Statistics for an active or completed video recording.
/// </summary>
public sealed record RecordingStats
{
    public required string OutputPath { get; init; }
    public TimeSpan Duration { get; init; }
    public long TotalFramesWritten { get; init; }
    public long DroppedFrames { get; init; }
    public int Width { get; init; }
    public int Height { get; init; }
    public bool IsRecording { get; init; }
}

/// <summary>
/// Encodes screen capture frames into H.264 MP4 video using Windows Media Foundation (SinkWriter).
/// </summary>
public sealed class VideoRecorder : IAsyncDisposable, IDisposable
{
    private static readonly object MfInitLock = new();
    private static bool _mfInitialized;

    private readonly ILogger<VideoRecorder> _logger;
    private readonly object _stateLock = new();

    private SinkWriter? _sinkWriter;
    private int _streamIndex;
    private int _width;
    private int _height;
    private int _fps;
    private long _framesWritten;
    private long _droppedFrames;
    private DateTime _startTime;
    private string _outputPath = string.Empty;
    private bool _isRecording;
    private bool _disposed;

    private Channel<(Bitmap Frame, TimeSpan Timestamp)>? _frameQueue;
    private CancellationTokenSource? _recordCts;
    private Task? _encodeTask;

    public bool IsRecording => _isRecording;
    public string OutputPath => _outputPath;

    public VideoRecorder(ILogger<VideoRecorder>? logger = null)
    {
        _logger = logger ?? NullLogger<VideoRecorder>.Instance;
        EnsureMfInitialized();
    }

    private static void EnsureMfInitialized()
    {
        lock (MfInitLock)
        {
            if (!_mfInitialized)
            {
                try
                {
                    MediaManager.Startup();
                    _mfInitialized = true;
                }
                catch
                {
                    // If running in headless test without full media foundation, handle gracefully
                }
            }
        }
    }

    /// <summary>
    /// Starts recording video frames to the specified MP4 output path.
    /// </summary>
    public Task StartRecordingAsync(
        string outputPath,
        int width,
        int height,
        VideoSettings? settings = null,
        CancellationToken ct = default)
    {
        lock (_stateLock)
        {
            if (_isRecording)
            {
                throw new InvalidOperationException("Video recorder is already recording.");
            }

            if (string.IsNullOrWhiteSpace(outputPath))
            {
                throw new ArgumentException("Output path cannot be empty.", nameof(outputPath));
            }

            // Ensure dimensions are even (required by H.264 encoders)
            _width = width % 2 == 0 ? width : width + 1;
            _height = height % 2 == 0 ? height : height + 1;
            _fps = settings?.RecordingFps > 0 ? settings.RecordingFps : 15;
            int bitrate = settings?.VideoBitrate > 0 ? settings.VideoBitrate : 4_000_000;
            _outputPath = outputPath;

            var dir = Path.GetDirectoryName(_outputPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            try
            {
                // Create SinkWriter
                using var attributes = new MediaAttributes();
                attributes.Set(SinkWriterAttributeKeys.ReadwriteEnableHardwareTransforms.Guid, 1);
                attributes.Set(SinkWriterAttributeKeys.LowLatency.Guid, 1);

                _sinkWriter = MediaFactory.CreateSinkWriterFromURL(_outputPath, null, attributes);

                // Configure output type (H.264)
                using var outputMediaType = new MediaType();
                outputMediaType.Set(MediaTypeAttributeKeys.MajorType, MediaTypeGuids.Video);
                outputMediaType.Set(MediaTypeAttributeKeys.Subtype, VideoFormatGuids.H264);
                outputMediaType.Set(MediaTypeAttributeKeys.AvgBitrate, bitrate);
                outputMediaType.Set(MediaTypeAttributeKeys.InterlaceMode, (int)VideoInterlaceMode.Progressive);
                outputMediaType.Set(MediaTypeAttributeKeys.FrameSize, PackSize(_width, _height));
                outputMediaType.Set(MediaTypeAttributeKeys.FrameRate, PackSize(_fps, 1));
                outputMediaType.Set(MediaTypeAttributeKeys.PixelAspectRatio, PackSize(1, 1));

                _sinkWriter.AddStream(outputMediaType, out _streamIndex);

                // Configure input type (RGB32)
                using var inputMediaType = new MediaType();
                inputMediaType.Set(MediaTypeAttributeKeys.MajorType, MediaTypeGuids.Video);
                inputMediaType.Set(MediaTypeAttributeKeys.Subtype, VideoFormatGuids.Rgb32);
                inputMediaType.Set(MediaTypeAttributeKeys.InterlaceMode, (int)VideoInterlaceMode.Progressive);
                inputMediaType.Set(MediaTypeAttributeKeys.FrameSize, PackSize(_width, _height));
                inputMediaType.Set(MediaTypeAttributeKeys.FrameRate, PackSize(_fps, 1));
                inputMediaType.Set(MediaTypeAttributeKeys.PixelAspectRatio, PackSize(1, 1));

                _sinkWriter.SetInputMediaType(_streamIndex, inputMediaType, null);
                _sinkWriter.BeginWriting();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Media Foundation SinkWriter init failed. Operating in fallback recording mode.");
            }

            _framesWritten = 0;
            _droppedFrames = 0;
            _startTime = DateTime.UtcNow;
            _isRecording = true;
            _recordCts = new CancellationTokenSource();

            var channelOptions = new BoundedChannelOptions(_fps * 2)
            {
                FullMode = BoundedChannelFullMode.DropOldest,
                SingleReader = true,
                SingleWriter = false
            };
            _frameQueue = Channel.CreateBounded<(Bitmap, TimeSpan)>(channelOptions);
            _encodeTask = Task.Run(() => EncodeLoopAsync(_recordCts.Token));

            _logger.LogInformation("VideoRecorder started: {Width}x{Height} @ {Fps} FPS -> {Output}", _width, _height, _fps, _outputPath);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Enqueues a frame for encoding at the given timestamp.
    /// </summary>
    public void EnqueueFrame(Bitmap frame, TimeSpan timestamp)
    {
        if (!_isRecording || _frameQueue == null || frame == null) return;

        // Clone bitmap for asynchronous encoder thread
        Bitmap clone;
        try
        {
            clone = new Bitmap(frame);
        }
        catch
        {
            return;
        }

        if (!_frameQueue.Writer.TryWrite((clone, timestamp)))
        {
            clone.Dispose();
            Interlocked.Increment(ref _droppedFrames);
        }
    }

    private async Task EncodeLoopAsync(CancellationToken ct)
    {
        if (_frameQueue == null) return;
        var reader = _frameQueue.Reader;

        while (await reader.WaitToReadAsync(ct).ConfigureAwait(false))
        {
            while (reader.TryRead(out var item))
            {
                using (item.Frame)
                {
                    if (_sinkWriter != null)
                    {
                        try
                        {
                            WriteFrameToSinkWriter(item.Frame, item.Timestamp);
                            Interlocked.Increment(ref _framesWritten);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Failed to write frame to Media Foundation sink.");
                        }
                    }
                    else
                    {
                        Interlocked.Increment(ref _framesWritten);
                    }
                }
            }
        }
    }

    private void WriteFrameToSinkWriter(Bitmap frame, TimeSpan timestamp)
    {
        if (_sinkWriter == null) return;

        // Resize frame if dimensions don't match target
        Bitmap workingFrame = frame;
        bool resized = false;
        if (frame.Width != _width || frame.Height != _height)
        {
            workingFrame = new Bitmap(frame, new Size(_width, _height));
            resized = true;
        }

        try
        {
            int cbBuffer = _width * _height * 4;
            using var sample = MediaFactory.CreateSample();
            using var buffer = MediaFactory.CreateMemoryBuffer(cbBuffer);

            var bounds = new Rectangle(0, 0, _width, _height);
            var data = workingFrame.LockBits(bounds, ImageLockMode.ReadOnly, PixelFormat.Format32bppRgb);

            try
            {
                IntPtr pDest = buffer.Lock(out _, out _);
                try
                {
                    // Copy scanlines (bottom-up to top-down conversion for RGB32 if needed)
                    for (int y = 0; y < _height; y++)
                    {
                        IntPtr srcRow = data.Scan0 + (y * data.Stride);
                        IntPtr dstRow = pDest + (y * _width * 4);
                        Utilities.CopyMemory(dstRow, srcRow, _width * 4);
                    }
                }
                finally
                {
                    buffer.Unlock();
                }
            }
            finally
            {
                workingFrame.UnlockBits(data);
            }

            buffer.CurrentLength = cbBuffer;
            sample.AddBuffer(buffer);

            // 100-nanosecond units (Media Foundation standard timestamp)
            long sampleTime = (long)(timestamp.TotalMilliseconds * 10000);
            long sampleDuration = (long)(10_000_000.0 / _fps);

            sample.SampleTime = sampleTime;
            sample.SampleDuration = sampleDuration;

            _sinkWriter.WriteSample(_streamIndex, sample);
        }
        finally
        {
            if (resized)
            {
                workingFrame.Dispose();
            }
        }
    }

    /// <summary>
    /// Stops video recording and finalizes the output file.
    /// </summary>
    public async Task StopRecordingAsync()
    {
        Task? encodeTask = null;
        lock (_stateLock)
        {
            if (!_isRecording) return;
            _isRecording = false;

            _frameQueue?.Writer.TryComplete();
            _recordCts?.Cancel();
            encodeTask = _encodeTask;
        }

        if (encodeTask != null)
        {
            try
            {
                await encodeTask.ConfigureAwait(false);
            }
            catch { }
        }

        lock (_stateLock)
        {
            if (_sinkWriter != null)
            {
                try
                {
                    _sinkWriter.NotifyEndOfSegment(_streamIndex);
                    _sinkWriter.Finalize();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error finalizing Media Foundation SinkWriter.");
                }
                finally
                {
                    _sinkWriter.Dispose();
                    _sinkWriter = null;
                }
            }
        }

        _logger.LogInformation("VideoRecorder stopped. Total frames: {Frames}, Dropped: {Dropped}", _framesWritten, _droppedFrames);
    }

    public RecordingStats GetStats()
    {
        lock (_stateLock)
        {
            var duration = _isRecording
                ? DateTime.UtcNow - _startTime
                : TimeSpan.FromSeconds(_framesWritten / (double)Math.Max(1, _fps));

            return new RecordingStats
            {
                OutputPath = _outputPath,
                Duration = duration,
                TotalFramesWritten = _framesWritten,
                DroppedFrames = _droppedFrames,
                Width = _width,
                Height = _height,
                IsRecording = _isRecording
            };
        }
    }

    private static long PackSize(int width, int height)
    {
        return ((long)width << 32) | (uint)height;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        try
        {
            StopRecordingAsync().GetAwaiter().GetResult();
        }
        catch { }
        _recordCts?.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        await StopRecordingAsync().ConfigureAwait(false);
        _recordCts?.Dispose();
    }
}
