public interface IDocumentGenerator
{
    Task<string> GenerateDocument(DocumentRequest request);
    Task<byte[]> GetDocument(string documentId);
    Task<List<DocumentTemplate>> GetAvailableTemplates(string lineOfBusiness, string state);
    Task<DocumentMetadata> GetDocumentMetadata(string documentId);
    Task<bool> DeleteDocument(string documentId);
    Task<List<DocumentField>> GetRequiredFields(string templateId);
    Task<DocumentValidationResult> ValidateData(string templateId, Dictionary<string, object> data);
    Task<string> MergeDocuments(List<string> documentIds, MergeOptions options);
}

public class DocumentRequest
{
    public string TemplateId { get; set; }
    public Dictionary<string, object> Data { get; set; }
    public string OutputFormat { get; set; } = "PDF";
    public DocumentOptions Options { get; set; }
    public string GeneratedBy { get; set; }
}

public class DocumentTemplate
{
    public string TemplateId { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
    public string Category { get; set; }
    public string Version { get; set; }
    public DateTime EffectiveDate { get; set; }
    public DateTime? ExpirationDate { get; set; }
    public List<string> SupportedFormats { get; set; }
    public List<string> SupportedStates { get; set; }
    public List<DocumentField> RequiredFields { get; set; }
    public ValidationRules ValidationRules { get; set; }
}

public class DocumentField
{
    public string Name { get; set; }
    public string DisplayName { get; set; }
    public string Description { get; set; }
    public string DataType { get; set; }
    public bool IsRequired { get; set; }
    public string DefaultValue { get; set; }
    public List<string> AllowedValues { get; set; }
    public ValidationRules ValidationRules { get; set; }
}

public class DocumentMetadata
{
    public string DocumentId { get; set; }
    public string TemplateId { get; set; }
    public string Name { get; set; }
    public string Format { get; set; }
    public long SizeInBytes { get; set; }
    public DateTime GeneratedDate { get; set; }
    public string GeneratedBy { get; set; }
    public Dictionary<string, object> UsedData { get; set; }
    public DocumentStatus Status { get; set; }
}

public class DocumentOptions
{
    public bool AddWatermark { get; set; }
    public string WatermarkText { get; set; }
    public bool AddPageNumbers { get; set; }
    public bool Compress { get; set; }
    public string Password { get; set; }
    public Dictionary<string, string> CustomHeaders { get; set; }
    public DocumentSecurity Security { get; set; }
}

public class MergeOptions
{
    public string OutputFormat { get; set; } = "PDF";
    public bool AddTableOfContents { get; set; }
    public bool AddBookmarks { get; set; }
    public string BookmarkTemplate { get; set; }
    public bool OptimizeSize { get; set; }
    public DocumentOptions DocumentOptions { get; set; }
}

public class DocumentSecurity
{
    public bool EnableCopy { get; set; }
    public bool EnablePrinting { get; set; }
    public bool EnableModification { get; set; }
    public string OwnerPassword { get; set; }
    public int EncryptionLevel { get; set; }
}

public class DocumentValidationResult
{
    public bool IsValid { get; set; }
    public List<DocumentValidationError> Errors { get; set; }
    public List<DocumentValidationWarning> Warnings { get; set; }
}

public class DocumentValidationError
{
    public string FieldName { get; set; }
    public string Message { get; set; }
    public string Code { get; set; }
}

public class DocumentValidationWarning
{
    public string FieldName { get; set; }
    public string Message { get; set; }
    public string Code { get; set; }
}

public enum DocumentStatus
{
    Pending,
    Generated,
    Failed,
    Expired,
    Deleted
} 