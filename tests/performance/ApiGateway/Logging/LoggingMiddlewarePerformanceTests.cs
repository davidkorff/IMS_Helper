using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;
using Microsoft.AspNetCore.Mvc.Testing;

public class LoggingMiddlewarePerformanceTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;
    private readonly ConcurrentBag<PerformanceMetric> _metrics;

    public LoggingMiddlewarePerformanceTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _client = _factory.CreateClient();
        _metrics = new ConcurrentBag<PerformanceMetric>();
    }

    [Theory]
    [InlineData(100)]
    [InlineData(1000)]
    [InlineData(5000)]
    public async Task LoggingOverhead_UnderThreshold(int requestCount)
    {
        // Arrange
        const int maxOverheadMs = 5; // Maximum acceptable logging overhead per request
        var tasks = new List<Task>();

        // Act
        var sw = Stopwatch.StartNew();
        
        for (int i = 0; i < requestCount; i++)
        {
            tasks.Add(MeasureRequestTime($"test-{i}"));
        }

        await Task.WhenAll(tasks);
        sw.Stop();

        // Assert
        var avgOverhead = _metrics.Average(m => m.LoggingOverhead);
        var p95Overhead = _metrics
            .OrderByDescending(m => m.LoggingOverhead)
            .Skip((int)(requestCount * 0.05))
            .First()
            .LoggingOverhead;

        Assert.True(avgOverhead < maxOverheadMs, 
            $"Average logging overhead ({avgOverhead:F2}ms) exceeded {maxOverheadMs}ms");
        Assert.True(p95Overhead < maxOverheadMs * 2, 
            $"P95 logging overhead ({p95Overhead:F2}ms) exceeded {maxOverheadMs * 2}ms");
    }

    [Fact]
    public async Task LargePayload_LoggingPerformance()
    {
        // Arrange
        const int payloadSizeKB = 1024; // 1MB
        var largePayload = new
        {
            data = new string('x', payloadSizeKB * 1024)
        };
        var content = new StringContent(
            JsonSerializer.Serialize(largePayload),
            Encoding.UTF8,
            "application/json");

        // Act
        var sw = Stopwatch.StartNew();
        var response = await _client.PostAsync("/api/test/log", content);
        sw.Stop();

        // Assert
        Assert.True(response.IsSuccessStatusCode);
        Assert.True(sw.ElapsedMilliseconds < 1000, 
            "Large payload logging took too long");
    }

    [Fact]
    public async Task ConcurrentLogging_Performance()
    {
        // Arrange
        const int concurrentRequests = 100;
        const int maxConcurrentOverheadMs = 10;
        var tasks = new List<Task>();
        var metrics = new ConcurrentBag<double>();

        // Act
        for (int i = 0; i < concurrentRequests; i++)
        {
            tasks.Add(Task.Run(async () =>
            {
                var sw = Stopwatch.StartNew();
                await _client.GetAsync("/api/test/log");
                sw.Stop();
                metrics.Add(sw.ElapsedMilliseconds);
            }));
        }

        await Task.WhenAll(tasks);

        // Assert
        var avgTime = metrics.Average();
        var p95Time = metrics
            .OrderByDescending(x => x)
            .Skip((int)(concurrentRequests * 0.05))
            .First();

        Assert.True(avgTime < maxConcurrentOverheadMs, 
            $"Average concurrent request time ({avgTime:F2}ms) exceeded {maxConcurrentOverheadMs}ms");
        Assert.True(p95Time < maxConcurrentOverheadMs * 2, 
            $"P95 concurrent request time ({p95Time:F2}ms) exceeded {maxConcurrentOverheadMs * 2}ms");
    }

    [Fact]
    public async Task MemoryUsage_UnderThreshold()
    {
        // Arrange
        const int requestCount = 10000;
        const int maxMemoryIncreaseMB = 50;
        var initialMemory = GC.GetTotalMemory(true);
        var tasks = new List<Task>();

        // Act
        for (int i = 0; i < requestCount; i++)
        {
            tasks.Add(_client.GetAsync("/api/test/log"));
            
            if (i % 1000 == 0)
            {
                GC.Collect();
                await Task.Delay(100);
            }
        }

        await Task.WhenAll(tasks);
        GC.Collect();
        var finalMemory = GC.GetTotalMemory(true);
        var memoryIncreaseMB = (finalMemory - initialMemory) / 1024 / 1024;

        // Assert
        Assert.True(memoryIncreaseMB < maxMemoryIncreaseMB, 
            $"Memory increase ({memoryIncreaseMB}MB) exceeded {maxMemoryIncreaseMB}MB");
    }

    private async Task MeasureRequestTime(string requestId)
    {
        var metric = new PerformanceMetric { RequestId = requestId };
        
        var sw = Stopwatch.StartNew();
        var baselineResponse = await _client.GetAsync($"/api/test/nolog?id={requestId}");
        sw.Stop();
        metric.BaselineTime = sw.ElapsedMilliseconds;

        sw.Restart();
        var loggedResponse = await _client.GetAsync($"/api/test/log?id={requestId}");
        sw.Stop();
        metric.LoggedTime = sw.ElapsedMilliseconds;

        metric.LoggingOverhead = metric.LoggedTime - metric.BaselineTime;
        _metrics.Add(metric);
    }

    private class PerformanceMetric
    {
        public string RequestId { get; set; }
        public double BaselineTime { get; set; }
        public double LoggedTime { get; set; }
        public double LoggingOverhead { get; set; }
    }
} 