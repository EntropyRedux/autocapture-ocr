using AutoCaptureOCR.Core.Models;

namespace AutoCaptureOCR.Core.Services;

/// <summary>
/// Manages metadata application to captures (delegates template management to TemplateService)
/// </summary>
public class MetadataService
{
    private readonly TemplateService templateService;

    public MetadataService(TemplateService templateService)
    {
        this.templateService = templateService ?? throw new ArgumentNullException(nameof(templateService));
    }

    /// <summary>
    /// Gets all available metadata templates (delegates to TemplateService)
    /// </summary>
    public List<MetadataTemplate> GetAllTemplates()
    {
        return templateService.Templates.ToList();
    }

    /// <summary>
    /// Gets a specific template by ID (delegates to TemplateService)
    /// </summary>
    public MetadataTemplate? GetTemplate(Guid templateId)
    {
        return templateService.GetTemplate(templateId);
    }


    /// <summary>
    /// Applies a template to a capture with provided values
    /// </summary>
    public CaptureMetadata ApplyTemplate(Guid templateId, Dictionary<string, string> values)
    {
        var template = GetTemplate(templateId);
        if (template == null)
            throw new ArgumentException($"Template {templateId} not found");

        // Record template usage
        templateService.RecordTemplateUsage(templateId);

        return new CaptureMetadata
        {
            TemplateId = templateId,
            TemplateName = template.Name,
            Values = values
        };
    }

    /// <summary>
    /// Validates metadata values against template requirements
    /// </summary>
    public (bool isValid, List<string> errors) ValidateMetadata(Guid templateId, Dictionary<string, string> values)
    {
        var template = GetTemplate(templateId);
        if (template == null)
            return (false, new List<string> { "Template not found" });

        var errors = new List<string>();

        foreach (var field in template.Fields.Where(f => f.IsRequired))
        {
            if (!values.ContainsKey(field.Name) || string.IsNullOrWhiteSpace(values[field.Name]))
            {
                errors.Add($"{field.Label} is required");
            }
        }

        return (errors.Count == 0, errors);
    }
}
