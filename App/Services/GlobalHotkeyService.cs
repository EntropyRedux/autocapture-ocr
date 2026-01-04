using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using AutoCaptureOCR.Core.Models;

namespace AutoCaptureOCR.App.Services;

/// <summary>
/// Manages global system hotkeys for the application
/// </summary>
public class GlobalHotkeyService : IDisposable
{
    private const int WM_HOTKEY = 0x0312;
    private readonly Dictionary<int, Hotkey> registeredHotkeys = new();
    private IntPtr windowHandle;
    private HwndSource? hwndSource;
    private int nextHotkeyId = 1;

    public event EventHandler<HotkeyPressedEventArgs>? HotkeyPressed;

    [DllImport("user32.dll")]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll")]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    /// <summary>
    /// Initialize the service with the window handle
    /// </summary>
    public void Initialize(Window window)
    {
        var helper = new WindowInteropHelper(window);
        windowHandle = helper.Handle;

        if (windowHandle == IntPtr.Zero)
        {
            // Window not yet created, wait for it
            window.SourceInitialized += (s, e) =>
            {
                windowHandle = new WindowInteropHelper(window).Handle;
                SetupHookSource();
            };
        }
        else
        {
            SetupHookSource();
        }
    }

    private void SetupHookSource()
    {
        hwndSource = HwndSource.FromHwnd(windowHandle);
        if (hwndSource != null)
        {
            hwndSource.AddHook(WndProc);
        }
    }

    /// <summary>
    /// Register a global hotkey
    /// </summary>
    public bool RegisterHotkey(Hotkey hotkey)
    {
        if (!hotkey.IsEnabled || windowHandle == IntPtr.Zero)
            return false;

        try
        {
            var hotkeyId = nextHotkeyId++;
            var modifiers = (uint)hotkey.Modifiers;
            var keyCode = (uint)hotkey.KeyCode;

            if (RegisterHotKey(windowHandle, hotkeyId, modifiers, keyCode))
            {
                registeredHotkeys[hotkeyId] = hotkey;
                return true;
            }

            return false;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Unregister a specific hotkey
    /// </summary>
    public void UnregisterHotkey(int hotkeyId)
    {
        if (registeredHotkeys.ContainsKey(hotkeyId))
        {
            UnregisterHotKey(windowHandle, hotkeyId);
            registeredHotkeys.Remove(hotkeyId);
        }
    }

    /// <summary>
    /// Unregister all hotkeys
    /// </summary>
    public void UnregisterAll()
    {
        foreach (var id in registeredHotkeys.Keys.ToList())
        {
            UnregisterHotKey(windowHandle, id);
        }
        registeredHotkeys.Clear();
    }

    /// <summary>
    /// Register multiple hotkeys
    /// </summary>
    public void RegisterHotkeys(IEnumerable<Hotkey> hotkeys)
    {
        foreach (var hotkey in hotkeys)
        {
            RegisterHotkey(hotkey);
        }
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_HOTKEY)
        {
            var hotkeyId = wParam.ToInt32();
            if (registeredHotkeys.TryGetValue(hotkeyId, out var hotkey))
            {
                HotkeyPressed?.Invoke(this, new HotkeyPressedEventArgs(hotkey));
                handled = true;
            }
        }

        return IntPtr.Zero;
    }

    public void Dispose()
    {
        UnregisterAll();
        if (hwndSource != null)
        {
            hwndSource.RemoveHook(WndProc);
        }
    }
}

/// <summary>
/// Event args for hotkey pressed events
/// </summary>
public class HotkeyPressedEventArgs : EventArgs
{
    public Hotkey Hotkey { get; }

    public HotkeyPressedEventArgs(Hotkey hotkey)
    {
        Hotkey = hotkey;
    }
}
