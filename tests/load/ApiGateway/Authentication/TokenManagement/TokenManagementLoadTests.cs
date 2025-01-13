using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Caching.Distributed;
using Xunit;

public class TokenManagementLoadTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly ITokenManagementService _tokenManagement;
    private readonly IDistributedCache _cache;
    private readonly List<string> _testTokenIds;
    private readonly ConcurrentDictionary<string, MetricCollector> _metrics;

    public TokenManagementLoadTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
        var scope = _factory.Services.CreateScope();
        _tokenManagement = scope.ServiceProvider.GetRequiredService<ITokenManagementService>();
        _cache = scope.ServiceProvider.GetRequiredService<IDistributedCache>();
        _testTokenIds = new List<string>();
        _metrics = new ConcurrentDictionary<string, MetricCollector>();
    }

    [Theory]
    [InlineData(100, 60)] // 100 RPS for 1 minute
    [InlineData(500, 120)] // 500 RPS for 2 minutes
    [InlineData(1000, 180)] // 1000 RPS for 3 minutes
    public async Task SustainedLoad_TokenOperations(int requestsPerSecond, int durationSeconds)
    {
        // Arrange
        var cts = new CancellationTokenSource();
        var loadTasks = new List<Task>();
        var startTime = DateTime.UtcNow;
        var endTime = startTime.AddSeconds(durationSeconds);

        _metrics.TryAdd("create", new MetricCollector());
        _metrics.TryAdd("validate", new MetricCollector());
        _metrics.TryAdd("revoke", new MetricCollector());

        // Act
        // Start load generation tasks
        for (int i = 0; i < Environment.ProcessorCount; i++)
        {
            loadTasks.Add(GenerateLoadAsync(requestsPerSecond, endTime, cts.Token));
        }

        // Monitor and report metrics every 5 seconds
        loadTasks.Add(MonitorMetricsAsync(endTime, cts.Token));

        // Wait for the test duration
        await Task.Delay(TimeSpan.FromSeconds(durationSeconds));
        cts.Cancel();

        try
        {
            await Task.WhenAll(loadTasks);
        }
        catch (OperationCanceledException)
        {
            // Expected when test completes
        }

        // Assert
        foreach (var metric in _metrics)
        {
            var stats = metric.Value.GetStats();
            Assert.True(stats.AverageLatency < 100, 
                $"Average {metric.Key} latency ({stats.AverageLatency:F2}ms) exceeded 100ms");
            Assert.True(stats.P95Latency < 200, 
                $"P95 {metric.Key} latency ({stats.P95Latency:F2}ms) exceeded 200ms");
            Assert.True(stats.ErrorRate < 0.01, 
                $"Error rate for {metric.Key} ({stats.ErrorRate:P}) exceeded 1%");
        }

        // Cleanup
        await CleanupTokens();
    }

    [Fact]
    public async Task BurstLoad_TokenCreation()
    {
        // Arrange
        const int burstSize = 5000;
        const int burstDurationMs = 1000;
        var metrics = new MetricCollector();
        var tasks = new List<Task>();

        // Act
        var sw = Stopwatch.StartNew();
        for (int i = 0; i < burstSize; i++)
        {
            tasks.Add(Task.Run(async () =>
            {
                try
                {
                    var stopwatch = Stopwatch.StartNew();
                    var token = await _tokenManagement.StoreTokenAsync(
                        $"user{Guid.NewGuid()}",
                        $"access{Guid.NewGuid()}",
                        $"refresh{Guid.NewGuid()}");
                    stopwatch.Stop();

                    metrics.AddSuccess(stopwatch.ElapsedMilliseconds);
                    _testTokenIds.Add(token.TokenId);
                }
                catch (Exception)
                {
                    metrics.AddError();
                }
            }));
        }

        await Task.WhenAll(tasks);
        sw.Stop();

        // Assert
        var stats = metrics.GetStats();
        Assert.True(sw.ElapsedMilliseconds < burstDurationMs, 
            $"Burst processing time ({sw.ElapsedMilliseconds}ms) exceeded {burstDurationMs}ms");
        Assert.True(stats.ErrorRate < 0.05, 
            $"Error rate ({stats.ErrorRate:P}) exceeded 5%");

        // Cleanup
        await CleanupTokens();
    }

    [Fact]
    public async Task EnduranceTest_TokenLifecycle()
    {
        // Arrange
        const int testDurationMinutes = 10;
        const int operationsPerSecond = 100;
        var cts = new CancellationTokenSource(TimeSpan.FromMinutes(testDurationMinutes));
        var metrics = new MetricCollector();
        var activeTokens = new ConcurrentBag<TokenMetadata>();

        // Act
        var loadTask = Task.Run(async () =>
        {
            while (!cts.Token.IsCancellationRequested)
            {
                try
                {
                    await SimulateTokenLifecycle(activeTokens, metrics);
                    await Task.Delay(1000 / operationsPerSecond, cts.Token);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        });

        var monitorTask = Task.Run(async () =>
        {
            while (!cts.Token.IsCancellationRequested)
            {
                var stats = metrics.GetStats();
                _testTokenIds.AddRange(
                    activeTokens.Select(t => t.TokenId));
                
                await Task.Delay(5000, cts.Token);
            }
        });

        await Task.WhenAll(loadTask, monitorTask);

        // Assert
        var finalStats = metrics.GetStats();
        Assert.True(finalStats.ErrorRate < 0.01, 
            $"Error rate ({finalStats.ErrorRate:P}) exceeded 1%");
        Assert.True(finalStats.AverageLatency < 150, 
            $"Average latency ({finalStats.AverageLatency:F2}ms) exceeded 150ms");

        // Cleanup
        await CleanupTokens();
    }

    private async Task GenerateLoadAsync(
        int requestsPerSecond,
        DateTime endTime,
        CancellationToken cancellationToken)
    {
        var random = new Random();
        var intervalMs = 1000 / requestsPerSecond;
        var activeTokens = new ConcurrentBag<TokenMetadata>();

        while (DateTime.UtcNow < endTime && !cancellationToken.IsCancellationRequested)
        {
            var sw = Stopwatch.StartNew();
            try
            {
                await SimulateRandomOperation(random, activeTokens);
            }
            catch (Exception)
            {
                _metrics["create"].AddError();
            }
            sw.Stop();

            var delay = intervalMs - sw.ElapsedMilliseconds;
            if (delay > 0)
            {
                await Task.Delay((int)delay, cancellationToken);
            }
        }
    }

    private async Task SimulateTokenLifecycle(
        ConcurrentBag<TokenMetadata> activeTokens,
        MetricCollector metrics)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            // Create token
            var token = await _tokenManagement.StoreTokenAsync(
                $"user{Guid.NewGuid()}",
                $"access{Guid.NewGuid()}",
                $"refresh{Guid.NewGuid()}");
            activeTokens.Add(token);

            // Validate token
            await _tokenManagement.ValidateTokenPairAsync(
                token.AccessToken,
                token.RefreshToken);

            // Revoke token (sometimes)
            if (Random.Shared.Next(100) < 20) // 20% chance
            {
                await _tokenManagement.RevokeTokenAsync(token.TokenId);
                activeTokens.TryTake(out _);
            }

            sw.Stop();
            metrics.AddSuccess(sw.ElapsedMilliseconds);
        }
        catch (Exception)
        {
            metrics.AddError();
        }
    }

    private async Task SimulateRandomOperation(
        Random random,
        ConcurrentBag<TokenMetadata> activeTokens)
    {
        var operation = random.Next(3);
        var sw = Stopwatch.StartNew();

        switch (operation)
        {
            case 0: // Create
                var token = await _tokenManagement.StoreTokenAsync(
                    $"user{Guid.NewGuid()}",
                    $"access{Guid.NewGuid()}",
                    $"refresh{Guid.NewGuid()}");
                activeTokens.Add(token);
                _metrics["create"].AddSuccess(sw.ElapsedMilliseconds);
                break;

            case 1: // Validate
                if (activeTokens.TryTake(out var tokenToValidate))
                {
                    await _tokenManagement.ValidateTokenPairAsync(
                        tokenToValidate.AccessToken,
                        tokenToValidate.RefreshToken);
                    activeTokens.Add(tokenToValidate);
                    _metrics["validate"].AddSuccess(sw.ElapsedMilliseconds);
                }
                break;

            case 2: // Revoke
                if (activeTokens.TryTake(out var tokenToRevoke))
                {
                    await _tokenManagement.RevokeTokenAsync(tokenToRevoke.TokenId);
                    _metrics["revoke"].AddSuccess(sw.ElapsedMilliseconds);
                }
                break;
        }
    }

    private async Task MonitorMetricsAsync(DateTime endTime, CancellationToken cancellationToken)
    {
        while (DateTime.UtcNow < endTime && !cancellationToken.IsCancellationRequested)
        {
            foreach (var metric in _metrics)
            {
                var stats = metric.Value.GetStats();
                Debug.WriteLine($"{metric.Key} - Avg: {stats.AverageLatency:F2}ms, " +
                    $"P95: {stats.P95Latency:F2}ms, Errors: {stats.ErrorRate:P}");
            }
            await Task.Delay(5000, cancellationToken);
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

public class MetricCollector
{
    private readonly ConcurrentBag<long> _latencies = new();
    private long _errorCount;
    private long _totalCount;

    public void AddSuccess(long latencyMs)
    {
        _latencies.Add(latencyMs);
        Interlocked.Increment(ref _totalCount);
    }

    public void AddError()
    {
        Interlocked.Increment(ref _errorCount);
        Interlocked.Increment(ref _totalCount);
    }

    public MetricStats GetStats()
    {
        var latencies = _latencies.ToArray();
        return new MetricStats
        {
            AverageLatency = latencies.Any() ? latencies.Average() : 0,
            P95Latency = latencies.Any() ? 
                latencies.OrderBy(x => x)
                    .Skip((int)(latencies.Length * 0.95))
                    .FirstOrDefault() : 0,
            ErrorRate = _totalCount > 0 ? 
                (double)_errorCount / _totalCount : 0
        };
    }
}

public class MetricStats
{
    public double AverageLatency { get; set; }
    public double P95Latency { get; set; }
    public double ErrorRate { get; set; }
} 