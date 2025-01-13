using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Http;
using System;
using System.Threading.Tasks;

public class UsageTrackingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IUsageTracker _usageTracker;
    private readonly ILogger<UsageTrackingMiddleware> _logger;

    public UsageTrackingMiddleware(
        RequestDelegate next,
        IUsageTracker usageTracker,
        ILogger<UsageTrackingMiddleware> logger)
    {
        _next = next;
        _usageTracker = usageTracker;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var apiKey = context.Request.Headers["X-API-Key"].ToString();
        var originalPath = context.Request.Path.Value;
        
        try
        {
            // Call the next middleware
            await _next(context);
        }
        finally
        {
            // Track usage after response
            await _usageTracker.TrackRequest(
                apiKey,
                originalPath,
                context.Response.StatusCode);
        }
    }
} 