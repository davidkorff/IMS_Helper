public static class MonitoringExtensions
{
    public static IServiceCollection AddMonitoring(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddApplicationInsightsTelemetry();
        
        services.AddHealthChecks()
            .AddSqlServer(configuration.GetConnectionString("DefaultConnection"))
            .AddRedis(configuration.GetConnectionString("Redis"))
            .AddUrlGroup(new Uri(configuration["IMS:BaseUrl"]), "IMS API");

        return services;
    }

    public static IApplicationBuilder UseMonitoring(this IApplicationBuilder app)
    {
        app.UseHealthChecks("/health", new HealthCheckOptions
        {
            ResponseWriter = async (context, report) =>
            {
                var result = new
                {
                    status = report.Status.ToString(),
                    checks = report.Entries.Select(entry => new
                    {
                        name = entry.Key,
                        status = entry.Value.Status.ToString(),
                        duration = entry.Value.Duration.TotalMilliseconds
                    })
                };

                context.Response.ContentType = "application/json";
                await context.Response.WriteAsJsonAsync(result);
            }
        });

        return app;
    }
} 