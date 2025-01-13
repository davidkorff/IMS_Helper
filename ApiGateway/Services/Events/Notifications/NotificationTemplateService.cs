public interface INotificationTemplateService
{
    Task<string> CreateTemplateAsync(NotificationTemplate template);
    Task<string> RenderTemplateAsync(string templateId, object data);
    Task<NotificationTemplate> GetTemplateAsync(string templateId);
    Task UpdateTemplateAsync(string templateId, NotificationTemplate template);
    Task<bool> ValidateTemplateAsync(string template, object sampleData);
}

public class NotificationTemplateService : INotificationTemplateService
{
    private readonly ILogger<NotificationTemplateService> _logger;
    private readonly IDistributedCache _cache;
    private readonly ApplicationDbContext _context;
    private readonly ConcurrentDictionary<string, CompiledTemplate> _templateCache;

    public NotificationTemplateService(
        ILogger<NotificationTemplateService> logger,
        IDistributedCache cache,
        ApplicationDbContext context)
    {
        _logger = logger;
        _cache = cache;
        _context = context;
        _templateCache = new ConcurrentDictionary<string, CompiledTemplate>();
    }

    public async Task<string> CreateTemplateAsync(NotificationTemplate template)
    {
        template.Id = Guid.NewGuid().ToString();
        template.CreatedAt = DateTime.UtcNow;
        template.Version = 1;

        // Validate template before saving
        if (!await ValidateTemplateAsync(template.Content, template.SampleData))
        {
            throw new InvalidOperationException("Template validation failed");
        }

        _context.NotificationTemplates.Add(template);
        await _context.SaveChangesAsync();

        // Invalidate cache
        await _cache.RemoveAsync($"template_{template.Id}");

        return template.Id;
    }

    public async Task<string> RenderTemplateAsync(string templateId, object data)
    {
        var template = await GetTemplateAsync(templateId);
        if (template == null)
        {
            throw new KeyNotFoundException($"Template {templateId} not found");
        }

        try
        {
            var compiled = await GetCompiledTemplateAsync(template);
            var context = new TemplateContext
            {
                Data = data,
                AlertSeverities = Enum.GetValues<AlertSeverity>().ToDictionary(
                    s => s.ToString().ToLower(), 
                    s => s),
                Formatters = new TemplateFormatters(),
                Now = DateTime.UtcNow
            };

            return await compiled.RenderAsync(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, 
                "Error rendering template {TemplateId}", templateId);
            throw;
        }
    }

    public async Task<NotificationTemplate> GetTemplateAsync(string templateId)
    {
        // Try cache first
        var cacheKey = $"template_{templateId}";
        var cached = await _cache.GetAsync(cacheKey);
        if (cached != null)
        {
            return JsonSerializer.Deserialize<NotificationTemplate>(cached);
        }

        // Get from database
        var template = await _context.NotificationTemplates
            .FirstOrDefaultAsync(t => t.Id == templateId);

        if (template != null)
        {
            // Cache for 5 minutes
            await _cache.SetAsync(
                cacheKey,
                JsonSerializer.SerializeToUtf8Bytes(template),
                new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5)
                });
        }

        return template;
    }

    public async Task UpdateTemplateAsync(
        string templateId, 
        NotificationTemplate template)
    {
        var existing = await _context.NotificationTemplates
            .FirstOrDefaultAsync(t => t.Id == templateId);

        if (existing == null)
        {
            throw new KeyNotFoundException($"Template {templateId} not found");
        }

        // Validate new template
        if (!await ValidateTemplateAsync(template.Content, template.SampleData))
        {
            throw new InvalidOperationException("Template validation failed");
        }

        // Update template
        existing.Content = template.Content;
        existing.Description = template.Description;
        existing.SampleData = template.SampleData;
        existing.UpdatedAt = DateTime.UtcNow;
        existing.Version++;

        await _context.SaveChangesAsync();

        // Invalidate cache
        await _cache.RemoveAsync($"template_{templateId}");
        _templateCache.TryRemove(templateId, out _);
    }

    public async Task<bool> ValidateTemplateAsync(string template, object sampleData)
    {
        try
        {
            var compiled = new CompiledTemplate(template);
            await compiled.CompileAsync();

            var context = new TemplateContext
            {
                Data = sampleData,
                AlertSeverities = Enum.GetValues<AlertSeverity>()
                    .ToDictionary(s => s.ToString().ToLower(), s => s),
                Formatters = new TemplateFormatters(),
                Now = DateTime.UtcNow
            };

            _ = await compiled.RenderAsync(context);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Template validation failed");
            return false;
        }
    }

    private async Task<CompiledTemplate> GetCompiledTemplateAsync(
        NotificationTemplate template)
    {
        return await _templateCache.GetOrAddAsync(template.Id, async _ =>
        {
            var compiled = new CompiledTemplate(template.Content);
            await compiled.CompileAsync();
            return compiled;
        });
    }
}

public class TemplateContext
{
    public object Data { get; set; }
    public Dictionary<string, AlertSeverity> AlertSeverities { get; set; }
    public TemplateFormatters Formatters { get; set; }
    public DateTime Now { get; set; }
}

public class TemplateFormatters
{
    public string FormatDate(DateTime date) => 
        date.ToString("yyyy-MM-dd HH:mm:ss UTC");
    
    public string FormatDuration(TimeSpan duration) => 
        duration.TotalSeconds < 60 
            ? $"{duration.TotalSeconds:F1}s" 
            : duration.ToString(@"hh\:mm\:ss");
    
    public string FormatNumber(decimal number) => 
        number.ToString("N2");
} 