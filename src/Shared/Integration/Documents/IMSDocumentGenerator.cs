using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;

public class IMSDocumentGenerator : IDocumentGenerator
{
    private readonly IIMSClient _imsClient;
    private readonly ILogger<IMSDocumentGenerator> _logger;
    private readonly IMemoryCache _cache;
    private readonly IMSDocumentSettings _settings;

    public IMSDocumentGenerator(
        IIMSClient imsClient,
        ILogger<IMSDocumentGenerator> logger,
        IMemoryCache cache,
        IOptions<IMSDocumentSettings> settings)
    {
        _imsClient = imsClient;
        _logger = logger;
        _cache = cache;
        _settings = settings.Value;
    }

    public async Task<string> GenerateDocument(DocumentRequest request)
    {
        try
        {
            _logger.LogInformation("Generating document using template {TemplateId}", 
                request.TemplateId);

            await ValidateRequest(request);

            var template = await GetTemplateById(request.TemplateId);
            if (template == null)
            {
                throw new DocumentGenerationException($"Template {request.TemplateId} not found");
            }

            var documentXml = await BuildDocumentXml(request, template);
            
            var response = await _imsClient.SendRequestAsync<ExecuteCommandRequest, ExecuteCommandResponse>(
                "DocumentFunctions.asmx",
                "GenerateDocument",
                new ExecuteCommandRequest
                {
                    ProcedureName = "GenerateDocument_WS",
                    Parameters = new[]
                    {
                        "@templateId", request.TemplateId,
                        "@documentXml", documentXml,
                        "@outputFormat", request.OutputFormat,
                        "@generatedBy", request.GeneratedBy ?? "SYSTEM"
                    }
                });

            var documentId = ParseDocumentResponse(response.Result);
            
            await SaveDocumentMetadata(documentId, request, template);

            return documentId;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate document for template {TemplateId}", 
                request.TemplateId);
            throw new DocumentGenerationException("Document generation failed", ex);
        }
    }

    public async Task<byte[]> GetDocument(string documentId)
    {
        try
        {
            var response = await _imsClient.SendRequestAsync<ExecuteCommandRequest, ExecuteCommandResponse>(
                "DocumentFunctions.asmx",
                "GetDocument",
                new ExecuteCommandRequest
                {
                    ProcedureName = "GetDocument_WS",
                    Parameters = new[] { "@documentId", documentId }
                });

            return Convert.FromBase64String(response.Result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve document {DocumentId}", documentId);
            throw new DocumentGenerationException($"Document retrieval failed: {documentId}", ex);
        }
    }

    public async Task<List<DocumentTemplate>> GetAvailableTemplates(
        string lineOfBusiness, 
        string state)
    {
        var cacheKey = $"doc_templates_{lineOfBusiness}_{state}";
        
        return await _cache.GetOrCreateAsync(cacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = 
                TimeSpan.FromMinutes(_settings.CacheExpirationMinutes);

            var response = await _imsClient.SendRequestAsync<ExecuteCommandRequest, ExecuteCommandResponse>(
                "DocumentFunctions.asmx",
                "GetTemplates",
                new ExecuteCommandRequest
                {
                    ProcedureName = "GetDocumentTemplates_WS",
                    Parameters = new[]
                    {
                        "@lineOfBusiness", lineOfBusiness,
                        "@state", state
                    }
                });

            return ParseDocumentTemplates(response.Result);
        });
    }

    public async Task<DocumentMetadata> GetDocumentMetadata(string documentId)
    {
        try
        {
            var response = await _imsClient.SendRequestAsync<ExecuteCommandRequest, ExecuteCommandResponse>(
                "DocumentFunctions.asmx",
                "GetDocumentMetadata",
                new ExecuteCommandRequest
                {
                    ProcedureName = "GetDocumentMetadata_WS",
                    Parameters = new[] { "@documentId", documentId }
                });

            return ParseDocumentMetadata(response.Result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get metadata for document {DocumentId}", documentId);
            throw new DocumentGenerationException($"Failed to get document metadata: {documentId}", ex);
        }
    }

    public async Task<bool> DeleteDocument(string documentId)
    {
        try
        {
            var response = await _imsClient.SendRequestAsync<ExecuteCommandRequest, ExecuteCommandResponse>(
                "DocumentFunctions.asmx",
                "DeleteDocument",
                new ExecuteCommandRequest
                {
                    ProcedureName = "DeleteDocument_WS",
                    Parameters = new[] { "@documentId", documentId }
                });

            return bool.Parse(response.Result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete document {DocumentId}", documentId);
            throw new DocumentGenerationException($"Document deletion failed: {documentId}", ex);
        }
    }

    public async Task<List<DocumentField>> GetRequiredFields(string templateId)
    {
        var template = await GetTemplateById(templateId);
        return template?.RequiredFields ?? new List<DocumentField>();
    }

    public async Task<DocumentValidationResult> ValidateData(
        string templateId, 
        Dictionary<string, object> data)
    {
        var template = await GetTemplateById(templateId);
        if (template == null)
        {
            throw new DocumentGenerationException($"Template {templateId} not found");
        }

        var errors = new List<DocumentValidationError>();
        var warnings = new List<DocumentValidationWarning>();

        foreach (var field in template.RequiredFields)
        {
            if (!data.TryGetValue(field.Name, out var value))
            {
                if (field.IsRequired)
                {
                    errors.Add(new DocumentValidationError
                    {
                        FieldName = field.Name,
                        Message = $"Required field {field.DisplayName} is missing",
                        Code = "MISSING_REQUIRED_FIELD"
                    });
                }
                continue;
            }

            ValidateFieldValue(field, value, errors, warnings);
        }

        return new DocumentValidationResult
        {
            IsValid = !errors.Any(),
            Errors = errors,
            Warnings = warnings
        };
    }

    public async Task<string> MergeDocuments(List<string> documentIds, MergeOptions options)
    {
        try
        {
            var mergeXml = BuildMergeXml(documentIds, options);
            
            var response = await _imsClient.SendRequestAsync<ExecuteCommandRequest, ExecuteCommandResponse>(
                "DocumentFunctions.asmx",
                "MergeDocuments",
                new ExecuteCommandRequest
                {
                    ProcedureName = "MergeDocuments_WS",
                    Parameters = new[]
                    {
                        "@mergeXml", mergeXml,
                        "@outputFormat", options.OutputFormat
                    }
                });

            return ParseDocumentResponse(response.Result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to merge documents");
            throw new DocumentGenerationException("Document merge failed", ex);
        }
    }

    private async Task ValidateRequest(DocumentRequest request)
    {
        if (string.IsNullOrEmpty(request.TemplateId))
        {
            throw new ArgumentException("TemplateId is required");
        }

        var template = await GetTemplateById(request.TemplateId);
        if (template == null)
        {
            throw new DocumentGenerationException($"Template {request.TemplateId} not found");
        }

        if (!template.SupportedFormats.Contains(request.OutputFormat))
        {
            throw new DocumentGenerationException(
                $"Output format {request.OutputFormat} not supported for template {request.TemplateId}");
        }

        var validation = await ValidateData(request.TemplateId, request.Data);
        if (!validation.IsValid)
        {
            throw new DocumentValidationException(
                "Document data validation failed", 
                validation.Errors);
        }
    }

    private async Task<DocumentTemplate> GetTemplateById(string templateId)
    {
        var cacheKey = $"doc_template_{templateId}";
        
        return await _cache.GetOrCreateAsync(cacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = 
                TimeSpan.FromMinutes(_settings.CacheExpirationMinutes);

            var response = await _imsClient.SendRequestAsync<ExecuteCommandRequest, ExecuteCommandResponse>(
                "DocumentFunctions.asmx",
                "GetTemplateById",
                new ExecuteCommandRequest
                {
                    ProcedureName = "GetDocumentTemplate_WS",
                    Parameters = new[] { "@templateId", templateId }
                });

            return ParseDocumentTemplate(response.Result);
        });
    }

    private void ValidateFieldValue(
        DocumentField field, 
        object value, 
        List<DocumentValidationError> errors,
        List<DocumentValidationWarning> warnings)
    {
        if (value == null)
        {
            if (field.IsRequired)
            {
                errors.Add(new DocumentValidationError
                {
                    FieldName = field.Name,
                    Message = $"Required field {field.DisplayName} cannot be null",
                    Code = "NULL_REQUIRED_FIELD"
                });
            }
            return;
        }

        if (field.AllowedValues?.Any() == true && 
            !field.AllowedValues.Contains(value.ToString()))
        {
            errors.Add(new DocumentValidationError
            {
                FieldName = field.Name,
                Message = $"Value {value} is not allowed for field {field.DisplayName}",
                Code = "INVALID_VALUE"
            });
        }

        // Add more validation based on field.ValidationRules
    }

    private async Task SaveDocumentMetadata(
        string documentId, 
        DocumentRequest request,
        DocumentTemplate template)
    {
        var metadata = new DocumentMetadata
        {
            DocumentId = documentId,
            TemplateId = template.TemplateId,
            Name = template.Name,
            Format = request.OutputFormat,
            GeneratedDate = DateTime.UtcNow,
            GeneratedBy = request.GeneratedBy ?? "SYSTEM",
            UsedData = request.Data,
            Status = DocumentStatus.Generated
        };

        var metadataXml = BuildMetadataXml(metadata);
        
        await _imsClient.SendRequestAsync<ExecuteCommandRequest, ExecuteCommandResponse>(
            "DocumentFunctions.asmx",
            "SaveDocumentMetadata",
            new ExecuteCommandRequest
            {
                ProcedureName = "SaveDocumentMetadata_WS",
                Parameters = new[]
                {
                    "@documentId", documentId,
                    "@metadataXml", metadataXml
                }
            });
    }

    // XML parsing and building methods...
    private string BuildDocumentXml(DocumentRequest request, DocumentTemplate template)
    {
        var doc = new XDocument(
            new XElement("Document",
                new XElement("TemplateId", template.TemplateId),
                new XElement("Data",
                    request.Data.Select(kvp =>
                        new XElement("Field",
                            new XElement("Name", kvp.Key),
                            new XElement("Value", kvp.Value)
                        )
                    )
                ),
                new XElement("Options",
                    new XElement("AddWatermark", request.Options?.AddWatermark ?? false),
                    new XElement("WatermarkText", request.Options?.WatermarkText),
                    new XElement("AddPageNumbers", request.Options?.AddPageNumbers ?? false)
                )
            )
        );

        return doc.ToString(SaveOptions.DisableFormatting);
    }

    private string BuildMergeXml(List<string> documentIds, MergeOptions options)
    {
        var doc = new XDocument(
            new XElement("MergeRequest",
                new XElement("Documents",
                    documentIds.Select(id => new XElement("DocumentId", id))
                ),
                new XElement("Options",
                    new XElement("AddTableOfContents", options.AddTableOfContents),
                    new XElement("AddBookmarks", options.AddBookmarks),
                    new XElement("OptimizeSize", options.OptimizeSize)
                )
            )
        );

        return doc.ToString(SaveOptions.DisableFormatting);
    }

    private string BuildMetadataXml(DocumentMetadata metadata)
    {
        var doc = new XDocument(
            new XElement("DocumentMetadata",
                new XElement("DocumentId", metadata.DocumentId),
                new XElement("TemplateId", metadata.TemplateId),
                new XElement("Name", metadata.Name),
                new XElement("Format", metadata.Format),
                new XElement("GeneratedDate", metadata.GeneratedDate),
                new XElement("GeneratedBy", metadata.GeneratedBy),
                new XElement("Status", metadata.Status),
                new XElement("UsedData",
                    metadata.UsedData.Select(kvp =>
                        new XElement("Field",
                            new XElement("Name", kvp.Key),
                            new XElement("Value", kvp.Value)
                        )
                    )
                )
            )
        );

        return doc.ToString(SaveOptions.DisableFormatting);
    }

    private string ParseDocumentResponse(string xml)
    {
        var doc = XDocument.Parse(xml);
        return doc.Element("DocumentResponse")
            .Element("DocumentId")
            .Value;
    }

    private List<DocumentTemplate> ParseDocumentTemplates(string xml)
    {
        var doc = XDocument.Parse(xml);
        return doc.Element("Templates")
            .Elements("Template")
            .Select(ParseDocumentTemplate)
            .ToList();
    }

    private DocumentTemplate ParseDocumentTemplate(string xml)
    {
        var element = XDocument.Parse(xml).Element("Template");
        return ParseDocumentTemplate(element);
    }

    private DocumentTemplate ParseDocumentTemplate(XElement element)
    {
        return new DocumentTemplate
        {
            TemplateId = element.Element("TemplateId").Value,
            Name = element.Element("Name").Value,
            Description = element.Element("Description").Value,
            Category = element.Element("Category").Value,
            Version = element.Element("Version").Value,
            EffectiveDate = DateTime.Parse(element.Element("EffectiveDate").Value),
            ExpirationDate = element.Element("ExpirationDate")?.Value != null
                ? DateTime.Parse(element.Element("ExpirationDate").Value)
                : null,
            SupportedFormats = element.Element("SupportedFormats")
                .Elements("Format")
                .Select(f => f.Value)
                .ToList(),
            SupportedStates = element.Element("SupportedStates")
                .Elements("State")
                .Select(s => s.Value)
                .ToList(),
            RequiredFields = element.Element("RequiredFields")
                .Elements("Field")
                .Select(ParseDocumentField)
                .ToList()
        };
    }

    private DocumentField ParseDocumentField(XElement element)
    {
        return new DocumentField
        {
            Name = element.Element("Name").Value,
            DisplayName = element.Element("DisplayName").Value,
            Description = element.Element("Description").Value,
            DataType = element.Element("DataType").Value,
            IsRequired = bool.Parse(element.Element("IsRequired").Value),
            DefaultValue = element.Element("DefaultValue")?.Value,
            AllowedValues = element.Element("AllowedValues")?
                .Elements("Value")
                .Select(v => v.Value)
                .ToList()
        };
    }

    private DocumentMetadata ParseDocumentMetadata(string xml)
    {
        var doc = XDocument.Parse(xml);
        var root = doc.Element("DocumentMetadata");

        return new DocumentMetadata
        {
            DocumentId = root.Element("DocumentId").Value,
            TemplateId = root.Element("TemplateId").Value,
            Name = root.Element("Name").Value,
            Format = root.Element("Format").Value,
            SizeInBytes = long.Parse(root.Element("SizeInBytes").Value),
            GeneratedDate = DateTime.Parse(root.Element("GeneratedDate").Value),
            GeneratedBy = root.Element("GeneratedBy").Value,
            Status = Enum.Parse<DocumentStatus>(root.Element("Status").Value),
            UsedData = root.Element("UsedData")
                .Elements("Field")
                .ToDictionary(
                    f => f.Element("Name").Value,
                    f => (object)f.Element("Value").Value
                )
        };
    }
} 