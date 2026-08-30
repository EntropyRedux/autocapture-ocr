using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace AutoCaptureOCR.Core.Capture;

/// <summary>
/// Lightweight pixel-difference calculator that gates OCR processing by checking
/// if a new frame has changed significantly compared to the last processed frame.
/// Operates on downsampled 160x120 thumbnails for sub-millisecond execution.
/// </summary>
public sealed class FrameDiffFilter : IDisposable
{
    private const int ThumbnailWidth = 160;
    private const int ThumbnailHeight = 120;

    private readonly object _syncLock = new();
    private byte[]? _lastThumbnailBytes;
    private bool _disposed;

    /// <summary>
    /// Default difference threshold (e.g., 0.02 = 2% pixel change).
    /// </summary>
    public double DefaultThreshold { get; set; } = 0.02;

    /// <summary>
    /// Evaluates whether the given <paramref name="currentFrame"/> has changed enough
    /// compared to the previous frame to warrant OCR processing.
    /// Returns true for the very first frame or if the difference exceeds the threshold.
    /// Updates the internal reference thumbnail when returning true.
    /// </summary>
    public bool ShouldProcess(Bitmap? currentFrame, double? threshold = null)
    {
        if (currentFrame == null) return false;

        double targetThreshold = threshold ?? DefaultThreshold;

        lock (_syncLock)
        {
            byte[] currentBytes = ExtractThumbnailRgb(currentFrame);

            if (_lastThumbnailBytes == null)
            {
                // First frame is always processed
                _lastThumbnailBytes = currentBytes;
                return true;
            }

            double diff = CalculateMeanAbsoluteDifference(_lastThumbnailBytes, currentBytes);

            if (diff > targetThreshold)
            {
                _lastThumbnailBytes = currentBytes;
                return true;
            }

            return false;
        }
    }

    /// <summary>
    /// Computes the normalized Mean Absolute Difference (0.0 to 1.0) between two RGB byte arrays.
    /// </summary>
    public static double CalculateMeanAbsoluteDifference(byte[] prevRgb, byte[] currRgb)
    {
        if (prevRgb.Length != currRgb.Length || prevRgb.Length == 0) return 1.0;

        long totalDiff = 0;
        int length = prevRgb.Length;

        for (int i = 0; i < length; i++)
        {
            totalDiff += Math.Abs(prevRgb[i] - currRgb[i]);
        }

        // Max possible difference is length * 255
        double maxDiff = (double)length * 255.0;
        return (double)totalDiff / maxDiff;
    }

    /// <summary>
    /// Extracts a 160x120 downsampled 24bpp RGB byte array from a source bitmap.
    /// </summary>
    private static byte[] ExtractThumbnailRgb(Bitmap source)
    {
        using var thumbnail = new Bitmap(ThumbnailWidth, ThumbnailHeight, PixelFormat.Format24bppRgb);
        using (var g = Graphics.FromImage(thumbnail))
        {
            g.InterpolationMode = InterpolationMode.Bilinear;
            g.SmoothingMode = SmoothingMode.HighSpeed;
            g.PixelOffsetMode = PixelOffsetMode.HighSpeed;
            g.DrawImage(source, 0, 0, ThumbnailWidth, ThumbnailHeight);
        }

        var bounds = new Rectangle(0, 0, ThumbnailWidth, ThumbnailHeight);
        var data = thumbnail.LockBits(bounds, ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);

        try
        {
            int totalBytes = Math.Abs(data.Stride) * ThumbnailHeight;
            byte[] rawBytes = new byte[totalBytes];
            System.Runtime.InteropServices.Marshal.Copy(data.Scan0, rawBytes, 0, totalBytes);
            return rawBytes;
        }
        finally
        {
            thumbnail.UnlockBits(data);
        }
    }

    /// <summary>
    /// Clears the stored previous frame reference.
    /// </summary>
    public void Reset()
    {
        lock (_syncLock)
        {
            _lastThumbnailBytes = null;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        lock (_syncLock)
        {
            _lastThumbnailBytes = null;
            _disposed = true;
        }
    }
}
