using Microsoft.AspNetCore.Mvc;
using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Xunit;
using Microsoft.AspNetCore.Mvc.Testing;
using System.Collections.Generic;

public class ErrorHandlingIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public ErrorHandlingIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.AddControllers()
                    .AddApplicationPart(typeof(TestErrorController).Assembly);
            });
        });
        _client = _factory.CreateClient();
    }

    [Fact]
    public async Task ValidationError_ReturnsFormattedResponse()
    {
        // Act
        var response = await _client.GetAsync("/api/test/validation");
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("VALIDATION_ERROR", error.Code);
        Assert.NotNull(error.TraceId);
        Assert.NotEmpty(error.Message);
    }

    [Fact]
    public async Task AuthenticationError_ReturnsFormattedResponse()
    {
        // Act
        var response = await _client.GetAsync("/api/test/unauthorized");
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("INVALID_TOKEN", error.Code);
        Assert.NotNull(error.TraceId);
    }

    [Fact]
    public async Task ForbiddenError_ReturnsFormattedResponse()
    {
        // Act
        var response = await _client.GetAsync("/api/test/forbidden");
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("FORBIDDEN", error.Code);
        Assert.NotNull(error.TraceId);
    }

    [Fact]
    public async Task NotFoundError_ReturnsFormattedResponse()
    {
        // Act
        var response = await _client.GetAsync("/api/test/notfound");
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("NOT_FOUND", error.Code);
        Assert.NotNull(error.TraceId);
    }

    [Fact]
    public async Task IMSError_ReturnsFormattedResponse()
    {
        // Act
        var response = await _client.GetAsync("/api/test/ims-error");
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();

        // Assert
        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
        Assert.Equal("IMS_ERROR", error.Code);
        Assert.NotNull(error.TraceId);
    }

    [Fact]
    public async Task UnexpectedError_ReturnsFormattedResponse()
    {
        // Act
        var response = await _client.GetAsync("/api/test/unexpected");
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();

        // Assert
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("INTERNAL_ERROR", error.Code);
        Assert.NotNull(error.TraceId);
    }

    [Theory]
    [InlineData("Development")]
    [InlineData("Production")]
    public async Task ErrorResponse_IncludesDetails_OnlyInDevelopment(string environment)
    {
        // Arrange
        var client = _factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment(environment);
        }).CreateClient();

        // Act
        var response = await client.GetAsync("/api/test/unexpected");
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();

        // Assert
        if (environment == "Development")
        {
            Assert.NotNull(error.Details);
            Assert.NotNull(error.Details.StackTrace);
            Assert.NotNull(error.Details.ExceptionType);
        }
        else
        {
            Assert.Null(error.Details);
        }
    }

    [Fact]
    public async Task ConcurrentRequests_HandleErrorsProperly()
    {
        // Arrange
        var tasks = new List<Task<HttpResponseMessage>>();
        for (int i = 0; i < 10; i++)
        {
            tasks.Add(_client.GetAsync("/api/test/validation"));
            tasks.Add(_client.GetAsync("/api/test/unauthorized"));
            tasks.Add(_client.GetAsync("/api/test/forbidden"));
            tasks.Add(_client.GetAsync("/api/test/notfound"));
            tasks.Add(_client.GetAsync("/api/test/ims-error"));
        }

        // Act
        var responses = await Task.WhenAll(tasks);

        // Assert
        foreach (var response in responses)
        {
            Assert.True(response.StatusCode >= HttpStatusCode.BadRequest);
            var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
            Assert.NotNull(error.Code);
            Assert.NotNull(error.TraceId);
        }
    }
}

[ApiController]
[Route("api/test")]
public class TestErrorController : ControllerBase
{
    [HttpGet("validation")]
    public IActionResult ThrowValidationError()
    {
        throw new ValidationException("Invalid test input");
    }

    [HttpGet("unauthorized")]
    public IActionResult ThrowUnauthorizedError()
    {
        throw new TokenValidationException("Invalid test token");
    }

    [HttpGet("forbidden")]
    public IActionResult ThrowForbiddenError()
    {
        throw new ForbiddenAccessException("Test access denied");
    }

    [HttpGet("notfound")]
    public IActionResult ThrowNotFoundError()
    {
        throw new NotFoundException("Test resource not found");
    }

    [HttpGet("ims-error")]
    public IActionResult ThrowIMSError()
    {
        throw new IMSIntegrationException("Test IMS error");
    }

    [HttpGet("unexpected")]
    public IActionResult ThrowUnexpectedError()
    {
        throw new Exception("Test unexpected error");
    }
} 