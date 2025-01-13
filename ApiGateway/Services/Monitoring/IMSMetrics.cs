public class IMSMetrics
{
    private readonly IMetricsRoot _metrics;
    private readonly IMeterFactory _meterFactory;

    public IMSMetrics(IMetricsRoot metrics)
    {
        _metrics = metrics;
        _meterFactory = metrics.Meter("IMS");

        ConfigureMetrics();
    }

    private Counter<long> RequestCounter { get; set; }
    private Histogram<double> ResponseTimeHistogram { get; set; }
    private Counter<long> ErrorCounter { get; set; }
    private Gauge<long> ActiveConnectionsGauge { get; set; }

    private void ConfigureMetrics()
    {
        RequestCounter = _meterFactory.CreateCounter<long>("ims_requests_total", 
            "Total number of IMS requests");
        
        ResponseTimeHistogram = _meterFactory.CreateHistogram<double>("ims_response_time_seconds", 
            "IMS request response time");
        
        ErrorCounter = _meterFactory.CreateCounter<long>("ims_errors_total", 
            "Total number of IMS errors");
        
        ActiveConnectionsGauge = _meterFactory.CreateGauge<long>("ims_active_connections", 
            "Number of active IMS connections");
    }

    public void RecordRequest(string endpoint, string method)
    {
        RequestCounter.Add(1, new KeyValuePair<string, object>[] {
            new("endpoint", endpoint),
            new("method", method)
        });
    }

    public void RecordResponseTime(string endpoint, TimeSpan duration)
    {
        ResponseTimeHistogram.Record(duration.TotalSeconds, new KeyValuePair<string, object>[] {
            new("endpoint", endpoint)
        });
    }

    public void RecordError(string endpoint, string errorType)
    {
        ErrorCounter.Add(1, new KeyValuePair<string, object>[] {
            new("endpoint", endpoint),
            new("error_type", errorType)
        });
    }

    public void SetActiveConnections(long count)
    {
        ActiveConnectionsGauge.Set(count);
    }
} 