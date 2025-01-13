public interface IMessageSerializer
{
    byte[] Serialize<T>(T message);
    T Deserialize<T>(byte[] data);
    bool TryDeserialize<T>(byte[] data, out T result);
    bool ValidateSchema<T>(byte[] data);
}

public class JsonMessageSerializer : IMessageSerializer
{
    private readonly JsonSerializerOptions _options;
    private readonly ILogger<JsonMessageSerializer> _logger;
    private readonly ConcurrentDictionary<Type, JsonSchema> _schemaCache;

    public JsonMessageSerializer(ILogger<JsonMessageSerializer> logger)
    {
        _logger = logger;
        _schemaCache = new ConcurrentDictionary<Type, JsonSchema>();
        _options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
    }

    public byte[] Serialize<T>(T message)
    {
        try
        {
            return JsonSerializer.SerializeToUtf8Bytes(message, _options);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error serializing message of type {Type}", typeof(T).Name);
            throw new MessageSerializationException("Failed to serialize message", ex);
        }
    }

    public T Deserialize<T>(byte[] data)
    {
        try
        {
            return JsonSerializer.Deserialize<T>(data, _options);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deserializing message to type {Type}", typeof(T).Name);
            throw new MessageSerializationException("Failed to deserialize message", ex);
        }
    }

    public bool TryDeserialize<T>(byte[] data, out T result)
    {
        try
        {
            result = JsonSerializer.Deserialize<T>(data, _options);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to deserialize message to type {Type}", typeof(T).Name);
            result = default;
            return false;
        }
    }

    public bool ValidateSchema<T>(byte[] data)
    {
        try
        {
            var schema = _schemaCache.GetOrAdd(typeof(T), GenerateSchema);
            var jsonDocument = JsonDocument.Parse(data);
            var validation = schema.Validate(jsonDocument.RootElement);
            
            if (!validation.IsValid)
            {
                _logger.LogWarning("Schema validation failed for type {Type}: {Errors}",
                    typeof(T).Name,
                    string.Join(", ", validation.Errors));
            }
            
            return validation.IsValid;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating schema for type {Type}", typeof(T).Name);
            return false;
        }
    }

    private JsonSchema GenerateSchema(Type type)
    {
        var generator = new JsonSchemaGenerator();
        return generator.Generate(type);
    }
}

public class MessageSerializationException : Exception
{
    public MessageSerializationException(string message, Exception inner = null) 
        : base(message, inner)
    {
    }
} 