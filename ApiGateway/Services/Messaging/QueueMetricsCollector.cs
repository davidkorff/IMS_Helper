using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Metrics;

public class QueueMetricsCollector : BackgroundService
{
    private readonly IMessageQueueService _queueService;
    private readonly IMetricsRoot _metrics;
    private readonly ILogger<QueueMetricsCollector> _logger;
    private readonly Dictionary<string, QueueConfiguration> _queueConfigs;

    private readonly Counter<long> _messagePublishedCounter;
    private readonly Counter<long> _messageConsumedCounter;
    private readonly Counter<long> _messageFailedCounter;
    private readonly Counter<long> _messageRetriedCounter;
    private readonly Gauge<long> _queueDepthGauge;
    private readonly Histogram<double> _messageProcessingDuration;

    public QueueMetricsCollector(
        IMessageQueueService queueService,
        IMetricsRoot metrics,
        IConfiguration configuration,
        ILogger<QueueMetricsCollector> logger)
    {
        _queueService = queueService;
        _metrics = metrics;
        _logger = logger;
        _queueConfigs = configuration
            .GetSection("MessageQueue:Queues")
            .Get<Dictionary<string, QueueConfiguration>>();

        var factory = metrics.Meter("MessageQueue");

        _messagePublishedCounter = factory.CreateCounter<long>(
            "messages_published_total",
            "Total number of messages published");
        
        _messageConsumedCounter = factory.CreateCounter<long>(
            "messages_consumed_total",
            "Total number of messages consumed");
        
        _messageFailedCounter = factory.CreateCounter<long>(
            "messages_failed_total",
            "Total number of messages that failed processing");
        
        _messageRetriedCounter = factory.CreateCounter<long>(
            "messages_retried_total",
            "Total number of messages that were retried");
        
        _queueDepthGauge = factory.CreateGauge<long>(
            "queue_depth",
            "Current number of messages in queue");
        
        _messageProcessingDuration = factory.CreateHistogram<double>(
            "message_processing_duration_seconds",
            "Message processing duration");
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CollectMetricsAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error collecting queue metrics");
            }

            await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken);
        }
    }

    private async Task CollectMetricsAsync()
    {
        foreach (var (queueName, config) in _queueConfigs)
        {
            try
            {
                var queueStats = await GetQueueStatisticsAsync(queueName);
                
                _queueDepthGauge.Set(queueStats.MessageCount, 
                    new KeyValuePair<string, object>("queue", queueName));

                if (config.EnableDeadLetterQueue)
                {
                    var dlqStats = await GetQueueStatisticsAsync($"{queueName}.dlq");
                    _queueDepthGauge.Set(dlqStats.MessageCount,
                        new KeyValuePair<string, object>("queue", $"{queueName}.dlq"));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, 
                    "Error collecting metrics for queue {QueueName}", queueName);
            }
        }
    }

    private async Task<QueueStatistics> GetQueueStatisticsAsync(string queueName)
    {
        // Implementation depends on the specific message queue system
        // This is a placeholder for the actual implementation
        return new QueueStatistics
        {
            MessageCount = 0,
            ConsumerCount = 0
        };
    }

    public void RecordMessagePublished(string queueName)
    {
        _messagePublishedCounter.Add(1, 
            new KeyValuePair<string, object>("queue", queueName));
    }

    public void RecordMessageConsumed(string queueName)
    {
        _messageConsumedCounter.Add(1, 
            new KeyValuePair<string, object>("queue", queueName));
    }

    public void RecordMessageFailed(string queueName)
    {
        _messageFailedCounter.Add(1, 
            new KeyValuePair<string, object>("queue", queueName));
    }

    public void RecordMessageRetried(string queueName)
    {
        _messageRetriedCounter.Add(1, 
            new KeyValuePair<string, object>("queue", queueName));
    }

    public IDisposable MeasureProcessingDuration(string queueName)
    {
        return _messageProcessingDuration.NewTimer(
            new KeyValuePair<string, object>("queue", queueName));
    }
}

public class QueueStatistics
{
    public long MessageCount { get; set; }
    public int ConsumerCount { get; set; }
} 