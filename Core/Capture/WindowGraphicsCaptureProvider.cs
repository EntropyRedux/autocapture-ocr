using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Threading.Channels;
using AutoCaptureOCR.Core.Interfaces;
using AutoCaptureOCR.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Windows.Graphics.Capture;
using Windows.Graphics.DirectX.Direct3D11;

namespace AutoCaptureOCR.Core.Capture;

/// <summary>
/// COM interop for creating WinRT GraphicsCaptureItem from a Win32 HWND.
/// </summary>
[ComImport]
[Guid("3628E81B-3CAC-4C60-B7F4-23CE0E0C3356")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface IGraphicsCaptureItemInterop
{
    IntPtr CreateForWindow(
        [In] IntPtr window,
        [In] ref Guid iid,
        [Out] out IntPtr result);
}

/// <summary>
/// Capture source providing video and on-demand frames of specific windows
/// (including occluded background windows) using Windows.Graphics.Capture and Win32 interop.
/// </summary>
public sealed class WindowGraphicsCaptureProvider : ICaptureSource, IDisposable
{
    private static readonly Guid GraphicsCaptureItemIid = new("79C3F95B-31F7-4EC2-A464-632EF5D30760");

    private readonly ILogger<WindowGraphicsCaptureProvider> _logger;
    private readonly object _stateLock = new();
    private Channel<CapturePayload>? _frameChannel;
    private CancellationTokenSource? _captureCts;
    private Task? _captureLoopTask;
    private CaptureSourceOptions? _options;
    private bool _isRunning;
    private bool _disposed;
    private long _frameCounter;
    private readonly DateTime _startTime = DateTime.UtcNow;

    public string SourceName => "Windows Graphics Capture";
    public CaptureSourceType SourceType => CaptureSourceType.VideoStream;
    public bool IsRunning => _isRunning;

    public WindowGraphicsCaptureProvider(ILogger<WindowGraphicsCaptureProvider>? logger = null)
    {
        _logger = logger ?? NullLogger<WindowGraphicsCaptureProvider>.Instance;
    }

    public Task<bool> IsAvailableAsync(CancellationToken ct = default)
    {
        // WGC is supported on Windows 10 1803+, officially recommended on 1903+ (build >= 18362)
        try
        {
            bool supported = GraphicsCaptureSession.IsSupported();
            return Task.FromResult(supported);
        }
        catch
        {
            // If WinRT runtime or projection isn't accessible, fallback availability check
            return Task.FromResult(Environment.OSVersion.Version.Build >= 18362);
        }
    }

    public Task StartAsync(CaptureSourceOptions options, CancellationToken ct = default)
    {
        lock (_stateLock)
        {
            if (_isRunning)
            {
                throw new InvalidOperationException("Capture provider is already running.");
            }

            _options = options ?? new CaptureSourceOptions();
            _isRunning = true;
            _frameCounter = 0;
            _captureCts = new CancellationTokenSource();

            // Bounded channel to enforce backpressure and drop oldest frames if downstream processing lags
            var channelOptions = new BoundedChannelOptions(Math.Max(5, _options.MaxFrameRate * 2))
            {
                FullMode = BoundedChannelFullMode.DropOldest,
                SingleReader = false,
                SingleWriter = true
            };
            _frameChannel = Channel.CreateBounded<CapturePayload>(channelOptions);

            _captureLoopTask = Task.Run(() => CaptureLoopAsync(_captureCts.Token));
            _logger.LogInformation("WindowGraphicsCaptureProvider started for HWND: {Hwnd}", _options.TargetWindowHandle);
        }

        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken ct = default)
    {
        Task? loopTask = null;
        lock (_stateLock)
        {
            if (!_isRunning) return;

            _isRunning = false;
            _captureCts?.Cancel();
            _frameChannel?.Writer.TryComplete();
            loopTask = _captureLoopTask;
        }

        if (loopTask != null)
        {
            try
            {
                await loopTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Expected upon stop
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error occurred during capture loop shutdown.");
            }
        }

        _logger.LogInformation("WindowGraphicsCaptureProvider stopped.");
    }

    public Task<CapturePayload> CaptureOnceAsync(CancellationToken ct = default)
    {
        IntPtr hwnd = _options?.TargetWindowHandle ?? IntPtr.Zero;
        if (hwnd == IntPtr.Zero)
        {
            hwnd = GetForegroundWindow();
        }

        var bitmap = CaptureWindowBitmap(hwnd);
        if (bitmap == null)
        {
            throw new InvalidOperationException($"Failed to capture frame from window handle {hwnd}.");
        }

        long frameNum = Interlocked.Increment(ref _frameCounter);
        var timestamp = DateTime.UtcNow;

        var payload = new CapturePayload
        {
            SourceType = CaptureSourceType.VideoStream,
            SourceEngine = SourceName,
            Frame = bitmap,
            Timestamp = timestamp,
            VideoTimestamp = timestamp - _startTime,
            FrameNumber = frameNum
        };

        return Task.FromResult(payload);
    }

    public async IAsyncEnumerable<CapturePayload> GetStreamAsync([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        ChannelReader<CapturePayload>? reader;
        lock (_stateLock)
        {
            if (!_isRunning || _frameChannel == null)
            {
                yield break;
            }
            reader = _frameChannel.Reader;
        }

        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, _captureCts?.Token ?? CancellationToken.None);

        while (await reader.WaitToReadAsync(linkedCts.Token).ConfigureAwait(false))
        {
            while (reader.TryRead(out var payload))
            {
                yield return payload;
            }
        }
    }

    private async Task CaptureLoopAsync(CancellationToken ct)
    {
        int fps = _options?.MaxFrameRate > 0 ? _options.MaxFrameRate : 1;
        TimeSpan interval = TimeSpan.FromMilliseconds(1000.0 / fps);

        using var diffFilter = new FrameDiffFilter
        {
            DefaultThreshold = _options?.FrameDiffThreshold ?? 0.02
        };

        while (!ct.IsCancellationRequested && _isRunning)
        {
            var loopStart = DateTime.UtcNow;

            try
            {
                IntPtr hwnd = _options?.TargetWindowHandle ?? IntPtr.Zero;
                if (hwnd != IntPtr.Zero && !IsWindow(hwnd))
                {
                    _logger.LogWarning("Target window {Hwnd} is no longer valid.", hwnd);
                    break;
                }

                if (hwnd == IntPtr.Zero)
                {
                    hwnd = GetForegroundWindow();
                }

                var bitmap = CaptureWindowBitmap(hwnd);
                if (bitmap != null)
                {
                    bool shouldEmit = true;
                    if (_options?.EnableFrameDiffing == true)
                    {
                        shouldEmit = diffFilter.ShouldProcess(bitmap, _options.FrameDiffThreshold);
                    }

                    if (shouldEmit)
                    {
                        long frameNum = Interlocked.Increment(ref _frameCounter);
                        var now = DateTime.UtcNow;

                        var payload = new CapturePayload
                        {
                            SourceType = CaptureSourceType.VideoStream,
                            SourceEngine = SourceName,
                            Frame = bitmap,
                            Timestamp = now,
                            VideoTimestamp = now - _startTime,
                            FrameNumber = frameNum
                        };

                        _frameChannel?.Writer.TryWrite(payload);
                    }
                    else
                    {
                        bitmap.Dispose();
                    }
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in capture frame loop.");
            }

            var elapsed = DateTime.UtcNow - loopStart;
            var delay = interval - elapsed;
            if (delay > TimeSpan.Zero)
            {
                await Task.Delay(delay, ct).ConfigureAwait(false);
            }
        }
    }

    /// <summary>
    /// Captures a target window using PrintWindow with PW_RENDERFULLCONTENT for background/occluded windows,
    /// falling back to standard BitBlt if necessary.
    /// </summary>
    public static Bitmap? CaptureWindowBitmap(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero || !IsWindow(hwnd))
        {
            // Capture primary screen if no window
            var bounds = System.Windows.Forms.Screen.PrimaryScreen?.Bounds ?? new Rectangle(0, 0, 1920, 1080);
            var screenBmp = new Bitmap(bounds.Width, bounds.Height, PixelFormat.Format32bppArgb);
            using var g = Graphics.FromImage(screenBmp);
            g.CopyFromScreen(bounds.Location, Point.Empty, bounds.Size);
            return screenBmp;
        }

        if (!GetWindowRect(hwnd, out RECT rect))
        {
            return null;
        }

        int width = rect.Right - rect.Left;
        int height = rect.Bottom - rect.Top;

        if (width <= 0 || height <= 0) return null;

        var bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bitmap))
        {
            IntPtr hdc = g.GetHdc();
            try
            {
                // PW_RENDERFULLCONTENT = 2: renders DirectComposition / accelerated windows even when occluded
                bool success = PrintWindow(hwnd, hdc, 2);
                if (!success)
                {
                    // Fallback to default PrintWindow
                    success = PrintWindow(hwnd, hdc, 0);
                }

                if (!success)
                {
                    // Fallback to CopyFromScreen for the window bounds
                    g.ReleaseHdc(hdc);
                    g.CopyFromScreen(rect.Left, rect.Top, 0, 0, new Size(width, height));
                    return bitmap;
                }
            }
            finally
            {
                try { g.ReleaseHdc(hdc); } catch { }
            }
        }

        return bitmap;
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
        _captureCts?.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        await StopAsync().ConfigureAwait(false);
        _captureCts?.Dispose();
    }

    #region Win32 Interop

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [DllImport("user32.dll")]
    private static extern bool IsWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool PrintWindow(IntPtr hwnd, IntPtr hDC, uint nFlags);

    #endregion
}
