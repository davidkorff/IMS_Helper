using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Caching.Distributed;
using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using System.Linq;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

public class QueueMonitoringService : BackgroundService
{
    private readonly ILogger<QueueMonitoringService> _logger;
    private readonly IMetricsCollector _metrics;
    private readonly NotificationPriorityQueue _queue;
    private readonly IDistributedCache _cache;
    private readonly ConcurrentDictionary<string, ChannelMetrics> _channelMetrics;
    private readonly TimeSpan _monitoringInterval = TimeSpan.FromSeconds(15);

    public QueueMonitoringService(
        ILogger<QueueMonitoringService> logger,
        IMetricsCollector metrics,
        NotificationPriorityQueue queue,
        IDistributedCache cache)
    {
        _logger = logger;
        _metrics = metrics;
        _queue = queue;
        _cache = cache;
        _channelMetrics = new ConcurrentDictionary<string, ChannelMetrics>();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CollectQueueMetricsAsync();
                await DetectAnomaliesAsync();
                await UpdateChannelHealthAsync();
                await Task.Delay(_monitoringInterval, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in queue monitoring");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }

    private async Task CollectQueueMetricsAsync()
    {
        var metrics = await _queue.GetQueueMetricsAsync();
        
        _metrics.RecordGauge("queue_length", metrics.QueueLength);
        _metrics.RecordGauge("queue_processing_rate", metrics.ProcessingRate);
        
        foreach (var (channel, rate) in metrics.ChannelRates)
        {
            _metrics.RecordGauge("channel_processing_rate", rate, 
                new Dictionary<string, string> { ["channel"] = channel });
        }

        foreach (var (severity, count) in metrics.SeverityDistribution)
        {
            _metrics.RecordGauge("queued_by_severity", count, 
                new Dictionary<string, string> { ["severity"] = severity });
        }

        // Update channel metrics
        foreach (var channel in metrics.ChannelMetrics)
        {
            _channelMetrics.AddOrUpdate(
                channel.Key,
                _ => new ChannelMetrics(channel.Value),
                (_, existing) => existing.Update(channel.Value));
        }
    }

    private async Task DetectAnomaliesAsync()
    {
        foreach (var (channel, metrics) in _channelMetrics)
        {
            var anomalies = metrics.DetectAnomalies();
            if (anomalies.Any())
            {
                _logger.LogWarning(
                    "Detected anomalies for channel {Channel}: {Anomalies}",
                    channel, string.Join(", ", anomalies));

                await _cache.SetStringAsync(
                    $"channel_anomalies_{channel}",
                    JsonSerializer.Serialize(anomalies),
                    new DistributedCacheEntryOptions
                    {
                        AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5)
                    });
            }
        }
    }

    private async Task UpdateChannelHealthAsync()
    {
        foreach (var (channel, metrics) in _channelMetrics)
        {
            var health = metrics.CalculateHealth();
            await _cache.SetStringAsync(
                $"channel_health_{channel}",
                JsonSerializer.Serialize(health),
                new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(1)
                });
        }
    }
}

public class ChannelMetrics
{
    private readonly Queue<MetricSample> _samples;
    private readonly object _lock = new();
    private const int MaxSamples = 100;

    public ChannelMetrics(ChannelMetricSnapshot initial)
    {
        _samples = new Queue<MetricSample>();
        Update(initial);
    }

    public ChannelMetrics Update(ChannelMetricSnapshot latest)
    {
        lock (_lock)
        {
            _samples.Enqueue(new MetricSample
            {
                Timestamp = DateTime.UtcNow,
                ProcessingRate = latest.ProcessingRate,
                ErrorRate = latest.ErrorRate,
                Latency = latest.AverageLatency
            });

            while (_samples.Count > MaxSamples)
                _samples.Dequeue();
        }

        return this;
    }

    public List<string> DetectAnomalies()
    {
        var anomalies = new List<string>();
        var samples = GetRecentSamples(TimeSpan.FromMinutes(5));

        if (!samples.Any()) return anomalies;

        var avgProcessingRate = samples.Average(s => s.ProcessingRate);
        var avgErrorRate = samples.Average(s => s.ErrorRate);
        var avgLatency = samples.Average(s => s.Latency.TotalMilliseconds);

        var latestSample = samples.Last();

        // Processing rate dropped significantly
        if (latestSample.ProcessingRate < avgProcessingRate * 0.5)
            anomalies.Add("ProcessingRateDrop");

        // Error rate spike
        if (latestSample.ErrorRate > avgErrorRate * 2 && 
            latestSample.ErrorRate > 0.1)
            anomalies.Add("ErrorRateSpike");

        // Latency spike
        if (latestSample.Latency.TotalMilliseconds > avgLatency * 2 && 
            latestSample.Latency.TotalMilliseconds > 1000)
            anomalies.Add("LatencySpike");

        return anomalies;
    }

    public ChannelHealth CalculateHealth()
    {
        var samples = GetRecentSamples(TimeSpan.FromMinutes(5));
        if (!samples.Any())
            return new ChannelHealth { Status = HealthStatus.Unknown };

        var errorRate = samples.Average(s => s.ErrorRate);
        var latency = samples.Average(s => s.Latency.TotalMilliseconds);
        var processingRate = samples.Average(s => s.ProcessingRate);

        var health = new ChannelHealth
        {
            ErrorRate = errorRate,
            AverageLatency = TimeSpan.FromMilliseconds(latency),
            ProcessingRate = processingRate
        };

        health.Status = (errorRate, latency) switch
        {
            ( > 0.25, _) => HealthStatus.Critical,
            ( > 0.1, _) => HealthStatus.Unhealthy,
            (_, > 5000) => HealthStatus.Unhealthy,
            (_, > 1000) => HealthStatus.Degraded,
            ( < 0.01, < 500) => HealthStatus.Healthy,
            _ => HealthStatus.Degraded
        };

        return health;
    }

    private List<MetricSample> GetRecentSamples(TimeSpan window)
    {
        var cutoff = DateTime.UtcNow - window;
        lock (_lock)
        {
            return _samples
                .Where(s => s.Timestamp >= cutoff)
                .ToList();
        }
    }
}

public class MetricSample
{
    public DateTime Timestamp { get; set; }
    public double ProcessingRate { get; set; }
    public double ErrorRate { get; set; }
    public TimeSpan Latency { get; set; }
}

public class ChannelHealth
{
    public HealthStatus Status { get; set; }
    public double ErrorRate { get; set; }
    public TimeSpan AverageLatency { get; set; }
    public double ProcessingRate { get; set; }
}

public enum HealthStatus
{
    Unknown,
    Healthy,
    Degraded,
    Unhealthy,
    Critical
} 