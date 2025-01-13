using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Caching.Distributed;
using Xunit;

public class TokenManagementPerformanceTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly ITokenManagementService _tokenManagement;
    private readonly IDistributedCache _cache;
    private readonly Stopwatch _stopwatch;
    private readonly List<string> _testTokenIds;

    public TokenManagementPerformanceTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
        var scope = _factory.Services.CreateScope();
        _tokenManagement = scope.ServiceProvider.GetRequiredService<ITokenManagementService>();
        _cache = scope.ServiceProvider.GetRequiredService<IDistributedCache>();
        _stopwatch = new Stopwatch();
        _testTokenIds = new List<string>();
    }

    [Theory]
    [InlineData(100)]
    [InlineData(1000)]
    [InlineData(10000)]
    public async Task TokenCreation_Performance(int tokenCount)
    {
        // Arrange
        var metrics = new List<long>();
        var tokens = new List<TokenMetadata>();

        // Act
        for (int i = 0; i < tokenCount; i++)
        {
            _stopwatch.Restart();
            var token = await _tokenManagement.StoreTokenAsync(
                $"user{i}",
                $"access{i}",
                $"refresh{i}");
            _stopwatch.Stop();
            
            metrics.Add(_stopwatch.ElapsedMilliseconds);
            tokens.Add(token);
            _testTokenIds.Add(token.TokenId);
        }

        // Assert
        var avgTime = metrics.Average();
        var p95Time = metrics.OrderByDescending(x => x)
            .Skip((int)(tokenCount * 0.05))
            .First();

        Assert.True(avgTime < 50, $"Average token creation time ({avgTime}ms) exceeded 50ms");
        Assert.True(p95Time < 100, $"95th percentile token creation time ({p95Time}ms) exceeded 100ms");

        // Cleanup
        await CleanupTokens();
    }

    [Theory]
    [InlineData(100)]
    [InlineData(1000)]
    public async Task TokenValidation_Performance(int concurrentValidations)
    {
        // Arrange
        var token = await _tokenManagement.StoreTokenAsync(
            "testuser",
            "access_token",
            "refresh_token");
        _testTokenIds.Add(token.TokenId);

        var tasks = new List<Task>();
        var metrics = new ConcurrentBag<long>();

        // Act
        for (int i = 0; i < concurrentValidations; i++)
        {
            tasks.Add(Task.Run(async () =>
            {
                _stopwatch.Restart();
                await _tokenManagement.ValidateTokenPairAsync(
                    token.AccessToken,
                    token.RefreshToken);
                _stopwatch.Stop();
                metrics.Add(_stopwatch.ElapsedMilliseconds);
            }));
        }

        await Task.WhenAll(tasks);

        // Assert
        var avgTime = metrics.Average();
        var p95Time = metrics.OrderByDescending(x => x)
            .Skip((int)(concurrentValidations * 0.05))
            .First();

        Assert.True(avgTime < 20, $"Average validation time ({avgTime}ms) exceeded 20ms");
        Assert.True(p95Time < 50, $"95th percentile validation time ({p95Time}ms) exceeded 50ms");

        // Cleanup
        await CleanupTokens();
    }

    [Theory]
    [InlineData(1000)]
    [InlineData(10000)]
    public async Task TokenCleanup_Performance(int tokenCount)
    {
        // Arrange
        var cleanupService = _factory.Services.GetRequiredService<TokenCleanupService>();
        await CreateExpiredTokens(tokenCount);

        // Act
        _stopwatch.Restart();
        await cleanupService.StartAsync(CancellationToken.None);
        await Task.Delay(TimeSpan.FromSeconds(90)); // Allow cleanup to complete
        _stopwatch.Stop();

        // Assert
        var remainingTokens = await Task.WhenAll(
            _testTokenIds.Select(t => _tokenManagement.GetTokenMetadataAsync(t)));
        
        Assert.True(_stopwatch.ElapsedMilliseconds < tokenCount * 10, 
            $"Cleanup time ({_stopwatch.ElapsedMilliseconds}ms) exceeded budget");
        Assert.Equal(0, remainingTokens.Count(t => t != null));

        // Cleanup
        await cleanupService.StopAsync(CancellationToken.None);
        await CleanupTokens();
    }

    [Fact]
    public async Task ConcurrentOperations_Performance()
    {
        // Arrange
        const int operationCount = 1000;
        var random = new Random();
        var metrics = new ConcurrentDictionary<string, ConcurrentBag<long>>();
        metrics["create"] = new ConcurrentBag<long>();
        metrics["validate"] = new ConcurrentBag<long>();
        metrics["revoke"] = new ConcurrentBag<long>();

        var tokens = new ConcurrentBag<TokenMetadata>();
        var tasks = new List<Task>();

        // Act
        for (int i = 0; i < operationCount; i++)
        {
            tasks.Add(Task.Run(async () =>
            {
                var operation = random.Next(3);
                _stopwatch.Restart();

                switch (operation)
                {
                    case 0: // Create
                        var token = await _tokenManagement.StoreTokenAsync(
                            $"user{Guid.NewGuid()}",
                            $"access{Guid.NewGuid()}",
                            $"refresh{Guid.NewGuid()}");
                        tokens.Add(token);
                        metrics["create"].Add(_stopwatch.ElapsedMilliseconds);
                        break;

                    case 1: // Validate
                        if (tokens.TryTake(out var tokenToValidate))
                        {
                            await _tokenManagement.ValidateTokenPairAsync(
                                tokenToValidate.AccessToken,
                                tokenToValidate.RefreshToken);
                            tokens.Add(tokenToValidate);
                            metrics["validate"].Add(_stopwatch.ElapsedMilliseconds);
                        }
                        break;

                    case 2: // Revoke
                        if (tokens.TryTake(out var tokenToRevoke))
                        {
                            await _tokenManagement.RevokeTokenAsync(tokenToRevoke.TokenId);
                            metrics["revoke"].Add(_stopwatch.ElapsedMilliseconds);
                        }
                        break;
                }

                _stopwatch.Stop();
            }));
        }

        await Task.WhenAll(tasks);

        // Assert
        foreach (var metric in metrics)
        {
            var avgTime = metric.Value.Average();
            var p95Time = metric.Value.OrderByDescending(x => x)
                .Skip((int)(metric.Value.Count * 0.05))
                .FirstOrDefault();

            Assert.True(avgTime < 100, 
                $"Average {metric.Key} time ({avgTime}ms) exceeded 100ms");
            Assert.True(p95Time < 200, 
                $"95th percentile {metric.Key} time ({p95Time}ms) exceeded 200ms");
        }

        // Cleanup
        foreach (var token in tokens)
        {
            _testTokenIds.Add(token.TokenId);
        }
        await CleanupTokens();
    }

    private async Task CreateExpiredTokens(int count)
    {
        for (int i = 0; i < count; i++)
        {
            var token = await _tokenManagement.StoreTokenAsync(
                $"user{i}",
                $"access{i}",
                $"refresh{i}");
            
            var metadata = await _tokenManagement.GetTokenMetadataAsync(token.TokenId);
            metadata.ExpiresAt = DateTime.UtcNow.AddDays(-1);
            
            await _cache.SetStringAsync(
                $"token:{token.TokenId}",
                JsonSerializer.Serialize(metadata));
            
            _testTokenIds.Add(token.TokenId);
        }
    }

    private async Task CleanupTokens()
    {
        foreach (var tokenId in _testTokenIds)
        {
            await _cache.RemoveAsync($"token:{tokenId}");
        }
        _testTokenIds.Clear();
    }
} 