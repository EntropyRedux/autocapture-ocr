namespace AutoCaptureOCR.Core.Models;

/// <summary>
/// Represents a global hotkey configuration
/// </summary>
public class Hotkey
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public HotkeyModifiers Modifiers { get; set; }
    public int KeyCode { get; set; }
    public bool IsEnabled { get; set; } = true;
}

/// <summary>
/// Modifier keys for hotkeys
/// </summary>
[Flags]
public enum HotkeyModifiers
{
    None = 0,
    Alt = 1,
    Control = 2,
    Shift = 4,
    Win = 8
}

/// <summary>
/// Default hotkey definitions
/// </summary>
public static class DefaultHotkeys
{
    public static List<Hotkey> GetDefaults()
    {
        return new List<Hotkey>
        {
            new Hotkey
            {
                Id = "capture_region",
                Name = "Capture Region",
                Description = "Capture a selected screen region",
                Modifiers = HotkeyModifiers.Control | HotkeyModifiers.Shift,
                KeyCode = 0x43, // C key
                IsEnabled = true
            },
            new Hotkey
            {
                Id = "capture_fullscreen",
                Name = "Capture Fullscreen",
                Description = "Capture the entire screen",
                Modifiers = HotkeyModifiers.Control | HotkeyModifiers.Shift,
                KeyCode = 0x46, // F key
                IsEnabled = true
            },
            new Hotkey
            {
                Id = "process_queue",
                Name = "Process Queue",
                Description = "Process OCR queue",
                Modifiers = HotkeyModifiers.Control | HotkeyModifiers.Shift,
                KeyCode = 0x50, // P key
                IsEnabled = true
            }
        };
    }
}
