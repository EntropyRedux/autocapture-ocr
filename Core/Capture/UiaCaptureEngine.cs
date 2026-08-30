using System.Threading.Channels;
using System.Windows.Automation;
using AutoCaptureOCR.Core.Interfaces;
using AutoCaptureOCR.Core.Models;
using AutoCaptureOCR.Core.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace AutoCaptureOCR.Core.Capture;

/// <summary>
/// Extracts conversation turns from desktop chat applications (Claude Desktop, ChatGPT)
/// via the Windows UI Automation (UIA) accessibility tree.
/// </summary>
public sealed class UiaCaptureEngine : ICaptureSource, IDisposable
{
    private readonly ILogger<UiaCaptureEngine> _logger;
    private readonly DedupEngine _dedupEngine;
    private readonly object _stateLock = new();

    private Channel<CapturePayload>? _payloadChannel;
    private CancellationTokenSource? _engineCts;
    private Task? _pollTask;
    private CaptureSourceOptions? _options;
    private bool _isRunning;
    private bool _disposed;

    public string SourceName => "Windows UI Automation";
    public CaptureSourceType SourceType => CaptureSourceType.LiveTextStream;
    public bool IsRunning => _isRunning;

    public UiaCaptureEngine(
        DedupEngine? dedupEngine = null,
        ILogger<UiaCaptureEngine>? logger = null)
    {
        _dedupEngine = dedupEngine ?? new DedupEngine();
        _logger = logger ?? NullLogger<UiaCaptureEngine>.Instance;
    }

    public Task<bool> IsAvailableAsync(CancellationToken ct = default)
    {
        try
        {
            // Verify UIA root element can be accessed
            var root = AutomationElement.RootElement;
            return Task.FromResult(root != null);
        }
        catch
        {
            return Task.FromResult(false);
        }
    }

    public Task StartAsync(CaptureSourceOptions options, CancellationToken ct = default)
    {
        lock (_stateLock)
        {
            if (_isRunning)
            {
                throw new InvalidOperationException("UIA capture engine is already running.");
            }

            _options = options ?? new CaptureSourceOptions();
            _isRunning = true;
            _engineCts = new CancellationTokenSource();

            var channelOptions = new BoundedChannelOptions(50)
            {
                FullMode = BoundedChannelFullMode.DropOldest,
                SingleReader = false,
                SingleWriter = true
            };
            _payloadChannel = Channel.CreateBounded<CapturePayload>(channelOptions);

            _pollTask = Task.Run(() => PollingLoopAsync(_engineCts.Token));
            _logger.LogInformation("UiaCaptureEngine started for HWND: {Hwnd}", _options.TargetWindowHandle);
        }

        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken ct = default)
    {
        Task? pollTask = null;
        lock (_stateLock)
        {
            if (!_isRunning) return;

            _isRunning = false;
            _engineCts?.Cancel();
            _payloadChannel?.Writer.TryComplete();
            pollTask = _pollTask;
        }

        if (pollTask != null)
        {
            try
            {
                await pollTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error occurred during UiaCaptureEngine shutdown.");
            }
        }

        _logger.LogInformation("UiaCaptureEngine stopped.");
    }

    public Task<CapturePayload> CaptureOnceAsync(CancellationToken ct = default)
    {
        IntPtr hwnd = _options?.TargetWindowHandle ?? IntPtr.Zero;
        if (hwnd == IntPtr.Zero)
        {
            return Task.FromResult(new CapturePayload
            {
                SourceType = CaptureSourceType.LiveTextStream,
                SourceEngine = SourceName,
                Turns = Array.Empty<ChatTurn>()
            });
        }

        var turns = ExtractTurnsFromWindow(hwnd);
        var payload = new CapturePayload
        {
            SourceType = CaptureSourceType.LiveTextStream,
            SourceEngine = SourceName,
            Turns = turns
        };

        return Task.FromResult(payload);
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

    private async Task PollingLoopAsync(CancellationToken ct)
    {
        var interval = _options?.PollInterval ?? TimeSpan.FromSeconds(2);

        while (!ct.IsCancellationRequested && _isRunning)
        {
            try
            {
                IntPtr hwnd = _options?.TargetWindowHandle ?? IntPtr.Zero;
                if (hwnd != IntPtr.Zero)
                {
                    var allTurns = ExtractTurnsFromWindow(hwnd);
                    var newTurns = allTurns.Where(t => _dedupEngine.IsNew(t)).ToList();

                    if (newTurns.Count > 0)
                    {
                        var payload = new CapturePayload
                        {
                            SourceType = CaptureSourceType.LiveTextStream,
                            SourceEngine = SourceName,
                            Turns = newTurns
                        };

                        _payloadChannel?.Writer.TryWrite(payload);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error occurred during UIA extraction poll.");
            }

            try
            {
                await Task.Delay(interval, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    /// <summary>
    /// Traverses the UIA tree for the given window handle and extracts text elements as ChatTurns.
    /// </summary>
    public static List<ChatTurn> ExtractTurnsFromWindow(IntPtr hwnd)
    {
        var turns = new List<ChatTurn>();
        if (hwnd == IntPtr.Zero) return turns;

        try
        {
            var windowElement = AutomationElement.FromHandle(hwnd);
            if (windowElement == null) return turns;

            // Search for document/text content elements
            var condition = new OrCondition(
                new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Document),
                new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Text),
                new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Group)
            );

            var elements = windowElement.FindAll(TreeScope.Descendants, condition);
            int turnIndex = 0;

            foreach (AutomationElement element in elements)
            {
                string text = ExtractElementText(element);
                if (string.IsNullOrWhiteSpace(text) || text.Length < 2) continue;

                // Infer role from element attributes and position
                string role = InferRole(element, windowElement);

                turns.Add(new ChatTurn
                {
                    Role = role,
                    Content = text.Trim(),
                    TurnIndex = turnIndex++,
                    Timestamp = DateTime.UtcNow
                });
            }
        }
        catch
        {
            // Graceful handling of disappearing elements or COM disconnects
        }

        return turns;
    }

    private static string ExtractElementText(AutomationElement element)
    {
        try
        {
            // Try TextPattern
            if (element.TryGetCurrentPattern(TextPattern.Pattern, out object textPatternObj) &&
                textPatternObj is TextPattern textPattern)
            {
                return textPattern.DocumentRange.GetText(-1);
            }

            // Try ValuePattern
            if (element.TryGetCurrentPattern(ValuePattern.Pattern, out object valuePatternObj) &&
                valuePatternObj is ValuePattern valuePattern)
            {
                return valuePattern.Current.Value;
            }

            // Fallback to Name property
            return element.Current.Name ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    private static string InferRole(AutomationElement element, AutomationElement windowElement)
    {
        try
        {
            string className = (element.Current.ClassName ?? "").ToLowerInvariant();
            string autoId = (element.Current.AutomationId ?? "").ToLowerInvariant();
            string name = (element.Current.Name ?? "").ToLowerInvariant();

            if (className.Contains("user") || autoId.Contains("user") || name.StartsWith("you:"))
                return "user";

            if (className.Contains("assistant") || autoId.Contains("assistant") || className.Contains("claude") || autoId.Contains("claude"))
                return "assistant";

            // Spatial heuristic: user messages are usually positioned towards the right half
            var elementRect = element.Current.BoundingRectangle;
            var windowRect = windowElement.Current.BoundingRectangle;

            if (windowRect.Width > 0 && elementRect.Width > 0)
            {
                double elementCenter = elementRect.Left + (elementRect.Width / 2.0);
                double windowCenter = windowRect.Left + (windowRect.Width / 2.0);

                if (elementCenter > windowCenter + 50)
                {
                    return "user";
                }
            }
        }
        catch { }

        return "assistant";
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
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        await StopAsync().ConfigureAwait(false);
        _engineCts?.Dispose();
    }
}
