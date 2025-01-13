using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Caching.Memory;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Text.Json;
using System.IO;

public class RouteConfigurationService : IRouteConfigurationService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<RouteConfigurationService> _logger;
    private readonly IMemoryCache _cache;
    private readonly string _routeConfigPath;
    private const string ROUTE_CACHE_KEY = "api_routes";

    public RouteConfigurationService(
        IConfiguration configuration,
        ILogger<RouteConfigurationService> logger,
        IMemoryCache cache)
    {
        _configuration = configuration;
        _logger = logger;
        _cache = cache;
        _routeConfigPath = configuration["RouteConfig:Path"] ?? "routes.json";
    }

    public async Task<IEnumerable<RouteConfig>> GetRoutesAsync()
    {
        if (_cache.TryGetValue<IEnumerable<RouteConfig>>(ROUTE_CACHE_KEY, out var routes))
        {
            return routes;
        }

        routes = await LoadRoutesAsync();
        var cacheOptions = new MemoryCacheEntryOptions()
            .SetAbsoluteExpiration(TimeSpan.FromMinutes(5))
            .SetSlidingExpiration(TimeSpan.FromMinutes(2));

        _cache.Set(ROUTE_CACHE_KEY, routes, cacheOptions);
        return routes;
    }

    public async Task<RouteConfig> GetRouteForPathAsync(string path, string method)
    {
        var routes = await GetRoutesAsync();
        return routes.FirstOrDefault(r => 
            RouteMatches(r.Path, path) && 
            (r.Methods.Contains(method) || r.Methods.Contains("*")));
    }

    private async Task<IEnumerable<RouteConfig>> LoadRoutesAsync()
    {
        try
        {
            var json = await File.ReadAllTextAsync(_routeConfigPath);
            var routes = JsonSerializer.Deserialize<List<RouteConfig>>(json);
            ValidateRoutes(routes);
            return routes;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load route configuration");
            throw new RouteConfigurationException("Failed to load route configuration", ex);
        }
    }

    private void ValidateRoutes(List<RouteConfig> routes)
    {
        foreach (var route in routes)
        {
            if (string.IsNullOrEmpty(route.Path))
                throw new RouteConfigurationException($"Route path cannot be empty");

            if (string.IsNullOrEmpty(route.Destination))
                throw new RouteConfigurationException($"Destination cannot be empty for route {route.Path}");

            if (!route.Methods?.Any() ?? true)
                route.Methods = new[] { "*" };

            if (route.Timeout <= 0)
                route.Timeout = 30000; // Default 30 seconds
        }
    }

    private bool RouteMatches(string routePath, string requestPath)
    {
        var routeParts = routePath.Split('/');
        var requestParts = requestPath.Split('/');

        if (routeParts.Length != requestParts.Length)
            return false;

        for (int i = 0; i < routeParts.Length; i++)
        {
            if (routeParts[i].StartsWith("{") && routeParts[i].EndsWith("}"))
                continue;

            if (routeParts[i] != requestParts[i])
                return false;
        }

        return true;
    }
} 