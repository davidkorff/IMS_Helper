using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

public class NotificationPriorityQueue : BackgroundService
{
    private readonly ILogger<NotificationPriorityQueue> _logger;
    private readonly INotificationService _notificationService;
    private readonly INotificationTrackingService _trackingService;
    private readonly IMetricsCollector _metrics;
    private readonly PriorityQueue<QueuedNotification, int> _queue;
    private readonly ConcurrentDictionary<string, RetryPolicy> _retryPolicies;
    private readonly SemaphoreSlim _processingSemaphore;
    private const int MaxConcurrentProcessing = 10;

    public NotificationPriorityQueue(
        ILogger<NotificationPriorityQueue> logger,
        INotificationService notificationService,
        INotificationTrackingService trackingService,
        IMetricsCollector metrics)
    {
        _logger = logger;
        _notificationService = notificationService;
        _trackingService = trackingService;
        _metrics = metrics;
        _queue = new PriorityQueue<QueuedNotification, int>();
        _retryPolicies = new ConcurrentDictionary<string, RetryPolicy>();
        _processingSemaphore = new SemaphoreSlim(MaxConcurrentProcessing);
    }

    public async Task EnqueueAsync(QueuedNotification notification)
    {
        var priority = CalculatePriority(notification);
        
        lock (_queue)
        {
            _queue.Enqueue(notification, priority);
        }

        _metrics.Increment("notification_queued", 1, new Dictionary<string, string>
        {
            ["priority"] = priority.ToString(),
            ["channel"] = notification.Channel.Type.ToString()
        });

        _logger.LogInformation(
            "Notification {Id} enqueued with priority {Priority}", 
            notification.Id, priority);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var processingTasks = new List<Task>();

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await _processingSemaphore.WaitAsync(stoppingToken);

                QueuedNotification notification;
                lock (_queue)
                {
                    if (!_queue.TryDequeue(out notification, out var priority))
                    {
                        _processingSemaphore.Release();
                        await Task.Delay(100, stoppingToken);
                        continue;
                    }
                }

                var processTask = ProcessNotificationAsync(notification)
                    .ContinueWith(_ => _processingSemaphore.Release());
                
                processingTasks.Add(processTask);

                // Clean up completed tasks
                processingTasks.RemoveAll(t => t.IsCompleted);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Error in notification queue processing");
                await Task.Delay(1000, stoppingToken);
            }
        }

        // Wait for remaining tasks to complete
        await Task.WhenAll(processingTasks);
    }

    private async Task ProcessNotificationAsync(QueuedNotification notification)
    {
        var retryPolicy = _retryPolicies.GetOrAdd(
            notification.Channel.Type.ToString(),
            _ => CreateRetryPolicy(notification.Channel.Type));

        try
        {
            await retryPolicy.ExecuteAsync(async () =>
            {
                var sw = Stopwatch.StartNew();
                
                await _notificationService.SendNotificationAsync(
                    notification.Channel,
                    notification.Message,
                    notification.Severity);

                await _trackingService.TrackDeliveryAttemptAsync(
                    new NotificationDeliveryAttempt
                    {
                        NotificationId = notification.Id,
                        ChannelType = notification.Channel.Type.ToString(),
                        Timestamp = DateTime.UtcNow,
                        Success = true,
                        DeliveryLatency = sw.Elapsed
                    });

                _metrics.RecordHistogram(
                    "notification_processing_time",
                    sw.ElapsedMilliseconds,
                    new Dictionary<string, string>
                    {
                        ["channel"] = notification.Channel.Type.ToString()
                    });
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, 
                "Failed to process notification {Id} after all retries", 
                notification.Id);

            await _trackingService.TrackDeliveryAttemptAsync(
                new NotificationDeliveryAttempt
                {
                    NotificationId = notification.Id,
                    ChannelType = notification.Channel.Type.ToString(),
                    Timestamp = DateTime.UtcNow,
                    Success = false,
                    Error = ex.Message
                });

            if (ShouldRequeue(notification, ex))
            {
                await RequeueNotificationAsync(notification);
            }
        }
    }

    private int CalculatePriority(QueuedNotification notification)
    {
        // Lower number = higher priority
        var priority = notification.Severity switch
        {
            AlertSeverity.Critical => 0,
            AlertSeverity.High => 1,
            AlertSeverity.Medium => 2,
            AlertSeverity.Low => 3,
            _ => 4
        };

        // Adjust priority based on age
        var age = DateTime.UtcNow - notification.CreatedAt;
        if (age > TimeSpan.FromMinutes(5))
        {
            priority = Math.Max(0, priority - 1);
        }

        // Adjust priority based on retry count
        priority += notification.RetryCount;

        return priority;
    }

    private RetryPolicy CreateRetryPolicy(NotificationChannelType channelType)
    {
        return channelType switch
        {
            NotificationChannelType.Email => new RetryPolicy
            {
                MaxRetries = 3,
                DelayStrategy = attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt))
            },
            NotificationChannelType.Slack => new RetryPolicy
            {
                MaxRetries = 5,
                DelayStrategy = attempt => TimeSpan.FromSeconds(attempt * 2)
            },
            NotificationChannelType.PagerDuty => new RetryPolicy
            {
                MaxRetries = 5,
                DelayStrategy = _ => TimeSpan.FromSeconds(1)
            },
            _ => new RetryPolicy
            {
                MaxRetries = 3,
                DelayStrategy = attempt => TimeSpan.FromSeconds(attempt)
            }
        };
    }

    private bool ShouldRequeue(QueuedNotification notification, Exception ex)
    {
        if (notification.RetryCount >= 3)
            return false;

        return ex switch
        {
            HttpRequestException => true,
            TimeoutException => true,
            _ => false
        };
    }

    private async Task RequeueNotificationAsync(QueuedNotification notification)
    {
        notification.RetryCount++;
        notification.LastRetryAt = DateTime.UtcNow;
        await EnqueueAsync(notification);

        _metrics.Increment("notification_requeued", 1, new Dictionary<string, string>
        {
            ["channel"] = notification.Channel.Type.ToString(),
            ["retry_count"] = notification.RetryCount.ToString()
        });
    }
}

public class QueuedNotification
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public NotificationChannel Channel { get; set; }
    public string Message { get; set; }
    public AlertSeverity Severity { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public int RetryCount { get; set; }
    public DateTime? LastRetryAt { get; set; }
    public Dictionary<string, string> Metadata { get; set; } = new();
}

public class RetryPolicy
{
    public int MaxRetries { get; set; }
    public Func<int, TimeSpan> DelayStrategy { get; set; }

    public async Task ExecuteAsync(Func<Task> action)
    {
        var attempt = 0;
        while (true)
        {
            try
            {
                await action();
                return;
            }
            catch (Exception) when (++attempt <= MaxRetries)
            {
                await Task.Delay(DelayStrategy(attempt));
            }
        }
    }
} 