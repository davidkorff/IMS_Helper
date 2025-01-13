public class IMSErrorMapping
{
    private static readonly Dictionary<string, ErrorMapDefinition> SoapToRestMap = new()
    {
        // Authentication Errors
        ["IMS.Auth.InvalidCredentials"] = new ErrorMapDefinition(
            HttpStatusCode.Unauthorized,
            "AUTH001",
            "Invalid credentials provided"),
        ["IMS.Auth.SessionExpired"] = new ErrorMapDefinition(
            HttpStatusCode.Unauthorized,
            "AUTH002",
            "Session has expired"),
        ["IMS.Auth.AccessDenied"] = new ErrorMapDefinition(
            HttpStatusCode.Forbidden,
            "AUTH003",
            "Access denied"),

        // Validation Errors
        ["IMS.Validation.InvalidRequest"] = new ErrorMapDefinition(
            HttpStatusCode.BadRequest,
            "VAL001",
            "Invalid request format"),
        ["IMS.Validation.MissingRequired"] = new ErrorMapDefinition(
            HttpStatusCode.BadRequest,
            "VAL002",
            "Required fields missing"),
        ["IMS.Validation.InvalidFormat"] = new ErrorMapDefinition(
            HttpStatusCode.BadRequest,
            "VAL003",
            "Invalid data format"),

        // Business Logic Errors
        ["IMS.Business.EntityNotFound"] = new ErrorMapDefinition(
            HttpStatusCode.NotFound,
            "BUS001",
            "Requested entity not found"),
        ["IMS.Business.DuplicateEntity"] = new ErrorMapDefinition(
            HttpStatusCode.Conflict,
            "BUS002",
            "Entity already exists"),
        ["IMS.Business.StateNotSupported"] = new ErrorMapDefinition(
            HttpStatusCode.UnprocessableEntity,
            "BUS003",
            "State not supported for this operation"),

        // System Errors
        ["IMS.System.ServiceUnavailable"] = new ErrorMapDefinition(
            HttpStatusCode.ServiceUnavailable,
            "SYS001",
            "IMS service is currently unavailable"),
        ["IMS.System.Timeout"] = new ErrorMapDefinition(
            HttpStatusCode.GatewayTimeout,
            "SYS002",
            "Request timed out"),
        ["IMS.System.InternalError"] = new ErrorMapDefinition(
            HttpStatusCode.InternalServerError,
            "SYS003",
            "Internal system error")
    };

    public static ErrorResponse MapSoapFaultToRestError(SoapFault fault)
    {
        var errorCode = ExtractErrorCode(fault);
        var errorMap = SoapToRestMap.GetValueOrDefault(errorCode) ?? 
            GetDefaultErrorMap(fault);

        return new ErrorResponse
        {
            StatusCode = errorMap.StatusCode,
            ErrorCode = errorMap.ErrorCode,
            Message = errorMap.Message,
            Details = ExtractErrorDetails(fault),
            TraceId = Activity.Current?.Id ?? "Unknown",
            Timestamp = DateTime.UtcNow
        };
    }

    private static string ExtractErrorCode(SoapFault fault)
    {
        // Extract error code from fault detail or message
        if (fault.Detail?.Elements()
            .FirstOrDefault(e => e.Name.LocalName == "ErrorCode")?.Value is string errorCode)
        {
            return errorCode;
        }

        // Fallback to pattern matching in fault message
        foreach (var mapping in SoapToRestMap)
        {
            if (fault.Message.Contains(mapping.Key, StringComparison.OrdinalIgnoreCase))
            {
                return mapping.Key;
            }
        }

        return "IMS.System.InternalError";
    }

    private static List<ErrorDetail> ExtractErrorDetails(SoapFault fault)
    {
        var details = new List<ErrorDetail>();

        if (fault.Detail != null)
        {
            // Extract validation errors
            var validationErrors = fault.Detail.Elements()
                .Where(e => e.Name.LocalName == "ValidationError")
                .Select(e => new ErrorDetail
                {
                    Field = e.Element("Field")?.Value,
                    Code = e.Element("Code")?.Value,
                    Message = e.Element("Message")?.Value
                });
            details.AddRange(validationErrors);

            // Extract business rule violations
            var businessErrors = fault.Detail.Elements()
                .Where(e => e.Name.LocalName == "BusinessError")
                .Select(e => new ErrorDetail
                {
                    Field = e.Element("Entity")?.Value,
                    Code = e.Element("Rule")?.Value,
                    Message = e.Element("Description")?.Value
                });
            details.AddRange(businessErrors);
        }

        // If no structured details, add the fault message
        if (!details.Any() && !string.IsNullOrEmpty(fault.Message))
        {
            details.Add(new ErrorDetail
            {
                Message = fault.Message
            });
        }

        return details;
    }

    private static ErrorMapDefinition GetDefaultErrorMap(SoapFault fault)
    {
        // Determine if it's likely a client error based on the fault
        var isClientError = fault.Message.Contains("invalid", StringComparison.OrdinalIgnoreCase) ||
                           fault.Message.Contains("missing", StringComparison.OrdinalIgnoreCase) ||
                           fault.Message.Contains("required", StringComparison.OrdinalIgnoreCase);

        return isClientError
            ? new ErrorMapDefinition(
                HttpStatusCode.BadRequest,
                "GEN001",
                "Invalid request")
            : new ErrorMapDefinition(
                HttpStatusCode.InternalServerError,
                "GEN002",
                "Internal server error");
    }
}

public class ErrorMapDefinition
{
    public HttpStatusCode StatusCode { get; }
    public string ErrorCode { get; }
    public string Message { get; }

    public ErrorMapDefinition(
        HttpStatusCode statusCode,
        string errorCode,
        string message)
    {
        StatusCode = statusCode;
        ErrorCode = errorCode;
        Message = message;
    }
}

public class ErrorResponse
{
    public HttpStatusCode StatusCode { get; set; }
    public string ErrorCode { get; set; }
    public string Message { get; set; }
    public List<ErrorDetail> Details { get; set; }
    public string TraceId { get; set; }
    public DateTime Timestamp { get; set; }
}

public class ErrorDetail
{
    public string Field { get; set; }
    public string Code { get; set; }
    public string Message { get; set; }
} 