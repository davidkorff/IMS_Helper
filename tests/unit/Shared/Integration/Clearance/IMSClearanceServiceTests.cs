using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Moq;
using Xunit;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Caching.Memory;
using Shared.Integration.Clearance;

public class IMSClearanceServiceTests
{
    private readonly IMSClearanceService _service;
    private readonly Mock<IIMSClient> _imsClientMock;
    private readonly Mock<ILogger<IMSClearanceService>> _loggerMock;
    private readonly Mock<IMemoryCache> _cacheMock;
    private readonly IMSClearanceSettings _settings;

    public IMSClearanceServiceTests()
    {
        _imsClientMock = new Mock<IIMSClient>();
        _loggerMock = new Mock<ILogger<IMSClearanceService>>();
        _cacheMock = new Mock<IMemoryCache>();
        
        _settings = new IMSClearanceSettings
        {
            EngineSettings = new ClearanceEngineSettings
            {
                EnableParallelProcessing = true,
                MaxConcurrentClearances = 5
            }
        };

        _service = new IMSClearanceService(
            _imsClientMock.Object,
            _loggerMock.Object,
            _cacheMock.Object,
            Options.Create(_settings));
    }

    [Fact]
    public async Task CheckClearance_ValidRequest_ReturnsClearanceResponse()
    {
        // Arrange
        var request = CreateSampleClearanceRequest();
        SetupClearanceResponse("CLR123", ClearanceStatus.Cleared);

        // Act
        var response = await _service.CheckClearance(request);

        // Assert
        Assert.NotNull(response);
        Assert.Equal("CLR123", response.ClearanceId);
        Assert.Equal(ClearanceStatus.Cleared, response.Status);
        VerifyClearanceCheck(request.EntityId);
    }

    [Fact]
    public async Task GetClearanceStatus_ValidId_ReturnsClearanceStatus()
    {
        // Arrange
        var clearanceId = "CLR123";
        SetupStatusResponse(clearanceId, ClearanceStatus.InProgress);

        // Act
        var response = await _service.GetClearanceStatus(clearanceId);

        // Assert
        Assert.NotNull(response);
        Assert.Equal(ClearanceStatus.InProgress, response.Status);
        VerifyStatusRetrieval(clearanceId);
    }

    [Fact]
    public async Task UpdateClearance_ValidRequest_ReturnsClearanceResponse()
    {
        // Arrange
        var clearanceId = "CLR123";
        var request = CreateSampleUpdateRequest();
        SetupUpdateResponse(clearanceId, ClearanceStatus.Override);

        // Act
        var response = await _service.UpdateClearance(clearanceId, request);

        // Assert
        Assert.NotNull(response);
        Assert.Equal(ClearanceStatus.Override, response.Status);
        VerifyClearanceUpdate(clearanceId);
    }

    private ClearanceRequest CreateSampleClearanceRequest()
    {
        return new ClearanceRequest
        {
            EntityType = "Policy",
            EntityId = "POL123",
            LineOfBusiness = "Commercial",
            State = "CA",
            EffectiveDate = DateTime.Today,
            Amount = 5000,
            RequestedBy = "Test User"
        };
    }

    private ClearanceUpdateRequest CreateSampleUpdateRequest()
    {
        return new ClearanceUpdateRequest
        {
            Overrides = new List<BlockerOverride>
            {
                new BlockerOverride
                {
                    BlockerId = "BLK001",
                    Reason = "Approved by underwriter",
                    ApprovedBy = "Test Approver",
                    ApprovalDate = DateTime.Today
                }
            },
            Comments = "Override approved",
            UpdatedBy = "Test User"
        };
    }

    private void SetupClearanceResponse(string clearanceId, ClearanceStatus status)
    {
        var responseXml = CreateSampleClearanceResponseXml(clearanceId, status);
        _imsClientMock
            .Setup(x => x.SendRequestAsync<ExecuteCommandRequest, ExecuteCommandResponse>(
                It.IsAny<string>(),
                "CheckClearance",
                It.IsAny<ExecuteCommandRequest>()))
            .ReturnsAsync(new ExecuteCommandResponse { Result = responseXml });
    }

    private void SetupStatusResponse(string clearanceId, ClearanceStatus status)
    {
        var responseXml = CreateSampleClearanceResponseXml(clearanceId, status);
        _imsClientMock
            .Setup(x => x.SendRequestAsync<ExecuteCommandRequest, ExecuteCommandResponse>(
                It.IsAny<string>(),
                "GetClearanceStatus",
                It.Is<ExecuteCommandRequest>(r => 
                    r.Parameters.Contains(clearanceId))))
            .ReturnsAsync(new ExecuteCommandResponse { Result = responseXml });
    }

    private void SetupUpdateResponse(string clearanceId, ClearanceStatus status)
    {
        var responseXml = CreateSampleClearanceResponseXml(clearanceId, status);
        _imsClientMock
            .Setup(x => x.SendRequestAsync<ExecuteCommandRequest, ExecuteCommandResponse>(
                It.IsAny<string>(),
                "UpdateClearance",
                It.Is<ExecuteCommandRequest>(r => 
                    r.Parameters.Contains(clearanceId))))
            .ReturnsAsync(new ExecuteCommandResponse { Result = responseXml });
    }

    private void VerifyClearanceCheck(string entityId)
    {
        _imsClientMock.Verify(
            x => x.SendRequestAsync<ExecuteCommandRequest, ExecuteCommandResponse>(
                It.IsAny<string>(),
                "CheckClearance",
                It.Is<ExecuteCommandRequest>(r => 
                    r.Parameters.Contains(entityId))),
            Times.Once);
    }

    private void VerifyStatusRetrieval(string clearanceId)
    {
        _imsClientMock.Verify(
            x => x.SendRequestAsync<ExecuteCommandRequest, ExecuteCommandResponse>(
                It.IsAny<string>(),
                "GetClearanceStatus",
                It.Is<ExecuteCommandRequest>(r => 
                    r.Parameters.Contains(clearanceId))),
            Times.Once);
    }

    private void VerifyClearanceUpdate(string clearanceId)
    {
        _imsClientMock.Verify(
            x => x.SendRequestAsync<ExecuteCommandRequest, ExecuteCommandResponse>(
                It.IsAny<string>(),
                "UpdateClearance",
                It.Is<ExecuteCommandRequest>(r => 
                    r.Parameters.Contains(clearanceId))),
            Times.Once);
    }

    private string CreateSampleClearanceResponseXml(
        string clearanceId, 
        ClearanceStatus status)
    {
        return $@"
            <ClearanceResponse>
                <ClearanceId>{clearanceId}</ClearanceId>
                <EntityId>POL123</EntityId>
                <EntityType>Policy</EntityType>
                <Status>{status}</Status>
                <RequestedDate>{DateTime.Now:yyyy-MM-dd}</RequestedDate>
                <RequestedBy>Test User</RequestedBy>
                <ProcessedDate>{DateTime.Now:yyyy-MM-dd}</ProcessedDate>
                <ProcessedBy>System</ProcessedBy>
            </ClearanceResponse>";
    }
} 