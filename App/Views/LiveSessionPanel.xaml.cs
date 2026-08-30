using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using AutoCaptureOCR.Core.Models;

namespace AutoCaptureOCR.App.Views;

public partial class LiveSessionPanel : UserControl
{
    private readonly DispatcherTimer _durationTimer;
    private DateTime _sessionStartTime;
    private int _turnsCount;
    private int _wordsCount;
    private readonly StringBuilder _textBuffer = new();

    public event EventHandler? StopRequested;

    public LiveSessionPanel()
    {
        InitializeComponent();

        _durationTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _durationTimer.Tick += DurationTimer_Tick;
    }

    public void StartSession(SessionType type, string sessionName)
    {
        _sessionStartTime = DateTime.UtcNow;
        _turnsCount = 0;
        _wordsCount = 0;
        _textBuffer.Clear();

        SessionTypeTextBlock.Text = type switch
        {
            SessionType.LiveChat => "💬 LIVE CHAT ARCHIVE",
            SessionType.VideoRecording => "🎬 VIDEO RECORDING & OCR",
            _ => "📸 LIVE SESSION"
        };
        SessionNameTextBlock.Text = $" - {sessionName}";

        LiveTextBox.Text = string.Empty;
        UpdateStatsDisplay();

        _durationTimer.Start();
        Visibility = Visibility.Visible;
    }

    public void StopSession()
    {
        _durationTimer.Stop();
        Visibility = Visibility.Collapsed;
    }

    public void AppendTurns(IReadOnlyList<ChatTurn> turns)
    {
        if (turns == null || turns.Count == 0) return;

        Dispatcher.Invoke(() =>
        {
            foreach (var turn in turns)
            {
                _turnsCount++;
                _wordsCount += CountWords(turn.Content);

                string heading = turn.Role.Equals("user", StringComparison.OrdinalIgnoreCase) ? "You" : "Assistant";
                _textBuffer.AppendLine($"[{heading}] ({turn.Timestamp:HH:mm:ss})");
                _textBuffer.AppendLine(turn.Content.Trim());
                _textBuffer.AppendLine(new string('-', 40));
                _textBuffer.AppendLine();
            }

            LiveTextBox.Text = _textBuffer.ToString();
            LiveTextBox.CaretIndex = LiveTextBox.Text.Length;
            TextScrollViewer.ScrollToEnd();

            UpdateStatsDisplay();
        });
    }

    public void AppendText(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return;

        Dispatcher.Invoke(() =>
        {
            _textBuffer.AppendLine(text.Trim());
            _textBuffer.AppendLine();

            LiveTextBox.Text = _textBuffer.ToString();
            LiveTextBox.CaretIndex = LiveTextBox.Text.Length;
            TextScrollViewer.ScrollToEnd();

            UpdateStatsDisplay();
        });
    }

    private void DurationTimer_Tick(object? sender, EventArgs e)
    {
        UpdateStatsDisplay();
    }

    private void UpdateStatsDisplay()
    {
        var elapsed = DateTime.UtcNow - _sessionStartTime;
        StatsTextBlock.Text = $"Turns: {_turnsCount} | Words: {_wordsCount} | Duration: {elapsed:mm\\:ss}";
    }

    private static int CountWords(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return 0;
        return text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length;
    }

    private void StopButton_Click(object sender, RoutedEventArgs e)
    {
        StopRequested?.Invoke(this, EventArgs.Empty);
    }
}
