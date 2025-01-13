public class IMSTransformSettings
{
    public bool EnableCaching { get; set; } = true;
    public int CacheExpirationMinutes { get; set; } = 60;
    public TransformationOptions Options { get; set; }
    public Dictionary<string, TypeTransformSettings> TypeSettings { get; set; }
    public ValidationSettings ValidationSettings { get; set; }
    public CultureSettings CultureSettings { get; set; }
}

public class TransformationOptions
{
    public bool StrictMapping { get; set; } = true;
    public bool IgnoreCase { get; set; } = true;
    public bool TrimStrings { get; set; } = true;
    public bool HandleNullValues { get; set; } = true;
    public string DateFormat { get; set; } = "yyyy-MM-dd";
    public string TimeFormat { get; set; } = "HH:mm:ss";
    public string NumberFormat { get; set; } = "0.00";
    public List<string> IgnoredFields { get; set; }
}

public class TypeTransformSettings
{
    public string XmlNamespace { get; set; }
    public Dictionary<string, string> PropertyMappings { get; set; }
    public List<string> RequiredProperties { get; set; }
    public Dictionary<string, string> DefaultValues { get; set; }
    public List<string> ExcludedProperties { get; set; }
}

public class ValidationSettings
{
    public bool ValidateTransformedData { get; set; } = true;
    public bool ThrowOnValidationError { get; set; } = true;
    public List<string> RequiredFields { get; set; }
    public Dictionary<string, string> ValidationRules { get; set; }
}

public class CultureSettings
{
    public string DefaultCulture { get; set; } = "en-US";
    public Dictionary<string, string> CultureMappings { get; set; }
    public bool UseInvariantCulture { get; set; } = false;
} 