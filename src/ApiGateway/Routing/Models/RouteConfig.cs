public class RouteConfig
{
    public string Path { get; set; }
    public string Destination { get; set; }
    public string[] Methods { get; set; }
    public int Timeout { get; set; } = 30000;
    public bool RequireAuthentication { get; set; } = true;
    public string[] RequiredScopes { get; set; } = Array.Empty<string>();
    public Dictionary<string, string> Headers { get; set; } = new();
    public RetryPolicy RetryPolicy { get; set; }
    public RateLimitPolicy RateLimit { get; set; }
    public CachingPolicy CachingPolicy { get; set; }
    public LoadBalancingPolicy LoadBalancing { get; set; }
    public CircuitBreakerPolicy CircuitBreaker { get; set; }
}

public class RetryPolicy
{
    public int MaxRetries { get; set; } = 3;
    public int DelayMs { get; set; } = 1000;
    public bool ExponentialBackoff { get; set; } = true;
    public string[] RetryableStatusCodes { get; set; } = new[] { "500", "502", "503", "504" };
}

public class RateLimitPolicy
{
    public int RequestsPerMinute { get; set; }
    public string ClientIdentifier { get; set; } = "ClientIP";
    public int BurstLimit { get; set; }
    public string[] ExcludedPaths { get; set; } = Array.Empty<string>();
}

public class CachingPolicy
{
    public bool Enabled { get; set; }
    public int DurationSeconds { get; set; }
    public string[] VaryByHeaders { get; set; } = Array.Empty<string>();
    public string[] VaryByQueryParams { get; set; } = Array.Empty<string>();
}

public class LoadBalancingPolicy
{
    public string Algorithm { get; set; } = "RoundRobin";
    public string[] Endpoints { get; set; } = Array.Empty<string>();
    public bool HealthCheckEnabled { get; set; } = true;
    public int HealthCheckIntervalSeconds { get; set; } = 30;
}

public class CircuitBreakerPolicy
{
    public int FailureThreshold { get; set; } = 5;
    public int BreakDurationSeconds { get; set; } = 30;
    public int MinimumThroughput { get; set; } = 10;
    public double FailurePercentageThreshold { get; set; } = 50;
} 