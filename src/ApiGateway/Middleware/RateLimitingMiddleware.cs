using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;

public class RateLimitingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IDistributedCache _cache;
    private readonly ILogger<RateLimitingMiddleware> _logger;
    private readonly IConfiguration _configuration;

    public RateLimitingMiddleware(
        RequestDelegate next,
        IDistributedCache cache,
        ILogger<RateLimitingMiddleware> logger,
        IConfiguration configuration)
    {
        _next = next;
        _cache = cache;
        _logger = logger;
        _configuration = configuration;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var apiKey = context.Request.Headers["X-API-Key"].ToString();
        
        if (string.IsNullOrEmpty(apiKey))
        {
            context.Response.StatusCode = 401;
            await context.Response.WriteAsJsonAsync(new { error = "API key required" });
            return;
        }

        var limit = await GetRateLimit(apiKey);
        var cacheKey = $"rate-limit:{apiKey}:{DateTime.UtcNow:yyyyMMddHH}";
        var currentUsageStr = await _cache.GetStringAsync(cacheKey);
        var currentUsage = string.IsNullOrEmpty(currentUsageStr) ? 0 : int.Parse(currentUsageStr);

        if (currentUsage >= limit)
        {
            context.Response.StatusCode = 429;
            await context.Response.WriteAsJsonAsync(new { error = "Rate limit exceeded" });
            return;
        }

        await _cache.SetStringAsync(
            cacheKey,
            (currentUsage + 1).ToString(),
            new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1)
            });

        await _next(context);
    }

    private async Task<int> GetRateLimit(string apiKey)
    {
        // In production, this would come from a database
        // For now, we'll use configuration
        return _configuration.GetValue<int>($"RateLimits:{apiKey}", 1000);
    }
} 