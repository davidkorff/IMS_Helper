using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Caching.Memory;
using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Linq;
using System.Xml;
using System.Xml.Schema;
using System.Collections.Generic;
using System.Xml.Serialization;
using Google.Protobuf;
using Google.Protobuf.Reflection;

public class XsdValidator : ISchemaValidator
{
    private readonly ILogger<XsdValidator> _logger;
    private readonly IMemoryCache _cache;

    public XsdValidator(
        ILogger<XsdValidator> logger,
        IMemoryCache cache)
    {
        _logger = logger;
        _cache = cache;
    }

    public async Task ValidateAsync(
        Stream content,
        string schemaContent,
        string contentType)
    {
        if (!contentType.Contains("xml", StringComparison.OrdinalIgnoreCase))
        {
            throw new SchemaValidationException(
                new[] { new ValidationError("Content must be XML") });
        }

        var schema = GetOrCreateSchema(schemaContent);
        var errors = new List<string>();

        var settings = new XmlReaderSettings
        {
            Schemas = schema,
            ValidationType = ValidationType.Schema,
            ValidationFlags = XmlSchemaValidationFlags.ReportValidationWarnings
        };

        settings.ValidationEventHandler += (s, e) =>
        {
            errors.Add(e.Message);
        };

        try
        {
            using var reader = XmlReader.Create(content, settings);
            while (await reader.ReadAsync()) { }

            if (errors.Any())
            {
                throw new SchemaValidationException(
                    errors.Select(e => new ValidationError(e)));
            }
        }
        catch (Exception ex) when (ex is not SchemaValidationException)
        {
            _logger.LogError(ex, "XSD validation failed");
            throw new SchemaValidationException(
                new[] { new ValidationError("Invalid XML content") });
        }
    }

    private XmlSchemaSet GetOrCreateSchema(string schemaContent)
    {
        var cacheKey = $"xsd:{HashString(schemaContent)}";
        
        return _cache.GetOrCreate(cacheKey, entry =>
        {
            entry.SlidingExpiration = TimeSpan.FromHours(1);
            
            var schema = new XmlSchemaSet();
            using var reader = XmlReader.Create(
                new StringReader(schemaContent));
            schema.Add(null, reader);
            schema.Compile();
            
            return schema;
        });
    }

    private static string HashString(string input)
    {
        using var sha256 = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(input);
        var hash = sha256.ComputeHash(bytes);
        return Convert.ToBase64String(hash);
    }
}

public class ProtobufValidator : ISchemaValidator
{
    private readonly ILogger<ProtobufValidator> _logger;
    private readonly IMemoryCache _cache;

    public ProtobufValidator(
        ILogger<ProtobufValidator> logger,
        IMemoryCache cache)
    {
        _logger = logger;
        _cache = cache;
    }

    public async Task ValidateAsync(
        Stream content,
        string schemaContent,
        string contentType)
    {
        if (!contentType.Contains("protobuf", StringComparison.OrdinalIgnoreCase))
        {
            throw new SchemaValidationException(
                new[] { new ValidationError("Content must be Protobuf") });
        }

        try
        {
            var descriptor = GetOrCreateDescriptor(schemaContent);
            using var ms = new MemoryStream();
            await content.CopyToAsync(ms);
            
            var message = descriptor.Parser.ParseFrom(ms.ToArray());
            
            if (!descriptor.IsValidMessage(message))
            {
                throw new SchemaValidationException(
                    new[] { new ValidationError("Invalid Protobuf message") });
            }
        }
        catch (Exception ex) when (ex is not SchemaValidationException)
        {
            _logger.LogError(ex, "Protobuf validation failed");
            throw new SchemaValidationException(
                new[] { new ValidationError("Invalid Protobuf content") });
        }
    }

    private MessageDescriptor GetOrCreateDescriptor(string schemaContent)
    {
        var cacheKey = $"protobuf:{HashString(schemaContent)}";
        
        return _cache.GetOrCreate(cacheKey, entry =>
        {
            entry.SlidingExpiration = TimeSpan.FromHours(1);
            
            // Parse schema and create descriptor
            // Implementation depends on specific Protobuf library
            return null;
        });
    }

    private static string HashString(string input)
    {
        using var sha256 = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(input);
        var hash = sha256.ComputeHash(bytes);
        return Convert.ToBase64String(hash);
    }
} 