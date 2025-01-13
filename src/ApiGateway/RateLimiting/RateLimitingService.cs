using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

public class RateLimitingService : IRateLimitingService
{
    private readonly IDistributedCache _cache;
    private readonly ILogger<RateLimitingService> _logger;
    private readonly IRouteConfigurationService _routeConfig;
    private readonly RateLimitingOptions _options;

    public RateLimitingService(
        IDistributedCache cache,
        ILogger<RateLimitingService> logger,
        IRouteConfigurationService routeConfig,
        IOptions<RateLimitingOptions> options)
    {
        _cache = cache;
        _logger = logger;
        _routeConfig = routeConfig;
        _options = options.Value;
    }

    public async Task<RateLimitPolicy> GetPolicyAsync(string endpoint)
    {
        var route = await _routeConfig.GetRouteForPathAsync(endpoint, "GET");
        if (route?.RateLimit == null)
        {
            return _options.DefaultPolicy;
        }

        return new RateLimitPolicy
        {
            RequestsPerMinute = route.RateLimit.RequestsPerMinute,
            BurstLimit = route.RateLimit.BurstLimit,
            ClientIdentifier = route.RateLimit.ClientIdentifier
        };
    }

    public async Task<RateLimitResult> CheckRateLimitAsync(
        string clientId,
        string endpoint,
        RateLimitPolicy policy)
    {
        var key = $"ratelimit:{endpoint}:{clientId}";
        var currentTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var windowStart = currentTime - 60; // 1-minute sliding window

        try
        {
            var requestCount = await GetRequestCountAsync(key, windowStart);
            
            if (requestCount >= policy.RequestsPerMinute)
            {
                var resetTime = 60 - (currentTime % 60);
                return new RateLimitResult
                {
                    IsAllowed = false,
                    RemainingRequests = 0,
                    ResetTimeSeconds = (int)resetTime
                };
            }

            // Check burst limit
            var burstCount = await GetBurstCountAsync(key);
            if (burstCount >= policy.BurstLimit)
            {
                return new RateLimitResult
                {
                    IsAllowed = false,
                    RemainingRequests = 0,
                    ResetTimeSeconds = 1
                };
            }

            await IncrementCountersAsync(key, currentTime);

            return new RateLimitResult
            {
                IsAllowed = true,
                RemainingRequests = policy.RequestsPerMinute - requestCount - 1,
                ResetTimeSeconds = 60 - (int)(currentTime % 60)
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking rate limit for {ClientId}", clientId);
            return new RateLimitResult { IsAllowed = true }; // Fail open
        }
    }

    private async Task<int> GetRequestCountAsync(string key, long windowStart)
    {
        var data = await _cache.GetAsync(key);
        if (data == null) return 0;

        var requests = DeserializeRequests(data);
        return requests.Count(r => r >= windowStart);
    }

    private async Task<int> GetBurstCountAsync(string key)
    {
        var burstKey = $"{key}:burst";
        var data = await _cache.GetAsync(burstKey);
        return data != null ? BitConverter.ToInt32(data) : 0;
    }

    private async Task IncrementCountersAsync(string key, long timestamp)
    {
        var data = await _cache.GetAsync(key) ?? Array.Empty<byte>();
        var requests = DeserializeRequests(data).ToList();
        
        // Add new request timestamp
        requests.Add(timestamp);
        
        // Remove old requests
        requests.RemoveAll(r => r < timestamp - 60);

        // Update request history
        await _cache.SetAsync(
            key,
            SerializeRequests(requests),
            new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(2)
            });

        // Update burst counter
        var burstKey = $"{key}:burst";
        var burstCount = await GetBurstCountAsync(key) + 1;
        await _cache.SetAsync(
            burstKey,
            BitConverter.GetBytes(burstCount),
            new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(1)
            });
    }

    private static byte[] SerializeRequests(List<long> requests)
    {
        var bytes = new byte[requests.Count * sizeof(long)];
        Buffer.BlockCopy(
            requests.Select(r => BitConverter.GetBytes(r)).SelectMany(b => b).ToArray(),
            0,
            bytes,
            0,
            bytes.Length);
        return bytes;
    }

    private static IEnumerable<long> DeserializeRequests(byte[] data)
    {
        var requests = new List<long>();
        for (int i = 0; i < data.Length; i += sizeof(long))
        {
            requests.Add(BitConverter.ToInt64(data, i));
        }
        return requests;
    }
} 