[Collection("IMS Integration Tests")]
public class IMSDocumentGeneratorIntegrationTests : IAsyncLifetime
{
    private readonly IMSDocumentGenerator _generator;
    private readonly IMSClient _imsClient;
    private readonly ILogger<IMSDocumentGenerator> _logger;
    private readonly IMSTestConfiguration _config;
    private readonly IMSTestCleanup _cleanup;
    private readonly List<string> _generatedDocuments;

    public IMSDocumentGeneratorIntegrationTests()
    {
        _config = IMSTestConfiguration.Load();
        
        var httpClient = new HttpClient
        {
            BaseAddress = new Uri(_config.BaseUrl)
        };

        _logger = LoggerFactory.Create(builder =>
        {
            builder.AddConsole();
            builder.AddDebug();
        }).CreateLogger<IMSDocumentGenerator>();

        _imsClient = new IMSClient(
            httpClient,
            _logger,
            Options.Create(new IMSSettings
            {
                BaseUrl = _config.BaseUrl,
                ProgramCode = _config.ProgramCode,
                ClientId = _config.ClientId
            }));

        var cache = new MemoryCache(new MemoryCacheOptions());

        _generator = new IMSDocumentGenerator(
            _imsClient,
            _logger,
            cache,
            Options.Create(new IMSDocumentSettings
            {
                CacheExpirationMinutes = 60,
                GenerationSettings = new DocumentGenerationSettings
                {
                    MaxConcurrentGenerations = 5,
                    GenerationTimeoutSeconds = 60
                }
            }));

        _cleanup = new IMSTestCleanup(_imsClient, _logger);
        _generatedDocuments = new List<string>();
    }

    public async Task InitializeAsync()
    {
        await _imsClient.LoginAsync(
            _config.ProgramCode,
            _config.TestEmail,
            _config.TestPassword);
    }

    public async Task DisposeAsync()
    {
        foreach (var documentId in _generatedDocuments)
        {
            try
            {
                await _generator.DeleteDocument(documentId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, 
                    "Failed to delete test document {DocumentId}", documentId);
            }
        }

        await _cleanup.DisposeAsync();
    }

    [Fact]
    public async Task GenerateDocument_PolicyDeclaration_GeneratesValidDocument()
    {
        // Arrange
        var templates = await _generator.GetAvailableTemplates("FLOOD", "FL");
        var policyDecTemplate = templates.First(t => 
            t.Name.Contains("Policy Declaration", StringComparison.OrdinalIgnoreCase));

        var request = new DocumentRequest
        {
            TemplateId = policyDecTemplate.TemplateId,
            Data = new Dictionary<string, object>
            {
                { "PolicyNumber", "TEST-POL-001" },
                { "InsuredName", "John Doe" },
                { "EffectiveDate", DateTime.Today },
                { "ExpirationDate", DateTime.Today.AddYears(1) },
                { "CoverageA", 250000 },
                { "Deductible", 1000 },
                { "Premium", 1500 }
            },
            OutputFormat = "PDF",
            Options = new DocumentOptions
            {
                AddWatermark = true,
                WatermarkText = "TEST DOCUMENT"
            }
        };

        // Act
        var documentId = await _generator.GenerateDocument(request);
        _generatedDocuments.Add(documentId);

        // Assert
        Assert.NotNull(documentId);
        var document = await _generator.GetDocument(documentId);
        Assert.NotNull(document);
        Assert.True(document.Length > 0);

        var metadata = await _generator.GetDocumentMetadata(documentId);
        Assert.Equal(request.TemplateId, metadata.TemplateId);
        Assert.Equal(request.OutputFormat, metadata.Format);
        Assert.Equal(DocumentStatus.Generated, metadata.Status);
    }

    [Fact]
    public async Task GetAvailableTemplates_ForFloodLine_ReturnsValidTemplates()
    {
        // Act
        var templates = await _generator.GetAvailableTemplates("FLOOD", "FL");

        // Assert
        Assert.NotNull(templates);
        Assert.NotEmpty(templates);
        Assert.Contains(templates, t => t.Category == "Policy");
        Assert.Contains(templates, t => t.Category == "Quote");
        Assert.All(templates, t =>
        {
            Assert.NotNull(t.TemplateId);
            Assert.NotNull(t.Name);
            Assert.Contains("FL", t.SupportedStates);
            Assert.True(t.EffectiveDate <= DateTime.Today);
            Assert.True(t.ExpirationDate == null || t.ExpirationDate > DateTime.Today);
        });
    }

    [Fact]
    public async Task MergeDocuments_MultiplePolicyForms_CreatesSingleDocument()
    {
        // Arrange
        var templates = await _generator.GetAvailableTemplates("FLOOD", "FL");
        var policyForms = templates
            .Where(t => t.Category == "Policy" && t.Name.Contains("Form"))
            .Take(2)
            .ToList();

        var documentIds = new List<string>();
        foreach (var template in policyForms)
        {
            var request = new DocumentRequest
            {
                TemplateId = template.TemplateId,
                Data = new Dictionary<string, object>
                {
                    { "PolicyNumber", "TEST-POL-002" },
                    { "InsuredName", "Jane Smith" }
                },
                OutputFormat = "PDF"
            };

            var documentId = await _generator.GenerateDocument(request);
            documentIds.Add(documentId);
            _generatedDocuments.Add(documentId);
        }

        var mergeOptions = new MergeOptions
        {
            OutputFormat = "PDF",
            AddTableOfContents = true,
            AddBookmarks = true
        };

        // Act
        var mergedDocumentId = await _generator.MergeDocuments(documentIds, mergeOptions);
        _generatedDocuments.Add(mergedDocumentId);

        // Assert
        Assert.NotNull(mergedDocumentId);
        var mergedDocument = await _generator.GetDocument(mergedDocumentId);
        Assert.NotNull(mergedDocument);
        Assert.True(mergedDocument.Length > 0);

        var metadata = await _generator.GetDocumentMetadata(mergedDocumentId);
        Assert.Equal("PDF", metadata.Format);
        Assert.Equal(DocumentStatus.Generated, metadata.Status);
    }

    [Fact]
    public async Task ValidateData_WithInvalidData_ReturnsValidationErrors()
    {
        // Arrange
        var templates = await _generator.GetAvailableTemplates("FLOOD", "FL");
        var template = templates.First(t => t.RequiredFields.Any());

        var data = new Dictionary<string, object>
        {
            // Intentionally missing required fields
            { "OptionalField", "Some Value" }
        };

        // Act
        var result = await _generator.ValidateData(template.TemplateId, data);

        // Assert
        Assert.False(result.IsValid);
        Assert.NotEmpty(result.Errors);
        Assert.All(result.Errors, error =>
        {
            Assert.NotNull(error.FieldName);
            Assert.NotNull(error.Message);
            Assert.Equal("MISSING_REQUIRED_FIELD", error.Code);
        });
    }

    [Fact]
    public async Task DeleteDocument_GeneratedDocument_SuccessfullyDeletes()
    {
        // Arrange
        var templates = await _generator.GetAvailableTemplates("FLOOD", "FL");
        var template = templates.First();

        var request = new DocumentRequest
        {
            TemplateId = template.TemplateId,
            Data = new Dictionary<string, object>
            {
                { "TestField", "Test Value" }
            },
            OutputFormat = "PDF"
        };

        var documentId = await _generator.GenerateDocument(request);

        // Act
        var result = await _generator.DeleteDocument(documentId);

        // Assert
        Assert.True(result);

        // Verify document is no longer accessible
        await Assert.ThrowsAsync<DocumentGenerationException>(
            async () => await _generator.GetDocument(documentId));
    }

    [Fact]
    public async Task GetRequiredFields_ForTemplate_ReturnsValidFields()
    {
        // Arrange
        var templates = await _generator.GetAvailableTemplates("FLOOD", "FL");
        var template = templates.First(t => t.RequiredFields.Any());

        // Act
        var fields = await _generator.GetRequiredFields(template.TemplateId);

        // Assert
        Assert.NotNull(fields);
        Assert.NotEmpty(fields);
        Assert.All(fields, field =>
        {
            Assert.NotNull(field.Name);
            Assert.NotNull(field.DisplayName);
            Assert.NotNull(field.DataType);
        });
    }
} 