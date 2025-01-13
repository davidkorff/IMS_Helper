using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Moq;
using Xunit;
using ApiGateway.Services.Policies;
using ApiGateway.Models;
using ApiGateway.Interfaces;
using ApiGateway.Validators;
using Microsoft.Extensions.Logging;

public class PolicyServiceTests
{
    private readonly PolicyService _service;
    private readonly Mock<IIMSClient> _imsClientMock;
    private readonly Mock<IDocumentService> _documentServiceMock;
    private readonly Mock<IPolicyHistoryRepository> _historyRepositoryMock;
    private readonly Mock<ILogger<PolicyService>> _loggerMock;
    private readonly Mock<IValidator<EndorsementRequest>> _endorsementValidatorMock;
    private readonly Mock<IValidator<CancellationRequest>> _cancellationValidatorMock;

    public PolicyServiceTests()
    {
        _imsClientMock = new Mock<IIMSClient>();
        _documentServiceMock = new Mock<IDocumentService>();
        _historyRepositoryMock = new Mock<IPolicyHistoryRepository>();
        _loggerMock = new Mock<ILogger<PolicyService>>();
        _endorsementValidatorMock = new Mock<IValidator<EndorsementRequest>>();
        _cancellationValidatorMock = new Mock<IValidator<CancellationRequest>>();

        _service = new PolicyService(
            _imsClientMock.Object,
            _documentServiceMock.Object,
            _historyRepositoryMock.Object,
            _loggerMock.Object,
            _endorsementValidatorMock.Object,
            _cancellationValidatorMock.Object
        );
    }

    [Fact]
    public async Task GetPolicy_ReturnsPolicyWithEndorsements()
    {
        // Arrange
        var policyId = "P123";
        var policy = new PolicyResponse { PolicyId = policyId, Status = PolicyStatus.Active };
        var endorsements = new List<EndorsementResponse>
        {
            new() { EndorsementId = "E1", PolicyId = policyId }
        };

        _imsClientMock.Setup(x => x.GetPolicy(policyId)).ReturnsAsync(policy);
        _imsClientMock.Setup(x => x.GetPolicyEndorsements(policyId)).ReturnsAsync(endorsements);

        // Act
        var result = await _service.GetPolicy(policyId);

        // Assert
        Assert.Equal(policyId, result.PolicyId);
        Assert.Single(result.Endorsements);
    }

    [Fact]
    public async Task CreateEndorsement_ValidRequest_CreatesEndorsement()
    {
        // Arrange
        var policyId = "P123";
        var request = new EndorsementRequest
        {
            EffectiveDate = DateTime.UtcNow.AddDays(1),
            Description = "Test endorsement"
        };

        var policy = new PolicyResponse { PolicyId = policyId, Status = PolicyStatus.Active };
        var expectedResponse = new EndorsementResponse { EndorsementId = "E1", PolicyId = policyId };

        _endorsementValidatorMock.Setup(x => x.ValidateAsync(request, default))
            .ReturnsAsync(new ValidationResult());
        _imsClientMock.Setup(x => x.GetPolicy(policyId)).ReturnsAsync(policy);
        _imsClientMock.Setup(x => x.CreateEndorsement(policyId, request))
            .ReturnsAsync(expectedResponse);

        // Act
        var result = await _service.CreateEndorsement(policyId, request);

        // Assert
        Assert.Equal(expectedResponse.EndorsementId, result.EndorsementId);
        _historyRepositoryMock.Verify(x => x.AddEntry(It.IsAny<PolicyHistoryEntry>()), Times.Once);
    }

    [Fact]
    public async Task CancelPolicy_ValidRequest_CancelsPolicy()
    {
        // Arrange
        var policyId = "P123";
        var request = new CancellationRequest
        {
            CancellationDate = DateTime.UtcNow.AddDays(1),
            Reason = CancellationReason.InsuredRequest
        };

        var policy = new PolicyResponse { PolicyId = policyId, Status = PolicyStatus.Active };
        var expectedResponse = new CancellationResponse 
        { 
            CancellationId = "C1", 
            PolicyId = policyId,
            ReturnPremium = 100.00m
        };

        _cancellationValidatorMock.Setup(x => x.ValidateAsync(request, default))
            .ReturnsAsync(new ValidationResult());
        _imsClientMock.Setup(x => x.GetPolicy(policyId)).ReturnsAsync(policy);
        _imsClientMock.Setup(x => x.CancelPolicy(policyId, request))
            .ReturnsAsync(expectedResponse);

        // Act
        var result = await _service.CancelPolicy(policyId, request);

        // Assert
        Assert.Equal(expectedResponse.CancellationId, result.CancellationId);
        _historyRepositoryMock.Verify(x => x.AddEntry(It.IsAny<PolicyHistoryEntry>()), Times.Once);
    }

    [Fact]
    public async Task ReinstatePolicy_ValidRequest_ReinstatesPolicy()
    {
        // Arrange
        var policyId = "P123";
        var request = new ReinstatementRequest { Reason = "Payment received" };
        var policy = new PolicyResponse { PolicyId = policyId, Status = PolicyStatus.Cancelled };
        var reinstatedPolicy = new PolicyResponse { PolicyId = policyId, Status = PolicyStatus.Active };

        _imsClientMock.Setup(x => x.GetPolicy(policyId)).ReturnsAsync(policy);
        _imsClientMock.Setup(x => x.ReinstatePolicy(policyId, request))
            .ReturnsAsync(reinstatedPolicy);

        // Act
        var result = await _service.ReinstatePolicy(policyId, request);

        // Assert
        Assert.Equal(PolicyStatus.Active, result.Status);
        _historyRepositoryMock.Verify(x => x.AddEntry(It.IsAny<PolicyHistoryEntry>()), Times.Once);
    }
} 