using System;
using System.Diagnostics;
using Prometheus;

public static class IMSLoadTestMetrics
{
    // System Metrics
    private static readonly Gauge CpuUsage = Metrics
        .CreateGauge("ims_cpu_usage_percent", "CPU usage percentage");
    
    private static readonly Gauge MemoryUsage = Metrics
        .CreateGauge("ims_memory_usage_bytes", "Memory usage in bytes");
    
    private static readonly Gauge ThreadCount = Metrics
        .CreateGauge("ims_thread_count", "Number of active threads");

    // Business Metrics
    private static readonly Counter QuotesCreated = Metrics
        .CreateCounter("ims_quotes_created_total", "Total quotes created",
            new CounterConfiguration { LabelNames = new[] { "coverage_type" } });
    
    private static readonly Counter PoliciesIssued = Metrics
        .CreateCounter("ims_policies_issued_total", "Total policies issued",
            new CounterConfiguration { LabelNames = new[] { "payment_plan" } });
    
    private static readonly Histogram PremiumAmount = Metrics
        .CreateHistogram("ims_premium_amount_dollars", 
            "Distribution of premium amounts",
            new HistogramConfiguration
            {
                LabelNames = new[] { "coverage_type" },
                Buckets = new[] { 100, 500, 1000, 5000, 10000, 50000 }
            });

    // Performance Metrics
    private static readonly Gauge ApiLatency = Metrics
        .CreateGauge("ims_api_latency_ms", "API latency in milliseconds",
            new GaugeConfiguration { LabelNames = new[] { "endpoint", "method" } });
    
    private static readonly Counter HttpErrors = Metrics
        .CreateCounter("ims_http_errors_total", "Total HTTP errors",
            new CounterConfiguration { LabelNames = new[] { "endpoint", "status_code" } });
    
    private static readonly Gauge ConcurrentRequests = Metrics
        .CreateGauge("ims_concurrent_requests", "Number of concurrent requests",
            new GaugeConfiguration { LabelNames = new[] { "endpoint" } });

    // Business SLA Metrics
    private static readonly Histogram QuoteResponseTime = Metrics
        .CreateHistogram("ims_quote_response_time_seconds",
            "Time to generate quotes",
            new HistogramConfiguration
            {
                LabelNames = new[] { "complexity" },
                Buckets = new[] { 0.1, 0.5, 1, 2, 5, 10 }
            });

    public static void UpdateSystemMetrics()
    {
        var process = Process.GetCurrentProcess();
        CpuUsage.Set(GetCpuUsage());
        MemoryUsage.Set(process.WorkingSet64);
        ThreadCount.Set(process.Threads.Count);
    }

    private static double GetCpuUsage()
    {
        // Implementation for CPU usage calculation
        return 0.0; // Placeholder
    }

    // ... additional metric update methods
} 