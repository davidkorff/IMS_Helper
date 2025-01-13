using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Moq;
using Xunit;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

public class IMSEndorsementServiceTests
{
    private readonly IMSEndorsementService _service;
    private readonly Mock<IIMSClient> _imsClientMock;
    private readonly Mock<ILogger<IMSEndorsementService>> _loggerMock;
    private readonly Mock<IMemoryCache> _cacheMock;
    private readonly IMSEndorsementSettings _settings;

    public IMSEndorsementServiceTests()
    {
        _imsClientMock = new Mock<IIMSClient>();
        _loggerMock = new Mock<ILogger<IMSEndorsementService>>();
        _cacheMock = new Mock<IMemoryCache>();
        
        _settings = new IMSEndorsementSettings
        {
            EngineSettings = new EndorsementEngineSettings
            {
                EnableParallelProcessing = true,
                MaxConcurrentEndorsements = 5
            }
        };

        _service = new IMSEndorsementService(
            _imsClientMock.Object,
            _loggerMock.Object,
            _cacheMock.Object,
            Options.Create(_settings));
    }

    [Fact]
    public async Task CreateEndorsement_ValidRequest_ReturnsEndorsementResponse()
    {
        // Arrange
        var request = CreateSampleEndorsementRequest();
        SetupEndorsementResponse("END123", "POL123");

        // Act
        var response = await _service.CreateEndorsement(request);

        // Assert
        Assert.NotNull(response);
        Assert.Equal("END123", response.EndorsementId);
        Assert.Equal("POL123", response.PolicyNumber);
        VerifyEndorsementRequest(request.PolicyNumber);
    }

    [Fact]
    public async Task GetEndorsementStatus_ValidId_ReturnsStatus()
    {
        // Arrange
        var endorsementId = "END123";
        SetupStatusRetrieval(endorsementId, EndorsementStatus.PendingApproval);

        // Act
        var status = await _service.GetEndorsementStatus(endorsementId);

        // Assert
        Assert.Equal(EndorsementStatus.PendingApproval, status);
        VerifyStatusRetrieval(endorsementId);
    }

    [Fact]
    public async Task ValidateEndorsement_WithErrors_ReturnsValidationErrors()
    {
        // Arrange
        var endorsementId = "END123";
        var validationXml = CreateSampleValidationErrorsXml();
        SetupValidationResponse(endorsementId, validationXml);

        // Act
        var errors = await _service.ValidateEndorsement(endorsementId);

        // Assert
        Assert.NotNull(errors);
        Assert.NotEmpty(errors);
        Assert.Contains(errors, e => e.ErrorCode == "INVALID_DATE");
        VerifyValidationRequest(endorsementId);
    }

    [Fact]
    public async Task GetEndorsementWorkflow_ValidRequest_ReturnsWorkflow()
    {
        // Arrange
        var lineOfBusiness = "AUTO";
        var state = "CA";
        var workflowXml = CreateSampleWorkflowXml();
        
        SetupWorkflowRetrieval(lineOfBusiness, state, workflowXml);
        SetupCache($"endorsement_workflow_{lineOfBusiness}_{state}", null);

        // Act
        var workflow = await _service.GetEndorsementWorkflow(lineOfBusiness, state);

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
        var endorsementId = "END123";
        var requirement = new EndorsementRequirementSubmission
        {
            RequirementId = "REQ123",
            Type = "PROOF_OF_LOSS",
            Content = new byte[] { 1, 2, 3 }
        };

        SetupRequirementSubmission(endorsementId, true);

        // Act
        var result = await _service.SubmitRequirement(endorsementId, requirement);

        // Assert
        Assert.True(result);
        VerifyRequirementSubmission(endorsementId);
    }

    // Helper methods
    private EndorsementRequest CreateSampleEndorsementRequest()
    {
        return new EndorsementRequest
        {
            PolicyNumber = "POL123",
            EndorsementType = "ADDRESS_CHANGE",
            EffectiveDate = DateTime.Today.AddDays(30),
            Changes = new Dictionary<string, object>
            {
                { "MailingAddress.Street", "123 New Street" },
                { "MailingAddress.City", "New City" }
            },
            RequestedBy = "Test User",
            Reason = "Address Update"
        };
    }

    private void SetupEndorsementResponse(string endorsementId, string policyNumber)
    {
        var responseXml = CreateSampleEndorsementResponseXml(endorsementId, policyNumber);
        _imsClientMock
            .Setup(x => x.SendRequestAsync<ExecuteCommandRequest, ExecuteCommandResponse>(
                It.IsAny<string>(),
                "CreateEndorsement",
                It.IsAny<ExecuteCommandRequest>()))
            .ReturnsAsync(new ExecuteCommandResponse { Result = responseXml });
    }

    private void SetupStatusRetrieval(string endorsementId, EndorsementStatus status)
    {
        _imsClientMock
            .Setup(x => x.SendRequestAsync<ExecuteCommandRequest, ExecuteCommandResponse>(
                It.IsAny<string>(),
                "GetEndorsementStatus",
                It.Is<ExecuteCommandRequest>(r => 
                    r.Parameters.Contains(endorsementId))))
            .ReturnsAsync(new ExecuteCommandResponse { Result = status.ToString() });
    }

    private void SetupValidationResponse(string endorsementId, string validationXml)
    {
        _imsClientMock
            .Setup(x => x.SendRequestAsync<ExecuteCommandRequest, ExecuteCommandResponse>(
                It.IsAny<string>(),
                "ValidateEndorsement",
                It.Is<ExecuteCommandRequest>(r => 
                    r.Parameters.Contains(endorsementId))))
            .ReturnsAsync(new ExecuteCommandResponse { Result = validationXml });
    }

    private void SetupWorkflowRetrieval(string lineOfBusiness, string state, string workflowXml)
    {
        _imsClientMock
            .Setup(x => x.SendRequestAsync<ExecuteCommandRequest, ExecuteCommandResponse>(
                It.IsAny<string>(),
                "GetEndorsementWorkflow",
                It.Is<ExecuteCommandRequest>(r => 
                    r.Parameters.Contains(lineOfBusiness) && 
                    r.Parameters.Contains(state))))
            .ReturnsAsync(new ExecuteCommandResponse { Result = workflowXml });
    }

    private void SetupRequirementSubmission(string endorsementId, bool success)
    {
        _imsClientMock
            .Setup(x => x.SendRequestAsync<ExecuteCommandRequest, ExecuteCommandResponse>(
                It.IsAny<string>(),
                "SubmitRequirement",
                It.Is<ExecuteCommandRequest>(r => 
                    r.Parameters.Contains(endorsementId))))
            .ReturnsAsync(new ExecuteCommandResponse { Result = success.ToString() });
    }

    private void SetupCache<T>(string key, T value)
    {
        var cacheEntry = Mock.Of<ICacheEntry>();
        _cacheMock
            .Setup(x => x.CreateEntry(key))
            .Returns(cacheEntry);
    }

    private void VerifyEndorsementRequest(string policyNumber)
    {
        _imsClientMock.Verify(
            x => x.SendRequestAsync<ExecuteCommandRequest, ExecuteCommandResponse>(
                It.IsAny<string>(),
                "CreateEndorsement",
                It.Is<ExecuteCommandRequest>(r => 
                    r.Parameters.Contains(policyNumber))),
            Times.Once);
    }

    private void VerifyStatusRetrieval(string endorsementId)
    {
        _imsClientMock.Verify(
            x => x.SendRequestAsync<ExecuteCommandRequest, ExecuteCommandResponse>(
                It.IsAny<string>(),
                "GetEndorsementStatus",
                It.Is<ExecuteCommandRequest>(r => 
                    r.Parameters.Contains(endorsementId))),
            Times.Once);
    }

    private void VerifyValidationRequest(string endorsementId)
    {
        _imsClientMock.Verify(
            x => x.SendRequestAsync<ExecuteCommandRequest, ExecuteCommandResponse>(
                It.IsAny<string>(),
                "ValidateEndorsement",
                It.Is<ExecuteCommandRequest>(r => 
                    r.Parameters.Contains(endorsementId))),
            Times.Once);
    }

    private void VerifyRequirementSubmission(string endorsementId)
    {
        _imsClientMock.Verify(
            x => x.SendRequestAsync<ExecuteCommandRequest, ExecuteCommandResponse>(
                It.IsAny<string>(),
                "SubmitRequirement",
                It.Is<ExecuteCommandRequest>(r => 
                    r.Parameters.Contains(endorsementId))),
            Times.Once);
    }

    private string CreateSampleEndorsementResponseXml(string endorsementId, string policyNumber)
    {
        return $@"
            <EndorsementResponse>
                <EndorsementId>{endorsementId}</EndorsementId>
                <PolicyNumber>{policyNumber}</PolicyNumber>
                <Status>Draft</Status>
                <ProcessedDate>{DateTime.Now:yyyy-MM-dd}</ProcessedDate>
            </EndorsementResponse>";
    }

    private string CreateSampleValidationErrorsXml()
    {
        return @"
            <ValidationErrors>
                <Error>
                    <ErrorCode>INVALID_DATE</ErrorCode>
                    <Message>Effective date must be in the future</Message>
                    <Field>EffectiveDate</Field>
                    <IsBlocking>true</IsBlocking>
                </Error>
            </ValidationErrors>";
    }

    private string CreateSampleWorkflowXml()
    {
        return @"
            <EndorsementWorkflow>
                <WorkflowId>WF123</WorkflowId>
                <Steps>
                    <Step>
                        <StepId>STEP_001</StepId>
                        <Name>Validation</Name>
                        <Description>Validate endorsement data</Description>
                        <IsRequired>true</IsRequired>
                        <Order>1</Order>
                    </Step>
                </Steps>
            </EndorsementWorkflow>";
    }
} 