using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Text;
using System.Text.RegularExpressions;

public class LoggingMiddlewareIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly Mock<ILogger<LoggingMiddleware>> _loggerMock;
    private readonly List<LogMessage> _logMessages;

    public LoggingMiddlewareIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _loggerMock = new Mock<ILogger<LoggingMiddleware>>();
        _logMessages = new List<LogMessage>();

        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.AddSingleton(_loggerMock.Object);
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
    public async Task CompleteRequestLifecycle_LogsAllStages()
    {
        // Arrange
        var client = _factory.CreateClient();
        var content = new StringContent(
            "{\"data\":\"test\"}", 
            Encoding.UTF8, 
            "application/json");

        // Act
        var response = await client.PostAsync("/api/test/log", content);

        // Assert
        Assert.True(response.IsSuccessStatusCode);
        
        var requestLog = _logMessages.First(m => 
            m.Message.Contains("HTTP Request"));
        Assert.Contains("POST", requestLog.Message);
        Assert.Contains("/api/test/log", requestLog.Message);
        Assert.Contains("test", requestLog.Message);

        var responseLog = _logMessages.First(m => 
            m.Message.Contains("HTTP Response"));
        Assert.Contains("200", responseLog.Message);
        Assert.Contains("Success", responseLog.Message);
    }

    [Fact]
    public async Task SlowRequest_LogsWarning()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/api/test/slow");

        // Assert
        Assert.True(response.IsSuccessStatusCode);
        Assert.Contains(_logMessages, m => 
            m.Level == LogLevel.Warning && 
            m.Message.Contains("Slow request detected"));
    }

    [Fact]
    public async Task SensitiveData_IsMasked()
    {
        // Arrange
        var client = _factory.CreateClient();
        var content = new StringContent(
            "{\"password\":\"secret123\",\"creditCard\":\"4111111111111111\"}", 
            Encoding.UTF8, 
            "application/json");

        // Act
        var response = await client.PostAsync("/api/test/sensitive", content);

        // Assert
        Assert.True(response.IsSuccessStatusCode);
        var requestLog = _logMessages.First(m => 
            m.Message.Contains("HTTP Request"));
        Assert.Contains("\"password\":\"***\"", requestLog.Message);
        Assert.Contains("\"creditCard\":\"***\"", requestLog.Message);
    }

    [Fact]
    public async Task ConcurrentRequests_LogsCorrectly()
    {
        // Arrange
        var client = _factory.CreateClient();
        var tasks = new List<Task<HttpResponseMessage>>();
        var requestCount = 10;

        // Act
        for (int i = 0; i < requestCount; i++)
        {
            tasks.Add(client.GetAsync($"/api/test/log?id={i}"));
        }
        await Task.WhenAll(tasks);

        // Assert
        var requestLogs = _logMessages.Where(m => 
            m.Message.Contains("HTTP Request")).ToList();
        var responseLogs = _logMessages.Where(m => 
            m.Message.Contains("HTTP Response")).ToList();

        Assert.Equal(requestCount, requestLogs.Count);
        Assert.Equal(requestCount, responseLogs.Count);

        // Verify trace IDs match for each request/response pair
        foreach (var requestLog in requestLogs)
        {
            var traceId = ExtractTraceId(requestLog.Message);
            Assert.Contains(responseLogs, r => 
                ExtractTraceId(r.Message) == traceId);
        }
    }

    private string ExtractTraceId(string logMessage)
    {
        var match = Regex.Match(logMessage, "TraceId\":\"([^\"]+)\"");
        return match.Success ? match.Groups[1].Value : null;
    }
}

[ApiController]
[Route("api/test")]
public class TestLoggingController : ControllerBase
{
    [HttpPost("log")]
    public IActionResult TestLog([FromBody] object data)
    {
        return Ok(new { message = "Success" });
    }

    [HttpGet("slow")]
    public async Task<IActionResult> TestSlow()
    {
        await Task.Delay(1000);
        return Ok();
    }

    [HttpPost("sensitive")]
    public IActionResult TestSensitive([FromBody] object data)
    {
        return Ok();
    }
}

public class TestLoggerProvider : ILoggerProvider
{
    private readonly List<LogMessage> _logMessages;

    public TestLoggerProvider(List<LogMessage> logMessages)
    {
        _logMessages = logMessages;
    }

    public ILogger CreateLogger(string categoryName)
    {
        return new TestLogger(_logMessages);
    }

    public void Dispose() { }
}

public class TestLogger : ILogger
{
    private readonly List<LogMessage> _logMessages;

    public TestLogger(List<LogMessage> logMessages)
    {
        _logMessages = logMessages;
    }

    public IDisposable BeginScope<TState>(TState state) => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception exception,
        Func<TState, Exception, string> formatter)
    {
        _logMessages.Add(new LogMessage
        {
            Level = logLevel,
            Message = formatter(state, exception),
            Exception = exception
        });
    }
}

public class LogMessage
{
    public LogLevel Level { get; set; }
    public string Message { get; set; }
    public Exception Exception { get; set; }
} 