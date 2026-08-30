using System.Net;
using System.Net.WebSockets;
using System.Text;
using AutoCaptureOCR.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace AutoCaptureOCR.Core.Services;

/// <summary>
/// Localhost WebSocket server listening on 127.0.0.1 for browser extension communication.
/// Extracts ChatTurns sent from the Manifest V3 content script and fires TurnsReceived events.
/// </summary>
public sealed class WebSocketBridge : IAsyncDisposable, IDisposable
{
    private readonly ILogger<WebSocketBridge> _logger;
    private readonly object _stateLock = new();

    private HttpListener? _httpListener;
    private CancellationTokenSource? _bridgeCts;
    private Task? _listenerTask;
    private WebSocket? _activeSocket;
    private string _expectedAuthToken = string.Empty;
    private bool _isRunning;
    private bool _disposed;

    public event Func<IReadOnlyList<ChatTurn>, Task>? TurnsReceived;

    public bool IsRunning => _isRunning;
    public bool IsClientConnected => _activeSocket != null && _activeSocket.State == WebSocketState.Open;

    public WebSocketBridge(ILogger<WebSocketBridge>? logger = null)
    {
        _logger = logger ?? NullLogger<WebSocketBridge>.Instance;
    }

    /// <summary>
    /// Starts the WebSocket bridge on 127.0.0.1:{port}/chatcapture/
    /// </summary>
    public Task StartAsync(int port = 49281, string authToken = "", CancellationToken ct = default)
    {
        lock (_stateLock)
        {
            if (_isRunning)
            {
                throw new InvalidOperationException("WebSocket bridge is already running.");
            }

            _expectedAuthToken = authToken;
            _bridgeCts = new CancellationTokenSource();
            _isRunning = true;

            try
            {
                _httpListener = new HttpListener();
                _httpListener.Prefixes.Add($"http://127.0.0.1:{port}/chatcapture/");
                _httpListener.Start();

                _listenerTask = Task.Run(() => ListenLoopAsync(_bridgeCts.Token));
                _logger.LogInformation("WebSocketBridge listening on http://127.0.0.1:{Port}/chatcapture/", port);
            }
            catch (Exception ex)
            {
                _isRunning = false;
                _logger.LogError(ex, "Failed to start HttpListener on port {Port}", port);
                throw;
            }
        }

        return Task.CompletedTask;
    }

    private async Task ListenLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested && _httpListener != null && _httpListener.IsListening)
        {
            try
            {
                var context = await _httpListener.GetContextAsync().ConfigureAwait(false);

                if (!context.Request.IsWebSocketRequest)
                {
                    context.Response.StatusCode = 400;
                    context.Response.Close();
                    continue;
                }

                // Auth token validation
                if (!string.IsNullOrEmpty(_expectedAuthToken))
                {
                    string? protocolHeader = context.Request.Headers["Sec-WebSocket-Protocol"];
                    string? queryToken = context.Request.QueryString["token"];

                    bool valid = string.Equals(protocolHeader, _expectedAuthToken, StringComparison.Ordinal) ||
                                 string.Equals(queryToken, _expectedAuthToken, StringComparison.Ordinal);

                    if (!valid)
                    {
                        _logger.LogWarning("WebSocket connection rejected: invalid auth token.");
                        context.Response.StatusCode = 401;
                        context.Response.Close();
                        continue;
                    }
                }

                _ = Task.Run(() => HandleClientAsync(context, ct));
            }
            catch (HttpListenerException) when (ct.IsCancellationRequested || !_isRunning)
            {
                break;
            }
            catch (Exception ex)
            {
                if (_isRunning && !ct.IsCancellationRequested)
                {
                    _logger.LogWarning(ex, "Error accepting WebSocket request.");
                }
            }
        }
    }

    private async Task HandleClientAsync(HttpListenerContext context, CancellationToken ct)
    {
        WebSocketContext wsContext;
        try
        {
            wsContext = await context.AcceptWebSocketAsync(subProtocol: null).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "WebSocket handshake failed.");
            return;
        }

        var socket = wsContext.WebSocket;
        lock (_stateLock)
        {
            _activeSocket?.Dispose();
            _activeSocket = socket;
        }

        _logger.LogInformation("ChatCapture browser extension connected via WebSocket.");

        var buffer = new byte[64 * 1024]; // 64KB buffer
        var ms = new MemoryStream();

        try
        {
            while (socket.State == WebSocketState.Open && !ct.IsCancellationRequested)
            {
                ms.SetLength(0);
                WebSocketReceiveResult result;

                do
                {
                    result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), ct).ConfigureAwait(false);
                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None).ConfigureAwait(false);
                        return;
                    }

                    ms.Write(buffer, 0, result.Count);
                }
                while (!result.EndOfMessage);

                if (result.MessageType == WebSocketMessageType.Text)
                {
                    string json = Encoding.UTF8.GetString(ms.ToArray());
                    await ProcessMessageJsonAsync(json).ConfigureAwait(false);
                }
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "WebSocket client communication error.");
        }
        finally
        {
            lock (_stateLock)
            {
                if (_activeSocket == socket)
                {
                    _activeSocket = null;
                }
            }
            socket.Dispose();
            _logger.LogInformation("ChatCapture browser extension disconnected.");
        }
    }

    private async Task ProcessMessageJsonAsync(string json)
    {
        try
        {
            List<ChatTurn>? turns = null;

            if (json.TrimStart().StartsWith("["))
            {
                // Direct array of ChatTurn
                turns = JsonConvert.DeserializeObject<List<ChatTurn>>(json);
            }
            else
            {
                // Envelope object { hostname, turns: [...] }
                var jObj = JObject.Parse(json);
                if (jObj["turns"] is JArray jArray)
                {
                    turns = jArray.ToObject<List<ChatTurn>>();
                }
            }

            if (turns != null && turns.Count > 0 && TurnsReceived != null)
            {
                await TurnsReceived.Invoke(turns).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse incoming WebSocket message as ChatTurn array.");
        }
    }

    /// <summary>
    /// Gracefully stops the WebSocket bridge and closes any active connections.
    /// </summary>
    public async Task StopAsync()
    {
        Task? listenerTask = null;
        lock (_stateLock)
        {
            if (!_isRunning) return;
            _isRunning = false;

            _bridgeCts?.Cancel();

            try
            {
                _httpListener?.Stop();
                _httpListener?.Close();
            }
            catch { }

            _activeSocket?.Dispose();
            _activeSocket = null;

            listenerTask = _listenerTask;
        }

        if (listenerTask != null)
        {
            try
            {
                await listenerTask.ConfigureAwait(false);
            }
            catch { }
        }

        _logger.LogInformation("WebSocketBridge stopped.");
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        try
        {
            StopAsync().GetAwaiter().GetResult();
        }
        catch { }

        _bridgeCts?.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        await StopAsync().ConfigureAwait(false);
        _bridgeCts?.Dispose();
    }
}
