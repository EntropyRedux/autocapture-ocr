using AutoCaptureOCR.Core.Models;
using AutoCaptureOCR.Core.Services;
using FluentAssertions;
using Xunit;

namespace AutoCaptureOCR.Tests.Services;

public class DedupEngineTests : IDisposable
{
    private readonly string _testSidecarPath;

    public DedupEngineTests()
    {
        _testSidecarPath = Path.Combine(Path.GetTempPath(), $"dedup_test_{Guid.NewGuid():N}.hashes");
    }

    public void Dispose()
    {
        if (File.Exists(_testSidecarPath))
        {
            try { File.Delete(_testSidecarPath); } catch { }
        }
    }

    [Fact]
    public void IsNew_FirstTime_ReturnsTrue()
    {
        using var engine = new DedupEngine();
        engine.IsNew("user", "Hello world").Should().BeTrue();
    }

    [Fact]
    public void IsNew_DuplicateContentSameRole_ReturnsFalse()
    {
        using var engine = new DedupEngine();
        engine.IsNew("user", "Hello world").Should().BeTrue();
        engine.IsNew("user", "Hello world").Should().BeFalse();
    }

    [Fact]
    public void IsNew_WhitespaceDifferences_TreatedAsDuplicate()
    {
        using var engine = new DedupEngine();
        engine.IsNew("user", "Hello    world\n\n").Should().BeTrue();
        engine.IsNew("user", "Hello world").Should().BeFalse();
    }

    [Fact]
    public void IsNew_DifferentRoles_TreatedAsDifferent()
    {
        using var engine = new DedupEngine();
        engine.IsNew("user", "Hello world").Should().BeTrue();
        engine.IsNew("assistant", "Hello world").Should().BeTrue();
    }

    [Fact]
    public void IsNew_WithChatTurn_MessageId_TakesPrecedence()
    {
        using var engine = new DedupEngine();
        var turn1 = new ChatTurn { Role = "assistant", Content = "Draft response...", MessageId = "msg-123" };
        var turn2 = new ChatTurn { Role = "assistant", Content = "Finished response with more tokens.", MessageId = "msg-123" };

        engine.IsNew(turn1).Should().BeTrue();
        engine.IsNew(turn2).Should().BeFalse(); // Same message ID
    }

    [Fact]
    public void Sidecar_Persistence_SurvivesReload()
    {
        using (var engine1 = new DedupEngine(_testSidecarPath))
        {
            engine1.IsNew("user", "Message 1").Should().BeTrue();
            engine1.IsNew("assistant", "Message 2").Should().BeTrue();
            engine1.Flush();
        }

        File.Exists(_testSidecarPath).Should().BeTrue();

        using (var engine2 = new DedupEngine(_testSidecarPath))
        {
            engine2.IsNew("user", "Message 1").Should().BeFalse();
            engine2.IsNew("assistant", "Message 2").Should().BeFalse();
            engine2.IsNew("user", "Message 3").Should().BeTrue();
        }
    }

    [Fact]
    public void IsNew_ConcurrentCalls_ThreadSafe()
    {
        using var engine = new DedupEngine();
        var results = new bool[100];

        Parallel.For(0, 100, i =>
        {
            results[i] = engine.IsNew("user", $"Message {i}");
        });

        results.Should().AllBeEquivalentTo(true);
        engine.TotalSeenCount.Should().Be(100);
    }
}
