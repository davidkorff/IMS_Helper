public class RateLimitPolicyConfig
{
    public string PathPattern { get; set; }
    public int RequestsPerMinute { get; set; }
    public int BurstLimit { get; set; }
    public string[] ExcludedPaths { get; set; } = Array.Empty<string>();
    public Dictionary<string, int> ClientSpecificLimits { get; set; } = 
        new Dictionary<string, int>();
    public RateLimitPenaltyConfig PenaltyConfig { get; set; }
    public bool Enabled { get; set; } = true;
    public DateTime? ExpiresAt { get; set; }
    public string[] RequiredScopes { get; set; } = Array.Empty<string>();
}

public class RateLimitPenaltyConfig
{
    public int ViolationThreshold { get; set; } = 3;
    public TimeSpan PenaltyDuration { get; set; } = TimeSpan.FromMinutes(5);
    public double PenaltyMultiplier { get; set; } = 2.0;
    public int MaxPenaltyMultiplier { get; set; } = 8;
}

public interface IRateLimitPolicyManager
{
    Task<IEnumerable<RateLimitPolicyConfig>> GetAllPoliciesAsync();
    Task<RateLimitPolicyConfig> GetPolicyAsync(string endpoint);
    Task AddOrUpdatePolicyAsync(RateLimitPolicyConfig policy);
    Task RemovePolicyAsync(string pathPattern);
    Task<bool> ValidatePolicyAsync(RateLimitPolicyConfig policy);
} 