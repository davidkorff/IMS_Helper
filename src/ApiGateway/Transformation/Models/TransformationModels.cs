public class TransformationOptions
{
    public string[] ExcludedPaths { get; set; } = Array.Empty<string>();
    public bool EnableRequestTransformation { get; set; } = true;
    public bool EnableResponseTransformation { get; set; } = true;
    public int MaxTransformationSize { get; set; } = 10 * 1024 * 1024; // 10MB
    public TimeSpan TransformationTimeout { get; set; } = TimeSpan.FromSeconds(30);
}

public class TransformationRule
{
    public string PathPattern { get; set; }
    public TransformationConfig RequestTransformation { get; set; }
    public TransformationConfig ResponseTransformation { get; set; }
    public string[] RequiredScopes { get; set; } = Array.Empty<string>();
    public bool Enabled { get; set; } = true;
}

public class TransformationConfig
{
    public string Type { get; set; }
    public Dictionary<string, string> Mappings { get; set; }
    public string[] FieldsToRemove { get; set; }
    public string[] FieldsToKeep { get; set; }
    public Dictionary<string, string> HeaderMappings { get; set; }
    public string SchemaValidation { get; set; }
}

public class TransformationErrorResponse
{
    public string Message { get; set; }
    public string Details { get; set; }
    public string Path { get; set; }
}

public enum TransformationType
{
    Request,
    Response
}

public class TransformationException : Exception
{
    public TransformationType TransformationType { get; }

    public TransformationException(
        string message,
        TransformationType type,
        Exception inner = null)
        : base(message, inner)
    {
        TransformationType = type;
    }
}

public interface ITransformationService
{
    bool CanTransformRequest(string path, string contentType);
    bool CanTransformResponse(string path, string contentType);
    Task<Stream> TransformRequestAsync(Stream requestBody, string path, string contentType);
    Task<Stream> TransformResponseAsync(Stream responseBody, string path, string contentType);
}

public interface IContentTransformer
{
    string ContentType { get; }
    Task<Stream> TransformAsync(Stream content, TransformationConfig config);
}

public interface ITransformationRuleProvider
{
    TransformationRule GetRule(string path);
} 