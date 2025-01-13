public interface IMessageQueueService
{
    Task PublishAsync<T>(string queueName, T message, Dictionary<string, string> headers = null);
    Task<MessageEnvelope<T>> ConsumeAsync<T>(string queueName, CancellationToken cancellationToken = default);
    Task AcknowledgeAsync(string queueName, string messageId);
    Task RejectAsync(string queueName, string messageId, bool requeue = false);
}

public class RabbitMQService : IMessageQueueService, IDisposable
{
    private readonly IConnection _connection;
    private readonly IModel _channel;
    private readonly ILogger<RabbitMQService> _logger;
    private readonly Dictionary<string, QueueConfiguration> _queueConfigs;
    private readonly ConcurrentDictionary<string, IModel> _channels;

    public RabbitMQService(
        IConfiguration configuration,
        ILogger<RabbitMQService> logger)
    {
        _logger = logger;
        _queueConfigs = configuration
            .GetSection("MessageQueue:Queues")
            .Get<Dictionary<string, QueueConfiguration>>();

        var factory = new ConnectionFactory
        {
            HostName = configuration["MessageQueue:HostName"],
            UserName = configuration["MessageQueue:UserName"],
            Password = configuration["MessageQueue:Password"],
            VirtualHost = configuration["MessageQueue:VirtualHost"]
        };

        _connection = factory.CreateConnection();
        _channel = _connection.CreateModel();
        _channels = new ConcurrentDictionary<string, IModel>();

        InitializeQueues();
    }

    private void InitializeQueues()
    {
        foreach (var (queueName, config) in _queueConfigs)
        {
            _channel.QueueDeclare(
                queue: queueName,
                durable: config.IsDurable,
                exclusive: false,
                autoDelete: config.AutoDelete,
                arguments: null);

            if (config.EnableDeadLetterQueue)
            {
                var dlqName = $"{queueName}.dlq";
                var dlqArgs = new Dictionary<string, object>
                {
                    { "x-dead-letter-exchange", "" },
                    { "x-dead-letter-routing-key", queueName }
                };

                _channel.QueueDeclare(
                    queue: dlqName,
                    durable: true,
                    exclusive: false,
                    autoDelete: false,
                    arguments: dlqArgs);
            }
        }
    }

    public async Task PublishAsync<T>(
        string queueName, 
        T message, 
        Dictionary<string, string> headers = null)
    {
        try
        {
            var channel = GetOrCreateChannel(queueName);
            var messageId = Guid.NewGuid().ToString();
            var correlationId = Activity.Current?.Id ?? messageId;

            var properties = channel.CreateBasicProperties();
            properties.MessageId = messageId;
            properties.CorrelationId = correlationId;
            properties.Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds());
            properties.Headers = headers?.ToDictionary(x => x.Key, x => (object)x.Value);

            var body = JsonSerializer.SerializeToUtf8Bytes(message);
            
            channel.BasicPublish(
                exchange: "",
                routingKey: queueName,
                mandatory: true,
                basicProperties: properties,
                body: body);

            _logger.LogInformation(
                "Message {MessageId} published to queue {QueueName}", 
                messageId, queueName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, 
                "Error publishing message to queue {QueueName}", queueName);
            throw;
        }
    }

    public async Task<MessageEnvelope<T>> ConsumeAsync<T>(
        string queueName, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            var channel = GetOrCreateChannel(queueName);
            var result = channel.BasicGet(queueName, false);
            
            if (result == null)
                return null;

            var message = JsonSerializer.Deserialize<T>(result.Body.Span);
            
            return new MessageEnvelope<T>
            {
                MessageId = result.BasicProperties.MessageId,
                CorrelationId = result.BasicProperties.CorrelationId,
                Headers = result.BasicProperties.Headers?
                    .ToDictionary(x => x.Key, x => x.Value.ToString()),
                Payload = message
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, 
                "Error consuming message from queue {QueueName}", queueName);
            throw;
        }
    }

    public async Task AcknowledgeAsync(string queueName, string messageId)
    {
        try
        {
            var channel = GetOrCreateChannel(queueName);
            channel.BasicAck(ulong.Parse(messageId), false);
            
            _logger.LogInformation(
                "Message {MessageId} acknowledged in queue {QueueName}", 
                messageId, queueName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, 
                "Error acknowledging message {MessageId} in queue {QueueName}", 
                messageId, queueName);
            throw;
        }
    }

    public async Task RejectAsync(string queueName, string messageId, bool requeue = false)
    {
        try
        {
            var channel = GetOrCreateChannel(queueName);
            channel.BasicReject(ulong.Parse(messageId), requeue);
            
            _logger.LogInformation(
                "Message {MessageId} rejected in queue {QueueName} (requeue: {Requeue})", 
                messageId, queueName, requeue);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, 
                "Error rejecting message {MessageId} in queue {QueueName}", 
                messageId, queueName);
            throw;
        }
    }

    private IModel GetOrCreateChannel(string queueName)
    {
        return _channels.GetOrAdd(queueName, _ => _connection.CreateModel());
    }

    public void Dispose()
    {
        foreach (var channel in _channels.Values)
        {
            channel.Dispose();
        }
        _channel.Dispose();
        _connection.Dispose();
    }
} 