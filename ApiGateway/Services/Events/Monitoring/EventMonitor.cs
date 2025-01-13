using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

public class EventMonitor : BackgroundService
{
    private readonly IEventStore _eventStore;
    private readonly IMetricsCollector _metrics;
    private readonly ILogger<EventMonitor> _logger;
    private readonly ConcurrentDictionary<string, EventMetrics> _eventMetrics;
    private readonly TimeSpan _aggregationInterval = TimeSpan.FromMinutes(1);

    public EventMonitor(
        IEventStore eventStore,
        IMetricsCollector metrics,
        ILogger<EventMonitor> logger)
    {
        _eventStore = eventStore;
        _metrics = metrics;
        _logger = logger;
        _eventMetrics = new ConcurrentDictionary<string, EventMetrics>();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CollectMetricsAsync();
                await Task.Delay(_aggregationInterval, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error collecting event metrics");
                await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
            }
        }
    }

    private async Task CollectMetricsAsync()
    {
        var now = DateTime.UtcNow;
        var metrics = _eventMetrics.ToList();
        
        foreach (var (eventType, eventMetrics) in metrics)
        {
            try
            {
                // Record throughput
                _metrics.RecordGauge(
                    "event_throughput",
                    eventMetrics.GetThroughput(),
                    new Dictionary<string, string> { ["event_type"] = eventType });

                // Record latency
                _metrics.RecordHistogram(
                    "event_processing_duration",
                    eventMetrics.GetAverageLatency().TotalMilliseconds,
                    new Dictionary<string, string> { ["event_type"] = eventType });

                // Record error rate
                _metrics.RecordGauge(
                    "event_error_rate",
                    eventMetrics.GetErrorRate(),
                    new Dictionary<string, string> { ["event_type"] = eventType });

                // Record subscription metrics
                foreach (var sub in eventMetrics.GetSubscriptionMetrics())
                {
                    _metrics.RecordGauge(
                        "subscription_delivery_rate",
                        sub.DeliveryRate,
                        new Dictionary<string, string>
                        {
                            ["event_type"] = eventType,
                            ["subscription_id"] = sub.SubscriptionId
                        });
                }

                // Reset metrics after recording
                eventMetrics.Reset();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, 
                    "Error processing metrics for event type {EventType}", eventType);
            }
        }
    }

    public void RecordEventProcessed(
        string eventType, 
        TimeSpan processingTime, 
        bool success)
    {
        var metrics = _eventMetrics.GetOrAdd(
            eventType, 
            _ => new EventMetrics());

        metrics.RecordEvent(processingTime, success);
    }

    public void RecordSubscriptionDelivery(
        string eventType, 
        string subscriptionId, 
        bool success)
    {
        var metrics = _eventMetrics.GetOrAdd(
            eventType, 
            _ => new EventMetrics());

        metrics.RecordSubscriptionDelivery(subscriptionId, success);
    }
}

public class EventMetrics
{
    private readonly object _lock = new();
    private long _totalEvents;
    private long _failedEvents;
    private readonly ConcurrentDictionary<string, SubscriptionMetrics> _subscriptionMetrics;
    private readonly List<double> _processingTimes;
    private DateTime _lastReset;

    public EventMetrics()
    {
        _subscriptionMetrics = new ConcurrentDictionary<string, SubscriptionMetrics>();
        _processingTimes = new List<double>();
        _lastReset = DateTime.UtcNow;
    }

    public void RecordEvent(TimeSpan processingTime, bool success)
    {
        lock (_lock)
        {
            _totalEvents++;
            if (!success) _failedEvents++;
            _processingTimes.Add(processingTime.TotalMilliseconds);
        }
    }

    public void RecordSubscriptionDelivery(string subscriptionId, bool success)
    {
        var metrics = _subscriptionMetrics.GetOrAdd(
            subscriptionId, 
            _ => new SubscriptionMetrics());

        metrics.RecordDelivery(success);
    }

    public double GetThroughput()
    {
        var duration = DateTime.UtcNow - _lastReset;
        return _totalEvents / duration.TotalSeconds;
    }

    public TimeSpan GetAverageLatency()
    {
        lock (_lock)
        {
            if (_processingTimes.Count == 0) return TimeSpan.Zero;
            return TimeSpan.FromMilliseconds(_processingTimes.Average());
        }
    }

    public double GetErrorRate()
    {
        return _totalEvents == 0 ? 0 : (double)_failedEvents / _totalEvents;
    }

    public IEnumerable<(string SubscriptionId, double DeliveryRate)> GetSubscriptionMetrics()
    {
        return _subscriptionMetrics.Select(kvp => 
            (kvp.Key, kvp.Value.GetDeliveryRate()));
    }

    public void Reset()
    {
        lock (_lock)
        {
            _totalEvents = 0;
            _failedEvents = 0;
            _processingTimes.Clear();
            _lastReset = DateTime.UtcNow;

            foreach (var metrics in _subscriptionMetrics.Values)
            {
                metrics.Reset();
            }
        }
    }
}

public class SubscriptionMetrics
{
    private long _totalDeliveries;
    private long _successfulDeliveries;
    private readonly object _lock = new();

    public void RecordDelivery(bool success)
    {
        lock (_lock)
        {
            _totalDeliveries++;
            if (success) _successfulDeliveries++;
        }
    }

    public double GetDeliveryRate()
    {
        return _totalDeliveries == 0 ? 0 : (double)_successfulDeliveries / _totalDeliveries;
    }

    public void Reset()
    {
        lock (_lock)
        {
            _totalDeliveries = 0;
            _successfulDeliveries = 0;
        }
    }
} 