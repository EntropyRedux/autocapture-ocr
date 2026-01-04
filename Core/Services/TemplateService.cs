using AutoCaptureOCR.Core.Models;
using Newtonsoft.Json;
using System.Collections.ObjectModel;

namespace AutoCaptureOCR.Core.Services;

/// <summary>
/// Manages metadata templates with built-in templates and user-defined templates
/// </summary>
public class TemplateService
{
    private readonly string templatesPath;
    private readonly ObservableCollection<MetadataTemplate> templates = new();

    public event EventHandler<TemplateChangedEventArgs>? TemplateChanged;

    public IReadOnlyList<MetadataTemplate> Templates => templates;

    public TemplateService(string appDataPath)
    {
        templatesPath = Path.Combine(appDataPath, "templates");
        Directory.CreateDirectory(templatesPath);

        LoadTemplates();
    }

    /// <summary>
    /// Load all templates (built-in + user-defined)
    /// </summary>
    private void LoadTemplates()
    {
        templates.Clear();

        // Add built-in templates first
        foreach (var builtInTemplate in CreateBuiltInTemplates())
        {
            templates.Add(builtInTemplate);
        }

        // Load user-defined templates
        var templateFiles = Directory.GetFiles(templatesPath, "*.json");
        foreach (var file in templateFiles)
        {
            try
            {
                var json = File.ReadAllText(file);
                var template = JsonConvert.DeserializeObject<MetadataTemplate>(json);
                if (template != null && !template.IsBuiltIn)
                {
                    templates.Add(template);
                }
            }
            catch
            {
                // Skip corrupted templates
            }
        }
    }

    /// <summary>
    /// Create built-in templates that ship with the application
    /// </summary>
    private List<MetadataTemplate> CreateBuiltInTemplates()
    {
        return new List<MetadataTemplate>
        {
            // UI Documentation Template
            new MetadataTemplate
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000001"),
                Name = "UI Documentation",
                Description = "Document user interface elements with structured metadata for design systems and component libraries",
                Category = "Development",
                IsBuiltIn = true,
                Fields = new List<MetadataField>
                {
                    new() { Name = "ElementType", Label = "Element Type", FieldType = MetadataFieldType.Dropdown,
                            DropdownOptions = new List<string> { "Button", "Input", "Dropdown", "Modal", "Menu", "Card", "Form", "Navigation", "Icon", "Other" },
                            IsRequired = true, DisplayOrder = 1, HelpText = "Type of UI component" },
                    new() { Name = "ComponentName", Label = "Component Name", FieldType = MetadataFieldType.Text,
                            IsRequired = true, DisplayOrder = 2, Placeholder = "e.g., LoginButton, SearchInput",
                            HelpText = "Unique identifier for this component" },
                    new() { Name = "PageLocation", Label = "Page/Screen", FieldType = MetadataFieldType.Text,
                            DisplayOrder = 3, Placeholder = "e.g., Dashboard, Settings",
                            HelpText = "Where this element appears" },
                    new() { Name = "State", Label = "State", FieldType = MetadataFieldType.Dropdown,
                            DropdownOptions = new List<string> { "Default", "Hover", "Active", "Disabled", "Error", "Loading" },
                            DefaultValue = "Default", DisplayOrder = 4 },
                    new() { Name = "VisibleText", Label = "Visible Text", FieldType = MetadataFieldType.Text,
                            DisplayOrder = 5, Placeholder = "Text displayed on element",
                            HelpText = "User-visible label or content" },
                    new() { Name = "Notes", Label = "Notes", FieldType = MetadataFieldType.MultilineText,
                            DisplayOrder = 6, Placeholder = "Additional documentation or implementation notes" }
                }
            },

            // Receipt Tracking Template
            new MetadataTemplate
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000002"),
                Name = "Receipt Tracking",
                Description = "Track receipts and invoices for expense management and tax reporting",
                Category = "Finance",
                IsBuiltIn = true,
                Fields = new List<MetadataField>
                {
                    new() { Name = "Vendor", Label = "Vendor/Store", FieldType = MetadataFieldType.Text,
                            IsRequired = true, DisplayOrder = 1, Placeholder = "e.g., Amazon, Walmart",
                            HelpText = "Business or store name" },
                    new() { Name = "Date", Label = "Transaction Date", FieldType = MetadataFieldType.Date,
                            IsRequired = true, DisplayOrder = 2, HelpText = "Date of purchase" },
                    new() { Name = "Amount", Label = "Total Amount", FieldType = MetadataFieldType.Currency,
                            IsRequired = true, DisplayOrder = 3, Placeholder = "0.00",
                            HelpText = "Total transaction amount" },
                    new() { Name = "Category", Label = "Expense Category", FieldType = MetadataFieldType.Dropdown,
                            DropdownOptions = new List<string> { "Office Supplies", "Travel", "Meals", "Equipment", "Software", "Utilities", "Other" },
                            IsRequired = true, DisplayOrder = 4 },
                    new() { Name = "PaymentMethod", Label = "Payment Method", FieldType = MetadataFieldType.Dropdown,
                            DropdownOptions = new List<string> { "Credit Card", "Debit Card", "Cash", "Check", "Bank Transfer", "PayPal", "Other" },
                            DisplayOrder = 5 },
                    new() { Name = "ReceiptNumber", Label = "Receipt/Invoice Number", FieldType = MetadataFieldType.Text,
                            DisplayOrder = 6, Placeholder = "Transaction or invoice ID" },
                    new() { Name = "TaxDeductible", Label = "Tax Deductible", FieldType = MetadataFieldType.Checkbox,
                            DefaultValue = "false", DisplayOrder = 7, HelpText = "Can this be claimed as business expense?" },
                    new() { Name = "Notes", Label = "Notes", FieldType = MetadataFieldType.MultilineText,
                            DisplayOrder = 8, Placeholder = "Purpose, attendees, or additional details" }
                }
            },

            // Code Documentation Template
            new MetadataTemplate
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000003"),
                Name = "Code Documentation",
                Description = "Document code snippets, functions, and implementations for knowledge bases and tutorials",
                Category = "Development",
                IsBuiltIn = true,
                Fields = new List<MetadataField>
                {
                    new() { Name = "Language", Label = "Programming Language", FieldType = MetadataFieldType.Dropdown,
                            DropdownOptions = new List<string> { "C#", "Python", "JavaScript", "TypeScript", "Java", "C++", "Go", "Rust", "SQL", "Other" },
                            IsRequired = true, DisplayOrder = 1 },
                    new() { Name = "CodeType", Label = "Code Type", FieldType = MetadataFieldType.Dropdown,
                            DropdownOptions = new List<string> { "Function", "Class", "Method", "API Endpoint", "Configuration", "Query", "Script", "Snippet" },
                            IsRequired = true, DisplayOrder = 2 },
                    new() { Name = "FunctionName", Label = "Function/Class Name", FieldType = MetadataFieldType.Text,
                            DisplayOrder = 3, Placeholder = "e.g., CalculateTotal, UserService",
                            HelpText = "Name of function, class, or endpoint" },
                    new() { Name = "Purpose", Label = "Purpose", FieldType = MetadataFieldType.MultilineText,
                            IsRequired = true, DisplayOrder = 4, Placeholder = "What does this code do?",
                            HelpText = "Brief description of functionality" },
                    new() { Name = "Parameters", Label = "Parameters/Arguments", FieldType = MetadataFieldType.MultilineText,
                            DisplayOrder = 5, Placeholder = "List input parameters and their types" },
                    new() { Name = "ReturnValue", Label = "Return Value", FieldType = MetadataFieldType.Text,
                            DisplayOrder = 6, Placeholder = "What does it return?" },
                    new() { Name = "SourceFile", Label = "Source File", FieldType = MetadataFieldType.Text,
                            DisplayOrder = 7, Placeholder = "e.g., UserController.cs, utils.py" },
                    new() { Name = "Tags", Label = "Tags", FieldType = MetadataFieldType.MultiSelect,
                            DropdownOptions = new List<string> { "Authentication", "Database", "API", "Utility", "Algorithm", "UI", "Testing", "Performance" },
                            DisplayOrder = 8, HelpText = "Categorize this code snippet" },
                    new() { Name = "Notes", Label = "Additional Notes", FieldType = MetadataFieldType.MultilineText,
                            DisplayOrder = 9, Placeholder = "Implementation details, gotchas, or examples" }
                }
            }
        };
    }

    /// <summary>
    /// Get template by ID
    /// </summary>
    public MetadataTemplate? GetTemplate(Guid id)
    {
        return templates.FirstOrDefault(t => t.Id == id);
    }

    /// <summary>
    /// Get template by name
    /// </summary>
    public MetadataTemplate? GetTemplateByName(string name)
    {
        return templates.FirstOrDefault(t => t.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Get all templates in a specific category
    /// </summary>
    public List<MetadataTemplate> GetTemplatesByCategory(string category)
    {
        return templates.Where(t => t.Category.Equals(category, StringComparison.OrdinalIgnoreCase)).ToList();
    }

    /// <summary>
    /// Get all unique categories
    /// </summary>
    public List<string> GetCategories()
    {
        return templates.Select(t => t.Category).Distinct().OrderBy(c => c).ToList();
    }

    /// <summary>
    /// Create a new user-defined template
    /// </summary>
    public MetadataTemplate CreateTemplate(string name, string description, string category)
    {
        var template = new MetadataTemplate
        {
            Name = name,
            Description = description,
            Category = category,
            IsBuiltIn = false
        };

        templates.Add(template);
        SaveTemplate(template);

        TemplateChanged?.Invoke(this, new TemplateChangedEventArgs(template, TemplateChangeType.Created));

        return template;
    }

    /// <summary>
    /// Update an existing template
    /// </summary>
    public void UpdateTemplate(MetadataTemplate template)
    {
        if (template.IsBuiltIn)
            throw new InvalidOperationException("Cannot modify built-in templates. Create a copy instead.");

        template.ModifiedAt = DateTime.UtcNow;
        SaveTemplate(template);

        TemplateChanged?.Invoke(this, new TemplateChangedEventArgs(template, TemplateChangeType.Updated));
    }

    /// <summary>
    /// Delete a template
    /// </summary>
    public void DeleteTemplate(Guid templateId)
    {
        var template = GetTemplate(templateId);
        if (template == null)
            return;

        if (template.IsBuiltIn)
            throw new InvalidOperationException("Cannot delete built-in templates.");

        templates.Remove(template);

        var filePath = GetTemplateFilePath(template.Id);
        if (File.Exists(filePath))
            File.Delete(filePath);

        TemplateChanged?.Invoke(this, new TemplateChangedEventArgs(template, TemplateChangeType.Deleted));
    }

    /// <summary>
    /// Duplicate a template (useful for customizing built-in templates)
    /// </summary>
    public MetadataTemplate DuplicateTemplate(Guid templateId)
    {
        var sourceTemplate = GetTemplate(templateId);
        if (sourceTemplate == null)
            throw new ArgumentException("Template not found");

        var duplicate = sourceTemplate.Clone();
        templates.Add(duplicate);
        SaveTemplate(duplicate);

        TemplateChanged?.Invoke(this, new TemplateChangedEventArgs(duplicate, TemplateChangeType.Created));

        return duplicate;
    }

    /// <summary>
    /// Increment usage count when template is applied
    /// </summary>
    public void RecordTemplateUsage(Guid templateId)
    {
        var template = GetTemplate(templateId);
        if (template != null)
        {
            template.UsageCount++;
            if (!template.IsBuiltIn)
            {
                SaveTemplate(template);
            }
        }
    }

    /// <summary>
    /// Save template to disk
    /// </summary>
    private void SaveTemplate(MetadataTemplate template)
    {
        if (template.IsBuiltIn)
            return; // Don't save built-in templates to disk

        var filePath = GetTemplateFilePath(template.Id);
        var json = JsonConvert.SerializeObject(template, Formatting.Indented);
        File.WriteAllText(filePath, json);
    }

    /// <summary>
    /// Get file path for template
    /// </summary>
    private string GetTemplateFilePath(Guid templateId)
    {
        return Path.Combine(templatesPath, $"{templateId}.json");
    }

    /// <summary>
    /// Validate template before saving
    /// </summary>
    public bool ValidateTemplate(MetadataTemplate template, out List<string> errors)
    {
        return template.IsValid(out errors);
    }

    /// <summary>
    /// Export template to JSON file
    /// </summary>
    public void ExportTemplate(Guid templateId, string outputPath)
    {
        var template = GetTemplate(templateId);
        if (template == null)
            throw new ArgumentException("Template not found");

        var json = JsonConvert.SerializeObject(template, Formatting.Indented);
        File.WriteAllText(outputPath, json);
    }

    /// <summary>
    /// Import template from JSON file
    /// </summary>
    public MetadataTemplate ImportTemplate(string filePath)
    {
        var json = File.ReadAllText(filePath);
        var template = JsonConvert.DeserializeObject<MetadataTemplate>(json);

        if (template == null)
            throw new InvalidOperationException("Invalid template file");

        // Generate new ID and mark as non-built-in
        template.Id = Guid.NewGuid();
        template.IsBuiltIn = false;
        template.CreatedAt = DateTime.UtcNow;
        template.ModifiedAt = DateTime.UtcNow;

        templates.Add(template);
        SaveTemplate(template);

        TemplateChanged?.Invoke(this, new TemplateChangedEventArgs(template, TemplateChangeType.Created));

        return template;
    }
}

/// <summary>
/// Event args for template changes
/// </summary>
public class TemplateChangedEventArgs : EventArgs
{
    public MetadataTemplate Template { get; }
    public TemplateChangeType ChangeType { get; }

    public TemplateChangedEventArgs(MetadataTemplate template, TemplateChangeType changeType)
    {
        Template = template;
        ChangeType = changeType;
    }
}

/// <summary>
/// Types of template changes
/// </summary>
public enum TemplateChangeType
{
    Created,
    Updated,
    Deleted
}
