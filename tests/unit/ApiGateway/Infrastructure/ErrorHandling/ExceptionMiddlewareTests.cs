using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Http;
using Moq;
using Xunit;
using System;
using System.Threading.Tasks;

public class ExceptionMiddlewareTests
{
    private readonly Mock<ILogger<ExceptionMiddleware>> _loggerMock;
    private readonly Mock<IHostEnvironment> _envMock;
    private readonly ExceptionMiddleware _middleware;

    public ExceptionMiddlewareTests()
    {
        _loggerMock = new Mock<ILogger<ExceptionMiddleware>>();
        _envMock = new Mock<IHostEnvironment>();
        _middleware = new ExceptionMiddleware(
            next: (context) => throw new Exception("Test exception"),
            _loggerMock.Object,
            _envMock.Object
        );
    }

    [Fact]
    public async Task HandleException_IMSException_Returns400()
    {
        // Arrange
        var context = new DefaultHttpContext();
        _middleware = new ExceptionMiddleware(
            next: (context) => throw new IMSException("TEST_ERROR", "Business error"),
            _loggerMock.Object,
            _envMock.Object
        );

        // Act
        await _middleware.InvokeAsync(context);

        // Assert
        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);
    }

    [Fact]
    public async Task HandleException_TokenException_Returns401()
    {
        // Arrange
        var context = new DefaultHttpContext();
        _middleware = new ExceptionMiddleware(
            next: (context) => throw new TokenException("Token error", null),
            _loggerMock.Object,
            _envMock.Object
        );

        // Act
        await _middleware.InvokeAsync(context);

        // Assert
        Assert.Equal(StatusCodes.Status401Unauthorized, context.Response.StatusCode);
    }
} 