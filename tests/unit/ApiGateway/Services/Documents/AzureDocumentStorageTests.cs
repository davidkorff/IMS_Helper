using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

public class AzureDocumentStorageTests
{
    private readonly AzureDocumentStorage _storage;
    private readonly Mock<BlobServiceClient> _blobServiceClientMock;
    private readonly Mock<BlobContainerClient> _containerClientMock;
    private readonly Mock<BlobClient> _blobClientMock;
    private readonly Mock<ILogger<AzureDocumentStorage>> _loggerMock;
    private readonly Mock<IConfiguration> _configMock;

    public AzureDocumentStorageTests()
    {
        _blobServiceClientMock = new Mock<BlobServiceClient>();
        _containerClientMock = new Mock<BlobContainerClient>();
        _blobClientMock = new Mock<BlobClient>();
        _loggerMock = new Mock<ILogger<AzureDocumentStorage>>();
        _configMock = new Mock<IConfiguration>();

        _configMock.Setup(x => x["Azure:Storage:ConnectionString"])
            .Returns("UseDevelopmentStorage=true");
        _configMock.Setup(x => x["Azure:Storage:ContainerName"])
            .Returns("documents");

        _blobServiceClientMock
            .Setup(x => x.GetBlobContainerClient(It.IsAny<string>()))
            .Returns(_containerClientMock.Object);

        _containerClientMock
            .Setup(x => x.GetBlobClient(It.IsAny<string>()))
            .Returns(_blobClientMock.Object);

        _storage = new AzureDocumentStorage(_configMock.Object, _loggerMock.Object);
    }

    [Fact]
    public async Task UploadDocument_Succeeds()
    {
        // Arrange
        var path = "quotes/Q123/doc1";
        var content = new MemoryStream(Encoding.UTF8.GetBytes("test content"));
        var info = new DocumentInfo
        {
            DocumentId = "doc1",
            FileName = "test.pdf",
            ContentType = "application/pdf",
            FileSize = 100,
            Metadata = new DocumentMetadata
            {
                Type = DocumentType.Quote,
                Description = "Test document"
            }
        };

        _blobClientMock
            .Setup(x => x.UploadAsync(
                It.IsAny<Stream>(),
                It.IsAny<BlobUploadOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Mock<Response<BlobContentInfo>>().Object);

        // Act
        var result = await _storage.UploadDocument(path, content, info);

        // Assert
        Assert.Equal(info.DocumentId, result.DocumentId);
        Assert.Equal(info.FileName, result.FileName);
        Assert.Equal(info.ContentType, result.ContentType);
    }

    [Fact]
    public async Task ListDocuments_ReturnsDocuments()
    {
        // Arrange
        var prefix = "quotes/Q123";
        var blobItems = new[]
        {
            BlobsModelFactory.BlobItem(
                name: "test.pdf",
                metadata: new Dictionary<string, string>
                {
                    { "documentId", "doc1" },
                    { "type", "Quote" },
                    { "description", "Test" }
                })
        };

        _containerClientMock
            .Setup(x => x.GetBlobsAsync(
                prefix: prefix,
                It.IsAny<CancellationToken>()))
            .Returns(AsyncPageable<BlobItem>.FromPages(
                new[] { Page<BlobItem>.FromValues(blobItems, null, null) }));

        // Act
        var results = await _storage.ListDocuments(prefix);

        // Assert
        Assert.Single(results);
        Assert.Equal("doc1", results[0].DocumentId);
    }

    [Fact]
    public async Task GetDocument_DocumentExists_ReturnsContent()
    {
        // Arrange
        var documentId = "doc1";
        var blobItems = new[]
        {
            BlobsModelFactory.BlobItem(
                name: "test.pdf",
                metadata: new Dictionary<string, string>
                {
                    { "documentId", documentId }
                })
        };

        _containerClientMock
            .Setup(x => x.GetBlobsAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Returns(AsyncPageable<BlobItem>.FromPages(
                new[] { Page<BlobItem>.FromValues(blobItems, null, null) }));

        var downloadResult = new Mock<Response<BlobDownloadInfo>>();
        downloadResult.Setup(x => x.Value.Content)
            .Returns(new MemoryStream());
        downloadResult.Setup(x => x.Value.Details.ContentType)
            .Returns("application/pdf");

        _blobClientMock
            .Setup(x => x.DownloadAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(downloadResult.Object);

        // Act
        var result = await _storage.GetDocument(documentId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("application/pdf", result.ContentType);
    }
} 