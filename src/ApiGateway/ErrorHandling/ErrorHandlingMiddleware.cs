using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Http;
using System;
using System.Threading.Tasks;

public class ErrorHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ErrorHandlingMiddleware> _logger;
    private readonly IHostEnvironment _environment;

    public ErrorHandlingMiddleware(
        RequestDelegate next,
        ILogger<ErrorHandlingMiddleware> logger,
        IHostEnvironment environment)
    {
        _next = next;
        _logger = logger;
        _environment = environment;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An unhandled exception occurred");
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var error = MapExceptionToError(exception);
        context.Response.StatusCode = error.StatusCode;
        context.Response.ContentType = "application/json";

        var response = new ErrorResponse
        {
            TraceId = context.TraceIdentifier,
            Message = error.Message,
            Code = error.Code
        };

        if (_environment.IsDevelopment())
        {
            response.Details = new ErrorDetails
            {
                ExceptionType = exception.GetType().Name,
                StackTrace = exception.StackTrace,
                InnerException = exception.InnerException?.Message
            };
        }

        await context.Response.WriteAsJsonAsync(response);
    }

    private static ErrorInfo MapExceptionToError(Exception exception) => exception switch
    {
        ValidationException ex => new ErrorInfo(
            StatusCodes.Status400BadRequest,
            "VALIDATION_ERROR",
            ex.Message),
            
        UnauthorizedAccessException => new ErrorInfo(
            StatusCodes.Status401Unauthorized,
            "UNAUTHORIZED",
            "Authentication is required"),
            
        TokenValidationException ex => new ErrorInfo(
            StatusCodes.Status401Unauthorized,
            "INVALID_TOKEN",
            ex.Message),
            
        ForbiddenAccessException => new ErrorInfo(
            StatusCodes.Status403Forbidden,
            "FORBIDDEN",
            "Insufficient permissions"),
            
        NotFoundException => new ErrorInfo(
            StatusCodes.Status404NotFound,
            "NOT_FOUND",
            "The requested resource was not found"),
            
        IMSIntegrationException ex => new ErrorInfo(
            StatusCodes.Status502BadGateway,
            "IMS_ERROR",
            ex.Message),
            
        _ => new ErrorInfo(
            StatusCodes.Status500InternalServerError,
            "INTERNAL_ERROR",
            "An unexpected error occurred")
    };
}

public record ErrorInfo(int StatusCode, string Code, string Message);

public class ErrorResponse
{
    public string TraceId { get; set; }
    public string Message { get; set; }
    public string Code { get; set; }
    public ErrorDetails Details { get; set; }
}

public class ErrorDetails
{
    public string ExceptionType { get; set; }
    public string StackTrace { get; set; }
    public string InnerException { get; set; }
} 