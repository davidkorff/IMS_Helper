public class RateLimitingOptions
{
    public ClientIdentifierPolicy ClientIdentifierPolicy { get; set; }
    public string CustomClientIdHeader { get; set; }
    public string[] ExcludedPaths { get; set; } = Array.Empty<string>();
    public RateLimitPolicy DefaultPolicy { get; set; } = new();
    public bool EnableRateLimitingHeaders { get; set; } = true;
    public TimeSpan CacheExpiration { get; set; } = TimeSpan.FromMinutes(1);
}

public enum ClientIdentifierPolicy
{
    IpAddress,
    AuthenticatedUser,
    CustomHeader
}

public class RateLimitPolicy
{
    public int RequestsPerMinute { get; set; } = 60;
    public int BurstLimit { get; set; } = 10;
    public string ClientIdentifier { get; set; } = "ClientIP";
}

public class RateLimitResult
{
    public bool IsAllowed { get; set; }
    public int RemainingRequests { get; set; }
    public int ResetTimeSeconds { get; set; }
}

public class RateLimitExceededResponse
{
    public string Message { get; set; }
    public int RetryAfterSeconds { get; set; }
}

public interface IRateLimitingService
{
    Task<RateLimitPolicy> GetPolicyAsync(string endpoint);
    Task<RateLimitResult> CheckRateLimitAsync(
        string clientId,
        string endpoint,
        RateLimitPolicy policy);
} 