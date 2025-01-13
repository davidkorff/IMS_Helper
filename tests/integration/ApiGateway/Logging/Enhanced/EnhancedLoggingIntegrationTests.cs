using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace ApiGateway.Logging.Enhanced
{
    public class EnhancedLoggingIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
    {
        private readonly WebApplicationFactory<Program> _factory;
        private readonly List<LogMessage> _logMessages;

        public EnhancedLoggingIntegrationTests(WebApplicationFactory<Program> factory)
        {
            _logMessages = new List<LogMessage>();
            _factory = factory.WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    services.AddControllers()
                        .AddApplicationPart(typeof(TestLoggingController).Assembly);
                });

                builder.ConfigureLogging(logging =>
                {
                    logging.ClearProviders();
                    logging.AddProvider(new TestLoggerProvider(_logMessages));
                });
            });
        }

        [Fact]
        public async Task RequestChain_MaintainsCorrelationId()
        {
            // Arrange
            var client = _factory.CreateClient();
            var correlationId = Guid.NewGuid().ToString();
            client.DefaultRequestHeaders.Add("X-Correlation-ID", correlationId);

            // Act
            var response = await client.GetAsync("/api/test/chain");

            // Assert
            Assert.True(response.IsSuccessStatusCode);
            Assert.All(_logMessages, message =>
            {
                var logData = JsonSerializer.Deserialize<Dictionary<string, object>>(message.Message);
                Assert.Equal(correlationId, logData["correlationId"].ToString());
            });
        }

        [Fact]
        public async Task StructuredLogging_IncludesMetadata()
        {
            // Arrange
            var client = _factory.CreateClient();

            // Act
            var response = await client.GetAsync("/api/test/structured");

            // Assert
            Assert.True(response.IsSuccessStatusCode);
            var logMessage = _logMessages.First(m => m.Message.Contains("structured_test"));
            var logData = JsonSerializer.Deserialize<Dictionary<string, object>>(logMessage.Message);

            Assert.NotNull(logData["timestamp"]);
            Assert.NotNull(logData["level"]);
            Assert.NotNull(logData["requestId"]);
            Assert.NotNull(logData["properties"]);
        }

        [Fact]
        public async Task ErrorLogging_IncludesEnrichedData()
        {
            // Arrange
            var client = _factory.CreateClient();

            // Act
            var response = await client.GetAsync("/api/test/error");

            // Assert
            Assert.False(response.IsSuccessStatusCode);
            var errorLog = _logMessages.First(m => m.Level == LogLevel.Error);
            var logData = JsonSerializer.Deserialize<Dictionary<string, object>>(errorLog.Message);

            Assert.NotNull(logData["exception"]);
            Assert.NotNull(logData["stackTrace"]);
            Assert.NotNull(logData["correlationId"]);
            Assert.NotNull(logData["requestPath"]);
        }

        [Fact]
        public async Task PerformanceLogging_TracksMetrics()
        {
            // Arrange
            var client = _factory.CreateClient();

            // Act
            var response = await client.GetAsync("/api/test/performance");

            // Assert
            Assert.True(response.IsSuccessStatusCode);
            var perfLog = _logMessages.First(m => m.Message.Contains("performance_metric"));
            var logData = JsonSerializer.Deserialize<Dictionary<string, object>>(perfLog.Message);

            Assert.NotNull(logData["duration"]);
            Assert.NotNull(logData["endpoint"]);
            Assert.NotNull(logData["timestamp"]);
        }
    }

    [ApiController]
    [Route("api/test")]
    public class TestLoggingController : ControllerBase
    {
        private readonly ILogger<TestLoggingController> _logger;
        private readonly ILogCorrelation _correlation;

        public TestLoggingController(
            ILogger<TestLoggingController> logger,
            ILogCorrelation correlation)
        {
            _logger = logger;
            _correlation = correlation;
        }

        [HttpGet("chain")]
        public IActionResult TestChain()
        {
            _logger.LogInformation("Chain test");
            return Ok(new { correlationId = _correlation.CorrelationId });
        }

        [HttpGet("structured")]
        public IActionResult TestStructured()
        {
            _logger
                .ForContext("test_id", 123)
                .ForContext("test_name", "structured_test")
                .LogInformation("Structured test");
            return Ok();
        }

        [HttpGet("error")]
        public IActionResult TestError()
        {
            throw new Exception("Test error");
        }

        [HttpGet("performance")]
        public async Task<IActionResult> TestPerformance()
        {
            var sw = Stopwatch.StartNew();
            await Task.Delay(100);
            sw.Stop();

            _logger.LogInformation(
                "Performance test completed in {Duration}ms",
                sw.ElapsedMilliseconds);
            return Ok();
        }
    }
} 