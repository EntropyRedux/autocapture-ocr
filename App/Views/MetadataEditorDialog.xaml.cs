using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using AutoCaptureOCR.Core.Models;
using AutoCaptureOCR.Core.Services;

namespace AutoCaptureOCR.App.Views;

public partial class MetadataEditorDialog : Window
{
    private readonly MetadataService metadataService;
    private readonly ScreenCapture capture;
    private readonly Dictionary<string, FrameworkElement> fieldControls = new();

    public CaptureMetadata? Result { get; private set; }

    public MetadataEditorDialog(MetadataService metadataService, ScreenCapture capture)
    {
        InitializeComponent();

        this.metadataService = metadataService ?? throw new ArgumentNullException(nameof(metadataService));
        this.capture = capture ?? throw new ArgumentNullException(nameof(capture));

        CaptureInfoTextBlock.Text = $"File: {Path.GetFileName(capture.FilePath)} • Captured: {capture.Timestamp:yyyy-MM-dd HH:mm:ss}";

        LoadTemplates();
    }

    private void LoadTemplates()
    {
        var templates = metadataService.GetAllTemplates();
        TemplateComboBox.ItemsSource = templates;

        // Select existing template or first template
        if (capture.TemplateMetadata != null)
        {
            var existingTemplate = templates.FirstOrDefault(t => t.Id == capture.TemplateMetadata.TemplateId);
            TemplateComboBox.SelectedItem = existingTemplate;
        }
        else if (templates.Count > 0)
        {
            TemplateComboBox.SelectedIndex = 0;
        }
    }

    private void TemplateComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (TemplateComboBox.SelectedItem is MetadataTemplate template)
        {
            BuildFieldsUI(template);
        }
    }

    private void BuildFieldsUI(MetadataTemplate template)
    {
        FieldsPanel.Children.Clear();
        fieldControls.Clear();

        foreach (var field in template.Fields.OrderBy(f => f.DisplayOrder))
        {
            // Label
            var label = new TextBlock
            {
                Text = field.Label + (field.IsRequired ? " *" : ""),
                Foreground = System.Windows.Media.Brushes.LightGray,
                FontSize = 12,
                Margin = new Thickness(0, 0, 0, 4)
            };
            FieldsPanel.Children.Add(label);

            // Field control based on type
            FrameworkElement control = field.FieldType switch
            {
                MetadataFieldType.Text => CreateTextBox(field, false),
                MetadataFieldType.MultilineText => CreateTextBox(field, true),
                MetadataFieldType.Number => CreateNumberBox(field),
                MetadataFieldType.Date => CreateDatePicker(field),
                MetadataFieldType.Dropdown => CreateComboBox(field),
                MetadataFieldType.Checkbox => CreateCheckBox(field),
                _ => CreateTextBox(field, false)
            };

            control.Margin = new Thickness(0, 0, 0, 16);
            FieldsPanel.Children.Add(control);
            fieldControls[field.Name] = control;

            // Set existing value if available
            if (capture.TemplateMetadata?.Values.ContainsKey(field.Name) == true)
            {
                SetControlValue(control, capture.TemplateMetadata.Values[field.Name]);
            }
            else if (!string.IsNullOrEmpty(field.DefaultValue))
            {
                SetControlValue(control, field.DefaultValue);
            }
        }
    }

    private TextBox CreateTextBox(MetadataField field, bool multiline)
    {
        var textBox = new TextBox
        {
            Height = multiline ? 80 : 32,
            FontSize = 13,
            Padding = new Thickness(8, 6, 8, 6),
            Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(45, 45, 48)),
            Foreground = System.Windows.Media.Brushes.White,
            BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(63, 63, 70)),
            BorderThickness = new Thickness(1),
            TextWrapping = multiline ? TextWrapping.Wrap : TextWrapping.NoWrap,
            AcceptsReturn = multiline,
            VerticalScrollBarVisibility = multiline ? ScrollBarVisibility.Auto : ScrollBarVisibility.Disabled
        };

        if (!string.IsNullOrEmpty(field.Placeholder))
        {
            textBox.Tag = field.Placeholder;
        }

        return textBox;
    }

    private TextBox CreateNumberBox(MetadataField field)
    {
        var textBox = CreateTextBox(field, false);
        textBox.PreviewTextInput += (s, e) =>
        {
            // Allow only numbers and decimal point
            e.Handled = !System.Text.RegularExpressions.Regex.IsMatch(e.Text, @"^[0-9.]+$");
        };
        return textBox;
    }

    private DatePicker CreateDatePicker(MetadataField field)
    {
        return new DatePicker
        {
            Height = 32,
            FontSize = 13,
            Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(45, 45, 48)),
            Foreground = System.Windows.Media.Brushes.White,
            BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(63, 63, 70)),
            BorderThickness = new Thickness(1),
            SelectedDate = DateTime.Today
        };
    }

    private ComboBox CreateComboBox(MetadataField field)
    {
        var comboBox = new ComboBox
        {
            Height = 32,
            FontSize = 13,
            Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(45, 45, 48)),
            Foreground = System.Windows.Media.Brushes.White,
            BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(63, 63, 70)),
            BorderThickness = new Thickness(1),
            ItemsSource = field.DropdownOptions
        };

        // Style for dropdown items to fix white-on-white issue
        var itemContainerStyle = new Style(typeof(ComboBoxItem));
        itemContainerStyle.Setters.Add(new Setter(ComboBoxItem.BackgroundProperty, new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(45, 45, 48))));
        itemContainerStyle.Setters.Add(new Setter(ComboBoxItem.ForegroundProperty, System.Windows.Media.Brushes.White));

        // Hover effect
        var hoverTrigger = new Trigger { Property = ComboBoxItem.IsMouseOverProperty, Value = true };
        hoverTrigger.Setters.Add(new Setter(ComboBoxItem.BackgroundProperty, new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(62, 62, 64))));
        itemContainerStyle.Triggers.Add(hoverTrigger);

        comboBox.ItemContainerStyle = itemContainerStyle;

        return comboBox;
    }

    private CheckBox CreateCheckBox(MetadataField field)
    {
        return new CheckBox
        {
            Content = field.Label,
            FontSize = 13,
            Foreground = System.Windows.Media.Brushes.LightGray,
            VerticalAlignment = VerticalAlignment.Center
        };
    }

    private void SetControlValue(FrameworkElement control, string value)
    {
        switch (control)
        {
            case TextBox textBox:
                textBox.Text = value;
                break;
            case DatePicker datePicker:
                if (DateTime.TryParse(value, out var date))
                    datePicker.SelectedDate = date;
                break;
            case ComboBox comboBox:
                comboBox.SelectedItem = value;
                break;
            case CheckBox checkBox:
                checkBox.IsChecked = value.Equals("true", StringComparison.OrdinalIgnoreCase);
                break;
        }
    }

    private Dictionary<string, string> GetFieldValues()
    {
        var values = new Dictionary<string, string>();

        foreach (var kvp in fieldControls)
        {
            var fieldName = kvp.Key;
            var control = kvp.Value;

            string? value = control switch
            {
                TextBox textBox => textBox.Text,
                DatePicker datePicker => datePicker.SelectedDate?.ToString("yyyy-MM-dd"),
                ComboBox comboBox => comboBox.SelectedItem?.ToString(),
                CheckBox checkBox => checkBox.IsChecked?.ToString().ToLower(),
                _ => null
            };

            if (!string.IsNullOrEmpty(value))
            {
                values[fieldName] = value;
            }
        }

        return values;
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        if (TemplateComboBox.SelectedItem is not MetadataTemplate template)
        {
            MessageBox.Show("Please select a template", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var values = GetFieldValues();
        var (isValid, errors) = metadataService.ValidateMetadata(template.Id, values);

        if (!isValid)
        {
            MessageBox.Show($"Please correct the following errors:\n\n{string.Join("\n", errors)}",
                "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        Result = metadataService.ApplyTemplate(template.Id, values);
        DialogResult = true;
        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            DialogResult = false;
            Close();
        }
    }
}
