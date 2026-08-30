using System.Text.RegularExpressions;

namespace AutoCaptureOCR.Core.Services;

/// <summary>
/// Temporal text stitcher for streaming OCR.
/// Merges consecutive OCR text results across video frames, eliminating redundant lines
/// caused by scrolling or unchanged viewport regions using sliding window LCS alignment.
/// </summary>
public sealed class TextStitcher
{
    private static readonly Regex LineSplitRegex = new(@"\r?\n", RegexOptions.Compiled);
    private readonly object _lock = new();
    private readonly List<string> _recentLines = new();
    private readonly int _maxHistoryLines;
    private readonly double _similarityThreshold;

    public TextStitcher(int maxHistoryLines = 50, double similarityThreshold = 0.80)
    {
        _maxHistoryLines = maxHistoryLines;
        _similarityThreshold = similarityThreshold;
    }

    /// <summary>
    /// Processes new OCR raw text and returns only the net-new lines that were not present in previous frames.
    /// </summary>
    public IReadOnlyList<string> StitchNewLines(string? ocrText)
    {
        if (string.IsNullOrWhiteSpace(ocrText))
        {
            return Array.Empty<string>();
        }

        var incomingLines = LineSplitRegex.Split(ocrText)
            .Select(l => l.Trim())
            .Where(l => !string.IsNullOrEmpty(l))
            .ToList();

        if (incomingLines.Count == 0)
        {
            return Array.Empty<string>();
        }

        lock (_lock)
        {
            if (_recentLines.Count == 0)
            {
                // First frame: all lines are net-new
                _recentLines.AddRange(incomingLines);
                TrimHistory();
                return incomingLines;
            }

            // Find best suffix of recentLines matching a prefix of incomingLines
            int overlapLength = FindOverlap(_recentLines, incomingLines);

            List<string> newLines;
            if (overlapLength > 0 && overlapLength <= incomingLines.Count)
            {
                // Emit only the lines after the overlap
                newLines = incomingLines.Skip(overlapLength).ToList();
            }
            else
            {
                // Check if all incoming lines are already contained in recent lines
                if (IsSubsequenceContained(_recentLines, incomingLines))
                {
                    return Array.Empty<string>();
                }

                // If completely disjoint (e.g. page switch / large scroll), emit all incoming lines
                newLines = incomingLines;
            }

            if (newLines.Count > 0)
            {
                _recentLines.AddRange(newLines);
                TrimHistory();
            }

            return newLines;
        }
    }

    private int FindOverlap(List<string> history, List<string> incoming)
    {
        int maxPossible = Math.Min(history.Count, incoming.Count);

        for (int len = maxPossible; len >= 1; len--)
        {
            int historyStart = history.Count - len;
            bool match = true;

            for (int i = 0; i < len; i++)
            {
                if (!AreLinesSimilar(history[historyStart + i], incoming[i]))
                {
                    match = false;
                    break;
                }
            }

            if (match)
            {
                return len;
            }
        }

        return 0;
    }

    private bool IsSubsequenceContained(List<string> history, List<string> incoming)
    {
        if (incoming.Count > history.Count) return false;

        for (int hStart = 0; hStart <= history.Count - incoming.Count; hStart++)
        {
            bool match = true;
            for (int i = 0; i < incoming.Count; i++)
            {
                if (!AreLinesSimilar(history[hStart + i], incoming[i]))
                {
                    match = false;
                    break;
                }
            }
            if (match) return true;
        }

        return false;
    }

    private bool AreLinesSimilar(string a, string b)
    {
        if (string.Equals(a, b, StringComparison.OrdinalIgnoreCase)) return true;

        // For short lines (<= 8 chars), require exact equality to prevent false matches (e.g. "Line 1" vs "Line 2")
        int maxLen = Math.Max(a.Length, b.Length);
        if (maxLen <= 8) return false;

        int dist = ComputeLevenshteinDistance(a.ToLowerInvariant(), b.ToLowerInvariant());
        double similarity = 1.0 - ((double)dist / maxLen);

        return similarity >= _similarityThreshold;
    }

    private static int ComputeLevenshteinDistance(string s, string t)
    {
        int n = s.Length;
        int m = t.Length;
        var d = new int[n + 1, m + 1];

        if (n == 0) return m;
        if (m == 0) return n;

        for (int i = 0; i <= n; d[i, 0] = i++) { }
        for (int j = 0; j <= m; d[0, j] = j++) { }

        for (int i = 1; i <= n; i++)
        {
            for (int j = 1; j <= m; j++)
            {
                int cost = (t[j - 1] == s[i - 1]) ? 0 : 1;
                d[i, j] = Math.Min(
                    Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1),
                    d[i - 1, j - 1] + cost);
            }
        }

        return d[n, m];
    }

    private void TrimHistory()
    {
        if (_recentLines.Count > _maxHistoryLines)
        {
            int toRemove = _recentLines.Count - _maxHistoryLines;
            _recentLines.RemoveRange(0, toRemove);
        }
    }

    public void Reset()
    {
        lock (_lock)
        {
            _recentLines.Clear();
        }
    }
}
