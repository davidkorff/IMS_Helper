using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Threading;

public class LogEnricher : ILogEnricher
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly AsyncLocal<Dictionary<string, object>> _localProperties;

    public LogEnricher(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
        _localProperties = new AsyncLocal<Dictionary<string, object>>();
    }

    public void EnrichLog(ILogger logger, LogEvent logEvent)
    {
        var context = _httpContextAccessor.HttpContext;
        var properties = _localProperties.Value ??= new Dictionary<string, object>();

        // Add correlation IDs
        logEvent.AddProperty("TraceId", context?.TraceIdentifier);
        logEvent.AddProperty("CorrelationId", GetOrCreateCorrelationId());

        // Add request context
        if (context?.Request != null)
        {
            logEvent.AddProperty("RequestMethod", context.Request.Method);
            logEvent.AddProperty("RequestPath", context.Request.Path);
            logEvent.AddProperty("UserAgent", context.Request.Headers["User-Agent"].ToString());
            logEvent.AddProperty("ClientIP", context.Connection.RemoteIpAddress?.ToString());
        }

        // Add user context
        if (context?.User?.Identity?.IsAuthenticated == true)
        {
            logEvent.AddProperty("UserId", context.User.FindFirst("sub")?.Value);
            logEvent.AddProperty("UserName", context.User.Identity.Name);
        }

        // Add custom properties
        foreach (var property in properties)
        {
            logEvent.AddProperty(property.Key, property.Value);
        }
    }

    private string GetOrCreateCorrelationId()
    {
        var context = _httpContextAccessor.HttpContext;
        if (context == null) return null;

        const string CorrelationIdHeader = "X-Correlation-ID";
        if (!context.Request.Headers.TryGetValue(CorrelationIdHeader, out var correlationId))
        {
            correlationId = Guid.NewGuid().ToString();
            context.Request.Headers[CorrelationIdHeader] = correlationId;
        }

        return correlationId;
    }
} 