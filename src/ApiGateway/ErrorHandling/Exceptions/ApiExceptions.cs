public class ApiException : Exception
{
    public string Code { get; }

    protected ApiException(string message, string code) : base(message)
    {
        Code = code;
    }

    protected ApiException(string message, string code, Exception inner) 
        : base(message, inner)
    {
        Code = code;
    }
}

public class ValidationException : ApiException
{
    public ValidationException(string message) 
        : base(message, "VALIDATION_ERROR")
    {
    }

    public ValidationException(string message, Exception inner) 
        : base(message, "VALIDATION_ERROR", inner)
    {
    }
}

public class TokenValidationException : ApiException
{
    public TokenValidationException(string message) 
        : base(message, "INVALID_TOKEN")
    {
    }

    public TokenValidationException(string message, Exception inner) 
        : base(message, "INVALID_TOKEN", inner)
    {
    }
}

public class ForbiddenAccessException : ApiException
{
    public ForbiddenAccessException(string message) 
        : base(message, "FORBIDDEN")
    {
    }

    public ForbiddenAccessException(string message, Exception inner) 
        : base(message, "FORBIDDEN", inner)
    {
    }
}

public class NotFoundException : ApiException
{
    public NotFoundException(string message) 
        : base(message, "NOT_FOUND")
    {
    }

    public NotFoundException(string message, Exception inner) 
        : base(message, "NOT_FOUND", inner)
    {
    }
}

public class IMSIntegrationException : ApiException
{
    public IMSIntegrationException(string message) 
        : base(message, "IMS_ERROR")
    {
    }

    public IMSIntegrationException(string message, Exception inner) 
        : base(message, "IMS_ERROR", inner)
    {
    }
} 