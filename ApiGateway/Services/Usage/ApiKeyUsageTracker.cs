using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

public class ApiKeyUsageTracker : IApiKeyUsageTracker
{
    private readonly IDistributedCache _cache;
    private readonly IApiKeyUsageRepository _repository;
    private readonly ILogger<ApiKeyUsageTracker> _logger;
    private readonly IMetricsService _metrics;

    public ApiKeyUsageTracker(
        IDistributedCache cache,
        IApiKeyUsageRepository repository,
        ILogger<ApiKeyUsageTracker> logger,
        IMetricsService metrics)
    {
        _cache = cache;
        _repository = repository;
        _logger = logger;
        _metrics = metrics;
    }

    public async Task TrackUsageAsync(ApiKeyUsageInfo usage)
    {
        try
        {
            // Update real-time metrics
            _metrics.IncrementCounter(
                "api_requests_total",
                1,
                new[] { "api_key", usage.ApiKeyId, "endpoint", usage.Endpoint }
            );

            // Track request size
            if (usage.RequestSize.HasValue)
            {
                _metrics.RecordHistogram(
                    "api_request_size_bytes",
                    usage.RequestSize.Value,
                    new[] { "api_key", usage.ApiKeyId }
                );
            }

            // Update cache for rate limiting
            await UpdateRateLimitCacheAsync(usage);

            // Queue detailed usage for batch processing
            await QueueUsageForProcessingAsync(usage);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error tracking API key usage");
            throw;
        }
    }

    private async Task UpdateRateLimitCacheAsync(ApiKeyUsageInfo usage)
    {
        var cacheKey = $"usage:minute:{usage.ApiKeyId}:{DateTime.UtcNow:yyyyMMddHHmm}";
        
        await _cache.IncrementCounterAsync(
            key: cacheKey,
            increment: 1,
            expiry: TimeSpan.FromMinutes(2)
        );
    }

    private async Task QueueUsageForProcessingAsync(ApiKeyUsageInfo usage)
    {
        var usageRecord = new ApiKeyUsageRecord
        {
            Id = Guid.NewGuid().ToString(),
            ApiKeyId = usage.ApiKeyId,
            AccountId = usage.AccountId,
            Timestamp = usage.Timestamp,
            Endpoint = usage.Endpoint,
            Method = usage.Method,
            StatusCode = usage.StatusCode,
            RequestSize = usage.RequestSize,
            ResponseSize = usage.ResponseSize,
            Duration = usage.Duration,
            IpAddress = usage.IpAddress,
            UserAgent = usage.UserAgent
        };

        await _repository.CreateAsync(usageRecord);
    }

    public async Task<ApiKeyUsageStats> GetUsageStatsAsync(
        string apiKeyId, 
        DateTime start, 
        DateTime end)
    {
        return await _repository.GetUsageStatsAsync(apiKeyId, start, end);
    }

    public async Task<List<ApiKeyUsageRecord>> GetUsageHistoryAsync(
        string apiKeyId,
        DateTime start,
        DateTime end,
        int page = 1,
        int pageSize = 100)
    {
        return await _repository.GetUsageHistoryAsync(
            apiKeyId, start, end, page, pageSize);
    }
} 