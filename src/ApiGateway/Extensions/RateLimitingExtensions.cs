public static class RateLimitingExtensions
{
    public static IServiceCollection AddApiRateLimiting(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<RateLimitingOptions>(
            configuration.GetSection("RateLimiting"));

        services.AddDistributedRedisCache(options =>
        {
            options.Configuration = 
                configuration.GetConnectionString("RateLimitingCache");
            options.InstanceName = "RateLimit_";
        });

        services.AddScoped<IRateLimitingService, RateLimitingService>();

        return services;
    }

    public static IApplicationBuilder UseApiRateLimiting(
        this IApplicationBuilder app)
    {
        return app.UseMiddleware<RateLimitingMiddleware>();
    }
} 