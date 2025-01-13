using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

public class TransformationService : ITransformationService
{
    private readonly ILogger<TransformationService> _logger;
    private readonly TransformationOptions _options;
    private readonly ITransformationRuleProvider _ruleProvider;
    private readonly IDictionary<string, IContentTransformer> _transformers;

    public TransformationService(
        ILogger<TransformationService> logger,
        IOptions<TransformationOptions> options,
        ITransformationRuleProvider ruleProvider,
        IEnumerable<IContentTransformer> transformers)
    {
        _logger = logger;
        _options = options.Value;
        _ruleProvider = ruleProvider;
        _transformers = transformers.ToDictionary(
            t => t.ContentType,
            t => t);
    }

    public bool CanTransformRequest(string path, string contentType)
    {
        var rule = _ruleProvider.GetRule(path);
        if (rule == null) return false;

        return rule.RequestTransformation != null && 
               _transformers.ContainsKey(contentType);
    }

    public bool CanTransformResponse(string path, string contentType)
    {
        var rule = _ruleProvider.GetRule(path);
        if (rule == null) return false;

        return rule.ResponseTransformation != null && 
               _transformers.ContainsKey(contentType);
    }

    public async Task<Stream> TransformRequestAsync(
        Stream requestBody,
        string path,
        string contentType)
    {
        var rule = _ruleProvider.GetRule(path);
        if (rule?.RequestTransformation == null)
        {
            throw new TransformationException(
                "No request transformation rule found",
                TransformationType.Request);
        }

        if (!_transformers.TryGetValue(contentType, out var transformer))
        {
            throw new TransformationException(
                $"No transformer found for content type: {contentType}",
                TransformationType.Request);
        }

        try
        {
            var transformedContent = await transformer
                .TransformAsync(
                    requestBody,
                    rule.RequestTransformation);

            var resultStream = new MemoryStream();
            await transformedContent.CopyToAsync(resultStream);
            resultStream.Position = 0;
            return resultStream;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, 
                "Error transforming request for path {Path}", path);
            throw new TransformationException(
                "Error applying request transformation",
                TransformationType.Request,
                ex);
        }
    }

    public async Task<Stream> TransformResponseAsync(
        Stream responseBody,
        string path,
        string contentType)
    {
        var rule = _ruleProvider.GetRule(path);
        if (rule?.ResponseTransformation == null)
        {
            throw new TransformationException(
                "No response transformation rule found",
                TransformationType.Response);
        }

        if (!_transformers.TryGetValue(contentType, out var transformer))
        {
            throw new TransformationException(
                $"No transformer found for content type: {contentType}",
                TransformationType.Response);
        }

        try
        {
            var transformedContent = await transformer
                .TransformAsync(
                    responseBody,
                    rule.ResponseTransformation);

            var resultStream = new MemoryStream();
            await transformedContent.CopyToAsync(resultStream);
            resultStream.Position = 0;
            return resultStream;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, 
                "Error transforming response for path {Path}", path);
            throw new TransformationException(
                "Error applying response transformation",
                TransformationType.Response,
                ex);
        }
    }
} 