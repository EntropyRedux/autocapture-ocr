using AutoCaptureOCR.Core.Models;
using AutoCaptureOCR.Core.Services;
using FluentAssertions;
using Xunit;

namespace AutoCaptureOCR.Tests.Services;

public class TranscriptWriterTests : IDisposable
{
    private readonly string _testTranscriptPath;

    public TranscriptWriterTests()
    {
        _testTranscriptPath = Path.Combine(Path.GetTempPath(), $"transcript_test_{Guid.NewGuid():N}.md");
    }

    public void Dispose()
    {
        if (File.Exists(_testTranscriptPath))
        {
            try { File.Delete(_testTranscriptPath); } catch { }
        }
    }

    [Fact]
    public async Task EnsureInitialized_CreatesFileWithYamlFrontmatter()
    {
        await using var writer = new TranscriptWriter(_testTranscriptPath, title: "Test Chat", source: "Claude Extension");
        await writer.EnsureInitializedAsync();

        File.Exists(_testTranscriptPath).Should().BeTrue();
        string content = await File.ReadAllTextAsync(_testTranscriptPath);

        content.Should().StartWith("---");
        content.Should().Contain("title: \"Test Chat\"");
        content.Should().Contain("source: \"Claude Extension\"");
        content.Should().Contain("generator: AutoCapture-OCR v3.0");
    }

    [Fact]
    public async Task AppendTurnsAsync_AppendsFormattedMarkdown()
    {
        await using var writer = new TranscriptWriter(_testTranscriptPath);

        var turns = new List<ChatTurn>
        {
            new() { Role = "user", Content = "What is 2 + 2?" },
            new() { Role = "assistant", Content = "2 + 2 = **4**." }
        };

        await writer.AppendTurnsAsync(turns);

        string content = await File.ReadAllTextAsync(_testTranscriptPath);
        content.Should().Contain("### You");
        content.Should().Contain("What is 2 + 2?");
        content.Should().Contain("### Assistant");
        content.Should().Contain("2 + 2 = **4**.");
    }

    [Fact]
    public async Task AppendTurnsAsync_PreservesCodeBlocks()
    {
        await using var writer = new TranscriptWriter(_testTranscriptPath);

        string codeContent = "```csharp\npublic void Test() => Console.WriteLine(\"Hello\");\n```";
        var turns = new List<ChatTurn>
        {
            new() { Role = "assistant", Content = codeContent }
        };

        await writer.AppendTurnsAsync(turns);

        string content = await File.ReadAllTextAsync(_testTranscriptPath);
        content.Should().Contain("```csharp");
        content.Should().Contain("public void Test()");
    }

    [Fact]
    public async Task AppendTimestampedTextAsync_WritesVideoOcrHeading()
    {
        await using var writer = new TranscriptWriter(_testTranscriptPath);

        var timestamp = new DateTime(2026, 8, 29, 10, 30, 0, DateTimeKind.Utc);
        var offset = TimeSpan.FromMinutes(2.5);

        await writer.AppendTimestampedTextAsync(timestamp, offset, "Captured OCR text line 1\nCaptured line 2");

        string content = await File.ReadAllTextAsync(_testTranscriptPath);
        content.Should().Contain("#### [00:02:30] 2026-08-29 10:30:00 UTC");
        content.Should().Contain("Captured OCR text line 1");
    }
}
