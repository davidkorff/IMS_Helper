public interface IEventRouter
{
    Task RouteEventAsync(IntegrationEvent @event);
    Task<List<EventSubscription>> GetRoutesAsync(IntegrationEvent @event);
    Task AddRouteAsync(EventSubscription subscription);
    Task RemoveRouteAsync(string subscriptionId);
}

public class EventRouter : IEventRouter
{
    private readonly IMessageQueueService _queueService;
    private readonly IEventStore _eventStore;
    private readonly IEventTransformer _transformer;
    private readonly ILogger<EventRouter> _logger;
    private readonly ConcurrentDictionary<string, List<EventSubscription>> _routeCache;

    public EventRouter(
        IMessageQueueService queueService,
        IEventStore eventStore,
        IEventTransformer transformer,
        ILogger<EventRouter> logger)
    {
        _queueService = queueService;
        _eventStore = eventStore;
        _transformer = transformer;
        _logger = logger;
        _routeCache = new ConcurrentDictionary<string, List<EventSubscription>>();
    }

    public async Task RouteEventAsync(IntegrationEvent @event)
    {
        try
        {
            var routes = await GetRoutesAsync(@event);
            
            foreach (var route in routes)
            {
                try
                {
                    if (!await EvaluateRouteConditionsAsync(@event, route))
                        continue;

                    var transformedEvent = route.TransformationTemplate != null
                        ? await _transformer.TransformEventAsync(
                            @event, route.TransformationTemplate)
                        : @event;

                    await _queueService.PublishAsync(
                        route.DestinationQueue,
                        transformedEvent,
                        new Dictionary<string, string>
                        {
                            ["OriginalEventId"] = @event.Id,
                            ["RouteId"] = route.Id
                        });

                    _logger.LogInformation(
                        "Event {EventId} routed to {Queue} via route {RouteId}",
                        @event.Id, route.DestinationQueue, route.Id);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                        "Error routing event {EventId} via route {RouteId}",
                        @event.Id, route.Id);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error routing event {EventId}", @event.Id);
            throw;
        }
    }

    public async Task<List<EventSubscription>> GetRoutesAsync(IntegrationEvent @event)
    {
        // Try cache first
        if (_routeCache.TryGetValue(@event.Type, out var cachedRoutes))
            return cachedRoutes;

        // Get from store and cache
        var routes = await _eventStore.GetSubscriptionsAsync(@event.Type);
        _routeCache.TryAdd(@event.Type, routes);

        return routes;
    }

    public async Task AddRouteAsync(EventSubscription subscription)
    {
        await _eventStore.UpdateSubscriptionAsync(subscription);
        _routeCache.TryRemove(subscription.EventType, out _);
    }

    public async Task RemoveRouteAsync(string subscriptionId)
    {
        var subscription = await _eventStore.GetSubscriptionAsync(subscriptionId);
        if (subscription != null)
        {
            subscription.IsActive = false;
            await _eventStore.UpdateSubscriptionAsync(subscription);
            _routeCache.TryRemove(subscription.EventType, out _);
        }
    }

    private async Task<bool> EvaluateRouteConditionsAsync(
        IntegrationEvent @event, 
        EventSubscription route)
    {
        if (!route.IsActive)
            return false;

        if (!string.IsNullOrEmpty(route.Source) && 
            route.Source != @event.Source)
            return false;

        if (!string.IsNullOrEmpty(route.Subject) && 
            route.Subject != @event.Subject)
            return false;

        foreach (var (path, value) in route.Filters)
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
} 