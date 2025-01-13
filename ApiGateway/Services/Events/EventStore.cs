public interface IEventStore
{
    Task<string> StoreEventAsync(IntegrationEvent @event);
    Task<IntegrationEvent> GetEventAsync(string eventId);
    Task<List<IntegrationEvent>> GetEventsByTypeAsync(string eventType, DateTime? since = null);
    Task<string> GetEventSchemaAsync(string eventType);
    Task<List<EventSubscription>> GetSubscriptionsAsync(string eventType);
    Task UpdateSubscriptionAsync(EventSubscription subscription);
}

public class EventStore : IEventStore
{
    private readonly ApplicationDbContext _context;
    private readonly IDistributedCache _cache;
    private readonly ILogger<EventStore> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    public EventStore(
        ApplicationDbContext context,
        IDistributedCache cache,
        ILogger<EventStore> logger)
    {
        _context = context;
        _cache = cache;
        _logger = logger;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
    }

    public async Task<string> StoreEventAsync(IntegrationEvent @event)
    {
        try
        {
            var eventEntity = new EventEntity
            {
                Id = @event.Id,
                Type = @event.Type,
                Source = @event.Source,
                Subject = @event.Subject,
                Timestamp = @event.Timestamp,
                CorrelationId = @event.CorrelationId,
                CausationId = @event.CausationId,
                Metadata = JsonSerializer.Serialize(@event.Metadata),
                Data = @event.Data.RootElement.GetRawText()
            };

            _context.Events.Add(eventEntity);
            await _context.SaveChangesAsync();

            // Invalidate cache
            await _cache.RemoveAsync($"event_type_{@event.Type}");

            return @event.Id;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error storing event {EventId}", @event.Id);
            throw;
        }
    }

    public async Task<IntegrationEvent> GetEventAsync(string eventId)
    {
        var eventEntity = await _context.Events.FindAsync(eventId);
        if (eventEntity == null) return null;

        return MapToIntegrationEvent(eventEntity);
    }

    public async Task<List<IntegrationEvent>> GetEventsByTypeAsync(
        string eventType, 
        DateTime? since = null)
    {
        var cacheKey = $"event_type_{eventType}_{since?.Ticks ?? 0}";
        var cached = await _cache.GetAsync(cacheKey);

        if (cached != null)
        {
            return JsonSerializer.Deserialize<List<IntegrationEvent>>(
                cached, _jsonOptions);
        }

        var query = _context.Events.Where(e => e.Type == eventType);
        if (since.HasValue)
        {
            query = query.Where(e => e.Timestamp >= since.Value);
        }

        var events = await query
            .OrderByDescending(e => e.Timestamp)
            .Take(1000)
            .Select(e => MapToIntegrationEvent(e))
            .ToListAsync();

        await _cache.SetAsync(
            cacheKey,
            JsonSerializer.SerializeToUtf8Bytes(events, _jsonOptions),
            new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5)
            });

        return events;
    }

    public async Task<string> GetEventSchemaAsync(string eventType)
    {
        var cacheKey = $"event_schema_{eventType}";
        var cached = await _cache.GetStringAsync(cacheKey);

        if (cached != null)
            return cached;

        var schema = await _context.EventSchemas
            .Where(s => s.EventType == eventType)
            .Select(s => s.Schema)
            .FirstOrDefaultAsync();

        if (schema != null)
        {
            await _cache.SetStringAsync(
                cacheKey,
                schema,
                new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1)
                });
        }

        return schema;
    }

    public async Task<List<EventSubscription>> GetSubscriptionsAsync(string eventType)
    {
        return await _context.EventSubscriptions
            .Where(s => s.EventType == eventType && s.IsActive)
            .ToListAsync();
    }

    public async Task UpdateSubscriptionAsync(EventSubscription subscription)
    {
        _context.EventSubscriptions.Update(subscription);
        await _context.SaveChangesAsync();
    }

    private IntegrationEvent MapToIntegrationEvent(EventEntity entity)
    {
        return new IntegrationEvent
        {
            Id = entity.Id,
            Type = entity.Type,
            Source = entity.Source,
            Subject = entity.Subject,
            Timestamp = entity.Timestamp,
            CorrelationId = entity.CorrelationId,
            CausationId = entity.CausationId,
            Metadata = JsonSerializer.Deserialize<Dictionary<string, string>>(
                entity.Metadata, _jsonOptions),
            Data = JsonDocument.Parse(entity.Data)
        };
    }
} 