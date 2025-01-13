using System;
using System.Threading.Tasks;
using Xunit;
using Moq;
using Microsoft.Extensions.Logging;
using ApiGateway.Services.Usage;

public class UsageTrackerTests
{
    private readonly IUsageTracker _usageTracker;
    private readonly Mock<ILogger<UsageTracker>> _loggerMock;

    public UsageTrackerTests()
    {
        _loggerMock = new Mock<ILogger<UsageTracker>>();
        _usageTracker = new UsageTracker(_loggerMock.Object);
    }

    [Fact]
    public async Task TrackRequest_ValidData_Succeeds()
    {
        // Arrange
        var apiKey = "test-key";
        var endpoint = "/api/quotes";
        var statusCode = 200;

        // Act & Assert
        await _usageTracker.TrackRequest(apiKey, endpoint, statusCode);
    }

    [Fact]
    public async Task GetMetrics_ReturnsMetrics()
    {
        // Arrange
        var apiKey = "test-key";
        var start = DateTime.UtcNow.AddDays(-1);
        var end = DateTime.UtcNow;

        // Act
        var metrics = await _usageTracker.GetMetrics(apiKey, start, end);

        // Assert
        Assert.NotNull(metrics);
    }
} 