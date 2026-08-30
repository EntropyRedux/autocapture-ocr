using System.Drawing;
using AutoCaptureOCR.Core.Models;
using FluentAssertions;
using Xunit;

namespace AutoCaptureOCR.Tests.Models;

public class CapturePayloadTests
{
    [Fact]
    public void HasImage_WithFrame_ReturnsTrue()
    {
        using var bitmap = new Bitmap(1, 1);
        var payload = new CapturePayload { Frame = bitmap };

        payload.HasImage.Should().BeTrue();
    }

    [Fact]
    public void HasImage_WithoutFrame_ReturnsFalse()
    {
        var payload = new CapturePayload { Frame = null };

        payload.HasImage.Should().BeFalse();
    }

    [Fact]
    public void HasText_WithTurns_ReturnsTrue()
    {
        var turns = new List<ChatTurn>
        {
            new() { Role = "user", Content = "Hello" }
        };
        var payload = new CapturePayload { Turns = turns };

        payload.HasText.Should().BeTrue();
    }

    [Fact]
    public void HasText_WithRawText_ReturnsTrue()
    {
        var payload = new CapturePayload { RawText = "Some OCR text" };

        payload.HasText.Should().BeTrue();
    }

    [Fact]
    public void HasText_WithEmptyTurnsAndNoRawText_ReturnsFalse()
    {
        var payload = new CapturePayload
        {
            Turns = new List<ChatTurn>(),
            RawText = null
        };

        payload.HasText.Should().BeFalse();
    }

    [Fact]
    public void HasText_WithEmptyRawText_ReturnsFalse()
    {
        var payload = new CapturePayload { RawText = "" };

        payload.HasText.Should().BeFalse();
    }

    [Fact]
    public void HasText_WithBothTurnsAndRawText_ReturnsTrue()
    {
        var turns = new List<ChatTurn>
        {
            new() { Role = "assistant", Content = "Hello!" }
        };
        var payload = new CapturePayload
        {
            Turns = turns,
            RawText = "fallback text"
        };

        payload.HasText.Should().BeTrue();
    }

    [Fact]
    public void HasImage_And_HasText_WithBoth_ReturnTrue()
    {
        using var bitmap = new Bitmap(10, 10);
        var turns = new List<ChatTurn>
        {
            new() { Role = "user", Content = "test" }
        };
        var payload = new CapturePayload
        {
            Frame = bitmap,
            Turns = turns
        };

        payload.HasImage.Should().BeTrue();
        payload.HasText.Should().BeTrue();
    }

    [Fact]
    public void HasImage_And_HasText_WithNeither_ReturnFalse()
    {
        var payload = new CapturePayload();

        payload.HasImage.Should().BeFalse();
        payload.HasText.Should().BeFalse();
    }

    [Fact]
    public void DefaultValues_AreCorrect()
    {
        var payload = new CapturePayload();

        payload.Id.Should().NotBeEmpty();
        payload.Timestamp.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        payload.SourceEngine.Should().BeEmpty();
        payload.Frame.Should().BeNull();
        payload.Region.Should().BeNull();
        payload.Turns.Should().BeNull();
        payload.RawText.Should().BeNull();
        payload.VideoTimestamp.Should().BeNull();
        payload.FrameNumber.Should().BeNull();
    }

    [Fact]
    public void CapturePayload_CarriesVideoMetadata()
    {
        var payload = new CapturePayload
        {
            SourceType = CaptureSourceType.VideoStream,
            SourceEngine = "WGC",
            VideoTimestamp = TimeSpan.FromSeconds(42),
            FrameNumber = 630
        };

        payload.SourceType.Should().Be(CaptureSourceType.VideoStream);
        payload.SourceEngine.Should().Be("WGC");
        payload.VideoTimestamp.Should().Be(TimeSpan.FromSeconds(42));
        payload.FrameNumber.Should().Be(630);
    }
}

public class ChatTurnTests
{
    [Fact]
    public void ChatTurn_RecordEquality_WorksOnValue()
    {
        var timestamp = new DateTime(2026, 8, 29, 10, 0, 0, DateTimeKind.Utc);
        var turn1 = new ChatTurn { Role = "user", Content = "Hello", TurnIndex = 0, Timestamp = timestamp };
        var turn2 = new ChatTurn { Role = "user", Content = "Hello", TurnIndex = 0, Timestamp = timestamp };

        turn1.Should().Be(turn2);
    }

    [Fact]
    public void ChatTurn_RecordEquality_DifferentContent_NotEqual()
    {
        var turn1 = new ChatTurn { Role = "user", Content = "Hello", TurnIndex = 0 };
        var turn2 = new ChatTurn { Role = "user", Content = "World", TurnIndex = 0 };

        turn1.Should().NotBe(turn2);
    }

    [Fact]
    public void ChatTurn_RecordEquality_DifferentRole_NotEqual()
    {
        var turn1 = new ChatTurn { Role = "user", Content = "Hello" };
        var turn2 = new ChatTurn { Role = "assistant", Content = "Hello" };

        turn1.Should().NotBe(turn2);
    }

    [Fact]
    public void ChatTurn_DefaultValues_AreCorrect()
    {
        var turn = new ChatTurn { Role = "user", Content = "test" };

        turn.MessageId.Should().BeNull();
        turn.TurnIndex.Should().Be(0);
        turn.Timestamp.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        turn.IsStreaming.Should().BeFalse();
    }

    [Fact]
    public void ChatTurn_IsImmutable_WithExpression()
    {
        var original = new ChatTurn { Role = "user", Content = "Hello", TurnIndex = 0 };
        var updated = original with { Content = "Updated", TurnIndex = 1 };

        original.Content.Should().Be("Hello");
        original.TurnIndex.Should().Be(0);
        updated.Content.Should().Be("Updated");
        updated.TurnIndex.Should().Be(1);
        updated.Role.Should().Be("user"); // preserved from original
    }

    [Fact]
    public void ChatTurn_StreamingTurn_PropertiesSet()
    {
        var turn = new ChatTurn
        {
            Role = "assistant",
            Content = "In progress...",
            IsStreaming = true,
            MessageId = "msg_12345"
        };

        turn.IsStreaming.Should().BeTrue();
        turn.MessageId.Should().Be("msg_12345");
    }
}

public class CaptureSourceTypeTests
{
    [Theory]
    [InlineData(CaptureSourceType.Snapshot, 0)]
    [InlineData(CaptureSourceType.LiveTextStream, 1)]
    [InlineData(CaptureSourceType.VideoStream, 2)]
    public void CaptureSourceType_HasExpectedValues(CaptureSourceType type, int expected)
    {
        ((int)type).Should().Be(expected);
    }
}

public class SessionTypeTests
{
    [Theory]
    [InlineData(SessionType.Snapshot, 0)]
    [InlineData(SessionType.LiveChat, 1)]
    [InlineData(SessionType.VideoRecording, 2)]
    public void SessionType_HasExpectedValues(SessionType type, int expected)
    {
        ((int)type).Should().Be(expected);
    }

    [Fact]
    public void CaptureSession_DefaultType_IsSnapshot()
    {
        var session = new CaptureSession();
        session.Type.Should().Be(SessionType.Snapshot);
    }

    [Fact]
    public void CaptureSession_LiveChatFields_AreNullByDefault()
    {
        var session = new CaptureSession();
        session.SourceEngine.Should().BeNull();
        session.TargetWindowTitle.Should().BeNull();
        session.TranscriptFilePath.Should().BeNull();
        session.VideoFilePath.Should().BeNull();
    }
}
