using AutoCaptureOCR.Core.Capture;
using AutoCaptureOCR.Core.Interfaces;
using AutoCaptureOCR.Core.Models;
using FluentAssertions;
using Xunit;

namespace AutoCaptureOCR.Tests.Capture;

public class UiaCaptureEngineTests
{
    [Fact]
    public void Properties_HaveExpectedDefaults()
    {
        using var engine = new UiaCaptureEngine();

        engine.SourceName.Should().Be("Windows UI Automation");
        engine.SourceType.Should().Be(CaptureSourceType.LiveTextStream);
        engine.IsRunning.Should().BeFalse();
    }

    [Fact]
    public async Task StartAsync_And_StopAsync_Lifecycle()
    {
        using var engine = new UiaCaptureEngine();
        var options = new CaptureSourceOptions
        {
            TargetWindowHandle = IntPtr.Zero,
            PollInterval = TimeSpan.FromSeconds(1)
        };

        await engine.StartAsync(options);
        engine.IsRunning.Should().BeTrue();

        await engine.StopAsync();
        engine.IsRunning.Should().BeFalse();
    }

    [Fact]
    public async Task StartAsync_WhenAlreadyRunning_ThrowsInvalidOperationException()
    {
        using var engine = new UiaCaptureEngine();
        var options = new CaptureSourceOptions();

        await engine.StartAsync(options);
        var act = () => engine.StartAsync(options);

        await act.Should().ThrowAsync<InvalidOperationException>();
        await engine.StopAsync();
    }

    [Fact]
    public async Task CaptureOnceAsync_ZeroHwnd_ReturnsEmptyTurns()
    {
        using var engine = new UiaCaptureEngine();
        var payload = await engine.CaptureOnceAsync();

        payload.Should().NotBeNull();
        payload.SourceType.Should().Be(CaptureSourceType.LiveTextStream);
        payload.Turns.Should().BeEmpty();
    }

    [Fact]
    public async Task IsAvailableAsync_ReturnsBoolean()
    {
        using var engine = new UiaCaptureEngine();
        bool available = await engine.IsAvailableAsync();
        // On Windows 10/11 environment, UIA root element is accessible
        available.Should().BeTrue();
    }
}
