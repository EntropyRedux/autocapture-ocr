using System.Drawing;
using System.Drawing.Imaging;
using AutoCaptureOCR.Core.Capture;
using FluentAssertions;
using Xunit;

namespace AutoCaptureOCR.Tests.Capture;

public class FrameDiffFilterTests
{
    [Fact]
    public void ShouldProcess_NullFrame_ReturnsFalse()
    {
        using var filter = new FrameDiffFilter();
        filter.ShouldProcess(null).Should().BeFalse();
    }

    [Fact]
    public void ShouldProcess_FirstFrame_AlwaysReturnsTrue()
    {
        using var filter = new FrameDiffFilter();
        using var bmp = new Bitmap(200, 200);

        filter.ShouldProcess(bmp).Should().BeTrue();
    }

    [Fact]
    public void ShouldProcess_IdenticalFrames_ReturnsFalse()
    {
        using var filter = new FrameDiffFilter();
        using var bmp1 = new Bitmap(200, 200);
        using (var g = Graphics.FromImage(bmp1))
        {
            g.Clear(Color.Blue);
        }

        using var bmp2 = new Bitmap(200, 200);
        using (var g = Graphics.FromImage(bmp2))
        {
            g.Clear(Color.Blue);
        }

        filter.ShouldProcess(bmp1).Should().BeTrue(); // First frame
        filter.ShouldProcess(bmp2).Should().BeFalse(); // Identical second frame
    }

    [Fact]
    public void ShouldProcess_CompletelyDifferentFrames_ReturnsTrue()
    {
        using var filter = new FrameDiffFilter();
        using var bmp1 = new Bitmap(200, 200);
        using (var g = Graphics.FromImage(bmp1))
        {
            g.Clear(Color.Black);
        }

        using var bmp2 = new Bitmap(200, 200);
        using (var g = Graphics.FromImage(bmp2))
        {
            g.Clear(Color.White);
        }

        filter.ShouldProcess(bmp1).Should().BeTrue();
        filter.ShouldProcess(bmp2).Should().BeTrue();
    }

    [Fact]
    public void ShouldProcess_BelowThreshold_ReturnsFalse()
    {
        using var filter = new FrameDiffFilter { DefaultThreshold = 0.05 }; // 5% threshold
        using var bmp1 = new Bitmap(200, 200);
        using (var g = Graphics.FromImage(bmp1))
        {
            g.Clear(Color.White);
        }

        using var bmp2 = new Bitmap(200, 200);
        using (var g = Graphics.FromImage(bmp2))
        {
            g.Clear(Color.White);
            // Draw a tiny 2x2 red dot on 200x200 canvas (negligible diff < 0.01%)
            g.FillRectangle(Brushes.Red, 10, 10, 2, 2);
        }

        filter.ShouldProcess(bmp1).Should().BeTrue();
        filter.ShouldProcess(bmp2).Should().BeFalse();
    }

    [Fact]
    public void Reset_ClearsStoredFrame_NextFrameProcessedAsFirst()
    {
        using var filter = new FrameDiffFilter();
        using var bmp = new Bitmap(200, 200);
        using (var g = Graphics.FromImage(bmp))
        {
            g.Clear(Color.Green);
        }

        filter.ShouldProcess(bmp).Should().BeTrue();
        filter.ShouldProcess(bmp).Should().BeFalse();

        filter.Reset();

        filter.ShouldProcess(bmp).Should().BeTrue();
    }

    [Fact]
    public void CalculateMeanAbsoluteDifference_IdenticalArrays_ReturnsZero()
    {
        byte[] a = new byte[] { 10, 20, 30, 40 };
        byte[] b = new byte[] { 10, 20, 30, 40 };

        double diff = FrameDiffFilter.CalculateMeanAbsoluteDifference(a, b);
        diff.Should().Be(0.0);
    }

    [Fact]
    public void CalculateMeanAbsoluteDifference_OppositeArrays_ReturnsOne()
    {
        byte[] a = new byte[] { 0, 0, 0 };
        byte[] b = new byte[] { 255, 255, 255 };

        double diff = FrameDiffFilter.CalculateMeanAbsoluteDifference(a, b);
        diff.Should().Be(1.0);
    }
}
