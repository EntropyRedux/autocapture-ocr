using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using AutoCaptureOCR.Core.Models;

namespace AutoCaptureOCR.Core.Services;

/// <summary>
/// Content-addressable deduplication engine using SHA-256 hashing.
/// Normalizes whitespace and role headers to prevent duplicate entries in transcripts.
/// Supports optional sidecar file persistence for session crash recovery.
/// </summary>
public sealed class DedupEngine : IDisposable
{
    private static readonly Regex WhitespaceRegex = new(@"\s+", RegexOptions.Compiled);

    private readonly object _lock = new();
    private readonly HashSet<string> _seenHashes;
    private readonly string? _sidecarPath;
    private bool _dirty;
    private bool _disposed;

    public int TotalSeenCount
    {
        get
        {
            lock (_lock) return _seenHashes.Count;
        }
    }

    public DedupEngine(string? sidecarPath = null)
    {
        _sidecarPath = sidecarPath;
        _seenHashes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (!string.IsNullOrWhiteSpace(_sidecarPath) && File.Exists(_sidecarPath))
        {
            try
            {
                var lines = File.ReadAllLines(_sidecarPath);
                foreach (var line in lines)
                {
                    var trimmed = line.Trim();
                    if (!string.IsNullOrEmpty(trimmed))
                    {
                        _seenHashes.Add(trimmed);
                    }
                }
            }
            catch
            {
                // Fallback to fresh memory state if sidecar read fails
            }
        }
    }

    /// <summary>
    /// Checks if a turn is new. If new, registers its hash and returns true.
    /// Thread-safe.
    /// </summary>
    public bool IsNew(string role, string content)
    {
        if (string.IsNullOrWhiteSpace(content)) return false;

        string hash = ComputeHash(role, content);

        lock (_lock)
        {
            if (_seenHashes.Add(hash))
            {
                _dirty = true;
                return true;
            }

            return false;
        }
    }

    /// <summary>
    /// Checks if a ChatTurn is new.
    /// If turn has a MessageId, uses role + messageId for hashing; otherwise uses normalized content.
    /// </summary>
    public bool IsNew(ChatTurn turn)
    {
        if (turn == null || string.IsNullOrWhiteSpace(turn.Content)) return false;

        string key = !string.IsNullOrEmpty(turn.MessageId)
            ? $"{turn.Role}:msgid:{turn.MessageId}"
            : $"{turn.Role}:{NormalizeContent(turn.Content)}";

        string hash = HashString(key);

        lock (_lock)
        {
            if (_seenHashes.Add(hash))
            {
                _dirty = true;
                return true;
            }

            return false;
        }
    }

    /// <summary>
    /// Computes the normalized SHA-256 hash for a given role and content.
    /// </summary>
    public static string ComputeHash(string role, string content)
    {
        string normalized = $"{role.Trim().ToLowerInvariant()}:{NormalizeContent(content)}";
        return HashString(normalized);
    }

    private static string NormalizeContent(string input)
    {
        if (string.IsNullOrEmpty(input)) return string.Empty;
        return WhitespaceRegex.Replace(input.Trim(), " ");
    }

    private static string HashString(string input)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(input);
        byte[] hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    /// <summary>
    /// Persists seen hashes to the sidecar file on disk.
    /// </summary>
    public void Flush()
    {
        if (string.IsNullOrWhiteSpace(_sidecarPath)) return;

        lock (_lock)
        {
            if (!_dirty) return;

            try
            {
                var dir = Path.GetDirectoryName(_sidecarPath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                File.WriteAllLines(_sidecarPath, _seenHashes);
                _dirty = false;
            }
            catch
            {
                // Silently ignore flush errors on read-only locations
            }
        }
    }

    /// <summary>
    /// Creates or loads a DedupEngine for the given sidecar path.
    /// </summary>
    public static DedupEngine LoadOrCreate(string sidecarPath)
    {
        return new DedupEngine(sidecarPath);
    }

    public void Clear()
    {
        lock (_lock)
        {
            _seenHashes.Clear();
            _dirty = true;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Flush();
    }
}
