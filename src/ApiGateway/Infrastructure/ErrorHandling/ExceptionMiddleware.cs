using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Http;
using System;
using System.Threading.Tasks;
using System.Diagnostics;
using ApiGateway.Infrastructure.ErrorHandling;

public class ExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionMiddleware> _logger;
    private readonly IHostEnvironment _environment;

    public ExceptionMiddleware(
        RequestDelegate next,
        ILogger<ExceptionMiddleware> logger,
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
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        _logger.LogError(exception, "An unhandled exception occurred");

        var response = context.Response;
        response.ContentType = "application/json";
        
        var errorResponse = new ApiErrorResponse
        {
            TraceId = Activity.Current?.Id ?? context.TraceIdentifier
        };

        switch (exception)
        {
            case IMSException imsEx:
                response.StatusCode = StatusCodes.Status400BadRequest;
                errorResponse.Message = imsEx.BusinessMessage;
                errorResponse.Code = imsEx.ErrorCode;
                break;

            case TokenException tokenEx:
                response.StatusCode = StatusCodes.Status401Unauthorized;
                errorResponse.Message = "Authentication failed";
                errorResponse.Code = "AUTH_ERROR";
                break;

            case ValidationException valEx:
                response.StatusCode = StatusCodes.Status400BadRequest;
                errorResponse.Message = "Validation failed";
                errorResponse.Code = "VALIDATION_ERROR";
                errorResponse.Details = valEx.Errors;
                break;

            default:
                response.StatusCode = StatusCodes.Status500InternalServerError;
                errorResponse.Message = _environment.IsDevelopment() 
                    ? exception.Message 
                    : "An unexpected error occurred";
                errorResponse.Code = "INTERNAL_ERROR";
                break;
        }

        await response.WriteAsJsonAsync(errorResponse);
    }
} 