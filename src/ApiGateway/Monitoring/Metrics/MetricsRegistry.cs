using System;
using System.Diagnostics;
using System.Threading;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Metrics;

public class MetricsRegistry
{
    private readonly IMetricsRoot _metrics;
    private readonly ILogger<MetricsRegistry> _logger;

    // Request metrics
    public Counter RequestCount { get; }
    public Timer RequestDuration { get; }
    public Counter ErrorCount { get; }
    public Histogram ResponseSize { get; }
    
    // Cache metrics
    public Counter CacheHits { get; }
    public Counter CacheMisses { get; }
    public Gauge CacheSize { get; }
    
    // Rate limiting metrics
    public Counter RateLimitExceeded { get; }
    public Gauge ActiveTokenBuckets { get; }
    
    // Transformation metrics
    public Timer TransformationDuration { get; }
    public Counter TransformationErrors { get; }
    
    // System metrics
    public Gauge CpuUsage { get; }
    public Gauge MemoryUsage { get; }
    public Gauge ThreadCount { get; }

    public MetricsRegistry(
        IMetricsRoot metrics,
        ILogger<MetricsRegistry> logger)
    {
        _metrics = metrics;
        _logger = logger;

        var context = new MetricTags("app", "api_gateway");

        // Initialize request metrics
        RequestCount = _metrics.CreateCounter(
            "request_total",
            "Total number of requests",
            new CounterConfiguration
            {
                LabelNames = new[] { "method", "path", "status_code" }
            });

        RequestDuration = _metrics.CreateTimer(
            "request_duration_ms",
            "Request duration in milliseconds",
            new TimerConfiguration
            {
                LabelNames = new[] { "method", "path" }
            });

        ErrorCount = _metrics.CreateCounter(
            "error_total",
            "Total number of errors",
            new CounterConfiguration
            {
                LabelNames = new[] { "error_type", "path" }
            });

        ResponseSize = _metrics.CreateHistogram(
            "response_size_bytes",
            "Response size in bytes",
            new HistogramConfiguration
            {
                LabelNames = new[] { "path" },
                Buckets = new[] { 1024, 10240, 102400, 1048576 }
            });

        // Initialize cache metrics
        CacheHits = _metrics.CreateCounter(
            "cache_hits_total",
            "Total number of cache hits");

        CacheMisses = _metrics.CreateCounter(
            "cache_misses_total",
            "Total number of cache misses");

        CacheSize = _metrics.CreateGauge(
            "cache_size_bytes",
            "Current cache size in bytes");

        // Initialize rate limiting metrics
        RateLimitExceeded = _metrics.CreateCounter(
            "rate_limit_exceeded_total",
            "Total number of rate limit violations",
            new CounterConfiguration
            {
                LabelNames = new[] { "client_id", "path" }
            });

        ActiveTokenBuckets = _metrics.CreateGauge(
            "active_token_buckets",
            "Number of active token buckets");

        // Initialize transformation metrics
        TransformationDuration = _metrics.CreateTimer(
            "transformation_duration_ms",
            "Transformation duration in milliseconds",
            new TimerConfiguration
            {
                LabelNames = new[] { "type", "path" }
            });

        TransformationErrors = _metrics.CreateCounter(
            "transformation_errors_total",
            "Total number of transformation errors",
            new CounterConfiguration
            {
                LabelNames = new[] { "type", "path" }
            });

        // Initialize system metrics
        CpuUsage = _metrics.CreateGauge(
            "cpu_usage_percent",
            "CPU usage percentage");

        MemoryUsage = _metrics.CreateGauge(
            "memory_usage_bytes",
            "Memory usage in bytes");

        ThreadCount = _metrics.CreateGauge(
            "thread_count",
            "Number of active threads");

        StartMetricsCollection();
    }

    private void StartMetricsCollection()
    {
        var timer = new Timer(CollectSystemMetrics, null, 
            TimeSpan.Zero, 
            TimeSpan.FromSeconds(15));
    }

    private void CollectSystemMetrics(object state)
    {
        try
        {
            // Update CPU usage
            using var cpuCounter = new PerformanceCounter(
                "Processor",
                "% Processor Time",
                "_Total");
            CpuUsage.Set(cpuCounter.NextValue());

            // Update memory usage
            var process = Process.GetCurrentProcess();
            MemoryUsage.Set(process.WorkingSet64);

            // Update thread count
            ThreadCount.Set(process.Threads.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error collecting system metrics");
        }
    }
} 