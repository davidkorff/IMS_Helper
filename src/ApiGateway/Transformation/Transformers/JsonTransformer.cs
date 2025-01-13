using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

public class JsonTransformer : IContentTransformer
{
    private readonly JsonSerializerOptions _serializerOptions;
    
    public string ContentType => "application/json";

    public JsonTransformer()
    {
        _serializerOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
    }

    public async Task<Stream> TransformAsync(
        Stream content, 
        TransformationConfig config)
    {
        using var jsonDoc = await JsonDocument.ParseAsync(content);
        var transformed = TransformElement(jsonDoc.RootElement, config);
        
        var resultStream = new MemoryStream();
        await JsonSerializer.SerializeAsync(
            resultStream, 
            transformed, 
            _serializerOptions);
        
        resultStream.Position = 0;
        return resultStream;
    }

    private JsonElement TransformElement(
        JsonElement element, 
        TransformationConfig config)
    {
        using var jsonDoc = JsonDocument.Parse("{}");
        var writer = new ArrayBufferWriter<byte>();
        using var jsonWriter = new Utf8JsonWriter(writer);

        TransformElement(element, config, jsonWriter);
        jsonWriter.Flush();

        using var transformed = JsonDocument.Parse(writer.WrittenSpan);
        return transformed.RootElement.Clone();
    }

    private void TransformElement(
        JsonElement element,
        TransformationConfig config,
        Utf8JsonWriter writer)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                foreach (var property in element.EnumerateObject())
                {
                    var propertyName = MapPropertyName(
                        property.Name, 
                        config.Mappings);
                    
                    if (ShouldIncludeProperty(propertyName, config))
                    {
                        writer.WritePropertyName(propertyName);
                        TransformElement(
                            property.Value, 
                            config, 
                            writer);
                    }
                }
                writer.WriteEndObject();
                break;

            case JsonValueKind.Array:
                writer.WriteStartArray();
                foreach (var item in element.EnumerateArray())
                {
                    TransformElement(item, config, writer);
                }
                writer.WriteEndArray();
                break;

            default:
                element.WriteTo(writer);
                break;
        }
    }

    private static string MapPropertyName(
        string originalName,
        Dictionary<string, string> mappings)
    {
        return mappings?.GetValueOrDefault(originalName) ?? originalName;
    }

    private static bool ShouldIncludeProperty(
        string propertyName,
        TransformationConfig config)
    {
        if (config.FieldsToKeep?.Length > 0)
        {
            return config.FieldsToKeep.Contains(
                propertyName, 
                StringComparer.OrdinalIgnoreCase);
        }

        if (config.FieldsToRemove?.Length > 0)
        {
            return !config.FieldsToRemove.Contains(
                propertyName, 
                StringComparer.OrdinalIgnoreCase);
        }

        return true;
    }
} 