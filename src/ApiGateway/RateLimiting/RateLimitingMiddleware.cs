using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

public class RateLimitingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RateLimitingMiddleware> _logger;
    private readonly IRateLimitingService _rateLimitingService;
    private readonly RateLimitingOptions _options;

    public RateLimitingMiddleware(
        RequestDelegate next,
        ILogger<RateLimitingMiddleware> logger,
        IRateLimitingService rateLimitingService,
        IOptions<RateLimitingOptions> options)
    {
        _next = next;
        _logger = logger;
        _rateLimitingService = rateLimitingService;
        _options = options.Value;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var clientId = GetClientIdentifier(context);
        var endpoint = context.Request.Path.Value;

        if (ShouldSkipRateLimiting(endpoint))
        {
            await _next(context);
            return;
        }

        var policy = await _rateLimitingService.GetPolicyAsync(endpoint);
        if (policy == null)
        {
            await _next(context);
            return;
        }

        var result = await _rateLimitingService.CheckRateLimitAsync(
            clientId, 
            endpoint, 
            policy);

        if (!result.IsAllowed)
        {
            _logger.LogWarning(
                "Rate limit exceeded for client {ClientId} on {Endpoint}. " +
                "Reset in {ResetTime} seconds",
                clientId,
                endpoint,
                result.ResetTimeSeconds);

            context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
            context.Response.Headers.Add(
                "X-RateLimit-Limit", 
                policy.RequestsPerMinute.ToString());
            context.Response.Headers.Add(
                "X-RateLimit-Remaining", 
                result.RemainingRequests.ToString());
            context.Response.Headers.Add(
                "X-RateLimit-Reset", 
                result.ResetTimeSeconds.ToString());
            context.Response.Headers.Add(
                "Retry-After", 
                result.ResetTimeSeconds.ToString());

            var response = new RateLimitExceededResponse
            {
                Message = "Rate limit exceeded",
                RetryAfterSeconds = result.ResetTimeSeconds
            };

            await context.Response.WriteAsJsonAsync(response);
            return;
        }

        // Add rate limit headers even for successful requests
        context.Response.OnStarting(() =>
        {
            context.Response.Headers.Add(
                "X-RateLimit-Limit", 
                policy.RequestsPerMinute.ToString());
            context.Response.Headers.Add(
                "X-RateLimit-Remaining", 
                result.RemainingRequests.ToString());
            context.Response.Headers.Add(
                "X-RateLimit-Reset", 
                result.ResetTimeSeconds.ToString());
            return Task.CompletedTask;
        });

        await _next(context);
    }

    private string GetClientIdentifier(HttpContext context)
    {
        return _options.ClientIdentifierPolicy switch
        {
            ClientIdentifierPolicy.IpAddress => 
                context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            
            ClientIdentifierPolicy.AuthenticatedUser => 
                context.User?.Identity?.Name ?? 
                context.User?.FindFirst("sub")?.Value ?? 
                "anonymous",
            
            ClientIdentifierPolicy.CustomHeader when 
                context.Request.Headers.TryGetValue(
                    _options.CustomClientIdHeader, out var customId) => 
                customId.ToString(),
            
            _ => throw new InvalidOperationException(
                "Invalid client identifier policy configuration")
        };
    }

    private bool ShouldSkipRateLimiting(string endpoint)
    {
        return _options.ExcludedPaths.Any(path => 
            endpoint.StartsWith(path, StringComparison.OrdinalIgnoreCase));
    }
} 