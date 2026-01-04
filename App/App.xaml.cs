using System.Configuration;
using System.Data;
using System.Windows;

namespace AutoCaptureOCR.App;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Register application exit handler
        this.Exit += App_Exit;
    }

    private void App_Exit(object? sender, ExitEventArgs e)
    {
        // Find MainWindow and ensure cleanup
        if (MainWindow is MainWindow mainWindow)
        {
            var viewModel = mainWindow.DataContext as ViewModels.MainViewModel;
            if (viewModel != null && viewModel.IsInContinuousMode)
            {
                // Force stop continuous mode to cleanup overlay
                Application.Current.Dispatcher.Invoke(() =>
                {
                    viewModel.StopContinuousModeCommand.Execute(null);
                });
            }
        }
    }
}

