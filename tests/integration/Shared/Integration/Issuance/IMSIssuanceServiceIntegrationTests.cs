using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using IMS.Integration.Services;
using IMS.Integration.Models;

namespace IMS.Integration.Tests
{
    [Collection("IMS Integration Tests")]
    public class IMSIssuanceServiceIntegrationTests : IAsyncLifetime
    {
        private readonly IMSIssuanceService _service;
        private readonly IMSClient _imsClient;
        private readonly ILogger<IMSIssuanceService> _logger;
        private readonly IMSTestConfiguration _config;
        private readonly IMSTestCleanup _cleanup;
        private readonly List<string> _createdPolicies;

        public IMSIssuanceServiceIntegrationTests()
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
            }).CreateLogger<IMSIssuanceService>();

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

            _service = new IMSIssuanceService(
                _imsClient,
                _logger,
                cache,
                Options.Create(new IMSIssuanceSettings()));

            _cleanup = new IMSTestCleanup(_imsClient, _logger);
            _createdPolicies = new List<string>();
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
            foreach (var policyNumber in _createdPolicies)
            {
                try
                {
                    await _cleanup.CleanupPolicy(policyNumber);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, 
                        "Failed to cleanup test policy {PolicyNumber}", policyNumber);
                }
            }

            await _cleanup.DisposeAsync();
        }

        [Fact]
        public async Task IssuePolicy_WithValidQuote_CreatesPolicy()
        {
            // Arrange
            var quoteId = await CreateTestQuote();
            var request = new IssuanceRequest
            {
                QuoteId = quoteId,
                EffectiveDate = DateTime.Today.AddDays(30),
                PaymentInfo = new PaymentInfo
                {
                    PaymentMethod = "CreditCard",
                    Amount = 1000.00m,
                    TransactionId = "TEST_TRX_001",
                    TransactionDate = DateTime.Today,
                    BillingInfo = new BillingInfo
                    {
                        BillingType = "Monthly",
                        PaymentPlan = "Standard"
                    }
                }
            };

            // Act
            var response = await _service.IssuePolicy(request);
            _createdPolicies.Add(response.PolicyNumber);

            // Assert
            Assert.NotNull(response);
            Assert.NotNull(response.IssuanceId);
            Assert.NotNull(response.PolicyNumber);
            Assert.Equal(IssuanceStatus.InProgress, response.Status);
        }

        [Fact]
        public async Task GetIssuanceWorkflow_ForFloodLine_ReturnsValidWorkflow()
        {
            // Arrange
            var lineOfBusiness = "FLOOD";
            var state = "FL";

            // Act
            var workflow = await _service.GetIssuanceWorkflow(lineOfBusiness, state);

            // Assert
            Assert.NotNull(workflow);
            Assert.Equal(lineOfBusiness, workflow.LineOfBusiness);
            Assert.Equal(state, workflow.State);
            Assert.NotEmpty(workflow.Steps);
            Assert.Contains(workflow.Steps, 
                s => s.Name == "Payment Verification");
            Assert.Contains(workflow.Steps, 
                s => s.Name == "Document Generation");
        }

        [Fact]
        public async Task ValidateForIssuance_WithMissingPayment_ReturnsErrors()
        {
            // Arrange
            var quoteId = await CreateTestQuote();

            // Act
            var errors = await _service.ValidateForIssuance(quoteId);

            // Assert
            Assert.NotNull(errors);
            Assert.NotEmpty(errors);
            Assert.Contains(errors, 
                e => e.ErrorCode == "PAYMENT_REQUIRED" && e.IsBlocking);
        }

        [Fact]
        public async Task SubmitRequirement_ValidDocument_Succeeds()
        {
            // Arrange
            var quoteId = await CreateTestQuote();
            var issuanceResponse = await _service.IssuePolicy(
                new IssuanceRequest
                {
                    QuoteId = quoteId,
                    EffectiveDate = DateTime.Today.AddDays(30)
                });
            _createdPolicies.Add(issuanceResponse.PolicyNumber);

            var requirement = new IssuanceRequirementSubmission
            {
                RequirementId = "REQ_TEST_001",
                Type = "PROOF_OF_INSURANCE",
                Content = File.ReadAllBytes("test_files/test_document.pdf"),
                ContentType = "application/pdf",
                FileName = "test_document.pdf",
                SubmittedBy = "Integration Test"
            };

            // Act
            var result = await _service.SubmitRequirement(
                issuanceResponse.IssuanceId, 
                requirement);

            // Assert
            Assert.True(result);
            var requirements = await _service.GetPendingRequirements(
                issuanceResponse.IssuanceId);
            Assert.DoesNotContain(requirements, 
                r => r.RequirementId == requirement.RequirementId);
        }

        [Fact]
        public async Task ReissuePolicy_WithValidChanges_CreatesNewPolicy()
        {
            // Arrange
            var originalPolicyNumber = await CreateTestPolicy();
            _createdPolicies.Add(originalPolicyNumber);

            var request = new ReissuanceRequest
            {
                PolicyNumber = originalPolicyNumber,
                ReissuanceReason = "Coverage Change",
                EffectiveDate = DateTime.Today.AddDays(30),
                Changes = new Dictionary<string, object>
                {
                    { "Coverage", 300000 }
                }
            };

            // Act
            var response = await _service.ReissuePolicy(request);
            _createdPolicies.Add(response.PolicyNumber);

            // Assert
            Assert.NotNull(response);
            Assert.NotEqual(originalPolicyNumber, response.PolicyNumber);
            Assert.Equal(IssuanceStatus.InProgress, response.Status);
        }

        private async Task<string> CreateTestQuote()
        {
            // Implementation to create a test quote
            // This would typically involve creating a submission first
            throw new NotImplementedException();
        }

        private async Task<string> CreateTestPolicy()
        {
            var quoteId = await CreateTestQuote();
            var response = await _service.IssuePolicy(
                new IssuanceRequest
                {
                    QuoteId = quoteId,
                    EffectiveDate = DateTime.Today.AddDays(30),
                    PaymentInfo = new PaymentInfo
                    {
                        PaymentMethod = "CreditCard",
                        Amount = 1000.00m,
                        TransactionId = "TEST_TRX_002",
                        TransactionDate = DateTime.Today
                    }
                });

            return response.PolicyNumber;
        }
    }
} 