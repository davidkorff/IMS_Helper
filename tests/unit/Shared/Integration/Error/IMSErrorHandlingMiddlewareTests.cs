using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Http;
using System;
using System.Threading.Tasks;
using Xunit;
using Moq;
using System.Xml.Linq;
using System.Net;
using System.Text.Json;

public class IMSErrorHandlingMiddlewareTests
{
    private readonly Mock<ILogger<IMSErrorHandlingMiddleware>> _loggerMock;

    public IMSErrorHandlingMiddlewareTests()
    {
        _loggerMock = new Mock<ILogger<IMSErrorHandlingMiddleware>>();
    }

    [Fact]
    public async Task InvokeAsync_NoException_CompletesSuccessfully()
    {
        // Arrange
        var middleware = CreateMiddleware(async (context) => 
        {
            context.Response.StatusCode = 200;
            await context.Response.WriteAsync("Success");
        });

        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        Assert.Equal(200, context.Response.StatusCode);
        Assert.Equal("Success", await GetResponseBody(context));
    }

    [Fact]
    public async Task InvokeAsync_SoapException_ReturnsMappedError()
    {
        // Arrange
        var soapFault = new SoapFault
        {
            Message = "Invalid credentials",
            Detail = new XElement("Detail",
                new XElement("ErrorCode", "IMS.Auth.InvalidCredentials"))
        };

        var middleware = CreateMiddleware(_ => 
            throw new SoapException("SOAP Error", soapFault));

        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        Assert.Equal(401, context.Response.StatusCode);
        Assert.Equal("application/json", context.Response.ContentType);

        var response = await GetResponseBody(context);
        var error = JsonSerializer.Deserialize<ErrorResponse>(response, 
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        Assert.Equal("AUTH001", error.ErrorCode);
        Assert.Equal(HttpStatusCode.Unauthorized, error.StatusCode);
        Assert.NotNull(error.TraceId);
    }

    [Fact]
    public async Task InvokeAsync_IMSException_ReturnsCustomError()
    {
        // Arrange
        var imsException = new IMSException(
            "Custom error",
            HttpStatusCode.BadRequest,
            "CUSTOM001",
            new List<ErrorDetail>
            {
                new() { Field = "TestField", Code = "TEST001", Message = "Test error" }
            });

        var middleware = CreateMiddleware(_ => throw imsException);

        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        Assert.Equal(400, context.Response.StatusCode);
        
        var response = await GetResponseBody(context);
        var error = JsonSerializer.Deserialize<ErrorResponse>(response, 
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        Assert.Equal("CUSTOM001", error.ErrorCode);
        Assert.Single(error.Details);
        Assert.Equal("TestField", error.Details[0].Field);
    }

    [Theory]
    [InlineData(true, "Test exception message")]
    [InlineData(false, "An unexpected error occurred")]
    public async Task InvokeAsync_UnhandledException_ReturnsAppropriateError(
        bool includeDetails, 
        string expectedMessage)
    {
        // Arrange
        var exception = new Exception("Test exception message");
        var middleware = CreateMiddleware(_ => throw exception, includeDetails);

        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        Assert.Equal(500, context.Response.StatusCode);
        
        var response = await GetResponseBody(context);
        var error = JsonSerializer.Deserialize<ErrorResponse>(response, 
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        Assert.Equal("SYS003", error.ErrorCode);
        Assert.Equal(expectedMessage, error.Message);
    }

    [Fact]
    public async Task InvokeAsync_Exception_LogsError()
    {
        // Arrange
        var exception = new Exception("Test exception");
        var middleware = CreateMiddleware(_ => throw exception);

        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => true),
                exception,
                It.IsAny<Func<It.IsAnyType, Exception, string>>()!),
            Times.Once);
    }

    private IMSErrorHandlingMiddleware CreateMiddleware(
        RequestDelegate next,
        bool includeDetails = false)
    {
        return new IMSErrorHandlingMiddleware(
            next,
            _loggerMock.Object,
            includeDetails);
    }

    private async Task<string> GetResponseBody(HttpContext context)
    {
        context.Response.Body.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(context.Response.Body);
        return await reader.ReadToEndAsync();
    }
} 