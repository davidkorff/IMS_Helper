public interface IIMSRequestValidator<TRequest>
{
    Task<ValidationResult> ValidateAsync(TRequest request, ValidationContext context);
}

public class ValidationResult
{
    public bool IsValid => !Errors.Any();
    public List<ValidationError> Errors { get; } = new();
    public Dictionary<string, object> Metadata { get; } = new();

    public void AddError(string field, string code, string message)
    {
        Errors.Add(new ValidationError(field, code, message));
    }

    public void AddErrors(IEnumerable<ValidationError> errors)
    {
        Errors.AddRange(errors);
    }

    public void ThrowIfInvalid()
    {
        if (!IsValid)
        {
            throw new ValidationException(Errors);
        }
    }
}

public class ValidationError
{
    public string Field { get; }
    public string Code { get; }
    public string Message { get; }

    public ValidationError(string field, string code, string message)
    {
        Field = field;
        Code = code;
        Message = message;
    }
}

public class ValidationContext
{
    public string Operation { get; set; }
    public Dictionary<string, object> Parameters { get; set; }
    public IServiceProvider Services { get; set; }
    public CancellationToken CancellationToken { get; set; }
}

public class ValidationException : Exception
{
    public List<ValidationError> Errors { get; }

    public ValidationException(List<ValidationError> errors)
        : base("Validation failed")
    {
        Errors = errors;
    }
}

public abstract class BaseRequestValidator<TRequest> : IIMSRequestValidator<TRequest>
{
    private readonly ILogger _logger;
    private readonly IMSValidationSettings _settings;

    protected BaseRequestValidator(
        ILogger logger,
        IOptions<IMSValidationSettings> settings)
    {
        _logger = logger;
        _settings = settings.Value;
    }

    public async Task<ValidationResult> ValidateAsync(
        TRequest request, 
        ValidationContext context)
    {
        var result = new ValidationResult();

        try
        {
            // Null check
            if (request == null)
            {
                result.AddError("request", "VAL001", "Request cannot be null");
                return result;
            }

            // Basic property validation
            await ValidatePropertiesAsync(request, result, context);

            // Business rules validation
            if (result.IsValid)
            {
                await ValidateBusinessRulesAsync(request, result, context);
            }

            // Custom validation
            if (result.IsValid)
            {
                await ValidateCustomRulesAsync(request, result, context);
            }

            if (!result.IsValid)
            {
                _logger.LogWarning(
                    "Validation failed for {RequestType} with {ErrorCount} errors",
                    typeof(TRequest).Name,
                    result.Errors.Count);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, 
                "Error occurred during validation of {RequestType}",
                typeof(TRequest).Name);
            result.AddError("", "VAL999", 
                "An unexpected error occurred during validation");
        }

        return result;
    }

    protected abstract Task ValidatePropertiesAsync(
        TRequest request,
        ValidationResult result,
        ValidationContext context);

    protected abstract Task ValidateBusinessRulesAsync(
        TRequest request,
        ValidationResult result,
        ValidationContext context);

    protected virtual Task ValidateCustomRulesAsync(
        TRequest request,
        ValidationResult result,
        ValidationContext context)
    {
        return Task.CompletedTask;
    }

    protected void ValidateRequired(
        string field,
        object value,
        ValidationResult result)
    {
        if (value == null || (value is string str && string.IsNullOrWhiteSpace(str)))
        {
            result.AddError(field, "VAL002", $"{field} is required");
        }
    }

    protected void ValidateLength(
        string field,
        string value,
        int maxLength,
        ValidationResult result)
    {
        if (!string.IsNullOrEmpty(value) && value.Length > maxLength)
        {
            result.AddError(field, "VAL003", 
                $"{field} cannot exceed {maxLength} characters");
        }
    }

    protected void ValidateRange<T>(
        string field,
        T value,
        T min,
        T max,
        ValidationResult result) where T : IComparable<T>
    {
        if (value != null && (value.CompareTo(min) < 0 || value.CompareTo(max) > 0))
        {
            result.AddError(field, "VAL004", 
                $"{field} must be between {min} and {max}");
        }
    }

    protected void ValidatePattern(
        string field,
        string value,
        string pattern,
        string description,
        ValidationResult result)
    {
        if (!string.IsNullOrEmpty(value) && !Regex.IsMatch(value, pattern))
        {
            result.AddError(field, "VAL005", 
                $"{field} must match pattern: {description}");
        }
    }

    protected void ValidateEnum<T>(
        string field,
        string value,
        ValidationResult result) where T : struct
    {
        if (!string.IsNullOrEmpty(value) && !Enum.TryParse<T>(value, true, out _))
        {
            result.AddError(field, "VAL006", 
                $"{field} must be a valid {typeof(T).Name}");
        }
    }

    protected void ValidateDate(
        string field,
        DateTime? value,
        ValidationResult result,
        DateTime? minDate = null,
        DateTime? maxDate = null)
    {
        if (value.HasValue)
        {
            if (minDate.HasValue && value < minDate)
            {
                result.AddError(field, "VAL007", 
                    $"{field} cannot be earlier than {minDate:d}");
            }
            if (maxDate.HasValue && value > maxDate)
            {
                result.AddError(field, "VAL008", 
                    $"{field} cannot be later than {maxDate:d}");
            }
        }
    }
} 