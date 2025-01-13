using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using Serilog.Core;
using Serilog.Events;

public class IMSLoggingEnricher : ILogEventEnricher
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private static readonly HashSet<string> SensitiveProperties = new()
    {
        "password",
        "credentials",
        "token",
        "secret"
    };

    public IMSLoggingEnricher(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        var context = _httpContextAccessor.HttpContext;
        if (context == null) return;

        // Add correlation ID
        var correlationId = context.Request.Headers["X-Correlation-ID"].FirstOrDefault()
            ?? context.TraceIdentifier;
        logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty(
            "CorrelationId", correlationId));

        // Add user context if available
        if (context.User?.Identity?.IsAuthenticated == true)
        {
            logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty(
                "UserId", context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value));
            logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty(
                "UserEmail", context.User.FindFirst(ClaimTypes.Email)?.Value));
        }

        // Add IMS connection context if available
        var imsConnectionId = context.Request.Headers["X-IMS-Connection-ID"].FirstOrDefault();
        if (!string.IsNullOrEmpty(imsConnectionId))
        {
            logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty(
                "IMSConnectionId", imsConnectionId));
        }

        // Mask sensitive data
        foreach (var property in logEvent.Properties)
        {
            if (SensitiveProperties.Contains(property.Key.ToLower()))
            {
                logEvent.RemovePropertyIfPresent(property.Key);
                logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty(
                    property.Key, "***REDACTED***"));
            }
        }
    }
} 