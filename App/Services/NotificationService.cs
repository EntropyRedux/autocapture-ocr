using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace AutoCaptureOCR.App.Services;

/// <summary>
/// Service for showing toast notifications
/// </summary>
public class NotificationService
{
    private readonly Window ownerWindow;
    private readonly Stack<Border> activeNotifications = new();
    private const int MaxNotifications = 3;
    private const double NotificationWidth = 350;
    private const double NotificationMargin = 16;

    public NotificationService(Window owner)
    {
        ownerWindow = owner ?? throw new ArgumentNullException(nameof(owner));
    }

    /// <summary>
    /// Show a success notification
    /// </summary>
    public void ShowSuccess(string message, int durationMs = 3000)
    {
        ShowNotification(message, NotificationType.Success, durationMs);
    }

    /// <summary>
    /// Show an info notification
    /// </summary>
    public void ShowInfo(string message, int durationMs = 3000)
    {
        ShowNotification(message, NotificationType.Info, durationMs);
    }

    /// <summary>
    /// Show a warning notification
    /// </summary>
    public void ShowWarning(string message, int durationMs = 4000)
    {
        ShowNotification(message, NotificationType.Warning, durationMs);
    }

    /// <summary>
    /// Show an error notification
    /// </summary>
    public void ShowError(string message, int durationMs = 5000)
    {
        ShowNotification(message, NotificationType.Error, durationMs);
    }

    private void ShowNotification(string message, NotificationType type, int durationMs)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            // Remove oldest notification if we have too many
            if (activeNotifications.Count >= MaxNotifications)
            {
                var oldest = activeNotifications.Pop();
                RemoveNotification(oldest, immediate: true);
            }

            var notification = CreateNotification(message, type);
            AddNotificationToWindow(notification);

            // Auto-hide after duration
            var timer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(durationMs)
            };
            timer.Tick += (s, e) =>
            {
                timer.Stop();
                RemoveNotification(notification);
            };
            timer.Start();
        });
    }

    private Border CreateNotification(string message, NotificationType type)
    {
        var (backgroundColor, borderColor, icon) = GetNotificationStyle(type);

        var iconText = new TextBlock
        {
            Text = icon,
            FontSize = 16,
            Foreground = new SolidColorBrush(borderColor),
            Margin = new Thickness(0, 0, 12, 0),
            VerticalAlignment = VerticalAlignment.Top
        };

        var messageText = new TextBlock
        {
            Text = message,
            FontSize = 13,
            Foreground = Brushes.White,
            TextWrapping = TextWrapping.Wrap,
            VerticalAlignment = VerticalAlignment.Center
        };

        var contentPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Children = { iconText, messageText }
        };

        var border = new Border
        {
            Background = new SolidColorBrush(backgroundColor),
            BorderBrush = new SolidColorBrush(borderColor),
            BorderThickness = new Thickness(1, 1, 1, 3),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(16, 12, 16, 12),
            Width = NotificationWidth,
            Child = contentPanel,
            Opacity = 0,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Top,
            Cursor = System.Windows.Input.Cursors.Hand,
            Effect = new System.Windows.Media.Effects.DropShadowEffect
            {
                Color = Colors.Black,
                Opacity = 0.3,
                BlurRadius = 10,
                ShadowDepth = 2
            }
        };

        // Click to dismiss
        border.MouseLeftButtonDown += (s, e) => RemoveNotification(border);

        return border;
    }

    private (Color background, Color border, string icon) GetNotificationStyle(NotificationType type)
    {
        return type switch
        {
            NotificationType.Success => (Color.FromRgb(78, 201, 176), Color.FromRgb(78, 201, 176), "✓"),
            NotificationType.Info => (Color.FromRgb(86, 156, 214), Color.FromRgb(86, 156, 214), "ℹ"),
            NotificationType.Warning => (Color.FromRgb(220, 220, 170), Color.FromRgb(220, 220, 170), "⚠"),
            NotificationType.Error => (Color.FromRgb(244, 135, 113), Color.FromRgb(244, 135, 113), "✗"),
            _ => (Color.FromRgb(45, 45, 48), Color.FromRgb(63, 63, 70), "•")
        };
    }

    private void AddNotificationToWindow(Border notification)
    {
        var grid = FindNotificationGrid();
        if (grid == null) return;

        activeNotifications.Push(notification);
        grid.Children.Add(notification);

        // Position notification
        UpdateNotificationPositions();

        // Fade in animation
        var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(300));
        notification.BeginAnimation(UIElement.OpacityProperty, fadeIn);

        // Slide in animation
        var slideIn = new ThicknessAnimation(
            new Thickness(NotificationMargin + 50, NotificationMargin, NotificationMargin, 0),
            new Thickness(NotificationMargin, NotificationMargin, NotificationMargin, 0),
            TimeSpan.FromMilliseconds(300)
        );
        notification.BeginAnimation(FrameworkElement.MarginProperty, slideIn);
    }

    private void RemoveNotification(Border notification, bool immediate = false)
    {
        var grid = FindNotificationGrid();
        if (grid == null) return;

        if (immediate)
        {
            grid.Children.Remove(notification);
            activeNotifications.TryPop(out _);
            UpdateNotificationPositions();
        }
        else
        {
            // Fade out animation
            var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(200));
            fadeOut.Completed += (s, e) =>
            {
                grid.Children.Remove(notification);
                activeNotifications.TryPop(out _);
                UpdateNotificationPositions();
            };
            notification.BeginAnimation(UIElement.OpacityProperty, fadeOut);
        }
    }

    private void UpdateNotificationPositions()
    {
        var yOffset = NotificationMargin;
        foreach (var notification in activeNotifications.Reverse())
        {
            notification.Margin = new Thickness(NotificationMargin, yOffset, NotificationMargin, 0);
            yOffset += notification.ActualHeight + 8;
        }
    }

    private Grid? FindNotificationGrid()
    {
        // Find or create notification grid in the main window
        if (ownerWindow.Content is Grid mainGrid)
        {
            var notificationGrid = mainGrid.Children.OfType<Grid>()
                .FirstOrDefault(g => g.Name == "NotificationGrid");

            if (notificationGrid == null)
            {
                notificationGrid = new Grid
                {
                    Name = "NotificationGrid",
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    VerticalAlignment = VerticalAlignment.Stretch,
                    IsHitTestVisible = false
                };

                // Add to main grid
                mainGrid.Children.Add(notificationGrid);
                Grid.SetRowSpan(notificationGrid, Math.Max(1, mainGrid.RowDefinitions.Count));
                Grid.SetColumnSpan(notificationGrid, Math.Max(1, mainGrid.ColumnDefinitions.Count));
            }

            // Enable hit test for notifications
            notificationGrid.IsHitTestVisible = true;

            return notificationGrid;
        }

        return null;
    }
}

public enum NotificationType
{
    Success,
    Info,
    Warning,
    Error
}
