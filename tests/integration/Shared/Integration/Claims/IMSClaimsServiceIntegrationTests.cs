using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using IMS.Integration.Claims;
using IMS.Integration.Clients;
using IMS.Integration.Settings;

namespace IMS.Integration.Tests.Integration.Claims
{
    [Collection("IMS Integration Tests")]
    public class IMSClaimsServiceIntegrationTests : IAsyncLifetime
    {
        private readonly IMSClaimsService _service;
        private readonly IMSClient _imsClient;
        private readonly ILogger<IMSClaimsService> _logger;
        private readonly IMSTestConfiguration _config;
        private readonly IMSTestCleanup _cleanup;
        private readonly List<string> _createdClaims;

        public IMSClaimsServiceIntegrationTests()
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
            }).CreateLogger<IMSClaimsService>();

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

            _service = new IMSClaimsService(
                _imsClient,
                _logger,
                cache,
                Options.Create(new IMSClaimsSettings()));

            _cleanup = new IMSTestCleanup(_imsClient, _logger);
            _createdClaims = new List<string>();
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
            foreach (var claimId in _createdClaims)
            {
                try
                {
                    // Cleanup logic for claims
                    await _cleanup.CleanupClaim(claimId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, 
                        "Failed to cleanup test claim {ClaimId}", claimId);
                }
            }

            await _cleanup.DisposeAsync();
        }

        [Fact]
        public async Task CreateClaim_WithValidData_CreatesClaimSuccessfully()
        {
            // Arrange
            var request = new ClaimCreationRequest
            {
                PolicyNumber = await CreateTestPolicy(),
                DateOfLoss = DateTime.Today.AddDays(-1),
                LossDescription = "Test water damage claim",
                ReportedBy = "Integration Test",
                ReportedDate = DateTime.Today,
                LossType = "Property",
                CauseOfLoss = "Water",
                LossLocation = new Address
                {
                    Street1 = "123 Test St",
                    City = "Test City",
                    State = "FL",
                    ZipCode = "33133"
                }
            };

            // Act
            var claimId = await _service.CreateClaim(request);
            _createdClaims.Add(claimId);

            // Assert
            Assert.NotNull(claimId);
            var details = await _service.GetClaimDetails(claimId);
            Assert.Equal(request.PolicyNumber, details.PolicyNumber);
            Assert.Equal(request.DateOfLoss.Date, details.DateOfLoss.Date);
            Assert.Equal(ClaimStatus.New, details.Status);
        }

        [Fact]
        public async Task ProcessClaimPayment_ValidPayment_ProcessesSuccessfully()
        {
            // Arrange
            var claimId = await CreateTestClaim();
            _createdClaims.Add(claimId);

            var request = new PaymentRequest
            {
                Amount = 1000,
                PaymentType = "Repair",
                PayeeName = "Test Contractor",
                PayeeType = "Vendor",
                Coverage = "Property",
                PaymentReason = "Initial repair payment",
                ApprovedBy = _config.TestAdjusterGuid
            };

            // Act
            var response = await _service.ProcessClaimPayment(claimId, request);

            // Assert
            Assert.NotNull(response);
            Assert.NotNull(response.PaymentId);
            Assert.Equal(request.Amount, response.Amount);
            Assert.Equal("Processed", response.Status);
        }

        [Fact]
        public async Task UploadClaimDocument_ValidDocument_UploadsSuccessfully()
        {
            // Arrange
            var claimId = await CreateTestClaim();
            _createdClaims.Add(claimId);

            var document = new ClaimDocumentUpload
            {
                DocumentType = "Estimate",
                Description = "Test estimate document",
                FileName = "test_estimate.pdf",
                Content = File.ReadAllBytes("test_files/test_estimate.pdf"),
                UploadedBy = "Integration Test",
                Metadata = new Dictionary<string, string>
                {
                    { "EstimateAmount", "1000.00" },
                    { "Contractor", "Test Contractor" }
                }
            };

            // Act
            var documentId = await _service.UploadClaimDocument(claimId, document);

            // Assert
            Assert.NotNull(documentId);
            var documents = await _service.GetClaimDocuments(claimId);
            Assert.Contains(documents, d => d.DocumentId == documentId);
        }

        [Fact]
        public async Task AddClaimNote_ValidNote_AddsSuccessfully()
        {
            // Arrange
            var claimId = await CreateTestClaim();
            _createdClaims.Add(claimId);

            var note = new ClaimNoteRequest
            {
                NoteType = "General",
                Content = "Test note from integration test",
                CreatedBy = "Integration Test",
                IsPrivate = false,
                Tags = new List<string> { "Test", "Integration" }
            };

            // Act
            var noteId = await _service.AddClaimNote(claimId, note);

            // Assert
            Assert.NotNull(noteId);
            var notes = await _service.GetClaimNotes(claimId);
            var addedNote = notes.FirstOrDefault(n => n.NoteId == noteId);
            Assert.NotNull(addedNote);
            Assert.Equal(note.Content, addedNote.Content);
        }

        private async Task<string> CreateTestPolicy()
        {
            // Implementation to create a test policy
            // This would typically involve creating a submission first
            throw new NotImplementedException();
        }

        private async Task<string> CreateTestClaim()
        {
            var policyNumber = await CreateTestPolicy();
            var request = new ClaimCreationRequest
            {
                PolicyNumber = policyNumber,
                DateOfLoss = DateTime.Today.AddDays(-1),
                LossDescription = "Test claim",
                ReportedBy = "Integration Test",
                ReportedDate = DateTime.Today,
                LossType = "Property",
                CauseOfLoss = "Water"
            };

            return await _service.CreateClaim(request);
        }
    }
} 