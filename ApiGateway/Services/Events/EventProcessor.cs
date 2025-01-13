public interface IEventProcessor
{
    Task<EventProcessingResult> ProcessEventAsync(IntegrationEvent @event);
    Task<List<EventSubscription>> GetMatchingSubscriptionsAsync(IntegrationEvent @event);
    Task<bool> ValidateEventAsync(IntegrationEvent @event);
}

public class EventProcessor : IEventProcessor
{
    private readonly IMessageQueueService _queueService;
    private readonly IEventStore _eventStore;
    private readonly IEventTransformer _transformer;
    private readonly ILogger<EventProcessor> _logger;
    private readonly ConcurrentDictionary<string, List<EventSubscription>> _subscriptionCache;
    private readonly JsonSchemaGenerator _schemaGenerator;

    public EventProcessor(
        IMessageQueueService queueService,
        IEventStore eventStore,
        IEventTransformer transformer,
        ILogger<EventProcessor> logger)
    {
        _queueService = queueService;
        _eventStore = eventStore;
        _transformer = transformer;
        _logger = logger;
        _subscriptionCache = new ConcurrentDictionary<string, List<EventSubscription>>();
        _schemaGenerator = new JsonSchemaGenerator();
    }

    public async Task<EventProcessingResult> ProcessEventAsync(IntegrationEvent @event)
    {
        var sw = Stopwatch.StartNew();
        var result = new EventProcessingResult
        {
            EventId = @event.Id,
            ProcessingMetadata = new Dictionary<string, string>()
        };

        try
        {
            // Validate event
            if (!await ValidateEventAsync(@event))
            {
                result.Success = false;
                result.Error = "Event validation failed";
                return result;
            }

            // Find matching subscriptions
            var subscriptions = await GetMatchingSubscriptionsAsync(@event);
            result.ProcessingMetadata["MatchingSubscriptions"] = subscriptions.Count.ToString();

            foreach (var subscription in subscriptions)
            {
                try
                {
                    // Apply filters
                    if (!EvaluateFilters(subscription.Filters, @event))
                        continue;

                    // Transform event if needed
                    var transformedEvent = subscription.TransformationTemplate != null
                        ? await _transformer.TransformEventAsync(@event, subscription.TransformationTemplate)
                        : @event;

                    // Route to destination queue
                    await _queueService.PublishAsync(
                        subscription.DestinationQueue,
                        transformedEvent,
                        new Dictionary<string, string>
                        {
                            ["OriginalEventId"] = @event.Id,
                            ["SubscriptionId"] = subscription.Id,
                            ["ProcessingTimestamp"] = DateTime.UtcNow.ToString("O")
                        });

                    // Update subscription metadata
                    await UpdateSubscriptionMetadataAsync(subscription, true);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                        "Error processing event {EventId} for subscription {SubscriptionId}",
                        @event.Id, subscription.Id);
                    result.ProcessingMetadata[$"Error_{subscription.Id}"] = ex.Message;
                }
            }

            // Store event
            await _eventStore.StoreEventAsync(@event);

            result.Success = true;
            result.ProcessingTime = sw.Elapsed;
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing event {EventId}", @event.Id);
            result.Success = false;
            result.Error = ex.Message;
            result.ProcessingTime = sw.Elapsed;
            return result;
        }
    }

    public async Task<List<EventSubscription>> GetMatchingSubscriptionsAsync(
        IntegrationEvent @event)
    {
        // Try get from cache first
        if (_subscriptionCache.TryGetValue(@event.Type, out var cachedSubscriptions))
            return cachedSubscriptions;

        // Get from store and cache
        var subscriptions = await _eventStore.GetSubscriptionsAsync(@event.Type);
        _subscriptionCache.TryAdd(@event.Type, subscriptions);

        return subscriptions;
    }

    public async Task<bool> ValidateEventAsync(IntegrationEvent @event)
    {
        try
        {
            // Validate required fields
            if (string.IsNullOrEmpty(@event.Type) || 
                string.IsNullOrEmpty(@event.Source) ||
                @event.Data == null)
                return false;

            // Validate against schema if available
            var schema = await _eventStore.GetEventSchemaAsync(@event.Type);
            if (schema != null)
            {
                var jsonSchema = JsonSchema.Parse(schema);
                var validation = jsonSchema.Validate(@event.Data.RootElement);
                return validation.IsValid;
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating event {EventId}", @event.Id);
            return false;
        }
    }

    private bool EvaluateFilters(
        Dictionary<string, string> filters, 
        IntegrationEvent @event)
    {
        foreach (var (path, value) in filters)
        {
            try
            {
                var actualValue = JsonPath.Parse(path)
                    .Evaluate(@event.Data.RootElement)
                    .ToString();

                if (actualValue != value)
                    return false;
            }
            catch
            {
                return false;
            }
        }

        return true;
    }

    private async Task UpdateSubscriptionMetadataAsync(
        EventSubscription subscription, 
        bool success)
    {
        subscription.LastTriggeredAt = DateTime.UtcNow;
        await _eventStore.UpdateSubscriptionAsync(subscription);
    }
} 