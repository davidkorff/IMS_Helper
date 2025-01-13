using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Http;
using System;
using System.Text;
using System.IO;
using System.Threading.Tasks;
using Moq;
using Xunit;

public class LoggingMiddlewareTests
{
    private readonly Mock<ILogger<LoggingMiddleware>> _loggerMock;
    private readonly LoggingSettings _settings;
    private readonly LoggingMiddleware _middleware;
    private readonly DefaultHttpContext _context;

    public LoggingMiddlewareTests()
    {
        _loggerMock = new Mock<ILogger<LoggingMiddleware>>();
        _settings = new LoggingSettings();
        _middleware = new LoggingMiddleware(
            async (context) => await Task.CompletedTask,
            _loggerMock.Object,
            Options.Create(_settings));
        _context = new DefaultHttpContext();
    }

    [Fact]
    public async Task InvokeAsync_LogsRequestInformation()
    {
        // Arrange
        _context.Request.Method = "POST";
        _context.Request.Path = "/api/test";
        _context.Request.QueryString = new QueryString("?key=value");
        _context.Request.ContentType = "application/json";
        _context.Request.Body = new MemoryStream(
            Encoding.UTF8.GetBytes("{\"test\":\"data\"}"));
        _context.Response.Body = new MemoryStream();

        // Act
        await _middleware.InvokeAsync(_context);

        // Assert
        VerifyLogContains("HTTP Request");
        VerifyLogContains("/api/test");
        VerifyLogContains("POST");
    }

    [Fact]
    public async Task InvokeAsync_LogsResponseInformation()
    {
        // Arrange
        _context.Response.StatusCode = 200;
        _context.Response.ContentType = "application/json";
        _context.Response.Body = new MemoryStream();

        // Act
        await _middleware.InvokeAsync(_context);

        // Assert
        VerifyLogContains("HTTP Response");
        VerifyLogContains("200");
    }

    [Fact]
    public async Task InvokeAsync_LogsSlowRequests()
    {
        // Arrange
        _settings.SlowRequestThresholdMs = 100;
        _context.Response.Body = new MemoryStream();
        var middleware = new LoggingMiddleware(
            async (context) => await Task.Delay(150),
            _loggerMock.Object,
            Options.Create(_settings));

        // Act
        await middleware.InvokeAsync(_context);

        // Assert
        VerifyLogContains("Slow request detected");
    }

    [Fact]
    public async Task InvokeAsync_ExcludesSpecifiedPaths()
    {
        // Arrange
        _settings.ExcludedPaths.Add("/health");
        _context.Request.Path = "/health";
        _context.Response.Body = new MemoryStream();

        // Act
        await _middleware.InvokeAsync(_context);

        // Assert
        VerifyLogNotContains("HTTP Request");
    }

    [Fact]
    public async Task InvokeAsync_MasksSensitiveData()
    {
        // Arrange
        _context.Request.ContentType = "application/json";
        _context.Request.Body = new MemoryStream(
            Encoding.UTF8.GetBytes("{\"password\":\"secret\"}"));
        _context.Response.Body = new MemoryStream();

        // Act
        await _middleware.InvokeAsync(_context);

        // Assert
        VerifyLogContains("\"password\":\"***\"");
    }

    [Fact]
    public async Task InvokeAsync_HandlesExceptions()
    {
        // Arrange
        var exception = new Exception("Test error");
        var middleware = new LoggingMiddleware(
            _ => throw exception,
            _loggerMock.Object,
            Options.Create(_settings));
        _context.Response.Body = new MemoryStream();

        // Act & Assert
        await Assert.ThrowsAsync<Exception>(() => 
            middleware.InvokeAsync(_context));
        VerifyLogContains("Request failed");
        VerifyLogContains("Test error");
    }

    private void VerifyLogContains(string expectedContent)
    {
        _loggerMock.Verify(
            x => x.Log(
                It.IsAny<LogLevel>(),
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => 
                    v.ToString().Contains(expectedContent)),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception, string>>()),
            Times.AtLeast(1));
    }

    private void VerifyLogNotContains(string unexpectedContent)
    {
        _loggerMock.Verify(
            x => x.Log(
                It.IsAny<LogLevel>(),
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => 
                    v.ToString().Contains(unexpectedContent)),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception, string>>()),
            Times.Never);
    }
} 