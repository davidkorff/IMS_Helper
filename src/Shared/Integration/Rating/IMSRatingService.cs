using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Xml.Linq;
using System.Text.RegularExpressions;

public class IMSRatingService : IRatingService
{
    private readonly IIMSClient _imsClient;
    private readonly ILogger<IMSRatingService> _logger;
    private readonly IMemoryCache _cache;
    private readonly IMSRatingSettings _settings;

    public IMSRatingService(
        IIMSClient imsClient,
        ILogger<IMSRatingService> logger,
        IMemoryCache cache,
        IOptions<IMSRatingSettings> settings)
    {
        _imsClient = imsClient;
        _logger = logger;
        _cache = cache;
        _settings = settings.Value;
    }

    public async Task<RatingResponse> RateQuote(string quoteId, RatingRequest request)
    {
        try
        {
            _logger.LogInformation("Rating quote {QuoteId} with option {RatingOptionId}", 
                quoteId, request.RatingOptionId);

            var ratingXml = await BuildRatingXml(request);
            
            var response = await _imsClient.SendRequestAsync<ExecuteCommandRequest, ExecuteCommandResponse>(
                "DataAccess.asmx",
                "ExecuteCommand",
                new ExecuteCommandRequest
                {
                    ProcedureName = "ImportRatingXml",
                    Parameters = new[]
                    {
                        "@quoteId", quoteId,
                        "@ratingXml", ratingXml,
                        "@underwriterGuid", request.UnderwriterGuid
                    }
                });

            return ParseRatingResponse(response.Result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to rate quote {QuoteId}", quoteId);
            throw new RatingException("Rating calculation failed", ex);
        }
    }

    public async Task<List<RatingOption>> GetRatingOptions(string lineOfBusiness, string state)
    {
        var cacheKey = $"rating_options_{lineOfBusiness}_{state}";
        
        return await _cache.GetOrCreateAsync(cacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1);

            var response = await _imsClient.SendRequestAsync<ExecuteCommandRequest, ExecuteCommandResponse>(
                "DataAccess.asmx",
                "ExecuteCommand",
                new ExecuteCommandRequest
                {
                    ProcedureName = "GetRatingOptions_WS",
                    Parameters = new[]
                    {
                        "@lineOfBusiness", lineOfBusiness,
                        "@state", state
                    }
                });

            return ParseRatingOptions(response.Result);
        });
    }

    public async Task<RatingValidationResponse> ValidateRatingData(RatingRequest request)
    {
        try
        {
            var factors = await GetRatingFactors(request.RatingOptionId);
            var errors = new List<RatingValidationError>();
            var warnings = new List<RatingWarning>();

            foreach (var factor in factors)
            {
                if (!request.RatingData.TryGetValue(factor.Code, out var value))
                {
                    if (factor.Required)
                    {
                        errors.Add(new RatingValidationError
                        {
                            FactorCode = factor.Code,
                            Message = $"Required factor {factor.Name} is missing"
                        });
                    }
                    continue;
                }

                ValidateFactorValue(factor, value, errors, warnings);
            }

            // Validate dependencies
            foreach (var factor in factors.Where(f => f.ValidationRules?.Dependencies?.Any() == true))
            {
                ValidateFactorDependencies(factor, request.RatingData, errors);
            }

            return new RatingValidationResponse
            {
                IsValid = !errors.Any(),
                Errors = errors,
                Warnings = warnings
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to validate rating data");
            throw new RatingException("Rating validation failed", ex);
        }
    }

    public async Task<byte[]> GenerateRatingWorksheet(string quoteId, string ratingOptionId)
    {
        try
        {
            var response = await _imsClient.SendRequestAsync<ExecuteCommandRequest, ExecuteCommandResponse>(
                "DocumentFunctions.asmx",
                "CreateRatingWorksheet",
                new ExecuteCommandRequest
                {
                    ProcedureName = "GenerateRatingWorksheet_WS",
                    Parameters = new[]
                    {
                        "@quoteId", quoteId,
                        "@ratingOptionId", ratingOptionId
                    }
                });

            return Convert.FromBase64String(response.Result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate rating worksheet for quote {QuoteId}", quoteId);
            throw new RatingException("Rating worksheet generation failed", ex);
        }
    }

    public async Task<List<RatingFactor>> GetRatingFactors(string ratingOptionId)
    {
        var cacheKey = $"rating_factors_{ratingOptionId}";
        
        return await _cache.GetOrCreateAsync(cacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1);

            var response = await _imsClient.SendRequestAsync<ExecuteCommandRequest, ExecuteCommandResponse>(
                "DataAccess.asmx",
                "ExecuteCommand",
                new ExecuteCommandRequest
                {
                    ProcedureName = "GetRatingFactors_WS",
                    Parameters = new[] { "@ratingOptionId", ratingOptionId }
                });

            return ParseRatingFactors(response.Result);
        });
    }

    private async Task<string> BuildRatingXml(RatingRequest request)
    {
        var factors = await GetRatingFactors(request.RatingOptionId);
        var doc = new XDocument(
            new XElement("Rating",
                new XElement("RatingOptionId", request.RatingOptionId),
                new XElement("EffectiveDate", request.EffectiveDate.ToString("yyyy-MM-dd")),
                new XElement("Factors",
                    request.RatingData.Select(kvp =>
                        new XElement("Factor",
                            new XElement("Code", kvp.Key),
                            new XElement("Value", kvp.Value?.ToString())
                        )
                    )
                ),
                request.Overrides?.Any() == true
                    ? new XElement("Overrides",
                        request.Overrides.Select(o =>
                            new XElement("Override",
                                new XElement("FactorCode", o.FactorCode),
                                new XElement("Value", o.Value?.ToString()),
                                new XElement("Reason", o.Reason),
                                new XElement("ApprovedBy", o.ApprovedBy)
                            )
                        )
                    )
                    : null
            )
        );

        return doc.ToString(SaveOptions.DisableFormatting);
    }

    private void ValidateFactorValue(
        RatingFactor factor, 
        object value, 
        List<RatingValidationError> errors, 
        List<RatingWarning> warnings)
    {
        if (factor.ValidationRules == null) return;

        if (factor.DataType == "Decimal" && decimal.TryParse(value?.ToString(), out var decimalValue))
        {
            if (factor.ValidationRules.MinValue.HasValue && 
                decimalValue < factor.ValidationRules.MinValue.Value)
            {
                errors.Add(new RatingValidationError
                {
                    FactorCode = factor.Code,
                    Message = $"Value must be greater than {factor.ValidationRules.MinValue.Value}"
                });
            }

            if (factor.ValidationRules.MaxValue.HasValue && 
                decimalValue > factor.ValidationRules.MaxValue.Value)
            {
                errors.Add(new RatingValidationError
                {
                    FactorCode = factor.Code,
                    Message = $"Value must be less than {factor.ValidationRules.MaxValue.Value}"
                });
            }
        }

        if (!string.IsNullOrEmpty(factor.ValidationRules.RegexPattern))
        {
            var regex = new Regex(factor.ValidationRules.RegexPattern);
            if (!regex.IsMatch(value?.ToString() ?? string.Empty))
            {
                errors.Add(new RatingValidationError
                {
                    FactorCode = factor.Code,
                    Message = $"Value does not match required format"
                });
            }
        }

        if (factor.AllowedValues?.Any() == true && 
            !factor.AllowedValues.Contains(value?.ToString()))
        {
            errors.Add(new RatingValidationError
            {
                FactorCode = factor.Code,
                Message = $"Value must be one of: {string.Join(", ", factor.AllowedValues)}"
            });
        }
    }

    private void ValidateFactorDependencies(
        RatingFactor factor,
        Dictionary<string, object> ratingData,
        List<RatingValidationError> errors)
    {
        foreach (var dependency in factor.ValidationRules.Dependencies)
        {
            if (!ratingData.ContainsKey(dependency))
            {
                errors.Add(new RatingValidationError
                {
                    FactorCode = factor.Code,
                    Message = $"Required dependent factor {dependency} is missing"
                });
            }
        }
    }

    private RatingResponse ParseRatingResponse(string xml)
    {
        var doc = XDocument.Parse(xml);
        var root = doc.Element("RatingResponse");

        return new RatingResponse
        {
            QuoteId = root.Element("QuoteId").Value,
            RatingId = root.Element("RatingId").Value,
            BasePremium = decimal.Parse(root.Element("BasePremium").Value),
            Modifications = root.Element("Modifications")
                .Elements("Modification")
                .Select(m => new RatingModification
                {
                    Type = m.Element("Type").Value,
                    Description = m.Element("Description").Value,
                    Factor = decimal.Parse(m.Element("Factor").Value),
                    Amount = decimal.Parse(m.Element("Amount").Value)
                }).ToList(),
            Fees = root.Element("Fees")
                .Elements("Fee")
                .Select(f => new Fee
                {
                    Type = f.Element("Type").Value,
                    Description = f.Element("Description").Value,
                    Amount = decimal.Parse(f.Element("Amount").Value),
                    IsOptional = bool.Parse(f.Element("IsOptional").Value)
                }).ToList(),
            TotalPremium = decimal.Parse(root.Element("TotalPremium").Value),
            RatedDate = DateTime.Parse(root.Element("RatedDate").Value),
            RatedBy = root.Element("RatedBy").Value
        };
    }

    private List<RatingOption> ParseRatingOptions(string xml)
    {
        var doc = XDocument.Parse(xml);
        return doc.Element("RatingOptions")
            .Elements("Option")
            .Select(o => new RatingOption
            {
                Id = o.Element("Id").Value,
                Name = o.Element("Name").Value,
                Description = o.Element("Description").Value,
                Version = o.Element("Version").Value,
                EffectiveDate = DateTime.Parse(o.Element("EffectiveDate").Value),
                ExpirationDate = o.Element("ExpirationDate")?.Value != null 
                    ? DateTime.Parse(o.Element("ExpirationDate").Value)
                    : null,
                SupportedStates = o.Element("SupportedStates")
                    .Elements("State")
                    .Select(s => s.Value)
                    .ToList()
            }).ToList();
    }

    private List<RatingFactor> ParseRatingFactors(string xml)
    {
        var doc = XDocument.Parse(xml);
        return doc.Element("RatingFactors")
            .Elements("Factor")
            .Select(f => new RatingFactor
            {
                Code = f.Element("Code").Value,
                Name = f.Element("Name").Value,
                Description = f.Element("Description").Value,
                DataType = f.Element("DataType").Value,
                Required = bool.Parse(f.Element("Required").Value),
                DefaultValue = f.Element("DefaultValue")?.Value,
                AllowedValues = f.Element("AllowedValues")?
                    .Elements("Value")
                    .Select(v => v.Value)
                    .ToList(),
                ValidationRules = f.Element("ValidationRules") != null
                    ? new ValidationRules
                    {
                        MinValue = decimal.TryParse(f.Element("ValidationRules")
                            .Element("MinValue")?.Value, out var min) ? min : null,
                        MaxValue = decimal.TryParse(f.Element("ValidationRules")
                            .Element("MaxValue")?.Value, out var max) ? max : null,
                        RegexPattern = f.Element("ValidationRules")
                            .Element("RegexPattern")?.Value,
                        Dependencies = f.Element("ValidationRules")
                            .Element("Dependencies")?
                            .Elements("Dependency")
                            .Select(d => d.Value)
                            .ToList()
                    }
                    : null
            }).ToList();
    }
} 