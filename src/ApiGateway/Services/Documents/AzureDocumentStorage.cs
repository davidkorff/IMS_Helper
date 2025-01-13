using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Azure.Storage.Blobs;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

public class AzureDocumentStorage : IDocumentStorage
{
    private readonly BlobServiceClient _blobServiceClient;
    private readonly string _containerName;
    private readonly ILogger<AzureDocumentStorage> _logger;

    public AzureDocumentStorage(
        IConfiguration configuration,
        ILogger<AzureDocumentStorage> logger)
    {
        _blobServiceClient = new BlobServiceClient(configuration["Azure:Storage:ConnectionString"]);
        _containerName = configuration["Azure:Storage:ContainerName"];
        _logger = logger;
    }

    public async Task<DocumentResponse> UploadDocument(string path, Stream content, DocumentInfo info)
    {
        try
        {
            var container = _blobServiceClient.GetBlobContainerClient(_containerName);
            var blob = container.GetBlobClient(path);

            // Upload content
            await blob.UploadAsync(content, new BlobUploadOptions
            {
                Metadata = new Dictionary<string, string>
                {
                    { "documentId", info.DocumentId },
                    { "type", info.Metadata.Type.ToString() },
                    { "description", info.Metadata.Description }
                }
            });

            return new DocumentResponse
            {
                DocumentId = info.DocumentId,
                FileName = info.FileName,
                ContentType = info.ContentType,
                FileSize = info.FileSize,
                UploadedAt = DateTime.UtcNow,
                Type = info.Metadata.Type,
                Description = info.Metadata.Description
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading document to blob storage");
            throw new StorageException("Failed to upload document to storage", ex);
        }
    }

    public async Task<List<DocumentResponse>> ListDocuments(string prefix)
    {
        try
        {
            var container = _blobServiceClient.GetBlobContainerClient(_containerName);
            var documents = new List<DocumentResponse>();

            await foreach (var blob in container.GetBlobsAsync(prefix: prefix))
            {
                var properties = await container.GetBlobClient(blob.Name)
                    .GetPropertiesAsync();

                documents.Add(new DocumentResponse
                {
                    DocumentId = blob.Metadata["documentId"],
                    FileName = Path.GetFileName(blob.Name),
                    ContentType = properties.Value.ContentType,
                    FileSize = properties.Value.ContentLength,
                    UploadedAt = properties.Value.LastModified.UtcDateTime,
                    Type = Enum.Parse<DocumentType>(blob.Metadata["type"]),
                    Description = blob.Metadata["description"]
                });
            }

            return documents;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing documents from blob storage");
            throw new StorageException("Failed to list documents from storage", ex);
        }
    }

    public async Task<DocumentContent> GetDocument(string documentId)
    {
        try
        {
            var container = _blobServiceClient.GetBlobContainerClient(_containerName);
            var blob = await FindBlobByDocumentId(container, documentId);

            var download = await blob.DownloadAsync();

            return new DocumentContent
            {
                Content = download.Value.Content,
                ContentType = download.Value.Details.ContentType,
                FileName = Path.GetFileName(blob.Name)
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving document from blob storage");
            throw new StorageException("Failed to retrieve document from storage", ex);
        }
    }

    private async Task<BlobClient> FindBlobByDocumentId(BlobContainerClient container, string documentId)
    {
        await foreach (var blob in container.GetBlobsAsync())
        {
            if (blob.Metadata.TryGetValue("documentId", out var blobDocumentId) 
                && blobDocumentId == documentId)
            {
                return container.GetBlobClient(blob.Name);
            }
        }

        throw new DocumentNotFoundException($"Document {documentId} not found");
    }
} 