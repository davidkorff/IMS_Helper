using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using IMS.Integration.Document;
using IMS.Integration.Client;
using IMS.Integration.Settings;

namespace IMS.Integration.Tests.Integration.Document
{
    [Collection("IMS Integration Tests")]
    public class IMSDocumentServiceIntegrationTests : IAsyncLifetime
    {
        private readonly IMSDocumentService _service;
        private readonly IMSClient _imsClient;
        private readonly ILogger<IMSDocumentService> _logger;
        private readonly IMSTestConfiguration _config;
        private readonly IMSTestCleanup _cleanup;
        private readonly List<string> _createdDocuments;

        public IMSDocumentServiceIntegrationTests()
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
            }).CreateLogger<IMSDocumentService>();

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

            _service = new IMSDocumentService(
                _imsClient,
                _logger,
                cache,
                Options.Create(new IMSDocumentSettings()));

            _cleanup = new IMSTestCleanup(_imsClient, _logger);
            _createdDocuments = new List<string>();
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
            foreach (var documentId in _createdDocuments)
            {
                try
                {
                    await _cleanup.CleanupDocument(documentId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, 
                        "Failed to cleanup test document {DocumentId}", 
                        documentId);
                }
            }

            await _cleanup.DisposeAsync();
        }

        [Fact]
        public async Task UploadAndRetrieveDocument_Success()
        {
            // Arrange
            var request = new DocumentUploadRequest
            {
                EntityType = "Policy",
                EntityId = await CreateTestPolicy(),
                DocumentType = "Declaration",
                Name = "Test Document",
                ContentType = "application/pdf",
                Content = File.ReadAllBytes("test_files/test_document.pdf")
            };

            // Act
            var uploadResponse = await _service.UploadDocument(request);
            _createdDocuments.Add(uploadResponse.DocumentId);

            var retrievedDocument = await _service.GetDocument(uploadResponse.DocumentId);

            // Assert
            Assert.NotNull(retrievedDocument);
            Assert.Equal(request.Name, retrievedDocument.Name);
            Assert.Equal(request.ContentType, retrievedDocument.ContentType);
            Assert.Equal(request.Content.Length, retrievedDocument.Content.Length);
        }

        [Fact]
        public async Task GenerateDocument_Success()
        {
            // Arrange
            var request = new DocumentGenerationRequest
            {
                TemplateId = _config.TestTemplateId,
                EntityType = "Policy",
                EntityId = await CreateTestPolicy(),
                Data = new Dictionary<string, object>
                {
                    { "PolicyNumber", "TEST-POL-001" },
                    { "InsuredName", "John Doe" },
                    { "EffectiveDate", DateTime.Today.ToString("yyyy-MM-dd") }
                }
            };

            // Act
            var content = await _service.GenerateDocument(request);

            // Assert
            Assert.NotNull(content);
            Assert.True(content.Length > 0);
            
            // Verify it's a valid PDF
            Assert.StartsWith("%PDF", System.Text.Encoding.ASCII.GetString(content));
        }

        [Fact]
        public async Task GetDocumentsByPolicy_ReturnsDocuments()
        {
            // Arrange
            var policyNumber = await CreateTestPolicy();
            await UploadTestDocument(policyNumber, "Declaration");
            await UploadTestDocument(policyNumber, "Schedule");

            // Act
            var documents = await _service.GetDocumentsByPolicy(policyNumber);

            // Assert
            Assert.NotNull(documents);
            Assert.Equal(2, documents.Count);
            Assert.Contains(documents, d => d.Type == "Declaration");
            Assert.Contains(documents, d => d.Type == "Schedule");
        }

        private async Task<string> CreateTestPolicy()
        {
            // Implementation to create a test policy
            // This would typically involve creating a submission first
            throw new NotImplementedException();
        }

        private async Task<string> UploadTestDocument(
            string policyNumber, 
            string documentType)
        {
            var response = await _service.UploadDocument(
                new DocumentUploadRequest
                {
                    EntityType = "Policy",
                    EntityId = policyNumber,
                    DocumentType = documentType,
                    Name = $"Test {documentType}",
                    ContentType = "application/pdf",
                    Content = File.ReadAllBytes("test_files/test_document.pdf")
                });

            _createdDocuments.Add(response.DocumentId);
            return response.DocumentId;
        }
    }
} 