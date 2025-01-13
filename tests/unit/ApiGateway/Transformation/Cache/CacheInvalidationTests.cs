using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

public class CacheInvalidationTests
{
    private readonly Mock<IDistributedCache> _cacheMock;
    private readonly Mock<ILogger<CacheInvalidationService>> _loggerMock;
    private readonly TransformationOptions _options;
    private readonly CacheInvalidationService _service;

    public CacheInvalidationTests()
    {
        _cacheMock = new Mock<IDistributedCache>();
        _loggerMock = new Mock<ILogger<CacheInvalidationService>>();
        _options = new TransformationOptions();
        _service = new CacheInvalidationService(
            _cacheMock.Object,
            _loggerMock.Object,
            Options.Create(_options));
    }

    [Fact]
    public async Task InvalidateByPattern_RemovesMatchingEntries()
    {
        // Arrange
        var pattern = "test:*";
        _cacheMock.Setup(x => x.RemoveAsync(
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await _service.InvalidateByPatternAsync(pattern);

        // Assert
        _cacheMock.Verify(
            x => x.RemoveAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task InvalidateByDependency_RemovesDependentEntries()
    {
        // Arrange
        var dependency = "user:123";
        var dependentKeys = new[] { "cache:1", "cache:2" };
        
        _cacheMock.Setup(x => x.GetAsync(
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(JsonSerializer.SerializeToUtf8Bytes(dependentKeys));

        // Act
        await _service.InvalidateByDependencyAsync(dependency);

        // Assert
        _cacheMock.Verify(
            x => x.RemoveAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Exactly(dependentKeys.Length + 1));
    }
} 