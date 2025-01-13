using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

public class QueuePerformanceOptimizer : BackgroundService
{
    private readonly IMessageQueueService _queueService;
    private readonly QueueMetricsCollector _metricsCollector;
    private readonly ILogger<QueuePerformanceOptimizer> _logger;
    private readonly Dictionary<string, QueueConfiguration> _queueConfigs;
    private readonly ConcurrentDictionary<string, PerformanceStats> _queueStats;

    public QueuePerformanceOptimizer(
        IMessageQueueService queueService,
        QueueMetricsCollector metricsCollector,
        IConfiguration configuration,
        ILogger<QueuePerformanceOptimizer> logger)
    {
        _queueService = queueService;
        _metricsCollector = metricsCollector;
        _logger = logger;
        _queueConfigs = configuration
            .GetSection("MessageQueue:Queues")
            .Get<Dictionary<string, QueueConfiguration>>();
        _queueStats = new ConcurrentDictionary<string, PerformanceStats>();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            foreach (var (queueName, config) in _queueConfigs)
            {
                try
                {
                    await OptimizeQueuePerformanceAsync(queueName, config);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, 
                        "Error optimizing performance for queue {QueueName}", queueName);
                }
            }

            await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
        }
    }

    private async Task OptimizeQueuePerformanceAsync(
        string queueName, 
        QueueConfiguration config)
    {
        var metrics = await _metricsCollector.GetQueueMetricsAsync(queueName);
        var stats = _queueStats.GetOrAdd(queueName, _ => new PerformanceStats());

        // Update performance statistics
        stats.UpdateMetrics(metrics);

        // Optimize based on message throughput
        if (stats.AverageThroughput > config.HighThroughputThreshold)
        {
            await OptimizeForHighThroughputAsync(queueName, config, stats);
        }
        else if (stats.AverageThroughput < config.LowThroughputThreshold)
        {
            await OptimizeForLowThroughputAsync(queueName, config, stats);
        }

        // Optimize based on message size
        if (stats.AverageMessageSize > config.LargeMessageThreshold)
        {
            await OptimizeForLargeMessagesAsync(queueName, config, stats);
        }

        // Optimize based on processing time
        if (stats.AverageProcessingTime > config.HighLatencyThreshold)
        {
            await OptimizeForHighLatencyAsync(queueName, config, stats);
        }

        // Update prefetch count based on consumer performance
        await OptimizePrefetchCountAsync(queueName, config, stats);
    }

    private async Task OptimizeForHighThroughputAsync(
        string queueName, 
        QueueConfiguration config, 
        PerformanceStats stats)
    {
        var newBatchSize = CalculateOptimalBatchSize(stats.AverageThroughput, stats.ConsumerCount);
        await _queueService.UpdateQueueConfigurationAsync(queueName, new QueueUpdateOptions
        {
            BatchSize = newBatchSize,
            EnableBatching = true,
            PrefetchCount = newBatchSize * 2
        });

        _logger.LogInformation(
            "Optimized {QueueName} for high throughput: Batch size = {BatchSize}", 
            queueName, newBatchSize);
    }

    private async Task OptimizeForLowThroughputAsync(
        string queueName, 
        QueueConfiguration config, 
        PerformanceStats stats)
    {
        await _queueService.UpdateQueueConfigurationAsync(queueName, new QueueUpdateOptions
        {
            EnableBatching = false,
            PrefetchCount = 1,
            IdleTimeout = TimeSpan.FromMinutes(5)
        });

        _logger.LogInformation(
            "Optimized {QueueName} for low throughput", queueName);
    }

    private async Task OptimizeForLargeMessagesAsync(
        string queueName, 
        QueueConfiguration config, 
        PerformanceStats stats)
    {
        await _queueService.UpdateQueueConfigurationAsync(queueName, new QueueUpdateOptions
        {
            EnableCompression = true,
            BatchSize = Math.Max(1, config.DefaultBatchSize / 2),
            PrefetchCount = Math.Max(1, config.DefaultPrefetchCount / 2)
        });

        _logger.LogInformation(
            "Optimized {QueueName} for large messages", queueName);
    }

    private async Task OptimizeForHighLatencyAsync(
        string queueName, 
        QueueConfiguration config, 
        PerformanceStats stats)
    {
        var newConcurrency = CalculateOptimalConcurrency(
            stats.AverageProcessingTime, 
            stats.ConsumerCount);

        await _queueService.UpdateQueueConfigurationAsync(queueName, new QueueUpdateOptions
        {
            MaxConcurrency = newConcurrency,
            EnableBatching = false,
            PrefetchCount = newConcurrency * 2
        });

        _logger.LogInformation(
            "Optimized {QueueName} for high latency: Concurrency = {Concurrency}", 
            queueName, newConcurrency);
    }

    private async Task OptimizePrefetchCountAsync(
        string queueName, 
        QueueConfiguration config, 
        PerformanceStats stats)
    {
        var optimalPrefetch = CalculateOptimalPrefetchCount(
            stats.AverageProcessingTime,
            stats.AverageThroughput,
            stats.ConsumerCount);

        await _queueService.UpdateQueueConfigurationAsync(queueName, new QueueUpdateOptions
        {
            PrefetchCount = optimalPrefetch
        });

        _logger.LogInformation(
            "Updated prefetch count for {QueueName}: {PrefetchCount}", 
            queueName, optimalPrefetch);
    }

    private int CalculateOptimalBatchSize(double throughput, int consumerCount)
    {
        // Calculate batch size based on throughput and consumer count
        var baseSize = (int)Math.Ceiling(throughput / consumerCount / 10);
        return Math.Min(Math.Max(baseSize, 1), 100); // Keep between 1 and 100
    }

    private int CalculateOptimalConcurrency(TimeSpan processingTime, int consumerCount)
    {
        // Calculate concurrency based on processing time
        var baseConcurrency = (int)Math.Ceiling(
            processingTime.TotalMilliseconds / 100 * consumerCount);
        return Math.Min(Math.Max(baseConcurrency, 1), 20); // Keep between 1 and 20
    }

    private int CalculateOptimalPrefetchCount(
        TimeSpan processingTime, 
        double throughput, 
        int consumerCount)
    {
        // Calculate prefetch count based on processing time and throughput
        var messagesPerSecond = throughput / consumerCount;
        var processingTimeSeconds = processingTime.TotalSeconds;
        var basePrefetch = (int)Math.Ceiling(messagesPerSecond * processingTimeSeconds * 1.5);
        return Math.Min(Math.Max(basePrefetch, 1), 1000); // Keep between 1 and 1000
    }
}

public class PerformanceStats
{
    private readonly Queue<MetricSample> _samples = new(30); // Keep last 30 samples
    private readonly object _lock = new();

    public double AverageThroughput { get; private set; }
    public double AverageProcessingTime { get; private set; }
    public double AverageMessageSize { get; private set; }
    public int ConsumerCount { get; private set; }

    public void UpdateMetrics(QueueMetrics metrics)
    {
        lock (_lock)
        {
            _samples.Enqueue(new MetricSample
            {
                Timestamp = DateTime.UtcNow,
                Throughput = metrics.MessageCount,
                ProcessingTime = metrics.AverageProcessingTime,
                MessageSize = metrics.AverageMessageSize,
                ConsumerCount = metrics.ConsumerCount
            });

            if (_samples.Count > 30)
                _samples.Dequeue();

            // Calculate averages
            AverageThroughput = _samples.Average(s => s.Throughput);
            AverageProcessingTime = _samples.Average(s => s.ProcessingTime);
            AverageMessageSize = _samples.Average(s => s.MessageSize);
            ConsumerCount = metrics.ConsumerCount;
        }
    }

    private class MetricSample
    {
        public DateTime Timestamp { get; set; }
        public double Throughput { get; set; }
        public double ProcessingTime { get; set; }
        public double MessageSize { get; set; }
        public int ConsumerCount { get; set; }
    }
} 