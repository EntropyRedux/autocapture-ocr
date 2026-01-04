using System.Windows;
using AutoCaptureOCR.Core.Models;
using AutoCaptureOCR.Core.Services;

namespace AutoCaptureOCR.App.Views;

/// <summary>
/// Dialog for creating and editing metadata templates
/// TODO: Full implementation in next phase
/// </summary>
public partial class TemplateEditorDialog : Window
{
    private readonly TemplateService templateService;
    private readonly MetadataTemplate? existingTemplate;

    public Guid TemplateId { get; private set; }

    public TemplateEditorDialog(TemplateService templateService, MetadataTemplate? template = null)
    {
        this.templateService = templateService;
        this.existingTemplate = template;

        // Placeholder - will implement full UI later
        MessageBox.Show("Template Editor is under construction. This feature will be available in the next update.",
            "Coming Soon", MessageBoxButton.OK, MessageBoxImage.Information);

        DialogResult = false;
    }
}
