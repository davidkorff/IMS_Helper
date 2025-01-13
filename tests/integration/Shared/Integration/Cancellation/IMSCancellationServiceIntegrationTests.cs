using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Caching.Memory;
using Xunit;
using IMS.Services;
using IMS.Models;

namespace IMS.Tests.Integration.Shared.Integration.Cancellation
{
    [Collection("IMS Integration Tests")]
    public class IMSCancellationServiceIntegrationTests : IAsyncLifetime
    {
        private readonly IMSCancellationService _service;
        private readonly IMSClient _imsClient;
        private readonly ILogger<IMSCancellationService> _logger;
        private readonly IMSTestConfiguration _config;
        private readonly IMSTestCleanup _cleanup;
        private readonly List<string> _createdCancellations;

        public IMSCancellationServiceIntegrationTests()
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
            }).CreateLogger<IMSCancellationService>();

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

            _service = new IMSCancellationService(
                _imsClient,
                _logger,
                cache,
                Options.Create(new IMSCancellationSettings()));

            _cleanup = new IMSTestCleanup(_imsClient, _logger);
            _createdCancellations = new List<string>();
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
            foreach (var cancellationId in _createdCancellations)
            {
                try
                {
                    await _cleanup.CleanupCancellation(cancellationId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, 
                        "Failed to cleanup test cancellation {CancellationId}", 
                        cancellationId);
                }
            }

            await _cleanup.DisposeAsync();
        }

        [Fact]
        public async Task InitiateCancellation_WithValidPolicy_CreatesCancellation()
        {
            // Arrange
            var policyNumber = await CreateTestPolicy();
            var request = new CancellationRequest
            {
                PolicyNumber = policyNumber,
                CancellationType = "Insured_Request",
                CancellationDate = DateTime.Today.AddDays(30),
                Reason = "Moving to different state",
                RequestedBy = "Integration Test"
            };

            // Act
            var response = await _service.InitiateCancellation(request);
            _createdCancellations.Add(response.CancellationId);

            // Assert
            Assert.NotNull(response);
            Assert.NotNull(response.CancellationId);
            Assert.Equal(request.PolicyNumber, response.PolicyNumber);
            Assert.Equal(CancellationStatus.Draft, response.Status);
        }

        [Fact]
        public async Task CalculateRefund_WithValidPolicy_ReturnsCalculation()
        {
            // Arrange
            var policyNumber = await CreateTestPolicy();
            var request = new CancellationCalculationRequest
            {
                CancellationDate = DateTime.Today.AddDays(30),
                CancellationType = "Insured_Request",
                CalculationMethod = "ProRata"
            };

            // Act
            var calculation = await _service.CalculateRefund(policyNumber, request);

            // Assert
            Assert.NotNull(calculation);
            Assert.True(calculation.RefundAmount >= 0);
            Assert.Equal("ProRata", calculation.CalculationMethod);
            Assert.NotNull(calculation.Breakdown);
        }

        [Fact]
        public async Task RescindCancellation_WithValidCancellation_Succeeds()
        {
            // Arrange
            var cancellationId = await CreateTestCancellation();
            _createdCancellations.Add(cancellationId);

            var request = new RescindRequest
            {
                Reason = "Customer changed mind",
                RequestedBy = "Integration Test"
            };

            // Act
            var response = await _service.RescindCancellation(cancellationId, request);

            // Assert
            Assert.NotNull(response);
            Assert.Equal(CancellationStatus.Rescinded, response.Status);
            var details = await _service.GetCancellationDetails(cancellationId);
            Assert.Equal(CancellationStatus.Rescinded, details.Status);
        }

        private async Task<string> CreateTestPolicy()
        {
            // Implementation to create a test policy
            // This would typically involve creating a submission first
            throw new NotImplementedException();
        }

        private async Task<string> CreateTestCancellation()
        {
            var policyNumber = await CreateTestPolicy();
            var response = await _service.InitiateCancellation(
                new CancellationRequest
                {
                    PolicyNumber = policyNumber,
                    CancellationType = "Insured_Request",
                    CancellationDate = DateTime.Today.AddDays(30),
                    Reason = "Test Cancellation",
                    RequestedBy = "Integration Test"
                });

            return response.CancellationId;
        }
    }
} 