using System.Net.WebSockets;
using System.Text;
using AutoCaptureOCR.Core.Models;
using AutoCaptureOCR.Core.Services;
using FluentAssertions;
using Newtonsoft.Json;
using Xunit;

namespace AutoCaptureOCR.Tests.Services;

public class WebSocketBridgeTests
{
    [Fact]
    public async Task StartAsync_And_StopAsync_Lifecycle()
    {
        using var bridge = new WebSocketBridge();
        int port = 59123;

        bridge.IsRunning.Should().BeFalse();
        bridge.IsClientConnected.Should().BeFalse();

        await bridge.StartAsync(port);
        bridge.IsRunning.Should().BeTrue();

        await bridge.StopAsync();
        bridge.IsRunning.Should().BeFalse();
    }

    [Fact]
    public async Task ClientConnect_And_SendTurns_FiresTurnsReceived()
    {
        using var bridge = new WebSocketBridge();
        int port = 59124;
        await bridge.StartAsync(port);

        IReadOnlyList<ChatTurn>? receivedTurns = null;
        var tcs = new TaskCompletionSource<bool>();

        bridge.TurnsReceived += turns =>
        {
            receivedTurns = turns;
            tcs.TrySetResult(true);
            return Task.CompletedTask;
        };

        using var client = new ClientWebSocket();
        var uri = new Uri($"ws://127.0.0.1:{port}/chatcapture/");

        try
        {
            await client.ConnectAsync(uri, CancellationToken.None);
            client.State.Should().Be(WebSocketState.Open);

            var turnsToSend = new List<ChatTurn>
            {
                new() { Role = "user", Content = "Test query from extension", TurnIndex = 0 },
                new() { Role = "assistant", Content = "Test answer from assistant", TurnIndex = 1 }
            };

            string json = JsonConvert.SerializeObject(new { hostname = "claude.ai", turns = turnsToSend });
            byte[] bytes = Encoding.UTF8.GetBytes(json);

            await client.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None);

            var completed = await Task.WhenAny(tcs.Task, Task.Delay(2000));
            completed.Should().Be(tcs.Task);

            receivedTurns.Should().NotBeNull();
            receivedTurns!.Should().HaveCount(2);
            receivedTurns![0].Content.Should().Be("Test query from extension");
            receivedTurns![1].Content.Should().Be("Test answer from assistant");
        }
        finally
        {
            if (client.State == WebSocketState.Open)
            {
                await client.CloseAsync(WebSocketCloseStatus.NormalClosure, "Done", CancellationToken.None);
            }
            await bridge.StopAsync();
        }
    }
}
