using System.IO;
using System.Text;
using AutoCaptureOCR.Core.Models;

namespace AutoCaptureOCR.Core.Services;

/// <summary>
/// Append-only Markdown transcript writer that manages session transcripts with YAML frontmatter.
/// Opens files with FileShare.Read so external markdown readers (Obsidian, VS Code) can read in real-time.
/// </summary>
public sealed class TranscriptWriter : IAsyncDisposable, IDisposable
{
    private readonly string _filePath;
    private readonly string _title;
    private readonly string _source;
    private readonly object _lock = new();
    private readonly SemaphoreSlim _asyncLock = new(1, 1);
    private bool _initialized;
    private bool _disposed;

    public string FilePath => _filePath;

    public TranscriptWriter(string filePath, string title = "Archived Conversation", string source = "AutoCapture-OCR")
    {
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("Transcript file path cannot be empty.", nameof(filePath));

        _filePath = filePath;
        _title = title;
        _source = source;
    }

    /// <summary>
    /// Ensures the file and its YAML frontmatter are initialized.
    /// </summary>
    public async Task EnsureInitializedAsync(CancellationToken ct = default)
    {
        if (_initialized) return;

        await _asyncLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (_initialized) return;

            var dir = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            if (!File.Exists(_filePath))
            {
                var sb = new StringBuilder();
                sb.AppendLine("---");
                sb.AppendLine($"title: \"{EscapeYaml(_title)}\"");
                sb.AppendLine($"source: \"{EscapeYaml(_source)}\"");
                sb.AppendLine($"created: {DateTime.UtcNow:yyyy-MM-ddTHH:mm:ssZ}");
                sb.AppendLine("generator: AutoCapture-OCR v3.0");
                sb.AppendLine("---");
                sb.AppendLine();

                await WriteTextWithRetryAsync(sb.ToString(), append: false, ct).ConfigureAwait(false);
            }

            _initialized = true;
        }
        finally
        {
            _asyncLock.Release();
        }
    }

    /// <summary>
    /// Appends a collection of chat turns to the transcript in Markdown format.
    /// </summary>
    public async Task AppendTurnsAsync(IEnumerable<ChatTurn> turns, CancellationToken ct = default)
    {
        if (turns == null) return;
        var list = turns.ToList();
        if (list.Count == 0) return;

        await EnsureInitializedAsync(ct).ConfigureAwait(false);

        await _asyncLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var sb = new StringBuilder();
            foreach (var turn in list)
            {
                string heading = FormatRoleHeading(turn.Role);
                sb.AppendLine($"### {heading}");
                sb.AppendLine();
                sb.AppendLine(turn.Content.Trim());
                sb.AppendLine();
            }

            await WriteTextWithRetryAsync(sb.ToString(), append: true, ct).ConfigureAwait(false);
        }
        finally
        {
            _asyncLock.Release();
        }
    }

    /// <summary>
    /// Appends a single timestamped raw text block (e.g. from Video OCR).
    /// </summary>
    public async Task AppendTimestampedTextAsync(
        DateTime timestamp,
        TimeSpan? videoOffset,
        string text,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(text)) return;

        await EnsureInitializedAsync(ct).ConfigureAwait(false);

        await _asyncLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var sb = new StringBuilder();
            string offsetStr = videoOffset.HasValue
                ? $"[{videoOffset.Value:hh\\:mm\\:ss}] "
                : "";

            sb.AppendLine($"#### {offsetStr}{timestamp:yyyy-MM-dd HH:mm:ss} UTC");
            sb.AppendLine();
            sb.AppendLine(text.Trim());
            sb.AppendLine();

            await WriteTextWithRetryAsync(sb.ToString(), append: true, ct).ConfigureAwait(false);
        }
        finally
        {
            _asyncLock.Release();
        }
    }

    private static string FormatRoleHeading(string role)
    {
        return role.Trim().ToLowerInvariant() switch
        {
            "user" => "You",
            "assistant" => "Assistant",
            "system" => "System",
            _ => char.ToUpperInvariant(role[0]) + role[1..]
        };
    }

    private static string EscapeYaml(string input)
    {
        return input.Replace("\"", "\\\"");
    }

    private async Task WriteTextWithRetryAsync(string text, bool append, CancellationToken ct)
    {
        var encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false); // UTF-8 without BOM

        using var stream = new FileStream(
            _filePath,
            append ? FileMode.Append : FileMode.Create,
            FileAccess.Write,
            FileShare.ReadWrite);

        using var writer = new StreamWriter(stream, encoding);
        await writer.WriteAsync(text.AsMemory(), ct).ConfigureAwait(false);
        await writer.FlushAsync(ct).ConfigureAwait(false);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _asyncLock.Dispose();
    }

    public ValueTask DisposeAsync()
    {
        Dispose();
        return ValueTask.CompletedTask;
    }
}
