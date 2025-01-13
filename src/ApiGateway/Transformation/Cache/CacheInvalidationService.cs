using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

public class CacheInvalidationService : ICacheInvalidationService
{
    private readonly IDistributedCache _cache;
    private readonly ILogger<CacheInvalidationService> _logger;
    private readonly TransformationOptions _options;
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks;

    public CacheInvalidationService(
        IDistributedCache cache,
        ILogger<CacheInvalidationService> logger,
        IOptions<TransformationOptions> options)
    {
        _cache = cache;
        _logger = logger;
        _options = options.Value;
        _locks = new ConcurrentDictionary<string, SemaphoreSlim>();
    }

    public async Task InvalidateByPatternAsync(string pattern)
    {
        try
        {
            var keys = await GetMatchingKeysAsync(pattern);
            foreach (var key in keys)
            {
                var lockObj = _locks.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
                await lockObj.WaitAsync();

                try
                {
                    await _cache.RemoveAsync(key);
                    _logger.LogInformation(
                        "Invalidated cache for key {Key} matching pattern {Pattern}",
                        key,
                        pattern);
                }
                finally
                {
                    lockObj.Release();
                    _locks.TryRemove(key, out _);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, 
                "Error invalidating cache for pattern {Pattern}",
                pattern);
            throw;
        }
    }

    public async Task InvalidateByDependencyAsync(string dependency)
    {
        try
        {
            var dependencyKey = $"dep:{dependency}";
            var dependentKeys = await GetDependentKeysAsync(dependencyKey);
            
            foreach (var key in dependentKeys)
            {
                await InvalidateKeyAsync(key);
            }

            await _cache.RemoveAsync(dependencyKey);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, 
                "Error invalidating cache for dependency {Dependency}",
                dependency);
            throw;
        }
    }

    public async Task InvalidateExpiredAsync()
    {
        try
        {
            var expiredKeys = await GetExpiredKeysAsync();
            foreach (var key in expiredKeys)
            {
                await InvalidateKeyAsync(key);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error invalidating expired cache entries");
            throw;
        }
    }

    private async Task InvalidateKeyAsync(string key)
    {
        var lockObj = _locks.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
        await lockObj.WaitAsync();

        try
        {
            await _cache.RemoveAsync(key);
            _logger.LogInformation("Invalidated cache for key {Key}", key);
        }
        finally
        {
            lockObj.Release();
            _locks.TryRemove(key, out _);
        }
    }

    private async Task<IEnumerable<string>> GetMatchingKeysAsync(string pattern)
    {
        // Implementation depends on cache provider
        // This is a simplified example
        return Array.Empty<string>();
    }

    private async Task<IEnumerable<string>> GetDependentKeysAsync(
        string dependencyKey)
    {
        var data = await _cache.GetAsync(dependencyKey);
        if (data == null) return Array.Empty<string>();

        return JsonSerializer.Deserialize<string[]>(data);
    }

    private async Task<IEnumerable<string>> GetExpiredKeysAsync()
    {
        // Implementation depends on cache provider
        // This is a simplified example
        return Array.Empty<string>();
    }
}

public interface ICacheInvalidationService
{
    Task InvalidateByPatternAsync(string pattern);
    Task InvalidateByDependencyAsync(string dependency);
    Task InvalidateExpiredAsync();
} 