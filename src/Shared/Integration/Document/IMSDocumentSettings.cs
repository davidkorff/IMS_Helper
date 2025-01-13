public class IMSDocumentSettings
{
    public int CacheExpirationMinutes { get; set; } = 60;
    public DocumentEngineSettings EngineSettings { get; set; }
    public StorageSettings StorageSettings { get; set; }
    public ValidationSettings ValidationSettings { get; set; }
    public TemplateSettings TemplateSettings { get; set; }
    public RetryPolicy RetryPolicy { get; set; }
}

public class DocumentEngineSettings
{
    public bool EnableParallelProcessing { get; set; } = true;
    public int MaxConcurrentUploads { get; set; } = 5;
    public int UploadTimeoutSeconds { get; set; } = 300;
    public bool CompressDocuments { get; set; } = true
    public int MaxDocumentSizeMB { get; set; } = 25;
    public List<string> AllowedFileTypes { get; set; }
    public Dictionary<string, DocumentTypeSettings> DocumentTypeConfigs { get; set; }
}

public class StorageSettings
{
    public string StorageProvider { get; set; } = "FileSystem";
    public string BasePath { get; set; }
    public bool EnableVersioning { get; set; } = true;
    public int RetentionDays { get; set; } = 90;
    public bool EncryptDocuments { get; set; } = true;
    public string EncryptionKey { get; set; }
    public Dictionary<string, string> ProviderSettings { get; set; }
}

public class ValidationSettings
{
    public bool StrictValidation { get; set; } = true;
    public List<string> RequiredMetadata { get; set; }
    public Dictionary<string, ValidationRule> CustomValidationRules { get; set; }
    public bool ValidateFileTypes { get; set; } = true;
    public bool ValidateFileSize { get; set; } = true;
    public int MaxRetryAttempts { get; set; } = 3;
}

public class TemplateSettings
{
    public string TemplateStoragePath { get; set; }
    public bool CacheTemplates { get; set; } = true;
    public int TemplateCacheMinutes { get; set; } = 60;
    public Dictionary<string, string> TemplateDefaults { get; set; }
    public List<string> SupportedTemplateTypes { get; set; }
}

public class DocumentTypeSettings
{
    public bool RequiresApproval { get; set; }
    public bool RequiresMetadata { get; set; }
    public List<string> RequiredMetadataFields { get; set; }
    public List<string> AllowedFileTypes { get; set; }
    public int MaxSizeMB { get; set; }
    public Dictionary<string, ValidationRule> ValidationRules { get; set; }
}

public class ValidationRule
{
    public string Expression { get; set; }
    public string ErrorMessage { get; set; }
    public string ErrorCode { get; set; }
    public bool IsBlocking { get; set; }
    public int Priority { get; set; }
} 