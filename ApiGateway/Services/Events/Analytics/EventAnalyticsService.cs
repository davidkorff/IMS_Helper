public interface IEventAnalyticsService
{
    Task<EventAnalytics> GetEventAnalyticsAsync(
        DateTime start, 
        DateTime end, 
        string eventType = null);
    Task<List<EventTrend>> GetEventTrendsAsync(
        DateTime start, 
        DateTime end, 
        string eventType = null);
    Task<List<EventCorrelation>> FindEventCorrelationsAsync(
        string eventType, 
        DateTime start, 
        DateTime end);
}

public class EventAnalyticsService : IEventAnalyticsService
{
    private readonly IEventStore _eventStore;
    private readonly ILogger<EventAnalyticsService> _logger;

    public EventAnalyticsService(
        IEventStore eventStore,
        ILogger<EventAnalyticsService> logger)
    {
        _eventStore = eventStore;
        _logger = logger;
    }

    public async Task<EventAnalytics> GetEventAnalyticsAsync(
        DateTime start, 
        DateTime end, 
        string eventType = null)
    {
        try
        {
            var events = await _eventStore.GetEventsInRangeAsync(start, end, eventType);
            var analytics = new EventAnalytics
            {
                Period = new DateRange { Start = start, End = end },
                TotalEvents = events.Count,
                EventTypeDistribution = events
                    .GroupBy(e => e.Type)
                    .ToDictionary(g => g.Key, g => g.Count()),
                SourceDistribution = events
                    .GroupBy(e => e.Source)
                    .ToDictionary(g => g.Key, g => g.Count()),
                HourlyDistribution = events
                    .GroupBy(e => e.Timestamp.Hour)
                    .OrderBy(g => g.Key)
                    .ToDictionary(g => g.Key, g => g.Count()),
                AverageProcessingTime = TimeSpan.FromMilliseconds(
                    events.Average(e => 
                        GetProcessingTime(e).TotalMilliseconds)),
                ErrorRate = events
                    .Count(e => IsError(e)) / (double)events.Count
            };

            return analytics;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating event analytics");
            throw;
        }
    }

    public async Task<List<EventTrend>> GetEventTrendsAsync(
        DateTime start, 
        DateTime end, 
        string eventType = null)
    {
        try
        {
            var events = await _eventStore.GetEventsInRangeAsync(start, end, eventType);
            var intervalMinutes = CalculateOptimalInterval(start, end);
            
            return events
                .GroupBy(e => RoundToInterval(e.Timestamp, intervalMinutes))
                .OrderBy(g => g.Key)
                .Select(g => new EventTrend
                {
                    Timestamp = g.Key,
                    Count = g.Count(),
                    ErrorCount = g.Count(IsError),
                    AverageProcessingTime = TimeSpan.FromMilliseconds(
                        g.Average(e => GetProcessingTime(e).TotalMilliseconds))
                })
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating event trends");
            throw;
        }
    }

    public async Task<List<EventCorrelation>> FindEventCorrelationsAsync(
        string eventType, 
        DateTime start, 
        DateTime end)
    {
        try
        {
            var events = await _eventStore.GetEventsInRangeAsync(start, end);
            var correlations = new List<EventCorrelation>();
            var targetEvents = events.Where(e => e.Type == eventType).ToList();

            foreach (var targetEvent in targetEvents)
            {
                var relatedEvents = events
                    .Where(e => e.CorrelationId == targetEvent.CorrelationId &&
                               e.Type != eventType)
                    .OrderBy(e => e.Timestamp)
                    .ToList();

                if (relatedEvents.Any())
                {
                    correlations.Add(new EventCorrelation
                    {
                        SourceEventId = targetEvent.Id,
                        SourceEventType = eventType,
                        RelatedEvents = relatedEvents
                            .Select(e => new RelatedEvent
                            {
                                EventId = e.Id,
                                EventType = e.Type,
                                TimeDifference = e.Timestamp - targetEvent.Timestamp
                            })
                            .ToList()
                    });
                }
            }

            return correlations;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error finding event correlations");
            throw;
        }
    }

    private static TimeSpan GetProcessingTime(IntegrationEvent @event)
    {
        if (@event.Metadata.TryGetValue("processing_time", out var timeStr) &&
            double.TryParse(timeStr, out var milliseconds))
        {
            return TimeSpan.FromMilliseconds(milliseconds);
        }
        return TimeSpan.Zero;
    }

    private static bool IsError(IntegrationEvent @event)
    {
        return @event.Metadata.TryGetValue("status", out var status) &&
               status.Equals("error", StringComparison.OrdinalIgnoreCase);
    }

    private static DateTime RoundToInterval(DateTime time, int intervalMinutes)
    {
        return new DateTime(
            time.Year, time.Month, time.Day, 
            time.Hour, time.Minute - (time.Minute % intervalMinutes), 0);
    }

    private static int CalculateOptimalInterval(DateTime start, DateTime end)
    {
        var totalMinutes = (end - start).TotalMinutes;
        if (totalMinutes <= 60) return 1;
        if (totalMinutes <= 180) return 5;
        if (totalMinutes <= 720) return 15;
        if (totalMinutes <= 1440) return 30;
        return 60;
    }
} 