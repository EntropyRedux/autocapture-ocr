using System.Threading.Channels;
using AutoCaptureOCR.Core.Interfaces;
using AutoCaptureOCR.Core.Models;
using AutoCaptureOCR.Core.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace AutoCaptureOCR.Core.Capture;

/// <summary>
/// Capture source that ingests conversation turns from the Chrome/Edge browser extension
/// via the local WebSocketBridge.
/// </summary>
public sealed class DomCaptureEngine : ICaptureSource, IDisposable
{
    private readonly WebSocketBridge _bridge;
    private readonly DedupEngine _dedupEngine;
    private readonly ILogger<DomCaptureEngine> _logger;
    private readonly object _stateLock = new();

    private Channel<CapturePayload>? _payloadChannel;
    private CancellationTokenSource? _engineCts;
    private List<ChatTurn> _latestTurns = new();
    private bool _isRunning;
    private bool _disposed;

    public string SourceName => "DOM Extension";
    public CaptureSourceType SourceType => CaptureSourceType.LiveTextStream;
    public bool IsRunning => _isRunning;
    public bool IsClientConnected => _bridge.IsClientConnected;

    public DomCaptureEngine(
        WebSocketBridge? bridge = null,
        DedupEngine? dedupEngine = null,
        ILogger<DomCaptureEngine>? logger = null)
    {
        _bridge = bridge ?? new WebSocketBridge();
        _dedupEngine = dedupEngine ?? new DedupEngine();
        _logger = logger ?? NullLogger<DomCaptureEngine>.Instance;
    }

    public Task<bool> IsAvailableAsync(CancellationToken ct = default)
    {
        // Localhost WebSocket bridge is always available on modern Windows
        return Task.FromResult(true);
    }

    public async Task StartAsync(CaptureSourceOptions options, CancellationToken ct = default)
    {
        lock (_stateLock)
        {
            if (_isRunning)
            {
                throw new InvalidOperationException("DomCaptureEngine is already running.");
            }

            _isRunning = true;
            _engineCts = new CancellationTokenSource();

            var channelOptions = new BoundedChannelOptions(100)
            {
                FullMode = BoundedChannelFullMode.DropOldest,
                SingleReader = false,
                SingleWriter = true
            };
            _payloadChannel = Channel.CreateBounded<CapturePayload>(channelOptions);

            _bridge.TurnsReceived += OnTurnsReceivedAsync;
        }

        if (!_bridge.IsRunning)
        {
            await _bridge.StartAsync(ct: ct).ConfigureAwait(false);
        }

        _logger.LogInformation("DomCaptureEngine started.");
    }

    private Task OnTurnsReceivedAsync(IReadOnlyList<ChatTurn> turns)
    {
        if (!_isRunning || turns == null || turns.Count == 0) return Task.CompletedTask;

        var newTurns = turns.Where(t => _dedupEngine.IsNew(t)).ToList();
        if (newTurns.Count == 0) return Task.CompletedTask;

        lock (_stateLock)
        {
            _latestTurns.AddRange(newTurns);
        }

        var payload = new CapturePayload
        {
            SourceType = CaptureSourceType.LiveTextStream,
            SourceEngine = SourceName,
            Turns = newTurns,
            Timestamp = DateTime.UtcNow
        };

        _payloadChannel?.Writer.TryWrite(payload);
        return Task.CompletedTask;
    }

    public Task<CapturePayload> CaptureOnceAsync(CancellationToken ct = default)
    {
        IReadOnlyList<ChatTurn> turnsCopy;
        lock (_stateLock)
        {
            turnsCopy = _latestTurns.ToList();
        }

        return Task.FromResult(new CapturePayload
        {
            SourceType = CaptureSourceType.LiveTextStream,
            SourceEngine = SourceName,
            Turns = turnsCopy,
            Timestamp = DateTime.UtcNow
        });
    }

    public async IAsyncEnumerable<CapturePayload> GetStreamAsync([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        ChannelReader<CapturePayload>? reader;
        lock (_stateLock)
        {
            if (!_isRunning || _payloadChannel == null)
            {
                yield break;
            }
            reader = _payloadChannel.Reader;
        }

        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, _engineCts?.Token ?? CancellationToken.None);

        while (await reader.WaitToReadAsync(linkedCts.Token).ConfigureAwait(false))
        {
            while (reader.TryRead(out var payload))
            {
                yield return payload;
            }
        }
    }

    public async Task StopAsync(CancellationToken ct = default)
    {
        lock (_stateLock)
        {
            if (!_isRunning) return;
            _isRunning = false;

            _bridge.TurnsReceived -= OnTurnsReceivedAsync;
            _engineCts?.Cancel();
            _payloadChannel?.Writer.TryComplete();
        }

        await _bridge.StopAsync().ConfigureAwait(false);
        _logger.LogInformation("DomCaptureEngine stopped.");
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

        _engineCts?.Dispose();
        _bridge.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        await StopAsync().ConfigureAwait(false);
        _engineCts?.Dispose();
        await _bridge.DisposeAsync().ConfigureAwait(false);
    }
}
