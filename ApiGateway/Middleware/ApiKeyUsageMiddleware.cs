using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Diagnostics;
using System.IO;
using System.Security.Claims;
using System.Threading.Tasks;

public class ApiKeyUsageMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IApiKeyUsageTracker _usageTracker;
    private readonly ILogger<ApiKeyUsageMiddleware> _logger;

    public ApiKeyUsageMiddleware(
        RequestDelegate next,
        IApiKeyUsageTracker usageTracker,
        ILogger<ApiKeyUsageMiddleware> logger)
    {
        _next = next;
        _usageTracker = usageTracker;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var sw = Stopwatch.StartNew();
        var originalBodyStream = context.Response.Body;

        try
        {
            using var responseBody = new MemoryStream();
            context.Response.Body = responseBody;

            await _next(context);

            sw.Stop();

            var apiKeyId = context.User?.FindFirst("ApiKeyId")?.Value;
            if (!string.IsNullOrEmpty(apiKeyId))
            {
                var usage = new ApiKeyUsageInfo
                {
                    ApiKeyId = apiKeyId,
                    AccountId = context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value,
                    Timestamp = DateTime.UtcNow,
                    Endpoint = context.Request.Path,
                    Method = context.Request.Method,
                    StatusCode = context.Response.StatusCode,
                    RequestSize = context.Request.ContentLength,
                    ResponseSize = responseBody.Length,
                    Duration = sw.Elapsed,
                    IpAddress = context.Connection.RemoteIpAddress?.ToString(),
                    UserAgent = context.Request.Headers["User-Agent"].ToString()
                };

                await _usageTracker.TrackUsageAsync(usage);
            }

            responseBody.Seek(0, SeekOrigin.Begin);
            await responseBody.CopyToAsync(originalBodyStream);
        }
        finally
        {
            context.Response.Body = originalBodyStream;
        }
    }
}

public static class ApiKeyUsageMiddlewareExtensions
{
    public static IApplicationBuilder UseApiKeyUsageTracking(
        this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<ApiKeyUsageMiddleware>();
    }
} 