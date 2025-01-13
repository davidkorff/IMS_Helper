public static class RoutingExtensions
{
    public static IServiceCollection AddApiRouting(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddMemoryCache();
        services.AddHttpClient();
        
        services.Configure<RouteConfigOptions>(
            configuration.GetSection("RouteConfig"));
        
        services.AddSingleton<IRouteConfigurationService, RouteConfigurationService>();
        
        // Add Polly policies
        services.AddHttpClient("RoutingClient")
            .AddTransientHttpErrorPolicy(builder => builder
                .WaitAndRetryAsync(3, retryAttempt =>
                    TimeSpan.FromSeconds(Math.Pow(2, retryAttempt))))
            .AddTransientHttpErrorPolicy(builder => builder
                .CircuitBreakerAsync(5, TimeSpan.FromSeconds(30)));

        return services;
    }

    public static IApplicationBuilder UseApiRouting(
        this IApplicationBuilder app)
    {
        return app.UseMiddleware<RequestForwardingMiddleware>();
    }
} 