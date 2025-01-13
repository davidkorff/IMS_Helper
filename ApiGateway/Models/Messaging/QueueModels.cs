public class QueueMessage
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Type { get; set; }
    public string Source { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string CorrelationId { get; set; }
    public Dictionary<string, string> Headers { get; set; } = new();
    public object Payload { get; set; }
    public MessageStatus Status { get; set; }
    public int RetryCount { get; set; }
    public string Error { get; set; }
}

public enum MessageStatus
{
    New,
    Processing,
    Completed,
    Failed,
    Retrying,
    DeadLetter
}

public class MessageEnvelope<T>
{
    public string MessageId { get; set; }
    public string CorrelationId { get; set; }
    public Dictionary<string, string> Headers { get; set; }
    public T Payload { get; set; }
}

public class QueueConfiguration
{
    public string Name { get; set; }
    public bool IsDurable { get; set; } = true;
    public bool AutoDelete { get; set; } = false;
    public int MaxRetries { get; set; } = 3;
    public TimeSpan RetryDelay { get; set; } = TimeSpan.FromMinutes(5);
    public bool EnableDeadLetterQueue { get; set; } = true;
} 