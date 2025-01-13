public abstract class BaseIntegration
{
    protected readonly IIMSGatewayClient _imsClient;
    protected readonly ILogger _logger;
    protected readonly IMetricsCollector _metrics;

    public virtual async Task<HealthCheck> CheckHealth()
    {
        // Common health check implementation
    }

    public virtual async Task<UsageMetrics> GetUsageMetrics()
    {
        // Common usage tracking
    }

    protected virtual async Task<Result<T>> ExecuteWithRetry<T>(
        Func<Task<T>> operation,
        RetryPolicy policy)
    {
        // Common retry logic
    }
} 