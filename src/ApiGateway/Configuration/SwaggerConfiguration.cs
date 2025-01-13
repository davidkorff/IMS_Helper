public static class SwaggerConfiguration
{
    public static IServiceCollection AddSwaggerDocumentation(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "IMS Integration Platform API",
                Version = "v1",
                Description = "API Gateway for IMS Integration Platform",
                Contact = new OpenApiContact
                {
                    Name = "API Support",
                    Email = "api-support@company.com",
                    Url = new Uri("https://company.com/api-support")
                },
                License = new OpenApiLicense
                {
                    Name = "Company Internal Use Only",
                    Url = new Uri("https://company.com/api-license")
                }
            });

            // Add JWT Authentication
            options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Description = "JWT Authorization header using the Bearer scheme. Example: \"Authorization: Bearer {token}\"",
                Name = "Authorization",
                In = ParameterLocation.Header,
                Type = SecuritySchemeType.ApiKey,
                Scheme = "Bearer"
            });

            options.AddSecurityRequirement(new OpenApiSecurityRequirement
            {
                {
                    new OpenApiSecurityScheme
                    {
                        Reference = new OpenApiReference
                        {
                            Type = ReferenceType.SecurityScheme,
                            Id = "Bearer"
                        }
                    },
                    Array.Empty<string>()
                }
            });

            // Include XML comments
            var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
            var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
            options.IncludeXmlComments(xmlPath);

            // Custom schema mappings
            options.MapType<TimeSpan>(() => new OpenApiSchema
            {
                Type = "string",
                Format = "duration",
                Description = "Duration in ISO 8601 format (e.g., 'PT5M' for 5 minutes)"
            });

            // Custom operation filters
            options.OperationFilter<RateLimitHeadersOperationFilter>();
            options.OperationFilter<ApiVersionOperationFilter>();

            // Group endpoints by controller
            options.TagActionsBy(api =>
            {
                if (api.GroupName != null)
                {
                    return new[] { api.GroupName };
                }

                var controller = api.ActionDescriptor.RouteValues["controller"];
                return new[] { controller };
            });

            options.DocInclusionPredicate((docName, api) => true);

            // Add response header examples
            options.OperationFilter<ResponseHeaderExamples>();
        });

        return services;
    }

    public static IApplicationBuilder UseSwaggerDocumentation(
        this IApplicationBuilder app,
        IConfiguration configuration)
    {
        app.UseSwagger(options =>
        {
            options.RouteTemplate = "api-docs/{documentName}/swagger.json";
            
            options.PreSerializeFilters.Add((swaggerDoc, httpReq) =>
            {
                swaggerDoc.Servers = new List<OpenApiServer>
                {
                    new()
                    {
                        Url = $"{httpReq.Scheme}://{httpReq.Host.Value}",
                        Description = "Current Environment"
                    }
                };
            });
        });

        app.UseSwaggerUI(options =>
        {
            options.SwaggerEndpoint("/api-docs/v1/swagger.json", "IMS Integration Platform API V1");
            options.RoutePrefix = "api-docs";
            options.DocumentTitle = "IMS Integration Platform API Documentation";
            options.EnableDeepLinking();
            options.DisplayRequestDuration();
            options.EnableFilter();
            options.EnableTryItOutByDefault();
            
            // Custom CSS for better documentation styling
            options.InjectStylesheet("/swagger-ui/custom.css");
        });

        return app;
    }
}

public class RateLimitHeadersOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        operation.Parameters ??= new List<OpenApiParameter>();

        operation.Parameters.Add(new OpenApiParameter
        {
            Name = "X-RateLimit-Limit",
            In = ParameterLocation.Header,
            Description = "The maximum number of requests allowed per minute",
            Required = false,
            Schema = new OpenApiSchema { Type = "integer" }
        });

        operation.Parameters.Add(new OpenApiParameter
        {
            Name = "X-RateLimit-Remaining",
            In = ParameterLocation.Header,
            Description = "The number of requests remaining in the current time window",
            Required = false,
            Schema = new OpenApiSchema { Type = "integer" }
        });

        operation.Parameters.Add(new OpenApiParameter
        {
            Name = "X-RateLimit-Reset",
            In = ParameterLocation.Header,
            Description = "The time in seconds until the rate limit resets",
            Required = false,
            Schema = new OpenApiSchema { Type = "integer" }
        });
    }
}

public class ApiVersionOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        operation.Parameters ??= new List<OpenApiParameter>();

        operation.Parameters.Add(new OpenApiParameter
        {
            Name = "api-version",
            In = ParameterLocation.Header,
            Description = "API Version",
            Required = false,
            Schema = new OpenApiSchema { Type = "string" }
        });
    }
} 