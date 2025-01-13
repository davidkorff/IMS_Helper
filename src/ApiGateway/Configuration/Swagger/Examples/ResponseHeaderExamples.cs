using Microsoft.OpenApi.Models;
using System;
using System.Collections.Generic;
using System.Reflection;

public class ResponseHeaderExamples : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        // Rate Limiting Headers
        var rateLimitHeaders = new Dictionary<string, OpenApiHeader>
        {
            {
                "X-RateLimit-Limit",
                new OpenApiHeader
                {
                    Description = "The maximum number of requests allowed per minute",
                    Schema = new OpenApiSchema { Type = "integer" },
                    Example = new OpenApiInteger(100)
                }
            },
            {
                "X-RateLimit-Remaining",
                new OpenApiHeader
                {
                    Description = "The number of requests remaining in the current time window",
                    Schema = new OpenApiSchema { Type = "integer" },
                    Example = new OpenApiInteger(87)
                }
            },
            {
                "X-RateLimit-Reset",
                new OpenApiHeader
                {
                    Description = "The time in seconds until the rate limit resets",
                    Schema = new OpenApiSchema { Type = "integer" },
                    Example = new OpenApiInteger(23)
                }
            },
            {
                "Retry-After",
                new OpenApiHeader
                {
                    Description = "The number of seconds to wait before retrying",
                    Schema = new OpenApiSchema { Type = "integer" },
                    Example = new OpenApiInteger(30)
                }
            }
        };

        // Caching Headers
        var cachingHeaders = new Dictionary<string, OpenApiHeader>
        {
            {
                "ETag",
                new OpenApiHeader
                {
                    Description = "Entity tag for cache validation",
                    Schema = new OpenApiSchema { Type = "string" },
                    Example = new OpenApiString("\"33a64df551425fcc55e4d42a148795d9f25f89d4\"")
                }
            },
            {
                "Cache-Control",
                new OpenApiHeader
                {
                    Description = "Caching directives",
                    Schema = new OpenApiSchema { Type = "string" },
                    Example = new OpenApiString("public, max-age=3600")
                }
            },
            {
                "Last-Modified",
                new OpenApiHeader
                {
                    Description = "Last modification date",
                    Schema = new OpenApiSchema { Type = "string", Format = "date-time" },
                    Example = new OpenApiString(DateTime.UtcNow.ToString("R"))
                }
            }
        };

        // Tracing Headers
        var tracingHeaders = new Dictionary<string, OpenApiHeader>
        {
            {
                "X-Correlation-ID",
                new OpenApiHeader
                {
                    Description = "Correlation ID for request tracing",
                    Schema = new OpenApiSchema { Type = "string", Format = "uuid" },
                    Example = new OpenApiString(Guid.NewGuid().ToString())
                }
            },
            {
                "X-Request-ID",
                new OpenApiHeader
                {
                    Description = "Unique request identifier",
                    Schema = new OpenApiSchema { Type = "string", Format = "uuid" },
                    Example = new OpenApiString(Guid.NewGuid().ToString())
                }
            },
            {
                "X-Processing-Time",
                new OpenApiHeader
                {
                    Description = "Request processing time in milliseconds",
                    Schema = new OpenApiSchema { Type = "integer" },
                    Example = new OpenApiInteger(123)
                }
            }
        };

        // Add headers to responses based on operation
        foreach (var response in operation.Responses)
        {
            response.Value.Headers ??= new Dictionary<string, OpenApiHeader>();

            // Add tracing headers to all responses
            foreach (var header in tracingHeaders)
            {
                response.Value.Headers[header.Key] = header.Value;
            }

            // Add rate limit headers to 429 responses
            if (response.Key == "429")
            {
                foreach (var header in rateLimitHeaders)
                {
                    response.Value.Headers[header.Key] = header.Value;
                }
            }

            // Add caching headers to GET 200 responses
            if (response.Key == "200" && 
                context.MethodInfo.GetCustomAttribute<CacheableAttribute>() != null)
            {
                foreach (var header in cachingHeaders)
                {
                    response.Value.Headers[header.Key] = header.Value;
                }
            }

            // Add specific examples based on response code
            switch (response.Key)
            {
                case "401":
                    response.Value.Headers["WWW-Authenticate"] = new OpenApiHeader
                    {
                        Description = "Authentication scheme",
                        Schema = new OpenApiSchema { Type = "string" },
                        Example = new OpenApiString("Bearer error=\"invalid_token\"")
                    };
                    break;

                case "503":
                    response.Value.Headers["Retry-After"] = new OpenApiHeader
                    {
                        Description = "Service unavailable retry delay",
                        Schema = new OpenApiSchema { Type = "integer" },
                        Example = new OpenApiInteger(60)
                    };
                    break;
            }
        }
    }
} 