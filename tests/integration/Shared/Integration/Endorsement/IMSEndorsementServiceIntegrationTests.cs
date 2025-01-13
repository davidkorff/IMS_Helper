using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using IMSShared.Integration.Endorsement;
using IMSShared.Integration.IMS;
using IMSShared.Integration.Tests;

namespace IMSShared.Integration.Endorsement
{
    [Collection("IMS Integration Tests")]
    public class IMSEndorsementServiceIntegrationTests : IAsyncLifetime
    {
        private readonly IMSEndorsementService _service;
        private readonly IMSClient _imsClient;
        private readonly ILogger<IMSEndorsementService> _logger;
        private readonly IMSTestConfiguration _config;
        private readonly IMSTestCleanup _cleanup;
        private readonly List<string> _createdEndorsements;

        public IMSEndorsementServiceIntegrationTests()
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
            }).CreateLogger<IMSEndorsementService>();

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

            _service = new IMSEndorsementService(
                _imsClient,
                _logger,
                cache,
                Options.Create(new IMSEndorsementSettings()));

            _cleanup = new IMSTestCleanup(_imsClient, _logger);
            _createdEndorsements = new List<string>();
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
            foreach (var endorsementId in _createdEndorsements)
            {
                try
                {
                    await _cleanup.CleanupEndorsement(endorsementId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, 
                        "Failed to cleanup test endorsement {EndorsementId}", 
                        endorsementId);
                }
            }

            await _cleanup.DisposeAsync();
        }

        [Fact]
        public async Task CreateEndorsement_WithValidPolicy_CreatesEndorsement()
        {
            // Arrange
            var policyNumber = await CreateTestPolicy();
            var request = new EndorsementRequest
            {
                PolicyNumber = policyNumber,
                EndorsementType = "ADDRESS_CHANGE",
                EffectiveDate = DateTime.Today.AddDays(30),
                Changes = new Dictionary<string, object>
                {
                    { "MailingAddress.Street", "123 New Street" },
                    { "MailingAddress.City", "New City" }
                },
                RequestedBy = "Integration Test",
                Reason = "Address Update"
            };

            // Act
            var response = await _service.CreateEndorsement(request);
            _createdEndorsements.Add(response.EndorsementId);

            // Assert
            Assert.NotNull(response);
            Assert.NotNull(response.EndorsementId);
            Assert.Equal(request.PolicyNumber, response.PolicyNumber);
            Assert.Equal(EndorsementStatus.Draft, response.Status);
        }

        [Fact]
        public async Task GetEndorsementWorkflow_ForAutoLine_ReturnsValidWorkflow()
        {
            // Arrange
            var lineOfBusiness = "AUTO";
            var state = "CA";

            // Act
            var workflow = await _service.GetEndorsementWorkflow(lineOfBusiness, state);

            // Assert
            Assert.NotNull(workflow);
            Assert.Equal(lineOfBusiness, workflow.LineOfBusiness);
            Assert.Equal(state, workflow.State);
            Assert.NotEmpty(workflow.Steps);
            Assert.Contains(workflow.Steps, 
                s => s.Name == "Validation");
            Assert.Contains(workflow.Steps, 
                s => s.Name == "Document Generation");
        }

        [Fact]
        public async Task ValidateEndorsement_WithInvalidChanges_ReturnsErrors()
        {
            // Arrange
            var endorsementId = await CreateTestEndorsement();
            _createdEndorsements.Add(endorsementId);

            // Act
            var errors = await _service.ValidateEndorsement(endorsementId);

            // Assert
            Assert.NotNull(errors);
            Assert.NotEmpty(errors);
            Assert.Contains(errors, 
                e => e.ErrorCode == "INVALID_DATE" && e.IsBlocking);
        }

        [Fact]
        public async Task SubmitRequirement_ValidDocument_Succeeds()
        {
            // Arrange
            var endorsementId = await CreateTestEndorsement();
            _createdEndorsements.Add(endorsementId);

            var requirement = new EndorsementRequirementSubmission
            {
                RequirementId = "REQ_TEST_001",
                Type = "PROOF_OF_LOSS",
                Content = File.ReadAllBytes("test_files/test_document.pdf"),
                ContentType = "application/pdf",
                FileName = "test_document.pdf",
                SubmittedBy = "Integration Test"
            };

            // Act
            var result = await _service.SubmitRequirement(
                endorsementId, 
                requirement);

            // Assert
            Assert.True(result);
            var requirements = await _service.GetPendingRequirements(endorsementId);
            Assert.DoesNotContain(requirements, 
                r => r.RequirementId == requirement.RequirementId);
        }

        private async Task<string> CreateTestPolicy()
        {
            // Implementation to create a test policy
            // This would typically involve creating a submission first
            throw new NotImplementedException();
        }

        private async Task<string> CreateTestEndorsement()
        {
            var policyNumber = await CreateTestPolicy();
            var response = await _service.CreateEndorsement(
                new EndorsementRequest
                {
                    PolicyNumber = policyNumber,
                    EndorsementType = "ADDRESS_CHANGE",
                    EffectiveDate = DateTime.Today.AddDays(-1), // Invalid date for testing
                    Changes = new Dictionary<string, object>
                    {
                        { "MailingAddress.Street", "123 Test Street" }
                    },
                    RequestedBy = "Integration Test"
                });

            return response.EndorsementId;
        }
    }
} 