public interface IMessageHandler<T>
{
    Task HandleAsync(MessageEnvelope<T> message);
}

public class MessageProcessor : BackgroundService
{
    private readonly IMessageQueueService _queueService;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<MessageProcessor> _logger;
    private readonly Dictionary<string, Type> _messageTypeMap;
    private readonly ConcurrentDictionary<string, Task> _processingTasks;

    public MessageProcessor(
        IMessageQueueService queueService,
        IServiceProvider serviceProvider,
        IConfiguration configuration,
        ILogger<MessageProcessor> logger)
    {
        _queueService = queueService;
        _serviceProvider = serviceProvider;
        _logger = logger;
        _messageTypeMap = new Dictionary<string, Type>();
        _processingTasks = new ConcurrentDictionary<string, Task>();

        RegisterMessageTypes(configuration);
    }

    private void RegisterMessageTypes(IConfiguration configuration)
    {
        var messageTypes = configuration.GetSection("MessageQueue:MessageTypes")
            .Get<Dictionary<string, string>>();

        foreach (var (queueName, typeName) in messageTypes)
        {
            var type = Type.GetType(typeName);
            if (type == null)
            {
                throw new InvalidOperationException(
                    $"Message type {typeName} for queue {queueName} not found");
            }
            _messageTypeMap[queueName] = type;
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        foreach (var queueName in _messageTypeMap.Keys)
        {
            var processTask = ProcessQueueAsync(queueName, stoppingToken);
            _processingTasks.TryAdd(queueName, processTask);
        }

        await Task.WhenAll(_processingTasks.Values);
    }

    private async Task ProcessQueueAsync(string queueName, CancellationToken stoppingToken)
    {
        var messageType = _messageTypeMap[queueName];
        var handlerType = typeof(IMessageHandler<>).MakeGenericType(messageType);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var message = await _queueService.ConsumeAsync(queueName, stoppingToken);
                if (message == null) continue;

                using var scope = _serviceProvider.CreateScope();
                var handler = scope.ServiceProvider.GetService(handlerType);

                if (handler == null)
                {
                    _logger.LogError(
                        "No handler registered for message type {MessageType}", 
                        messageType.Name);
                    await _queueService.RejectAsync(queueName, message.MessageId);
                    continue;
                }

                try
                {
                    await (Task)handlerType.GetMethod("HandleAsync")
                        .Invoke(handler, new[] { message });
                    
                    await _queueService.AcknowledgeAsync(queueName, message.MessageId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, 
                        "Error processing message {MessageId}", message.MessageId);
                    await _queueService.RejectAsync(queueName, message.MessageId, true);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in message processing loop for {QueueName}", 
                    queueName);
                await Task.Delay(1000, stoppingToken);
            }
        }
    }
} 