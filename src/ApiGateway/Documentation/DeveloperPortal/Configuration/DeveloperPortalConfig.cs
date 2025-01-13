using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using System;
using System.Collections.Generic;
using System.IO;

public static class DeveloperPortalConfig
{
    public static IServiceCollection AddDeveloperPortal(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<DeveloperPortalOptions>(
            configuration.GetSection("DeveloperPortal"));

        services.AddScoped<IApiDocumentationService, ApiDocumentationService>();
        services.AddScoped<IApiExampleGenerator, ApiExampleGenerator>();
        services.AddScoped<IMarkdownProcessor, MarkdownProcessor>();
        
        services.AddRazorPages()
            .AddRazorPagesOptions(options =>
            {
                options.Conventions.AddPageRoute(
                    "/DeveloperPortal/Index",
                    "/docs");
                options.Conventions.AddPageRoute(
                    "/DeveloperPortal/Api/{apiId}",
                    "/docs/api/{apiId}");
                options.Conventions.AddPageRoute(
                    "/DeveloperPortal/Guides/{guideId}",
                    "/docs/guides/{guideId}");
            });

        return services;
    }

    public static IApplicationBuilder UseDeveloperPortal(
        this IApplicationBuilder app,
        IConfiguration configuration)
    {
        var options = configuration
            .GetSection("DeveloperPortal")
            .Get<DeveloperPortalOptions>();

        app.UseStaticFiles(new StaticFileOptions
        {
            FileProvider = new PhysicalFileProvider(
                Path.Combine(Directory.GetCurrentDirectory(), "Documentation")),
            RequestPath = "/docs/content"
        });

        if (options.EnableSwaggerUI)
        {
            app.UseSwaggerUI(c =>
            {
                c.SwaggerEndpoint("/swagger/v1/swagger.json", "API V1");
                c.RoutePrefix = "docs/api";
                c.DocumentTitle = options.Title;
                c.InjectStylesheet("/docs/content/css/swagger-custom.css");
            });
        }

        return app;
    }
}

public class DeveloperPortalOptions
{
    public string Title { get; set; } = "API Developer Portal";
    public string Description { get; set; }
    public string Version { get; set; } = "1.0";
    public bool EnableSwaggerUI { get; set; } = true;
    public bool EnableApiExplorer { get; set; } = true;
    public bool EnableGuides { get; set; } = true;
    public string[] ApiVersions { get; set; } = { "v1" };
    public string ContactEmail { get; set; }
    public string RepositoryUrl { get; set; }
    public Dictionary<string, string> ExternalDocs { get; set; }
} 