using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Text.Json;
using System.Threading.Tasks;

public class DistributedRateLimitCache : IRateLimitCache
{
    private readonly IDistributedCache _cache;
    private readonly ILogger<DistributedRateLimitCache> _logger;
    private readonly RateLimitingOptions _options;
    private readonly JsonSerializerOptions _jsonOptions;

    public DistributedRateLimitCache(
        IDistributedCache cache,
        ILogger<DistributedRateLimitCache> logger,
        IOptions<RateLimitingOptions> options)
    {
        _cache = cache;
        _logger = logger;
        _options = options.Value;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
    }

    public async Task<RateLimitCounter> GetAsync(string key)
    {
        try
        {
            var data = await _cache.GetAsync(key);
            if (data == null) return null;

            return JsonSerializer.Deserialize<RateLimitCounter>(
                data, 
                _jsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving rate limit counter for key {Key}", key);
            return null;
        }
    }

    public async Task SetAsync(
        string key,
        RateLimitCounter counter,
        TimeSpan? expiration = null)
    {
        try
        {
            var options = new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = 
                    expiration ?? _options.CacheExpiration
            };

            var data = JsonSerializer.SerializeToUtf8Bytes(
                counter, 
                _jsonOptions);
            
            await _cache.SetAsync(key, data, options);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, 
                "Error setting rate limit counter for key {Key}", key);
        }
    }

    public async Task<bool> ExistsAsync(string key)
    {
        try
        {
            var data = await _cache.GetAsync(key);
            return data != null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, 
                "Error checking rate limit counter existence for key {Key}", key);
            return false;
        }
    }

    public async Task RemoveAsync(string key)
    {
        try
        {
            await _cache.RemoveAsync(key);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, 
                "Error removing rate limit counter for key {Key}", key);
        }
    }
} 