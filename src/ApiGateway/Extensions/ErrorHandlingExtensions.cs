public static class ErrorHandlingExtensions
{
    public static IApplicationBuilder UseErrorHandling(
        this IApplicationBuilder app)
    {
        return app.UseMiddleware<ErrorHandlingMiddleware>();
    }

    public static IServiceCollection AddErrorHandling(
        this IServiceCollection services)
    {
        services.AddScoped<ErrorHandlingMiddleware>();
        return services;
    }
} 