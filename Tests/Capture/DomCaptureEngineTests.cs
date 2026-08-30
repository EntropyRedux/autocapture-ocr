using AutoCaptureOCR.Core.Capture;
using AutoCaptureOCR.Core.Interfaces;
using AutoCaptureOCR.Core.Models;
using AutoCaptureOCR.Core.Services;
using FluentAssertions;
using Xunit;

namespace AutoCaptureOCR.Tests.Capture;

public class DomCaptureEngineTests
{
    [Fact]
    public void Properties_HaveExpectedDefaults()
    {
        using var engine = new DomCaptureEngine();

        engine.SourceName.Should().Be("DOM Extension");
        engine.SourceType.Should().Be(CaptureSourceType.LiveTextStream);
        engine.IsRunning.Should().BeFalse();
    }

    [Fact]
    public async Task StartAsync_And_StopAsync_Lifecycle()
    {
        var bridge = new WebSocketBridge();
        var dedup = new DedupEngine();
        using var engine = new DomCaptureEngine(bridge, dedup);

        var options = new CaptureSourceOptions();
        await engine.StartAsync(options);
        engine.IsRunning.Should().BeTrue();

        await engine.StopAsync();
        engine.IsRunning.Should().BeFalse();
    }

    [Fact]
    public async Task CaptureOnceAsync_InitialState_ReturnsEmptyTurns()
    {
        using var engine = new DomCaptureEngine();
        var payload = await engine.CaptureOnceAsync();

        payload.Should().NotBeNull();
        payload.SourceType.Should().Be(CaptureSourceType.LiveTextStream);
        payload.Turns.Should().BeEmpty();
    }

    [Fact]
    public async Task IsAvailableAsync_ReturnsTrue()
    {
        using var engine = new DomCaptureEngine();
        bool available = await engine.IsAvailableAsync();
        available.Should().BeTrue();
    }
}
