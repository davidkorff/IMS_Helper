using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Logging;
using Xunit;

public class RoutingStressTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly ConcurrentDictionary<string, StressMetricCollector> _metrics;
    private readonly List<string> _endpoints;
    private readonly Random _random;
    private readonly ILogger<RoutingStressTests> _logger;

    public RoutingStressTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _metrics = new ConcurrentDictionary<string, StressMetricCollector>();
        _endpoints = new List<string>
        {
            "/api/test",
            "/api/users",
            "/api/products",
            "/api/orders"
        };
        _random = new Random();
        _logger = LoggerFactory.Create(builder => builder
            .AddConsole()
            .AddDebug())
            .CreateLogger<RoutingStressTests>();
    }

    [Fact]
    public async Task ExtremeConcurrency_SystemStability()
    {
        // Arrange
        var client = _factory.CreateClient();
        const int concurrentConnections = 5000;
        const int requestsPerConnection = 100;
        const int maxDurationMinutes = 5;

        _metrics.TryAdd("extreme_concurrency", new StressMetricCollector());
        var cts = new CancellationTokenSource(TimeSpan.FromMinutes(maxDurationMinutes));

        // Act
        var tasks = new List<Task>();
        for (int i = 0; i < concurrentConnections; i++)
        {
            tasks.Add(Task.Run(async () =>
            {
                for (int j = 0; j < requestsPerConnection && !cts.Token.IsCancellationRequested; j++)
                {
                    await MeasureStressRequest(client, "extreme_concurrency");
                    await Task.Delay(_random.Next(10, 100));
                }
            }, cts.Token));
        }

        tasks.Add(MonitorSystemResourcesAsync(cts.Token));

        try
        {
            await Task.WhenAll(tasks);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Test duration exceeded maximum time limit");
        }

        // Assert
        var stats = _metrics["extreme_concurrency"].GetStatistics();
        Assert.True(stats.ErrorRate < 0.05, 
            $"Error rate under extreme load ({stats.ErrorRate:P}) exceeded 5%");
        Assert.True(stats.MaxMemoryUsageMB < 2048, 
            $"Memory usage ({stats.MaxMemoryUsageMB}MB) exceeded 2GB");
    }

    [Fact]
    public async Task ResourceExhaustion_SystemRecovery()
    {
        // Arrange
        var client = _factory.CreateClient();
        const int largePayloadSizeKB = 1024; // 1MB
        const int concurrentRequests = 1000;
        var payload = new string('x', largePayloadSizeKB * 1024);

        _metrics.TryAdd("resource_exhaustion", new StressMetricCollector());

        // Act
        // Phase 1: Generate heavy load
        _logger.LogInformation("Starting resource exhaustion phase");
        var exhaustionTasks = new List<Task>();
        for (int i = 0; i < concurrentRequests; i++)
        {
            exhaustionTasks.Add(SendLargePayload(client, payload));
        }
        await Task.WhenAll(exhaustionTasks);

        // Phase 2: Measure recovery
        _logger.LogInformation("Starting recovery phase");
        var recoveryStart = DateTime.UtcNow;
        var recovered = false;
        var recoveryTimeout = TimeSpan.FromMinutes(2);

        while (DateTime.UtcNow - recoveryStart < recoveryTimeout && !recovered)
        {
            var healthCheck = await client.GetAsync("/health");
            recovered = healthCheck.IsSuccessStatusCode;
            await Task.Delay(1000);
        }

        // Assert
        Assert.True(recovered, "System failed to recover within timeout period");
        var stats = _metrics["resource_exhaustion"].GetStatistics();
        Assert.True(stats.RecoveryTimeMs < recoveryTimeout.TotalMilliseconds,
            $"Recovery time ({stats.RecoveryTimeMs}ms) exceeded timeout");
    }

    [Fact]
    public async Task MemoryLeakDetection_LongRunning()
    {
        // Arrange
        var client = _factory.CreateClient();
        const int durationMinutes = 10;
        const int samplingIntervalSeconds = 30;
        var memoryReadings = new ConcurrentBag<long>();
        var endTime = DateTime.UtcNow.AddMinutes(durationMinutes);

        _metrics.TryAdd("memory_leak", new StressMetricCollector());

        // Act
        var loadTask = GenerateConstantLoadAsync(client, endTime);
        var monitoringTask = Task.Run(async () =>
        {
            while (DateTime.UtcNow < endTime)
            {
                GC.Collect();
                var memory = GC.GetTotalMemory(true);
                memoryReadings.Add(memory / 1024 / 1024); // Convert to MB
                await Task.Delay(TimeSpan.FromSeconds(samplingIntervalSeconds));
            }
        });

        await Task.WhenAll(loadTask, monitoringTask);

        // Assert
        var memoryGrowthRate = CalculateMemoryGrowthRate(memoryReadings.ToArray());
        Assert.True(memoryGrowthRate < 1.0, 
            $"Memory growth rate ({memoryGrowthRate:F2} MB/min) indicates potential leak");
    }

    private async Task GenerateConstantLoadAsync(HttpClient client, DateTime endTime)
    {
        var tasks = new List<Task>();
        while (DateTime.UtcNow < endTime)
        {
            tasks.Add(MeasureStressRequest(client, "memory_leak"));
            if (tasks.Count >= 100)
            {
                await Task.WhenAll(tasks);
                tasks.Clear();
            }
        }
    }

    private async Task MonitorSystemResourcesAsync(CancellationToken cancellationToken)
    {
        var process = Process.GetCurrentProcess();
        while (!cancellationToken.IsCancellationRequested)
        {
            var memoryUsage = process.WorkingSet64 / 1024 / 1024; // MB
            var cpuTime = process.TotalProcessorTime;
            var threadCount = process.Threads.Count;

            _logger.LogInformation(
                "System Metrics - Memory: {Memory}MB, Threads: {Threads}, CPU Time: {CpuTime}",
                memoryUsage, threadCount, cpuTime);

            foreach (var metric in _metrics.Values)
            {
                metric.UpdateSystemMetrics(memoryUsage, threadCount, cpuTime.TotalMilliseconds);
            }

            await Task.Delay(5000, cancellationToken);
        }
    }

    private async Task MeasureStressRequest(HttpClient client, string metricKey)
    {
        try
        {
            var sw = Stopwatch.StartNew();
            var endpoint = _endpoints[_random.Next(_endpoints.Count)];
            var response = await client.GetAsync(endpoint);
            sw.Stop();

            _metrics[metricKey].AddMetric(sw.ElapsedMilliseconds, response.IsSuccessStatusCode);
        }
        catch (Exception ex)
        {
            _metrics[metricKey].AddMetric(0, false);
            _logger.LogError(ex, "Request failed during stress test");
        }
    }

    private async Task SendLargePayload(HttpClient client, string payload)
    {
        try
        {
            var content = new StringContent(payload, Encoding.UTF8, "application/json");
            var sw = Stopwatch.StartNew();
            var response = await client.PostAsync("/api/test", content);
            sw.Stop();

            _metrics["resource_exhaustion"].AddMetric(
                sw.ElapsedMilliseconds, 
                response.IsSuccessStatusCode);
        }
        catch (Exception ex)
        {
            _metrics["resource_exhaustion"].AddMetric(0, false);
            _logger.LogError(ex, "Large payload request failed");
        }
    }

    private double CalculateMemoryGrowthRate(long[] memoryReadings)
    {
        if (memoryReadings.Length < 2) return 0;

        var firstReading = memoryReadings.First();
        var lastReading = memoryReadings.Last();
        var timeSpanMinutes = memoryReadings.Length * 0.5; // 30-second intervals

        return (lastReading - firstReading) / timeSpanMinutes;
    }

    private class StressMetricCollector
    {
        private readonly ConcurrentBag<double> _latencies = new();
        private readonly ConcurrentBag<SystemMetrics> _systemMetrics = new();
        private long _totalCount;
        private long _errorCount;
        private readonly Stopwatch _recoveryTimer = new();

        public void AddMetric(double latency, bool success)
        {
            if (latency > 0)
            {
                _latencies.Add(latency);
            }
            Interlocked.Increment(ref _totalCount);
            if (!success)
            {
                Interlocked.Increment(ref _errorCount);
                if (!_recoveryTimer.IsRunning)
                {
                    _recoveryTimer.Start();
                }
            }
            else if (_recoveryTimer.IsRunning)
            {
                _recoveryTimer.Stop();
            }
        }

        public void UpdateSystemMetrics(long memoryMB, int threadCount, double cpuTimeMs)
        {
            _systemMetrics.Add(new SystemMetrics
            {
                MemoryUsageMB = memoryMB,
                ThreadCount = threadCount,
                CpuTimeMs = cpuTimeMs,
                Timestamp = DateTime.UtcNow
            });
        }

        public StressMetricStats GetStatistics()
        {
            var metrics = _systemMetrics.ToArray();
            return new StressMetricStats
            {
                AverageLatency = _latencies.Any() ? _latencies.Average() : 0,
                P95Latency = CalculatePercentile(_latencies.ToArray(), 0.95),
                P99Latency = CalculatePercentile(_latencies.ToArray(), 0.99),
                ErrorRate = _totalCount > 0 ? (double)_errorCount / _totalCount : 0,
                MaxMemoryUsageMB = metrics.Any() ? metrics.Max(m => m.MemoryUsageMB) : 0,
                MaxThreadCount = metrics.Any() ? metrics.Max(m => m.ThreadCount) : 0,
                TotalCpuTimeMs = metrics.Any() ? metrics.Max(m => m.CpuTimeMs) : 0,
                RecoveryTimeMs = _recoveryTimer.ElapsedMilliseconds
            };
        }

        private static double CalculatePercentile(double[] values, double percentile)
        {
            if (!values.Any()) return 0;
            Array.Sort(values);
            var index = (int)Math.Ceiling(percentile * values.Length) - 1;
            return values[Math.Max(0, index)];
        }
    }

    private class SystemMetrics
    {
        public long MemoryUsageMB { get; set; }
        public int ThreadCount { get; set; }
        public double CpuTimeMs { get; set; }
        public DateTime Timestamp { get; set; }
    }

    private class StressMetricStats
    {
        public double AverageLatency { get; set; }
        public double P95Latency { get; set; }
        public double P99Latency { get; set; }
        public double ErrorRate { get; set; }
        public long MaxMemoryUsageMB { get; set; }
        public int MaxThreadCount { get; set; }
        public double TotalCpuTimeMs { get; set; }
        public double RecoveryTimeMs { get; set; }
    }
} 