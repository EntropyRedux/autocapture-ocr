using AutoCaptureOCR.Core.Models;
using System.Drawing;
using System.Drawing.Imaging;

namespace AutoCaptureOCR.Core.Capture;

/// <summary>
/// Handles screen capture operations
/// </summary>
public class CaptureManager
{
    public async Task<CaptureResult> CaptureFullscreenAsync()
    {
        try
        {
            await Task.Delay(100); // Small delay for UI to hide

            var bounds = System.Windows.Forms.Screen.PrimaryScreen!.Bounds;
            var bitmap = new Bitmap(bounds.Width, bounds.Height);

            using (var graphics = Graphics.FromImage(bitmap))
            {
                graphics.CopyFromScreen(
                    bounds.X, bounds.Y,
                    0, 0,
                    bounds.Size,
                    CopyPixelOperation.SourceCopy
                );
            }

            return new CaptureResult
            {
                Image = bitmap,
                Timestamp = DateTime.UtcNow,
                Region = bounds,
                Success = true
            };
        }
        catch (Exception ex)
        {
            return new CaptureResult
            {
                Success = false,
                ErrorMessage = ex.Message,
                Timestamp = DateTime.UtcNow
            };
        }
    }

    public async Task<CaptureResult?> CaptureRegionInteractiveAsync(Func<Task<Rectangle?>>? regionSelector = null)
    {
        try
        {
            if (regionSelector == null)
            {
                // Fallback to center 800x600 region if no selector provided
                var bounds = System.Windows.Forms.Screen.PrimaryScreen!.Bounds;
                var centerX = (bounds.Width - 800) / 2;
                var centerY = (bounds.Height - 600) / 2;
                var region = new Rectangle(centerX, centerY, 800, 600);

                await Task.Delay(100);

                return await CaptureRegionAsync(region);
            }

            // Use provided region selector (will be WPF overlay)
            var selectedRegion = await regionSelector();

            if (!selectedRegion.HasValue || selectedRegion.Value.Width < 10 || selectedRegion.Value.Height < 10)
            {
                return null; // User cancelled or invalid selection
            }

            return await CaptureRegionAsync(selectedRegion.Value);
        }
        catch (Exception ex)
        {
            return new CaptureResult
            {
                Success = false,
                ErrorMessage = ex.Message,
                Timestamp = DateTime.UtcNow
            };
        }
    }

    /// <summary>
    /// Captures a specific screen region with given coordinates (for continuous mode)
    /// </summary>
    public async Task<CaptureResult> CaptureSpecificRegionAsync(int x, int y, int width, int height)
    {
        var region = new Rectangle(x, y, width, height);
        return await CaptureRegionAsync(region);
    }

    private async Task<CaptureResult> CaptureRegionAsync(Rectangle region)
    {
        try
        {
            // Wait to ensure all UI elements (overlay, main window) are hidden
            await Task.Delay(200);

            // Ensure we have valid dimensions
            if (region.Width <= 0 || region.Height <= 0)
            {
                throw new ArgumentException($"Invalid region dimensions: {region.Width}x{region.Height}");
            }

            // CRITICAL FIX for multi-monitor with negative coordinates:
            // Graphics.CopyFromScreen doesn't handle negative coordinates correctly
            // We need to capture the entire virtual screen and crop the region we want

            // Get virtual screen bounds
            var virtualScreenBounds = System.Windows.Forms.SystemInformation.VirtualScreen;

            // Calculate region position relative to virtual screen
            var relativeX = region.X - virtualScreenBounds.X;
            var relativeY = region.Y - virtualScreenBounds.Y;

            // Capture the entire virtual screen
            using (var fullBitmap = new Bitmap(virtualScreenBounds.Width, virtualScreenBounds.Height, PixelFormat.Format32bppArgb))
            {
                using (var graphics = Graphics.FromImage(fullBitmap))
                {
                    graphics.CopyFromScreen(
                        virtualScreenBounds.X, virtualScreenBounds.Y,
                        0, 0,
                        virtualScreenBounds.Size,
                        CopyPixelOperation.SourceCopy
                    );
                }

                // Crop to the selected region
                var croppedBitmap = new Bitmap(region.Width, region.Height, PixelFormat.Format32bppArgb);
                using (var graphics = Graphics.FromImage(croppedBitmap))
                {
                    graphics.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighQuality;
                    graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;

                    graphics.DrawImage(fullBitmap,
                        new Rectangle(0, 0, region.Width, region.Height),  // Destination
                        new Rectangle(relativeX, relativeY, region.Width, region.Height),  // Source
                        GraphicsUnit.Pixel);
                }

                return new CaptureResult
                {
                    Image = croppedBitmap,
                    Timestamp = DateTime.UtcNow,
                    Region = region,
                    Success = true
                };
            }
        }
        catch (Exception ex)
        {
            return new CaptureResult
            {
                Success = false,
                ErrorMessage = $"Capture failed at ({region.X}, {region.Y}, {region.Width}x{region.Height}): {ex.Message}",
                Timestamp = DateTime.UtcNow
            };
        }
    }
}
