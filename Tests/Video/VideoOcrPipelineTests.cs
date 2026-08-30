using System.Drawing;
using AutoCaptureOCR.Core.Interfaces;
using AutoCaptureOCR.Core.Models;
using AutoCaptureOCR.Core.Video;
using FluentAssertions;
using Moq;
using Xunit;

namespace AutoCaptureOCR.Tests.Video;

public class VideoOcrPipelineTests : IDisposable
{
    private readonly string _testVideoPath;
    private readonly string _testTranscriptPath;

    public VideoOcrPipelineTests()
    {
        string id = Guid.NewGuid().ToString("N");
        _testVideoPath = Path.Combine(Path.GetTempPath(), $"video_test_{id}.mp4");
        _testTranscriptPath = Path.Combine(Path.GetTempPath(), $"video_transcript_test_{id}.md");
    }

    public void Dispose()
    {
        if (File.Exists(_testVideoPath))
        {
            try { File.Delete(_testVideoPath); } catch { }
        }
        if (File.Exists(_testTranscriptPath))
        {
            try { File.Delete(_testTranscriptPath); } catch { }
        }
    }

    [Fact]
    public async Task VideoRecorder_Lifecycle_StartsAndStops()
    {
        using var recorder = new VideoRecorder();

        recorder.IsRecording.Should().BeFalse();

        await recorder.StartRecordingAsync(_testVideoPath, 320, 240, new VideoSettings { RecordingFps = 15 });
        recorder.IsRecording.Should().BeTrue();
        recorder.OutputPath.Should().Be(_testVideoPath);

        // Enqueue a synthetic frame
        using (var bmp = new Bitmap(320, 240))
        {
            recorder.EnqueueFrame(bmp, TimeSpan.FromMilliseconds(100));
        }

        await recorder.StopRecordingAsync();
        recorder.IsRecording.Should().BeFalse();

        var stats = recorder.GetStats();
        stats.Should().NotBeNull();
        stats.OutputPath.Should().Be(_testVideoPath);
    }

    [Fact]
    public async Task VideoOcrPipeline_Lifecycle_WithMocks()
    {
        var mockSource = new Mock<ICaptureSource>();
        mockSource.Setup(s => s.StartAsync(It.IsAny<CaptureSourceOptions>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        mockSource.Setup(s => s.StopAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        mockSource.Setup(s => s.CaptureOnceAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CapturePayload
            {
                Frame = new Bitmap(640, 480)
            });

        // Setup empty stream
        mockSource.Setup(s => s.GetStreamAsync(It.IsAny<CancellationToken>()))
            .Returns(EmptyAsyncEnumerable<CapturePayload>());

        var mockOcr = new Mock<IOCREngine>();
        mockOcr.Setup(o => o.ProcessAsync(It.IsAny<Bitmap>()))
            .ReturnsAsync(new OCRResult { Text = "Sample Frame OCR" });

        using var pipeline = new VideoOcrPipeline(mockSource.Object, mockOcr.Object);

        pipeline.IsRunning.Should().BeFalse();

        await pipeline.StartAsync(IntPtr.Zero, _testVideoPath, _testTranscriptPath, new VideoSettings());
        pipeline.IsRunning.Should().BeTrue();

        await pipeline.StopAsync();
        pipeline.IsRunning.Should().BeFalse();

        File.Exists(_testTranscriptPath).Should().BeTrue();
    }

    private static async IAsyncEnumerable<T> EmptyAsyncEnumerable<T>()
    {
        await Task.CompletedTask;
        yield break;
    }
}
