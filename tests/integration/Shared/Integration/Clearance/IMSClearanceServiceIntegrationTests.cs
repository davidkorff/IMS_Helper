using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using IMSClearanceService;
using IMSClearanceService.Models;
using IMSClearanceService.Integration;

namespace IMSClearanceService.Integration
{
    [Collection("IMS Integration Tests")]
    public class IMSClearanceServiceIntegrationTests : IAsyncLifetime
    {
        private readonly IMSClearanceService _service;
        private readonly IMSClient _imsClient;
        private readonly ILogger<IMSClearanceService> _logger;
        private readonly IMSTestConfiguration _config;
        private readonly IMSTestCleanup _cleanup;
        private readonly List<string> _createdClearances;

        public IMSClearanceServiceIntegrationTests()
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
            }).CreateLogger<IMSClearanceService>();

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

            _service = new IMSClearanceService(
                _imsClient,
                _logger,
                cache,
                Options.Create(new IMSClearanceSettings()));

            _cleanup = new IMSTestCleanup(_imsClient, _logger);
            _createdClearances = new List<string>();
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
            foreach (var clearanceId in _createdClearances)
            {
                try
                {
                    await _cleanup.CleanupClearance(clearanceId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, 
                        "Failed to cleanup test clearance {ClearanceId}", 
                        clearanceId);
                }
            }

            await _cleanup.DisposeAsync();
        }

        [Fact]
        public async Task CheckClearance_ValidRequest_Success()
        {
            // Arrange
            var request = new ClearanceRequest
            {
                EntityType = "Policy",
                EntityId = await CreateTestPolicy(),
                LineOfBusiness = "Commercial",
                State = "CA",
                EffectiveDate = DateTime.Today,
                Amount = 5000,
                RequestedBy = "Integration Test"
            };

            // Act
            var response = await _service.CheckClearance(request);
            _createdClearances.Add(response.ClearanceId);

            // Assert
            Assert.NotNull(response);
            Assert.NotNull(response.ClearanceId);
            Assert.Equal(request.EntityId, response.EntityId);
        }

        [Fact]
        public async Task UpdateAndGetStatus_Success()
        {
            // Arrange
            var clearanceId = await CreateTestClearance();
            _createdClearances.Add(clearanceId);

            var updateRequest = new ClearanceUpdateRequest
            {
                Overrides = new List<BlockerOverride>
                {
                    new BlockerOverride
                    {
                        BlockerId = "BLK001",
                        Reason = "Test override",
                        ApprovedBy = "Integration Test",
                        ApprovalDate = DateTime.Today
                    }
                },
                Comments = "Test update",
                UpdatedBy = "Integration Test"
            };

            // Act
            var updateResponse = await _service.UpdateClearance(clearanceId, updateRequest);
            var statusResponse = await _service.GetClearanceStatus(clearanceId);

            // Assert
            Assert.NotNull(updateResponse);
            Assert.Equal(clearanceId, updateResponse.ClearanceId);
            Assert.Equal(updateResponse.Status, statusResponse.Status);
        }

        private async Task<string> CreateTestPolicy()
        {
            // Implementation to create a test policy
            // This would typically involve creating a submission first
            throw new NotImplementedException();
        }

        private async Task<string> CreateTestClearance()
        {
            var policyNumber = await CreateTestPolicy();
            var response = await _service.CheckClearance(
                new ClearanceRequest
                {
                    EntityType = "Policy",
                    EntityId = policyNumber,
                    LineOfBusiness = "Commercial",
                    State = "CA",
                    EffectiveDate = DateTime.Today,
                    Amount = 5000,
                    RequestedBy = "Integration Test"
                });

            return response.ClearanceId;
        }
    }
} 