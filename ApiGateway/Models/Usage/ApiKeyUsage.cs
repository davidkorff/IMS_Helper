public class ApiKeyUsageInfo
{
    public string ApiKeyId { get; set; }
    public string AccountId { get; set; }
    public DateTime Timestamp { get; set; }
    public string Endpoint { get; set; }
    public string Method { get; set; }
    public int StatusCode { get; set; }
    public long? RequestSize { get; set; }
    public long? ResponseSize { get; set; }
    public TimeSpan Duration { get; set; }
    public string IpAddress { get; set; }
    public string UserAgent { get; set; }
}

public class ApiKeyUsageRecord : ApiKeyUsageInfo
{
    public string Id { get; set; }
}

public class ApiKeyUsageStats
{
    public long TotalRequests { get; set; }
    public long SuccessfulRequests { get; set; }
    public long FailedRequests { get; set; }
    public double AverageResponseTime { get; set; }
    public long TotalDataTransferred { get; set; }
    public Dictionary<string, long> EndpointUsage { get; set; }
    public Dictionary<int, long> StatusCodeDistribution { get; set; }
} 