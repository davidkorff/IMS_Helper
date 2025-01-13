public static class TransformationExtensions
{
    public static IServiceCollection AddTransformation(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<TransformationOptions>(
            configuration.GetSection("Transformation"));

        services.AddScoped<ITransformationService, TransformationService>();
        services.AddScoped<ITransformationRuleProvider, TransformationRuleProvider>();
        
        services.AddScoped<IContentTransformer, JsonTransformer>();
        services.AddScoped<IContentTransformer, XmlTransformer>();

        return services;
    }

    public static IApplicationBuilder UseTransformation(
        this IApplicationBuilder app)
    {
        return app.UseMiddleware<TransformationMiddleware>();
    }
} 