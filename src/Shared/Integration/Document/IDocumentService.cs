public interface IDocumentService
{
    Task<DocumentResponse> GetDocument(string documentId);
    Task<List<DocumentMetadata>> GetDocumentsByPolicy(string policyNumber);
    Task<List<DocumentMetadata>> GetDocumentsBySubmission(string submissionId);
    Task<List<DocumentMetadata>> GetDocumentsByQuote(string quoteId);
    Task<DocumentResponse> UploadDocument(DocumentUploadRequest request);
    Task<bool> DeleteDocument(string documentId);
    Task<DocumentMetadata> UpdateDocumentMetadata(string documentId, DocumentMetadataUpdate update);
    Task<byte[]> GenerateDocument(DocumentGenerationRequest request);
    Task<List<DocumentTemplate>> GetAvailableTemplates(string lineOfBusiness, string state);
    Task<DocumentSearchResponse> SearchDocuments(DocumentSearchRequest request);
}

public class DocumentResponse
{
    public string DocumentId { get; set; }
    public string Name { get; set; }
    public string Type { get; set; }
    public string ContentType { get; set; }
    public long Size { get; set; }
    public byte[] Content { get; set; }
    public Dictionary<string, string> Metadata { get; set; }
    public DateTime CreatedDate { get; set; }
    public string CreatedBy { get; set; }
    public DateTime? ModifiedDate { get; set; }
    public string ModifiedBy { get; set; }
}

// Additional classes and models... 