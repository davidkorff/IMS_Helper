using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

public class EnhancedLoggingTests
{
    private readonly Mock<IHttpContextAccessor> _httpContextAccessor;
    private readonly Mock<ILogger<TestController>> _loggerMock;
    private readonly DefaultHttpContext _httpContext;
    private readonly LogEnricher _logEnricher;
    private readonly LogCorrelation _logCorrelation;

    public EnhancedLoggingTests()
    {
        _httpContextAccessor = new Mock<IHttpContextAccessor>();
        _loggerMock = new Mock<ILogger<TestController>>();
        _httpContext = new DefaultHttpContext();
        _httpContextAccessor.Setup(x => x.HttpContext).Returns(_httpContext);
        
        _logEnricher = new LogEnricher(_httpContextAccessor.Object);
        _logCorrelation = new LogCorrelation(_httpContextAccessor.Object);
    }

    [Fact]
    public void LogEnricher_AddsRequestContext()
    {
        // Arrange
        _httpContext.Request.Method = "POST";
        _httpContext.Request.Path = "/api/test";
        _httpContext.Request.Headers["User-Agent"] = "TestAgent";
        _httpContext.Connection.RemoteIpAddress = IPAddress.Parse("127.0.0.1");

        var logEvent = new LogEvent();

        // Act
        _logEnricher.EnrichLog(_loggerMock.Object, logEvent);

        // Assert
        Assert.Equal("POST", logEvent.Properties["RequestMethod"]);
        Assert.Equal("/api/test", logEvent.Properties["RequestPath"]);
        Assert.Equal("TestAgent", logEvent.Properties["UserAgent"]);
        Assert.Equal("127.0.0.1", logEvent.Properties["ClientIP"]);
    }

    [Fact]
    public void LogEnricher_AddsUserContext()
    {
        // Arrange
        var claims = new List<Claim>
        {
            new Claim("sub", "user123"),
            new Claim(ClaimTypes.Name, "TestUser")
        };
        _httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"));

        var logEvent = new LogEvent();

        // Act
        _logEnricher.EnrichLog(_loggerMock.Object, logEvent);

        // Assert
        Assert.Equal("user123", logEvent.Properties["UserId"]);
        Assert.Equal("TestUser", logEvent.Properties["UserName"]);
    }

    [Fact]
    public void StructuredLogging_AddsCustomProperties()
    {
        // Arrange
        var logger = _loggerMock.Object.ForContext("CustomKey", "CustomValue");
        var logMessages = new List<string>();

        _loggerMock
            .Setup(x => x.Log(
                It.IsAny<LogLevel>(),
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception, string>>()))
            .Callback<LogLevel, EventId, object, Exception, Delegate>(
                (level, id, state, ex, formatter) =>
                {
                    logMessages.Add(state.ToString());
                });

        // Act
        logger.LogInformation("Test message");

        // Assert
        Assert.Contains(logMessages, m => m.Contains("CustomKey") && m.Contains("CustomValue"));
    }

    [Fact]
    public void LogCorrelation_MaintainsCorrelationId()
    {
        // Arrange
        const string correlationId = "test-correlation-id";
        _httpContext.Request.Headers["X-Correlation-ID"] = correlationId;

        // Act
        var resultId = _logCorrelation.CorrelationId;

        // Assert
        Assert.Equal(correlationId, resultId);
    }

    [Fact]
    public void LogCorrelation_CreateNewIdWhenMissing()
    {
        // Act
        var correlationId = _logCorrelation.CorrelationId;

        // Assert
        Assert.NotNull(correlationId);
        Assert.True(Guid.TryParse(correlationId, out _));
    }

    [Fact]
    public void LogCorrelation_HandlesBaggageItems()
    {
        // Arrange
        var testValue = new { id = 1, name = "test" };

        // Act
        _logCorrelation.AddBaggageItem("testKey", testValue);
        var retrievedValue = _logCorrelation.GetBaggageItem<object>("testKey");

        // Assert
        Assert.NotNull(retrievedValue);
        Assert.Equal(testValue, retrievedValue);
    }

    private class LogEvent
    {
        public Dictionary<string, object> Properties { get; } = new();

        public void AddProperty(string key, object value)
        {
            Properties[key] = value;
        }
    }

    private class TestController { }
} 