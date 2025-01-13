public class IntegrationEvent
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Type { get; set; }
    public string Source { get; set; }
    public string Subject { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string CorrelationId { get; set; }
    public string CausationId { get; set; }
    public Dictionary<string, string> Metadata { get; set; } = new();
    public JsonDocument Data { get; set; }
}

public class EventSubscription
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; }
    public string EventType { get; set; }
    public string Source { get; set; }
    public string Subject { get; set; }
    public Dictionary<string, string> Filters { get; set; } = new();
    public string DestinationQueue { get; set; }
    public string TransformationTemplate { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastTriggeredAt { get; set; }
}

public class EventProcessingResult
{
    public string EventId { get; set; }
    public string SubscriptionId { get; set; }
    public bool Success { get; set; }
    public string Error { get; set; }
    public TimeSpan ProcessingTime { get; set; }
    public Dictionary<string, string> ProcessingMetadata { get; set; } = new();
}

public enum EventProcessingStatus
{
    Received,
    Filtered,
    Transformed,
    Routed,
    Delivered,
    Failed
} 