using System.Linq;
using System.Windows;
using System.Windows.Input;
using AutoCaptureOCR.App.Services;
using AutoCaptureOCR.App.ViewModels;
using AutoCaptureOCR.Core.Models;

namespace AutoCaptureOCR.App;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private readonly MainViewModel viewModel;
    private GlobalHotkeyService? hotkeyService;
    private NotificationService? notificationService;

    public MainWindow()
    {
        InitializeComponent();
        viewModel = new MainViewModel();
        DataContext = viewModel;

        Loaded += MainWindow_Loaded;
        Closed += MainWindow_Closed;
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        // Initialize notification service
        notificationService = new NotificationService(this);
        viewModel.NotificationService = notificationService;

        // Initialize hotkey service
        hotkeyService = new GlobalHotkeyService();
        hotkeyService.Initialize(this);
        hotkeyService.HotkeyPressed += HotkeyService_HotkeyPressed;
        viewModel.HotkeyService = hotkeyService;

        // Register default hotkeys
        var defaultHotkeys = DefaultHotkeys.GetDefaults();
        hotkeyService.RegisterHotkeys(defaultHotkeys);

        // Show welcome notification
        notificationService.ShowSuccess("AutoCapture OCR ready! Use Ctrl+Shift+C to capture region");

        // Subscribe to metadata editor close event to reset panel width
        viewModel.PropertyChanged += ViewModel_PropertyChanged;
    }

    private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.IsMetadataEditorOpen))
        {
            if (viewModel.IsMetadataEditorOpen)
            {
                // Open metadata editor at default width (400px)
                MetadataEditorColumn.Width = new System.Windows.GridLength(400);
            }
            else
            {
                // Close metadata editor (set width to 0)
                MetadataEditorColumn.Width = new System.Windows.GridLength(0);
            }
        }
    }

    private void MainWindow_Closed(object? sender, EventArgs e)
    {
        // Stop continuous mode if active (closes overlay)
        if (viewModel.IsInContinuousMode)
        {
            viewModel.StopContinuousModeCommand.Execute(null);
        }

        // Dispose hotkey service
        hotkeyService?.Dispose();
    }

    private async void HotkeyService_HotkeyPressed(object? sender, HotkeyPressedEventArgs e)
    {
        switch (e.Hotkey.Id)
        {
            case "capture_region":
                if (viewModel.CaptureRegionCommand.CanExecute(null))
                {
                    await ((AsyncRelayCommand)viewModel.CaptureRegionCommand).ExecuteAsync();
                }
                break;

            case "capture_fullscreen":
                if (viewModel.CaptureFullscreenCommand.CanExecute(null))
                {
                    await ((AsyncRelayCommand)viewModel.CaptureFullscreenCommand).ExecuteAsync();
                }
                break;

            case "process_queue":
                if (viewModel.ProcessQueueCommand.CanExecute(null))
                {
                    viewModel.ProcessQueueCommand.Execute(null);
                }
                break;

            case "continuous_capture_f9":
                if (viewModel.CaptureLockedRegionCommand.CanExecute(null))
                {
                    await ((AsyncRelayCommand)viewModel.CaptureLockedRegionCommand).ExecuteAsync();
                }
                break;
        }
    }

    private void Thumbnail_Click(object sender, MouseButtonEventArgs e)
    {
        // Thumbnail click now just selects the item - no modal dialog
        // The ListBox selection will trigger CapturesListBox_SelectionChanged
        // which updates the OCR panel and shows action buttons
    }

    private void CapturesListBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (sender is System.Windows.Controls.ListBox listBox && DataContext is MainViewModel vm)
        {
            vm.UpdateSelectedCaptures(listBox.SelectedItems);
        }
    }

    private void MetadataValue_LostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.TextBox textBox &&
            DataContext is MainViewModel vm &&
            vm.SelectedCaptures.Count == 1)
        {
            var key = textBox.Tag as string;
            var newValue = textBox.Text;

            if (!string.IsNullOrEmpty(key))
            {
                var capture = vm.SelectedCaptures[0];
                if (capture.Metadata.ContainsKey(key))
                {
                    capture.Metadata[key] = newValue;
                    // Save changes
                    if (vm.SelectedProject != null)
                    {
                        // The project service will handle saving
                        vm.StatusText = $"Updated '{key}' metadata";
                    }
                }
            }
        }
    }

    private void QuickTag_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm)
        {
            var selectedItem = ElementTypeComboBox.SelectedItem as System.Windows.Controls.ComboBoxItem;
            if (selectedItem != null)
            {
                var elementType = selectedItem.Content.ToString();
                if (!string.IsNullOrEmpty(elementType))
                {
                    // Add "ElementType" metadata field with the selected value
                    vm.NewMetadataKey = "ElementType";
                    vm.NewMetadataValue = elementType;
                    vm.AddMetadataFieldCommand.Execute(null);

                    // Clear the selection for next use
                    ElementTypeComboBox.SelectedIndex = -1;

                    vm.StatusText = $"Tagged as '{elementType}'";
                    notificationService?.ShowSuccess($"Tagged as '{elementType}'");
                }
            }
            else
            {
                notificationService?.ShowWarning("Please select an element type first");
            }
        }
    }
}
