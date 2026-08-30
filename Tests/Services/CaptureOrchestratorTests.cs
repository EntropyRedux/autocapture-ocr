using System.Drawing;
using AutoCaptureOCR.Core.Capture;
using AutoCaptureOCR.Core.Interfaces;
using AutoCaptureOCR.Core.Models;
using AutoCaptureOCR.Core.Services;
using FluentAssertions;
using Moq;
using Xunit;

namespace AutoCaptureOCR.Tests.Services;

public class CaptureOrchestratorTests : IDisposable
{
    private readonly string _testDirPath;
    private readonly Project _project;
    private readonly CaptureSession _session;

    public CaptureOrchestratorTests()
    {
        _testDirPath = Path.Combine(Path.GetTempPath(), $"orch_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDirPath);

        _project = new Project
        {
            Name = "TestProject",
            SavePath = _testDirPath
        };

        _session = new CaptureSession
        {
            Name = "Session1"
        };
        _project.Sessions.Add(_session);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDirPath))
        {
            try { Directory.Delete(_testDirPath, recursive: true); } catch { }
        }
    }

    [Fact]
    public async Task CaptureSnapshotAsync_CreatesScreenCapture()
    {
        var mockOcr = new Mock<IOCREngine>();
        mockOcr.Setup(o => o.ProcessAsync(It.IsAny<Bitmap>()))
            .ReturnsAsync(new OCRResult { Text = "Snapshot OCR Content" });

        var mockSource = new Mock<ICaptureSource>();
        mockSource.Setup(s => s.SourceName).Returns("Mock Source");
        mockSource.Setup(s => s.CaptureOnceAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CapturePayload
            {
                Frame = new Bitmap(100, 100),
                SourceEngine = "Mock Source"
            });

        var projectService = new ProjectService(_testDirPath);
        using var orchestrator = new CaptureOrchestrator(mockOcr.Object, projectService);

        var capture = await orchestrator.CaptureSnapshotAsync(_project, _session, mockSource.Object);

        capture.Should().NotBeNull();
        capture.OCRResult.Should().NotBeNull();
        capture.OCRResult!.Text.Should().Be("Snapshot OCR Content");
        File.Exists(capture.FilePath).Should().BeTrue();
    }

    [Fact]
    public async Task StartLiveChatSessionAsync_And_StopActiveSessionAsync_Lifecycle()
    {
        var mockSource = new Mock<ICaptureSource>();
        mockSource.Setup(s => s.SourceName).Returns("Mock Live Chat");
        mockSource.Setup(s => s.StartAsync(It.IsAny<CaptureSourceOptions>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        mockSource.Setup(s => s.StopAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        mockSource.Setup(s => s.GetStreamAsync(It.IsAny<CancellationToken>()))
            .Returns(EmptyAsyncEnumerable<CapturePayload>());

        var projectService = new ProjectService(_testDirPath);
        using var orchestrator = new CaptureOrchestrator(projectService: projectService);

        orchestrator.IsStreaming.Should().BeFalse();

        await orchestrator.StartLiveChatSessionAsync(_project, _session, mockSource.Object);
        orchestrator.IsStreaming.Should().BeTrue();
        orchestrator.ActiveSessionType.Should().Be(SessionType.LiveChat);

        // Attempting to start another session while active throws
        var act = () => orchestrator.StartLiveChatSessionAsync(_project, _session, mockSource.Object);
        await act.Should().ThrowAsync<InvalidOperationException>();

        await orchestrator.StopActiveSessionAsync();
        orchestrator.IsStreaming.Should().BeFalse();
    }

    private static async IAsyncEnumerable<T> EmptyAsyncEnumerable<T>()
    {
        await Task.CompletedTask;
        yield break;
    }
}
