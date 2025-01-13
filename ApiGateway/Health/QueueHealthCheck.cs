using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

public class QueueHealthCheck : IHealthCheck
{
    private readonly IMessageQueueService _queueService;
    private readonly QueueMetricsCollector _metricsCollector;
    private readonly ILogger<QueueHealthCheck> _logger;
    private readonly Dictionary<string, QueueConfiguration> _queueConfigs;

    public QueueHealthCheck(
        IMessageQueueService queueService,
        QueueMetricsCollector metricsCollector,
        IConfiguration configuration,
        ILogger<QueueHealthCheck> logger)
    {
        _queueService = queueService;
        _metricsCollector = metricsCollector;
        _logger = logger;
        _queueConfigs = configuration
            .GetSection("MessageQueue:Queues")
            .Get<Dictionary<string, QueueConfiguration>>();
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, 
        CancellationToken cancellationToken = default)
    {
        var data = new Dictionary<string, object>();
        var isHealthy = true;
        var errors = new List<string>();

        foreach (var (queueName, config) in _queueConfigs)
        {
            try
            {
                var queueHealth = await CheckQueueHealthAsync(queueName, config);
                data[queueName] = queueHealth;
                
                if (!queueHealth.IsHealthy)
                {
                    isHealthy = false;
                    errors.Add($"Queue {queueName}: {queueHealth.Status}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking health for queue {QueueName}", queueName);
                isHealthy = false;
                errors.Add($"Queue {queueName}: {ex.Message}");
            }
        }

        return isHealthy
            ? HealthCheckResult.Healthy("All queues are healthy", data)
            : HealthCheckResult.Unhealthy($"Queue issues detected: {string.Join(", ", errors)}", null, data);
    }

    private async Task<QueueHealth> CheckQueueHealthAsync(
        string queueName, 
        QueueConfiguration config)
    {
        var metrics = await _metricsCollector.GetQueueMetricsAsync(queueName);
        var health = new QueueHealth
        {
            QueueName = queueName,
            MessageCount = metrics.MessageCount,
            ConsumerCount = metrics.ConsumerCount,
            ErrorRate = metrics.ErrorRate,
            ProcessingLatency = metrics.AverageProcessingTime
        };

        // Check for queue health issues
        if (metrics.ConsumerCount == 0)
        {
            health.IsHealthy = false;
            health.Status = "No active consumers";
        }
        else if (metrics.ErrorRate > 0.1) // 10% error rate threshold
        {
            health.IsHealthy = false;
            health.Status = $"High error rate: {metrics.ErrorRate:P}";
        }
        else if (metrics.AverageProcessingTime > TimeSpan.FromSeconds(30))
        {
            health.IsHealthy = false;
            health.Status = "High processing latency";
        }
        else
        {
            health.IsHealthy = true;
            health.Status = "Healthy";
        }

        return health;
    }
}

public class QueueHealth
{
    public string QueueName { get; set; }
    public bool IsHealthy { get; set; }
    public string Status { get; set; }
    public long MessageCount { get; set; }
    public int ConsumerCount { get; set; }
    public double ErrorRate { get; set; }
    public TimeSpan ProcessingLatency { get; set; }
} 