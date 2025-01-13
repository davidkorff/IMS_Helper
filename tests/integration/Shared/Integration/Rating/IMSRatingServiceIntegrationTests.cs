using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;

namespace Shared.Integration.Rating
{
    [Collection("IMS Integration Tests")]
    public class IMSRatingServiceIntegrationTests : IAsyncLifetime
    {
        private readonly IMSRatingService _service;
        private readonly IMSClient _imsClient;
        private readonly ILogger<IMSRatingService> _logger;
        private readonly IMSTestConfiguration _config;
        private readonly IMSTestCleanup _cleanup;
        private string _testQuoteId;

        public IMSRatingServiceIntegrationTests()
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
            }).CreateLogger<IMSRatingService>();

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

            _service = new IMSRatingService(
                _imsClient,
                _logger,
                cache,
                Options.Create(new IMSRatingSettings
                {
                    DefaultRatingOption = "STD_FLOOD",
                    EnableCaching = true,
                    CacheExpirationMinutes = 60
                }));

            _cleanup = new IMSTestCleanup(_imsClient, _logger);
        }

        public async Task InitializeAsync()
        {
            await _imsClient.LoginAsync(
                _config.ProgramCode,
                _config.TestEmail,
                _config.TestPassword);

            // Create a test quote to use in rating tests
            _testQuoteId = await CreateTestQuote();
            _cleanup.TrackQuote(_testQuoteId);
        }

        public async Task DisposeAsync()
        {
            await _cleanup.DisposeAsync();
        }

        [Fact]
        public async Task RateQuote_StandardFlood_CalculatesCorrectPremium()
        {
            // Arrange
            var request = new RatingRequest
            {
                QuoteId = _testQuoteId,
                RatingOptionId = "STD_FLOOD",
                RatingData = new Dictionary<string, object>
                {
                    { "COVERAGE_A", 250000 },
                    { "DEDUCTIBLE", 1000 },
                    { "TERRITORY", "01" },
                    { "CONSTRUCTION", "1" },
                    { "NUM_STORIES", 1 },
                    { "FLOOD_ZONE", "X" }
                },
                EffectiveDate = DateTime.Today.AddDays(30)
            };

            // Act
            var response = await _service.RateQuote(_testQuoteId, request);

            // Assert
            Assert.NotNull(response);
            Assert.Equal(_testQuoteId, response.QuoteId);
            Assert.NotNull(response.RatingId);
            Assert.True(response.BasePremium > 0);
            Assert.NotEmpty(response.Modifications);
            Assert.NotEmpty(response.Fees);
            Assert.True(response.TotalPremium > response.BasePremium);
        }

        [Fact]
        public async Task GetRatingOptions_ForFloodLine_ReturnsValidOptions()
        {
            // Act
            var options = await _service.GetRatingOptions("FLOOD", "FL");

            // Assert
            Assert.NotNull(options);
            Assert.NotEmpty(options);
            Assert.Contains(options, o => o.Id == "STD_FLOOD");
            Assert.All(options, o =>
            {
                Assert.NotNull(o.Name);
                Assert.NotNull(o.Version);
                Assert.Contains("FL", o.SupportedStates);
            });
        }

        [Fact]
        public async Task ValidateRatingData_WithInvalidValues_ReturnsValidationErrors()
        {
            // Arrange
            var request = new RatingRequest
            {
                RatingOptionId = "STD_FLOOD",
                RatingData = new Dictionary<string, object>
                {
                    { "COVERAGE_A", 10000000 }, // Exceeds maximum
                    { "DEDUCTIBLE", 750 }, // Invalid value
                    { "FLOOD_ZONE", "INVALID" } // Invalid zone
                }
            };

            // Act
            var validation = await _service.ValidateRatingData(request);

            // Assert
            Assert.False(validation.IsValid);
            Assert.NotEmpty(validation.Errors);
            Assert.Contains(validation.Errors, e => e.FactorCode == "COVERAGE_A");
            Assert.Contains(validation.Errors, e => e.FactorCode == "DEDUCTIBLE");
            Assert.Contains(validation.Errors, e => e.FactorCode == "FLOOD_ZONE");
        }

        [Fact]
        public async Task GenerateRatingWorksheet_ForRatedQuote_ReturnsWorksheet()
        {
            // Arrange
            var ratingId = await RateTestQuote();

            // Act
            var worksheet = await _service.GenerateRatingWorksheet(_testQuoteId, ratingId);

            // Assert
            Assert.NotNull(worksheet);
            Assert.True(worksheet.Length > 0);
        }

        private async Task<string> CreateTestQuote()
        {
            // Implementation to create a test quote using IMS client
            // This would typically involve creating a submission first
            throw new NotImplementedException();
        }

        private async Task<string> RateTestQuote()
        {
            var request = new RatingRequest
            {
                QuoteId = _testQuoteId,
                RatingOptionId = "STD_FLOOD",
                RatingData = new Dictionary<string, object>
                {
                    { "COVERAGE_A", 250000 },
                    { "DEDUCTIBLE", 1000 },
                    { "TERRITORY", "01" },
                    { "CONSTRUCTION", "1" }
                },
                EffectiveDate = DateTime.Today.AddDays(30)
            };

            var response = await _service.RateQuote(_testQuoteId, request);
            return response.RatingId;
        }
    }
} 