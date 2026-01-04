using System.ComponentModel;

namespace AutoCaptureOCR.Core.Models;

/// <summary>
/// Defines a metadata template with custom fields
/// </summary>
public class MetadataTemplate : INotifyPropertyChanged
{
    private string name = string.Empty;
    private string description = string.Empty;
    private string category = string.Empty;

    public Guid Id { get; set; } = Guid.NewGuid();

    public string Name
    {
        get => name;
        set
        {
            if (name != value)
            {
                name = value;
                OnPropertyChanged(nameof(Name));
            }
        }
    }

    public string Description
    {
        get => description;
        set
        {
            if (description != value)
            {
                description = value;
                OnPropertyChanged(nameof(Description));
            }
        }
    }

    /// <summary>
    /// Category for grouping templates (e.g., "Development", "Finance", "General")
    /// </summary>
    public string Category
    {
        get => category;
        set
        {
            if (category != value)
            {
                category = value;
                OnPropertyChanged(nameof(Category));
            }
        }
    }

    public List<MetadataField> Fields { get; set; } = new();
    public bool IsBuiltIn { get; set; } = false;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ModifiedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Usage count for analytics
    /// </summary>
    public int UsageCount { get; set; }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    /// <summary>
    /// Creates a copy of this template (useful for duplicating built-in templates)
    /// </summary>
    public MetadataTemplate Clone()
    {
        return new MetadataTemplate
        {
            Id = Guid.NewGuid(),
            Name = $"{Name} (Copy)",
            Description = Description,
            Category = Category,
            IsBuiltIn = false,
            Fields = Fields.Select(f => f.Clone()).ToList(),
            CreatedAt = DateTime.UtcNow,
            ModifiedAt = DateTime.UtcNow,
            UsageCount = 0
        };
    }

    /// <summary>
    /// Validates that all required fields are properly defined
    /// </summary>
    public bool IsValid(out List<string> errors)
    {
        errors = new List<string>();

        if (string.IsNullOrWhiteSpace(Name))
            errors.Add("Template name is required");

        if (Fields.Count == 0)
            errors.Add("Template must have at least one field");

        var fieldNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var field in Fields)
        {
            if (string.IsNullOrWhiteSpace(field.Name))
                errors.Add("All fields must have a name");

            if (fieldNames.Contains(field.Name))
                errors.Add($"Duplicate field name: {field.Name}");
            else
                fieldNames.Add(field.Name);
        }

        return errors.Count == 0;
    }
}

/// <summary>
/// Defines a single field in a metadata template
/// </summary>
public class MetadataField : INotifyPropertyChanged
{
    private string name = string.Empty;
    private string label = string.Empty;
    private MetadataFieldType fieldType = MetadataFieldType.Text;
    private bool isRequired;
    private string? defaultValue;
    private string? placeholder;
    private string? helpText;

    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Internal field name (used as key in metadata dictionary)
    /// </summary>
    public string Name
    {
        get => name;
        set
        {
            if (name != value)
            {
                name = value;
                OnPropertyChanged(nameof(Name));
            }
        }
    }

    /// <summary>
    /// Display label shown in UI
    /// </summary>
    public string Label
    {
        get => label;
        set
        {
            if (label != value)
            {
                label = value;
                OnPropertyChanged(nameof(Label));
            }
        }
    }

    public MetadataFieldType FieldType
    {
        get => fieldType;
        set
        {
            if (fieldType != value)
            {
                fieldType = value;
                OnPropertyChanged(nameof(FieldType));
            }
        }
    }

    public bool IsRequired
    {
        get => isRequired;
        set
        {
            if (isRequired != value)
            {
                isRequired = value;
                OnPropertyChanged(nameof(IsRequired));
            }
        }
    }

    public string? DefaultValue
    {
        get => defaultValue;
        set
        {
            if (defaultValue != value)
            {
                defaultValue = value;
                OnPropertyChanged(nameof(DefaultValue));
            }
        }
    }

    public List<string>? DropdownOptions { get; set; }

    public string? Placeholder
    {
        get => placeholder;
        set
        {
            if (placeholder != value)
            {
                placeholder = value;
                OnPropertyChanged(nameof(Placeholder));
            }
        }
    }

    /// <summary>
    /// Help text shown to user as tooltip or hint
    /// </summary>
    public string? HelpText
    {
        get => helpText;
        set
        {
            if (helpText != value)
            {
                helpText = value;
                OnPropertyChanged(nameof(HelpText));
            }
        }
    }

    public int DisplayOrder { get; set; }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    /// <summary>
    /// Creates a copy of this field definition
    /// </summary>
    public MetadataField Clone()
    {
        return new MetadataField
        {
            Id = Guid.NewGuid(),
            Name = Name,
            Label = Label,
            FieldType = FieldType,
            IsRequired = IsRequired,
            DefaultValue = DefaultValue,
            DropdownOptions = DropdownOptions != null ? new List<string>(DropdownOptions) : null,
            Placeholder = Placeholder,
            HelpText = HelpText,
            DisplayOrder = DisplayOrder
        };
    }
}

/// <summary>
/// Types of metadata fields supported
/// </summary>
public enum MetadataFieldType
{
    Text,           // Single-line text input
    MultilineText,  // Multi-line text area
    Number,         // Numeric input
    Date,           // Date picker
    Dropdown,       // Dropdown/combo box with predefined options
    Checkbox,       // Boolean checkbox
    Email,          // Email address input
    Url,            // URL input
    Currency,       // Currency amount
    MultiSelect     // Multiple selection checkboxes
}

/// <summary>
/// Applied metadata values for a capture
/// </summary>
public class CaptureMetadata
{
    public Guid TemplateId { get; set; }
    public string TemplateName { get; set; } = string.Empty;
    public Dictionary<string, string> Values { get; set; } = new();
    public DateTime AppliedAt { get; set; } = DateTime.UtcNow;
}
