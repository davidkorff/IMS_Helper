using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Caching.Memory;
using Xunit;
using IMS.Shared.Integration.Rating;
using IMS.Shared.Integration.Client;
using IMS.Shared.Integration.Configuration;
using IMS.Shared.Integration.Cleanup;

namespace IMS.Shared.Integration.Tests
{
    [Collection("IMS Integration Tests")]
    public class IMSRatingEngineIntegrationTests : IAsyncLifetime
    {
        private readonly IMSRatingEngine _engine;
        private readonly IMSClient _imsClient;
        private readonly ILogger<IMSRatingEngine> _logger;
        private readonly IMSTestConfiguration _config;
        private readonly IMSTestCleanup _cleanup;
        private readonly List<string> _createdQuotes;

        public IMSRatingEngineIntegrationTests()
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
            }).CreateLogger<IMSRatingEngine>();

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

            _engine = new IMSRatingEngine(
                _imsClient,
                _logger,
                cache,
                Options.Create(new IMSRatingSettings()));

            _cleanup = new IMSTestCleanup(_imsClient, _logger);
            _createdQuotes = new List<string>();
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
            foreach (var quoteId in _createdQuotes)
            {
                try
                {
                    await _cleanup.CleanupQuote(quoteId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, 
                        "Failed to cleanup test quote {QuoteId}", quoteId);
                }
            }

            await _cleanup.DisposeAsync();
        }

        [Fact]
        public async Task RatePolicy_ValidData_GeneratesRating()
        {
            // Arrange
            var quoteId = await CreateTestQuote();
            _createdQuotes.Add(quoteId);

            var request = new RatingRequest
            {
                QuoteId = quoteId,
                LineOfBusiness = "FLOOD",
                State = "FL",
                EffectiveDate = DateTime.Today.AddDays(30),
                RatingData = new Dictionary<string, object>
                {
                    { "Coverage", 250000 },
                    { "Deductible", 1000 },
                    { "Construction", "Frame" },
                    { "Territory", "01" }
                }
            };

            // Act
            var response = await _engine.RatePolicy(request);

            // Assert
            Assert.NotNull(response);
            Assert.Equal(quoteId, response.QuoteId);
            Assert.True(response.BasePremium > 0);
            Assert.True(response.TotalPremium >= response.BasePremium);
            Assert.NotNull(response.PremiumComponents);
            Assert.NotEmpty(response.PremiumComponents);
        }

        [Fact]
        public async Task GetRatingFactors_ForFloodLine_ReturnsValidFactors()
        {
            // Arrange
            var lineOfBusiness = "FLOOD";
            var state = "FL";

            // Act
            var factors = await _engine.GetRatingFactors(lineOfBusiness, state);

            // Assert
            Assert.NotNull(factors);
            Assert.NotEmpty(factors);
            Assert.Contains(factors, f => f.Category == "Property");
            Assert.Contains(factors, f => f.Category == "Territory");
            Assert.All(factors, factor =>
            {
                Assert.NotNull(factor.Values);
                Assert.NotEmpty(factor.Values);
                Assert.All(factor.Values, v => Assert.True(v.Value > 0));
            });
        }

        [Fact]
        public async Task CalculatePremium_WithDiscounts_AppliesDiscountsCorrectly()
        {
            // Arrange
            var quoteId = await CreateTestQuote();
            _createdQuotes.Add(quoteId);

            var request = new PremiumCalculationRequest
            {
                RatingData = new Dictionary<string, object>
                {
                    { "Coverage", 250000 },
                    { "Deductible", 1000 }
                },
                RequestedDiscounts = new List<string> { "SAFETY_FEATURES" },
                IncludeWorksheet = true
            };

            // Act
            var premium = await _engine.CalculatePremium(quoteId, request);
            var worksheet = await _engine.GetRatingWorksheet(quoteId);

            // Assert
            Assert.True(premium > 0);
            Assert.NotNull(worksheet);
            Assert.Contains(worksheet.AppliedFactors, 
                f => f.Category == "Discount" && f.Value < 1.0m);
        }

        [Fact]
        public async Task ValidateRatingData_WithInvalidData_ReturnsValidationErrors()
        {
            // Arrange
            var quoteId = await CreateTestQuote();
            _createdQuotes.Add(quoteId);

            var invalidData = new Dictionary<string, object>
            {
                { "Coverage", -1000 },
                { "Deductible", 0 },
                { "Construction", "InvalidType" }
            };

            // Act
            var result = await _engine.ValidateRatingData(quoteId, invalidData);

            // Assert
            Assert.False(result.IsValid);
            Assert.NotEmpty(result.Errors);
            Assert.Contains(result.Errors, 
                e => e.Field == "Coverage" && e.ErrorCode == "INVALID_AMOUNT");
            Assert.Contains(result.Errors, 
                e => e.Field == "Construction" && e.ErrorCode == "INVALID_VALUE");
        }

        [Fact]
        public async Task CompareRates_BetweenQuotes_ShowsPremiumDifferences()
        {
            // Arrange
            var baseQuoteId = await CreateTestQuote();
            var comparisonQuoteId = await CreateTestQuote();
            _createdQuotes.Add(baseQuoteId);
            _createdQuotes.Add(comparisonQuoteId);

            // Rate both quotes with different coverages
            await _engine.RatePolicy(new RatingRequest
            {
                QuoteId = baseQuoteId,
                LineOfBusiness = "FLOOD",
                State = "FL",
                EffectiveDate = DateTime.Today.AddDays(30),
                RatingData = new Dictionary<string, object>
                {
                    { "Coverage", 250000 },
                    { "Deductible", 1000 }
                }
            });

            await _engine.RatePolicy(new RatingRequest
            {
                QuoteId = comparisonQuoteId,
                LineOfBusiness = "FLOOD",
                State = "FL",
                EffectiveDate = DateTime.Today.AddDays(30),
                RatingData = new Dictionary<string, object>
                {
                    { "Coverage", 300000 },
                    { "Deductible", 1000 }
                }
            });

            // Act
            var comparison = await _engine.CompareRates(baseQuoteId, comparisonQuoteId);

            // Assert
            Assert.NotNull(comparison);
            Assert.True(comparison.PremiumDifference != 0);
            Assert.NotEmpty(comparison.ComponentComparisons);
            Assert.Contains(comparison.ComponentComparisons, 
                c => c.ComponentId == "BASE" && c.Difference != 0);
        }

        private async Task<string> CreateTestQuote()
        {
            // Implementation to create a test quote
            // This would typically involve creating a submission first
            throw new NotImplementedException();
        }
    }
} 