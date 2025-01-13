using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;
using Microsoft.AspNetCore.Mvc.Testing;
using ApiGateway;

public class RateLimitingPerformanceTests
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly ConcurrentDictionary<string, List<double>> _latencies;
    private readonly ITestOutputHelper _output;

    public RateLimitingPerformanceTests(
        WebApplicationFactory<Program> factory,
        ITestOutputHelper output)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.AddStackExchangeRedisCache(options =>
                {
                    options.Configuration = "localhost:6379";
                });
            });
        });
        _latencies = new ConcurrentDictionary<string, List<double>>();
        _output = output;
    }

    [Theory]
    [InlineData(1000, 10)]  // 1000 requests over 10 seconds
    [InlineData(5000, 30)]  // 5000 requests over 30 seconds
    public async Task RateLimiting_Performance(int totalRequests, int durationSeconds)
    {
        // Arrange
        var client = _factory.CreateClient();
        var endpoint = "/api/test";
        var requestsPerSecond = totalRequests / durationSeconds;
        var sw = Stopwatch.StartNew();
        var tasks = new List<Task>();

        _latencies.TryAdd("requests", new List<double>());

        // Act
        for (int second = 0; second < durationSeconds; second++)
        {
            var secondStart = sw.ElapsedMilliseconds;
            
            for (int i = 0; i < requestsPerSecond; i++)
            {
                tasks.Add(MeasureRequest(client, endpoint));
            }

            await Task.WhenAll(tasks);
            tasks.Clear();

            var delay = 1000 - (sw.ElapsedMilliseconds - secondStart);
            if (delay > 0)
            {
                await Task.Delay((int)delay);
            }
        }

        // Assert
        var stats = CalculateStats(_latencies["requests"]);
        
        _output.WriteLine($"Total Requests: {totalRequests}");
        _output.WriteLine($"Duration: {durationSeconds} seconds");
        _output.WriteLine($"Average Latency: {stats.Average:F2}ms");
        _output.WriteLine($"P95 Latency: {stats.P95:F2}ms");
        _output.WriteLine($"P99 Latency: {stats.P99:F2}ms");
        _output.WriteLine($"Max Latency: {stats.Max:F2}ms");

        Assert.True(stats.Average < 10, 
            $"Average latency ({stats.Average:F2}ms) exceeded 10ms");
        Assert.True(stats.P95 < 50, 
            $"P95 latency ({stats.P95:F2}ms) exceeded 50ms");
    }

    [Fact]
    public async Task ConcurrentRequests_Performance()
    {
        // Arrange
        var client = _factory.CreateClient();
        var endpoint = "/api/test";
        const int concurrentRequests = 100;
        const int iterations = 10;

        _latencies.TryAdd("concurrent", new List<double>());

        // Act
        for (int i = 0; i < iterations; i++)
        {
            var tasks = Enumerable.Range(0, concurrentRequests)
                .Select(_ => MeasureRequest(client, endpoint))
                .ToList();

            await Task.WhenAll(tasks);
            await Task.Delay(1000); // Wait between bursts
        }

        // Assert
        var stats = CalculateStats(_latencies["concurrent"]);
        
        _output.WriteLine($"Concurrent Requests: {concurrentRequests}");
        _output.WriteLine($"Iterations: {iterations}");
        _output.WriteLine($"Average Latency: {stats.Average:F2}ms");
        _output.WriteLine($"P95 Latency: {stats.P95:F2}ms");

        Assert.True(stats.Average < 20, 
            $"Average concurrent latency ({stats.Average:F2}ms) exceeded 20ms");
    }

    private async Task MeasureRequest(HttpClient client, string endpoint)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            await client.GetAsync(endpoint);
        }
        finally
        {
            sw.Stop();
            _latencies["requests"].Add(sw.ElapsedMilliseconds);
        }
    }

    private static (double Average, double P95, double P99, double Max) 
        CalculateStats(List<double> latencies)
    {
        var sorted = latencies.OrderBy(l => l).ToList();
        var p95Index = (int)(sorted.Count * 0.95);
        var p99Index = (int)(sorted.Count * 0.99);

        return (
            Average: latencies.Average(),
            P95: sorted[p95Index],
            P99: sorted[p99Index],
            Max: sorted.Last()
        );
    }
} 