using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ApiGateway.RateLimiting.Cache;
using ApiGateway.RateLimiting.Models;
using ApiGateway.RateLimiting.Tests;

public class DistributedRateLimitCacheTests : IClassFixture<RedisFixture>
{
    private readonly RedisFixture _redis;
    private readonly ITestOutputHelper _output;
    private readonly DistributedRateLimitCache _cache;

    public DistributedRateLimitCacheTests(
        RedisFixture redis,
        ITestOutputHelper output)
    {
        _redis = redis;
        _output = output;
        
        var loggerFactory = LoggerFactory.Create(builder => 
            builder.AddXUnit(output));
        
        var options = Options.Create(new RateLimitingOptions());
        
        _cache = new DistributedRateLimitCache(
            _redis.Cache,
            loggerFactory.CreateLogger<DistributedRateLimitCache>(),
            options);
    }

    [Fact]
    public async Task SetAndGet_WorksWithRedis()
    {
        // Arrange
        var key = $"test:ratelimit:{Guid.NewGuid()}";
        var counter = new RateLimitCounter
        {
            Timestamp = DateTimeOffset.UtcNow,
            Count = 5
        };

        // Act
        await _cache.SetAsync(key, counter);
        var retrieved = await _cache.GetAsync(key);

        // Assert
        Assert.NotNull(retrieved);
        Assert.Equal(counter.Count, retrieved.Count);
        Assert.Equal(
            counter.Timestamp.ToUnixTimeSeconds(),
            retrieved.Timestamp.ToUnixTimeSeconds());
    }

    [Fact]
    public async Task Expiration_WorksAsExpected()
    {
        // Arrange
        var key = $"test:ratelimit:{Guid.NewGuid()}";
        var counter = new RateLimitCounter
        {
            Timestamp = DateTimeOffset.UtcNow,
            Count = 1
        };

        // Act
        await _cache.SetAsync(key, counter, TimeSpan.FromSeconds(1));
        var beforeExpiration = await _cache.ExistsAsync(key);
        await Task.Delay(1500); // Wait for expiration
        var afterExpiration = await _cache.ExistsAsync(key);

        // Assert
        Assert.True(beforeExpiration);
        Assert.False(afterExpiration);
    }

    [Fact]
    public async Task ConcurrentAccess_HandledCorrectly()
    {
        // Arrange
        var key = $"test:ratelimit:{Guid.NewGuid()}";
        const int concurrentTasks = 100;
        var tasks = new List<Task>();

        // Act
        for (int i = 0; i < concurrentTasks; i++)
        {
            tasks.Add(Task.Run(async () =>
            {
                var counter = new RateLimitCounter
                {
                    Timestamp = DateTimeOffset.UtcNow,
                    Count = 1
                };
                await _cache.SetAsync(key, counter);
                await _cache.GetAsync(key);
            }));
        }

        // Assert
        await Task.WhenAll(tasks);
        var finalValue = await _cache.GetAsync(key);
        Assert.NotNull(finalValue);
    }
} 