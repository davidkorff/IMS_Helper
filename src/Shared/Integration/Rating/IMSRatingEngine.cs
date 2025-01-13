using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Xml.Linq;

public class IMSRatingEngine : IRatingEngine
{
    private readonly IIMSClient _imsClient;
    private readonly ILogger<IMSRatingEngine> _logger;
    private readonly IMemoryCache _cache;
    private readonly IMSRatingSettings _settings;

    public IMSRatingEngine(
        IIMSClient imsClient,
        ILogger<IMSRatingEngine> logger,
        IMemoryCache cache,
        IOptions<IMSRatingSettings> settings)
    {
        _imsClient = imsClient;
        _logger = logger;
        _cache = cache;
        _settings = settings.Value;
    }

    public async Task<RatingResponse> RatePolicy(RatingRequest request)
    {
        try
        {
            _logger.LogInformation("Rating policy for quote {QuoteId}", request.QuoteId);

            await ValidateRatingRequest(request);

            var ratingXml = BuildRatingRequestXml(request);
            
            var response = await _imsClient.SendRequestAsync<ExecuteCommandRequest, ExecuteCommandResponse>(
                "RatingFunctions.asmx",
                "RatePolicy",
                new ExecuteCommandRequest
                {
                    ProcedureName = "RatePolicy_WS",
                    Parameters = new[]
                    {
                        "@quoteId", request.QuoteId,
                        "@ratingXml", ratingXml
                    }
                });

            return ParseRatingResponse(response.Result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to rate policy for quote {QuoteId}", request.QuoteId);
            throw new RatingException("Policy rating failed", ex);
        }
    }

    public async Task<List<RatingFactor>> GetRatingFactors(string lineOfBusiness, string state)
    {
        try
        {
            var cacheKey = $"rating_factors_{lineOfBusiness}_{state}";
            
            return await _cache.GetOrCreateAsync(
                cacheKey,
                async entry =>
                {
                    entry.AbsoluteExpirationRelativeToNow = 
                        TimeSpan.FromMinutes(_settings.CacheExpirationMinutes);

                    var response = await _imsClient.SendRequestAsync<ExecuteCommandRequest, ExecuteCommandResponse>(
                        "RatingFunctions.asmx",
                        "GetRatingFactors",
                        new ExecuteCommandRequest
                        {
                            ProcedureName = "GetRatingFactors_WS",
                            Parameters = new[]
                            {
                                "@lineOfBusiness", lineOfBusiness,
                                "@state", state
                            }
                        });

                    return ParseRatingFactors(response.Result);
                });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, 
                "Failed to get rating factors for {LineOfBusiness} in {State}", 
                lineOfBusiness, state);
            throw new RatingException("Failed to retrieve rating factors", ex);
        }
    }

    public async Task<RatingWorksheet> GetRatingWorksheet(string quoteId)
    {
        try
        {
            var response = await _imsClient.SendRequestAsync<ExecuteCommandRequest, ExecuteCommandResponse>(
                "RatingFunctions.asmx",
                "GetRatingWorksheet",
                new ExecuteCommandRequest
                {
                    ProcedureName = "GetRatingWorksheet_WS",
                    Parameters = new[] { "@quoteId", quoteId }
                });

            return ParseRatingWorksheet(response.Result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get rating worksheet for quote {QuoteId}", quoteId);
            throw new RatingException($"Failed to retrieve rating worksheet: {quoteId}", ex);
        }
    }

    public async Task<List<RatingRule>> GetActiveRatingRules(string lineOfBusiness, string state)
    {
        try
        {
            var cacheKey = $"rating_rules_{lineOfBusiness}_{state}";
            
            return await _cache.GetOrCreateAsync(
                cacheKey,
                async entry =>
                {
                    entry.AbsoluteExpirationRelativeToNow = 
                        TimeSpan.FromMinutes(_settings.CacheExpirationMinutes);

                    var response = await _imsClient.SendRequestAsync<ExecuteCommandRequest, ExecuteCommandResponse>(
                        "RatingFunctions.asmx",
                        "GetActiveRatingRules",
                        new ExecuteCommandRequest
                        {
                            ProcedureName = "GetActiveRatingRules_WS",
                            Parameters = new[]
                            {
                                "@lineOfBusiness", lineOfBusiness,
                                "@state", state
                            }
                        });

                    return ParseRatingRules(response.Result);
                });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, 
                "Failed to get active rating rules for {LineOfBusiness} in {State}", 
                lineOfBusiness, state);
            throw new RatingException("Failed to retrieve rating rules", ex);
        }
    }

    public async Task<decimal> CalculatePremium(string quoteId, PremiumCalculationRequest request)
    {
        try
        {
            _logger.LogInformation("Calculating premium for quote {QuoteId}", quoteId);

            var calculationXml = BuildPremiumCalculationXml(request);
            
            var response = await _imsClient.SendRequestAsync<ExecuteCommandRequest, ExecuteCommandResponse>(
                "RatingFunctions.asmx",
                "CalculatePremium",
                new ExecuteCommandRequest
                {
                    ProcedureName = "CalculatePremium_WS",
                    Parameters = new[]
                    {
                        "@quoteId", quoteId,
                        "@calculationXml", calculationXml
                    }
                });

            return decimal.Parse(response.Result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to calculate premium for quote {QuoteId}", quoteId);
            throw new RatingException($"Premium calculation failed: {quoteId}", ex);
        }
    }

    public async Task<List<Discount>> GetAvailableDiscounts(string quoteId)
    {
        try
        {
            var response = await _imsClient.SendRequestAsync<ExecuteCommandRequest, ExecuteCommandResponse>(
                "RatingFunctions.asmx",
                "GetAvailableDiscounts",
                new ExecuteCommandRequest
                {
                    ProcedureName = "GetAvailableDiscounts_WS",
                    Parameters = new[] { "@quoteId", quoteId }
                });

            return ParseDiscounts(response.Result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get available discounts for quote {QuoteId}", quoteId);
            throw new RatingException($"Failed to retrieve available discounts: {quoteId}", ex);
        }
    }

    public async Task<RatingValidationResult> ValidateRatingData(
        string quoteId, 
        Dictionary<string, object> data)
    {
        try
        {
            var validationXml = BuildValidationDataXml(data);
            
            var response = await _imsClient.SendRequestAsync<ExecuteCommandRequest, ExecuteCommandResponse>(
                "RatingFunctions.asmx",
                "ValidateRatingData",
                new ExecuteCommandRequest
                {
                    ProcedureName = "ValidateRatingData_WS",
                    Parameters = new[]
                    {
                        "@quoteId", quoteId,
                        "@validationXml", validationXml
                    }
                });

            return ParseValidationResult(response.Result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to validate rating data for quote {QuoteId}", quoteId);
            throw new RatingException($"Rating data validation failed: {quoteId}", ex);
        }
    }

    public async Task<ComparisonResult> CompareRates(string quoteId, string comparisonQuoteId)
    {
        try
        {
            var response = await _imsClient.SendRequestAsync<ExecuteCommandRequest, ExecuteCommandResponse>(
                "RatingFunctions.asmx",
                "CompareRates",
                new ExecuteCommandRequest
                {
                    ProcedureName = "CompareRates_WS",
                    Parameters = new[]
                    {
                        "@quoteId", quoteId,
                        "@comparisonQuoteId", comparisonQuoteId
                    }
                });

            return ParseComparisonResult(response.Result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, 
                "Failed to compare rates between quotes {QuoteId} and {ComparisonQuoteId}", 
                quoteId, comparisonQuoteId);
            throw new RatingException("Rate comparison failed", ex);
        }
    }

    // Private helper methods for XML building and parsing...
    private async Task ValidateRatingRequest(RatingRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.QuoteId))
            throw new ArgumentException("Quote ID is required");

        if (string.IsNullOrWhiteSpace(request.LineOfBusiness))
            throw new ArgumentException("Line of business is required");

        if (string.IsNullOrWhiteSpace(request.State))
            throw new ArgumentException("State is required");

        if (request.EffectiveDate < DateTime.Today)
            throw new ArgumentException("Effective date cannot be in the past");

        // Additional validation logic...
    }

    private string BuildRatingRequestXml(RatingRequest request)
    {
        var doc = new XDocument(
            new XElement("RatingRequest",
                new XElement("QuoteId", request.QuoteId),
                new XElement("LineOfBusiness", request.LineOfBusiness),
                new XElement("State", request.State),
                new XElement("EffectiveDate", 
                    request.EffectiveDate.ToString("yyyy-MM-dd")),
                BuildRatingDataXml(request.RatingData),
                BuildDiscountsXml(request.RequestedDiscounts),
                BuildOptionsXml(request.Options)
            )
        );

        return doc.ToString(SaveOptions.DisableFormatting);
    }

    private XElement BuildRatingDataXml(Dictionary<string, object> data)
    {
        return new XElement("RatingData",
            data?.Select(kvp =>
                new XElement("Factor",
                    new XElement("Name", kvp.Key),
                    new XElement("Value", kvp.Value)
                )
            )
        );
    }

    private XElement BuildDiscountsXml(List<string> discounts)
    {
        return new XElement("RequestedDiscounts",
            discounts?.Select(d =>
                new XElement("DiscountId", d)
            )
        );
    }

    private XElement BuildOptionsXml(RatingOptions options)
    {
        return new XElement("Options",
            new XElement("IncludeWorksheet", 
                options?.IncludeWorksheet ?? false),
            new XElement("ValidateOnly", 
                options?.ValidateOnly ?? false),
            new XElement("ApplyAllEligibleDiscounts", 
                options?.ApplyAllEligibleDiscounts ?? false),
            new XElement("RateVersion", 
                options?.RateVersion ?? "Current"),
            BuildOverridesXml(options?.OverrideValues)
        );
    }

    private XElement BuildOverridesXml(Dictionary<string, object> overrides)
    {
        return new XElement("Overrides",
            overrides?.Select(kvp =>
                new XElement("Override",
                    new XElement("Name", kvp.Key),
                    new XElement("Value", kvp.Value)
                )
            )
        );
    }

    // Additional XML building and parsing methods...
} 