using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Moq;
using Xunit;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Caching.Memory;
using Shared.Integration.Cancellation;
using Shared.Integration.Client;

public class IMSCancellationServiceTests
{
    private readonly IMSCancellationService _service;
    private readonly Mock<IIMSClient> _imsClientMock;
    private readonly Mock<ILogger<IMSCancellationService>> _loggerMock;
    private readonly Mock<IMemoryCache> _cacheMock;
    private readonly IMSCancellationSettings _settings;

    public IMSCancellationServiceTests()
    {
        _imsClientMock = new Mock<IIMSClient>();
        _loggerMock = new Mock<ILogger<IMSCancellationService>>();
        _cacheMock = new Mock<IMemoryCache>();
        
        _settings = new IMSCancellationSettings
        {
            EngineSettings = new CancellationEngineSettings
            {
                EnableParallelProcessing = true,
                MaxConcurrentCancellations = 5
            }
        };

        _service = new IMSCancellationService(
            _imsClientMock.Object,
            _loggerMock.Object,
            _cacheMock.Object,
            Options.Create(_settings));
    }

    [Fact]
    public async Task InitiateCancellation_ValidRequest_ReturnsCancellationResponse()
    {
        // Arrange
        var request = CreateSampleCancellationRequest();
        SetupCancellationResponse("CAN123", "POL123");

        // Act
        var response = await _service.InitiateCancellation(request);

        // Assert
        Assert.NotNull(response);
        Assert.Equal("CAN123", response.CancellationId);
        Assert.Equal("POL123", response.PolicyNumber);
        VerifyCancellationRequest(request.PolicyNumber);
    }

    [Fact]
    public async Task GetCancellationStatus_ValidId_ReturnsStatus()
    {
        // Arrange
        var cancellationId = "CAN123";
        SetupStatusRetrieval(cancellationId, CancellationStatus.PendingApproval);

        // Act
        var status = await _service.GetCancellationStatus(cancellationId);

        // Assert
        Assert.Equal(CancellationStatus.PendingApproval, status);
        VerifyStatusRetrieval(cancellationId);
    }

    [Fact]
    public async Task CalculateRefund_ValidRequest_ReturnsCalculation()
    {
        // Arrange
        var policyNumber = "POL123";
        var request = new CancellationCalculationRequest
        {
            CancellationDate = DateTime.Today.AddDays(30),
            CancellationType = "Insured_Request",
            CalculationMethod = "ProRata"
        };

        SetupRefundCalculation(policyNumber, 500.00m);

        // Act
        var calculation = await _service.CalculateRefund(policyNumber, request);

        // Assert
        Assert.NotNull(calculation);
        Assert.Equal(500.00m, calculation.RefundAmount);
        VerifyRefundCalculation(policyNumber);
    }

    [Fact]
    public async Task RescindCancellation_ValidRequest_ReturnsCancellationResponse()
    {
        // Arrange
        var cancellationId = "CAN123";
        var request = new RescindRequest
        {
            Reason = "Customer changed mind",
            RequestedBy = "Test User"
        };

        SetupRescindResponse(cancellationId, CancellationStatus.Rescinded);

        // Act
        var response = await _service.RescindCancellation(cancellationId, request);

        // Assert
        Assert.NotNull(response);
        Assert.Equal(CancellationStatus.Rescinded, response.Status);
        VerifyRescindRequest(cancellationId);
    }

    // Helper methods
    private CancellationRequest CreateSampleCancellationRequest()
    {
        return new CancellationRequest
        {
            PolicyNumber = "POL123",
            CancellationType = "Insured_Request",
            CancellationDate = DateTime.Today.AddDays(30),
            Reason = "Moving to different state",
            RequestedBy = "Test User"
        };
    }

    private void SetupCancellationResponse(string cancellationId, string policyNumber)
    {
        var responseXml = CreateSampleCancellationResponseXml(cancellationId, policyNumber);
        _imsClientMock
            .Setup(x => x.SendRequestAsync<ExecuteCommandRequest, ExecuteCommandResponse>(
                It.IsAny<string>(),
                "InitiateCancellation",
                It.IsAny<ExecuteCommandRequest>()))
            .ReturnsAsync(new ExecuteCommandResponse { Result = responseXml });
    }

    private void SetupStatusRetrieval(string cancellationId, CancellationStatus status)
    {
        _imsClientMock
            .Setup(x => x.SendRequestAsync<ExecuteCommandRequest, ExecuteCommandResponse>(
                It.IsAny<string>(),
                "GetCancellationStatus",
                It.Is<ExecuteCommandRequest>(r => 
                    r.Parameters.Contains(cancellationId))))
            .ReturnsAsync(new ExecuteCommandResponse { Result = status.ToString() });
    }

    private void SetupRefundCalculation(string policyNumber, decimal refundAmount)
    {
        var calculationXml = CreateSampleCalculationResponseXml(refundAmount);
        _imsClientMock
            .Setup(x => x.SendRequestAsync<ExecuteCommandRequest, ExecuteCommandResponse>(
                It.IsAny<string>(),
                "CalculateRefund",
                It.Is<ExecuteCommandRequest>(r => 
                    r.Parameters.Contains(policyNumber))))
            .ReturnsAsync(new ExecuteCommandResponse { Result = calculationXml });
    }

    private void SetupRescindResponse(string cancellationId, CancellationStatus status)
    {
        var responseXml = CreateSampleRescindResponseXml(cancellationId, status);
        _imsClientMock
            .Setup(x => x.SendRequestAsync<ExecuteCommandRequest, ExecuteCommandResponse>(
                It.IsAny<string>(),
                "RescindCancellation",
                It.Is<ExecuteCommandRequest>(r => 
                    r.Parameters.Contains(cancellationId))))
            .ReturnsAsync(new ExecuteCommandResponse { Result = responseXml });
    }

    private void VerifyCancellationRequest(string policyNumber)
    {
        _imsClientMock.Verify(
            x => x.SendRequestAsync<ExecuteCommandRequest, ExecuteCommandResponse>(
                It.IsAny<string>(),
                "InitiateCancellation",
                It.Is<ExecuteCommandRequest>(r => 
                    r.Parameters.Contains(policyNumber))),
            Times.Once);
    }

    private void VerifyStatusRetrieval(string cancellationId)
    {
        _imsClientMock.Verify(
            x => x.SendRequestAsync<ExecuteCommandRequest, ExecuteCommandResponse>(
                It.IsAny<string>(),
                "GetCancellationStatus",
                It.Is<ExecuteCommandRequest>(r => 
                    r.Parameters.Contains(cancellationId))),
            Times.Once);
    }

    private void VerifyRefundCalculation(string policyNumber)
    {
        _imsClientMock.Verify(
            x => x.SendRequestAsync<ExecuteCommandRequest, ExecuteCommandResponse>(
                It.IsAny<string>(),
                "CalculateRefund",
                It.Is<ExecuteCommandRequest>(r => 
                    r.Parameters.Contains(policyNumber))),
            Times.Once);
    }

    private void VerifyRescindRequest(string cancellationId)
    {
        _imsClientMock.Verify(
            x => x.SendRequestAsync<ExecuteCommandRequest, ExecuteCommandResponse>(
                It.IsAny<string>(),
                "RescindCancellation",
                It.Is<ExecuteCommandRequest>(r => 
                    r.Parameters.Contains(cancellationId))),
            Times.Once);
    }

    private string CreateSampleCancellationResponseXml(
        string cancellationId, 
        string policyNumber)
    {
        return $@"
            <CancellationResponse>
                <CancellationId>{cancellationId}</CancellationId>
                <PolicyNumber>{policyNumber}</PolicyNumber>
                <Status>Draft</Status>
                <ProcessedDate>{DateTime.Now:yyyy-MM-dd}</ProcessedDate>
            </CancellationResponse>";
    }

    private string CreateSampleCalculationResponseXml(decimal refundAmount)
    {
        return $@"
            <CancellationCalculation>
                <EarnedPremium>1000.00</EarnedPremium>
                <UnearnedPremium>{refundAmount}</UnearnedPremium>
                <RefundAmount>{refundAmount}</RefundAmount>
                <CancellationFee>0.00</CancellationFee>
                <CalculationMethod>ProRata</CalculationMethod>
            </CancellationCalculation>";
    }

    private string CreateSampleRescindResponseXml(
        string cancellationId, 
        CancellationStatus status)
    {
        return $@"
            <CancellationResponse>
                <CancellationId>{cancellationId}</CancellationId>
                <Status>{status}</Status>
                <ProcessedDate>{DateTime.Now:yyyy-MM-dd}</ProcessedDate>
            </CancellationResponse>";
    }
} 