using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Http;
using System;
using System.Threading.Tasks;
using Xunit;
using Moq;
using System.IO;
using System.Text;
using System.Text.Json;

public class ErrorHandlingMiddlewareTests
{
    private readonly Mock<ILogger<ErrorHandlingMiddleware>> _loggerMock;
    private readonly Mock<IHostEnvironment> _environmentMock;
    private HttpContext _context;
    private readonly ErrorHandlingMiddleware _middleware;
    private readonly RequestDelegate _next;

    public ErrorHandlingMiddlewareTests()
    {
        _loggerMock = new Mock<ILogger<ErrorHandlingMiddleware>>();
        _environmentMock = new Mock<IHostEnvironment>();
        _next = new RequestDelegate(context => 
            throw new Exception("Test exception"));

        _middleware = new ErrorHandlingMiddleware(
            _next, 
            _loggerMock.Object,
            _environmentMock.Object);

        SetupHttpContext();
    }

    [Fact]
    public async Task InvokeAsync_ValidationException_Returns400()
    {
        // Arrange
        _next = _ => throw new ValidationException("Invalid input");

        // Act
        await _middleware.InvokeAsync(_context);

        // Assert
        Assert.Equal(StatusCodes.Status400BadRequest, _context.Response.StatusCode);
        var response = await GetErrorResponse();
        Assert.Equal("VALIDATION_ERROR", response.Code);
    }

    [Fact]
    public async Task InvokeAsync_TokenValidationException_Returns401()
    {
        // Arrange
        _next = _ => throw new TokenValidationException("Invalid token");

        // Act
        await _middleware.InvokeAsync(_context);

        // Assert
        Assert.Equal(StatusCodes.Status401Unauthorized, _context.Response.StatusCode);
        var response = await GetErrorResponse();
        Assert.Equal("INVALID_TOKEN", response.Code);
    }

    [Fact]
    public async Task InvokeAsync_ForbiddenAccessException_Returns403()
    {
        // Arrange
        _next = _ => throw new ForbiddenAccessException("Access denied");

        // Act
        await _middleware.InvokeAsync(_context);

        // Assert
        Assert.Equal(StatusCodes.Status403Forbidden, _context.Response.StatusCode);
        var response = await GetErrorResponse();
        Assert.Equal("FORBIDDEN", response.Code);
    }

    [Fact]
    public async Task InvokeAsync_NotFoundException_Returns404()
    {
        // Arrange
        _next = _ => throw new NotFoundException("Resource not found");

        // Act
        await _middleware.InvokeAsync(_context);

        // Assert
        Assert.Equal(StatusCodes.Status404NotFound, _context.Response.StatusCode);
        var response = await GetErrorResponse();
        Assert.Equal("NOT_FOUND", response.Code);
    }

    [Fact]
    public async Task InvokeAsync_IMSIntegrationException_Returns502()
    {
        // Arrange
        _next = _ => throw new IMSIntegrationException("IMS error");

        // Act
        await _middleware.InvokeAsync(_context);

        // Assert
        Assert.Equal(StatusCodes.Status502BadGateway, _context.Response.StatusCode);
        var response = await GetErrorResponse();
        Assert.Equal("IMS_ERROR", response.Code);
    }

    [Fact]
    public async Task InvokeAsync_UnhandledException_Returns500()
    {
        // Arrange
        _next = _ => throw new Exception("Unexpected error");

        // Act
        await _middleware.InvokeAsync(_context);

        // Assert
        Assert.Equal(StatusCodes.Status500InternalServerError, _context.Response.StatusCode);
        var response = await GetErrorResponse();
        Assert.Equal("INTERNAL_ERROR", response.Code);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task InvokeAsync_IncludesDetails_BasedOnEnvironment(bool isDevelopment)
    {
        // Arrange
        _environmentMock.Setup(e => e.IsDevelopment()).Returns(isDevelopment);
        _next = _ => throw new Exception("Test exception");

        // Act
        await _middleware.InvokeAsync(_context);

        // Assert
        var response = await GetErrorResponse();
        Assert.Equal(isDevelopment, response.Details != null);
    }

    private void SetupHttpContext()
    {
        _context = new DefaultHttpContext();
        _context.TraceIdentifier = "test-trace-id";
        _context.Response.Body = new MemoryStream();
    }

    private async Task<ErrorResponse> GetErrorResponse()
    {
        _context.Response.Body.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(_context.Response.Body);
        var json = await reader.ReadToEndAsync();
        return JsonSerializer.Deserialize<ErrorResponse>(json);
    }
} 