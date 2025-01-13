using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

public class RouteConfigurationServiceTests
{
    private readonly Mock<IConfiguration> _configMock;
    private readonly Mock<ILogger<RouteConfigurationService>> _loggerMock;
    private readonly Mock<IMemoryCache> _cacheMock;
    private readonly RouteConfigurationService _service;
    private readonly string _testConfigPath;

    public RouteConfigurationServiceTests()
    {
        _configMock = new Mock<IConfiguration>();
        _loggerMock = new Mock<ILogger<RouteConfigurationService>>();
        _cacheMock = new Mock<IMemoryCache>();
        _testConfigPath = Path.GetTempFileName();

        _configMock.Setup(x => x["RouteConfig:Path"])
            .Returns(_testConfigPath);

        _service = new RouteConfigurationService(
            _configMock.Object,
            _loggerMock.Object,
            _cacheMock.Object);
    }

    [Fact]
    public async Task GetRoutes_LoadsAndCachesRoutes()
    {
        // Arrange
        var routes = new List<RouteConfig>
        {
            new() { Path = "/api/test", Destination = "http://test" }
        };
        await File.WriteAllTextAsync(_testConfigPath, 
            JsonSerializer.Serialize(routes));

        object cachedRoutes = null;
        _cacheMock.Setup(x => x.TryGetValue(It.IsAny<string>(), out cachedRoutes))
            .Returns(false);

        // Act
        var result = await _service.GetRoutesAsync();

        // Assert
        Assert.Single(result);
        Assert.Equal("/api/test", result.First().Path);
        _cacheMock.Verify(x => x.Set(
            It.IsAny<string>(),
            It.IsAny<IEnumerable<RouteConfig>>(),
            It.IsAny<MemoryCacheEntryOptions>()),
            Times.Once);
    }

    [Fact]
    public async Task GetRouteForPath_MatchesCorrectRoute()
    {
        // Arrange
        var routes = new List<RouteConfig>
        {
            new() { 
                Path = "/api/test/{id}", 
                Destination = "http://test",
                Methods = new[] { "GET" }
            },
            new() { 
                Path = "/api/other", 
                Destination = "http://other",
                Methods = new[] { "POST" }
            }
        };
        await File.WriteAllTextAsync(_testConfigPath, 
            JsonSerializer.Serialize(routes));

        // Act
        var result = await _service.GetRouteForPathAsync("/api/test/123", "GET");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("/api/test/{id}", result.Path);
    }

    [Fact]
    public async Task GetRouteForPath_HandlesWildcardMethods()
    {
        // Arrange
        var routes = new List<RouteConfig>
        {
            new() { 
                Path = "/api/test", 
                Destination = "http://test",
                Methods = new[] { "*" }
            }
        };
        await File.WriteAllTextAsync(_testConfigPath, 
            JsonSerializer.Serialize(routes));

        // Act
        var result = await _service.GetRouteForPathAsync("/api/test", "DELETE");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("/api/test", result.Path);
    }

    [Fact]
    public async Task LoadRoutes_ValidatesConfiguration()
    {
        // Arrange
        var routes = new List<RouteConfig>
        {
            new() { Path = "", Destination = "http://test" }
        };
        await File.WriteAllTextAsync(_testConfigPath, 
            JsonSerializer.Serialize(routes));

        // Act & Assert
        await Assert.ThrowsAsync<RouteConfigurationException>(
            () => _service.GetRoutesAsync());
    }

    public void Dispose()
    {
        File.Delete(_testConfigPath);
    }
} 