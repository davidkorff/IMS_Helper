using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Caching.Distributed;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using System.Management;
using System.Text;

public class ApiGatewayHealthCheck : IHealthCheck
{
    private readonly ILogger<ApiGatewayHealthCheck> _logger;
    private readonly IHttpClientFactory _clientFactory;
    private readonly IConfiguration _configuration;
    private readonly IMemoryCache _cache;
    private readonly IDistributedCache _distributedCache;

    public ApiGatewayHealthCheck(
        ILogger<ApiGatewayHealthCheck> logger,
        IHttpClientFactory clientFactory,
        IConfiguration configuration,
        IMemoryCache cache,
        IDistributedCache distributedCache)
    {
        _logger = logger;
        _clientFactory = clientFactory;
        _configuration = configuration;
        _cache = cache;
        _distributedCache = distributedCache;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var checks = new Dictionary<string, object>();
        var isHealthy = true;

        try
        {
            // Check memory cache
            var memoryCacheStatus = CheckMemoryCache();
            checks.Add("MemoryCache", memoryCacheStatus);
            isHealthy &= memoryCacheStatus == "Healthy";

            // Check distributed cache
            var distributedCacheStatus = await CheckDistributedCacheAsync();
            checks.Add("DistributedCache", distributedCacheStatus);
            isHealthy &= distributedCacheStatus == "Healthy";

            // Check downstream services
            var serviceChecks = await CheckDownstreamServicesAsync();
            foreach (var (service, status) in serviceChecks)
            {
                checks.Add(service, status);
                isHealthy &= status == "Healthy";
            }

            // Check system resources
            var resourceChecks = CheckSystemResources();
            foreach (var (resource, status) in resourceChecks)
            {
                checks.Add(resource, status);
                isHealthy &= status.Contains("Healthy");
            }

            return isHealthy
                ? HealthCheckResult.Healthy("API Gateway is healthy", checks)
                : HealthCheckResult.Degraded("API Gateway is degraded", null, checks);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Health check failed");
            return HealthCheckResult.Unhealthy(
                "API Gateway is unhealthy",
                ex,
                checks);
        }
    }

    private string CheckMemoryCache()
    {
        try
        {
            var key = $"health_check_{Guid.NewGuid()}";
            _cache.Set(key, "test", TimeSpan.FromSeconds(1));
            var value = _cache.Get(key);
            return value != null ? "Healthy" : "Unhealthy";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Memory cache health check failed");
            return "Unhealthy";
        }
    }

    private async Task<string> CheckDistributedCacheAsync()
    {
        try
        {
            var key = $"health_check_{Guid.NewGuid()}";
            await _distributedCache.SetAsync(
                key,
                Encoding.UTF8.GetBytes("test"),
                new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(1)
                });
            var value = await _distributedCache.GetAsync(key);
            return value != null ? "Healthy" : "Unhealthy";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Distributed cache health check failed");
            return "Unhealthy";
        }
    }

    private async Task<Dictionary<string, string>> CheckDownstreamServicesAsync()
    {
        var results = new Dictionary<string, string>();
        var services = _configuration
            .GetSection("DownstreamServices")
            .Get<string[]>();

        if (services == null) return results;

        foreach (var service in services)
        {
            try
            {
                using var client = _clientFactory.CreateClient();
                var response = await client.GetAsync(
                    $"{service}/health",
                    HttpCompletionOption.ResponseHeadersRead);

                results[service] = response.IsSuccessStatusCode
                    ? "Healthy"
                    : "Unhealthy";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, 
                    "Health check failed for service {Service}",
                    service);
                results[service] = "Unhealthy";
            }
        }

        return results;
    }

    private Dictionary<string, string> CheckSystemResources()
    {
        var results = new Dictionary<string, string>();

        // Check CPU usage
        var cpuUsage = GetCpuUsage();
        results["CPU"] = cpuUsage < 80
            ? $"Healthy ({cpuUsage}%)"
            : $"Degraded ({cpuUsage}%)";

        // Check memory usage
        var memoryInfo = GetMemoryInfo();
        results["Memory"] = memoryInfo.PercentUsed < 80
            ? $"Healthy ({memoryInfo.PercentUsed}%)"
            : $"Degraded ({memoryInfo.PercentUsed}%)";

        // Check disk space
        var diskInfo = GetDiskInfo();
        results["Disk"] = diskInfo.PercentUsed < 80
            ? $"Healthy ({diskInfo.PercentUsed}%)"
            : $"Degraded ({diskInfo.PercentUsed}%)";

        return results;
    }

    private double GetCpuUsage()
    {
        using var cpuCounter = new PerformanceCounter(
            "Processor",
            "% Processor Time",
            "_Total");
        
        cpuCounter.NextValue(); // First call will always return 0
        Thread.Sleep(1000); // Wait for next sample
        return Math.Round(cpuCounter.NextValue(), 2);
    }

    private (double PercentUsed, long Available) GetMemoryInfo()
    {
        var totalMemory = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes;
        var usedMemory = Process.GetCurrentProcess().WorkingSet64;
        var percentUsed = Math.Round(
            (double)usedMemory / totalMemory * 100,
            2);
        
        return (percentUsed, totalMemory - usedMemory);
    }

    private (double PercentUsed, long Available) GetDiskInfo()
    {
        var drive = DriveInfo.GetDrives()
            .First(d => d.Name == Path.GetPathRoot(Environment.CurrentDirectory));
        
        var percentUsed = Math.Round(
            (double)(drive.TotalSize - drive.AvailableFreeSpace) 
            / drive.TotalSize * 100,
            2);
        
        return (percentUsed, drive.AvailableFreeSpace);
    }
} 