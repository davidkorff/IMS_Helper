using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

public class RateLimitingTests
{
    private readonly Mock<IDistributedCache> _cacheMock;
    private readonly Mock<ILogger<RateLimitingService>> _loggerMock;
    private readonly Mock<IRouteConfigurationService> _routeConfigMock;
    private readonly RateLimitingOptions _options;
    private readonly RateLimitingService _service;
    private readonly DefaultHttpContext _context;
    private readonly RequestDelegate _next;

    public RateLimitingTests()
    {
        _cacheMock = new Mock<IDistributedCache>();
        _loggerMock = new Mock<ILogger<RateLimitingService>>();
        _routeConfigMock = new Mock<IRouteConfigurationService>();
        _options = new RateLimitingOptions();
        _service = new RateLimitingService(
            _cacheMock.Object,
            _loggerMock.Object,
            _routeConfigMock.Object,
            Options.Create(_options));
        _context = new DefaultHttpContext();
        _next = _ => Task.CompletedTask;
    }

    [Fact]
    public async Task CheckRateLimit_UnderLimit_Allowed()
    {
        // Arrange
        var clientId = "test-client";
        var endpoint = "/api/test";
        var policy = new RateLimitPolicy
        {
            RequestsPerMinute = 10,
            BurstLimit = 5
        };

        _cacheMock.Setup(x => x.GetAsync(
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync((byte[])null);

        // Act
        var result = await _service.CheckRateLimitAsync(clientId, endpoint, policy);

        // Assert
        Assert.True(result.IsAllowed);
        Assert.Equal(9, result.RemainingRequests);
    }

    [Fact]
    public async Task CheckRateLimit_OverLimit_Blocked()
    {
        // Arrange
        var clientId = "test-client";
        var endpoint = "/api/test";
        var policy = new RateLimitPolicy
        {
            RequestsPerMinute = 10,
            BurstLimit = 5
        };

        var requests = Enumerable.Range(0, 10)
            .Select(i => DateTimeOffset.UtcNow.ToUnixTimeSeconds())
            .ToList();

        _cacheMock.Setup(x => x.GetAsync(
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(SerializeRequests(requests));

        // Act
        var result = await _service.CheckRateLimitAsync(clientId, endpoint, policy);

        // Assert
        Assert.False(result.IsAllowed);
        Assert.Equal(0, result.RemainingRequests);
    }

    [Fact]
    public async Task CheckRateLimit_BurstLimit_Enforced()
    {
        // Arrange
        var clientId = "test-client";
        var endpoint = "/api/test";
        var policy = new RateLimitPolicy
        {
            RequestsPerMinute = 100,
            BurstLimit = 5
        };

        _cacheMock.Setup(x => x.GetAsync(
            It.Is<string>(k => k.EndsWith(":burst")),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(BitConverter.GetBytes(5));

        // Act
        var result = await _service.CheckRateLimitAsync(clientId, endpoint, policy);

        // Assert
        Assert.False(result.IsAllowed);
    }

    [Fact]
    public async Task Middleware_ExcludedPath_SkipsRateLimit()
    {
        // Arrange
        _options.ExcludedPaths = new[] { "/health" };
        var middleware = new RateLimitingMiddleware(
            _next,
            _loggerMock.Object,
            _service,
            Options.Create(_options));

        _context.Request.Path = "/health";

        // Act
        await middleware.InvokeAsync(_context);

        // Assert
        Assert.Equal(200, _context.Response.StatusCode);
        _cacheMock.Verify(
            x => x.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Middleware_AddsHeaders_WhenEnabled()
    {
        // Arrange
        _options.EnableRateLimitingHeaders = true;
        var middleware = new RateLimitingMiddleware(
            _next,
            _loggerMock.Object,
            _service,
            Options.Create(_options));

        _context.Request.Path = "/api/test";
        _routeConfigMock.Setup(x => x.GetRouteForPathAsync(
            It.IsAny<string>(),
            It.IsAny<string>()))
            .ReturnsAsync(new RouteConfig
            {
                RateLimit = new RateLimitPolicy { RequestsPerMinute = 100 }
            });

        // Act
        await middleware.InvokeAsync(_context);

        // Assert
        Assert.True(_context.Response.Headers.ContainsKey("X-RateLimit-Limit"));
        Assert.True(_context.Response.Headers.ContainsKey("X-RateLimit-Remaining"));
    }

    private static byte[] SerializeRequests(List<long> requests)
    {
        var bytes = new byte[requests.Count * sizeof(long)];
        Buffer.BlockCopy(
            requests.Select(r => BitConverter.GetBytes(r)).SelectMany(b => b).ToArray(),
            0,
            bytes,
            0,
            bytes.Length);
        return bytes;
    }
} 