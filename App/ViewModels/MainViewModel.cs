using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Input;
using AutoCaptureOCR.App.Views;
using AutoCaptureOCR.Core.Capture;
using AutoCaptureOCR.Core.Configuration;
using AutoCaptureOCR.Core.Interfaces;
using AutoCaptureOCR.Core.Models;
using AutoCaptureOCR.Core.OCR;
using AutoCaptureOCR.Core.Services;

namespace AutoCaptureOCR.App.ViewModels;

public class MainViewModel : BaseViewModel
{
    private readonly ProjectService projectService;
    private readonly CaptureManager captureManager;
    private readonly IOCREngine ocrEngine;
    private readonly ConfigManager configManager;
    private readonly MetadataService metadataService;
    private readonly AnalyticsService analyticsService;
    private readonly TemplateService templateService;
    private readonly ConcurrentQueue<(Project project, CaptureSession session, ScreenCapture capture)> ocrQueue = new();
    private CancellationTokenSource? queueCancellationTokenSource;
    private Task? queueProcessorTask;

    private Project? selectedProject;
    private CaptureSession? selectedSession;
    private string statusText = "Ready";
    private int queueCount = 0;
    private bool isProcessingQueue = false;

    public ObservableCollection<Project> Projects { get; } = new();

    public Project? SelectedProject
    {
        get => selectedProject;
        set
        {
            if (SetProperty(ref selectedProject, value))
            {
                OnPropertyChanged(nameof(HasNoCaptures));
                // Auto-select first session if available
                if (selectedProject?.Sessions.Count > 0)
                {
                    SelectedSession = selectedProject.Sessions[0];
                }
            }
        }
    }

    public CaptureSession? SelectedSession
    {
        get => selectedSession;
        set
        {
            if (SetProperty(ref selectedSession, value))
            {
                OnPropertyChanged(nameof(HasNoCaptures));
            }
        }
    }

    public string StatusText
    {
        get => statusText;
        set => SetProperty(ref statusText, value);
    }

    public int QueueCount
    {
        get => queueCount;
        set => SetProperty(ref queueCount, value);
    }

    public bool IsProcessingQueue
    {
        get => isProcessingQueue;
        set
        {
            if (SetProperty(ref isProcessingQueue, value))
            {
                ((RelayCommand)ProcessQueueCommand).RaiseCanExecuteChanged();
            }
        }
    }

    public bool HasNoCaptures => SelectedSession == null || SelectedSession.Captures.Count == 0;

    // Multi-select captures and combined OCR
    private ObservableCollection<ScreenCapture> selectedCaptures = new();
    private string combinedOCRText = "";
    private string currentDisplayMode = "continuous";

    public ObservableCollection<ScreenCapture> SelectedCaptures
    {
        get => selectedCaptures;
        set
        {
            if (SetProperty(ref selectedCaptures, value))
            {
                UpdateCombinedOCRText();
            }
        }
    }

    public string CombinedOCRText
    {
        get => combinedOCRText;
        private set => SetProperty(ref combinedOCRText, value);
    }

    public string CurrentDisplayMode
    {
        get => currentDisplayMode;
        set
        {
            if (SetProperty(ref currentDisplayMode, value))
            {
                UpdateCombinedOCRText();
            }
        }
    }

    public List<string> DisplayModes { get; } = new() { "continuous", "lines", "structured", "json" };

    // Inline Metadata Editor
    private bool isMetadataEditorOpen = false;
    private string newMetadataKey = "";
    private string newMetadataValue = "";
    private ObservableCollection<AutoCaptureOCR.App.Models.MetadataField> currentMetadataFields = new();

    public bool IsMetadataEditorOpen
    {
        get => isMetadataEditorOpen;
        set
        {
            if (SetProperty(ref isMetadataEditorOpen, value) && value)
            {
                // Refresh metadata fields when opening
                RefreshMetadataFields();
            }
        }
    }

    public string NewMetadataKey
    {
        get => newMetadataKey;
        set => SetProperty(ref newMetadataKey, value);
    }

    public string NewMetadataValue
    {
        get => newMetadataValue;
        set => SetProperty(ref newMetadataValue, value);
    }

    public ObservableCollection<AutoCaptureOCR.App.Models.MetadataField> CurrentMetadataFields
    {
        get => currentMetadataFields;
        set => SetProperty(ref currentMetadataFields, value);
    }

    // Continuous Capture Mode
    private bool isInContinuousMode = false;
    private LockedRegion? lockedRegion = null;
    private System.Timers.Timer? autoCaptureTimer = null;
    private bool isAutoCapturing = false;
    private int autoCaptureInterval = 10; // seconds
    private LockedRegionOverlay? regionOverlay = null;

    public bool IsInContinuousMode
    {
        get => isInContinuousMode;
        set => SetProperty(ref isInContinuousMode, value);
    }

    public LockedRegion? LockedRegion
    {
        get => lockedRegion;
        set
        {
            if (SetProperty(ref lockedRegion, value))
            {
                OnPropertyChanged(nameof(LockedRegionInfo));
                OnPropertyChanged(nameof(CaptureCountText));
            }
        }
    }

    public bool IsAutoCapturing
    {
        get => isAutoCapturing;
        set => SetProperty(ref isAutoCapturing, value);
    }

    public int AutoCaptureInterval
    {
        get => autoCaptureInterval;
        set => SetProperty(ref autoCaptureInterval, value);
    }

    public string LockedRegionInfo => LockedRegion?.GetCoordinatesString() ?? "No region locked";
    public string CaptureCountText => LockedRegion != null ? $"{LockedRegion.CaptureCount} captures" : "0 captures";

    // Capture Mode
    private Core.Models.CaptureMode currentCaptureMode = Core.Models.CaptureMode.CaptureAndOcr;

    public Core.Models.CaptureMode CurrentCaptureMode
    {
        get => currentCaptureMode;
        set
        {
            if (SetProperty(ref currentCaptureMode, value))
            {
                OnPropertyChanged(nameof(CaptureModeText));
                OnPropertyChanged(nameof(CaptureModeIcon));
            }
        }
    }

    public string CaptureModeText => CurrentCaptureMode switch
    {
        Core.Models.CaptureMode.CaptureAndOcr => "Capture + OCR",
        Core.Models.CaptureMode.CaptureOnly => "Capture Only",
        _ => "Unknown"
    };

    public string CaptureModeIcon => CurrentCaptureMode switch
    {
        Core.Models.CaptureMode.CaptureAndOcr => "📸+📝",
        Core.Models.CaptureMode.CaptureOnly => "📸",
        _ => "❓"
    };

    public MetadataService MetadataService => metadataService;
    public AnalyticsService AnalyticsService => analyticsService;
    public TemplateService TemplateService => templateService;

    public Services.NotificationService? NotificationService { get; set; }
    public Services.GlobalHotkeyService? HotkeyService { get; set; }

    // Commands
    public ICommand CreateProjectCommand { get; }
    public ICommand CreateSessionCommand { get; }
    public ICommand EditProjectCommand { get; }
    public ICommand DeleteProjectCommand { get; }
    public ICommand EditSessionCommand { get; }
    public ICommand DeleteSessionCommand { get; }
    public ICommand DeleteCapturesCommand { get; }
    public ICommand CaptureRegionCommand { get; }
    public ICommand CaptureFullscreenCommand { get; }
    public ICommand ProcessQueueCommand { get; }
    public ICommand ExportProjectCommand { get; }
    public ICommand ExportSessionCommand { get; }
    public ICommand ViewAnalyticsCommand { get; }
    public ICommand OpenTemplateManagerCommand { get; }
    public ICommand OpenSettingsCommand { get; }
    public ICommand StartContinuousModeCommand { get; }
    public ICommand StopContinuousModeCommand { get; }
    public ICommand CaptureLockedRegionCommand { get; }
    public ICommand ToggleAutoCaptureCommand { get; }
    public ICommand CycleCaptureModeCommand { get; }
    public ICommand ProcessSelectedOCRCommand { get; }
    public ICommand ViewImageCommand { get; }
    public ICommand ViewFolderCommand { get; }
    public ICommand EditMetadataCommand { get; }
    public ICommand AddMetadataFieldCommand { get; }
    public ICommand RemoveMetadataFieldCommand { get; }

    public MainViewModel()
    {
        // Initialize services
        var appDataPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "AutoCaptureOCR"
        );
        Directory.CreateDirectory(appDataPath);

        projectService = new ProjectService(appDataPath);
        captureManager = new CaptureManager();
        ocrEngine = new WindowsOCREngine();
        configManager = new ConfigManager();
        templateService = new TemplateService(appDataPath);
        metadataService = new MetadataService(templateService);
        analyticsService = new AnalyticsService();

        // Initialize commands
        CreateProjectCommand = new RelayCommand(CreateProject);
        CreateSessionCommand = new RelayCommand(CreateSession, _ => SelectedProject != null);
        EditProjectCommand = new RelayCommand(EditProject, _ => SelectedProject != null);
        DeleteProjectCommand = new RelayCommand(DeleteProject, _ => SelectedProject != null);
        EditSessionCommand = new RelayCommand(EditSession, _ => SelectedSession != null);
        DeleteSessionCommand = new RelayCommand(DeleteSession, _ => SelectedSession != null);
        DeleteCapturesCommand = new RelayCommand(DeleteCaptures, _ => SelectedCaptures.Count > 0);
        CaptureRegionCommand = new AsyncRelayCommand(CaptureRegionAsync, () => SelectedSession != null);
        CaptureFullscreenCommand = new AsyncRelayCommand(CaptureFullscreenAsync, () => SelectedSession != null);
        ProcessQueueCommand = new RelayCommand(ProcessQueue, _ => QueueCount > 0 && !IsProcessingQueue);
        ExportProjectCommand = new RelayCommand(ExportProject, _ => SelectedProject != null);
        ExportSessionCommand = new RelayCommand(ExportSession, _ => SelectedSession != null);
        ViewAnalyticsCommand = new RelayCommand(ViewAnalytics, _ => SelectedProject != null);
        OpenTemplateManagerCommand = new RelayCommand(OpenTemplateManager);
        OpenSettingsCommand = new RelayCommand(OpenSettings);
        StartContinuousModeCommand = new AsyncRelayCommand(StartContinuousModeAsync, () => SelectedSession != null && !IsInContinuousMode);
        StopContinuousModeCommand = new RelayCommand(StopContinuousMode, _ => IsInContinuousMode);
        CaptureLockedRegionCommand = new AsyncRelayCommand(CaptureLockedRegionAsync, () => IsInContinuousMode && LockedRegion != null);
        ToggleAutoCaptureCommand = new RelayCommand(ToggleAutoCapture, _ => IsInContinuousMode);
        CycleCaptureModeCommand = new RelayCommand(CycleCaptureMode);
        ProcessSelectedOCRCommand = new AsyncRelayCommand(ProcessSelectedOCRAsync, () => SelectedCaptures.Count > 0);
        ViewImageCommand = new RelayCommand(ViewImage, _ => SelectedCaptures.Count == 1);
        ViewFolderCommand = new RelayCommand(ViewFolder, _ => SelectedCaptures.Count >= 1);
        EditMetadataCommand = new RelayCommand(EditMetadata, _ => SelectedCaptures.Count == 1);
        AddMetadataFieldCommand = new RelayCommand(AddMetadataField, _ => SelectedCaptures.Count == 1 && !string.IsNullOrWhiteSpace(NewMetadataKey));
        RemoveMetadataFieldCommand = new RelayCommand(RemoveMetadataField);

        // Start background queue processor
        StartBackgroundQueueProcessor();

        // Load existing projects
        LoadProjects();

        // If no projects exist, create a default one
        if (Projects.Count == 0)
        {
            var defaultProject = projectService.CreateProject("My First Project", "Default project for screen captures");
            Projects.Add(defaultProject);
            SelectedProject = defaultProject;

            // Create default session
            var defaultSession = projectService.CreateSession(defaultProject, "Session 1");
            SelectedSession = defaultSession;

            StatusText = "Welcome! Created default project and session. Click capture to get started.";
        }
        else
        {
            StatusText = $"Loaded {Projects.Count} projects. Select a project and session to begin.";
        }
    }

    private void LoadProjects()
    {
        Projects.Clear();
        var projects = projectService.GetAllProjects();
        foreach (var project in projects)
        {
            Projects.Add(project);
        }

        if (Projects.Count > 0)
        {
            SelectedProject = Projects[0];
        }
    }

    private void CreateProject(object? parameter)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            var dialog = new Views.CreateProjectDialog($"Project {Projects.Count + 1}");
            if (dialog.ShowDialog() == true)
            {
                var project = projectService.CreateProject(dialog.ProjectName, dialog.Description);
                Projects.Add(project);
                SelectedProject = project;

                // Create default first session
                var session = projectService.CreateSession(project, "Session 1");
                SelectedSession = session;

                StatusText = $"Created project '{dialog.ProjectName}' with first session";
            }
        });
    }

    private void CreateSession(object? parameter)
    {
        if (SelectedProject == null)
            return;

        Application.Current.Dispatcher.Invoke(() =>
        {
            var dialog = new Views.CreateSessionDialog(
                SelectedProject.Name,
                $"Session {SelectedProject.Sessions.Count + 1}"
            );

            if (dialog.ShowDialog() == true)
            {
                var session = projectService.CreateSession(SelectedProject, dialog.SessionName);
                SelectedSession = session;

                // Refresh UI
                OnPropertyChanged(nameof(SelectedProject));

                StatusText = $"Created session '{dialog.SessionName}'";
            }
        });
    }

    private void EditProject(object? parameter)
    {
        if (SelectedProject == null)
            return;

        Application.Current.Dispatcher.Invoke(() =>
        {
            var dialog = new Views.CreateProjectDialog(SelectedProject.Name);
            if (dialog.ShowDialog() == true)
            {
                // Store reference and index
                var projectToUpdate = SelectedProject;
                var projectIndex = Projects.IndexOf(projectToUpdate);

                // Update the name and save
                projectToUpdate.Name = dialog.ProjectName;
                projectService.SaveProject(projectToUpdate);

                // Clear selection first
                SelectedProject = null;

                // Replace item in collection to trigger UI update
                Projects.RemoveAt(projectIndex);
                Projects.Insert(projectIndex, projectToUpdate);

                // Re-select after collection update
                SelectedProject = projectToUpdate;

                StatusText = $"Renamed project to '{dialog.ProjectName}'";
                NotificationService?.ShowSuccess("Project renamed successfully");
            }
        });
    }

    private void DeleteProject(object? parameter)
    {
        if (SelectedProject == null)
            return;

        var result = System.Windows.MessageBox.Show(
            $"Delete project '{SelectedProject.Name}' and all its sessions?\n\nThis action cannot be undone.",
            "Delete Project",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Warning
        );

        if (result == System.Windows.MessageBoxResult.Yes)
        {
            var projectName = SelectedProject.Name;
            projectService.DeleteProject(SelectedProject);
            Projects.Remove(SelectedProject);
            SelectedProject = Projects.FirstOrDefault();

            StatusText = $"Deleted project '{projectName}'";
            NotificationService?.ShowSuccess($"Project '{projectName}' deleted");
        }
    }

    private void EditSession(object? parameter)
    {
        if (SelectedSession == null || SelectedProject == null)
            return;

        Application.Current.Dispatcher.Invoke(() =>
        {
            var dialog = new Views.CreateSessionDialog(SelectedProject.Name, SelectedSession.Name);
            if (dialog.ShowDialog() == true)
            {
                // Store reference and index
                var sessionToUpdate = SelectedSession;
                var sessionIndex = SelectedProject.Sessions.IndexOf(sessionToUpdate);

                // Update the name and save
                sessionToUpdate.Name = dialog.SessionName;
                projectService.SaveProject(SelectedProject);

                // Clear selection first
                SelectedSession = null;

                // Replace item in collection to trigger UI update
                SelectedProject.Sessions.RemoveAt(sessionIndex);
                SelectedProject.Sessions.Insert(sessionIndex, sessionToUpdate);

                // Re-select after collection update
                SelectedSession = sessionToUpdate;

                StatusText = $"Renamed session to '{dialog.SessionName}'";
                NotificationService?.ShowSuccess("Session renamed successfully");
            }
        });
    }

    private void DeleteSession(object? parameter)
    {
        if (SelectedSession == null || SelectedProject == null)
            return;

        var result = System.Windows.MessageBox.Show(
            $"Delete session '{SelectedSession.Name}' and all its captures?\n\nThis action cannot be undone.",
            "Delete Session",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Warning
        );

        if (result == System.Windows.MessageBoxResult.Yes)
        {
            var sessionName = SelectedSession.Name;
            SelectedProject.Sessions.Remove(SelectedSession);
            projectService.SaveProject(SelectedProject);
            SelectedSession = SelectedProject.Sessions.FirstOrDefault();

            OnPropertyChanged(nameof(SelectedProject));
            StatusText = $"Deleted session '{sessionName}'";
            NotificationService?.ShowSuccess($"Session '{sessionName}' deleted");
        }
    }

    private void DeleteCaptures(object? parameter)
    {
        if (SelectedCaptures.Count == 0 || SelectedProject == null || SelectedSession == null)
            return;

        var captureCount = SelectedCaptures.Count;
        var result = System.Windows.MessageBox.Show(
            $"Delete {captureCount} capture(s)?\n\nImage files and OCR data will be permanently deleted.",
            "Delete Captures",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Warning
        );

        if (result == System.Windows.MessageBoxResult.Yes)
        {
            var capturesToDelete = SelectedCaptures.ToList();

            foreach (var capture in capturesToDelete)
            {
                // Delete the image file
                try
                {
                    if (File.Exists(capture.FilePath))
                    {
                        File.Delete(capture.FilePath);
                    }
                }
                catch
                {
                    // Ignore file deletion errors
                }

                // Remove from session
                SelectedSession.Captures.Remove(capture);
            }

            // Save project
            projectService.SaveProject(SelectedProject);

            // Refresh UI
            OnPropertyChanged(nameof(SelectedSession));
            OnPropertyChanged(nameof(HasNoCaptures));

            StatusText = $"Deleted {captureCount} capture(s)";
            NotificationService?.ShowSuccess($"{captureCount} capture(s) deleted");
        }
    }

    private async Task CaptureRegionAsync()
    {
        if (SelectedProject == null || SelectedSession == null)
            return;

        StatusText = "Select region on screen...";

        try
        {
            // Minimize main window
            Application.Current.Dispatcher.Invoke(() =>
            {
                Application.Current.MainWindow.WindowState = WindowState.Minimized;
            });

            await Task.Delay(300);

            // Show region selection overlay
            System.Drawing.Rectangle? selectedRegion = null;
            Application.Current.Dispatcher.Invoke(() =>
            {
                var overlay = new Views.RegionCaptureOverlay();
                if (overlay.ShowDialog() == true)
                {
                    selectedRegion = overlay.SelectedRegion;
                }
            });

            if (!selectedRegion.HasValue)
            {
                // Restore main window if cancelled
                Application.Current.Dispatcher.Invoke(() =>
                {
                    Application.Current.MainWindow.WindowState = WindowState.Normal;
                });
                StatusText = "Region selection cancelled";
                return;
            }

            // Keep window minimized and wait longer before capture
            StatusText = "Capturing selected region...";
            await Task.Delay(500); // Increased delay to ensure overlay is fully closed

            // Capture the selected region
            var result = await captureManager.CaptureRegionInteractiveAsync(async () =>
            {
                return await Task.FromResult<System.Drawing.Rectangle?>(selectedRegion.Value);
            });

            // Now restore main window after capture
            Application.Current.Dispatcher.Invoke(() =>
            {
                Application.Current.MainWindow.WindowState = WindowState.Normal;
            });

            if (result != null && result.Success && result.Image != null)
            {
                await ProcessCaptureAsync(result);
            }
            else
            {
                StatusText = "Capture failed";
            }
        }
        catch (Exception ex)
        {
            StatusText = $"Capture error: {ex.Message}";

            Application.Current.Dispatcher.Invoke(() =>
            {
                Application.Current.MainWindow.WindowState = WindowState.Normal;
            });
        }
    }

    private async Task CaptureFullscreenAsync()
    {
        if (SelectedProject == null || SelectedSession == null)
            return;

        StatusText = "Capturing fullscreen...";
        await Task.Delay(500); // Give window time to minimize

        try
        {
            // Minimize window
            Application.Current.Dispatcher.Invoke(() =>
            {
                Application.Current.MainWindow.WindowState = WindowState.Minimized;
            });

            await Task.Delay(500);

            var result = await captureManager.CaptureFullscreenAsync();

            // Restore window
            Application.Current.Dispatcher.Invoke(() =>
            {
                Application.Current.MainWindow.WindowState = WindowState.Normal;
            });

            if (result.Success && result.Image != null)
            {
                await ProcessCaptureAsync(result);
            }
            else
            {
                StatusText = $"Capture failed: {result.ErrorMessage}";
            }
        }
        catch (Exception ex)
        {
            StatusText = $"Capture error: {ex.Message}";

            Application.Current.Dispatcher.Invoke(() =>
            {
                Application.Current.MainWindow.WindowState = WindowState.Normal;
            });
        }
    }

    private async Task ProcessCaptureAsync(CaptureResult captureResult)
    {
        if (SelectedProject == null || SelectedSession == null || captureResult.Image == null)
            return;

        try
        {
            // Save image using config
            var config = configManager.LoadConfig();
            var timestamp = DateTime.Now.ToString(config.Naming.TimestampFormat);
            var session = SelectedSession.Name.Replace(" ", "_");
            var fileName = config.Naming.DefaultPattern
                .Replace("{session}", session)
                .Replace("{timestamp}", timestamp)
                .Replace("{sequence}", DateTime.Now.Millisecond.ToString("D3"));

            var extension = config.Capture.DefaultImageFormat.ToLower() == "jpg" ? "jpg" : "png";
            fileName = $"{fileName}.{extension}";

            var capturesPath = Path.Combine(SelectedProject.SavePath, "captures");
            Directory.CreateDirectory(capturesPath);
            var filePath = Path.Combine(capturesPath, fileName);

            // Save based on format setting
            if (config.Capture.DefaultImageFormat == "JPG")
            {
                var jpegEncoder = System.Drawing.Imaging.ImageCodecInfo.GetImageEncoders()
                    .First(enc => enc.FormatID == System.Drawing.Imaging.ImageFormat.Jpeg.Guid);
                var encoderParams = new System.Drawing.Imaging.EncoderParameters(1);
                encoderParams.Param[0] = new System.Drawing.Imaging.EncoderParameter(
                    System.Drawing.Imaging.Encoder.Quality, (long)config.Capture.JpegQuality);
                captureResult.Image.Save(filePath, jpegEncoder, encoderParams);
            }
            else
            {
                captureResult.Image.Save(filePath, System.Drawing.Imaging.ImageFormat.Png);
            }

            // Add to session
            var capture = projectService.AddCapture(SelectedProject, SelectedSession, filePath);

            // Update UI
            OnPropertyChanged(nameof(SelectedSession));
            OnPropertyChanged(nameof(HasNoCaptures));

            // Queue for OCR based on capture mode
            if (CurrentCaptureMode == Core.Models.CaptureMode.CaptureAndOcr)
            {
                StatusText = $"Captured {fileName}, queued for OCR processing";
                capture.Status = CaptureStatus.Processing;

                // Add to queue for background processing
                ocrQueue.Enqueue((SelectedProject, SelectedSession, capture));
                QueueCount = ocrQueue.Count;
                ((RelayCommand)ProcessQueueCommand).RaiseCanExecuteChanged();

                // Show success notification
                NotificationService?.ShowSuccess($"Capture saved! Queued for OCR ({QueueCount} in queue)");
            }
            else
            {
                StatusText = $"Captured {fileName} (OCR disabled in Capture Only mode)";
                capture.Status = CaptureStatus.Captured;

                // Show success notification
                NotificationService?.ShowSuccess($"Capture saved! ({CurrentCaptureMode})");
            }

            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            StatusText = $"Capture error: {ex.Message}";
            NotificationService?.ShowError($"Capture failed: {ex.Message}");
        }
    }

    private void StartBackgroundQueueProcessor()
    {
        queueCancellationTokenSource = new CancellationTokenSource();
        queueProcessorTask = Task.Run(async () =>
        {
            while (!queueCancellationTokenSource.Token.IsCancellationRequested)
            {
                try
                {
                    if (ocrQueue.TryDequeue(out var item))
                    {
                        await ProcessOCRItemAsync(item.project, item.session, item.capture);
                    }
                    else
                    {
                        await Task.Delay(500, queueCancellationTokenSource.Token);
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        StatusText = $"Queue processor error: {ex.Message}";
                    });
                }
            }
        }, queueCancellationTokenSource.Token);
    }

    private async Task ProcessOCRItemAsync(Project project, CaptureSession session, ScreenCapture capture)
    {
        try
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                IsProcessingQueue = true;
                StatusText = $"Processing OCR for {Path.GetFileName(capture.FilePath)}...";
            });

            // Load image from file (using stream to avoid file locking)
            System.Drawing.Bitmap bitmap;
            using (var stream = new FileStream(capture.FilePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                bitmap = new System.Drawing.Bitmap(stream);
            }

            // Process OCR
            OCRResult ocrResult;
            try
            {
                ocrResult = await ocrEngine.ProcessAsync(bitmap);
            }
            finally
            {
                bitmap.Dispose();
            }

            // Save OCR result
            var capturesPath = Path.GetDirectoryName(capture.FilePath)!;
            var ocrFilePath = Path.Combine(capturesPath, $"{Path.GetFileNameWithoutExtension(capture.FilePath)}_ocr.txt");
            await File.WriteAllTextAsync(ocrFilePath, ocrResult.Text);

            // Smart filename renaming (if enabled)
            var config = configManager.LoadConfig();
            if (config.Naming.UseSmartFilenames)
            {
                var currentFileName = Path.GetFileName(capture.FilePath);
                var extension = Path.GetExtension(capture.FilePath).TrimStart('.');

                // Get file creation time before any operations
                var captureTime = File.GetCreationTime(capture.FilePath);
                var smartFileName = GenerateSmartFilename(ocrResult, captureTime, extension);

                // Only rename if the new name is different
                if (smartFileName != currentFileName)
                {
                    var newImagePath = Path.Combine(capturesPath, smartFileName);
                    var newOcrPath = Path.Combine(capturesPath, $"{Path.GetFileNameWithoutExtension(smartFileName)}_ocr.txt");

                    // Add small delay to ensure file handles are released
                    await Task.Delay(100);

                    // Rename image file
                    if (File.Exists(capture.FilePath) && !File.Exists(newImagePath))
                    {
                        File.Move(capture.FilePath, newImagePath);
                        capture.FilePath = newImagePath;
                    }

                    // Rename OCR text file
                    if (File.Exists(ocrFilePath) && !File.Exists(newOcrPath))
                    {
                        File.Move(ocrFilePath, newOcrPath);
                        ocrFilePath = newOcrPath;
                    }
                }
            }

            // Update capture
            Application.Current.Dispatcher.Invoke(() =>
            {
                projectService.UpdateCaptureOCR(project, capture, ocrResult);
                QueueCount = ocrQueue.Count;
                ((RelayCommand)ProcessQueueCommand).RaiseCanExecuteChanged();

                var wordCount = ocrResult.Text.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
                StatusText = $"✓ Completed OCR - {wordCount} words extracted ({ocrResult.Confidence:P0} confidence) - {QueueCount} remaining";

                // Show notification
                if (QueueCount == 0)
                {
                    NotificationService?.ShowSuccess($"OCR complete! {wordCount} words extracted");
                }

                // Refresh UI
                OnPropertyChanged(nameof(SelectedSession));
            });
        }
        catch (Exception ex)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                StatusText = $"OCR processing error: {ex.Message}";
                capture.Status = CaptureStatus.Failed;
                QueueCount = ocrQueue.Count;
                NotificationService?.ShowError($"OCR failed: {ex.Message}");
                OnPropertyChanged(nameof(SelectedSession));
            });
        }
        finally
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                IsProcessingQueue = ocrQueue.Count > 0;
            });
        }
    }

    private void ProcessQueue(object? parameter)
    {
        // Manual trigger to process queue (currently auto-processes in background)
        StatusText = $"Queue is being processed automatically in background - {QueueCount} items remaining";
    }

    private void ExportProject(object? parameter)
    {
        if (SelectedProject == null)
            return;

        Application.Current.Dispatcher.Invoke(() =>
        {
            var dialog = new Views.ExportDialog(SelectedProject);
            dialog.Owner = Application.Current.MainWindow;
            dialog.ShowDialog();
        });
    }

    private void ExportSession(object? parameter)
    {
        if (SelectedSession == null || SelectedProject == null)
            return;

        Application.Current.Dispatcher.Invoke(() =>
        {
            var dialog = new Views.ExportDialog(SelectedSession, SelectedProject.Name);
            dialog.Owner = Application.Current.MainWindow;
            dialog.ShowDialog();
        });
    }

    private void ViewAnalytics(object? parameter)
    {
        if (SelectedProject == null)
            return;

        Application.Current.Dispatcher.Invoke(() =>
        {
            var dashboard = new Views.AnalyticsDashboard(SelectedProject, analyticsService);
            dashboard.Owner = Application.Current.MainWindow;
            dashboard.ShowDialog();
        });
    }

    private void OpenTemplateManager(object? parameter)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            var dialog = new Views.TemplateManagerDialog(templateService);
            dialog.Owner = Application.Current.MainWindow;
            dialog.ShowDialog();
        });
    }

    private void OpenSettings(object? parameter)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            var dialog = new Views.SettingsInfoDialog();
            dialog.Owner = Application.Current.MainWindow;
            dialog.ShowDialog();
        });
    }

    /// <summary>
    /// Updates the combined OCR text from selected captures
    /// </summary>
    private void UpdateCombinedOCRText()
    {
        if (SelectedCaptures == null || SelectedCaptures.Count == 0)
        {
            CombinedOCRText = "No captures selected";
            return;
        }

        if (SelectedCaptures.Count == 1)
        {
            // Single selection - format based on display mode
            var ocrResult = SelectedCaptures[0].OCRResult;
            if (ocrResult == null)
            {
                CombinedOCRText = "No OCR data available";
                return;
            }

            CombinedOCRText = FormatOCRText(ocrResult, CurrentDisplayMode);
            return;
        }

        // Multiple selections - comma separated
        var formattedTexts = SelectedCaptures
            .Where(c => c.OCRResult != null)
            .Select(c => FormatOCRText(c.OCRResult!, CurrentDisplayMode))
            .Where(t => !string.IsNullOrWhiteSpace(t));

        if (!formattedTexts.Any())
        {
            CombinedOCRText = "No OCR data available for selected captures";
            return;
        }

        CombinedOCRText = string.Join(", ", formattedTexts);
    }

    /// <summary>
    /// Formats OCR text based on display mode
    /// </summary>
    private string FormatOCRText(OCRResult ocrResult, string displayMode)
    {
        if (string.IsNullOrWhiteSpace(ocrResult.Text))
        {
            return string.Empty;
        }

        switch (displayMode.ToLower())
        {
            case "lines":
                // Lines comma-separated: "Orders, Total Sales, $113,506.58, ..."
                var lines = ocrResult.Lines
                    .Select(line => line.Text.Trim())
                    .Where(text => !string.IsNullOrWhiteSpace(text));
                return string.Join(", ", lines);

            case "structured":
                // Structured with line numbers: "[1] Orders\n[2] Total Sales\n[3] $113,506.58"
                var structured = ocrResult.Lines
                    .Where(line => !string.IsNullOrWhiteSpace(line.Text))
                    .Select(line => $"[{line.LineNumber + 1}] {line.Text.Trim()}");
                return string.Join("\n", structured);

            case "json":
                // JSON array: ["Orders", "Total Sales", "$113,506.58", ...]
                var jsonLines = ocrResult.Lines
                    .Select(line => line.Text.Trim())
                    .Where(text => !string.IsNullOrWhiteSpace(text))
                    .Select(text => $"\"{text.Replace("\"", "\\\"")}\"");
                return $"[{string.Join(", ", jsonLines)}]";

            case "continuous":
            default:
                // Continuous paragraph: "Orders Total Sales $113,506.58 ..."
                return ocrResult.Text.Trim();
        }
    }

    /// <summary>
    /// Call this when capture selection changes from UI
    /// </summary>
    public void UpdateSelectedCaptures(System.Collections.IList selectedItems)
    {
        SelectedCaptures.Clear();
        if (selectedItems != null)
        {
            foreach (ScreenCapture capture in selectedItems)
            {
                SelectedCaptures.Add(capture);
            }
        }
        UpdateCombinedOCRText();

        // Close metadata editor when selection changes
        IsMetadataEditorOpen = false;

        // Refresh command states for new action buttons
        ((AsyncRelayCommand)ProcessSelectedOCRCommand).RaiseCanExecuteChanged();
        ((RelayCommand)ViewImageCommand).RaiseCanExecuteChanged();
        ((RelayCommand)ViewFolderCommand).RaiseCanExecuteChanged();
        ((RelayCommand)EditMetadataCommand).RaiseCanExecuteChanged();
        ((RelayCommand)DeleteCapturesCommand).RaiseCanExecuteChanged();
    }

    /// <summary>
    /// Generates a smart filename from OCR result
    /// </summary>
    private string GenerateSmartFilename(OCRResult? ocrResult, DateTime timestamp, string extension)
    {
        var config = configManager.LoadConfig();

        // If smart filenames disabled or no OCR data, use fallback pattern
        if (!config.Naming.UseSmartFilenames || ocrResult == null || string.IsNullOrWhiteSpace(ocrResult.Text))
        {
            var fallbackName = config.Naming.FallbackPattern
                .Replace("{timestamp}", timestamp.ToString(config.Naming.TimestampFormat))
                .Replace("{sequence}", timestamp.Millisecond.ToString("D3"));
            return $"{fallbackName}.{extension}";
        }

        // Get first line from OCR result
        string firstLine;
        if (ocrResult.Lines != null && ocrResult.Lines.Count > 0)
        {
            firstLine = ocrResult.Lines[0].Text;
        }
        else
        {
            var lines = ocrResult.Text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            firstLine = lines.Length > 0 ? lines[0] : ocrResult.Text;
        }

        // Sanitize and limit length
        var sanitized = SanitizeFilename(firstLine.Trim());

        if (sanitized.Length > config.Naming.SmartFilenameMaxLength)
        {
            sanitized = sanitized.Substring(0, config.Naming.SmartFilenameMaxLength);
        }

        sanitized = sanitized.TrimEnd('-', '_');

        // If sanitization resulted in empty string, use fallback
        if (string.IsNullOrWhiteSpace(sanitized))
        {
            var fallbackName = config.Naming.FallbackPattern
                .Replace("{timestamp}", timestamp.ToString(config.Naming.TimestampFormat))
                .Replace("{sequence}", timestamp.Millisecond.ToString("D3"));
            return $"{fallbackName}.{extension}";
        }

        // Build smart filename: sanitized-text_timestamp.ext
        var timestampStr = timestamp.ToString(config.Naming.TimestampFormat);
        return $"{sanitized}_{timestampStr}.{extension}";
    }

    /// <summary>
    /// Sanitizes a string to be safe for use as a filename
    /// </summary>
    private string SanitizeFilename(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return string.Empty;

        // Replace invalid filename characters with underscore
        var invalid = Path.GetInvalidFileNameChars();
        var result = new string(input.Select(c => invalid.Contains(c) ? '_' : c).ToArray());

        // Replace multiple spaces/underscores with single underscore
        result = System.Text.RegularExpressions.Regex.Replace(result, @"[_\s]+", "_");

        return result;
    }

    #region Continuous Capture Mode

    /// <summary>
    /// Starts continuous capture mode by locking a screen region
    /// </summary>
    private async Task StartContinuousModeAsync()
    {
        try
        {
            StatusText = "Select region to lock for continuous capture...";

            // Minimize window and show region selector
            Application.Current.Dispatcher.Invoke(() =>
            {
                Application.Current.MainWindow.WindowState = WindowState.Minimized;
            });

            await Task.Delay(300);

            // Show region selection overlay
            System.Drawing.Rectangle? selectedRegion = null;
            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                var overlay = new RegionCaptureOverlay();
                overlay.ShowDialog();
                selectedRegion = overlay.SelectedRegion;
            });

            if (!selectedRegion.HasValue)
            {
                StatusText = "Continuous mode cancelled";
                Application.Current.Dispatcher.Invoke(() =>
                {
                    Application.Current.MainWindow.WindowState = WindowState.Normal;
                });
                return;
            }

            // Create locked region (no need to actually capture, just store coordinates)
            LockedRegion = new LockedRegion
            {
                X = selectedRegion.Value.X,
                Y = selectedRegion.Value.Y,
                Width = selectedRegion.Value.Width,
                Height = selectedRegion.Value.Height,
                LockedAt = DateTime.Now,
                CaptureCount = 0
            };

            // Restore window and show overlay
            Application.Current.Dispatcher.Invoke(() =>
            {
                Application.Current.MainWindow.WindowState = WindowState.Normal;

                // Show visual overlay for locked region
                regionOverlay = new LockedRegionOverlay(
                    selectedRegion.Value.X,
                    selectedRegion.Value.Y,
                    selectedRegion.Value.Width,
                    selectedRegion.Value.Height
                );
                regionOverlay.Show();
            });

            IsInContinuousMode = true;
            StatusText = $"Continuous mode active - Region locked at {LockedRegion.GetCoordinatesString()}";
            NotificationService?.ShowSuccess($"Continuous capture mode started! Press F9 to capture locked region.");

            // Register global F9 hotkey
            if (HotkeyService != null)
            {
                var f9Hotkey = new Hotkey
                {
                    Id = "continuous_capture_f9",
                    Name = "Continuous Capture",
                    Description = "Capture locked region (F9)",
                    Modifiers = HotkeyModifiers.None,
                    KeyCode = 0x78, // F9
                    IsEnabled = true
                };
                HotkeyService.RegisterHotkey(f9Hotkey);
            }

            // Refresh command states
            ((AsyncRelayCommand)StartContinuousModeCommand).RaiseCanExecuteChanged();
            ((RelayCommand)StopContinuousModeCommand).RaiseCanExecuteChanged();
            ((AsyncRelayCommand)CaptureLockedRegionCommand).RaiseCanExecuteChanged();
            ((RelayCommand)ToggleAutoCaptureCommand).RaiseCanExecuteChanged();
        }
        catch (Exception ex)
        {
            StatusText = $"Failed to start continuous mode: {ex.Message}";
            NotificationService?.ShowError($"Continuous mode error: {ex.Message}");
        }
    }

    /// <summary>
    /// Stops continuous capture mode
    /// </summary>
    private void StopContinuousMode(object? parameter)
    {
        // Stop auto-capture if running
        if (IsAutoCapturing)
        {
            StopAutoCapture();
        }

        // Close and dispose overlay
        if (regionOverlay != null)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                regionOverlay.Close();
                regionOverlay = null;
            });
        }

        // Unregister global F9 hotkey
        HotkeyService?.UnregisterAll();

        IsInContinuousMode = false;
        var captureCount = LockedRegion?.CaptureCount ?? 0;
        LockedRegion = null;

        StatusText = $"Continuous mode stopped - {captureCount} captures taken";
        NotificationService?.ShowInfo($"Continuous mode stopped. {captureCount} captures saved.");

        // Refresh command states
        ((AsyncRelayCommand)StartContinuousModeCommand).RaiseCanExecuteChanged();
        ((RelayCommand)StopContinuousModeCommand).RaiseCanExecuteChanged();
        ((AsyncRelayCommand)CaptureLockedRegionCommand).RaiseCanExecuteChanged();
        ((RelayCommand)ToggleAutoCaptureCommand).RaiseCanExecuteChanged();
    }

    /// <summary>
    /// Captures the currently locked region
    /// </summary>
    private async Task CaptureLockedRegionAsync()
    {
        if (LockedRegion == null || SelectedSession == null || SelectedProject == null)
        {
            return;
        }

        try
        {
            StatusText = $"Capturing locked region...";

            // Capture the locked region
            var captureResult = await captureManager.CaptureSpecificRegionAsync(
                LockedRegion.X,
                LockedRegion.Y,
                LockedRegion.Width,
                LockedRegion.Height
            );

            if (captureResult == null || captureResult.Image == null)
            {
                StatusText = "Capture failed";
                return;
            }

            // Save capture
            var config = configManager.LoadConfig();
            var timestamp = DateTime.Now.ToString(config.Naming.TimestampFormat);
            var session = SelectedSession.Name.Replace(" ", "_");
            var fileName = config.Naming.DefaultPattern
                .Replace("{session}", session)
                .Replace("{timestamp}", timestamp)
                .Replace("{sequence}", LockedRegion.CaptureCount.ToString("D3"));

            var extension = config.Capture.DefaultImageFormat.ToLower() == "jpg" ? "jpg" : "png";
            fileName = $"{fileName}.{extension}";

            var capturesPath = Path.Combine(SelectedProject.SavePath, "captures");
            Directory.CreateDirectory(capturesPath);
            var filePath = Path.Combine(capturesPath, fileName);

            // Save based on format setting
            if (config.Capture.DefaultImageFormat == "JPG")
            {
                var jpegEncoder = System.Drawing.Imaging.ImageCodecInfo.GetImageEncoders()
                    .First(enc => enc.FormatID == System.Drawing.Imaging.ImageFormat.Jpeg.Guid);
                var encoderParams = new System.Drawing.Imaging.EncoderParameters(1);
                encoderParams.Param[0] = new System.Drawing.Imaging.EncoderParameter(
                    System.Drawing.Imaging.Encoder.Quality, (long)config.Capture.JpegQuality);
                captureResult.Image.Save(filePath, jpegEncoder, encoderParams);
            }
            else
            {
                captureResult.Image.Save(filePath, System.Drawing.Imaging.ImageFormat.Png);
            }

            // Add to session
            var capture = projectService.AddCapture(SelectedProject, SelectedSession, filePath);

            // Update UI
            OnPropertyChanged(nameof(SelectedSession));
            OnPropertyChanged(nameof(HasNoCaptures));

            // Increment capture count
            LockedRegion.CaptureCount++;
            OnPropertyChanged(nameof(CaptureCountText));

            // Queue for OCR based on capture mode
            if (CurrentCaptureMode == Core.Models.CaptureMode.CaptureAndOcr)
            {
                StatusText = $"Captured locked region ({LockedRegion.CaptureCount} total), queued for OCR";
                capture.Status = CaptureStatus.Processing;

                // Add to queue for background processing
                ocrQueue.Enqueue((SelectedProject, SelectedSession, capture));
                QueueCount = ocrQueue.Count;
                ((RelayCommand)ProcessQueueCommand).RaiseCanExecuteChanged();

                // Show success notification
                NotificationService?.ShowSuccess($"Capture #{LockedRegion.CaptureCount} saved! Queued for OCR ({QueueCount} in queue)");
            }
            else
            {
                StatusText = $"Captured locked region ({LockedRegion.CaptureCount} total) - OCR disabled";
                capture.Status = CaptureStatus.Captured;

                // Show success notification
                NotificationService?.ShowSuccess($"Capture #{LockedRegion.CaptureCount} saved! ({CurrentCaptureMode})");
            }

            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            StatusText = $"Capture error: {ex.Message}";
            NotificationService?.ShowError($"Capture failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Toggles auto-capture mode
    /// </summary>
    private void ToggleAutoCapture(object? parameter)
    {
        if (IsAutoCapturing)
        {
            StopAutoCapture();
        }
        else
        {
            StartAutoCapture();
        }
    }

    /// <summary>
    /// Starts auto-capture timer
    /// </summary>
    private void StartAutoCapture()
    {
        if (autoCaptureTimer != null)
        {
            autoCaptureTimer.Stop();
            autoCaptureTimer.Dispose();
        }

        autoCaptureTimer = new System.Timers.Timer(AutoCaptureInterval * 1000);
        autoCaptureTimer.Elapsed += async (sender, e) =>
        {
            await Application.Current.Dispatcher.InvokeAsync(async () =>
            {
                await CaptureLockedRegionAsync();
            });
        };

        autoCaptureTimer.Start();
        IsAutoCapturing = true;

        StatusText = $"Auto-capture started - capturing every {AutoCaptureInterval} seconds";
        NotificationService?.ShowSuccess($"Auto-capture every {AutoCaptureInterval}s started!");
    }

    /// <summary>
    /// Stops auto-capture timer
    /// </summary>
    private void StopAutoCapture()
    {
        if (autoCaptureTimer != null)
        {
            autoCaptureTimer.Stop();
            autoCaptureTimer.Dispose();
            autoCaptureTimer = null;
        }

        IsAutoCapturing = false;
        StatusText = "Auto-capture stopped";
        NotificationService?.ShowInfo("Auto-capture stopped");
    }

    #endregion

    #region Capture Mode Commands

    /// <summary>
    /// Toggles between capture modes: CaptureAndOcr <-> CaptureOnly
    /// </summary>
    private void CycleCaptureMode(object? parameter)
    {
        // Toggle between Capture+OCR and Capture Only (OCR-only mode removed)
        CurrentCaptureMode = CurrentCaptureMode == Core.Models.CaptureMode.CaptureAndOcr
            ? Core.Models.CaptureMode.CaptureOnly
            : Core.Models.CaptureMode.CaptureAndOcr;

        StatusText = $"Capture mode: {CaptureModeText}";
        NotificationService?.ShowInfo($"Mode: {CaptureModeText}");

        // Refresh command states
        ((AsyncRelayCommand)ProcessSelectedOCRCommand).RaiseCanExecuteChanged();
    }

    /// <summary>
    /// Processes OCR for selected captures
    /// </summary>
    private async Task ProcessSelectedOCRAsync()
    {
        if (SelectedCaptures.Count == 0 || SelectedProject == null || SelectedSession == null)
        {
            return;
        }

        try
        {
            var captureList = SelectedCaptures.ToList();
            var queuedCount = 0;

            foreach (var capture in captureList)
            {
                // Only queue if not already processed or processing
                if (capture.Status != CaptureStatus.Completed && capture.Status != CaptureStatus.Processing)
                {
                    capture.Status = CaptureStatus.Processing;
                    ocrQueue.Enqueue((SelectedProject, SelectedSession, capture));
                    queuedCount++;
                }
            }

            QueueCount = ocrQueue.Count;
            ((RelayCommand)ProcessQueueCommand).RaiseCanExecuteChanged();

            StatusText = $"Queued {queuedCount} captures for OCR processing";
            NotificationService?.ShowSuccess($"{queuedCount} captures queued for OCR ({QueueCount} total in queue)");

            // Refresh UI
            OnPropertyChanged(nameof(SelectedSession));

            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            StatusText = $"Error queuing OCR: {ex.Message}";
            NotificationService?.ShowError($"Failed to queue OCR: {ex.Message}");
        }
    }

    #endregion

    #region Image Actions

    /// <summary>
    /// Opens the selected capture in the system default image viewer
    /// </summary>
    private void ViewImage(object? parameter)
    {
        if (SelectedCaptures.Count != 1)
            return;

        var capture = SelectedCaptures[0];

        try
        {
            if (File.Exists(capture.FilePath))
            {
                // Use Process.Start to open with default image viewer
                var processStartInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = capture.FilePath,
                    UseShellExecute = true
                };
                System.Diagnostics.Process.Start(processStartInfo);

                StatusText = $"Opened {capture.FileName} in default viewer";
            }
            else
            {
                StatusText = $"Image file not found: {capture.FilePath}";
                NotificationService?.ShowError($"Image file not found");
            }
        }
        catch (Exception ex)
        {
            StatusText = $"Failed to open image: {ex.Message}";
            NotificationService?.ShowError($"Failed to open image: {ex.Message}");
        }
    }

    /// <summary>
    /// Opens the folder containing the selected capture(s)
    /// </summary>
    private void ViewFolder(object? parameter)
    {
        if (SelectedCaptures.Count == 0)
            return;

        var capture = SelectedCaptures[0];

        try
        {
            if (File.Exists(capture.FilePath))
            {
                var folderPath = Path.GetDirectoryName(capture.FilePath);
                if (!string.IsNullOrEmpty(folderPath))
                {
                    // Open folder and select the file
                    var processStartInfo = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = "explorer.exe",
                        Arguments = $"/select,\"{capture.FilePath}\"",
                        UseShellExecute = true
                    };
                    System.Diagnostics.Process.Start(processStartInfo);

                    StatusText = $"Opened folder for {capture.FileName}";
                }
            }
            else
            {
                StatusText = $"File not found: {capture.FilePath}";
                NotificationService?.ShowError($"File not found");
            }
        }
        catch (Exception ex)
        {
            StatusText = $"Failed to open folder: {ex.Message}";
            NotificationService?.ShowError($"Failed to open folder: {ex.Message}");
        }
    }

    /// <summary>
    /// Toggles the inline metadata editor for the selected capture
    /// </summary>
    private void EditMetadata(object? parameter)
    {
        if (SelectedCaptures.Count != 1)
            return;

        // Toggle the inline metadata editor
        IsMetadataEditorOpen = !IsMetadataEditorOpen;

        if (IsMetadataEditorOpen)
        {
            StatusText = $"Editing metadata for {SelectedCaptures[0].FileName}";
        }
        else
        {
            StatusText = "Metadata editor closed";
            // Clear new field inputs when closing
            NewMetadataKey = "";
            NewMetadataValue = "";

            // Notify that we need to reset the metadata panel width
            OnPropertyChanged(nameof(IsMetadataEditorOpen));
        }
    }

    /// <summary>
    /// Refreshes the metadata fields collection from the selected capture
    /// </summary>
    private void RefreshMetadataFields()
    {
        CurrentMetadataFields.Clear();

        if (SelectedCaptures.Count == 1)
        {
            var capture = SelectedCaptures[0];
            foreach (var kvp in capture.Metadata)
            {
                CurrentMetadataFields.Add(new AutoCaptureOCR.App.Models.MetadataField
                {
                    Key = kvp.Key,
                    Value = kvp.Value
                });
            }
        }
    }

    /// <summary>
    /// Adds a new metadata field to the selected capture
    /// </summary>
    private void AddMetadataField(object? parameter)
    {
        if (SelectedCaptures.Count != 1 || string.IsNullOrWhiteSpace(NewMetadataKey))
            return;

        var capture = SelectedCaptures[0];

        try
        {
            // Add or update the metadata field
            if (capture.Metadata.ContainsKey(NewMetadataKey))
            {
                capture.Metadata[NewMetadataKey] = NewMetadataValue ?? "";
                StatusText = $"Updated metadata field '{NewMetadataKey}'";
            }
            else
            {
                capture.Metadata.Add(NewMetadataKey, NewMetadataValue ?? "");
                StatusText = $"Added metadata field '{NewMetadataKey}'";
            }

            // Save to project
            if (SelectedProject != null && SelectedSession != null)
            {
                projectService.SaveProject(SelectedProject);
            }

            // Clear inputs
            NewMetadataKey = "";
            NewMetadataValue = "";

            // Refresh the metadata fields list
            RefreshMetadataFields();

            NotificationService?.ShowSuccess($"Metadata field added");
        }
        catch (Exception ex)
        {
            StatusText = $"Error adding metadata: {ex.Message}";
            NotificationService?.ShowError($"Failed to add metadata: {ex.Message}");
        }
    }

    /// <summary>
    /// Removes a metadata field from the selected capture
    /// </summary>
    private void RemoveMetadataField(object? parameter)
    {
        if (SelectedCaptures.Count != 1 || parameter is not string key)
            return;

        var capture = SelectedCaptures[0];

        try
        {
            if (capture.Metadata.Remove(key))
            {
                // Save to project
                if (SelectedProject != null && SelectedSession != null)
                {
                    projectService.SaveProject(SelectedProject);
                }

                StatusText = $"Removed metadata field '{key}'";

                // Refresh the metadata fields list
                RefreshMetadataFields();

                NotificationService?.ShowSuccess($"Metadata field removed");
            }
        }
        catch (Exception ex)
        {
            StatusText = $"Error removing metadata: {ex.Message}";
            NotificationService?.ShowError($"Failed to remove metadata: {ex.Message}");
        }
    }

    #endregion
}
