using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

public class IMSDocumentGeneratorTests
{
    private readonly IMSDocumentGenerator _generator;
    private readonly Mock<IIMSClient> _imsClientMock;
    private readonly Mock<ILogger<IMSDocumentGenerator>> _loggerMock;
    private readonly Mock<IMemoryCache> _cacheMock;
    private readonly IMSDocumentSettings _settings;

    public IMSDocumentGeneratorTests()
    {
        _imsClientMock = new Mock<IIMSClient>();
        _loggerMock = new Mock<ILogger<IMSDocumentGenerator>>();
        _cacheMock = new Mock<IMemoryCache>();
        
        _settings = new IMSDocumentSettings
        {
            CacheExpirationMinutes = 60,
            GenerationSettings = new DocumentGenerationSettings
            {
                MaxConcurrentGenerations = 5,
                GenerationTimeoutSeconds = 60,
                SupportedOutputFormats = new List<string> { "PDF", "DOCX" }
            }
        };

        _generator = new IMSDocumentGenerator(
            _imsClientMock.Object,
            _loggerMock.Object,
            _cacheMock.Object,
            Options.Create(_settings));
    }

    [Fact]
    public async Task GenerateDocument_ValidRequest_ReturnsDocumentId()
    {
        // Arrange
        var request = new DocumentRequest
        {
            TemplateId = "TEMPLATE_001",
            Data = new Dictionary<string, object>
            {
                { "PolicyNumber", "POL123" },
                { "InsuredName", "John Doe" }
            },
            OutputFormat = "PDF"
        };

        SetupTemplateResponse(request.TemplateId);
        SetupDocumentGeneration("DOC123");

        // Act
        var documentId = await _generator.GenerateDocument(request);

        // Assert
        Assert.NotNull(documentId);
        Assert.Equal("DOC123", documentId);
        VerifyDocumentGeneration(request.TemplateId);
    }

    [Fact]
    public async Task GetDocument_ValidId_ReturnsDocumentBytes()
    {
        // Arrange
        var documentId = "DOC123";
        var documentContent = Convert.ToBase64String(
            Encoding.UTF8.GetBytes("Sample document content"));

        SetupDocumentRetrieval(documentId, documentContent);

        // Act
        var result = await _generator.GetDocument(documentId);

        // Assert
        Assert.NotNull(result);
        Assert.NotEmpty(result);
        VerifyDocumentRetrieval(documentId);
    }

    [Fact]
    public async Task GetAvailableTemplates_ValidRequest_ReturnsTemplates()
    {
        // Arrange
        var lineOfBusiness = "FLOOD";
        var state = "FL";
        var templatesXml = CreateSampleTemplatesXml();

        SetupTemplatesRetrieval(lineOfBusiness, state, templatesXml);
        SetupCache($"doc_templates_{lineOfBusiness}_{state}", null);

        // Act
        var templates = await _generator.GetAvailableTemplates(lineOfBusiness, state);

        // Assert
        Assert.NotNull(templates);
        Assert.NotEmpty(templates);
        Assert.All(templates, template =>
        {
            Assert.NotNull(template.TemplateId);
            Assert.NotNull(template.Name);
            Assert.Contains(state, template.SupportedStates);
        });
    }

    [Fact]
    public async Task ValidateData_MissingRequiredField_ReturnsErrors()
    {
        // Arrange
        var templateId = "TEMPLATE_001";
        var data = new Dictionary<string, object>
        {
            { "OptionalField", "Value" }
            // Missing required field "RequiredField"
        };

        SetupTemplateResponse(templateId, includeRequiredFields: true);

        // Act
        var result = await _generator.ValidateData(templateId, data);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, 
            e => e.Code == "MISSING_REQUIRED_FIELD");
    }

    [Fact]
    public async Task MergeDocuments_ValidRequest_ReturnsMergedDocumentId()
    {
        // Arrange
        var documentIds = new List<string> { "DOC1", "DOC2" };
        var options = new MergeOptions
        {
            OutputFormat = "PDF",
            AddTableOfContents = true
        };

        SetupDocumentMerge("MERGED_DOC123");

        // Act
        var mergedDocumentId = await _generator.MergeDocuments(documentIds, options);

        // Assert
        Assert.NotNull(mergedDocumentId);
        Assert.Equal("MERGED_DOC123", mergedDocumentId);
        VerifyDocumentMerge(documentIds);
    }

    [Fact]
    public async Task DeleteDocument_ValidId_ReturnsSuccess()
    {
        // Arrange
        var documentId = "DOC123";
        SetupDocumentDeletion(documentId, true);

        // Act
        var result = await _generator.DeleteDocument(documentId);

        // Assert
        Assert.True(result);
        VerifyDocumentDeletion(documentId);
    }

    [Theory]
    [InlineData("", "Data cannot be empty")]
    [InlineData(null, "Template ID is required")]
    [InlineData("INVALID_FORMAT", "Unsupported output format")]
    public async Task GenerateDocument_InvalidRequest_ThrowsException(
        string outputFormat, 
        string expectedError)
    {
        // Arrange
        var request = new DocumentRequest
        {
            TemplateId = "TEMPLATE_001",
            Data = new Dictionary<string, object>(),
            OutputFormat = outputFormat
        };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<DocumentGenerationException>(
            () => _generator.GenerateDocument(request));
        Assert.Contains(expectedError, exception.Message);
    }

    private void SetupTemplateResponse(string templateId, bool includeRequiredFields = false)
    {
        var templateXml = CreateSampleTemplateXml(templateId, includeRequiredFields);
        _imsClientMock
            .Setup(x => x.SendRequestAsync<ExecuteCommandRequest, ExecuteCommandResponse>(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.Is<ExecuteCommandRequest>(r => 
                    r.Parameters.Contains(templateId))))
            .ReturnsAsync(new ExecuteCommandResponse { Result = templateXml });
    }

    private void SetupDocumentGeneration(string documentId)
    {
        var responseXml = $@"
            <DocumentResponse>
                <DocumentId>{documentId}</DocumentId>
                <Status>Success</Status>
            </DocumentResponse>";

        _imsClientMock
            .Setup(x => x.SendRequestAsync<ExecuteCommandRequest, ExecuteCommandResponse>(
                It.IsAny<string>(),
                "GenerateDocument",
                It.IsAny<ExecuteCommandRequest>()))
            .ReturnsAsync(new ExecuteCommandResponse { Result = responseXml });
    }

    private void SetupDocumentRetrieval(string documentId, string content)
    {
        _imsClientMock
            .Setup(x => x.SendRequestAsync<ExecuteCommandRequest, ExecuteCommandResponse>(
                It.IsAny<string>(),
                "GetDocument",
                It.Is<ExecuteCommandRequest>(r => 
                    r.Parameters.Contains(documentId))))
            .ReturnsAsync(new ExecuteCommandResponse { Result = content });
    }

    private void SetupTemplatesRetrieval(
        string lineOfBusiness, 
        string state, 
        string templatesXml)
    {
        _imsClientMock
            .Setup(x => x.SendRequestAsync<ExecuteCommandRequest, ExecuteCommandResponse>(
                It.IsAny<string>(),
                "GetTemplates",
                It.Is<ExecuteCommandRequest>(r => 
                    r.Parameters.Contains(lineOfBusiness) && 
                    r.Parameters.Contains(state))))
            .ReturnsAsync(new ExecuteCommandResponse { Result = templatesXml });
    }

    private void SetupDocumentMerge(string mergedDocumentId)
    {
        var responseXml = $@"
            <MergeResponse>
                <DocumentId>{mergedDocumentId}</DocumentId>
                <Status>Success</Status>
            </MergeResponse>";

        _imsClientMock
            .Setup(x => x.SendRequestAsync<ExecuteCommandRequest, ExecuteCommandResponse>(
                It.IsAny<string>(),
                "MergeDocuments",
                It.IsAny<ExecuteCommandRequest>()))
            .ReturnsAsync(new ExecuteCommandResponse { Result = responseXml });
    }

    private void SetupDocumentDeletion(string documentId, bool success)
    {
        _imsClientMock
            .Setup(x => x.SendRequestAsync<ExecuteCommandRequest, ExecuteCommandResponse>(
                It.IsAny<string>(),
                "DeleteDocument",
                It.Is<ExecuteCommandRequest>(r => 
                    r.Parameters.Contains(documentId))))
            .ReturnsAsync(new ExecuteCommandResponse { Result = success.ToString() });
    }

    private void SetupCache<T>(string key, T value)
    {
        object cached = value;
        _cacheMock
            .Setup(x => x.TryGetValue(key, out cached))
            .Returns(value != null);
    }

    private void VerifyDocumentGeneration(string templateId)
    {
        _imsClientMock.Verify(
            x => x.SendRequestAsync<ExecuteCommandRequest, ExecuteCommandResponse>(
                It.IsAny<string>(),
                "GenerateDocument",
                It.Is<ExecuteCommandRequest>(r => 
                    r.Parameters.Contains(templateId))),
            Times.Once);
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

    private void VerifyDocumentMerge(List<string> documentIds)
    {
        _imsClientMock.Verify(
            x => x.SendRequestAsync<ExecuteCommandRequest, ExecuteCommandResponse>(
                It.IsAny<string>(),
                "MergeDocuments",
                It.Is<ExecuteCommandRequest>(r => 
                    documentIds.All(id => r.Parameters.Contains(id)))),
            Times.Once);
    }

    private void VerifyDocumentDeletion(string documentId)
    {
        _imsClientMock.Verify(
            x => x.SendRequestAsync<ExecuteCommandRequest, ExecuteCommandResponse>(
                It.IsAny<string>(),
                "DeleteDocument",
                It.Is<ExecuteCommandRequest>(r => 
                    r.Parameters.Contains(documentId))),
            Times.Once);
    }

    private string CreateSampleTemplateXml(string templateId, bool includeRequiredFields)
    {
        var requiredFieldsXml = includeRequiredFields
            ? @"
                <RequiredFields>
                    <Field>
                        <Name>RequiredField</Name>
                        <DisplayName>Required Field</DisplayName>
                        <DataType>String</DataType>
                        <IsRequired>true</IsRequired>
                    </Field>
                </RequiredFields>"
            : "";

        return $@"
            <Template>
                <TemplateId>{templateId}</TemplateId>
                <Name>Sample Template</Name>
                <Description>Sample template for testing</Description>
                <Category>Policy</Category>
                <Version>1.0</Version>
                <EffectiveDate>2024-01-01</EffectiveDate>
                <SupportedFormats>
                    <Format>PDF</Format>
                    <Format>DOCX</Format>
                </SupportedFormats>
                <SupportedStates>
                    <State>FL</State>
                    <State>TX</State>
                </SupportedStates>
                {requiredFieldsXml}
            </Template>";
    }

    private string CreateSampleTemplatesXml()
    {
        return @"
            <Templates>
                <Template>
                    <TemplateId>TEMPLATE_001</TemplateId>
                    <Name>Policy Declaration</Name>
                    <Description>Policy declaration page</Description>
                    <Category>Policy</Category>
                    <Version>1.0</Version>
                    <EffectiveDate>2024-01-01</EffectiveDate>
                    <SupportedFormats>
                        <Format>PDF</Format>
                        <Format>DOCX</Format>
                    </SupportedFormats>
                    <SupportedStates>
                        <State>FL</State>
                        <State>TX</State>
                    </SupportedStates>
                </Template>
                <Template>
                    <TemplateId>TEMPLATE_002</TemplateId>
                    <Name>Quote Summary</Name>
                    <Description>Quote summary document</Description>
                    <Category>Quote</Category>
                    <Version>1.0</Version>
                    <EffectiveDate>2024-01-01</EffectiveDate>
                    <SupportedFormats>
                        <Format>PDF</Format>
                    </SupportedFormats>
                    <SupportedStates>
                        <State>FL</State>
                    </SupportedStates>
                </Template>
            </Templates>";
    }
} 