using NBomber.Contracts;
using NBomber.Plugins.Network.Ping;
using Prometheus;
using Serilog;
using Serilog.Events;
using System.Diagnostics;
using System.Diagnostics.Metrics;

public class IMSLoadTestReporting
{
    private static readonly Counter RequestCounter = Metrics
        .CreateCounter("ims_requests_total", "Total number of IMS requests", 
            new CounterConfiguration
            {
                LabelNames = new[] { "endpoint", "status" }
            });

    private static readonly Histogram ResponseTime = Metrics
        .CreateHistogram("ims_response_time_seconds", 
            "Histogram of IMS response times",
            new HistogramConfiguration
            {
                LabelNames = new[] { "endpoint" },
                Buckets = new[] { 0.1, 0.2, 0.5, 1, 2, 5, 10 }
            });

    private static readonly Gauge ActiveUsers = Metrics
        .CreateGauge("ims_active_users", 
            "Number of active virtual users");

    private static readonly Meter Meter = new(
        "IMSLoadTest", 
        "1.0.0");

    private static readonly Counter<int> ErrorCounter = Meter.CreateCounter<int>(
        "ims_errors_total",
        description: "Total number of errors");

    public static ILogger CreateLogger()
    {
        return new LoggerConfiguration()
            .MinimumLevel.Information()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .WriteTo.Console()
            .WriteTo.File("logs/ims_load_test_.log",
                rollingInterval: RollingInterval.Hour,
                retainedFileCountLimit: 72)
            .WriteTo.Elasticsearch(new ElasticsearchSinkOptions(
                new Uri("http://localhost:9200"))
            {
                AutoRegisterTemplate = true,
                IndexFormat = "ims-loadtest-{0:yyyy.MM.dd}"
            })
            .Enrich.WithThreadId()
            .Enrich.WithEnvironmentName()
            .CreateLogger();
    }

    public static IReportingPlugin[] CreateReportingPlugins()
    {
        return new IReportingPlugin[]
        {
            new CustomReportingPlugin(),
            new PrometheusReportingPlugin(),
            new ElasticSearchReportingPlugin()
        };
    }

    public static void TrackRequest(string endpoint, int statusCode, TimeSpan duration)
    {
        var status = statusCode < 400 ? "success" : "failure";
        RequestCounter.Labels(endpoint, status).Inc();
        ResponseTime.Labels(endpoint).Observe(duration.TotalSeconds);
    }

    public static void UpdateActiveUsers(int count)
    {
        ActiveUsers.Set(count);
    }

    public static void TrackError(string endpoint, Exception ex)
    {
        ErrorCounter.Add(1);
        Log.Error(ex, "Error during load test at endpoint: {Endpoint}", endpoint);
    }
}

public class CustomReportingPlugin : IReportingPlugin
{
    private readonly Stopwatch _stopwatch = new();
    private readonly List<ScenarioStats> _stats = new();

    public string Name => "CustomReportingPlugin";

    public Task StartTest()
    {
        _stopwatch.Start();
        return Task.CompletedTask;
    }

    public Task StopTest()
    {
        _stopwatch.Stop();
        GenerateCustomReport();
        return Task.CompletedTask;
    }

    public Task Report(ScenarioStats stats)
    {
        _stats.Add(stats);
        return Task.CompletedTask;
    }

    private void GenerateCustomReport()
    {
        var report = new
        {
            TestDuration = _stopwatch.Elapsed,
            TotalRequests = _stats.Sum(s => s.Ok.Request.Count),
            FailedRequests = _stats.Sum(s => s.Fail.Request.Count),
            AverageResponseTime = _stats.Average(s => s.Ok.Latency.Mean),
            Percentiles = _stats.Select(s => new
            {
                Scenario = s.ScenarioName,
                P50 = s.Ok.Latency.Percent50,
                P75 = s.Ok.Latency.Percent75,
                P95 = s.Ok.Latency.Percent95,
                P99 = s.Ok.Latency.Percent99
            }).ToList()
        };

        File.WriteAllText(
            $"reports/custom_report_{DateTime.Now:yyyyMMddHHmmss}.json",
            JsonSerializer.Serialize(report, new JsonSerializerOptions 
            { 
                WriteIndented = true 
            }));
    }
}

public class PrometheusReportingPlugin : IReportingPlugin
{
    private readonly MetricServer _metricServer;

    public PrometheusReportingPlugin()
    {
        _metricServer = new MetricServer(port: 9091);
    }

    public string Name => "PrometheusReportingPlugin";

    public Task StartTest()
    {
        _metricServer.Start();
        return Task.CompletedTask;
    }

    public Task StopTest()
    {
        _metricServer.Stop();
        return Task.CompletedTask;
    }

    public Task Report(ScenarioStats stats)
    {
        // Metrics are handled by static counters/gauges
        return Task.CompletedTask;
    }
}

public class ElasticSearchReportingPlugin : IReportingPlugin
{
    private readonly ElasticClient _client;
    private readonly string _indexName;

    public ElasticSearchReportingPlugin()
    {
        var settings = new ConnectionSettings(
            new Uri("http://localhost:9200"))
                .DefaultIndex("ims-loadtest-stats");

        _client = new ElasticClient(settings);
        _indexName = $"ims-loadtest-{DateTime.Now:yyyy.MM.dd}";
    }

    public string Name => "ElasticSearchReportingPlugin";

    public Task StartTest()
    {
        return Task.CompletedTask;
    }

    public Task StopTest()
    {
        return Task.CompletedTask;
    }

    public async Task Report(ScenarioStats stats)
    {
        var document = new
        {
            timestamp = DateTime.UtcNow,
            scenarioName = stats.ScenarioName,
            okCount = stats.Ok.Request.Count,
            failCount = stats.Fail.Request.Count,
            meanResponseTime = stats.Ok.Latency.Mean,
            p95ResponseTime = stats.Ok.Latency.Percent95,
            rps = stats.Ok.Request.RPS
        };

        await _client.IndexDocumentAsync(document);
    }
} 