public static class LoggingExtensions
{
    public static IApplicationBuilder UseApiLogging(
        this IApplicationBuilder app)
    {
        return app.UseMiddleware<LoggingMiddleware>();
    }

    public static IServiceCollection AddApiLogging(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<LoggingSettings>(
            configuration.GetSection("Logging"));

        services.AddLogging(builder =>
        {
            var loggingSettings = configuration
                .GetSection("Logging")
                .Get<LoggingSettings>();

            builder
                .ClearProviders()
                .AddConsole()
                .AddDebug()
                .AddSeq(configuration.GetSection("Seq"))
                .SetMinimumLevel(LogLevel.Information);

            // Apply custom log levels
            if (loggingSettings?.LogLevels != null)
            {
                builder.AddFilter("Default", 
                    Enum.Parse<LogLevel>(loggingSettings.LogLevels.Default));
                builder.AddFilter("Microsoft", 
                    Enum.Parse<LogLevel>(loggingSettings.LogLevels.Microsoft));
                builder.AddFilter("System", 
                    Enum.Parse<LogLevel>(loggingSettings.LogLevels.System));

                foreach (var ns in loggingSettings.LogLevels.CustomNamespaces)
                {
                    builder.AddFilter(ns.Key, 
                        Enum.Parse<LogLevel>(ns.Value));
                }
            }
        });

        return services;
    }

    public static IServiceCollection AddEnhancedLogging(
        this IServiceCollection services)
    {
        services.AddHttpContextAccessor();
        services.AddScoped<ILogEnricher, LogEnricher>();
        services.AddScoped<ILogCorrelation, LogCorrelation>();

        // Configure Serilog with enrichers
        Log.Logger = new LoggerConfiguration()
            .Enrich.FromLogContext()
            .Enrich.With<CorrelationIdEnricher>()
            .WriteTo.Console(new JsonFormatter())
            .WriteTo.Seq("http://seq:5341")
            .CreateLogger();

        return services;
    }
} 