using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Text.Json;
using System.Threading.Tasks;

public class IMSResponseCache
{
    private readonly IDistributedCache _cache;
    private readonly ILogger<IMSResponseCache> _logger;
    private readonly DistributedCacheEntryOptions _defaultOptions;

    public IMSResponseCache(
        IDistributedCache cache,
        ILogger<IMSResponseCache> logger,
        IConfiguration configuration)
    {
        _cache = cache;
        _logger = logger;
        _defaultOptions = new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = 
                TimeSpan.FromMinutes(configuration.GetValue<int>("Cache:DefaultExpirationMinutes", 5)),
            SlidingExpiration = 
                TimeSpan.FromMinutes(configuration.GetValue<int>("Cache:SlidingExpirationMinutes", 2))
        };
    }

    public async Task<T> GetOrSetAsync<T>(
        string key,
        Func<Task<T>> factory,
        TimeSpan? expiration = null)
    {
        var cacheKey = $"ims:{key}";
        var cached = await _cache.GetAsync(cacheKey);

        if (cached != null)
        {
            try
            {
                return JsonSerializer.Deserialize<T>(cached);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deserializing cached value for {Key}", key);
                await _cache.RemoveAsync(cacheKey);
            }
        }

        var value = await factory();

        try
        {
            var options = new DistributedCacheEntryOptions(_defaultOptions);
            if (expiration.HasValue)
            {
                options.AbsoluteExpirationRelativeToNow = expiration;
            }

            var serialized = JsonSerializer.SerializeToUtf8Bytes(value);
            await _cache.SetAsync(cacheKey, serialized, options);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error caching value for {Key}", key);
        }

        return value;
    }

    public async Task InvalidateAsync(string key)
    {
        await _cache.RemoveAsync($"ims:{key}");
    }

    public async Task InvalidatePatternAsync(string pattern)
    {
        // Note: This is a simplified implementation.
        // In production, you might want to use Redis SCAN or similar
        // to properly handle pattern-based cache invalidation
        _logger.LogWarning(
            "Pattern-based cache invalidation called for {Pattern}. " +
            "Implementation depends on cache provider.", pattern);
    }
} 