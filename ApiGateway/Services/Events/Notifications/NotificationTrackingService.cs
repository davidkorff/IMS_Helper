public interface INotificationTrackingService
{
    Task TrackDeliveryAttemptAsync(NotificationDeliveryAttempt attempt);
    Task<List<NotificationDeliveryAttempt>> GetDeliveryAttemptsAsync(string notificationId);
    Task<NotificationDeliveryStats> GetDeliveryStatsAsync(
        DateTime start, 
        DateTime end, 
        string channelType = null);
    Task<bool> IsDeliveredAsync(string notificationId);
}

public class NotificationTrackingService : INotificationTrackingService
{
    private readonly ILogger<NotificationTrackingService> _logger;
    private readonly ApplicationDbContext _context;
    private readonly IMetricsCollector _metrics;
    private readonly ConcurrentDictionary<string, NotificationDeliveryStatus> _deliveryStatus;

    public NotificationTrackingService(
        ILogger<NotificationTrackingService> logger,
        ApplicationDbContext context,
        IMetricsCollector metrics)
    {
        _logger = logger;
        _context = context;
        _metrics = metrics;
        _deliveryStatus = new ConcurrentDictionary<string, NotificationDeliveryStatus>();
    }

    public async Task TrackDeliveryAttemptAsync(NotificationDeliveryAttempt attempt)
    {
        try
        {
            // Record attempt in database
            _context.NotificationDeliveryAttempts.Add(attempt);
            await _context.SaveChangesAsync();

            // Update in-memory status
            _deliveryStatus.AddOrUpdate(
                attempt.NotificationId,
                _ => new NotificationDeliveryStatus
                {
                    LastAttempt = attempt.Timestamp,
                    IsDelivered = attempt.Success,
                    Attempts = 1
                },
                (_, status) =>
                {
                    status.LastAttempt = attempt.Timestamp;
                    status.IsDelivered |= attempt.Success;
                    status.Attempts++;
                    return status;
                });

            // Record metrics
            RecordDeliveryMetrics(attempt);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, 
                "Error tracking delivery attempt for notification {NotificationId}", 
                attempt.NotificationId);
            throw;
        }
    }

    public async Task<List<NotificationDeliveryAttempt>> GetDeliveryAttemptsAsync(
        string notificationId)
    {
        return await _context.NotificationDeliveryAttempts
            .Where(a => a.NotificationId == notificationId)
            .OrderByDescending(a => a.Timestamp)
            .ToListAsync();
    }

    public async Task<NotificationDeliveryStats> GetDeliveryStatsAsync(
        DateTime start,
        DateTime end,
        string channelType = null)
    {
        var query = _context.NotificationDeliveryAttempts
            .Where(a => a.Timestamp >= start && a.Timestamp <= end);

        if (!string.IsNullOrEmpty(channelType))
        {
            query = query.Where(a => a.ChannelType == channelType);
        }

        var attempts = await query.ToListAsync();

        return new NotificationDeliveryStats
        {
            Period = new DateRange { Start = start, End = end },
            TotalAttempts = attempts.Count,
            SuccessfulDeliveries = attempts.Count(a => a.Success),
            FailedDeliveries = attempts.Count(a => !a.Success),
            AverageLatency = TimeSpan.FromMilliseconds(
                attempts.Average(a => a.DeliveryLatency?.TotalMilliseconds ?? 0)),
            DeliveryRateByChannel = attempts
                .GroupBy(a => a.ChannelType)
                .ToDictionary(
                    g => g.Key,
                    g => g.Count(a => a.Success) / (double)g.Count())
        };
    }

    public Task<bool> IsDeliveredAsync(string notificationId)
    {
        return Task.FromResult(
            _deliveryStatus.TryGetValue(notificationId, out var status) && 
            status.IsDelivered);
    }

    private void RecordDeliveryMetrics(NotificationDeliveryAttempt attempt)
    {
        var tags = new Dictionary<string, string>
        {
            ["channel"] = attempt.ChannelType,
            ["success"] = attempt.Success.ToString()
        };

        _metrics.Increment("notification_delivery_attempts", 1, tags);

        if (attempt.DeliveryLatency.HasValue)
        {
            _metrics.RecordHistogram(
                "notification_delivery_latency",
                attempt.DeliveryLatency.Value.TotalMilliseconds,
                tags);
        }

        if (!attempt.Success)
        {
            _metrics.Increment("notification_delivery_failures", 1, tags);
        }
    }
}

public class NotificationDeliveryStatus
{
    public DateTime LastAttempt { get; set; }
    public bool IsDelivered { get; set; }
    public int Attempts { get; set; }
}

public class NotificationDeliveryStats
{
    public DateRange Period { get; set; }
    public int TotalAttempts { get; set; }
    public int SuccessfulDeliveries { get; set; }
    public int FailedDeliveries { get; set; }
    public TimeSpan AverageLatency { get; set; }
    public Dictionary<string, double> DeliveryRateByChannel { get; set; }
} 