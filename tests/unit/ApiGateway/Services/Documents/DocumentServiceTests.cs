using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Moq;
using Xunit;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Http;
using ApiGateway.Services.Documents;
using ApiGateway.Models;

public class DocumentServiceTests
{
    private readonly DocumentService _service;
    private readonly Mock<IIMSClient> _imsClientMock;
    private readonly Mock<IDocumentStorage> _storageMock;
    private readonly Mock<ILogger<DocumentService>> _loggerMock;
    private readonly Mock<IValidator<DocumentMetadata>> _validatorMock;

    public DocumentServiceTests()
    {
        _imsClientMock = new Mock<IIMSClient>();
        _storageMock = new Mock<IDocumentStorage>();
        _loggerMock = new Mock<ILogger<DocumentService>>();
        _validatorMock = new Mock<IValidator<DocumentMetadata>>();

        _service = new DocumentService(
            _imsClientMock.Object,
            _storageMock.Object,
            _loggerMock.Object,
            _validatorMock.Object
        );
    }

    [Fact]
    public async Task GetQuoteDocuments_ReturnsAllDocuments()
    {
        // Arrange
        var quoteId = "Q123";
        var imsDocuments = new List<DocumentResponse>
        {
            new() { DocumentId = "1", Type = DocumentType.Quote }
        };
        var storedDocuments = new List<DocumentResponse>
        {
            new() { DocumentId = "2", Type = DocumentType.Other }
        };

        _imsClientMock.Setup(x => x.GetQuoteDocuments(quoteId))
            .ReturnsAsync(imsDocuments);
        _storageMock.Setup(x => x.ListDocuments($"quotes/{quoteId}"))
            .ReturnsAsync(storedDocuments);

        // Act
        var result = await _service.GetQuoteDocuments(quoteId);

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Contains(result, d => d.DocumentId == "1");
        Assert.Contains(result, d => d.DocumentId == "2");
    }

    [Fact]
    public async Task UploadDocument_ValidFile_Succeeds()
    {
        // Arrange
        var file = new Mock<IFormFile>();
        file.Setup(f => f.Length).Returns(1024); // 1KB
        file.Setup(f => f.ContentType).Returns("application/pdf");
        file.Setup(f => f.FileName).Returns("test.pdf");

        var metadata = new DocumentMetadata
        {
            ReferenceId = "Q123",
            Type = DocumentType.Quote
        };

        _validatorMock.Setup(x => x.ValidateAsync(metadata, default))
            .ReturnsAsync(new ValidationResult());

        var expectedResponse = new DocumentResponse
        {
            DocumentId = "test-id",
            FileName = "test.pdf"
        };

        _storageMock.Setup(x => x.UploadDocument(
            It.IsAny<string>(),
            It.IsAny<Stream>(),
            It.IsAny<DocumentInfo>()))
            .ReturnsAsync(expectedResponse);

        // Act
        var result = await _service.UploadDocument(file.Object, metadata);

        // Assert
        Assert.Equal(expectedResponse.DocumentId, result.DocumentId);
        Assert.Equal(expectedResponse.FileName, result.FileName);
    }

    [Fact]
    public async Task UploadDocument_InvalidFile_ThrowsException()
    {
        // Arrange
        var file = new Mock<IFormFile>();
        file.Setup(f => f.Length).Returns(20 * 1024 * 1024); // 20MB
        file.Setup(f => f.ContentType).Returns("application/pdf");

        var metadata = new DocumentMetadata
        {
            ReferenceId = "Q123",
            Type = DocumentType.Quote
        };

        // Act & Assert
        await Assert.ThrowsAsync<ValidationException>(() =>
            _service.UploadDocument(file.Object, metadata));
    }
} 