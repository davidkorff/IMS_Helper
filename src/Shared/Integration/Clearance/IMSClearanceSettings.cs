public class IMSClearanceSettings
{
    public int CacheExpirationMinutes { get; set; } = 60;
    public ClearanceEngineSettings EngineSettings { get; set; }
    public ValidationSettings ValidationSettings { get; set; }
    public BlockerSettings BlockerSettings { get; set; }
    public OverrideSettings OverrideSettings { get; set; }
    public RetryPolicy RetryPolicy { get; set; }
}

public class ClearanceEngineSettings
{
    public bool EnableParallelProcessing { get; set; } = true;
    public int MaxConcurrentClearances { get; set; } = 5;
    public int ClearanceTimeoutSeconds { get; set; } = 300;
    public bool BypassCache { get; set; } = false;
    public bool RequireApprovalForOverrides { get; set; } = true;
    public List<string> AutoApprovalCriteria { get; set; }
    public Dictionary<string, ClearanceTypeSettings> ClearanceTypeConfigs { get; set; }
}

public class ValidationSettings
{
    public bool StrictValidation { get; set; } = true;
    public List<string> RequiredFields { get; set; }
    public Dictionary<string, ValidationRule> CustomValidationRules { get; set; }
    public bool ValidateEffectiveDate { get; set; } = true;
    public bool ValidateAmount { get; set; } = true;
    public int MaxRetryAttempts { get; set; } = 3;
}

public class BlockerSettings
{
    public bool EnableCustomBlockers { get; set; } = true;
    public Dictionary<string, BlockerConfig> BlockerConfigs { get; set; }
    public List<string> CriticalBlockerTypes { get; set; }
    public bool RequireDocumentsForOverride { get; set; } = true;
    public int BlockerExpirationDays { get; set; } = 30;
}

public class OverrideSettings
{
    public Dictionary<string, List<string>> OverridePermissions { get; set; }
    public bool RequireComments { get; set; } = true;
    public bool RequireApproval { get; set; } = true;
    public List<string> AutoApprovalBlockers { get; set; }
    public int OverrideExpirationDays { get; set; } = 90;
}

public class ClearanceTypeSettings
{
    public bool RequiresApproval { get; set; }
    public bool RequiresDocuments { get; set; }
    public List<string> RequiredDocuments { get; set; }
    public Dictionary<string, ValidationRule> ValidationRules { get; set; }
    public List<string> AllowedOverrides { get; set; }
}

public class BlockerConfig
{
    public string Type { get; set; }
    public string Severity { get; set; }
    public bool IsOverridable { get; set; }
    public string OverrideLevel { get; set; }
    public List<string> RequiredDocuments { get; set; }
    public int ExpirationDays { get; set; }
}

public class ValidationRule
{
    public string Expression { get; set; }
    public string ErrorMessage { get; set; }
    public string ErrorCode { get; set; }
    public bool IsBlocking { get; set; }
    public int Priority { get; set; }
} 