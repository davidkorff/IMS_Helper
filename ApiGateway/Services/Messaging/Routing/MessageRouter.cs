public interface IMessageRouter
{
    Task RouteAsync<T>(T message, string source, Dictionary<string, string> headers = null);
    void RegisterRoute<T>(string source, string destinationQueue, Func<T, bool> condition = null);
}

public class MessageRouter : IMessageRouter
{
    private readonly IMessageQueueService _queueService;
    private readonly ILogger<MessageRouter> _logger;
    private readonly ConcurrentDictionary<Type, List<RouteDefinition>> _routes;

    public MessageRouter(
        IMessageQueueService queueService,
        ILogger<MessageRouter> logger)
    {
        _queueService = queueService;
        _logger = logger;
        _routes = new ConcurrentDictionary<Type, List<RouteDefinition>>();
    }

    public void RegisterRoute<T>(
        string source, 
        string destinationQueue, 
        Func<T, bool> condition = null)
    {
        var routes = _routes.GetOrAdd(typeof(T), _ => new List<RouteDefinition>());
        routes.Add(new RouteDefinition
        {
            Source = source,
            DestinationQueue = destinationQueue,
            Condition = message => condition?.Invoke((T)message) ?? true
        });
    }

    public async Task RouteAsync<T>(
        T message, 
        string source, 
        Dictionary<string, string> headers = null)
    {
        if (!_routes.TryGetValue(typeof(T), out var routes))
        {
            _logger.LogWarning(
                "No routes registered for message type {MessageType}", 
                typeof(T).Name);
            return;
        }

        var matchingRoutes = routes
            .Where(r => r.Source == source && r.Condition(message))
            .ToList();

        if (!matchingRoutes.Any())
        {
            _logger.LogWarning(
                "No matching routes found for message type {MessageType} from source {Source}", 
                typeof(T).Name, source);
            return;
        }

        foreach (var route in matchingRoutes)
        {
            try
            {
                await _queueService.PublishAsync(
                    route.DestinationQueue, 
                    message, 
                    headers);
                
                _logger.LogInformation(
                    "Routed message of type {MessageType} from {Source} to {Destination}",
                    typeof(T).Name, source, route.DestinationQueue);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Error routing message of type {MessageType} to {Destination}",
                    typeof(T).Name, route.DestinationQueue);
            }
        }
    }

    private class RouteDefinition
    {
        public string Source { get; set; }
        public string DestinationQueue { get; set; }
        public Func<object, bool> Condition { get; set; }
    }
} 