using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Caching.Memory;
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Schema;

public class SchemaValidator : ISchemaValidator
{
    private readonly ILogger<SchemaValidator> _logger;
    private readonly IMemoryCache _cache;
    private readonly JsonSchemaGeneratorSettings _settings;

    public SchemaValidator(
        ILogger<SchemaValidator> logger,
        IMemoryCache cache)
    {
        _logger = logger;
        _cache = cache;
        _settings = new JsonSchemaGeneratorSettings
        {
            SchemaType = SchemaType.OpenApi3,
            GenerateEnumMappingDescription = true,
            FlattenInheritanceHierarchy = true
        };
    }

    public async Task ValidateAsync(
        Stream content,
        string schemaContent,
        string contentType)
    {
        var schema = GetOrCreateSchema(schemaContent);

        try
        {
            string jsonContent;
            if (contentType.Contains("xml", StringComparison.OrdinalIgnoreCase))
            {
                jsonContent = await ConvertXmlToJsonAsync(content);
            }
            else
            {
                using var reader = new StreamReader(content, leaveOpen: true);
                jsonContent = await reader.ReadToEndAsync();
                content.Position = 0;
            }

            var errors = new List<ValidationError>();
            var isValid = schema.Validate(
                jsonContent,
                out ICollection<ValidationError> validationErrors);

            if (!isValid)
            {
                errors.AddRange(validationErrors);
                throw new SchemaValidationException(errors);
            }
        }
        catch (Exception ex) when (ex is not SchemaValidationException)
        {
            _logger.LogError(ex, "Schema validation failed");
            throw new SchemaValidationException(
                new[] { new ValidationError("Invalid content format") });
        }
    }

    private JsonSchema GetOrCreateSchema(string schemaContent)
    {
        var cacheKey = $"schema:{HashString(schemaContent)}";
        
        return _cache.GetOrCreate(cacheKey, entry =>
        {
            entry.SlidingExpiration = TimeSpan.FromHours(1);
            
            var schema = JsonSchema.FromJsonAsync(
                schemaContent,
                _settings).GetAwaiter().GetResult();
                
            return schema;
        });
    }

    private static async Task<string> ConvertXmlToJsonAsync(Stream xmlContent)
    {
        using var reader = new StreamReader(xmlContent, leaveOpen: true);
        var xml = await reader.ReadToEndAsync();
        xmlContent.Position = 0;

        var xmlDoc = new XmlDocument();
        xmlDoc.LoadXml(xml);

        return JsonConvert.SerializeXmlNode(xmlDoc);
    }

    private static string HashString(string input)
    {
        using var sha256 = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(input);
        var hash = sha256.ComputeHash(bytes);
        return Convert.ToBase64String(hash);
    }
}

public class SchemaValidationException : Exception
{
    public IEnumerable<ValidationError> Errors { get; }

    public SchemaValidationException(
        IEnumerable<ValidationError> errors)
        : base("Content failed schema validation")
    {
        Errors = errors;
    }
}

public interface ISchemaValidator
{
    Task ValidateAsync(Stream content, string schemaContent, string contentType);
} 