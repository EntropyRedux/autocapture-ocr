using AutoCaptureOCR.Core.Capture;
using AutoCaptureOCR.Core.Interfaces;
using AutoCaptureOCR.Core.Models;
using FluentAssertions;
using Xunit;

namespace AutoCaptureOCR.Tests.Capture;

public class WindowGraphicsCaptureProviderTests
{
    [Fact]
    public void Properties_HaveExpectedDefaults()
    {
        using var provider = new WindowGraphicsCaptureProvider();

        provider.SourceName.Should().Be("Windows Graphics Capture");
        provider.SourceType.Should().Be(CaptureSourceType.VideoStream);
        provider.IsRunning.Should().BeFalse();
    }

    [Fact]
    public async Task StartAsync_SetsIsRunningTrue()
    {
        var provider = new WindowGraphicsCaptureProvider();
        var options = new CaptureSourceOptions
        {
            MaxFrameRate = 1,
            EnableFrameDiffing = true
        };

        try
        {
            await provider.StartAsync(options);
            provider.IsRunning.Should().BeTrue();
        }
        finally
        {
            await provider.StopAsync();
            provider.IsRunning.Should().BeFalse();
        }
    }

    [Fact]
    public async Task StartAsync_WhenAlreadyRunning_ThrowsInvalidOperationException()
    {
        var provider = new WindowGraphicsCaptureProvider();
        var options = new CaptureSourceOptions();

        try
        {
            await provider.StartAsync(options);
            var act = () => provider.StartAsync(options);
            await act.Should().ThrowAsync<InvalidOperationException>();
        }
        finally
        {
            await provider.StopAsync();
        }
    }

    [Fact]
    public async Task StopAsync_WhenNotRunning_DoesNotThrow()
    {
        var provider = new WindowGraphicsCaptureProvider();
        var act = () => provider.StopAsync();
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task CaptureManager_ImplementsICaptureSource_Correctly()
    {
        ICaptureSource manager = new CaptureManager();

        manager.SourceName.Should().Be("GDI Screen Capture");
        manager.SourceType.Should().Be(CaptureSourceType.Snapshot);
        (await manager.IsAvailableAsync()).Should().BeTrue();

        await manager.StartAsync(new CaptureSourceOptions());
        manager.IsRunning.Should().BeTrue();

        await manager.StopAsync();
        manager.IsRunning.Should().BeFalse();
    }
}
