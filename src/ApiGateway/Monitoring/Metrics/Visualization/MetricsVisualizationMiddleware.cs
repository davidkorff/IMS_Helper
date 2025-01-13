using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Caching.Memory;
using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Builder;

public class MetricsVisualizationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<MetricsVisualizationMiddleware> _logger;
    private readonly MetricsRegistry _metrics;
    private readonly IMemoryCache _cache;

    public MetricsVisualizationMiddleware(
        RequestDelegate next,
        ILogger<MetricsVisualizationMiddleware> logger,
        MetricsRegistry metrics,
        IMemoryCache cache)
    {
        _next = next;
        _logger = logger;
        _metrics = metrics;
        _cache = cache;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.Request.Path.StartsWithSegments("/metrics-ui"))
        {
            await HandleMetricsUIRequest(context);
            return;
        }

        await _next(context);
    }

    private async Task HandleMetricsUIRequest(HttpContext context)
    {
        var data = await GetMetricsData();
        var html = GenerateMetricsHtml(data);
        
        context.Response.ContentType = "text/html";
        await context.Response.WriteAsync(html);
    }

    private async Task<MetricsData> GetMetricsData()
    {
        return await _cache.GetOrCreateAsync(
            "metrics_visualization",
            async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(15);

                return new MetricsData
                {
                    RequestMetrics = await GetRequestMetrics(),
                    CacheMetrics = await GetCacheMetrics(),
                    SystemMetrics = await GetSystemMetrics(),
                    RateLimitMetrics = await GetRateLimitMetrics(),
                    TransformationMetrics = await GetTransformationMetrics()
                };
            });
    }

    private string GenerateMetricsHtml(MetricsData data)
    {
        return $@"
<!DOCTYPE html>
<html>
<head>
    <title>API Gateway Metrics</title>
    <link rel='stylesheet' href='/css/metrics-ui.css'>
    <script src='https://cdn.plot.ly/plotly-latest.min.js'></script>
</head>
<body>
    <div class='metrics-dashboard'>
        <div class='header'>
            <h1>API Gateway Metrics</h1>
            <span class='timestamp'>Last updated: {DateTime.Now:HH:mm:ss}</span>
        </div>
        
        <div class='metrics-grid'>
            {GenerateRequestMetricsHtml(data.RequestMetrics)}
            {GenerateCacheMetricsHtml(data.CacheMetrics)}
            {GenerateSystemMetricsHtml(data.SystemMetrics)}
            {GenerateRateLimitMetricsHtml(data.RateLimitMetrics)}
        </div>

        <div class='charts-container'>
            {GenerateRequestChart(data.RequestMetrics)}
            {GenerateLatencyChart(data.RequestMetrics)}
            {GenerateErrorChart(data.RequestMetrics)}
        </div>
    </div>

    <script>
        {GenerateRefreshScript()}
    </script>
</body>
</html>";
    }

    private string GenerateRequestChart(RequestMetricsData data)
    {
        var chartData = new
        {
            x = data.TimePoints,
            y = data.RequestCounts,
            type = "scatter",
            name = "Requests"
        };

        return $@"
<div class='chart-card'>
    <h3>Request Rate</h3>
    <div id='requestChart'></div>
    <script>
        Plotly.newPlot('requestChart', [{JsonSerializer.Serialize(chartData)}], {{
            title: 'Requests per Minute',
            height: 300,
            margin: {{ t: 30, r: 30, b: 40, l: 50 }}
        }});
    </script>
</div>";
    }

    // Additional helper methods for generating specific metrics sections
    // and charts have been omitted for brevity
}

public class MetricsData
{
    public RequestMetricsData RequestMetrics { get; set; }
    public CacheMetricsData CacheMetrics { get; set; }
    public SystemMetricsData SystemMetrics { get; set; }
    public RateLimitMetricsData RateLimitMetrics { get; set; }
    public TransformationMetricsData TransformationMetrics { get; set; }
} 