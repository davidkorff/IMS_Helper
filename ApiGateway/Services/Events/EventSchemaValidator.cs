public interface IEventSchemaValidator
{
    Task<bool> ValidateAsync(IntegrationEvent @event);
    Task<ValidationResult> ValidateWithDetailsAsync(IntegrationEvent @event);
    Task<string> GenerateSchemaAsync(string eventType, JsonDocument sampleData);
}

public class EventSchemaValidator : IEventSchemaValidator
{
    private readonly IEventStore _eventStore;
    private readonly ILogger<EventSchemaValidator> _logger;
    private readonly ConcurrentDictionary<string, JsonSchema> _schemaCache;

    public EventSchemaValidator(
        IEventStore eventStore,
        ILogger<EventSchemaValidator> logger)
    {
        _eventStore = eventStore;
        _logger = logger;
        _schemaCache = new ConcurrentDictionary<string, JsonSchema>();
    }

    public async Task<bool> ValidateAsync(IntegrationEvent @event)
    {
        try
        {
            var schema = await GetSchemaAsync(@event.Type);
            if (schema == null) return true; // No schema = no validation

            return schema.Validate(@event.Data.RootElement).IsValid;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, 
                "Error validating event {EventId} against schema", @event.Id);
            return false;
        }
    }

    public async Task<ValidationResult> ValidateWithDetailsAsync(IntegrationEvent @event)
    {
        try
        {
            var schema = await GetSchemaAsync(@event.Type);
            if (schema == null)
            {
                return new ValidationResult
                {
                    IsValid = true,
                    Message = "No schema defined for this event type"
                };
            }

            var validation = schema.Validate(@event.Data.RootElement);
            return new ValidationResult
            {
                IsValid = validation.IsValid,
                Message = validation.IsValid ? "Validation successful" : "Validation failed",
                Errors = validation.Errors
                    .Select(e => new ValidationError
                    {
                        Property = e.Path,
                        Message = e.Message
                    })
                    .ToList()
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, 
                "Error performing detailed validation for event {EventId}", @event.Id);
            return new ValidationResult
            {
                IsValid = false,
                Message = "Validation error occurred",
                Errors = new List<ValidationError>
                {
                    new() { Message = ex.Message }
                }
            };
        }
    }

    public async Task<string> GenerateSchemaAsync(
        string eventType, 
        JsonDocument sampleData)
    {
        try
        {
            var generator = new JsonSchemaGenerator();
            var schema = generator.Generate(sampleData.RootElement);
            
            // Add additional schema metadata
            schema.Title = eventType;
            schema.Description = $"Schema for {eventType} events";
            schema.Version = "1.0";

            return schema.ToJson();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, 
                "Error generating schema for event type {EventType}", eventType);
            throw;
        }
    }

    private async Task<JsonSchema> GetSchemaAsync(string eventType)
    {
        return await _schemaCache.GetOrAddAsync(eventType, async type =>
        {
            var schemaJson = await _eventStore.GetEventSchemaAsync(type);
            return schemaJson != null ? JsonSchema.Parse(schemaJson) : null;
        });
    }
}

public class ValidationResult
{
    public bool IsValid { get; set; }
    public string Message { get; set; }
    public List<ValidationError> Errors { get; set; } = new();
}

public class ValidationError
{
    public string Property { get; set; }
    public string Message { get; set; }
} 