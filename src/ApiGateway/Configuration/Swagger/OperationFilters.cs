using Microsoft.OpenApi.Models;
using System;
using System.Collections.Generic;
using System.Reflection;

public class CorrelationIdOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        operation.Parameters ??= new List<OpenApiParameter>();

        operation.Parameters.Add(new OpenApiParameter
        {
            Name = "X-Correlation-ID",
            In = ParameterLocation.Header,
            Description = "Correlation ID for request tracing",
            Required = false,
            Schema = new OpenApiSchema { Type = "string", Format = "uuid" }
        });
    }
}

public class ClientIdOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        operation.Parameters ??= new List<OpenApiParameter>();

        operation.Parameters.Add(new OpenApiParameter
        {
            Name = "X-Client-ID",
            In = ParameterLocation.Header,
            Description = "Client identifier for rate limiting",
            Required = false,
            Schema = new OpenApiSchema { Type = "string" }
        });
    }
}

public class CachingOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var methodInfo = context.MethodInfo;
        var cachingAttribute = methodInfo.GetCustomAttribute<CacheableAttribute>();

        if (cachingAttribute != null)
        {
            operation.Parameters ??= new List<OpenApiParameter>();

            operation.Parameters.Add(new OpenApiParameter
            {
                Name = "Cache-Control",
                In = ParameterLocation.Header,
                Description = "Caching directives",
                Required = false,
                Schema = new OpenApiSchema { Type = "string" }
            });

            operation.Parameters.Add(new OpenApiParameter
            {
                Name = "If-None-Match",
                In = ParameterLocation.Header,
                Description = "ETag for cache validation",
                Required = false,
                Schema = new OpenApiSchema { Type = "string" }
            });
        }
    }
} 