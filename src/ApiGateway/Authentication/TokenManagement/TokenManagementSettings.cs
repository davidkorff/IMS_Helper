public class TokenManagementSettings
{
    public int AccessTokenExpirationMinutes { get; set; } = 60;
    public int RefreshTokenExpirationDays { get; set; } = 7;
    public int RevokedTokenRetentionHours { get; set; } = 24;
    public int MaxActiveTokensPerUser { get; set; } = 5;
    public int TokenCleanupIntervalMinutes { get; set; } = 60;
    public bool EnableTokenCleanup { get; set; } = true;
    public TokenStorageOptions StorageOptions { get; set; }
    public TokenMonitoringOptions MonitoringOptions { get; set; }
}

public class TokenStorageOptions
{
    public string StorageProvider { get; set; } = "Redis";
    public int CacheExpirationMinutes { get; set; } = 60;
    public bool EnableCompression { get; set; } = true;
    public int MaxCacheSize { get; set; } = 10000;
    public RetryOptions RetryOptions { get; set; }
}

public class TokenMonitoringOptions
{
    public bool EnableUsageTracking { get; set; } = true;
    public bool TrackLocationInfo { get; set; } = true;
    public bool TrackDeviceInfo { get; set; } = true;
    public int UsageRetentionDays { get; set; } = 30;
    public List<string> ExcludedIpRanges { get; set; }
}

public class RetryOptions
{
    public int MaxRetries { get; set; } = 3;
    public int RetryDelayMilliseconds { get; set; } = 1000;
    public bool ExponentialBackoff { get; set; } = true;
} 