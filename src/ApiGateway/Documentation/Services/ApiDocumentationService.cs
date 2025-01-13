using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

public class ApiDocumentationService : IApiDocumentationService
{
    private readonly ILogger<ApiDocumentationService> _logger;
    private readonly IApiExampleGenerator _exampleGenerator;
    private readonly IMarkdownProcessor _markdownProcessor;
    private readonly DeveloperPortalOptions _options;
    private readonly IMemoryCache _cache;

    public ApiDocumentationService(
        ILogger<ApiDocumentationService> logger,
        IApiExampleGenerator exampleGenerator,
        IMarkdownProcessor markdownProcessor,
        IOptions<DeveloperPortalOptions> options,
        IMemoryCache cache)
    {
        _logger = logger;
        _exampleGenerator = exampleGenerator;
        _markdownProcessor = markdownProcessor;
        _options = options.Value;
        _cache = cache;
    }

    public async Task<ApiDocumentation> GetApiDocumentationAsync(
        string version,
        string format = "html")
    {
        var cacheKey = $"api_docs_{version}_{format}";
        
        return await _cache.GetOrCreateAsync(
            cacheKey,
            async entry =>
            {
                entry.SlidingExpiration = TimeSpan.FromHours(1);
                return await GenerateDocumentationAsync(version, format);
            });
    }

    public async Task<IEnumerable<ApiGuide>> GetGuidesAsync()
    {
        return await _cache.GetOrCreateAsync(
            "api_guides",
            async entry =>
            {
                entry.SlidingExpiration = TimeSpan.FromHours(1);
                return await LoadGuidesAsync();
            });
    }

    public async Task<ApiGuide> GetGuideAsync(string guideId)
    {
        var guides = await GetGuidesAsync();
        return guides.FirstOrDefault(g => g.Id == guideId);
    }

    private async Task<ApiDocumentation> GenerateDocumentationAsync(
        string version,
        string format)
    {
        try
        {
            var documentation = new ApiDocumentation
            {
                Version = version,
                GeneratedAt = DateTime.UtcNow,
                Endpoints = await GetEndpointsAsync(version),
                Models = await GetModelsAsync(version),
                Examples = await _exampleGenerator.GenerateExamplesAsync(version)
            };

            if (format == "markdown")
            {
                documentation.Content = await ConvertToMarkdownAsync(documentation);
            }
            else
            {
                documentation.Content = await ConvertToHtmlAsync(documentation);
            }

            return documentation;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, 
                "Error generating API documentation for version {Version}",
                version);
            throw;
        }
    }

    private async Task<IEnumerable<ApiEndpoint>> GetEndpointsAsync(
        string version)
    {
        // Implementation depends on your API structure
        // This could read from OpenAPI/Swagger documentation
        return Array.Empty<ApiEndpoint>();
    }

    private async Task<IEnumerable<ApiModel>> GetModelsAsync(
        string version)
    {
        // Implementation depends on your API structure
        // This could read from your model classes
        return Array.Empty<ApiModel>();
    }

    private async Task<IEnumerable<ApiGuide>> LoadGuidesAsync()
    {
        var guidesPath = Path.Combine(
            Directory.GetCurrentDirectory(),
            "Documentation",
            "Guides");

        var guides = new List<ApiGuide>();
        
        foreach (var file in Directory.GetFiles(guidesPath, "*.md"))
        {
            var content = await File.ReadAllTextAsync(file);
            var guide = ParseGuideContent(content, file);
            guides.Add(guide);
        }

        return guides;
    }

    private ApiGuide ParseGuideContent(string content, string filePath)
    {
        // Parse front matter and markdown content
        var guide = new ApiGuide
        {
            Id = Path.GetFileNameWithoutExtension(filePath),
            Content = _markdownProcessor.ProcessMarkdown(content)
        };

        // Extract metadata from front matter
        var frontMatter = ExtractFrontMatter(content);
        if (frontMatter.ContainsKey("title"))
            guide.Title = frontMatter["title"];
        if (frontMatter.ContainsKey("description"))
            guide.Description = frontMatter["description"];
        if (frontMatter.ContainsKey("order"))
            guide.Order = int.Parse(frontMatter["order"]);

        return guide;
    }

    private Dictionary<string, string> ExtractFrontMatter(string content)
    {
        var result = new Dictionary<string, string>();
        
        var match = Regex.Match(content, @"^---\s*\n(.*?)\n---\s*\n",
            RegexOptions.Singleline);
            
        if (match.Success)
        {
            var frontMatter = match.Groups[1].Value;
            var lines = frontMatter.Split('\n');
            
            foreach (var line in lines)
            {
                var parts = line.Split(':', 2);
                if (parts.Length == 2)
                {
                    result[parts[0].Trim()] = parts[1].Trim();
                }
            }
        }

        return result;
    }
} 