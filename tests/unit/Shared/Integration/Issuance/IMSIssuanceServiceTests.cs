using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Moq;
using Xunit;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Caching.Memory;
using Shared.Integration.Issuance;

public class IMSIssuanceServiceTests
{
    private readonly IMSIssuanceService _service;
    private readonly Mock<IIMSClient> _imsClientMock;
    private readonly Mock<ILogger<IMSIssuanceService>> _loggerMock;
    private readonly Mock<IMemoryCache> _cacheMock;
    private readonly IMSIssuanceSettings _settings;

    public IMSIssuanceServiceTests()
    {
        _imsClientMock = new Mock<IIMSClient>();
        _loggerMock = new Mock<ILogger<IMSIssuanceService>>();
        _cacheMock = new Mock<IMemoryCache>();
        
        _settings = new IMSIssuanceSettings
        {
            EngineSettings = new IssuanceEngineSettings
            {
                EnableParallelProcessing = true,
                MaxConcurrentIssuance = 5
            }
        };

        _service = new IMSIssuanceService(
            _imsClientMock.Object,
            _loggerMock.Object,
            _cacheMock.Object,
            Options.Create(_settings));
    }

    [Fact]
    public async Task IssuePolicy_ValidRequest_ReturnsIssuanceResponse()
    {
        // Arrange
        var request = CreateSampleIssuanceRequest();
        SetupIssuanceResponse("ISS123", "POL123");

        // Act
        var response = await _service.IssuePolicy(request);

        // Assert
        Assert.NotNull(response);
        Assert.Equal("ISS123", response.IssuanceId);
        Assert.Equal("POL123", response.PolicyNumber);
        VerifyIssuanceRequest(request.QuoteId);
    }

    [Fact]
    public async Task GetIssuanceStatus_ValidId_ReturnsStatus()
    {
        // Arrange
        var issuanceId = "ISS123";
        SetupStatusRetrieval(issuanceId, IssuanceStatus.InProgress);

        // Act
        var status = await _service.GetIssuanceStatus(issuanceId);

        // Assert
        Assert.Equal(IssuanceStatus.InProgress, status);
        VerifyStatusRetrieval(issuanceId);
    }

    [Fact]
    public async Task ValidateForIssuance_WithErrors_ReturnsValidationErrors()
    {
        // Arrange
        var quoteId = "QUOTE123";
        var validationXml = CreateSampleValidationErrorsXml();
        SetupValidationResponse(quoteId, validationXml);

        // Act
        var errors = await _service.ValidateForIssuance(quoteId);

        // Assert
        Assert.NotNull(errors);
        Assert.NotEmpty(errors);
        Assert.Contains(errors, e => e.ErrorCode == "PAYMENT_REQUIRED");
        VerifyValidationRequest(quoteId);
    }

    [Fact]
    public async Task GetIssuanceWorkflow_ValidRequest_ReturnsWorkflow()
    {
        // Arrange
        var lineOfBusiness = "FLOOD";
        var state = "FL";
        var workflowXml = CreateSampleWorkflowXml();
        
        SetupWorkflowRetrieval(lineOfBusiness, state, workflowXml);
        SetupCache($"issuance_workflow_{lineOfBusiness}_{state}", null);

        // Act
        var workflow = await _service.GetIssuanceWorkflow(lineOfBusiness, state);

        // Assert
        Assert.NotNull(workflow);
        Assert.NotEmpty(workflow.Steps);
        Assert.All(workflow.Steps, step =>
        {
            Assert.NotNull(step.StepId);
            Assert.NotNull(step.Name);
        });
    }

    [Fact]
    public async Task SubmitRequirement_ValidSubmission_ReturnsSuccess()
    {
        // Arrange
        var issuanceId = "ISS123";
        var requirement = new IssuanceRequirementSubmission
        {
            RequirementId = "REQ123",
            Type = "PROOF_OF_INSURANCE",
            Content = new byte[] { 1, 2, 3 }
        };

        SetupRequirementSubmission(issuanceId, true);

        // Act
        var result = await _service.SubmitRequirement(issuanceId, requirement);

        // Assert
        Assert.True(result);
        VerifyRequirementSubmission(issuanceId);
    }

    // Helper methods
    private IssuanceRequest CreateSampleIssuanceRequest()
    {
        return new IssuanceRequest
        {
            QuoteId = "QUOTE123",
            EffectiveDate = DateTime.Today.AddDays(30),
            PaymentInfo = new PaymentInfo
            {
                PaymentMethod = "CreditCard",
                Amount = 1000.00m,
                TransactionId = "TRX123",
                TransactionDate = DateTime.Today,
                BillingInfo = new BillingInfo
                {
                    BillingType = "Monthly",
                    PaymentPlan = "Standard"
                }
            }
        };
    }

    private void SetupIssuanceResponse(string issuanceId, string policyNumber)
    {
        var responseXml = CreateSampleIssuanceResponseXml(issuanceId, policyNumber);
        _imsClientMock
            .Setup(x => x.SendRequestAsync<ExecuteCommandRequest, ExecuteCommandResponse>(
                It.IsAny<string>(),
                "IssuePolicy",
                It.IsAny<ExecuteCommandRequest>()))
            .ReturnsAsync(new ExecuteCommandResponse { Result = responseXml });
    }

    private void SetupStatusRetrieval(string issuanceId, IssuanceStatus status)
    {
        _imsClientMock
            .Setup(x => x.SendRequestAsync<ExecuteCommandRequest, ExecuteCommandResponse>(
                It.IsAny<string>(),
                "GetIssuanceStatus",
                It.Is<ExecuteCommandRequest>(r => 
                    r.Parameters.Contains(issuanceId))))
            .ReturnsAsync(new ExecuteCommandResponse { Result = status.ToString() });
    }

    private void SetupValidationResponse(string quoteId, string validationXml)
    {
        _imsClientMock
            .Setup(x => x.SendRequestAsync<ExecuteCommandRequest, ExecuteCommandResponse>(
                It.IsAny<string>(),
                "ValidateForIssuance",
                It.Is<ExecuteCommandRequest>(r => 
                    r.Parameters.Contains(quoteId))))
            .ReturnsAsync(new ExecuteCommandResponse { Result = validationXml });
    }

    private void SetupWorkflowRetrieval(string lineOfBusiness, string state, string workflowXml)
    {
        _imsClientMock
            .Setup(x => x.SendRequestAsync<ExecuteCommandRequest, ExecuteCommandResponse>(
                It.IsAny<string>(),
                "GetIssuanceWorkflow",
                It.Is<ExecuteCommandRequest>(r => 
                    r.Parameters.Contains(lineOfBusiness) && 
                    r.Parameters.Contains(state))))
            .ReturnsAsync(new ExecuteCommandResponse { Result = workflowXml });
    }

    private void SetupRequirementSubmission(string issuanceId, bool success)
    {
        _imsClientMock
            .Setup(x => x.SendRequestAsync<ExecuteCommandRequest, ExecuteCommandResponse>(
                It.IsAny<string>(),
                "SubmitRequirement",
                It.Is<ExecuteCommandRequest>(r => 
                    r.Parameters.Contains(issuanceId))))
            .ReturnsAsync(new ExecuteCommandResponse { Result = success.ToString() });
    }

    private void SetupCache<T>(string key, T value)
    {
        var cacheEntry = Mock.Of<ICacheEntry>();
        _cacheMock
            .Setup(x => x.CreateEntry(key))
            .Returns(cacheEntry);
    }

    private void VerifyIssuanceRequest(string quoteId)
    {
        _imsClientMock.Verify(
            x => x.SendRequestAsync<ExecuteCommandRequest, ExecuteCommandResponse>(
                It.IsAny<string>(),
                "IssuePolicy",
                It.Is<ExecuteCommandRequest>(r => 
                    r.Parameters.Contains(quoteId))),
            Times.Once);
    }

    private void VerifyStatusRetrieval(string issuanceId)
    {
        _imsClientMock.Verify(
            x => x.SendRequestAsync<ExecuteCommandRequest, ExecuteCommandResponse>(
                It.IsAny<string>(),
                "GetIssuanceStatus",
                It.Is<ExecuteCommandRequest>(r => 
                    r.Parameters.Contains(issuanceId))),
            Times.Once);
    }

    private void VerifyValidationRequest(string quoteId)
    {
        _imsClientMock.Verify(
            x => x.SendRequestAsync<ExecuteCommandRequest, ExecuteCommandResponse>(
                It.IsAny<string>(),
                "ValidateForIssuance",
                It.Is<ExecuteCommandRequest>(r => 
                    r.Parameters.Contains(quoteId))),
            Times.Once);
    }

    private void VerifyRequirementSubmission(string issuanceId)
    {
        _imsClientMock.Verify(
            x => x.SendRequestAsync<ExecuteCommandRequest, ExecuteCommandResponse>(
                It.IsAny<string>(),
                "SubmitRequirement",
                It.Is<ExecuteCommandRequest>(r => 
                    r.Parameters.Contains(issuanceId))),
            Times.Once);
    }

    private string CreateSampleIssuanceResponseXml(string issuanceId, string policyNumber)
    {
        return $@"
            <IssuanceResponse>
                <IssuanceId>{issuanceId}</IssuanceId>
                <PolicyNumber>{policyNumber}</PolicyNumber>
                <Status>InProgress</Status>
                <ProcessedDate>{DateTime.Now:yyyy-MM-dd}</ProcessedDate>
            </IssuanceResponse>";
    }

    private string CreateSampleValidationErrorsXml()
    {
        return @"
            <ValidationErrors>
                <Error>
                    <ErrorCode>PAYMENT_REQUIRED</ErrorCode>
                    <Message>Payment information is required</Message>
                    <Field>PaymentInfo</Field>
                    <IsBlocking>true</IsBlocking>
                </Error>
            </ValidationErrors>";
    }

    private string CreateSampleWorkflowXml()
    {
        return @"
            <IssuanceWorkflow>
                <WorkflowId>WF123</WorkflowId>
                <Steps>
                    <Step>
                        <StepId>STEP_001</StepId>
                        <Name>Payment Verification</Name>
                        <Description>Verify payment information</Description>
                        <IsRequired>true</IsRequired>
                        <Order>1</Order>
                    </Step>
                </Steps>
            </IssuanceWorkflow>";
    }
} 