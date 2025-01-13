public class IMSValidationSettings
{
    public bool EnableCaching { get; set; } = true;
    public int CacheExpirationMinutes { get; set; } = 60;
    public ValidationBehavior Behavior { get; set; }
    public Dictionary<string, RequestTypeSettings> RequestTypes { get; set; }
    public List<string> GloballyRequiredFields { get; set; }
    public Dictionary<string, ValidationRule> GlobalRules { get; set; }
}

public class ValidationBehavior
{
    public bool StopOnFirstFailure { get; set; } = false;
    public bool ValidateAllProperties { get; set; } = true;
    public bool ThrowOnFailure { get; set; } = true;
    public bool EnableAsyncValidation { get; set; } = true;
    public int AsyncTimeoutSeconds { get; set; } = 30
    public ValidationMode Mode { get; set; } = ValidationMode.Strict;
}

public class RequestTypeSettings
{
    public bool Enabled { get; set; } = true;
    public ValidationMode Mode { get; set; }
    public List<string> RequiredFields { get; set; }
    public Dictionary<string, FieldValidation> FieldRules { get; set; }
    public List<string> DependentFields { get; set; }
    public Dictionary<string, string> CustomMessages { get; set; }
}

public class FieldValidation
{
    public bool Required { get; set; }
    public int? MinLength { get; set; }
    public int? MaxLength { get; set; }
    public string Pattern { get; set; }
    public string Format { get; set; }
    public object MinValue { get; set; }
    public object MaxValue { get; set; }
    public List<string> AllowedValues { get; set; }
    public Dictionary<string, object> CustomRules { get; set; }
}

public class ValidationRule
{
    public string Expression { get; set; }
    public string ErrorMessage { get; set; }
    public string ErrorCode { get; set; }
    public bool IsBlocking { get; set; }
    public int Priority { get; set; }
}

public enum ValidationMode
{
    Strict,
    Lenient,
    Custom
} 