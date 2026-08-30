using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;

namespace AutoCaptureOCR.App.Views;

public sealed record WindowInfo
{
    public required IntPtr Handle { get; init; }
    public required string Title { get; init; }
    public required string ProcessName { get; init; }
    public string HandleHex => $"0x{Handle.ToInt64():X}";
}

public partial class WindowPickerDialog : Window
{
    private readonly List<WindowInfo> _allWindows = new();

    public IntPtr SelectedWindowHandle { get; private set; } = IntPtr.Zero;
    public string SelectedWindowTitle { get; private set; } = string.Empty;

    public WindowPickerDialog()
    {
        InitializeComponent();
        RefreshWindows();
    }

    private void RefreshWindows()
    {
        _allWindows.Clear();

        EnumWindows((hwnd, lParam) =>
        {
            if (IsWindowVisible(hwnd))
            {
                int length = GetWindowTextLength(hwnd);
                if (length > 0)
                {
                    var sb = new StringBuilder(length + 1);
                    GetWindowText(hwnd, sb, sb.Capacity);
                    string title = sb.ToString().Trim();

                    if (!string.IsNullOrEmpty(title) && title != "Program Manager")
                    {
                        GetWindowThreadProcessId(hwnd, out uint pid);
                        string processName = "Unknown";
                        try
                        {
                            var proc = Process.GetProcessById((int)pid);
                            processName = proc.ProcessName;
                        }
                        catch { }

                        _allWindows.Add(new WindowInfo
                        {
                            Handle = hwnd,
                            Title = title,
                            ProcessName = processName
                        });
                    }
                }
            }
            return true;
        }, IntPtr.Zero);

        ApplyFilter();
    }

    private void ApplyFilter()
    {
        string query = SearchTextBox.Text?.Trim().ToLowerInvariant() ?? "";
        var filtered = string.IsNullOrEmpty(query)
            ? _allWindows
            : _allWindows.Where(w => w.Title.ToLowerInvariant().Contains(query) || w.ProcessName.ToLowerInvariant().Contains(query)).ToList();

        WindowsListBox.ItemsSource = filtered;
    }

    private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        ApplyFilter();
    }

    private void WindowsListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        SelectButton.IsEnabled = WindowsListBox.SelectedItem is WindowInfo;
    }

    private void WindowsListBox_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (WindowsListBox.SelectedItem is WindowInfo selected)
        {
            SelectedWindowHandle = selected.Handle;
            SelectedWindowTitle = selected.Title;
            DialogResult = true;
            Close();
        }
    }

    private void SelectButton_Click(object sender, RoutedEventArgs e)
    {
        if (WindowsListBox.SelectedItem is WindowInfo selected)
        {
            SelectedWindowHandle = selected.Handle;
            SelectedWindowTitle = selected.Title;
            DialogResult = true;
            Close();
        }
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void RefreshButton_Click(object sender, RoutedEventArgs e)
    {
        RefreshWindows();
    }

    #region Win32 P/Invoke

    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc enumProc, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern int GetWindowTextLength(IntPtr hWnd);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    #endregion
}
