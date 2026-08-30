using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using AutoCaptureOCR.Core.Capture;
using AutoCaptureOCR.Core.Configuration;
using AutoCaptureOCR.Core.Interfaces;
using AutoCaptureOCR.Core.Models;
using AutoCaptureOCR.Core.OCR;
using AutoCaptureOCR.Core.Video;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace AutoCaptureOCR.Core.Services;

/// <summary>
/// Central orchestrator coordinating snapshot captures, live chat archiving,
/// and real-time video OCR sessions across all capture engines.
/// </summary>
public sealed class CaptureOrchestrator : IAsyncDisposable, IDisposable
{
    private readonly IOCREngine _ocrEngine;
    private readonly ProjectService _projectService;
    private readonly ConfigManager? _configManager;
    private readonly ILogger<CaptureOrchestrator> _logger;
    private readonly object _stateLock = new();

    private CaptureSession? _activeSession;
    private Project? _activeProject;
    private ICaptureSource? _activeSource;
    private VideoOcrPipeline? _activeVideoPipeline;
    private TranscriptWriter? _activeTranscriptWriter;
    private DedupEngine? _activeDedupEngine;
    private CancellationTokenSource? _sessionCts;
    private Task? _streamingTask;

    private int _capturedTurnsCount;
    private int _capturedWordsCount;
    private bool _isStreaming;
    private bool _disposed;

    public bool IsStreaming => _isStreaming;
    public CaptureSession? ActiveSession => _activeSession;
    public SessionType? ActiveSessionType => _activeSession?.Type;
    public int CapturedTurnsCount => _capturedTurnsCount;
    public int CapturedWordsCount => _capturedWordsCount;

    public event EventHandler<IReadOnlyList<ChatTurn>>? TurnsStreamed;
    public event EventHandler<SessionType>? StreamingStarted;
    public event EventHandler? StreamingStopped;

    public CaptureOrchestrator(
        IOCREngine? ocrEngine = null,
        ProjectService? projectService = null,
        ConfigManager? configManager = null,
        ILogger<CaptureOrchestrator>? logger = null)
    {
        _ocrEngine = ocrEngine ?? new WindowsOCREngine();
        _projectService = projectService ?? new ProjectService(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AutoCaptureOCR"));
        _configManager = configManager;
        _logger = logger ?? NullLogger<CaptureOrchestrator>.Instance;
    }

    /// <summary>
    /// Executes a single snapshot capture, saves the image to the project session,
    /// performs OCR recognition, and adds the ScreenCapture to the project.
    /// </summary>
    public async Task<ScreenCapture> CaptureSnapshotAsync(
        Project project,
        CaptureSession session,
        ICaptureSource? source = null,
        CaptureSourceOptions? options = null,
        CancellationToken ct = default)
    {
        if (project == null) throw new ArgumentNullException(nameof(project));
        if (session == null) throw new ArgumentNullException(nameof(session));

        var captureSource = source ?? new CaptureManager();
        options ??= new CaptureSourceOptions();

        _logger.LogInformation("Capturing snapshot for session: {SessionName}", session.Name);

        var payload = await captureSource.CaptureOnceAsync(ct).ConfigureAwait(false);
        if (payload.Frame == null)
        {
            throw new InvalidOperationException("Capture source did not return image frame data.");
        }

        string sessionDir = Path.Combine(project.SavePath, "captures", session.Name);
        if (!Directory.Exists(sessionDir))
        {
            Directory.CreateDirectory(sessionDir);
        }

        string fileName = $"capture_{session.Captures.Count + 1:D3}_{DateTime.UtcNow:yyyyMMdd_HHmmss}.png";
        string filePath = Path.Combine(sessionDir, fileName);

        payload.Frame.Save(filePath, ImageFormat.Png);

        // Perform OCR
        OCRResult? ocrResult = null;
        try
        {
            ocrResult = await _ocrEngine.ProcessAsync(payload.Frame).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "OCR processing failed for captured snapshot.");
        }

        var capture = _projectService.AddCapture(project, session, filePath);
        capture.SourceEngine = payload.SourceEngine;
        if (ocrResult != null)
        {
            _projectService.UpdateCaptureOCR(project, capture, ocrResult);
        }

        payload.Frame.Dispose();

        return capture;
    }

    /// <summary>
    /// Starts a live chat archiving session from a DOM extension or UIA source.
    /// </summary>
    public async Task StartLiveChatSessionAsync(
        Project project,
        CaptureSession session,
        ICaptureSource source,
        string? transcriptPath = null,
        CancellationToken ct = default)
    {
        lock (_stateLock)
        {
            if (_isStreaming)
            {
                throw new InvalidOperationException("A streaming capture session is already active.");
            }

            _isStreaming = true;
            _activeProject = project;
            _activeSession = session;
            _activeSource = source;
            _capturedTurnsCount = 0;
            _capturedWordsCount = 0;
            _sessionCts = new CancellationTokenSource();
        }

        session.Type = SessionType.LiveChat;
        session.SourceEngine = source.SourceName;

        string sessionDir = Path.Combine(project.SavePath, "transcripts", session.Name);
        if (!Directory.Exists(sessionDir))
        {
            Directory.CreateDirectory(sessionDir);
        }

        string mdPath = transcriptPath ?? Path.Combine(sessionDir, $"{session.Name}_transcript.md");
        session.TranscriptFilePath = mdPath;

        _activeTranscriptWriter = new TranscriptWriter(mdPath, title: session.Name, source: source.SourceName);
        await _activeTranscriptWriter.EnsureInitializedAsync(ct).ConfigureAwait(false);

        _activeDedupEngine = new DedupEngine(Path.Combine(sessionDir, $"{session.Name}.hashes"));

        var options = new CaptureSourceOptions();
        await source.StartAsync(options, ct).ConfigureAwait(false);

        _streamingTask = Task.Run(() => ProcessLiveChatStreamAsync(_sessionCts.Token));
        StreamingStarted?.Invoke(this, SessionType.LiveChat);

        _logger.LogInformation("LiveChat session started -> {Transcript}", mdPath);
    }

    private async Task ProcessLiveChatStreamAsync(CancellationToken ct)
    {
        if (_activeSource == null || _activeTranscriptWriter == null) return;

        try
        {
            await foreach (var payload in _activeSource.GetStreamAsync(ct).ConfigureAwait(false))
            {
                if (payload.Turns != null && payload.Turns.Count > 0)
                {
                    var newTurns = _activeDedupEngine != null
                        ? payload.Turns.Where(t => _activeDedupEngine.IsNew(t)).ToList()
                        : payload.Turns.ToList();

                    if (newTurns.Count > 0)
                    {
                        await _activeTranscriptWriter.AppendTurnsAsync(newTurns, ct).ConfigureAwait(false);

                        lock (_stateLock)
                        {
                            _capturedTurnsCount += newTurns.Count;
                            _capturedWordsCount += newTurns.Sum(t => CountWords(t.Content));
                        }

                        TurnsStreamed?.Invoke(this, newTurns);
                    }
                }
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing live chat stream.");
        }
    }

    /// <summary>
    /// Starts a video recording session with real-time frame-differenced OCR index.
    /// </summary>
    public async Task StartVideoSessionAsync(
        Project project,
        CaptureSession session,
        IntPtr targetHwnd,
        string? videoPath = null,
        string? transcriptPath = null,
        VideoSettings? settings = null,
        CancellationToken ct = default)
    {
        lock (_stateLock)
        {
            if (_isStreaming)
            {
                throw new InvalidOperationException("A streaming capture session is already active.");
            }

            _isStreaming = true;
            _activeProject = project;
            _activeSession = session;
            _sessionCts = new CancellationTokenSource();
        }

        session.Type = SessionType.VideoRecording;
        session.SourceEngine = "Windows Graphics Capture";

        string videoDir = Path.Combine(project.SavePath, "recordings", session.Name);
        if (!Directory.Exists(videoDir))
        {
            Directory.CreateDirectory(videoDir);
        }

        string mp4Path = videoPath ?? Path.Combine(videoDir, $"{session.Name}.mp4");
        string mdPath = transcriptPath ?? Path.Combine(videoDir, $"{session.Name}_index.md");

        session.VideoFilePath = mp4Path;
        session.TranscriptFilePath = mdPath;

        _activeVideoPipeline = new VideoOcrPipeline(_activeSource, _ocrEngine);
        await _activeVideoPipeline.StartAsync(targetHwnd, mp4Path, mdPath, settings, ct).ConfigureAwait(false);

        StreamingStarted?.Invoke(this, SessionType.VideoRecording);
        _logger.LogInformation("VideoRecording session started -> MP4: {Video}, MD: {Transcript}", mp4Path, mdPath);
    }

    /// <summary>
    /// Gracefully stops any active streaming session (LiveChat or VideoRecording).
    /// </summary>
    public async Task StopActiveSessionAsync()
    {
        Task? streamTask = null;
        ICaptureSource? source = null;
        VideoOcrPipeline? videoPipeline = null;
        TranscriptWriter? writer = null;
        DedupEngine? dedup = null;

        lock (_stateLock)
        {
            if (!_isStreaming) return;
            _isStreaming = false;

            _sessionCts?.Cancel();
            streamTask = _streamingTask;
            source = _activeSource;
            videoPipeline = _activeVideoPipeline;
            writer = _activeTranscriptWriter;
            dedup = _activeDedupEngine;

            _activeSource = null;
            _activeVideoPipeline = null;
            _activeTranscriptWriter = null;
            _activeDedupEngine = null;
            _activeSession = null;
            _activeProject = null;
        }

        if (streamTask != null)
        {
            try
            {
                await streamTask.ConfigureAwait(false);
            }
            catch { }
        }

        if (source != null)
        {
            await source.StopAsync().ConfigureAwait(false);
        }

        if (videoPipeline != null)
        {
            await videoPipeline.StopAsync().ConfigureAwait(false);
            videoPipeline.Dispose();
        }

        if (writer != null)
        {
            await writer.DisposeAsync().ConfigureAwait(false);
        }

        if (dedup != null)
        {
            dedup.Flush();
            dedup.Dispose();
        }

        StreamingStopped?.Invoke(this, EventArgs.Empty);
        _logger.LogInformation("CaptureOrchestrator active session stopped.");
    }

    private static int CountWords(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return 0;
        return text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        try
        {
            StopActiveSessionAsync().GetAwaiter().GetResult();
        }
        catch { }

        _sessionCts?.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        await StopActiveSessionAsync().ConfigureAwait(false);
        _sessionCts?.Dispose();
    }
}
