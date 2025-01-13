using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Http;
using System;
using System.Threading.Tasks;
using System.IO;
using System.Linq;
using System.Collections.Generic;

public class LoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<LoggingMiddleware> _logger;
    private readonly LoggingSettings _settings;

    public LoggingMiddleware(
        RequestDelegate next,
        ILogger<LoggingMiddleware> logger,
        IOptions<LoggingSettings> settings)
    {
        _next = next;
        _logger = logger;
        _settings = settings.Value;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var start = DateTime.UtcNow;
        var originalBodyStream = context.Response.Body;

        using var requestMemoryStream = new MemoryStream();
        using var responseMemoryStream = new MemoryStream();

        try
        {
            // Capture request
            if (_settings.LogRequestBody)
            {
                context.Request.EnableBuffering();
                await CaptureRequestAsync(context, start);
            }

            // Replace response body with memory stream to capture it
            context.Response.Body = responseMemoryStream;

            // Process request
            await _next(context);

            // Capture response
            if (_settings.LogResponseBody)
            {
                await CaptureResponseAsync(context, start, responseMemoryStream);
            }
        }
        catch (Exception ex)
        {
            LogException(context, start, ex);
            throw;
        }
        finally
        {
            // Copy response to original stream
            if (responseMemoryStream.Length > 0)
            {
                responseMemoryStream.Seek(0, SeekOrigin.Begin);
                await responseMemoryStream.CopyToAsync(originalBodyStream);
            }
            context.Response.Body = originalBodyStream;
        }

        // Log performance metrics
        LogPerformanceMetrics(context, start);
    }

    private async Task CaptureRequestAsync(HttpContext context, DateTime start)
    {
        var request = context.Request;
        var requestBody = string.Empty;

        if (ShouldLogRequestBody(request.ContentType))
        {
            request.Body.Seek(0, SeekOrigin.Begin);
            requestBody = await new StreamReader(request.Body).ReadToEndAsync();
            request.Body.Seek(0, SeekOrigin.Begin);
        }

        var logMessage = new
        {
            Timestamp = start,
            TraceId = context.TraceIdentifier,
            RequestMethod = request.Method,
            RequestPath = request.Path,
            QueryString = request.QueryString.ToString(),
            RequestHeaders = GetFilteredHeaders(request.Headers),
            RequestBody = requestBody,
            ClientIP = context.Connection.RemoteIpAddress?.ToString(),
            UserAgent = request.Headers["User-Agent"].ToString()
        };

        _logger.LogInformation("HTTP Request: {@RequestData}", logMessage);
    }

    private async Task CaptureResponseAsync(
        HttpContext context,
        DateTime start,
        MemoryStream responseStream)
    {
        var response = context.Response;
        var responseBody = string.Empty;

        if (ShouldLogResponseBody(response.ContentType))
        {
            responseStream.Seek(0, SeekOrigin.Begin);
            responseBody = await new StreamReader(responseStream).ReadToEndAsync();
        }

        var logMessage = new
        {
            Timestamp = DateTime.UtcNow,
            TraceId = context.TraceIdentifier,
            StatusCode = response.StatusCode,
            ResponseHeaders = GetFilteredHeaders(response.Headers),
            ResponseBody = responseBody,
            Duration = (DateTime.UtcNow - start).TotalMilliseconds
        };

        _logger.LogInformation("HTTP Response: {@ResponseData}", logMessage);
    }

    private void LogException(HttpContext context, DateTime start, Exception ex)
    {
        var logMessage = new
        {
            Timestamp = DateTime.UtcNow,
            TraceId = context.TraceIdentifier,
            RequestPath = context.Request.Path,
            ExceptionType = ex.GetType().Name,
            ExceptionMessage = ex.Message,
            Duration = (DateTime.UtcNow - start).TotalMilliseconds
        };

        _logger.LogError(ex, "Request failed: {@ErrorData}", logMessage);
    }

    private void LogPerformanceMetrics(HttpContext context, DateTime start)
    {
        var duration = (DateTime.UtcNow - start).TotalMilliseconds;
        if (duration > _settings.SlowRequestThresholdMs)
        {
            _logger.LogWarning(
                "Slow request detected: {Path} took {Duration}ms",
                context.Request.Path,
                duration);
        }

        // Log metrics for monitoring
        _logger.LogInformation(
            "Request metrics: {Path} {Method} {StatusCode} {Duration}ms",
            context.Request.Path,
            context.Request.Method,
            context.Response.StatusCode,
            duration);
    }

    private bool ShouldLogRequestBody(string contentType)
    {
        return _settings.LogRequestBody &&
            contentType != null &&
            !_settings.ExcludedContentTypes.Any(x => 
                contentType.StartsWith(x, StringComparison.OrdinalIgnoreCase));
    }

    private bool ShouldLogResponseBody(string contentType)
    {
        return _settings.LogResponseBody &&
            contentType != null &&
            !_settings.ExcludedContentTypes.Any(x => 
                contentType.StartsWith(x, StringComparison.OrdinalIgnoreCase));
    }

    private IDictionary<string, string> GetFilteredHeaders(IHeaderDictionary headers)
    {
        return headers
            .Where(h => !_settings.ExcludedHeaders.Contains(h.Key))
            .ToDictionary(h => h.Key, h => h.Value.ToString());
    }
} 