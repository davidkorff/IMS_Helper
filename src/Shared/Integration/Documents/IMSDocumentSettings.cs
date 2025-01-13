public class IMSDocumentSettings
{
    public int CacheExpirationMinutes { get; set; } = 60;
    public DocumentGenerationSettings GenerationSettings { get; set; }
    public SecuritySettings Security { get; set; }
    public RetryPolicy RetryPolicy { get; set; }
    public ValidationSettings Validation { get; set; }
    public StorageSettings Storage { get; set; }
}

public class DocumentGenerationSettings
{
    public int MaxConcurrentGenerations { get; set; } = 5;
    public int GenerationTimeoutSeconds { get; set; } = 60;
    public List<string> SupportedOutputFormats { get; set; } = new()
    {
        "PDF",
        "DOCX",
        "HTML"
    };
    public WatermarkSettings DefaultWatermark { get; set; }
    public bool EnableCompression { get; set; } = true;
    public int CompressionLevel { get; set; } = 7;
    public Dictionary<string, string> DefaultHeaders { get; set; }
}

public class SecuritySettings
{
    public bool EnableEncryption { get; set; } = true;
    public int DefaultEncryptionLevel { get; set; } = 128;
    public string EncryptionKey { get; set; }
    public bool RestrictPrinting { get; set; } = false;
    public bool RestrictCopy { get; set; } = true;
    public List<string> AllowedIPs { get; set; }
    public Dictionary<string, List<string>> TemplatePermissions { get; set; }
}

public class ValidationSettings
{
    public bool StrictValidation { get; set; } = true;
    public bool ValidateTemplateAccess { get; set; } = true;
    public int MaxFieldLength { get; set; } = 5000;
    public List<string> RestrictedFields { get; set; }
    public Dictionary<string, ValidationRule> CustomValidationRules { get; set; }
}

public class StorageSettings
{
    public string StoragePath { get; set; }
    public int RetentionDays { get; set; } = 30;
    public bool EnableVersioning { get; set; } = true;
    public int MaxVersions { get; set; } = 5;
    public bool AutoDeleteExpired { get; set; } = true;
    public List<string> ExcludedTemplates { get; set; }
}

public class WatermarkSettings
{
    public string DefaultText { get; set; } = "CONFIDENTIAL";
    public string FontFamily { get; set; } = "Arial";
    public int FontSize { get; set; } = 48;
    public string Color { get; set; } = "#808080";
    public int Opacity { get; set; } = 30;
    public string Position { get; set; } = "Center"
}

public class ValidationRule
{
    public string Pattern { get; set; }
    public string ErrorMessage { get; set; }
    public bool IsRequired { get; set; }
    public int? MinLength { get; set; }
    public int? MaxLength { get; set; }
    public string DataType { get; set; }
    public List<string> AllowedValues { get; set; }
} 