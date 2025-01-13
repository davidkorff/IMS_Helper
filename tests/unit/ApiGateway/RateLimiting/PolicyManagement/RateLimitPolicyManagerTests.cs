using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Moq;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

public class RateLimitPolicyManagerTests
{
    private readonly Mock<IDistributedCache> _cacheMock;
    private readonly Mock<ILogger<RateLimitPolicyManager>> _loggerMock;
    private readonly RateLimitingOptions _options;
    private readonly RateLimitPolicyManager _manager;

    public RateLimitPolicyManagerTests()
    {
        _cacheMock = new Mock<IDistributedCache>();
        _loggerMock = new Mock<ILogger<RateLimitPolicyManager>>();
        _options = new RateLimitingOptions();
        _manager = new RateLimitPolicyManager(
            _cacheMock.Object,
            _loggerMock.Object,
            Options.Create(_options));
    }

    [Fact]
    public async Task AddPolicy_ValidatesAndStores()
    {
        // Arrange
        var policy = new RateLimitPolicyConfig
        {
            PathPattern = "/api/test/*",
            RequestsPerMinute = 100,
            BurstLimit = 10
        };

        _cacheMock.Setup(x => x.GetAsync(
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync((byte[])null);

        // Act
        var isValid = await _manager.ValidatePolicyAsync(policy);
        await _manager.AddOrUpdatePolicyAsync(policy);

        // Assert
        Assert.True(isValid);
        _cacheMock.Verify(x => x.SetAsync(
            It.IsAny<string>(),
            It.IsAny<byte[]>(),
            It.IsAny<DistributedCacheEntryOptions>(),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetPolicy_MatchesPattern()
    {
        // Arrange
        var policies = new List<RateLimitPolicyConfig>
        {
            new() { PathPattern = "/api/test/*", RequestsPerMinute = 100 },
            new() { PathPattern = "/api/users/**", RequestsPerMinute = 50 }
        };

        _cacheMock.Setup(x => x.GetAsync(
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(JsonSerializer.SerializeToUtf8Bytes(policies));

        // Act
        var result = await _manager.GetPolicyAsync("/api/test/123");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("/api/test/*", result.PathPattern);
    }

    [Fact]
    public async Task ValidatePolicy_DetectsConflicts()
    {
        // Arrange
        var existingPolicies = new List<RateLimitPolicyConfig>
        {
            new() { PathPattern = "/api/test/**", RequestsPerMinute = 100 }
        };

        _cacheMock.Setup(x => x.GetAsync(
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(JsonSerializer.SerializeToUtf8Bytes(existingPolicies));

        var conflictingPolicy = new RateLimitPolicyConfig
        {
            PathPattern = "/api/test/users/*",
            RequestsPerMinute = 50
        };

        // Act
        var isValid = await _manager.ValidatePolicyAsync(conflictingPolicy);

        // Assert
        Assert.False(isValid);
    }
} 