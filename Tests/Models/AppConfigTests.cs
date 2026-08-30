using AutoCaptureOCR.Core.Models;
using AutoCaptureOCR.Core.Configuration;
using FluentAssertions;
using Newtonsoft.Json;
using Xunit;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace AutoCaptureOCR.Tests.Models;

public class AppConfigTests
{
    [Fact]
    public void GetDefault_ReturnsNonNullConfig()
    {
        var config = AppConfig.GetDefault();

        config.Should().NotBeNull();
        config.App.Should().NotBeNull();
        config.Capture.Should().NotBeNull();
        config.OCR.Should().NotBeNull();
        config.Naming.Should().NotBeNull();
        config.Export.Should().NotBeNull();
        config.UI.Should().NotBeNull();
    }

    [Fact]
    public void GetDefault_ChatCaptureSettings_HasDefaults()
    {
        var config = AppConfig.GetDefault();

        config.ChatCapture.Should().NotBeNull();
        config.ChatCapture.Enabled.Should().BeFalse();
        config.ChatCapture.WebSocketPort.Should().Be(49281);
        config.ChatCapture.UiaPollInterval.Should().Be(TimeSpan.FromSeconds(2));
        config.ChatCapture.AutoStartOnLaunch.Should().BeFalse();
        config.ChatCapture.TranscriptOutputDir.Should().BeEmpty();
        config.ChatCapture.MonitoredHostnames.Should().Contain("claude.ai");
        config.ChatCapture.MonitoredHostnames.Should().Contain("chatgpt.com");
        config.ChatCapture.AuthToken.Should().BeEmpty();
    }

    [Fact]
    public void GetDefault_VideoSettings_HasDefaults()
    {
        var config = AppConfig.GetDefault();

        config.Video.Should().NotBeNull();
        config.Video.OcrFrameRate.Should().Be(1);
        config.Video.RecordingFps.Should().Be(15);
        config.Video.VideoCodec.Should().Be("H264");
        config.Video.VideoBitrate.Should().Be(4_000_000);
        config.Video.EnableFrameDiffing.Should().BeTrue();
        config.Video.FrameDiffThreshold.Should().Be(0.02);
        config.Video.OutputDirectory.Should().BeEmpty();
    }

    [Fact]
    public void ChatCaptureSettings_JsonRoundTrip()
    {
        var original = new ChatCaptureSettings
        {
            Enabled = true,
            WebSocketPort = 55555,
            AutoStartOnLaunch = true,
            MonitoredHostnames = new List<string> { "custom.ai" },
            AuthToken = "test-token-123"
        };

        var json = JsonConvert.SerializeObject(original, Formatting.Indented);
        var deserialized = JsonConvert.DeserializeObject<ChatCaptureSettings>(json);

        deserialized.Should().NotBeNull();
        deserialized!.Enabled.Should().BeTrue();
        deserialized.WebSocketPort.Should().Be(55555);
        deserialized.AutoStartOnLaunch.Should().BeTrue();
        deserialized.MonitoredHostnames.Should().ContainSingle().Which.Should().Be("custom.ai");
        deserialized.AuthToken.Should().Be("test-token-123");
    }

    [Fact]
    public void VideoSettings_JsonRoundTrip()
    {
        var original = new VideoSettings
        {
            OcrFrameRate = 2,
            RecordingFps = 30,
            VideoBitrate = 8_000_000,
            EnableFrameDiffing = false,
            FrameDiffThreshold = 0.05
        };

        var json = JsonConvert.SerializeObject(original);
        var deserialized = JsonConvert.DeserializeObject<VideoSettings>(json);

        deserialized.Should().NotBeNull();
        deserialized!.OcrFrameRate.Should().Be(2);
        deserialized.RecordingFps.Should().Be(30);
        deserialized.VideoBitrate.Should().Be(8_000_000);
        deserialized.EnableFrameDiffing.Should().BeFalse();
        deserialized.FrameDiffThreshold.Should().Be(0.05);
    }

    [Fact]
    public void FullAppConfig_YamlRoundTrip()
    {
        var original = AppConfig.GetDefault();
        original.ChatCapture.Enabled = true;
        original.ChatCapture.WebSocketPort = 12345;
        original.Video.RecordingFps = 30;

        var serializer = new SerializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .Build();
        var yaml = serializer.Serialize(original);

        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .Build();
        var deserialized = deserializer.Deserialize<AppConfig>(yaml);

        deserialized.Should().NotBeNull();
        deserialized!.ChatCapture.Enabled.Should().BeTrue();
        deserialized.ChatCapture.WebSocketPort.Should().Be(12345);
        deserialized.Video.RecordingFps.Should().Be(30);
        deserialized.App.Version.Should().Be("3.0.0");
    }

    [Fact]
    public void ScreenCapture_DefaultSourceEngine_IsManual()
    {
        var capture = new ScreenCapture();
        capture.SourceEngine.Should().Be("Manual");
        capture.VideoTimestamp.Should().BeNull();
    }

    [Fact]
    public void ScreenCapture_VideoTimestamp_CanBeSet()
    {
        var capture = new ScreenCapture
        {
            SourceEngine = "WGC",
            VideoTimestamp = TimeSpan.FromMinutes(5.5)
        };

        capture.SourceEngine.Should().Be("WGC");
        capture.VideoTimestamp.Should().Be(TimeSpan.FromMinutes(5.5));
    }

    [Fact]
    public void CaptureSourceOptions_DefaultValues_AreCorrect()
    {
        var options = new CaptureSourceOptions();

        options.TargetWindowHandle.Should().Be(IntPtr.Zero);
        options.TargetHostname.Should().BeNull();
        options.PollInterval.Should().Be(TimeSpan.FromSeconds(2));
        options.MaxFrameRate.Should().Be(1);
        options.EnableFrameDiffing.Should().BeTrue();
        options.FrameDiffThreshold.Should().Be(0.02);
        options.CaptureRegion.Should().BeNull();
    }
}
