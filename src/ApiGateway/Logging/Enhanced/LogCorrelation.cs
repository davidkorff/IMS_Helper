using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Threading;

public class LogCorrelation : ILogCorrelation
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly AsyncLocal<string> _correlationId;
    private readonly AsyncLocal<Dictionary<string, object>> _baggage;

    public LogCorrelation(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
        _correlationId = new AsyncLocal<string>();
        _baggage = new AsyncLocal<Dictionary<string, object>>();
    }

    public string CorrelationId
    {
        get => _correlationId.Value ??= CreateCorrelationId();
        set => _correlationId.Value = value;
    }

    public IReadOnlyDictionary<string, object> Baggage => 
        _baggage.Value ??= new Dictionary<string, object>();

    public void AddBaggageItem(string key, object value)
    {
        var baggage = _baggage.Value ??= new Dictionary<string, object>();
        baggage[key] = value;
    }

    public T GetBaggageItem<T>(string key)
    {
        var baggage = _baggage.Value;
        if (baggage != null && baggage.TryGetValue(key, out var value))
        {
            return (T)value;
        }
        return default;
    }

    private string CreateCorrelationId()
    {
        var context = _httpContextAccessor.HttpContext;
        if (context == null) return Guid.NewGuid().ToString();

        const string CorrelationHeader = "X-Correlation-ID";
        
        if (context.Request.Headers.TryGetValue(CorrelationHeader, out var existingId))
        {
            return existingId;
        }

        var newId = Guid.NewGuid().ToString();
        context.Request.Headers[CorrelationHeader] = newId;
        return newId;
    }
} 