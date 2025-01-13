public class ApiErrorResponse
{
    public string Code { get; set; }
    public string Message { get; set; }
    public string TraceId { get; set; }
    public object Details { get; set; }
}

public class ValidationException : Exception
{
    public IEnumerable<ValidationError> Errors { get; }

    public ValidationException(IEnumerable<ValidationError> errors)
    {
        Errors = errors;
    }
}

public class ValidationError
{
    public string Field { get; set; }
    public string Message { get; set; }
} 