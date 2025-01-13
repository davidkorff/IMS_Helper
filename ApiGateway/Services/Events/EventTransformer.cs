public interface IEventTransformer
{
    Task<IntegrationEvent> TransformEventAsync(
        IntegrationEvent @event, 
        string transformationTemplate);
    Task<string> CompileTemplateAsync(string template);
    Task<bool> ValidateTransformationAsync(
        string template, 
        string sampleEventJson);
}

public class EventTransformer : IEventTransformer
{
    private readonly ILogger<EventTransformer> _logger;
    private readonly ConcurrentDictionary<string, CompiledTemplate> _templateCache;
    private readonly JsonSerializerOptions _jsonOptions;

    public EventTransformer(ILogger<EventTransformer> logger)
    {
        _logger = logger;
        _templateCache = new ConcurrentDictionary<string, CompiledTemplate>();
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
    }

    public async Task<IntegrationEvent> TransformEventAsync(
        IntegrationEvent @event, 
        string transformationTemplate)
    {
        try
        {
            var template = await GetCompiledTemplateAsync(transformationTemplate);
            var eventJson = JsonSerializer.Serialize(@event, _jsonOptions);
            var transformedJson = template.Transform(eventJson);

            var transformedEvent = JsonSerializer.Deserialize<IntegrationEvent>(
                transformedJson, _jsonOptions);

            // Preserve original event metadata
            transformedEvent.Id = @event.Id;
            transformedEvent.Timestamp = @event.Timestamp;
            transformedEvent.CorrelationId = @event.CorrelationId;
            transformedEvent.CausationId = @event.CausationId;

            return transformedEvent;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, 
                "Error transforming event {EventId} with template", @event.Id);
            throw;
        }
    }

    public async Task<string> CompileTemplateAsync(string template)
    {
        try
        {
            var compiled = new CompiledTemplate(template);
            await Task.Run(() => compiled.Compile());
            return compiled.TemplateId;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error compiling transformation template");
            throw;
        }
    }

    public async Task<bool> ValidateTransformationAsync(
        string template, 
        string sampleEventJson)
    {
        try
        {
            var compiled = await GetCompiledTemplateAsync(template);
            var transformed = compiled.Transform(sampleEventJson);
            
            // Validate the transformed JSON is valid
            using var doc = JsonDocument.Parse(transformed);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Template validation failed");
            return false;
        }
    }

    private async Task<CompiledTemplate> GetCompiledTemplateAsync(string template)
    {
        return _templateCache.GetOrAdd(template, t =>
        {
            var compiled = new CompiledTemplate(t);
            compiled.Compile();
            return compiled;
        });
    }

    private class CompiledTemplate
    {
        public string TemplateId { get; }
        public string Template { get; }
        private readonly Lazy<Scriban.Template> _compiled;

        public CompiledTemplate(string template)
        {
            TemplateId = Guid.NewGuid().ToString();
            Template = template;
            _compiled = new Lazy<Scriban.Template>(() => 
                Scriban.Template.Parse(template));
        }

        public void Compile()
        {
            // Force compilation
            _ = _compiled.Value;
        }

        public string Transform(string input)
        {
            var model = JsonSerializer.Deserialize<Dictionary<string, object>>(input);
            return _compiled.Value.Render(model);
        }
    }
} 