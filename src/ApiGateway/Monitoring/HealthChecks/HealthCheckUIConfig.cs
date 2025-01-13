using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http;

public static class HealthCheckUIConfig
{
    public static IServiceCollection AddHealthMonitoring(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddHealthChecks()
            .AddCheck<ApiGatewayHealthCheck>(
                "api_gateway",
                tags: new[] { "core" })
            .AddRedis(
                configuration.GetConnectionString("Redis"),
                name: "redis",
                tags: new[] { "cache" })
            .AddUrlGroup(
                new Uri[] 
                {
                    new(configuration["AuthService:Url"] + "/health"),
                    new(configuration["UserService:Url"] + "/health")
                },
                name: "downstream_services",
                tags: new[] { "services" });

        services.AddHealthChecksUI(setup =>
        {
            setup.SetEvaluationTimeInSeconds(30);
            setup.MaximumHistoryEntriesPerEndpoint(60);
            setup.SetApiMaxActiveRequests(1);
            setup.AddHealthCheckEndpoint("api_gateway", "/health");
        })
        .AddInMemoryStorage();

        return services;
    }

    public static IApplicationBuilder UseHealthMonitoring(
        this IApplicationBuilder app)
    {
        app.UseHealthChecks("/health", new HealthCheckOptions
        {
            Predicate = _ => true,
            ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse,
            ResultStatusCodes =
            {
                [HealthStatus.Healthy] = StatusCodes.Status200OK,
                [HealthStatus.Degraded] = StatusCodes.Status200OK,
                [HealthStatus.Unhealthy] = StatusCodes.Status503ServiceUnavailable
            },
            AllowCachingResponses = false
        });

        app.UseHealthChecks("/health/ready", new HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains("ready"),
            ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
        });

        app.UseHealthChecks("/health/live", new HealthCheckOptions
        {
            Predicate = _ => false,
            ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
        });

        app.UseHealthChecksUI(config =>
        {
            config.UIPath = "/health-ui";
            config.AddCustomStylesheet("wwwroot/css/health-ui.css");
        });

        return app;
    }
} 