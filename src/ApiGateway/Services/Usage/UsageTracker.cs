using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Caching.Distributed;
using System;
using System.Threading.Tasks;
using System.Linq;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

public interface IUsageTracker
{
    Task TrackRequest(string apiKey, string endpoint, int statusCode);
    Task<UsageMetrics> GetMetrics(string apiKey, DateTime start, DateTime end);
}

public class UsageTracker : IUsageTracker
{
    private readonly ILogger<UsageTracker> _logger;
    private readonly IDistributedCache _cache;
    private readonly UsageDbContext _dbContext;

    public UsageTracker(
        ILogger<UsageTracker> logger,
        IDistributedCache cache,
        UsageDbContext dbContext)
    {
        _logger = logger;
        _cache = cache;
        _dbContext = dbContext;
    }

    public async Task TrackRequest(string apiKey, string endpoint, int statusCode)
    {
        try
        {
            // Record usage in database
            var usage = new UsageRecord
            {
                ApiKey = apiKey,
                Endpoint = endpoint,
                StatusCode = statusCode,
                Timestamp = DateTime.UtcNow
            };

            _dbContext.UsageRecords.Add(usage);
            await _dbContext.SaveChangesAsync();

            // Update cache for rate limiting
            var cacheKey = $"usage:{apiKey}:{DateTime.UtcNow:yyyyMMddHH}";
            var currentUsage = await _cache.GetStringAsync(cacheKey);
            var count = string.IsNullOrEmpty(currentUsage) ? 1 : int.Parse(currentUsage) + 1;
            
            await _cache.SetStringAsync(
                cacheKey,
                count.ToString(),
                new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1)
                });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error tracking usage for API key {ApiKey}", apiKey);
            // Don't throw - we don't want to fail the request due to usage tracking
        }
    }

    public async Task<UsageMetrics> GetMetrics(string apiKey, DateTime start, DateTime end)
    {
        var records = await _dbContext.UsageRecords
            .Where(r => r.ApiKey == apiKey && r.Timestamp >= start && r.Timestamp <= end)
            .GroupBy(r => new { r.Endpoint, r.StatusCode })
            .Select(g => new EndpointMetric
            {
                Endpoint = g.Key.Endpoint,
                StatusCode = g.Key.StatusCode,
                Count = g.Count()
            })
            .ToListAsync();

        return new UsageMetrics
        {
            ApiKey = apiKey,
            StartDate = start,
            EndDate = end,
            Endpoints = records
        };
    }
}

public class UsageMetrics
{
    public string ApiKey { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public List<EndpointMetric> Endpoints { get; set; }
}

public class EndpointMetric
{
    public string Endpoint { get; set; }
    public int StatusCode { get; set; }
    public int Count { get; set; }
} 