public interface IDocumentService
{
    Task<List<DocumentResponse>> GetQuoteDocuments(string quoteId);
    Task<List<DocumentResponse>> GetPolicyDocuments(string policyId);
    Task<DocumentResponse> UploadDocument(IFormFile file, DocumentMetadata metadata);
    Task<DocumentContent> GetDocument(string documentId);
}

public class DocumentMetadata
{
    public string ReferenceId { get; set; } // Quote or Policy ID
    public DocumentType Type { get; set; }
    public string Description { get; set; }
}

public class DocumentResponse
{
    public string DocumentId { get; set; }
    public string FileName { get; set; }
    public string ContentType { get; set; }
    public long FileSize { get; set; }
    public DateTime UploadedAt { get; set; }
    public DocumentType Type { get; set; }
    public string Description { get; set; }
}

public class DocumentContent
{
    public Stream Content { get; set; }
    public string ContentType { get; set; }
    public string FileName { get; set; }
}

public enum DocumentType
{
    Quote,
    Policy,
    Endorsement,
    Cancellation,
    Invoice,
    Other
} 