public interface IIMSResponseTransformer
{
    Task<TResponse> TransformAsync<TResponse>(string soapResponse, TransformContext context)
        where TResponse : class, new();
}

public class IMSResponseTransformer : IIMSResponseTransformer
{
    private readonly ILogger<IMSResponseTransformer> _logger;
    private readonly Dictionary<Type, IResponseTypeTransformer> _typeTransformers;
    private readonly IMSTransformSettings _settings;

    public IMSResponseTransformer(
        ILogger<IMSResponseTransformer> logger,
        IEnumerable<IResponseTypeTransformer> typeTransformers,
        IOptions<IMSTransformSettings> settings)
    {
        _logger = logger;
        _settings = settings.Value;
        _typeTransformers = typeTransformers.ToDictionary(t => t.ResponseType);
    }

    public async Task<TResponse> TransformAsync<TResponse>(
        string soapResponse, 
        TransformContext context) 
        where TResponse : class, new()
    {
        try
        {
            _logger.LogDebug("Transforming SOAP response to {ResponseType}", typeof(TResponse).Name);

            // Parse SOAP XML
            var soapXml = XDocument.Parse(soapResponse);
            var responseData = ExtractResponseData(soapXml);

            // Get type-specific transformer if available
            if (_typeTransformers.TryGetValue(typeof(TResponse), out var typeTransformer))
            {
                return await typeTransformer.TransformAsync<TResponse>(responseData, context);
            }

            // Fall back to default transformation
            return await TransformResponseDataAsync<TResponse>(responseData, context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to transform response to {ResponseType}", 
                typeof(TResponse).Name);
            throw new TransformException(
                $"Failed to transform response to {typeof(TResponse).Name}", ex);
        }
    }

    private XElement ExtractResponseData(XDocument soapXml)
    {
        // Extract the actual response data from SOAP envelope
        return soapXml.Descendants()
            .FirstOrDefault(e => e.Name.LocalName.EndsWith("Response"))
            ?? throw new TransformException("Could not find response element in SOAP XML");
    }

    private async Task<TResponse> TransformResponseDataAsync<TResponse>(
        XElement responseData, 
        TransformContext context) 
        where TResponse : class, new()
    {
        var response = new TResponse();
        var responseType = typeof(TResponse);

        foreach (var property in responseType.GetProperties())
        {
            var value = await TransformPropertyAsync(property, responseData, context);
            if (value != null)
            {
                property.SetValue(response, value);
            }
        }

        return response;
    }

    private async Task<object> TransformPropertyAsync(
        PropertyInfo property, 
        XElement responseData, 
        TransformContext context)
    {
        // Check for custom transformation attributes
        var transformAttribute = property.GetCustomAttribute<TransformPropertyAttribute>();
        if (transformAttribute != null)
        {
            return await transformAttribute.TransformAsync(responseData, context);
        }

        // Get the XML element name (can be customized via attribute)
        var elementName = property.GetCustomAttribute<XmlElementAttribute>()?.ElementName 
            ?? property.Name;

        // Find the corresponding element in the response
        var element = responseData.Elements()
            .FirstOrDefault(e => e.Name.LocalName.Equals(
                elementName, 
                StringComparison.OrdinalIgnoreCase));

        if (element == null)
        {
            return null;
        }

        // Transform based on property type
        return TransformValue(element.Value, property.PropertyType);
    }

    private object TransformValue(string value, Type targetType)
    {
        if (string.IsNullOrEmpty(value))
        {
            return null;
        }

        try
        {
            if (targetType == typeof(string))
            {
                return value;
            }
            if (targetType == typeof(int) || targetType == typeof(int?))
            {
                return int.Parse(value);
            }
            if (targetType == typeof(decimal) || targetType == typeof(decimal?))
            {
                return decimal.Parse(value);
            }
            if (targetType == typeof(DateTime) || targetType == typeof(DateTime?))
            {
                return DateTime.Parse(value);
            }
            if (targetType == typeof(bool) || targetType == typeof(bool?))
            {
                return bool.Parse(value);
            }
            if (targetType.IsEnum)
            {
                return Enum.Parse(targetType, value);
            }

            throw new TransformException(
                $"Unsupported property type for transformation: {targetType.Name}");
        }
        catch (Exception ex)
        {
            throw new TransformException(
                $"Failed to transform value '{value}' to type {targetType.Name}", ex);
        }
    }
}

public class TransformContext
{
    public string Operation { get; set; }
    public Dictionary<string, object> Parameters { get; set; }
    public CultureInfo Culture { get; set; }
    public IServiceProvider Services { get; set; }
}

public class TransformException : Exception
{
    public TransformException(string message) : base(message) { }
    public TransformException(string message, Exception inner) : base(message, inner) { }
}

[AttributeUsage(AttributeTargets.Property)]
public abstract class TransformPropertyAttribute : Attribute
{
    public abstract Task<object> TransformAsync(
        XElement responseData, 
        TransformContext context);
} 