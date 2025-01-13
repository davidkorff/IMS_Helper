public interface IMessageReplayService
{
    Task<ReplayResult> ReplayMessagesAsync(
        string queueName, 
        DateTime? fromDate = null, 
        string messageType = null, 
        string correlationId = null);
    Task<ReplayResult> ReplayDeadLetterQueueAsync(
        string queueName, 
        bool purgeAfterReplay = false);
    Task<ReplayStatus> GetReplayStatusAsync(string replayId);
}

public class MessageReplayService : IMessageReplayService
{
    private readonly IMessageQueueService _queueService;
    private readonly IMessageSerializer _serializer;
    private readonly ILogger<MessageReplayService> _logger;
    private readonly ConcurrentDictionary<string, ReplayStatus> _replayStatus;

    public MessageReplayService(
        IMessageQueueService queueService,
        IMessageSerializer serializer,
        ILogger<MessageReplayService> logger)
    {
        _queueService = queueService;
        _serializer = serializer;
        _logger = logger;
        _replayStatus = new ConcurrentDictionary<string, ReplayStatus>();
    }

    public async Task<ReplayResult> ReplayMessagesAsync(
        string queueName,
        DateTime? fromDate = null,
        string messageType = null,
        string correlationId = null)
    {
        var replayId = Guid.NewGuid().ToString();
        var status = new ReplayStatus
        {
            Id = replayId,
            QueueName = queueName,
            StartTime = DateTime.UtcNow,
            Status = "In Progress"
        };
        _replayStatus[replayId] = status;

        try
        {
            var messages = await _queueService.GetMessagesAsync(
                queueName, 
                fromDate, 
                messageType, 
                correlationId);

            var replayTasks = messages.Select(msg => ReplayMessageAsync(queueName, msg));
            var results = await Task.WhenAll(replayTasks);

            status.EndTime = DateTime.UtcNow;
            status.Status = "Completed";
            status.TotalMessages = messages.Count;
            status.SuccessCount = results.Count(r => r);
            status.FailureCount = results.Count(r => !r);

            return new ReplayResult
            {
                ReplayId = replayId,
                QueueName = queueName,
                TotalMessages = messages.Count,
                SuccessCount = status.SuccessCount,
                FailureCount = status.FailureCount
            };
        }
        catch (Exception ex)
        {
            status.Status = "Failed";
            status.Error = ex.Message;
            _logger.LogError(ex, "Error replaying messages for queue {QueueName}", queueName);
            throw;
        }
    }

    public async Task<ReplayResult> ReplayDeadLetterQueueAsync(
        string queueName, 
        bool purgeAfterReplay = false)
    {
        var dlqName = $"{queueName}.dlq";
        var replayId = Guid.NewGuid().ToString();
        var status = new ReplayStatus
        {
            Id = replayId,
            QueueName = dlqName,
            StartTime = DateTime.UtcNow,
            Status = "In Progress"
        };
        _replayStatus[replayId] = status;

        try
        {
            var successCount = 0;
            var failureCount = 0;
            var message = await _queueService.ConsumeAsync<object>(dlqName);

            while (message != null)
            {
                try
                {
                    await _queueService.PublishAsync(queueName, message.Payload, message.Headers);
                    await _queueService.AcknowledgeAsync(dlqName, message.MessageId);
                    successCount++;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, 
                        "Error replaying message {MessageId} from DLQ", 
                        message.MessageId);
                    failureCount++;
                }

                message = await _queueService.ConsumeAsync<object>(dlqName);
            }

            status.EndTime = DateTime.UtcNow;
            status.Status = "Completed";
            status.SuccessCount = successCount;
            status.FailureCount = failureCount;
            status.TotalMessages = successCount + failureCount;

            return new ReplayResult
            {
                ReplayId = replayId,
                QueueName = queueName,
                TotalMessages = status.TotalMessages,
                SuccessCount = successCount,
                FailureCount = failureCount
            };
        }
        catch (Exception ex)
        {
            status.Status = "Failed";
            status.Error = ex.Message;
            _logger.LogError(ex, "Error replaying DLQ for queue {QueueName}", queueName);
            throw;
        }
    }

    public async Task<ReplayStatus> GetReplayStatusAsync(string replayId)
    {
        return _replayStatus.TryGetValue(replayId, out var status) 
            ? status 
            : null;
    }

    private async Task<bool> ReplayMessageAsync(string queueName, QueueMessage message)
    {
        try
        {
            await _queueService.PublishAsync(queueName, message.Payload, message.Headers);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, 
                "Error replaying message {MessageId} to queue {QueueName}", 
                message.MessageId, queueName);
            return false;
        }
    }
}

public class ReplayResult
{
    public string ReplayId { get; set; }
    public string QueueName { get; set; }
    public int TotalMessages { get; set; }
    public int SuccessCount { get; set; }
    public int FailureCount { get; set; }
}

public class ReplayStatus
{
    public string Id { get; set; }
    public string QueueName { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public string Status { get; set; }
    public int TotalMessages { get; set; }
    public int SuccessCount { get; set; }
    public int FailureCount { get; set; }
    public string Error { get; set; }
} 