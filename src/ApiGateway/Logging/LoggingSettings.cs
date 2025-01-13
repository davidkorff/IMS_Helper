public class LoggingSettings
{
    public bool LogRequestBody { get; set; } = true;
    public bool LogResponseBody { get; set; } = true;
    public int SlowRequestThresholdMs { get; set; } = 500;
    public List<string> ExcludedPaths { get; set; } = new()
    {
        "/health",
        "/metrics",
        "/favicon.ico"
    };
    public List<string> ExcludedContentTypes { get; set; } = new()
    {
        "application/octet-stream",
        "image/",
        "video/",
        "audio/"
    };
    public List<string> ExcludedHeaders { get; set; } = new()
    {
        "Authorization",
        "Cookie",
        "X-API-Key"
    };
    public LoggingLevelSettings LogLevels { get; set; } = new();
    public MetricsSettings Metrics { get; set; } = new();
    public SensitiveDataSettings SensitiveData { get; set; } = new();
}

public class LoggingLevelSettings
{
    public string Default { get; set; } = "Information";
    public string Microsoft { get; set; } = "Warning";
    public string System { get; set; } = "Warning";
    public Dictionary<string, string> CustomNamespaces { get; set; } = new();
}

public class MetricsSettings
{
    public bool Enabled { get; set; } = true;
    public int FlushIntervalSeconds { get; set; } = 15;
    public List<string> Tags { get; set; } = new();
}

public class SensitiveDataSettings
{
    public List<string> FieldNames { get; set; } = new()
    {
        "password",
        "ssn",
        "creditCard",
        "accountNumber"
    };
    public List<string> Patterns { get; set; } = new()
    {
        @"\b\d{16}\b", // Credit card numbers
        @"\b\d{3}-\d{2}-\d{4}\b" // SSN pattern
    };
    public string MaskCharacter { get; set; } = "*";
    public bool EnableRegexScanning { get; set; } = true;
} 