using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

public class TransformationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<TransformationMiddleware> _logger;
    private readonly ITransformationService _transformationService;
    private readonly TransformationOptions _options;

    public TransformationMiddleware(
        RequestDelegate next,
        ILogger<TransformationMiddleware> logger,
        ITransformationService transformationService,
        IOptions<TransformationOptions> options)
    {
        _next = next;
        _logger = logger;
        _transformationService = transformationService;
        _options = options.Value;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var originalBody = context.Request.Body;
        var originalResponse = context.Response.Body;

        try
        {
            // Transform request if needed
            if (ShouldTransformRequest(context))
            {
                using var requestMemoryStream = new MemoryStream();
                await context.Request.Body.CopyToAsync(requestMemoryStream);
                requestMemoryStream.Position = 0;

                var transformedRequest = await _transformationService
                    .TransformRequestAsync(
                        requestMemoryStream, 
                        context.Request.Path,
                        context.Request.ContentType);

                var transformedRequestStream = new MemoryStream();
                await transformedRequest.CopyToAsync(transformedRequestStream);
                transformedRequestStream.Position = 0;
                context.Request.Body = transformedRequestStream;
            }

            // Capture response for transformation
            using var responseMemoryStream = new MemoryStream();
            context.Response.Body = responseMemoryStream;

            // Continue pipeline
            await _next(context);

            // Transform response if needed
            if (ShouldTransformResponse(context))
            {
                responseMemoryStream.Position = 0;
                var transformedResponse = await _transformationService
                    .TransformResponseAsync(
                        responseMemoryStream,
                        context.Request.Path,
                        context.Response.ContentType);

                context.Response.Body = originalResponse;
                await transformedResponse.CopyToAsync(context.Response.Body);
            }
            else
            {
                // No transformation needed, copy original response
                responseMemoryStream.Position = 0;
                await responseMemoryStream.CopyToAsync(originalResponse);
            }
        }
        catch (TransformationException ex)
        {
            _logger.LogError(ex, 
                "Error during {TransformationType} transformation for {Path}",
                ex.TransformationType,
                context.Request.Path);

            context.Response.StatusCode = StatusCodes.Status422UnprocessableEntity;
            context.Response.Body = originalResponse;

            var error = new TransformationErrorResponse
            {
                Message = "Error transforming request/response",
                Details = ex.Message,
                Path = context.Request.Path
            };

            await JsonSerializer.SerializeAsync(
                context.Response.Body,
                error);
        }
        finally
        {
            context.Request.Body = originalBody;
            context.Response.Body = originalResponse;
        }
    }

    private bool ShouldTransformRequest(HttpContext context)
    {
        if (_options.ExcludedPaths.Any(p => 
            context.Request.Path.StartsWithSegments(p)))
            return false;

        if (string.IsNullOrEmpty(context.Request.ContentType))
            return false;

        return _transformationService.CanTransformRequest(
            context.Request.Path,
            context.Request.ContentType);
    }

    private bool ShouldTransformResponse(HttpContext context)
    {
        if (_options.ExcludedPaths.Any(p => 
            context.Request.Path.StartsWithSegments(p)))
            return false;

        if (string.IsNullOrEmpty(context.Response.ContentType))
            return false;

        return _transformationService.CanTransformResponse(
            context.Request.Path,
            context.Response.ContentType);
    }
} 