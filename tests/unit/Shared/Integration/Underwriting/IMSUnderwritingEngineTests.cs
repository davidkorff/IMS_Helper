using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Moq;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Caching.Memory;
using System.Xml.Linq;

public class IMSUnderwritingEngineTests
{
    private readonly IMSUnderwritingEngine _engine;
    private readonly Mock<IIMSClient> _imsClientMock;
    private readonly Mock<ILogger<IMSUnderwritingEngine>> _loggerMock;
    private readonly Mock<IMemoryCache> _cacheMock;
    private readonly Mock<IRuleExpressionEvaluator> _evaluatorMock;
    private readonly IMSUnderwritingSettings _settings;

    public IMSUnderwritingEngineTests()
    {
        _imsClientMock = new Mock<IIMSClient>();
        _loggerMock = new Mock<ILogger<IMSUnderwritingEngine>>();
        _cacheMock = new Mock<IMemoryCache>();
        _evaluatorMock = new Mock<IRuleExpressionEvaluator>();
        
        _settings = new IMSUnderwritingSettings
        {
            CacheExpirationMinutes = 60,
            EnableStrictValidation = true,
            RuleEvaluation = new RuleEvaluationSettings
            {
                MaxConcurrentEvaluations = 5,
                EvaluationTimeoutSeconds = 30
            }
        };

        _engine = new IMSUnderwritingEngine(
            _imsClientMock.Object,
            _loggerMock.Object,
            _cacheMock.Object,
            Options.Create(_settings),
            _evaluatorMock.Object);
    }

    [Fact]
    public async Task EvaluateRules_AllRulesPassed_ReturnsApproved()
    {
        // Arrange
        var quoteId = "Q123";
        var request = CreateSampleRequest(quoteId);
        var rules = CreateSampleRules();

        SetupGetActiveRules(rules);
        SetupRuleEvaluation(true);

        // Act
        var response = await _engine.EvaluateRules(quoteId, request);

        // Assert
        Assert.Equal(UnderwritingDecision.Approved, response.Decision);
        Assert.All(response.RuleResults, r => Assert.True(r.Passed));
        Assert.Empty(response.RequiredActions);
    }

    [Fact]
    public async Task EvaluateRules_BlockingRuleFailed_ReturnsReferToUnderwriter()
    {
        // Arrange
        var quoteId = "Q123";
        var request = CreateSampleRequest(quoteId);
        var rules = new List<UnderwritingRule>
        {
            new UnderwritingRule
            {
                RuleId = "R1",
                Action = new UnderwritingAction
                {
                    Type = ActionType.UnderwriterReview
                }
            }
        };

        SetupGetActiveRules(rules);
        SetupRuleEvaluation(false);

        // Act
        var response = await _engine.EvaluateRules(quoteId, request);

        // Assert
        Assert.Equal(UnderwritingDecision.ReferToUnderwriter, response.Decision);
        Assert.Contains(response.RequiredActions, 
            a => a.Type == ActionType.UnderwriterReview);
    }

    [Fact]
    public async Task RequestRuleOverride_ValidRequest_ProcessesOverride()
    {
        // Arrange
        var quoteId = "Q123";
        var ruleId = "R1";
        var request = new RuleOverrideRequest
        {
            QuoteId = quoteId,
            RuleId = ruleId,
            Reason = "Test override",
            UnderwriterGuid = "UW123"
        };

        SetupRuleOverride(quoteId, ruleId);

        // Act
        var response = await _engine.RequestRuleOverride(quoteId, request);

        // Assert
        Assert.NotNull(response);
        VerifyOverrideWasSaved(quoteId, ruleId);
    }

    private UnderwritingRequest CreateSampleRequest(string quoteId)
    {
        return new UnderwritingRequest
        {
            QuoteId = quoteId,
            LineOfBusiness = "FLOOD",
            State = "FL",
            RiskData = new Dictionary<string, object>
            {
                { "COVERAGE_A", 250000 },
                { "CONSTRUCTION_TYPE", "1" }
            }
        };
    }

    private List<UnderwritingRule> CreateSampleRules()
    {
        return new List<UnderwritingRule>
        {
            new UnderwritingRule
            {
                RuleId = "R1",
                Expression = "[COVERAGE_A] <= 1000000",
                IsActive = true
            },
            new UnderwritingRule
            {
                RuleId = "R2",
                Expression = "[CONSTRUCTION_TYPE] IN ('1','2','3')",
                IsActive = true
            }
        };
    }

    private void SetupGetActiveRules(List<UnderwritingRule> rules)
    {
        var rulesXml = CreateRulesXml(rules);
        _imsClientMock
            .Setup(x => x.SendRequestAsync<ExecuteCommandRequest, ExecuteCommandResponse>(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.Is<ExecuteCommandRequest>(r => 
                    r.ProcedureName == "GetActiveUnderwritingRules_WS")))
            .ReturnsAsync(new ExecuteCommandResponse { Result = rulesXml });
    }

    private void SetupRuleEvaluation(bool passed)
    {
        _evaluatorMock
            .Setup(x => x.Evaluate(It.IsAny<string>(), It.IsAny<Dictionary<string, object>>()))
            .ReturnsAsync(passed);
    }

    private void SetupRuleOverride(string quoteId, string ruleId)
    {
        _imsClientMock
            .Setup(x => x.SendRequestAsync<ExecuteCommandRequest, ExecuteCommandResponse>(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.Is<ExecuteCommandRequest>(r => 
                    r.ProcedureName == "RequestUnderwritingOverride_WS")))
            .ReturnsAsync(new ExecuteCommandResponse 
            { 
                Result = CreateOverrideResponseXml(quoteId, ruleId) 
            });
    }

    private void VerifyOverrideWasSaved(string quoteId, string ruleId)
    {
        _imsClientMock.Verify(
            x => x.SendRequestAsync<ExecuteCommandRequest, ExecuteCommandResponse>(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.Is<ExecuteCommandRequest>(r => 
                    r.ProcedureName == "RequestUnderwritingOverride_WS" &&
                    r.Parameters.Contains(quoteId) &&
                    r.Parameters.Contains(ruleId))),
            Times.Once);
    }

    private string CreateRulesXml(List<UnderwritingRule> rules)
    {
        var doc = new XDocument(
            new XElement("Rules",
                rules.Select(r =>
                    new XElement("Rule",
                        new XElement("RuleId", r.RuleId),
                        new XElement("Expression", r.Expression),
                        new XElement("IsActive", r.IsActive)
                    )
                )
            )
        );
        return doc.ToString();
    }

    private string CreateOverrideResponseXml(string quoteId, string ruleId)
    {
        return $@"
            <OverrideResponse>
                <QuoteId>{quoteId}</QuoteId>
                <RuleId>{ruleId}</RuleId>
                <Status>Approved</Status>
                <ProcessedDate>{DateTime.UtcNow:yyyy-MM-dd}</ProcessedDate>
            </OverrideResponse>";
    }
} 