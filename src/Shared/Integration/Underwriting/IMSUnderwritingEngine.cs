using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;

public class IMSUnderwritingEngine : IUnderwritingEngine
{
    private readonly IIMSClient _imsClient;
    private readonly ILogger<IMSUnderwritingEngine> _logger;
    private readonly IMemoryCache _cache;
    private readonly IMSUnderwritingSettings _settings;
    private readonly IRuleExpressionEvaluator _expressionEvaluator;

    public IMSUnderwritingEngine(
        IIMSClient imsClient,
        ILogger<IMSUnderwritingEngine> logger,
        IMemoryCache cache,
        IOptions<IMSUnderwritingSettings> settings,
        IRuleExpressionEvaluator expressionEvaluator)
    {
        _imsClient = imsClient;
        _logger = logger;
        _cache = cache;
        _settings = settings.Value;
        _expressionEvaluator = expressionEvaluator;
    }

    public async Task<UnderwritingResponse> EvaluateRules(string quoteId, UnderwritingRequest request)
    {
        try
        {
            _logger.LogInformation("Evaluating underwriting rules for quote {QuoteId}", quoteId);

            var rules = await GetActiveRules(request.LineOfBusiness, request.State);
            var applicableRules = FilterRulesByCategories(rules, request.RuleCategories);

            var ruleResults = new List<UnderwritingRuleResult>();
            var requiredActions = new List<UnderwritingAction>();
            var recommendations = new List<UnderwritingRecommendation>();

            foreach (var rule in applicableRules)
            {
                var result = await EvaluateRule(rule.RuleId, request.RiskData);
                ruleResults.Add(result);

                if (!result.Passed && result.RequiredAction != null)
                {
                    requiredActions.Add(result.RequiredAction);
                }
            }

            if (request.IncludeRecommendations)
            {
                recommendations = await GenerateRecommendations(request, ruleResults);
            }

            var decision = DetermineUnderwritingDecision(ruleResults);

            var response = new UnderwritingResponse
            {
                QuoteId = quoteId,
                EvaluationId = Guid.NewGuid().ToString(),
                EvaluationDate = DateTime.UtcNow,
                Decision = decision,
                RuleResults = ruleResults,
                RequiredActions = requiredActions,
                Recommendations = recommendations,
                EvaluatedBy = request.UnderwriterGuid ?? "SYSTEM"
            };

            await SaveEvaluationResults(quoteId, response);

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to evaluate underwriting rules for quote {QuoteId}", quoteId);
            throw new UnderwritingException("Rule evaluation failed", ex);
        }
    }

    public async Task<List<UnderwritingRule>> GetActiveRules(string lineOfBusiness, string state)
    {
        var cacheKey = $"uw_rules_{lineOfBusiness}_{state}";
        
        return await _cache.GetOrCreateAsync(cacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = 
                TimeSpan.FromMinutes(_settings.CacheExpirationMinutes);

            var response = await _imsClient.SendRequestAsync<ExecuteCommandRequest, ExecuteCommandResponse>(
                "UnderwritingFunctions.asmx",
                "GetActiveRules",
                new ExecuteCommandRequest
                {
                    ProcedureName = "GetActiveUnderwritingRules_WS",
                    Parameters = new[]
                    {
                        "@lineOfBusiness", lineOfBusiness,
                        "@state", state
                    }
                });

            return ParseUnderwritingRules(response.Result);
        });
    }

    public async Task<UnderwritingRuleResult> EvaluateRule(
        string ruleId, 
        Dictionary<string, object> data)
    {
        try
        {
            var rule = await GetRuleById(ruleId);
            if (rule == null)
            {
                throw new UnderwritingException($"Rule {ruleId} not found");
            }

            await ValidateRuleData(rule.ValidationMetadata, data);

            var passed = await _expressionEvaluator.Evaluate(rule.Expression, data);

            return new UnderwritingRuleResult
            {
                RuleId = ruleId,
                Passed = passed,
                Message = passed ? "Rule passed" : rule.Description,
                EvaluatedData = data,
                RequiredAction = !passed ? rule.Action : null,
                CanOverride = rule.AllowOverride,
                AllowedOverrideRoles = rule.RequiredRoles
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to evaluate rule {RuleId}", ruleId);
            throw new UnderwritingException($"Rule evaluation failed: {ruleId}", ex);
        }
    }

    public async Task<UnderwritingResponse> RequestRuleOverride(
        string quoteId, 
        RuleOverrideRequest request)
    {
        try
        {
            var rule = await GetRuleById(request.RuleId);
            if (rule == null || !rule.AllowOverride)
            {
                throw new UnderwritingException("Rule override not allowed");
            }

            var overrideXml = BuildOverrideXml(request);
            
            var response = await _imsClient.SendRequestAsync<ExecuteCommandRequest, ExecuteCommandResponse>(
                "UnderwritingFunctions.asmx",
                "RequestRuleOverride",
                new ExecuteCommandRequest
                {
                    ProcedureName = "RequestUnderwritingOverride_WS",
                    Parameters = new[]
                    {
                        "@quoteId", quoteId,
                        "@overrideXml", overrideXml
                    }
                });

            return ParseUnderwritingResponse(response.Result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to request override for quote {QuoteId}, rule {RuleId}", 
                quoteId, request.RuleId);
            throw new UnderwritingException("Override request failed", ex);
        }
    }

    public async Task<List<UnderwritingAction>> GetAvailableActions(string quoteId)
    {
        try
        {
            var response = await _imsClient.SendRequestAsync<ExecuteCommandRequest, ExecuteCommandResponse>(
                "UnderwritingFunctions.asmx",
                "GetAvailableActions",
                new ExecuteCommandRequest
                {
                    ProcedureName = "GetUnderwritingActions_WS",
                    Parameters = new[] { "@quoteId", quoteId }
                });

            return ParseUnderwritingActions(response.Result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get available actions for quote {QuoteId}", quoteId);
            throw new UnderwritingException("Failed to retrieve actions", ex);
        }
    }

    private List<UnderwritingRule> FilterRulesByCategories(
        List<UnderwritingRule> rules,
        List<string> categories)
    {
        if (categories == null || !categories.Any())
        {
            return rules;
        }

        return rules.Where(r => categories.Contains(r.Category)).ToList();
    }

    private async Task<List<UnderwritingRecommendation>> GenerateRecommendations(
        UnderwritingRequest request,
        List<UnderwritingRuleResult> ruleResults)
    {
        // Implementation depends on business logic for generating recommendations
        // based on rule results and risk data
        return new List<UnderwritingRecommendation>();
    }

    private UnderwritingDecision DetermineUnderwritingDecision(
        List<UnderwritingRuleResult> ruleResults)
    {
        var hasBlockingRules = ruleResults.Any(r => 
            !r.Passed && 
            r.RequiredAction?.Type == ActionType.UnderwriterReview);

        var hasRequiredActions = ruleResults.Any(r => 
            !r.Passed && 
            r.RequiredAction != null);

        if (hasBlockingRules)
        {
            return UnderwritingDecision.ReferToUnderwriter;
        }
        else if (hasRequiredActions)
        {
            return UnderwritingDecision.PendingAction;
        }
        
        return UnderwritingDecision.Approved;
    }

    private async Task SaveEvaluationResults(string quoteId, UnderwritingResponse response)
    {
        var resultXml = BuildEvaluationResultXml(response);
        
        await _imsClient.SendRequestAsync<ExecuteCommandRequest, ExecuteCommandResponse>(
            "UnderwritingFunctions.asmx",
            "SaveEvaluationResults",
            new ExecuteCommandRequest
            {
                ProcedureName = "SaveUnderwritingResults_WS",
                Parameters = new[]
                {
                    "@quoteId", quoteId,
                    "@resultXml", resultXml
                }
            });
    }

    private async Task<UnderwritingRule> GetRuleById(string ruleId)
    {
        var cacheKey = $"uw_rule_{ruleId}";
        
        return await _cache.GetOrCreateAsync(cacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = 
                TimeSpan.FromMinutes(_settings.CacheExpirationMinutes);

            var response = await _imsClient.SendRequestAsync<ExecuteCommandRequest, ExecuteCommandResponse>(
                "UnderwritingFunctions.asmx",
                "GetRuleById",
                new ExecuteCommandRequest
                {
                    ProcedureName = "GetUnderwritingRule_WS",
                    Parameters = new[] { "@ruleId", ruleId }
                });

            return ParseUnderwritingRule(response.Result);
        });
    }

    private async Task ValidateRuleData(
        ValidationMetadata metadata, 
        Dictionary<string, object> data)
    {
        if (metadata?.RequiredFields == null) return;

        var errors = new List<string>();

        foreach (var field in metadata.RequiredFields)
        {
            if (!data.ContainsKey(field) || data[field] == null)
            {
                errors.Add($"Required field {field} is missing");
            }
        }

        if (errors.Any())
        {
            throw new UnderwritingException(
                "Invalid rule data: " + string.Join(", ", errors));
        }
    }

    // XML parsing methods...
    private List<UnderwritingRule> ParseUnderwritingRules(string xml)
    {
        var doc = XDocument.Parse(xml);
        return doc.Root.Elements("Rule")
            .Select(ParseUnderwritingRule)
            .ToList();
    }

    private UnderwritingRule ParseUnderwritingRule(XElement element)
    {
        return new UnderwritingRule
        {
            RuleId = element.Element("RuleId").Value,
            Name = element.Element("Name").Value,
            Description = element.Element("Description").Value,
            Category = element.Element("Category").Value,
            Severity = int.Parse(element.Element("Severity").Value),
            IsActive = bool.Parse(element.Element("IsActive").Value),
            Expression = element.Element("Expression").Value,
            Action = ParseUnderwritingAction(element.Element("Action")),
            AllowOverride = bool.Parse(element.Element("AllowOverride").Value),
            RequiredRoles = element.Element("RequiredRoles")
                ?.Elements("Role")
                .Select(r => r.Value)
                .ToList() ?? new List<string>(),
            ValidationMetadata = ParseValidationMetadata(element.Element("ValidationMetadata"))
        };
    }

    private UnderwritingAction ParseUnderwritingAction(XElement element)
    {
        if (element == null) return null;

        return new UnderwritingAction
        {
            ActionId = element.Element("ActionId").Value,
            Name = element.Element("Name").Value,
            Type = Enum.Parse<ActionType>(element.Element("Type").Value),
            Description = element.Element("Description").Value,
            IsRequired = bool.Parse(element.Element("IsRequired").Value),
            DueDate = element.Element("DueDate")?.Value != null
                ? DateTime.Parse(element.Element("DueDate").Value)
                : null,
            AssignedTo = element.Element("AssignedTo")?.Value,
            Status = Enum.Parse<ActionStatus>(element.Element("Status").Value)
        };
    }

    // XML building methods...
    private string BuildOverrideXml(RuleOverrideRequest request)
    {
        var doc = new XDocument(
            new XElement("Override",
                new XElement("RuleId", request.RuleId),
                new XElement("Reason", request.Reason),
                new XElement("UnderwriterGuid", request.UnderwriterGuid),
                new XElement("AdditionalData",
                    request.AdditionalData?.Select(kvp =>
                        new XElement("Data",
                            new XElement("Key", kvp.Key),
                            new XElement("Value", kvp.Value)
                        )
                    )
                )
            )
        );

        return doc.ToString(SaveOptions.DisableFormatting);
    }

    private string BuildEvaluationResultXml(UnderwritingResponse response)
    {
        var doc = new XDocument(
            new XElement("UnderwritingEvaluation",
                new XElement("EvaluationId", response.EvaluationId),
                new XElement("QuoteId", response.QuoteId),
                new XElement("EvaluationDate", response.EvaluationDate),
                new XElement("Decision", response.Decision),
                new XElement("EvaluatedBy", response.EvaluatedBy),
                new XElement("RuleResults",
                    response.RuleResults.Select(r =>
                        new XElement("RuleResult",
                            new XElement("RuleId", r.RuleId),
                            new XElement("Passed", r.Passed),
                            new XElement("Message", r.Message)
                        )
                    )
                )
            )
        );

        return doc.ToString(SaveOptions.DisableFormatting);
    }
} 