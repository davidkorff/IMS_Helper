public class IMSUnderwritingSettings
{
    public int CacheExpirationMinutes { get; set; } = 60;
    public bool EnableStrictValidation { get; set; } = true;
    public RetryPolicy RetryPolicy { get; set; }
    public RuleEvaluationSettings RuleEvaluation { get; set; }
    public List<string> RestrictedRules { get; set; }
    public Dictionary<string, int> SeverityThresholds { get; set; }
}

public class RuleEvaluationSettings
{
    public int MaxConcurrentEvaluations { get; set; } = 5;
    public int EvaluationTimeoutSeconds { get; set; } = 30;
    public bool CacheExpressions { get; set; } = true;
    public List<string> DisabledRules { get; set; }
    public Dictionary<string, string> DefaultValues { get; set; }
}

public class RetryPolicy
{
    public int MaxRetries { get; set; } = 3;
    public int DelayMilliseconds { get; set; } = 1000;
    public bool ExponentialBackoff { get; set; } = true;
} 