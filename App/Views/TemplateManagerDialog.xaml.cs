using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using AutoCaptureOCR.Core.Models;
using AutoCaptureOCR.Core.Services;

namespace AutoCaptureOCR.App.Views;

public partial class TemplateManagerDialog : Window
{
    private readonly TemplateService templateService;
    private readonly ObservableCollection<MetadataTemplate> filteredTemplates = new();
    private MetadataTemplate? selectedTemplate;

    public TemplateManagerDialog(TemplateService templateService)
    {
        InitializeComponent();
        this.templateService = templateService;

        LoadTemplates();
    }

    private void LoadTemplates()
    {
        filteredTemplates.Clear();
        foreach (var template in templateService.Templates)
        {
            filteredTemplates.Add(template);
        }

        TemplateListBox.ItemsSource = filteredTemplates;
    }

    private void FilterTemplates(string searchText)
    {
        filteredTemplates.Clear();

        var filtered = templateService.Templates
            .Where(t => string.IsNullOrWhiteSpace(searchText) ||
                       t.Name.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
                       t.Category.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
                       t.Description.Contains(searchText, StringComparison.OrdinalIgnoreCase));

        foreach (var template in filtered)
        {
            filteredTemplates.Add(template);
        }
    }

    private void ShowTemplateDetails(MetadataTemplate template)
    {
        selectedTemplate = template;

        TemplateNameText.Text = template.Name;
        TemplateDescriptionText.Text = template.Description;
        TemplateCategoryText.Text = template.Category;
        TemplateUsageText.Text = $"{template.UsageCount} times";

        FieldsListBox.ItemsSource = template.Fields.OrderBy(f => f.DisplayOrder);

        // Show/hide buttons based on template type
        EditButton.IsEnabled = !template.IsBuiltIn;
        DeleteButton.IsEnabled = !template.IsBuiltIn;
        DuplicateButton.IsEnabled = true;

        // Show details panel
        EmptyStatePanel.Visibility = Visibility.Collapsed;
        DetailsPanel.Visibility = Visibility.Visible;
    }

    private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        FilterTemplates(SearchTextBox.Text);
    }

    private void TemplateListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (TemplateListBox.SelectedItem is MetadataTemplate template)
        {
            ShowTemplateDetails(template);
        }
        else
        {
            EmptyStatePanel.Visibility = Visibility.Visible;
            DetailsPanel.Visibility = Visibility.Collapsed;
        }
    }

    private void NewTemplateButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new TemplateEditorDialog(templateService);
        if (dialog.ShowDialog() == true)
        {
            LoadTemplates();

            // Select the newly created template
            var newTemplate = filteredTemplates.FirstOrDefault(t => t.Id == dialog.TemplateId);
            if (newTemplate != null)
            {
                TemplateListBox.SelectedItem = newTemplate;
            }
        }
    }

    private void EditButton_Click(object sender, RoutedEventArgs e)
    {
        if (selectedTemplate == null || selectedTemplate.IsBuiltIn)
            return;

        var dialog = new TemplateEditorDialog(templateService, selectedTemplate);
        if (dialog.ShowDialog() == true)
        {
            LoadTemplates();
            ShowTemplateDetails(selectedTemplate);
        }
    }

    private void DuplicateButton_Click(object sender, RoutedEventArgs e)
    {
        if (selectedTemplate == null)
            return;

        try
        {
            var duplicate = templateService.DuplicateTemplate(selectedTemplate.Id);
            LoadTemplates();

            // Select the duplicated template
            TemplateListBox.SelectedItem = filteredTemplates.FirstOrDefault(t => t.Id == duplicate.Id);

            MessageBox.Show($"Template '{selectedTemplate.Name}' has been duplicated successfully.",
                "Template Duplicated", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to duplicate template: {ex.Message}",
                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void DeleteButton_Click(object sender, RoutedEventArgs e)
    {
        if (selectedTemplate == null || selectedTemplate.IsBuiltIn)
            return;

        var result = MessageBox.Show(
            $"Are you sure you want to delete the template '{selectedTemplate.Name}'?\n\nThis action cannot be undone.",
            "Confirm Delete",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result == MessageBoxResult.Yes)
        {
            try
            {
                templateService.DeleteTemplate(selectedTemplate.Id);
                LoadTemplates();

                EmptyStatePanel.Visibility = Visibility.Visible;
                DetailsPanel.Visibility = Visibility.Collapsed;

                MessageBox.Show("Template deleted successfully.",
                    "Deleted", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to delete template: {ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Close();
        }
    }
}
