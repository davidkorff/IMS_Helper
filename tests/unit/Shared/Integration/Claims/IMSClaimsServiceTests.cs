using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Moq;
using Xunit;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Caching.Memory;
using Shared.Integration.Claims;

public class IMSClaimsServiceTests
{
    private readonly IMSClaimsService _service;
    private readonly Mock<IIMSClient> _imsClientMock;
    private readonly Mock<ILogger<IMSClaimsService>> _loggerMock;
    private readonly Mock<IMemoryCache> _cacheMock;
    private readonly IMSClaimsSettings _settings;

    public IMSClaimsServiceTests()
    {
        _imsClientMock = new Mock<IIMSClient>();
        _loggerMock = new Mock<ILogger<IMSClaimsService>>();
        _cacheMock = new Mock<IMemoryCache>();
        
        _settings = new IMSClaimsSettings
        {
            ValidationSettings = new ClaimValidationSettings
            {
                RequireInitialDocuments = false,
                RequireAdjusterAssignment = true
            }
        };

        _service = new IMSClaimsService(
            _imsClientMock.Object,
            _loggerMock.Object,
            _cacheMock.Object,
            Options.Create(_settings));
    }

    [Fact]
    public async Task CreateClaim_ValidRequest_ReturnsClaimId()
    {
        // Arrange
        var request = CreateSampleClaimRequest();
        SetupClaimCreation("CLAIM123");

        // Act
        var claimId = await _service.CreateClaim(request);

        // Assert
        Assert.Equal("CLAIM123", claimId);
        VerifyClaimCreation(request.PolicyNumber);
    }

    [Fact]
    public async Task GetClaimDetails_ValidId_ReturnsDetails()
    {
        // Arrange
        var claimId = "CLAIM123";
        var detailsXml = CreateSampleClaimDetailsXml(claimId);
        SetupClaimDetailsRetrieval(claimId, detailsXml);

        // Act
        var details = await _service.GetClaimDetails(claimId);

        // Assert
        Assert.NotNull(details);
        Assert.Equal(claimId, details.ClaimId);
        Assert.NotNull(details.PolicyNumber);
        Assert.NotNull(details.InsuredName);
    }

    [Fact]
    public async Task ProcessClaimPayment_ValidRequest_ReturnsPaymentResponse()
    {
        // Arrange
        var claimId = "CLAIM123";
        var request = new PaymentRequest
        {
            Amount = 1000,
            PaymentType = "Repair",
            PayeeName = "John Doe"
        };

        SetupPaymentProcessing(claimId, "PAY123");

        // Act
        var response = await _service.ProcessClaimPayment(claimId, request);

        // Assert
        Assert.NotNull(response);
        Assert.Equal("PAY123", response.PaymentId);
        VerifyPaymentProcessing(claimId, request.Amount);
    }

    [Fact]
    public async Task UploadClaimDocument_ValidDocument_ReturnsDocumentId()
    {
        // Arrange
        var claimId = "CLAIM123";
        var document = new ClaimDocumentUpload
        {
            DocumentType = "Invoice",
            FileName = "invoice.pdf",
            Content = new byte[] { 1, 2, 3 }
        };

        SetupDocumentUpload(claimId, "DOC123");

        // Act
        var documentId = await _service.UploadClaimDocument(claimId, document);

        // Assert
        Assert.Equal("DOC123", documentId);
        VerifyDocumentUpload(claimId);
    }

    [Fact]
    public async Task AddClaimNote_ValidNote_ReturnsNoteId()
    {
        // Arrange
        var claimId = "CLAIM123";
        var note = new ClaimNoteRequest
        {
            Content = "Test note",
            CreatedBy = "Test User"
        };

        SetupNoteAddition(claimId, "NOTE123");

        // Act
        var noteId = await _service.AddClaimNote(claimId, note);

        // Assert
        Assert.Equal("NOTE123", noteId);
        VerifyNoteAddition(claimId);
    }

    // Helper methods
    private ClaimCreationRequest CreateSampleClaimRequest()
    {
        return new ClaimCreationRequest
        {
            PolicyNumber = "POL123",
            DateOfLoss = DateTime.Today.AddDays(-1),
            LossDescription = "Water damage",
            ReportedBy = "John Doe",
            ReportedDate = DateTime.Today,
            LossType = "Property",
            CauseOfLoss = "Water",
            LossLocation = new Address
            {
                Street1 = "123 Main St",
                City = "Anytown",
                State = "FL",
                ZipCode = "12345"
            }
        };
    }

    private void SetupClaimCreation(string claimId)
    {
        var responseXml = $@"<ClaimResponse><ClaimId>{claimId}</ClaimId></ClaimResponse>";
        _imsClientMock
            .Setup(x => x.SendRequestAsync<ExecuteCommandRequest, ExecuteCommandResponse>(
                It.IsAny<string>(),
                "CreateClaim",
                It.IsAny<ExecuteCommandRequest>()))
            .ReturnsAsync(new ExecuteCommandResponse { Result = responseXml });
    }

    private void SetupClaimDetailsRetrieval(string claimId, string detailsXml)
    {
        _imsClientMock
            .Setup(x => x.SendRequestAsync<ExecuteCommandRequest, ExecuteCommandResponse>(
                It.IsAny<string>(),
                "GetClaimDetails",
                It.Is<ExecuteCommandRequest>(r => r.Parameters.Contains(claimId))))
            .ReturnsAsync(new ExecuteCommandResponse { Result = detailsXml });
    }

    private void SetupPaymentProcessing(string claimId, string paymentId)
    {
        var responseXml = CreateSamplePaymentResponseXml(paymentId);
        _imsClientMock
            .Setup(x => x.SendRequestAsync<ExecuteCommandRequest, ExecuteCommandResponse>(
                It.IsAny<string>(),
                "ProcessPayment",
                It.Is<ExecuteCommandRequest>(r => r.Parameters.Contains(claimId))))
            .ReturnsAsync(new ExecuteCommandResponse { Result = responseXml });
    }

    private void SetupDocumentUpload(string claimId, string documentId)
    {
        _imsClientMock
            .Setup(x => x.SendRequestAsync<ExecuteCommandRequest, ExecuteCommandResponse>(
                It.IsAny<string>(),
                "UploadDocument",
                It.Is<ExecuteCommandRequest>(r => r.Parameters.Contains(claimId))))
            .ReturnsAsync(new ExecuteCommandResponse { Result = documentId });
    }

    private void SetupNoteAddition(string claimId, string noteId)
    {
        _imsClientMock
            .Setup(x => x.SendRequestAsync<ExecuteCommandRequest, ExecuteCommandResponse>(
                It.IsAny<string>(),
                "AddNote",
                It.Is<ExecuteCommandRequest>(r => r.Parameters.Contains(claimId))))
            .ReturnsAsync(new ExecuteCommandResponse { Result = noteId });
    }

    private void VerifyClaimCreation(string policyNumber)
    {
        _imsClientMock.Verify(
            x => x.SendRequestAsync<ExecuteCommandRequest, ExecuteCommandResponse>(
                It.IsAny<string>(),
                "CreateClaim",
                It.Is<ExecuteCommandRequest>(r => r.Parameters.Contains(policyNumber))),
            Times.Once);
    }

    private void VerifyPaymentProcessing(string claimId, decimal amount)
    {
        _imsClientMock.Verify(
            x => x.SendRequestAsync<ExecuteCommandRequest, ExecuteCommandResponse>(
                It.IsAny<string>(),
                "ProcessPayment",
                It.Is<ExecuteCommandRequest>(r => 
                    r.Parameters.Contains(claimId) && 
                    r.Parameters.Contains(amount.ToString()))),
            Times.Once);
    }

    private void VerifyDocumentUpload(string claimId)
    {
        _imsClientMock.Verify(
            x => x.SendRequestAsync<ExecuteCommandRequest, ExecuteCommandResponse>(
                It.IsAny<string>(),
                "UploadDocument",
                It.Is<ExecuteCommandRequest>(r => r.Parameters.Contains(claimId))),
            Times.Once);
    }

    private void VerifyNoteAddition(string claimId)
    {
        _imsClientMock.Verify(
            x => x.SendRequestAsync<ExecuteCommandRequest, ExecuteCommandResponse>(
                It.IsAny<string>(),
                "AddNote",
                It.Is<ExecuteCommandRequest>(r => r.Parameters.Contains(claimId))),
            Times.Once);
    }

    private string CreateSampleClaimDetailsXml(string claimId)
    {
        return $@"
            <ClaimDetails>
                <ClaimId>{claimId}</ClaimId>
                <PolicyNumber>POL123</PolicyNumber>
                <InsuredName>John Doe</InsuredName>
                <DateOfLoss>2024-01-01</DateOfLoss>
                <Status>New</Status>
            </ClaimDetails>";
    }

    private string CreateSamplePaymentResponseXml(string paymentId)
    {
        return $@"
            <PaymentResponse>
                <PaymentId>{paymentId}</PaymentId>
                <Status>Processed</Status>
                <ProcessedDate>2024-01-01</ProcessedDate>
                <Amount>1000.00</Amount>
            </PaymentResponse>";
    }
} 