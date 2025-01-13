[XmlRoot(Namespace = "http://tempuri.org/")]
public class CreateDocumentRequest
{
    public string EntityId { get; set; }
    public DocumentMetadataXml Metadata { get; set; }
    public byte[] Content { get; set; }
}

[XmlRoot(Namespace = "http://tempuri.org/")]
public class CreateDocumentResponse
{
    public string DocumentId { get; set; }
    public string Status { get; set; }
}

public class DocumentMetadataXml
{
    public string Description { get; set; }
    public string DocumentType { get; set; }
    public string ReferenceId { get; set; }
    public DateTime DocumentDate { get; set; }
    public string FolderId { get; set; }
}

[XmlRoot(Namespace = "http://tempuri.org/")]
public class GetDocumentRequest
{
    public string DocumentId { get; set; }
}

[XmlRoot(Namespace = "http://tempuri.org/")]
public class GetDocumentResponse
{
    public string DocumentId { get; set; }
    public string FileName { get; set; }
    public string ContentType { get; set; }
    public byte[] Content { get; set; }
    public DocumentMetadataXml Metadata { get; set; }
} 