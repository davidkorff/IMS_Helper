using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Caching.Distributed;
using Xunit;

public class TokenManagementStressTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly ITokenManagementService _tokenManagement;
    private readonly IDistributedCache _cache;
    private readonly List<string> _testTokenIds;
    private readonly ConcurrentDictionary<string, MetricCollector> _metrics;

    public TokenManagementStressTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
        var scope = _factory.Services.CreateScope();
        _tokenManagement = scope.ServiceProvider.GetRequiredService<ITokenManagementService>();
        _cache = scope.ServiceProvider.GetRequiredService<IDistributedCache>();
        _testTokenIds = new List<string>();
        _metrics = new ConcurrentDictionary<string, MetricCollector>();
    }

    [Theory]
    [InlineData(2000, 30)]  // 2000 RPS for 30 seconds
    [InlineData(5000, 15)]  // 5000 RPS for 15 seconds
    [InlineData(10000, 5)]  // 10000 RPS for 5 seconds
    public async Task ExtremeLoad_TokenOperations(int requestsPerSecond, int durationSeconds)
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
        // Start stress load generation tasks
        for (int i = 0; i < Environment.ProcessorCount * 2; i++)
        {
            loadTasks.Add(GenerateStressLoadAsync(requestsPerSecond, endTime, cts.Token));
        }

        loadTasks.Add(MonitorSystemResourcesAsync(endTime, cts.Token));
        loadTasks.Add(MonitorMetricsAsync(endTime, cts.Token));

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
            Assert.True(stats.ErrorRate < 0.05, 
                $"Error rate for {metric.Key} ({stats.ErrorRate:P}) exceeded 5%");
        }

        // Cleanup
        await CleanupTokens();
    }

    [Fact]
    public async Task ResourceExhaustion_MemoryPressure()
    {
        // Arrange
        const int largeTokenCount = 1_000_000;
        var metrics = new MetricCollector();
        var tasks = new List<Task>();
        var memoryWatcher = new MemoryWatcher();

        // Act
        memoryWatcher.Start();
        
        for (int i = 0; i < largeTokenCount; i++)
        {
            if (i % 10000 == 0)
            {
                // Allow GC to run and monitor memory
                await Task.Delay(100);
                memoryWatcher.CheckPoint();
            }

            tasks.Add(Task.Run(async () =>
            {
                try
                {
                    var token = await _tokenManagement.StoreTokenAsync(
                        $"user{Guid.NewGuid()}",
                        $"access{Guid.NewGuid()}",
                        $"refresh{Guid.NewGuid()}");
                    metrics.AddSuccess(0);
                    _testTokenIds.Add(token.TokenId);
                }
                catch (Exception)
                {
                    metrics.AddError();
                }
            }));
        }

        await Task.WhenAll(tasks);
        var memoryStats = memoryWatcher.Stop();

        // Assert
        Assert.True(metrics.GetStats().ErrorRate < 0.05, 
            "Error rate exceeded 5% during memory pressure test");
        Assert.True(memoryStats.MaxMemoryGrowthRate < 100, 
            "Memory growth rate exceeded 100MB/s");

        // Cleanup
        await CleanupTokens();
    }

    [Fact]
    public async Task ConcurrencyLimit_TokenOperations()
    {
        // Arrange
        const int concurrentOperations = 10000;
        var metrics = new MetricCollector();
        var tasks = new List<Task>();
        var semaphore = new SemaphoreSlim(1000); // Limit concurrent operations

        // Act
        var sw = Stopwatch.StartNew();
        for (int i = 0; i < concurrentOperations; i++)
        {
            tasks.Add(Task.Run(async () =>
            {
                try
                {
                    await semaphore.WaitAsync();
                    var stopwatch = Stopwatch.StartNew();
                    
                    var token = await _tokenManagement.StoreTokenAsync(
                        $"user{Guid.NewGuid()}",
                        $"access{Guid.NewGuid()}",
                        $"refresh{Guid.NewGuid()}");
                    
                    await _tokenManagement.ValidateTokenPairAsync(
                        token.AccessToken,
                        token.RefreshToken);
                    
                    await _tokenManagement.RevokeTokenAsync(token.TokenId);
                    
                    stopwatch.Stop();
                    metrics.AddSuccess(stopwatch.ElapsedMilliseconds);
                    _testTokenIds.Add(token.TokenId);
                }
                catch (Exception)
                {
                    metrics.AddError();
                }
                finally
                {
                    semaphore.Release();
                }
            }));
        }

        await Task.WhenAll(tasks);
        sw.Stop();

        // Assert
        var stats = metrics.GetStats();
        Assert.True(stats.ErrorRate < 0.05, 
            $"Error rate ({stats.ErrorRate:P}) exceeded 5%");
        Assert.True(stats.P95Latency < 500, 
            $"P95 latency ({stats.P95Latency:F2}ms) exceeded 500ms under concurrent load");

        // Cleanup
        await CleanupTokens();
    }

    [Fact]
    public async Task NetworkPartition_Resilience()
    {
        // Arrange
        var networkSimulator = new NetworkSimulator(_cache);
        var metrics = new MetricCollector();
        const int operationCount = 1000;
        var tasks = new List<Task>();

        // Act
        for (int i = 0; i < operationCount; i++)
        {
            tasks.Add(Task.Run(async () =>
            {
                try
                {
                    // Simulate network issues
                    networkSimulator.SimulateNetworkIssue();
                    
                    var token = await _tokenManagement.StoreTokenAsync(
                        $"user{Guid.NewGuid()}",
                        $"access{Guid.NewGuid()}",
                        $"refresh{Guid.NewGuid()}");
                    
                    _testTokenIds.Add(token.TokenId);
                    metrics.AddSuccess(0);
                }
                catch (Exception)
                {
                    metrics.AddError();
                }
            }));
        }

        await Task.WhenAll(tasks);

        // Assert
        var stats = metrics.GetStats();
        Assert.True(stats.ErrorRate < 0.10, 
            $"Error rate ({stats.ErrorRate:P}) exceeded 10% during network issues");

        // Cleanup
        await CleanupTokens();
    }

    private async Task GenerateStressLoadAsync(
        int requestsPerSecond,
        DateTime endTime,
        CancellationToken cancellationToken)
    {
        var random = new Random();
        var intervalMs = 1000.0 / requestsPerSecond;
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

    private async Task MonitorSystemResourcesAsync(
        DateTime endTime,
        CancellationToken cancellationToken)
    {
        var process = Process.GetCurrentProcess();
        while (DateTime.UtcNow < endTime && !cancellationToken.IsCancellationRequested)
        {
            var memoryMB = process.WorkingSet64 / 1024 / 1024;
            var cpuTime = process.TotalProcessorTime;
            var threadCount = process.Threads.Count;

            Debug.WriteLine($"Memory: {memoryMB}MB, Threads: {threadCount}");
            await Task.Delay(1000, cancellationToken);
        }
    }

    // Helper classes
    private class MemoryWatcher
    {
        private readonly List<(DateTime Time, long Memory)> _measurements;
        private readonly Process _process;

        public MemoryWatcher()
        {
            _measurements = new List<(DateTime, long)>();
            _process = Process.GetCurrentProcess();
        }

        public void Start() => CheckPoint();

        public void CheckPoint()
        {
            _measurements.Add((DateTime.UtcNow, _process.WorkingSet64));
        }

        public MemoryStats Stop()
        {
            CheckPoint();
            return CalculateStats();
        }

        private MemoryStats CalculateStats()
        {
            var growthRates = new List<double>();
            for (int i = 1; i < _measurements.Count; i++)
            {
                var timeDiff = (_measurements[i].Time - _measurements[i-1].Time).TotalSeconds;
                var memoryDiff = (_measurements[i].Memory - _measurements[i-1].Memory) / 1024.0 / 1024.0;
                growthRates.Add(memoryDiff / timeDiff);
            }

            return new MemoryStats
            {
                MaxMemoryGrowthRate = growthRates.Max(),
                AverageMemoryGrowthRate = growthRates.Average()
            };
        }
    }

    private class NetworkSimulator
    {
        private readonly IDistributedCache _cache;
        private readonly Random _random;

        public NetworkSimulator(IDistributedCache cache)
        {
            _cache = cache;
            _random = new Random();
        }

        public void SimulateNetworkIssue()
        {
            if (_random.Next(100) < 20) // 20% chance of network issue
            {
                Thread.Sleep(_random.Next(100, 500)); // Random delay
                if (_random.Next(100) < 50) // 50% chance of exception
                {
                    throw new TimeoutException("Simulated network timeout");
                }
            }
        }
    }

    private class MemoryStats
    {
        public double MaxMemoryGrowthRate { get; set; }
        public double AverageMemoryGrowthRate { get; set; }
    }

    // Other helper methods remain the same as in load tests
    private async Task SimulateRandomOperation(Random random, ConcurrentBag<TokenMetadata> activeTokens)
    {
        // Implementation same as load tests
    }

    private async Task MonitorMetricsAsync(DateTime endTime, CancellationToken cancellationToken)
    {
        // Implementation same as load tests
    }

    private async Task CleanupTokens()
    {
        // Implementation same as load tests
    }
} 