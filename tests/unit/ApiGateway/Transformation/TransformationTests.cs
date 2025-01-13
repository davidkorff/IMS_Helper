using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

public class TransformationTests
{
    private readonly Mock<ITransformationService> _transformationServiceMock;
    private readonly Mock<ILogger<TransformationMiddleware>> _loggerMock;
    private readonly TransformationOptions _options;
    private readonly DefaultHttpContext _context;
    private readonly RequestDelegate _next;

    public TransformationTests()
    {
        _transformationServiceMock = new Mock<ITransformationService>();
        _loggerMock = new Mock<ILogger<TransformationMiddleware>>();
        _options = new TransformationOptions();
        _context = new DefaultHttpContext();
        _next = _ => Task.CompletedTask;
    }

    [Fact]
    public async Task RequestTransformation_AppliedWhenNeeded()
    {
        // Arrange
        var middleware = new TransformationMiddleware(
            _next,
            _loggerMock.Object,
            _transformationServiceMock.Object,
            Options.Create(_options));

        var requestContent = "original request";
        var requestStream = new MemoryStream(Encoding.UTF8.GetBytes(requestContent));
        _context.Request.Body = requestStream;
        _context.Request.ContentType = "application/json";

        _transformationServiceMock
            .Setup(x => x.CanTransformRequest(
                It.IsAny<string>(),
                It.IsAny<string>()))
            .Returns(true);

        _transformationServiceMock
            .Setup(x => x.TransformRequestAsync(
                It.IsAny<Stream>(),
                It.IsAny<string>(),
                It.IsAny<string>()))
            .ReturnsAsync(new MemoryStream(
                Encoding.UTF8.GetBytes("transformed request")));

        // Act
        await middleware.InvokeAsync(_context);

        // Assert
        _transformationServiceMock.Verify(
            x => x.TransformRequestAsync(
                It.IsAny<Stream>(),
                It.IsAny<string>(),
                It.IsAny<string>()),
            Times.Once);
    }

    [Fact]
    public async Task ResponseTransformation_AppliedWhenNeeded()
    {
        // Arrange
        var middleware = new TransformationMiddleware(
            _next,
            _loggerMock.Object,
            _transformationServiceMock.Object,
            Options.Create(_options));

        _context.Response.ContentType = "application/json";
        _context.Response.Body = new MemoryStream();

        _transformationServiceMock
            .Setup(x => x.CanTransformResponse(
                It.IsAny<string>(),
                It.IsAny<string>()))
            .Returns(true);

        _transformationServiceMock
            .Setup(x => x.TransformResponseAsync(
                It.IsAny<Stream>(),
                It.IsAny<string>(),
                It.IsAny<string>()))
            .ReturnsAsync(new MemoryStream(
                Encoding.UTF8.GetBytes("transformed response")));

        // Act
        await middleware.InvokeAsync(_context);

        // Assert
        _transformationServiceMock.Verify(
            x => x.TransformResponseAsync(
                It.IsAny<Stream>(),
                It.IsAny<string>(),
                It.IsAny<string>()),
            Times.Once);
    }

    [Fact]
    public async Task TransformationError_HandledGracefully()
    {
        // Arrange
        var middleware = new TransformationMiddleware(
            _next,
            _loggerMock.Object,
            _transformationServiceMock.Object,
            Options.Create(_options));

        _context.Request.ContentType = "application/json";
        _context.Response.Body = new MemoryStream();

        _transformationServiceMock
            .Setup(x => x.CanTransformRequest(
                It.IsAny<string>(),
                It.IsAny<string>()))
            .Returns(true);

        _transformationServiceMock
            .Setup(x => x.TransformRequestAsync(
                It.IsAny<Stream>(),
                It.IsAny<string>(),
                It.IsAny<string>()))
            .ThrowsAsync(new TransformationException(
                "Test error",
                TransformationType.Request));

        // Act
        await middleware.InvokeAsync(_context);

        // Assert
        Assert.Equal(StatusCodes.Status422UnprocessableEntity, _context.Response.StatusCode);
    }
} 