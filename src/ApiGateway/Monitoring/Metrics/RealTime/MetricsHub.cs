using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;

public class MetricsHub : Hub
{
    private readonly ILogger<MetricsHub> _logger;
    private readonly MetricsRegistry _metrics;

    public MetricsHub(
        ILogger<MetricsHub> logger,
        MetricsRegistry metrics)
    {
        _logger = logger;
        _metrics = metrics;
    }

    public override async Task OnConnectedAsync()
    {
        _logger.LogInformation(
            "Client connected to metrics hub: {ConnectionId}",
            Context.ConnectionId);

        // Send initial metrics data
        await SendMetricsUpdate();
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception exception)
    {
        _logger.LogInformation(
            "Client disconnected from metrics hub: {ConnectionId}",
            Context.ConnectionId);

        await base.OnDisconnectedAsync(exception);
    }

    private async Task SendMetricsUpdate()
    {
        try
        {
            var metricsData = new
            {
                Timestamp = DateTime.UtcNow,
                System = new
                {
                    CpuUsage = _metrics.CpuUsage.Value,
                    MemoryUsage = _metrics.MemoryUsage.Value,
                    ThreadCount = _metrics.ThreadCount.Value
                },
                Requests = new
                {
                    Total = _metrics.RequestCount.Value,
                    Errors = _metrics.ErrorCount.Value,
                    AverageDuration = _metrics.RequestDuration.Mean
                },
                Cache = new
                {
                    Hits = _metrics.CacheHits.Value,
                    Misses = _metrics.CacheMisses.Value,
                    Size = _metrics.CacheSize.Value
                },
                RateLimiting = new
                {
                    Exceeded = _metrics.RateLimitExceeded.Value,
                    ActiveBuckets = _metrics.ActiveTokenBuckets.Value
                }
            };

            await Clients.All.SendAsync("MetricsUpdate", metricsData);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending metrics update");
        }
    }
} 