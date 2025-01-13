using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;
using Moq;
using Microsoft.Extensions.Logging;
using ApiGateway.Services.Claims;
using ApiGateway.Models;
using ApiGateway.Validators;

public class ClaimsServiceTests
{
    private readonly ClaimsService _service;
    private readonly Mock<IIMSClient> _imsClientMock;
    private readonly Mock<IDocumentService> _documentServiceMock;
    private readonly Mock<IPolicyService> _policyServiceMock;
    private readonly Mock<ILogger<ClaimsService>> _loggerMock;
    private readonly Mock<IValidator<CreateClaimRequest>> _createClaimValidatorMock;
    private readonly Mock<IValidator<UpdateClaimStatusRequest>> _updateStatusValidatorMock;

    public ClaimsServiceTests()
    {
        _imsClientMock = new Mock<IIMSClient>();
        _documentServiceMock = new Mock<IDocumentService>();
        _policyServiceMock = new Mock<IPolicyService>();
        _loggerMock = new Mock<ILogger<ClaimsService>>();
        _createClaimValidatorMock = new Mock<IValidator<CreateClaimRequest>>();
        _updateStatusValidatorMock = new Mock<IValidator<UpdateClaimStatusRequest>>();

        _service = new ClaimsService(
            _imsClientMock.Object,
            _documentServiceMock.Object,
            _policyServiceMock.Object,
            _loggerMock.Object,
            _createClaimValidatorMock.Object,
            _updateStatusValidatorMock.Object
        );
    }

    [Fact]
    public async Task CreateClaim_ValidRequest_CreatesClaim()
    {
        // Arrange
        var request = new CreateClaimRequest
        {
            PolicyId = "P123",
            DateOfLoss = DateTime.UtcNow.AddDays(-1),
            Type = ClaimType.Property
        };

        var policy = new PolicyResponse
        {
            PolicyId = "P123",
            Status = PolicyStatus.Active
        };

        var expectedClaim = new ClaimResponse
        {
            ClaimId = "C123",
            PolicyId = "P123",
            Status = ClaimStatus.New
        };

        _createClaimValidatorMock.Setup(x => x.ValidateAsync(request, default))
            .ReturnsAsync(new ValidationResult());
        _policyServiceMock.Setup(x => x.GetPolicy(request.PolicyId))
            .ReturnsAsync(policy);
        _imsClientMock.Setup(x => x.CreateClaim(request))
            .ReturnsAsync(expectedClaim);

        // Act
        var result = await _service.CreateClaim(request);

        // Assert
        Assert.Equal(expectedClaim.ClaimId, result.ClaimId);
        Assert.Equal(ClaimStatus.New, result.Status);
    }

    [Fact]
    public async Task CreateClaim_InactivePolicy_ThrowsException()
    {
        // Arrange
        var request = new CreateClaimRequest { PolicyId = "P123" };
        var policy = new PolicyResponse
        {
            PolicyId = "P123",
            Status = PolicyStatus.Cancelled
        };

        _createClaimValidatorMock.Setup(x => x.ValidateAsync(request, default))
            .ReturnsAsync(new ValidationResult());
        _policyServiceMock.Setup(x => x.GetPolicy(request.PolicyId))
            .ReturnsAsync(policy);

        // Act & Assert
        await Assert.ThrowsAsync<PolicyException>(() => 
            _service.CreateClaim(request));
    }

    [Fact]
    public async Task UpdateClaimStatus_ValidTransition_UpdatesStatus()
    {
        // Arrange
        var claimId = "C123";
        var request = new UpdateClaimStatusRequest
        {
            NewStatus = ClaimStatus.UnderReview,
            Reason = "Starting review"
        };

        var currentClaim = new ClaimResponse
        {
            ClaimId = claimId,
            Status = ClaimStatus.New
        };

        var updatedClaim = new ClaimResponse
        {
            ClaimId = claimId,
            Status = ClaimStatus.UnderReview
        };

        _updateStatusValidatorMock.Setup(x => x.ValidateAsync(request, default))
            .ReturnsAsync(new ValidationResult());
        _imsClientMock.Setup(x => x.GetClaim(claimId))
            .ReturnsAsync(currentClaim);
        _imsClientMock.Setup(x => x.UpdateClaimStatus(claimId, request))
            .ReturnsAsync(updatedClaim);

        // Act
        var result = await _service.UpdateClaimStatus(claimId, request);

        // Assert
        Assert.Equal(ClaimStatus.UnderReview, result.Status);
    }

    [Fact]
    public async Task UpdateClaimStatus_InvalidTransition_ThrowsException()
    {
        // Arrange
        var claimId = "C123";
        var request = new UpdateClaimStatusRequest
        {
            NewStatus = ClaimStatus.Closed,
            Reason = "Invalid transition"
        };

        var currentClaim = new ClaimResponse
        {
            ClaimId = claimId,
            Status = ClaimStatus.New
        };

        _updateStatusValidatorMock.Setup(x => x.ValidateAsync(request, default))
            .ReturnsAsync(new ValidationResult());
        _imsClientMock.Setup(x => x.GetClaim(claimId))
            .ReturnsAsync(currentClaim);

        // Act & Assert
        await Assert.ThrowsAsync<ValidationException>(() => 
            _service.UpdateClaimStatus(claimId, request));
    }
} 