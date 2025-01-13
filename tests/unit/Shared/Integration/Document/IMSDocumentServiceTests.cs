using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

public class IMSDocumentServiceTests
{
    private readonly IMSDocumentService _service;
    private readonly Mock<IIMSClient> _imsClientMock;
    private readonly Mock<ILogger<IMSDocumentService>> _loggerMock;
    private readonly Mock<IMemoryCache> _cacheMock;
    private readonly IMSDocumentSettings _settings;

    public IMSDocumentServiceTests()
    {
        _imsClientMock = new Mock<IIMSClient>();
        _loggerMock = new Mock<ILogger<IMSDocumentService>>();
        _cacheMock = new Mock<IMemoryCache>();
        
        _settings = new IMSDocumentSettings
        {
            EngineSettings = new DocumentEngineSettings
            {
                EnableParallelProcessing = true,
                MaxConcurrentUploads = 5
            }
        };

        _service = new IMSDocumentService(
            _imsClientMock.Object,
            _loggerMock.Object,
            _cacheMock.Object,
            Options.Create(_settings));
    }

    [Fact]
    public async Task GetDocument_ValidId_ReturnsDocument()
    {
        // Arrange
        var documentId = "DOC123";
        SetupDocumentResponse(documentId, "Test Document");

        // Act
        var response = await _service.GetDocument(documentId);

        // Assert
        Assert.NotNull(response);
        Assert.Equal(documentId, response.DocumentId);
        VerifyDocumentRetrieval(documentId);
    }

    [Fact]
    public async Task GetDocumentsByPolicy_ValidPolicy_ReturnsDocuments()
    {
        // Arrange
        var policyNumber = "POL123";
        SetupPolicyDocumentsResponse(policyNumber);

        // Act
        var documents = await _service.GetDocumentsByPolicy(policyNumber);

        // Assert
        Assert.NotNull(documents);
        Assert.NotEmpty(documents);
        VerifyPolicyDocumentsRetrieval(policyNumber);
    }

    [Fact]
    public async Task UploadDocument_ValidRequest_ReturnsDocumentResponse()
    {
        // Arrange
        var request = CreateSampleUploadRequest();
        SetupDocumentUploadResponse("DOC123");

        // Act
        var response = await _service.UploadDocument(request);

        // Assert
        Assert.NotNull(response);
        Assert.Equal("DOC123", response.DocumentId);
        VerifyDocumentUpload();
    }

    [Fact]
    public async Task GenerateDocument_ValidRequest_ReturnsDocumentContent()
    {
        // Arrange
        var request = CreateSampleGenerationRequest();
        SetupDocumentGenerationResponse();

        // Act
        var content = await _service.GenerateDocument(request);

        // Assert
        Assert.NotNull(content);
        Assert.True(content.Length > 0);
        VerifyDocumentGeneration();
    }

    private DocumentUploadRequest CreateSampleUploadRequest()
    {
        return new DocumentUploadRequest
        {
            EntityType = "Policy",
            EntityId = "POL123",
            DocumentType = "Declaration",
            Name = "Test Document",
            ContentType = "application/pdf",
            Content = new byte[] { 1, 2, 3, 4, 5 }
        };
    }

    private DocumentGenerationRequest CreateSampleGenerationRequest()
    {
        return new DocumentGenerationRequest
        {
            TemplateId = "TEMPLATE_001",
            EntityType = "Policy",
            EntityId = "POL123",
            Data = new Dictionary<string, object>
            {
                { "PolicyNumber", "POL123" },
                { "InsuredName", "John Doe" }
            }
        };
    }

    private void SetupDocumentResponse(string documentId, string name)
    {
        var responseXml = CreateSampleDocumentResponseXml(documentId, name);
        _imsClientMock
            .Setup(x => x.SendRequestAsync<ExecuteCommandRequest, ExecuteCommandResponse>(
                It.IsAny<string>(),
                "GetDocument",
                It.Is<ExecuteCommandRequest>(r => 
                    r.Parameters.Contains(documentId))))
            .ReturnsAsync(new ExecuteCommandResponse { Result = responseXml });
    }

    private void SetupPolicyDocumentsResponse(string policyNumber)
    {
        var responseXml = CreateSamplePolicyDocumentsResponseXml();
        _imsClientMock
            .Setup(x => x.SendRequestAsync<ExecuteCommandRequest, ExecuteCommandResponse>(
                It.IsAny<string>(),
                "GetDocumentsByPolicy",
                It.Is<ExecuteCommandRequest>(r => 
                    r.Parameters.Contains(policyNumber))))
            .ReturnsAsync(new ExecuteCommandResponse { Result = responseXml });
    }

    private void SetupDocumentUploadResponse(string documentId)
    {
        var responseXml = CreateSampleDocumentResponseXml(documentId, "Uploaded Document");
        _imsClientMock
            .Setup(x => x.SendRequestAsync<ExecuteCommandRequest, ExecuteCommandResponse>(
                It.IsAny<string>(),
                "UploadDocument",
                It.IsAny<ExecuteCommandRequest>()))
            .ReturnsAsync(new ExecuteCommandResponse { Result = responseXml });
    }

    private void SetupDocumentGenerationResponse()
    {
        var content = Convert.ToBase64String(new byte[] { 1, 2, 3, 4, 5 });
        _imsClientMock
            .Setup(x => x.SendRequestAsync<ExecuteCommandRequest, ExecuteCommandResponse>(
                It.IsAny<string>(),
                "GenerateDocument",
                It.IsAny<ExecuteCommandRequest>()))
            .ReturnsAsync(new ExecuteCommandResponse { Result = content });
    }

    private void VerifyDocumentRetrieval(string documentId)
    {
        _imsClientMock.Verify(
            x => x.SendRequestAsync<ExecuteCommandRequest, ExecuteCommandResponse>(
                It.IsAny<string>(),
                "GetDocument",
                It.Is<ExecuteCommandRequest>(r => 
                    r.Parameters.Contains(documentId))),
            Times.Once);
    }

    private void VerifyPolicyDocumentsRetrieval(string policyNumber)
    {
        _imsClientMock.Verify(
            x => x.SendRequestAsync<ExecuteCommandRequest, ExecuteCommandResponse>(
                It.IsAny<string>(),
                "GetDocumentsByPolicy",
                It.Is<ExecuteCommandRequest>(r => 
                    r.Parameters.Contains(policyNumber))),
            Times.Once);
    }

    private void VerifyDocumentUpload()
    {
        _imsClientMock.Verify(
            x => x.SendRequestAsync<ExecuteCommandRequest, ExecuteCommandResponse>(
                It.IsAny<string>(),
                "UploadDocument",
                It.IsAny<ExecuteCommandRequest>()),
            Times.Once);
    }

    private void VerifyDocumentGeneration()
    {
        _imsClientMock.Verify(
            x => x.SendRequestAsync<ExecuteCommandRequest, ExecuteCommandResponse>(
                It.IsAny<string>(),
                "GenerateDocument",
                It.IsAny<ExecuteCommandRequest>()),
            Times.Once);
    }

    private string CreateSampleDocumentResponseXml(string documentId, string name)
    {
        return $@"
            <Document>
                <DocumentId>{documentId}</DocumentId>
                <Name>{name}</Name>
                <Type>Declaration</Type>
                <ContentType>application/pdf</ContentType>
                <Size>1024</Size>
                <Content>{Convert.ToBase64String(new byte[] { 1, 2, 3, 4, 5 })}</Content>
                <CreatedDate>{DateTime.Now:yyyy-MM-dd}</CreatedDate>
                <CreatedBy>Test User</CreatedBy>
            </Document>";
    }

    private string CreateSamplePolicyDocumentsResponseXml()
    {
        return @"
            <Documents>
                <Document>
                    <DocumentId>DOC123</DocumentId>
                    <Name>Policy Declaration</Name>
                    <Type>Declaration</Type>
                    <ContentType>application/pdf</ContentType>
                    <Size>1024</Size>
                    <CreatedDate>2024-01-01</CreatedDate>
                </Document>
                <Document>
                    <DocumentId>DOC124</DocumentId>
                    <Name>Policy Schedule</Name>
                    <Type>Schedule</Type>
                    <ContentType>application/pdf</ContentType>
                    <Size>2048</Size>
                    <CreatedDate>2024-01-01</CreatedDate>
                </Document>
            </Documents>";
    }
} 