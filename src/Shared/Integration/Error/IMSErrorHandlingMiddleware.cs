using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Http;
using System;
using System.Threading.Tasks;
using System.Net;
using System.Diagnostics;
using System.Text;
using System.Text.Json;

public class IMSErrorHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<IMSErrorHandlingMiddleware> _logger;
    private readonly bool _includeDetails;

    public IMSErrorHandlingMiddleware(
        RequestDelegate next,
        ILogger<IMSErrorHandlingMiddleware> logger,
        bool includeDetails = false)
    {
        _next = next;
        _logger = logger;
        _includeDetails = includeDetails;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred processing the request");
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        ErrorResponse error;

        switch (exception)
        {
            case SoapException soapEx:
                error = IMSErrorMapping.MapSoapFaultToRestError(soapEx.Fault);
                break;

            case IMSException imsEx:
                error = new ErrorResponse
                {
                    StatusCode = imsEx.StatusCode,
                    ErrorCode = imsEx.ErrorCode,
                    Message = imsEx.Message,
                    Details = imsEx.Details,
                    TraceId = Activity.Current?.Id ?? context.TraceIdentifier,
                    Timestamp = DateTime.UtcNow
                };
                break;

            default:
                error = new ErrorResponse
                {
                    StatusCode = HttpStatusCode.InternalServerError,
                    ErrorCode = "SYS003",
                    Message = _includeDetails ? exception.Message : "An unexpected error occurred",
                    TraceId = Activity.Current?.Id ?? context.TraceIdentifier,
                    Timestamp = DateTime.UtcNow
                };
                break;
        }

        context.Response.ContentType = "application/json";
        context.Response.StatusCode = (int)error.StatusCode;

        var json = JsonSerializer.Serialize(error, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        });

        await context.Response.WriteAsync(json);
    }
}

// Extension method to make it easier to add the middleware
public static class IMSErrorHandlingMiddlewareExtensions
{
    public static IApplicationBuilder UseIMSErrorHandling(
        this IApplicationBuilder builder,
        bool includeDetails = false)
    {
        return builder.UseMiddleware<IMSErrorHandlingMiddleware>(includeDetails);
    }
} 