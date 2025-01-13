public class IMSRatingSettings
{
    public int CacheExpirationMinutes { get; set; } = 60;
    public RatingEngineSettings EngineSettings { get; set; }
    public ValidationSettings ValidationSettings { get; set; }
    public DiscountSettings DiscountSettings { get; set; }
    public WorksheetSettings WorksheetSettings { get; set; }
    public RetryPolicy RetryPolicy { get; set; }
}

public class RatingEngineSettings
{
    public bool EnableParallelProcessing { get; set; } = true;
    public int MaxConcurrentCalculations { get; set; } = 5
    public int CalculationTimeoutSeconds { get; set; } = 30;
    public bool EnableRoundingRules { get; set; } = true;
    public int DecimalPrecision { get; set; } = 2;
    public string DefaultRateVersion { get; set; } = "Current";
    public Dictionary<string, string> StateRateVersions { get; set; }
    public List<string> SupportedLineOfBusiness { get; set; }
}

public class ValidationSettings
{
    public bool StrictValidation { get; set; } = true;
    public bool ValidateFactorRanges { get; set; } = true;
    public bool ValidateStateAvailability { get; set; } = true;
    public bool ValidateEffectiveDates { get; set; } = true;
    public int MaxFactorValue { get; set; } = 5;
    public Dictionary<string, ValidationRule> CustomValidationRules { get; set; }
}

public class DiscountSettings
{
    public bool AutoApplyEligibleDiscounts { get; set; } = false;
    public decimal MaxCombinedDiscountPercentage { get; set; } = 40;
    public bool RequireDocumentation { get; set; } = true;
    public List<string> RestrictedDiscounts { get; set; }
    public Dictionary<string, decimal> DiscountLimits { get; set; }
}

public class WorksheetSettings
{
    public bool IncludeDetailedSteps { get; set; } = true;
    public bool ShowFactorDetails { get; set; } = true;
    public bool IncludeMetadata { get; set; } = false;
    public List<string> ExcludedComponents { get; set; }
    public Dictionary<string, string> ComponentMapping { get; set; }
}

public class ValidationRule
{
    public string Expression { get; set; }
    public string ErrorMessage { get; set; }
    public string ErrorCode { get; set; }
    public bool IsWarning { get; set; }
    public int Priority { get; set; }
} 