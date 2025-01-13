public class IMSSettings
{
    public string BaseUrl { get; set; }
    public string ProgramCode { get; set; }
    public string ClientId { get; set; }
    public int TokenExpirationMinutes { get; set; } = 60;
    public RetryPolicy RetryPolicy { get; set; }
}

public class RetryPolicy
{
    public int MaxRetries { get; set; } = 3;
    public int DelayMilliseconds { get; set; } = 1000;
    public bool ExponentialBackoff { get; set; } = true;
} 