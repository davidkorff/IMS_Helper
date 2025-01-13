using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using IMSUnderwritingEngine;
using IMSUnderwritingEngine.Models;
using IMSUnderwritingEngine.Services;
using IMSUnderwritingEngine.Tests;

namespace IMSUnderwritingEngine.Tests
{
    [Collection("IMS Integration Tests")]
    public class IMSUnderwritingEngineIntegrationTests : IAsyncLifetime
    {
        private readonly IMSUnderwritingEngine _engine;
        private readonly IMSClient _imsClient;
        private readonly ILogger<IMSUnderwritingEngine> _logger;
        private readonly IMSTestConfiguration _config;
        private readonly IMSTestCleanup _cleanup;
        private readonly DefaultRuleExpressionEvaluator _evaluator;
        private string _testQuoteId;

        public IMSUnderwritingEngineIntegrationTests()
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
            }).CreateLogger<IMSUnderwritingEngine>();

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

            _evaluator = new DefaultRuleExpressionEvaluator(
                _logger,
                cache,
                Options.Create(new IMSUnderwritingSettings()));

            _engine = new IMSUnderwritingEngine(
                _imsClient,
                _logger,
                cache,
                Options.Create(new IMSUnderwritingSettings()),
                _evaluator);

            _cleanup = new IMSTestCleanup(_imsClient, _logger);
        }

        public async Task InitializeAsync()
        {
            await _imsClient.LoginAsync(
                _config.ProgramCode,
                _config.TestEmail,
                _config.TestPassword);

            _testQuoteId = await CreateTestQuote();
            _cleanup.TrackQuote(_testQuoteId);
        }

        public async Task DisposeAsync()
        {
            await _cleanup.DisposeAsync();
        }

        [Fact]
        public async Task EvaluateRules_StandardFloodQuote_EvaluatesAllRules()
        {
            // Arrange
            var request = new UnderwritingRequest
            {
                QuoteId = _testQuoteId,
                LineOfBusiness = "FLOOD",
                State = "FL",
                RiskData = new Dictionary<string, object>
                {
                    { "COVERAGE_A", 250000 },
                    { "DEDUCTIBLE", 1000 },
                    { "CONSTRUCTION_TYPE", "1" },
                    { "FLOOD_ZONE", "X" },
                    { "NUM_STORIES", 1 },
                    { "YEAR_BUILT", 2000 }
                }
            };

            // Act
            var response = await _engine.EvaluateRules(_testQuoteId, request);

            // Assert
            Assert.NotNull(response);
            Assert.NotEmpty(response.RuleResults);
            Assert.NotNull(response.Decision);
            Assert.Equal(_testQuoteId, response.QuoteId);
        }

        [Fact]
        public async Task GetActiveRules_ForFloodLine_ReturnsValidRules()
        {
            // Act
            var rules = await _engine.GetActiveRules("FLOOD", "FL");

            // Assert
            Assert.NotNull(rules);
            Assert.NotEmpty(rules);
            Assert.All(rules, rule =>
            {
                Assert.NotNull(rule.RuleId);
                Assert.NotNull(rule.Expression);
                Assert.True(rule.IsActive);
            });
        }

        [Fact]
        public async Task RequestRuleOverride_ValidRequest_ProcessesOverride()
        {
            // Arrange
            var rules = await _engine.GetActiveRules("FLOOD", "FL");
            var overridableRule = rules.First(r => r.AllowOverride);

            var request = new RuleOverrideRequest
            {
                QuoteId = _testQuoteId,
                RuleId = overridableRule.RuleId,
                Reason = "Integration test override",
                UnderwriterGuid = _config.TestUnderwriterGuid
            };

            // Act
            var response = await _engine.RequestRuleOverride(_testQuoteId, request);

            // Assert
            Assert.NotNull(response);
            Assert.Equal(_testQuoteId, response.QuoteId);
            Assert.Contains(response.RuleResults, 
                r => r.RuleId == overridableRule.RuleId && r.Passed);
        }

        private async Task<string> CreateTestQuote()
        {
            // Implementation to create a test quote
            // This would typically involve creating a submission first
            throw new NotImplementedException();
        }
    }
} 