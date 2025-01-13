using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Microsoft.AspNetCore.Mvc.Testing;

public class RoutingLoadTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly ConcurrentDictionary<string, MetricCollector> _metrics;
    private readonly List<string> _endpoints;
    private readonly Random _random;

    public RoutingLoadTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _metrics = new ConcurrentDictionary<string, MetricCollector>();
        _endpoints = new List<string>
        {
            "/api/test",
            "/api/users",
            "/api/products",
            "/api/orders"
        };
        _random = new Random();
    }

    [Theory]
    [InlineData(100, 30)]  // 100 RPS for 30 seconds
    [InlineData(500, 15)]  // 500 RPS for 15 seconds
    [InlineData(1000, 5)]  // 1000 RPS for 5 seconds
    public async Task SustainedLoad_MaintainsPerformance(int requestsPerSecond, int durationSeconds)
    {
        // Arrange
        var client = _factory.CreateClient();
        var endTime = DateTime.UtcNow.AddSeconds(durationSeconds);
        var cts = new CancellationTokenSource();
        var loadTasks = new List<Task>();

        _metrics.TryAdd("routing", new MetricCollector());

        // Act
        // Start load generation tasks
        for (int i = 0; i < Environment.ProcessorCount; i++)
        {
            loadTasks.Add(GenerateLoadAsync(client, requestsPerSecond, endTime, cts.Token));
        }

        // Start metrics monitoring
        loadTasks.Add(MonitorMetricsAsync(endTime, cts.Token));

        await Task.WhenAll(loadTasks);

        // Assert
        var stats = _metrics["routing"].GetStatistics();
        
        Assert.True(stats.AverageLatency < 100, 
            $"Average latency ({stats.AverageLatency:F2}ms) exceeded 100ms");
        Assert.True(stats.P95Latency < 200, 
            $"P95 latency ({stats.P95Latency:F2}ms) exceeded 200ms");
        Assert.True(stats.ErrorRate < 0.01, 
            $"Error rate ({stats.ErrorRate:P}) exceeded 1%");
    }

    [Fact]
    public async Task BurstLoad_HandlesSpikes()
    {
        // Arrange
        var client = _factory.CreateClient();
        const int burstSize = 1000;
        const int burstCount = 5;
        var tasks = new List<Task>();

        _metrics.TryAdd("burst", new MetricCollector());

        // Act
        for (int burst = 0; burst < burstCount; burst++)
        {
            // Generate burst
            var burstTasks = Enumerable.Range(0, burstSize)
                .Select(_ => MeasureRequest(client, _endpoints[_random.Next(_endpoints.Count)]))
                .ToList();

            await Task.WhenAll(burstTasks);
            await Task.Delay(1000); // Wait between bursts
        }

        // Assert
        var stats = _metrics["burst"].GetStatistics();
        
        Assert.True(stats.AverageLatency < 200, 
            $"Burst average latency ({stats.AverageLatency:F2}ms) exceeded 200ms");
        Assert.True(stats.ErrorRate < 0.02, 
            $"Burst error rate ({stats.ErrorRate:P}) exceeded 2%");
    }

    [Fact]
    public async Task ConcurrentRoutes_MaintainsIsolation()
    {
        // Arrange
        var client = _factory.CreateClient();
        const int concurrentRoutes = 10;
        const int requestsPerRoute = 1000;
        var routeMetrics = new ConcurrentDictionary<string, MetricCollector>();

        // Act
        var tasks = new List<Task>();
        foreach (var endpoint in _endpoints)
        {
            routeMetrics.TryAdd(endpoint, new MetricCollector());
            for (int i = 0; i < requestsPerRoute; i++)
            {
                tasks.Add(MeasureRouteRequest(client, endpoint, routeMetrics[endpoint]));
            }
        }

        await Task.WhenAll(tasks);

        // Assert
        foreach (var endpoint in _endpoints)
        {
            var stats = routeMetrics[endpoint].GetStatistics();
            Assert.True(stats.AverageLatency < 150, 
                $"Route {endpoint} average latency ({stats.AverageLatency:F2}ms) exceeded 150ms");
        }
    }

    private async Task GenerateLoadAsync(
        HttpClient client,
        int requestsPerSecond,
        DateTime endTime,
        CancellationToken cancellationToken)
    {
        var interval = TimeSpan.FromSeconds(1.0 / requestsPerSecond);
        var sw = Stopwatch.StartNew();

        while (DateTime.UtcNow < endTime && !cancellationToken.IsCancellationRequested)
        {
            var endpoint = _endpoints[_random.Next(_endpoints.Count)];
            await MeasureRequest(client, endpoint);

            var elapsed = sw.Elapsed;
            if (elapsed < interval)
            {
                await Task.Delay(interval - elapsed);
            }
            sw.Restart();
        }
    }

    private async Task MonitorMetricsAsync(DateTime endTime, CancellationToken cancellationToken)
    {
        while (DateTime.UtcNow < endTime && !cancellationToken.IsCancellationRequested)
        {
            foreach (var metric in _metrics.Values)
            {
                var stats = metric.GetStatistics();
                Debug.WriteLine($"Current Stats - Avg: {stats.AverageLatency:F2}ms, " +
                              $"P95: {stats.P95Latency:F2}ms, " +
                              $"Errors: {stats.ErrorRate:P}");
            }
            await Task.Delay(1000, cancellationToken);
        }
    }

    private async Task MeasureRequest(HttpClient client, string endpoint)
    {
        try
        {
            var sw = Stopwatch.StartNew();
            var response = await client.GetAsync(endpoint);
            sw.Stop();

            _metrics["routing"].AddMetric(sw.ElapsedMilliseconds, response.IsSuccessStatusCode);
        }
        catch (Exception)
        {
            _metrics["routing"].AddMetric(0, false);
        }
    }

    private async Task MeasureRouteRequest(
        HttpClient client,
        string endpoint,
        MetricCollector collector)
    {
        try
        {
            var sw = Stopwatch.StartNew();
            var response = await client.GetAsync(endpoint);
            sw.Stop();

            collector.AddMetric(sw.ElapsedMilliseconds, response.IsSuccessStatusCode);
        }
        catch (Exception)
        {
            collector.AddMetric(0, false);
        }
    }

    private class MetricCollector
    {
        private readonly ConcurrentBag<double> _latencies = new();
        private long _totalCount;
        private long _errorCount;

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
            }
        }

        public MetricStats GetStatistics()
        {
            var latencyArray = _latencies.ToArray();
            Array.Sort(latencyArray);

            return new MetricStats
            {
                AverageLatency = latencyArray.Any() ? latencyArray.Average() : 0,
                P95Latency = latencyArray.Any() ? 
                    latencyArray.Skip((int)(latencyArray.Length * 0.95)).FirstOrDefault() : 0,
                ErrorRate = _totalCount > 0 ? (double)_errorCount / _totalCount : 0
            };
        }
    }

    private class MetricStats
    {
        public double AverageLatency { get; set; }
        public double P95Latency { get; set; }
        public double ErrorRate { get; set; }
    }
} 