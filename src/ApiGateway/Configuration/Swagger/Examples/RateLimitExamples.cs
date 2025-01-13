public class RateLimitPolicyExample : IExampleProvider<RateLimitPolicyConfig>
{
    public RateLimitPolicyConfig GetExample()
    {
        return new RateLimitPolicyConfig
        {
            PathPattern = "/api/users/**",
            RequestsPerMinute = 100,
            BurstLimit = 10,
            ExcludedPaths = new[] { "/api/users/health" },
            ClientSpecificLimits = new Dictionary<string, int>
            {
                { "trusted-client", 200 },
                { "internal-service", 500 }
            },
            PenaltyConfig = new RateLimitPenaltyConfig
            {
                ViolationThreshold = 3,
                PenaltyDuration = TimeSpan.FromMinutes(5),
                PenaltyMultiplier = 2.0,
                MaxPenaltyMultiplier = 8
            },
            Enabled = true,
            ExpiresAt = DateTime.UtcNow.AddYears(1),
            RequiredScopes = new[] { "read:users", "write:users" }
        };
    }
}

public class RateLimitResponseExample : IExampleProvider<RateLimitExceededResponse>
{
    public RateLimitExceededResponse GetExample()
    {
        return new RateLimitExceededResponse
        {
            Message = "Rate limit exceeded. Please try again later.",
            RetryAfterSeconds = 30
        };
    }
}

public class SwaggerExampleFilter : IOperationFilter
{
    private readonly IDictionary<Type, object> _examples;

    public SwaggerExampleFilter()
    {
        _examples = new Dictionary<Type, object>
        {
            { typeof(RateLimitPolicyConfig), new RateLimitPolicyExample().GetExample() },
            { typeof(RateLimitExceededResponse), new RateLimitResponseExample().GetExample() }
        };
    }

    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        foreach (var response in operation.Responses)
        {
            var mediaType = response.Value.Content.FirstOrDefault();
            if (mediaType.Value?.Schema?.Reference == null) continue;

            var type = context.SchemaGenerator.SchemaRepository.Schemas[
                mediaType.Value.Schema.Reference.Id].Reference.Id;

            if (_examples.TryGetValue(Type.GetType(type), out var example))
            {
                mediaType.Value.Example = new OpenApiString(
                    JsonSerializer.Serialize(example, new JsonSerializerOptions
                    {
                        WriteIndented = true
                    }));
            }
        }
    }
} 